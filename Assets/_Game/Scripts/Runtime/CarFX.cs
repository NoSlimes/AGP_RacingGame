using System.Collections.Generic;
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
        [SerializeField] private float pitchSmoothSpeed = 10f;
        [SerializeField] private float volumeSmoothSpeed = 5f;
        [SerializeField] private float rpmFlutuation = 0.05f;

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

        private Dictionary<WheelControl, ParticleSystem> skidParticles = new();

        private float currentRPM;
        private float engineLoad;

        public int Priority => 100;

        public void Initialize(Car ownerCar)
        {
            carControl = ownerCar.GetCarComponent<CarControl>();
            carInput = ownerCar.GetCarComponent<CarInputComponent>();
            rb = ownerCar.Rigidbody;
            wheelControllers = ownerCar.GetCarComponents<WheelControl>();

            foreach (var wheelController in wheelControllers)
            {
                var ps = wheelController.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop();
                    skidParticles[wheelController] = ps;
                }
            }

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
                if (skidTrail != null) Destroy(skidTrail.gameObject);
            }
        }

        public void TickComponent()
        {
            UpdateEngineSound();
        }

        public void FixedTickComponent()
        {
            float maxSlipForAudio = 0f;

            for (int i = 0; i < wheelControllers.Length; i++)
            {
                WheelControl control = wheelControllers[i];

                // Usually we only show trails for rear wheels, but we need slip data from all
                // for the audio to sound correct.
                WheelCollider col = wheelColliders[i];
                SkidTrail trail = skidTrails[i];

                if (!col.GetGroundHit(out WheelHit hit))
                {
                    trail.EndTrail();
                    if (skidParticles.TryGetValue(control, out ParticleSystem psOff)) psOff.Stop();
                    continue;
                }

                // --- GRASS DETECTION ---
                bool isOnGrass = (hit.collider.sharedMaterial != null && hit.collider.sharedMaterial.name.Contains("Grass"))
                                 || hit.collider.name.Contains("Grass");

                // On grass, the reported slip values are much lower because friction is lower.
                // We scale the threshold and the reported slip to compensate.
                float effectiveThreshold = isOnGrass ? (skidSlipThreshold * 0.25f) : skidSlipThreshold;
                float slip = Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip));

                // Update max slip for audio (we normalize grass slip so it's audible)
                float audioSlip = isOnGrass ? (slip * 4f) : slip;
                if (audioSlip > maxSlipForAudio) maxSlipForAudio = audioSlip;

                if (control.IsFront) continue; 

                if (slip > effectiveThreshold)
                {
                    // Calculate intensity based on the effective threshold
                    float intensity = Mathf.InverseLerp(effectiveThreshold, effectiveThreshold * 3f, slip);
                    trail.AddPoint(hit.point + hit.normal * 0.02f, hit.normal, intensity);

                    if (skidParticles.TryGetValue(control, out ParticleSystem psOn))
                    {
                        if (!psOn.isPlaying) psOn.Play();
                    }
                }
                else
                {
                    trail.EndTrail();
                    if (skidParticles.TryGetValue(control, out ParticleSystem psOff))
                    {
                        if (psOff.isPlaying) psOff.Stop();
                    }
                }
            }

            UpdateSlipSound(maxSlipForAudio);
        }

        private void UpdateEngineSound()
        {
            if (engineAudioSource == null || carControl == null) return;

            // 1. Get Forward Speed (ignores vertical falling speed)
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            float speedRatio = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / carControl.MaxSpeed);

            // 2. Determine Grounded State
            bool isGrounded = false;
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i].isGrounded) { isGrounded = true; break; }
            }

            // 3. Calculate Engine Load (How hard is the engine working?)
            // If we are in the air, load is just throttle. 
            // If on ground, load is throttle + a bit of speed resistance.
            float throttle = Mathf.Abs(carInput.Inputs.MoveInput.y);
            float targetLoad = isGrounded ? throttle : throttle * 0.5f;
            engineLoad = Mathf.Lerp(engineLoad, targetLoad, Time.deltaTime * 3f);

            // 4. Calculate RPM (Pitch)
            float targetRPM = 0f;

            if (isGrounded)
            {
                // PITCH follows Gears when on ground (even if rolling)
                float gearCount = 5f;
                float gearSector = 1f / gearCount;
                float gearRelativeSpeed = (speedRatio % gearSector) / gearSector;

                // Base RPM from wheels + extra RPM from throttle "strain"
                targetRPM = Mathf.Lerp(0.2f, 0.9f, gearRelativeSpeed) + (throttle * 0.1f);
            }
            else
            {
                // PITCH follows throttle only when in air (Revving)
                // This prevents "insane" engine sounds when falling
                targetRPM = Mathf.Lerp(0.1f, 0.7f, throttle);
            }

            if (carInput.Inputs.NitroInput) targetRPM += 0.2f;
            currentRPM = Mathf.Lerp(currentRPM, targetRPM, Time.deltaTime * 10f);

            // 5. Apply Pitch
            engineAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, currentRPM);

            // 6. Apply Volume (The "Rolling" Fix)
            // We want the engine to be audible when rolling, but much quieter than when accelerating.
            // minVolume = Idle/Rolling volume
            // maxVolume = Full throttle volume
            float gearVolumeFactor = isGrounded ? (speedRatio * 0.2f) : 0f; // slight volume increase with speed
            float loadVolume = engineLoad * (maxVolume - minVolume);

            engineAudioSource.volume = minVolume + loadVolume + gearVolumeFactor;
        }

        private void UpdateSlipSound(float slip)
        {
            if (slipAudioSource == null) return;

            // slip is now pre-normalized for grass in FixedTickComponent
            float targetVolume = Mathf.InverseLerp(slipStart, slipFull, slip);
            targetVolume = Mathf.Clamp01(targetVolume);

            slipAudioSource.volume = Mathf.Lerp(slipAudioSource.volume, targetVolume, Time.fixedDeltaTime * 8f) * slipAudioVolume;
            slipAudioSource.pitch = Mathf.Lerp(0.9f, 1.2f, targetVolume);
        }
    }
}