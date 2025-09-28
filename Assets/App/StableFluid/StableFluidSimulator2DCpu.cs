using System;
using MySimulator;
using UnityEngine;

namespace App.StableFluid
{
    [Serializable]
    public class StableFluidSimulator2DCpu : GridSimulator2D
    {
        public enum ValueType 
        {
            Scalar,
            VectorX,
            VectorY
        }
        
        [SerializeField]
        private StableFluidParameterAsset2D _stableFluidParameterAsset2D;

        // シミュレーション用配列
        private float[,] _density;
        private float[,] _prevDensity;
        private float[,] _velocityX;
        private float[,] _prevVelocityX;
        private float[,] _velocityY;
        private float[,] _prevVelocityY;
        
        // レンダリング用バッファ
        private Texture2D _texture2dToRender;
        
        protected override void InternalSetUp()
        {
            GridParameterAsset2D = _stableFluidParameterAsset2D;

            var gridParam = GridParameterAsset2D.GridParameter;
            _density = new float[gridParam.XMax,gridParam.YMax];
            _prevDensity = new float[gridParam.XMax,gridParam.YMax];
            _velocityX = new float[gridParam.XMax,gridParam.YMax];
            _prevVelocityX = new float[gridParam.XMax,gridParam.YMax];
            _velocityY = new float[gridParam.XMax,gridParam.YMax];
            _prevVelocityY = new float[gridParam.XMax,gridParam.YMax];
            
            // 初期値
            for (var x = 0; x < gridParam.XMax; x++)
            {
                for (var y = 0; y < gridParam.YMax; y++)
                {
                    // densityは中心が高くなるように
                    var cx = x - gridParam.XMax / 2;
                    var cy = y - gridParam.YMax / 2;
                    _density[x, y] = Mathf.Exp(-(cx * cx + cy * cy) / 1000.0f);
                    
                    // perlin noiseで初期値を設定
                    _velocityX[x, y] = Mathf.PerlinNoise(x * 0.1f + 200, y * 0.1f) - 0.5f;
                    _velocityY[x, y] = Mathf.PerlinNoise(x * 0.1f, y * 0.1f + 100) - 0.5f;
                }
            }
        }

        protected override void InternalUpdate(float dt)
        {
            UpdateVelocity(dt);
            UpdateDensity(dt);
        }

        protected override void InternalSetUpRenderer(RenderTexture texture)
        {
            _texture2dToRender = new Texture2D(texture.width, texture.height, TextureFormat.RFloat, false);
            _texture2dToRender.filterMode = FilterMode.Point;
            var tex = RenderTexture.active;
            Graphics.Blit(tex, texture);
        }

        protected override void InternalRender(RenderTexture texture)
        {
            for (var x = 0; x < texture.width; x++)
            {
                for (var y = 0; y < texture.height; y++)
                {
                    // 密度をそのまま色に変換
                    var d = Mathf.Clamp01(_density[x, y]);
                    _texture2dToRender.SetPixel(x, y, new Color(d, d, d, 1.0f));
                }
            }
            
            _texture2dToRender.Apply();
            Graphics.Blit(_texture2dToRender, texture);
        }

#region Logic

        /// <summary>
        /// 密度の更新 
        /// </summary>
        /// <param name="dt"></param>
        private void UpdateDensity(float dt)
        {
            ResetValue(_prevDensity);
            
            // 密度のソース追加
            AddSourceOrForce(dt, _density, _prevDensity);
            Swap(ref _density, ref _prevDensity); //prevを更新
            
            // 拡散
            // 初期値に適当な値入っているが、反復法である程度収束するはずなので気にしないことにした
            Diffuse(dt, _stableFluidParameterAsset2D.DensDiffusion, _density, _prevDensity, ValueType.Scalar);
            Swap(ref _density, ref _prevDensity); // prevを更新
            
            // 移流
            Advect(dt, _density, _prevDensity, _velocityX, _velocityY, ValueType.Scalar);
        }
        
