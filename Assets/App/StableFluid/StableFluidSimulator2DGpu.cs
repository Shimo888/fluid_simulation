using System;
using MySimulator;
using UnityEngine;
using Random = UnityEngine.Random;

namespace App.StableFluid
{
    [Serializable]
    public class StableFluidSimulator2DGpu : GridSimulator2D
    {
        public enum ValueType 
        {
            Scalar,
            VectorX,
            VectorY
        }
        
        [SerializeField] 
        private ComputeShader _computeShader;
        
        [SerializeField] 
        private StableFluidParameterAsset2D _parameterAsset;

        private RenderTexture _density;
        private RenderTexture _prevDensity;
        private RenderTexture _velocityX;
        private RenderTexture _prevVelocityX;
        private RenderTexture _velocityY;
        private RenderTexture _prevVelocityY;
        private RenderTexture _tmpBuffer; // ヤコビ法などのping-pongなどで一時的に使用するバッファ

        private int _updateUserInteraction;
        private int _addSourceOrForceKernel;
        private int _diffuseKernel;
        private int _advectKernel;
        private int _projectCalcDivVelKernel;
        private int _projectCalcPoissonKernel;
        private int _projectCalcDiv0Kernel;
        private int _setBoundaryKernel;
        private int _resetKernel;
        private int _renderKernel;

        private Vector2Int _groupThreadSize; 
            
        private static readonly int Dt = Shader.PropertyToID("Dt");
        private static readonly int Diffusion = Shader.PropertyToID("Diffusion");
        private static readonly int GridSize = Shader.PropertyToID("GridSize");
        
        private static readonly int InputFloat = Shader.PropertyToID("InputFloat");
        private static readonly int InputFloat2 = Shader.PropertyToID("InputFloat2");
        private static readonly int InputFloat3 = Shader.PropertyToID("InputFloat3");
        private static readonly int OutputFloat = Shader.PropertyToID("OutputFloat");
        private static readonly int OutputFloat2 = Shader.PropertyToID("OutputFloat2");
        private static readonly int OutputFloat3 = Shader.PropertyToID("OutputFloat3");
        private static readonly int RenderTextureBuf = Shader.PropertyToID("RenderTexture");
        private static readonly int ValueType1 = Shader.PropertyToID("ValueType");

        protected override void InternalSetUp()
        {
            GridParameterAsset2D = _parameterAsset;
            var gridParameter = _parameterAsset.GridParameter;
            
            // カーネルの取得
            _updateUserInteraction = _computeShader.FindKernel("UpdateUserInteraction");
            _addSourceOrForceKernel = _computeShader.FindKernel("AddSourceOrForce");
            _diffuseKernel = _computeShader.FindKernel("Diffuse");
            _advectKernel = _computeShader.FindKernel("Advect");
            _projectCalcDivVelKernel = _computeShader.FindKernel("Project_CalcDivVel");
            _projectCalcPoissonKernel = _computeShader.FindKernel("Project_CalcPoissonP");
            _projectCalcDiv0Kernel = _computeShader.FindKernel("Project_CalcDiv0");
            _setBoundaryKernel = _computeShader.FindKernel("SetBoundary");
            _resetKernel = _computeShader.FindKernel("Reset");
            
            // パラメータの設定(格子数はシミュレーションは普遍である制限をおく)
            _computeShader.SetInts(GridSize, gridParameter.XMax, gridParameter.YMax); // Boundaryを含めたサイズ(N+2)
            
            // RenderTextureのSetUp
            // Texture2DだとGPUから書き込めないらしいのでRenderTextureを使用
            // floatだけなのでRFloatを使用する
            // ARGBFloatとか使ってメモリを無駄にしたくないから
            // ちなみに流速をfloat2ではなくfloatにして複数のテクスチャにしたのはCSのカーネルを使い回すため
            _density = InitializeRenderTexture();
            _prevDensity = InitializeRenderTexture();
            _velocityX = InitializeRenderTexture();
            _prevVelocityX = InitializeRenderTexture();
            _velocityY = InitializeRenderTexture();
            _prevVelocityY = InitializeRenderTexture(); 
            _tmpBuffer = InitializeRenderTexture();
            
            // Group Thread Size
            // [numthreads(8,8,1)]の情報はC#側から取得できなさそうなのでハードコード
            _groupThreadSize = new (Mathf.FloorToInt(gridParameter.XMax/8f), Mathf.FloorToInt(gridParameter.YMax/8f));
            
            // 初期条件の適用
            SetInitialCondition();
        }

