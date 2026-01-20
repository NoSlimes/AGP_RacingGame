using UnityEngine;

namespace RacingGame
{
    public class CarFX : MonoBehaviour, ICarComponent
    {
        [SerializeField] private AudioSource engineAudioSource;
        [SerializeField, Range(0f, 5f)] private float minPitch = 0.8f;
        [SerializeField, Range(0f, 5f)] private float maxPitch = 2.5f;
        [SerializeField, Range(0f, 1f)] private float minVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float maxVolume = 1.0f;

        private CarControl carControl;
        private CarInputComponent carInput;
        private Rigidbody rb;

        public int Priority => 100;

        public void Initialize(Car ownerCar)
        {
            carControl = ownerCar.GetCarComponent<CarControl>();
            carInput = ownerCar.GetCarComponent<CarInputComponent>();
            rb = ownerCar.Rigidbody;

            if (engineAudioSource != null)
            {
                engineAudioSource.loop = true;
                if (!engineAudioSource.isPlaying) engineAudioSource.Play();
            }
        }

        public void TickComponent()
        {
            if (engineAudioSource == null || carControl == null) return;

            float speedRatio = rb.linearVelocity.magnitude / carControl.MaxSpeed;
            float gearValue = (speedRatio * 5f) % 1f;

            float targetPitch = Mathf.Lerp(minPitch, maxPitch, gearValue);
            if (carInput.Inputs.MoveInput.y > 0.1f) targetPitch += 0.1f;
            if (carInput.Inputs.NitroInput) targetPitch += 0.4f;

            engineAudioSource.pitch = Mathf.Lerp(engineAudioSource.pitch, targetPitch, Time.deltaTime * 5f);
            float targetVol = carInput.Inputs.MoveInput.y > 0.1f ? maxVolume : minVolume;
            engineAudioSource.volume = Mathf.Lerp(engineAudioSource.volume, targetVol, Time.deltaTime * 3f);
        }
    }
}