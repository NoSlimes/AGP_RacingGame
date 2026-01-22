using NoSlimes.Logging;
using UnityEngine;

#if UNITY_EDITOR
#endif

namespace RacingGame
{
    public class CarInputComponent : MonoBehaviour, ICarComponent
    {
        [SerializeReference] private ICarInputs _carInputs;

        private bool hasStarted = false;

        public ICarInputs Inputs => _carInputs;
        int ICarComponent.Priority => -100;

        public bool IsPlayerControlled => _carInputs is not null and PlayerCarInputs;

        public void Initialize(Car _) { }

        public void SetInputs(ICarInputs inputs)
        {
            if (inputs == null)
            {
                DLogger.LogDevError("CarInputComponent.SetInputs was given null inputs.", this);
            }

            _carInputs = inputs;
            _carInputs.Initialize(transform);
            _carInputs.PostInitialize();
        }

        private void OnEnable()
        {
            if (_carInputs == null)
            {
                DLogger.LogDevError("CarInputComponent.CarInputs has not been set.", this);
                return;
            }

            _carInputs.Initialize(transform);

            if (hasStarted)
            {
                _carInputs.PostInitialize();
            }
        }

        private void Start()
        {
            _carInputs?.PostInitialize();
            hasStarted = true;
        }

        private void OnDisable()
        {
            _carInputs?.Deinitialize();
        }
    }

#if UNITY_EDITOR
#endif

}