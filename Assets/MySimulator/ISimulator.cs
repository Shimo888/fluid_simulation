using System;

namespace MySimulator
{
    public interface ISimulator : IDisposable
    {
        public ParameterAsset ParameterAsset { get; }
        public void SetUp();
        public void ManualFixedUpdate(float dt);
    }
}