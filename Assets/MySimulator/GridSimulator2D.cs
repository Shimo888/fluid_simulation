using System;
using UnityEngine;

namespace MySimulator
{
    [Serializable]
    public abstract class GridSimulator2D : ISimulator
    {
        [SerializeField]
        private GridSimulationRenderer2D _renderer;

        public GridParameterAsset2D GridParameterAsset2D {get; protected set; }
        public ParameterAsset ParameterAsset => GridParameterAsset2D;

        public void SetUp()
        {
            InternalSetUp();
            _renderer.SetUp(GridParameterAsset2D, InternalSetUpRenderer);
        }
        
        public void Dispose()
        {
            _renderer.Dispose();
        }

        public void ManualFixedUpdate(float dt)
        {
            InternalUpdate(dt);
            _renderer.Render(InternalRender);
        }

        protected abstract void InternalSetUp();

        protected abstract void InternalUpdate(float dt);

        protected abstract void InternalSetUpRenderer(Texture2D texture);

        protected abstract void InternalRender(Texture2D texture);
    }
}