        /// <summary>
        /// 流速の更新
        /// </summary>
        /// <param name="dt"></param>
        private void UpdateVelocity(float dt)
        {
            ResetValue(_prevVelocityX);
            ResetValue(_prevVelocityY);
            
            // 流速の力の追加
            AddSourceOrForce(dt, _velocityX, _prevVelocityX);
            AddSourceOrForce(dt, _velocityY, _prevVelocityY);
            Swap(ref _velocityX, ref _prevVelocityX); // prevを更新
            Swap(ref _velocityY, ref _prevVelocityY); // prevを更新
            
            // 拡散(粘性項)
            // 初期値に適当な値入っているが、反復法である程度収束するはずなので気にしないことにした
            Diffuse(dt, _stableFluidParameterAsset2D.Viscosity, _velocityX, _prevVelocityX, ValueType.VectorX);
            Diffuse(dt, _stableFluidParameterAsset2D.Viscosity, _velocityY, _prevVelocityY, ValueType.VectorY);
            
            // ここでも射影
            // prevは計算用のバッファ
            Project(_velocityX, _velocityY, _prevVelocityX, _prevVelocityY);
            Swap(ref _velocityX, ref _prevVelocityX); // prevを更新
            Swap(ref _velocityY, ref _prevVelocityY); // prevを更新
            
            // 流速の移流
            Advect(dt, _velocityX, _prevVelocityX, _prevVelocityX, _prevVelocityY, ValueType.VectorX);
            Advect(dt, _velocityY, _prevVelocityY, _prevVelocityX, _prevVelocityY, ValueType.VectorY);
            
            // div=0の成分を求める
            // prevはあくまで計算用のバッファ
            Project(_velocityX, _velocityY, _prevVelocityX, _prevVelocityY);
        }

        private void ResetValue(float[,] val)
        {
            for (var x = 0; x < GridParameterAsset2D.GridParameter.XMax; x++)
            {
                for (var y = 0; y < GridParameterAsset2D.GridParameter.YMax; y++)
                {
                    val[x, y] = 0;
                }
            }
        }

        /// <summary>
        /// 力やソースの追加
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="val">流速or密度</param>
        /// <param name="source">ソース</param>
        private void AddSourceOrForce(float dt, float[,] val, float[,] source)
        {
            for (var x =0; x < GridParameterAsset2D.GridParameter.XMax; x++)
            {
                for (var y = 0; y < GridParameterAsset2D.GridParameter.YMax; y++)
                {
                    val[x, y] += dt * source[x, y];
                }
            }
        }

        /// <summary>
        /// 拡散
        /// 粘性項の更新にも使用
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="diff"></param>
        /// <param name="val"></param>
        /// <param name="prevVal"></param>
        /// <param name="valType"></param>
        private void Diffuse(float dt, float diff, float[,] val, float[,] prevVal, ValueType valType)
        {
            // 格子全体の幅と高さが1であると仮定している↓　(a= dt*diff/dr^2) dr*N=1 
            var a = dt * diff * (GridParameterAsset2D.GridParameter.XMax - 2) * (GridParameterAsset2D.GridParameter.YMax - 2);
            
            for (var k = 0; k < 20; k++) // ガウスザイデル法(固定数反復)
            {
                for (var x = 1; x < GridParameterAsset2D.GridParameter.XMax - 1; x++) // 境界以外でループ
                {
                    for (var y = 1; y < GridParameterAsset2D.GridParameter.YMax - 1; y++)
                    {
                        val[x, y] = (prevVal[x, y] + a * (val[x - 1, y] + val[x + 1, y] + val[x, y - 1] + val[x, y + 1])) / (1 + 4 * a);
                    }
                }
                SetBoundary(valType, val);
            }
        }

        /// <summary>
        /// 移流
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="val"></param>
        /// <param name="prevVal"></param>
        /// <param name="velX"></param>
        /// <param name="velY"></param>
        /// <param name="valType"></param>
        private void Advect(float dt, float [,] val, float[,] prevVal, float[,] velX, float[,] velY, ValueType valType)
        {
            var gridParameter2D = GridParameterAsset2D.GridParameter;
            
            for (var x = 1; x < gridParameter2D.XMax - 1; x++) // 境界以外でループ
            {
                for (var y = 1; y < gridParameter2D.YMax - 1; y++)
                {
                    // 1frame前の流速の位置をバックトレース
                    // indexと同じ単位に変換している
                    // vel * dt / dr = vel * dt * N (dr * N = 1の仮定ありき)
                    var posX = x - dt * (gridParameter2D.XMax - 2) * velX[x, y]; 
                    var posY = y - dt * (gridParameter2D.YMax - 2) * velY[x, y]; 

                    // バックトレースした位置が格子の外に出ないようにする
                    // (0.5は境界にめり込んでいる状態になるのを防ぐため)
                    posX = Mathf.Clamp(posX, 0.5f, gridParameter2D.XMax - 1.5f);
                    var i0 = (int)posX;
                    var i1 = i0 + 1;
                    posY = Mathf.Clamp(posY, 0.5f, gridParameter2D.YMax - 1.5f);
                    var j0 = (int)posY;
                    var j1 = j0 + 1;

                    // 補間係数
                    var s1 = posX - i0;
                    var s0 = 1 - s1;
                    var t1 = posY - j0;
                    var t0 = 1 - t1;

                    // 双線形補間して値を決定
                    // x方向に補完した値→y方向に補完で求まる
                    val[x, y] = s0 * (t0 * prevVal[i0, j0] + t1 * prevVal[i0, j1]) +
                                s1 * (t0 * prevVal[i1, j0] + t1 * prevVal[i1, j1]);
                }
            }
            SetBoundary(valType, val);
        }

