using MyEditor.Attribute;
using UnityEngine;

namespace MySimulator
{
    public class SimulationUpdater : MonoBehaviour
    {
        [SerializeReference, SubclassSelector]
        private ISimulator _simulator;

        private void Awake()
        {
            _simulator?.SetUp();
        }

        private void OnDestroy()
        {
            _simulator?.Dispose(); 
        }
        
        private void FixedUpdate()
        {
            var fixedDt = Time.fixedDeltaTime;
            ManualFixedUpdate(fixedDt);
        }

        private void ManualFixedUpdate(float dt)
        {
            _simulator?.ManualFixedUpdate(dt);
        }
    }
}