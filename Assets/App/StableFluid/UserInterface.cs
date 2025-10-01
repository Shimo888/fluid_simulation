using System;
using UnityEngine;
using UnityEngine.UI;

namespace App.StableFluid
{
    /// <summary>
    /// カーソルの位置返す用の適当クラス
    /// </summary>
    [Serializable]
    public class UserInterface 
    {
        [SerializeField] 
        private Canvas _canvas;

        [SerializeField] 
        private RawImage _texture;
        
        public Vector2? InputPos => TryGetInput(out var pos) ? pos : null;

        private bool TryGetInput(out Vector2 position)
        {
            if (!Input.GetMouseButton(0))
            {
                position = default;
                return false;
            }
            
            var camera = _canvas.worldCamera;
            var screenPos = Input.mousePosition;
            var textureRectTransform = _texture.rectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(textureRectTransform, screenPos, camera, out var localPos))
            {    
                if (textureRectTransform.rect.Contains(localPos))
                {
                    var pivot = textureRectTransform.pivot;
                    var x = (localPos.x + textureRectTransform.rect.width * pivot.x) / textureRectTransform.rect.width;
                    var y = (localPos.y + textureRectTransform.rect.height * pivot.y) / textureRectTransform.rect.height;
                    position = new Vector2(x, y);
                    return true;
                }
            }    
            position = default;
            return false;
        }

    }
}