using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MySimulator
{
    [Serializable]
    public class GridSimulationRenderer2D : IDisposable
    {
        [SerializeField] 
        private RawImage _imageUi;
        private RenderTexture _texture;

        public void SetUp(GridParameterAsset2D gridParameterAsset2D, Action<RenderTexture> setupAction)
        {
            var gridParameter = gridParameterAsset2D.GridParameter;
            _texture = SetUpTexture(gridParameter);
            
            setupAction?.Invoke(_texture);
        }

        public void Dispose()
        {
            Object.Destroy(_texture);
            _texture = null;
        }

        public void Render(Action<RenderTexture> renderAction)
        {
            renderAction?.Invoke(_texture);
        }

        private RenderTexture SetUpTexture(GridParameterAsset2D.GridParameter2D gridParameter)
        {
            var texture = new RenderTexture(gridParameter.XMax, gridParameter.YMax, 0, RenderTextureFormat.ARGBFloat);
            texture.enableRandomWrite = true;
            _imageUi.texture = texture;
            return texture;
        }
    }
}