        /// <summary>
        /// ヘルムホルツホッジ分解した際のdiv=0の成分を求める
        /// </summary>
        /// <param name="velocityX"></param>
        /// <param name="velocityY"></param>
        /// <param name="p">あくまで計算用に使用するバッファ</param>
        /// <param name="div">あくまで計算用に使用するバッファ</param>
        private void Project(float[,] velocityX, float[,] velocityY, float[,] p, float[,] div)
        {
            var gridParameter2D = GridParameterAsset2D.GridParameter;
            var h = 1.0f / (gridParameter2D.XMax - 2); // dr*N=1の仮定
            
            // 発散の計算
            for (var x = 1; x < gridParameter2D.XMax - 1; x++) // 境界以外でループ
            {
                for (var y = 1; y < gridParameter2D.YMax - 1; y++)
                { 
                    div[x, y] = -0.5f * h * (velocityX[x + 1, y] - velocityX[x - 1, y] + 
                        velocityY[x, y + 1] - velocityY[x, y - 1]); 
                    p[x, y] = 0;
                }
            }
            SetBoundary(ValueType.Scalar, div);
            SetBoundary(ValueType.Scalar, p); // 境界も0になる

            // ポアソン方程式をガウスザイデル法で解く
            for (var k = 0; k < 20; k++) // ガウスザイデル法(固定数反復)
            {
                for (var x = 1; x < gridParameter2D.XMax - 1; x++) // 境界以外でループ
                {
                    for (var y = 1; y < gridParameter2D.YMax - 1; y++)
                    {
                        p[x, y] = (div[x, y] + p[x - 1, y] + p[x + 1, y] + p[x, y - 1] + p[x, y + 1]) / 4;
                    }
                }
                SetBoundary(ValueType.Scalar, p);
            }
            
            // 流速場のdiv=0の成分を求める(gradient pを差し引く)
            for (var x = 1; x < gridParameter2D.XMax - 1; x++) // 境界以外でループ
            {
                for (var y = 1; y < gridParameter2D.YMax - 1; y++)
                {
                    velocityX[x, y] -= 0.5f * (p[x + 1, y] - p[x - 1, y]) / h;
                    velocityY[x, y] -= 0.5f * (p[x, y + 1] - p[x, y - 1]) / h;
                }
            }
            SetBoundary(ValueType.VectorX, velocityX);
            SetBoundary(ValueType.VectorY, velocityY);
        }

        /// <summary>
        /// 境界条件の設定
        /// </summary>
        /// <param name="valueType">値の種類</param>
        /// <param name="value">境界条件を適用する値</param>
        private void SetBoundary(ValueType valueType, float[,] value)
        {
            var gridParameter2D = GridParameterAsset2D.GridParameter;
            var xMax = gridParameter2D.XMax;
            var yMax = gridParameter2D.YMax;

            for (var x = 1; x < xMax - 1; x++) // 境界以外でループ
            {
                switch (valueType)
                {
                    case ValueType.VectorY: // 鏡面反射
                        value[x, 0] = -value[x, 1]; // 上面
                        value[x, yMax - 1] = -value[x, yMax - 2]; // 下面
                        break;
                    default:
                        value[x, 0] = value[x, 1]; // 上面
                        value[x, yMax - 1] = value[x, yMax - 2]; // 下面
                        break;
                }
            }

            for (var y = 1; y < yMax - 1; y++) // 境界以外でループ
            {
                switch (valueType)
                {
                    case ValueType.VectorX: // 鏡面反射
                        value[0, y] = -value[1, y];
                        value[xMax - 1, y] = -value[xMax - 2, y];
                        break;
                    default:
                        value[0, y] = value[1, y];
                        value[xMax - 1, y] = value[xMax - 2, y];
                        break;
                }
            }

            // 四隅のセルは平均値を使用する
            value[0, 0] = 0.5f * (value[1, 0] + value[0, 1]);
            value[0, yMax - 1] = 0.5f * (value[1, yMax - 1] + value[0, yMax - 2]);
            value[xMax - 1, 0] = 0.5f * (value[xMax - 2, 0] + value[xMax - 1, 1]);
            value[xMax - 1, yMax - 1] = 0.5f * (value[xMax - 2, yMax - 1] + value[xMax - 1, yMax - 2]);
        }
        
        /// <summary>
        /// 入れ替え
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        private void Swap(ref float[,] a, ref float[,] b)
        {
            (a, b) = (b, a);
        }
#endregion
    }
}