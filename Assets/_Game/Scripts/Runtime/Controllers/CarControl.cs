using NUnit.Framework.Interfaces;
using UnityEngine;
namespace RacingGame
{

    [RequireComponent(typeof(Rigidbody), typeof(CarInputComponent))]
    public class CarControl : MonoBehaviour, ICarComponent
    {
        [Header("Car Properties")]
        [SerializeField] private float motorTorque = 2000f;
        [SerializeField] private float brakeTorque = 2000f;
        [SerializeField] private float maxSpeed = 20f;
        [SerializeField] private float steeringRange = 30f;
        [SerializeField] private float steeringRangeAtMaxSpeed = 10f;
        [SerializeField] private float centreOfGravityOffset = -1f;

        [Header("Nitro Configurations")]
        [SerializeField] private float nitroMultiplier; // How strong the nitro boost is
        [SerializeField] private float nitroDuration; // For how long is the boost going to last
        [SerializeField] private float nitroCooldown; // how long is the cooldown before we can boost again
        private float nitroTimer;

        [Header("Effects")]
        public TrailRenderer[] TireMarks;
        private bool tireMarkFlag;

        private WheelControl[] wheels;
        private Rigidbody rigidBody;

        private bool nitroActive;
        private bool nitroOnCooldown;

        private CarInputComponent carInput;

        public float MaxSpeed => maxSpeed;
        public float SteeringRange => steeringRange;
        public float SteeringRangeAtMaxSpeed => steeringRangeAtMaxSpeed;

        public float NitroMultiplier => nitroMultiplier;
        public float NitroDuration => nitroDuration;
        public float NitroCooldown => nitroCooldown;

        public void Initialize(Car ownerCar) 
        {
            carInput = ownerCar.GetCarComponent<CarInputComponent>();
            rigidBody = ownerCar.Rigidbody;
            wheels = ownerCar.GetCarComponents<WheelControl>();

            Vector3 centerOfMass = rigidBody.centerOfMass;
            centerOfMass.y += centreOfGravityOffset;
            rigidBody.centerOfMass = centerOfMass;
        }

