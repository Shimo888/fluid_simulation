using MySimulator;
using UnityEngine;

namespace App.StableFluid
{
    [CreateAssetMenu(fileName = "StableFluidParameterAsset2D", menuName = "StableFluid/StableFluidParameterAsset2D")]
    public class StableFluidParameterAsset2D : GridParameterAsset2D
    {
        [SerializeField]
        private float _densDiffusion = 0.0001f;
        
        [SerializeField]
        private float _viscosity = 0.0001f;
        
        /// <summary>
        /// 密度の拡散係数
        /// </summary>
        public float DensDiffusion => _densDiffusion;
        
        /// <summary>
        /// 流速の粘性率
        /// </summary>
        public float Viscosity => _viscosity;
    }
}