        protected override void InternalUpdate(float dt)
        {
            UpdateParameter(dt);
            UpdateUserInteraction();
            UpdateVelocity();
            UpdateDensity();
        }

        protected override void InternalSetUpRenderer(RenderTexture texture)
        {
            _renderKernel = _computeShader.FindKernel("Render");
        }

        protected override void InternalRender(RenderTexture texture)
        {
            _computeShader.SetTexture(_renderKernel, InputFloat, _density);
            _computeShader.SetTexture(_renderKernel, RenderTextureBuf, texture);
            _computeShader.Dispatch(_renderKernel, _groupThreadSize.x, _groupThreadSize.y, 1);
        }

        private RenderTexture InitializeRenderTexture()
        {
            var gridParameter = _parameterAsset.GridParameter;
            var texture = new RenderTexture(gridParameter.XMax, gridParameter.YMax, 0, RenderTextureFormat.RFloat) 
            {
                enableRandomWrite = true
            };
            texture.Create();
            return texture;
        }
        
        /// <summary>
        /// 初期条件の設定
        /// </summary>
        private void SetInitialCondition()
        {
            var gridParameter = _parameterAsset.GridParameter;
            
            // ComputeShaderで計算するほどでもないので、Texture2D側で初期条件を設定する
            // Density
            var texture = new Texture2D(gridParameter.XMax, gridParameter.YMax, TextureFormat.RFloat, false);
            for (int j = 0; j < gridParameter.YMax; j++)
            {
                for (int i = 0; i < gridParameter.XMax; i++)
                {
                    // 密度の初期条件
                    // 中心に向かってガウス分布
                    float x = (i - gridParameter.XMax / 2f) / (gridParameter.XMax / 2f);
                    float y = (j - gridParameter.YMax / 2f) / (gridParameter.YMax / 2f);
                    float dens = Mathf.Exp(-(x * x + y * y) * 10f);
                    texture.SetPixel(i, j, new Color(dens, 0, 0, 0));
                }
            }
            texture.Apply();
            Graphics.Blit(texture, _density);
            
            var texture2 = new Texture2D(gridParameter.XMax, gridParameter.YMax, TextureFormat.RFloat, false);
            // Velocity
            for (int j = 0; j < gridParameter.YMax; j++)
            {
                for (int i = 0; i < gridParameter.XMax; i++)
                {
                    var velX = Random.value - 0.5f;
                    var velY = Random.value - 0.5f; 
                    texture.SetPixel(i, j, new Color(velX, 0, 0, 0));
                    texture2.SetPixel(i, j, new Color(velY, 0, 0, 0));
                }
            }
            
            texture.Apply();
            texture2.Apply();
            Graphics.Blit(texture, _velocityX);
            Graphics.Blit(texture2, _velocityY);
        }

        /// <summary>
        /// 基本パラメータの更新(dt, 拡散係数, 粘性率)
        /// </summary>
        /// <param name="dt"></param>
        private void UpdateParameter(float dt)
        {
            _computeShader.SetFloat(Dt, dt);
        }

        /// <summary>
        /// ユーザーインタラクションによる外力やソースの追加
        /// prevDensity, prevVelocityX, prevVelocityYに追加する
        /// </summary>
        private void UpdateUserInteraction()
        {
            _computeShader.SetTexture(_updateUserInteraction, OutputFloat,  _prevDensity);
            _computeShader.SetTexture(_updateUserInteraction, OutputFloat2, _prevVelocityX);
            _computeShader.SetTexture(_updateUserInteraction, OutputFloat3, _prevVelocityY);
            _computeShader.Dispatch(_updateUserInteraction, _groupThreadSize.x, _groupThreadSize.y, 1);
        }