        // FixedUpdate is called at a fixed time interval
        public void FixedTickComponent()
        {
            // Read the Vector2 input from the new Input System
            Vector2 inputVector = carInput.Inputs.MoveInput;

            // Read nitro and break Input
            bool nitroInput = carInput.Inputs.NitroInput;
            bool brakeInput = carInput.Inputs.BrakeInput;

            // Get player input for acceleration and steering
            float vInput = inputVector.y; // Forward/backward input
            float hInput = inputVector.x; // Steering input

            // Calculate current speed along the car's forward axis
            float forwardSpeed = Vector3.Dot(transform.forward, rigidBody.linearVelocity);
            float effectiveMaxSpeed = nitroActive ? maxSpeed * nitroMultiplier : maxSpeed;
            float speedFactor = Mathf.InverseLerp(0, effectiveMaxSpeed, Mathf.Abs(forwardSpeed)); // Normalized speed factor

            // Reduce motor torque and steering at high speeds for better handling
            float torqueFade = nitroActive ? 0f : speedFactor;
            float baseTorque = Mathf.Lerp(motorTorque, 0f, torqueFade);
            float currentMotorTorque = nitroActive ? baseTorque * nitroMultiplier : baseTorque;
            // float currentMotorTorque = Mathf.Lerp(motorTorque, 0f, torqueFade);
            float currentSteerRange = Mathf.Lerp(steeringRange, steeringRangeAtMaxSpeed, speedFactor);

            // Determine if the player is accelerating or trying to reverse
            bool isAccelerating = !brakeInput && Mathf.Sign(vInput) == Mathf.Sign(forwardSpeed);

            // Decrease the nitro timer if we are curently using nitro boost
            if (nitroActive)
            {
                nitroTimer -= Time.fixedDeltaTime;

                // Start cooldown and deactivate nitro
                if (nitroTimer <= 0f)
                {
                    nitroActive = false;
                    nitroOnCooldown = true;
                    Invoke(nameof(ResetNitroCooldown), nitroCooldown);
                }
            }

            // Activate nitro
            if (nitroInput && !nitroActive && !nitroOnCooldown)
            {
                nitroActive = true;
                nitroTimer = nitroDuration;

                rigidBody.AddForce(
                    transform.forward * motorTorque * nitroMultiplier,
                    ForceMode.Impulse
                );
            }

            foreach (var wheel in wheels)
            {             
                // Apply Speed based traction
                WheelFrictionCurve sideways = wheel.WheelCollider.sidewaysFriction;
                sideways.stiffness = Mathf.Lerp(2.0f, 0.9f, speedFactor);
                wheel.WheelCollider.sidewaysFriction = sideways;

                // Apply steering to wheels that support steering
                if (wheel.Steerable)
                {
                    //wheel.WheelCollider.steerAngle = hInput * currentSteerRange; // without steering damping
                    // Added damping to steeriing
                    wheel.WheelCollider.steerAngle = Mathf.Lerp(wheel.WheelCollider.steerAngle, hInput * currentSteerRange, Time.fixedDeltaTime * 6f);
                }

                if (isAccelerating)
                {
                    // Apply torque to motorized wheels
                    if (wheel.Motorized)
                    {
                        wheel.WheelCollider.motorTorque = vInput * currentMotorTorque;
                    }
                    // Release brakes when accelerating
                    wheel.WheelCollider.brakeTorque = 0f;
                }
                else
                {
                    // Apply brakes when reversing direction
                    wheel.WheelCollider.motorTorque = 0f;
                    wheel.WheelCollider.brakeTorque = Mathf.Abs(vInput) * brakeTorque;
                }

                // Apply breaking
                if (brakeInput)
                {
                    wheel.WheelCollider.brakeTorque = brakeTorque;
                    StartEmmiter();
                    continue;
                }
                else StopEmmiter();
            }

            // Hard speed clamp
            Vector3 flatVelocity = Vector3.ProjectOnPlane(rigidBody.linearVelocity, transform.up);

            if (!nitroActive && flatVelocity.magnitude > maxSpeed)
            {
                rigidBody.linearVelocity =
                    flatVelocity.normalized * maxSpeed +
                    Vector3.Project(rigidBody.linearVelocity, transform.up);
            }

            // Engine Drag
            if (Mathf.Abs(vInput) < 0.1f && !brakeInput)
            {
                rigidBody.AddForce(
                    -transform.forward * forwardSpeed * 0.5f,
                    ForceMode.Acceleration
                );
            }
        }

        private void OnDrawGizmos()
        {
            // Visualize the center of mass in the editor
            if (rigidBody != null)
            {
                Gizmos.color = Color.red;
                Vector3 comPosition = transform.position + rigidBody.centerOfMass;
                Gizmos.DrawSphere(comPosition, 0.1f);
            }

            // Visualize the input direction
            if (carInput != null)
            {
                Vector2 inputVector = carInput.Inputs.MoveInput;
                Gizmos.color = Color.green;
                Vector3 inputDirection = transform.right * inputVector.x + transform.forward * inputVector.y;
                Gizmos.DrawLine(transform.position, transform.position + inputDirection * 2f);
            }

            // Visualize the movement direction
            if (rigidBody != null)
            {
                Gizmos.color = Color.blue;
                Vector3 velocityDirection = rigidBody.linearVelocity.normalized;
                Gizmos.DrawLine(transform.position, transform.position + velocityDirection * 2f);
            }
        }

        // Activates the nitro timer
        public void NitroBoost()
        {
            if (nitroActive || nitroOnCooldown)
                return;

            nitroActive = true;
            nitroTimer = nitroDuration;
        }

        public void ResetNitroCooldown()
        {
            nitroOnCooldown = false;
        }

        private void StartEmmiter()
        {
            if (tireMarkFlag) return;
            foreach(TrailRenderer T in TireMarks)
            {
                T.emitting = true;
            }

            tireMarkFlag = true;
        }

        private void StopEmmiter()
        {
            if (!tireMarkFlag) return;
            foreach (TrailRenderer T in TireMarks)
            {
                T.emitting = false;
            }

            tireMarkFlag = false;
        }
    }
}