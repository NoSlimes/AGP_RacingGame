using System.Linq;
using UnityEngine;

namespace RacingGame
{
    public class CarFX : MonoBehaviour, ICarComponent
    {
        [Header("Audio: Engine")]
        [SerializeField] private AudioSource engineAudioSource;
        [SerializeField, Range(0f, 5f)] private float minPitch = 0.8f;
        [SerializeField, Range(0f, 5f)] private float maxPitch = 2.5f;
        [SerializeField, Range(0f, 1f)] private float minVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float maxVolume = 1.0f;

        [Header("Audio: Tire Slipping")]
        [SerializeField] private AudioSource slipAudioSource;
        [SerializeField, Range(0f, 2f)] private float slipStart = 0.35f;
        [SerializeField, Range(0f, 2f)] private float slipFull = 1.0f;
        [SerializeField, Range(0f, 1f)] private float slipAudioVolume = 0.5f;

        [Header("Skid Marks")]
        [SerializeField] private SkidTrail skidTrailPrefab;
        [SerializeField, Range(0f, 2f)] private float skidSlipThreshold = 0.4f;

        private CarControl carControl;
        private CarInputComponent carInput;
        private Rigidbody rb;
        private WheelControl[] wheelControllers;
        private WheelCollider[] wheelColliders;
        private SkidTrail[] skidTrails;

        public int Priority => 100;

        public void Initialize(Car ownerCar)
        {
            carControl = ownerCar.GetCarComponent<CarControl>();
            carInput = ownerCar.GetCarComponent<CarInputComponent>();
            rb = ownerCar.Rigidbody;
            wheelControllers = ownerCar.GetCarComponents<WheelControl>();

            wheelColliders = new WheelCollider[wheelControllers.Length];
            for (int i = 0; i < wheelControllers.Length; i++)
            {
                WheelControl wheelController = wheelControllers[i];
                wheelColliders[i] = wheelController.GetComponent<WheelCollider>();  
            }

            if (engineAudioSource != null)
            {
                engineAudioSource.loop = true;
                if (!engineAudioSource.isPlaying) engineAudioSource.Play();
            }

            if (slipAudioSource != null)
            {
                slipAudioSource.loop = true;
                slipAudioSource.volume = 0f;
                if (!slipAudioSource.isPlaying) slipAudioSource.Play();
            }

            var go = GameObject.Find("SkidTrailParent");
            Transform skidParent = go != null ? go.transform : null;
            if (skidParent == null)
            {
                skidParent = new GameObject("SkidTrailParent").transform;
                skidParent.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            skidTrails = new SkidTrail[wheelControllers.Length];
            for (int i = 0; i < wheelControllers.Length; i++)
            {
                skidTrails[i] = Instantiate(skidTrailPrefab, skidParent);
            }
        }

        private void OnDestroy()
        {
            foreach (var skidTrail in skidTrails)
            {
                Destroy(skidTrail);
            }
        }

        public void TickComponent()
        {
            UpdateEngineSound();
        }

        public void FixedTickComponent()
        {
            float maxSlip = 0f;

            for (int i = 0; i < wheelControllers.Length; i++)
            {
                WheelControl control = wheelControllers[i];

                if (control.IsFront)
                    continue;

                WheelCollider col = wheelColliders[i];
                SkidTrail trail = skidTrails[i];

                if (!col.GetGroundHit(out WheelHit hit))
                {
                    trail.EndTrail();
                    continue;
                }

                float slip = Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip));
                if (slip > maxSlip) maxSlip = slip;

                if (slip > skidSlipThreshold)
                {
                    float intensity = Mathf.InverseLerp(skidSlipThreshold, 1.2f, slip);
                    trail.AddPoint(hit.point + hit.normal * 0.02f, hit.normal, intensity);
                }
                else
                {
                    trail.EndTrail();
                }
            }

            UpdateSlipSound(maxSlip);
        }

        private void UpdateEngineSound()
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

        private void UpdateSlipSound(float slip)
        {
            if (slipAudioSource == null) return;

            float targetVolume = Mathf.InverseLerp(slipStart, slipFull, slip);
            targetVolume = Mathf.Clamp01(targetVolume);

            slipAudioSource.volume = Mathf.Lerp(slipAudioSource.volume, targetVolume, Time.fixedDeltaTime * 8f) * slipAudioVolume;
            slipAudioSource.pitch = Mathf.Lerp(0.9f, 1.2f, targetVolume);
        }
    }
}