        /// <summary>
        /// 密度の更新
        /// </summary>
        private void UpdateDensity()
        {
            // 力の追加
            AddSourceOrForce( _density, _prevDensity);
            (_prevDensity, _density) = (_density, _prevDensity);
            
            // 拡散 (ヤコビ法)
            Diffuse(_density, _prevDensity, _parameterAsset.DensDiffusion, ValueType.Scalar);
            (_prevDensity, _density) = (_density, _prevDensity);
            
            // 移流
            Advect(_density, _prevDensity, _velocityX, _velocityY, ValueType.Scalar);
        }

        /// <summary>
        /// 流速の更新
        /// </summary>
        private void UpdateVelocity()
        {
            // 外力の追加 
            AddSourceOrForce(_velocityX, _prevVelocityX);
            AddSourceOrForce(_velocityY, _prevVelocityY);
            (_prevVelocityX, _velocityX) = (_velocityX, _prevVelocityX);
            (_prevVelocityY, _velocityY) = (_velocityY, _prevVelocityY);
            
            // 拡散(粘性項)
            Diffuse(_velocityX, _prevVelocityX, _parameterAsset.Viscosity, ValueType.VectorX);
            Diffuse(_velocityY, _prevVelocityY, _parameterAsset.Viscosity, ValueType.VectorY);
            
            // 射影(1回目)
            // prevVelocityはあくまで計算用のバッファで意味はない
            Project(_velocityX, _velocityY, _prevVelocityX, _prevVelocityY);
            (_prevVelocityX, _velocityX) = (_velocityX, _prevVelocityX);
            (_prevVelocityY, _velocityY) = (_velocityY, _prevVelocityY);
            
            // 移流
            Advect(_velocityX, _prevVelocityX, _prevVelocityX, _prevVelocityY, ValueType.VectorX);
            Advect(_velocityY, _prevVelocityY, _prevVelocityX, _prevVelocityY, ValueType.VectorY);
            
            // 射影(2回目)
            Project(_velocityX, _velocityY, _prevVelocityX, _prevVelocityY);
        }

        /// <summary>
        /// 力やソースの追加
        /// </summary>
        /// <param name="nextVal"></param>
        /// <param name="prevVal"></param>
        private void AddSourceOrForce(RenderTexture nextVal, RenderTexture prevVal)
        {
            _computeShader.SetTexture(_addSourceOrForceKernel, InputFloat, prevVal);
            _computeShader.SetTexture(_addSourceOrForceKernel, OutputFloat, nextVal);
            _computeShader.Dispatch(_addSourceOrForceKernel, _groupThreadSize.x, _groupThreadSize.y, 1);
        }

        /// <summary>
        /// ヤコビ法を用いた拡散法の計算
        /// Ping-Pongなんちゃらってやつ
        /// </summary>
        /// <param name="nextVal"></param>
        /// <param name="prevVal"></param>
        /// <param name="diffusion"></param>
        /// <param name="valueType"></param>
        private void Diffuse(RenderTexture nextVal, RenderTexture prevVal, float diffusion, ValueType valueType)
        {
            // nextValの初期値の影響を小さくするために、値をReset
            _computeShader.SetTexture(_resetKernel, OutputFloat, nextVal);
            _computeShader.Dispatch(_resetKernel, _groupThreadSize.x, _groupThreadSize.y, 1);
            
            // 拡散計算
            _computeShader.SetFloat(Diffusion, diffusion);
            _computeShader.SetTexture(_diffuseKernel, InputFloat, prevVal);
            // 並列計算で計算途中の値が使われるのを避けるために一時用バッファーを使用する
            Graphics.CopyTexture(nextVal, _tmpBuffer); 
            for (int i = 0; i < 40; i++)
            {
                (nextVal, _tmpBuffer) = (_tmpBuffer, nextVal);
                _computeShader.SetTexture(_diffuseKernel, InputFloat2, _tmpBuffer);
                _computeShader.SetTexture(_diffuseKernel, OutputFloat, nextVal);
                _computeShader.Dispatch(_diffuseKernel, _groupThreadSize.x, _groupThreadSize.y, 1);
                
                SetBoundary(valueType, nextVal);
            }
        }

