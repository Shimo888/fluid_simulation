using System;
using UnityEngine;

namespace MySimulator
{
    [CreateAssetMenu(fileName = "ParameterAsset", menuName = "MySimulator/ParameterAsset")]
    public class GridParameterAsset2D : ParameterAsset
    {
        [Serializable]
        public class GridParameter2D
        {
            [SerializeField] 
            private int _xMax = 256;
            
            [SerializeField]
            private int _yMax = 256;
            
            public int XMax => _xMax;
            public int YMax => _yMax;
        }

        [SerializeField] 
        private GridParameter2D _gridParameter2D;
        
        public GridParameter2D GridParameter => _gridParameter2D;
    }
}