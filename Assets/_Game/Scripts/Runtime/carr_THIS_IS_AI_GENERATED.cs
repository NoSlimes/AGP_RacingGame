using NoSlimes.UnityUtils.Runtime;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace RacingGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class carr_THIS_IS_AI_GENERATED : MonoBehaviour
    {
        [System.Serializable]
        public struct Wheel
        {
            public WheelCollider collider;
            public Transform visualMesh;
            public ParticleSystem smokeParticles; // Assign a smoke PS to each wheel
            public bool isFront;
            public bool isLeft;
        }

        [Header("Particle Settings")]
        [SerializeField] private ParticleSystem exhaustParticles;
        [SerializeField] private Transform exhaustPoint;
        public float maxExhaustEmission = 50f;
        public float idleExhaustEmission = 5f;

        [Header("Audio Settings")]
        [SerializeField] private AudioResource engineAudio;
        public float minPitch = 0.8f;
        public float maxPitch = 2.5f;
        private AudioSource _engineSource;

        [Header("Wheel Setup")]
        [SerializeField] private Wheel[] wheels;

        [Header("Drive Settings")]
        public float motorTorque = 2500f;
        public float brakeTorque = 6000f;
        public float maxSteerAngle = 35f;
        public float highSpeedSteerAngle = 15f;
        public float maxSpeedKmh = 180f;

        [Header("Stability & Suspension")]
        public float antiRollForce = 10000f;
        public float downforce = 100f;
        public float centerOfMassOffset = -0.6f;

        private Rigidbody _rb;
        private Vector2 _inputMove;
        private bool _isBraking;
        private bool _isNitro;

        private InputAction _moveAction;
        private InputAction _brakeAction;
        private InputAction _nitroAction;

        private ParticleSystem _exhaustInstance;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = new Vector3(0, centerOfMassOffset, 0);

            // Setup Input Actions
            _moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _brakeAction = new InputAction("Brake", binding: "<Keyboard>/space");
            _brakeAction.AddBinding("<Gamepad>/buttonSouth");

            _nitroAction = new InputAction("Nitro", binding: "<Keyboard>/leftShift");
            _nitroAction.AddBinding("<Gamepad>/rightTrigger");

            _moveAction.Enable();
            _brakeAction.Enable();
            _nitroAction.Enable();

            // Auto-Tune Suspension to fix the "falling down" bug
            foreach (var w in wheels)
            {
                JointSpring spring = w.collider.suspensionSpring;
                spring.spring = 45000;
                spring.damper = 5000;
                w.collider.suspensionSpring = spring;

                WheelFrictionCurve sideFriction = w.collider.sidewaysFriction;
                sideFriction.stiffness = 2.0f;
                w.collider.sidewaysFriction = sideFriction;
            }
        }

        private void Start()
        {
            // Engine Audio
            _engineSource = AudioManager.Instance.PlayAudioAttached(engineAudio, transform, 1f);
            if (_engineSource != null) _engineSource.loop = true;

            // Exhaust Particles
            if (exhaustParticles != null)
            {
                _exhaustInstance = ParticleManager.Instance.Play(exhaustParticles, transform.position, transform.rotation, transform);
                var main = _exhaustInstance.main;
                main.loop = true;
                _exhaustInstance.transform.localPosition = exhaustPoint.localPosition;
            }
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _brakeAction.Disable();
            _nitroAction.Disable();
        }

        private void Update()
        {
            _inputMove = _moveAction.ReadValue<Vector2>();
            _isBraking = _brakeAction.ReadValue<float>() > 0.1f;
            _isNitro = _nitroAction.ReadValue<float>() > 0.1f;

            UpdateEngineAudio();
            UpdateParticles();
        }

        private void FixedUpdate()
        {
            float speedKmh = _rb.linearVelocity.magnitude * 3.6f;
            ApplySteering(speedKmh);
            ApplyDrive(speedKmh);
            ApplyAntiRoll();
            ApplyDownforce();
            UpdateVisuals();
        }

        private void UpdateEngineAudio()
        {
            if (_engineSource == null) return;

            float speedRatio = (_rb.linearVelocity.magnitude * 3.6f) / maxSpeedKmh;
            float gearValue = (speedRatio * 5f) % 1f;

            float targetPitch = Mathf.Lerp(minPitch, maxPitch, gearValue);
            if (_inputMove.y > 0.1f) targetPitch += 0.2f;
            if (_isNitro) targetPitch += 0.3f;

            _engineSource.pitch = Mathf.Lerp(_engineSource.pitch, targetPitch, Time.deltaTime * 5f);
            _engineSource.volume = Mathf.Lerp(_engineSource.volume, _inputMove.y > 0 ? 1.0f : 0.5f, Time.deltaTime * 2f);
        }

        private void UpdateParticles()
        {
            // 1. Exhaust Modulation
            if (_exhaustInstance != null)
            {
                var emission = _exhaustInstance.emission;
                float throttle = Mathf.Abs(_inputMove.y);
                emission.rateOverTime = Mathf.Lerp(idleExhaustEmission, maxExhaustEmission, throttle);

                var main = _exhaustInstance.main;
                main.startSpeed = Mathf.Lerp(2, 10, throttle);
            }

            // 2. Tire Smoke (Drifting/Wheelspin)
            foreach (var wheel in wheels)
            {
                if (wheel.smokeParticles == null) continue;

                WheelHit hit;
                if (wheel.collider.GetGroundHit(out hit))
                {
                    // If the tire is sliding sideways (drift) or spinning too fast (burnout)
                    if (Mathf.Abs(hit.sidewaysSlip) > 0.35f || Mathf.Abs(hit.forwardSlip) > 0.5f)
                    {
                        var emission = wheel.smokeParticles.emission;
                        emission.enabled = true;
                    }
                    else
                    {
                        var emission = wheel.smokeParticles.emission;
                        emission.enabled = false;
                    }
                }
            }
        }

        private void ApplySteering(float speedKmh)
        {
            float speedFactor = Mathf.InverseLerp(0, maxSpeedKmh, speedKmh);
            float steerAngle = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedFactor);
            foreach (var wheel in wheels)
                if (wheel.isFront) wheel.collider.steerAngle = _inputMove.x * steerAngle;
        }

        private void ApplyDrive(float speedKmh)
        {
            float finalTorque = _inputMove.y * motorTorque;
            if (_isNitro) finalTorque *= 2f; // NITRO BOOST

            foreach (var wheel in wheels)
            {
                if (_isBraking)
                {
                    wheel.collider.motorTorque = 0;
                    wheel.collider.brakeTorque = brakeTorque;
                }
                else
                {
                    wheel.collider.brakeTorque = 0;
                    if (speedKmh < (_isNitro ? maxSpeedKmh * 1.5f : maxSpeedKmh))
                        wheel.collider.motorTorque = finalTorque;
                    else
                        wheel.collider.motorTorque = 0;
                }
            }
        }

        private void ApplyAntiRoll()
        {
            for (int i = 0; i < wheels.Length; i += 2)
            {
                if (i + 1 >= wheels.Length) break;
                Wheel leftW = wheels[i]; Wheel rightW = wheels[i + 1];
                float travelL = 1.0f; float travelR = 1.0f;
                WheelHit hit;

                if (leftW.collider.GetGroundHit(out hit))
                    travelL = (-leftW.collider.transform.InverseTransformPoint(hit.point).y - leftW.collider.radius) / leftW.collider.suspensionDistance;
                if (rightW.collider.GetGroundHit(out hit))
                    travelR = (-rightW.collider.transform.InverseTransformPoint(hit.point).y - rightW.collider.radius) / rightW.collider.suspensionDistance;

                float antiRollValue = (travelL - travelR) * antiRollForce;
                if (leftW.collider.isGrounded) _rb.AddForceAtPosition(leftW.collider.transform.up * -antiRollValue, leftW.collider.transform.position);
                if (rightW.collider.isGrounded) _rb.AddForceAtPosition(rightW.collider.transform.up * antiRollValue, rightW.collider.transform.position);
            }
        }

        private void ApplyDownforce() => _rb.AddForce(-transform.up * downforce * _rb.linearVelocity.magnitude);

        private void UpdateVisuals()
        {
            foreach (var wheel in wheels)
            {
                Vector3 pos; Quaternion rot;
                wheel.collider.GetWorldPose(out pos, out rot);
                if (wheel.visualMesh != null)
                {
                    wheel.visualMesh.position = pos;
                    wheel.visualMesh.rotation = rot;
                }
            }
        }
    }
}