        /// <summary>
        /// 移流項
        /// </summary>
        private void Advect(RenderTexture nextVal, RenderTexture prevVal, RenderTexture velX, RenderTexture velY, ValueType valueType)
        {
            _computeShader.SetTexture(_advectKernel, InputFloat, prevVal);
            _computeShader.SetTexture(_advectKernel, InputFloat2, velX);
            _computeShader.SetTexture(_advectKernel, InputFloat3, velY);
            _computeShader.SetTexture(_advectKernel, OutputFloat, nextVal);
            _computeShader.Dispatch(_advectKernel, _groupThreadSize.x, _groupThreadSize.y, 1);
            
            SetBoundary(valueType, nextVal);
        }

        /// <summary>
        /// 射影
        /// </summary>
        private void Project(RenderTexture velX, RenderTexture velY, RenderTexture pBuffer, RenderTexture divVelBuffer)
        {
            // 速度の発散の計算
            // div Velをtmp buffer に格納する
            // あと、p=0に初期化しておく
            _computeShader.SetTexture(_projectCalcDivVelKernel, InputFloat2, velX);
            _computeShader.SetTexture(_projectCalcDivVelKernel, InputFloat3, velY);
            _computeShader.SetTexture(_projectCalcDivVelKernel, OutputFloat, divVelBuffer);
            _computeShader.Dispatch(_projectCalcDivVelKernel, _groupThreadSize.x, _groupThreadSize.y, 1);
            SetBoundary(ValueType.Scalar, divVelBuffer);
            
            // 初期値の影響を小さくするためにpをリセット
            _computeShader.SetTexture(_resetKernel, OutputFloat, pBuffer);
            _computeShader.Dispatch(_resetKernel, _groupThreadSize.x, _groupThreadSize.y, 1);
            
            // pの計算
            // ヤコビ法用いてポアソン方程式を解く
            _computeShader.SetTexture(_projectCalcPoissonKernel, InputFloat, divVelBuffer);
            Graphics.CopyTexture(pBuffer, _tmpBuffer); // ヤコビ法の初期値設定
            for (int i = 0; i < 40; i++)
            {
                (pBuffer, _tmpBuffer) = (_tmpBuffer, pBuffer);
                _computeShader.SetTexture(_projectCalcPoissonKernel, InputFloat2, _tmpBuffer);
                _computeShader.SetTexture(_projectCalcPoissonKernel, OutputFloat, pBuffer);
                _computeShader.Dispatch(_projectCalcPoissonKernel, _groupThreadSize.x, _groupThreadSize.y, 1);
                SetBoundary(ValueType.Scalar, pBuffer);
            }
            
            // 流速のdiv=0の成分を求める(grad pを差し引く)
            _computeShader.SetTexture(_projectCalcDiv0Kernel, InputFloat, pBuffer);
            _computeShader.SetTexture(_projectCalcDiv0Kernel, OutputFloat, velX);
            _computeShader.SetTexture(_projectCalcDiv0Kernel, OutputFloat2, velY);
            _computeShader.Dispatch(_projectCalcDiv0Kernel, _groupThreadSize.x, _groupThreadSize.y, 1);
            SetBoundary(ValueType.VectorX, velX);
            SetBoundary(ValueType.VectorY, velY);
        }

        private void SetBoundary(ValueType valueType, RenderTexture val)
        {
            _computeShader.SetInt(ValueType1, (int)valueType);
            _computeShader.SetTexture(_setBoundaryKernel, OutputFloat, val);
            _computeShader.Dispatch(_setBoundaryKernel, _groupThreadSize.x, _groupThreadSize.y, 1);
        }
    }
}