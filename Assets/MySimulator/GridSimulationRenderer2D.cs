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
        private Image _imageUi;
        private Texture2D _texture;

        public void SetUp(GridParameterAsset2D gridParameterAsset2D, Action<Texture2D> setupAction)
        {
            var gridParameter = gridParameterAsset2D.GridParameter;
            _texture = SetUpTexture(gridParameter);
            _ = SetUpSprite(_texture);
            
            setupAction?.Invoke(_texture);
        }

        public void Dispose()
        {
            Object.Destroy(_texture);
            _texture = null;
        }

        public void Render(Action<Texture2D> renderAction)
        {
            renderAction?.Invoke(_texture);
            _texture.Apply();
        }

        private Texture2D SetUpTexture(GridParameterAsset2D.GridParameter2D gridParameter)
        {
            var texture = new Texture2D(gridParameter.XMax, gridParameter.YMax)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            
            texture.Apply();
            return texture;
        }

        private Sprite SetUpSprite(Texture2D texture)
        {
            var sprite =  Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
            _imageUi.sprite = sprite;
            return sprite;
        }
    }
}