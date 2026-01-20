using NUnit.Framework.Interfaces;
using UnityEngine;
namespace RacingGame
{

    [RequireComponent(typeof(Rigidbody), typeof(CarInputComponent))]
    public class CarControl : TickableBehaviour
    {
        [Header("Car Properties")]
        [SerializeField] private float motorTorque = 2000f;
        [SerializeField] private float brakeTorque = 2000f;
        [SerializeField] private float maxSpeed = 20f;
        [SerializeField] private float steeringRange = 30f;
        [SerializeField] private float steeringRangeAtMaxSpeed = 10f;
        [SerializeField] private float centreOfGravityOffset = -1f;

        [Header("Nitro Configurations")]
        public float nitroMultiplier; // How strong the nitro boos is
        public float nitroDuration; // For how long is the boost going to last
        public float nitroCooldown; // how long is the cooldown before we can boost again
        private float nitroTimer;

        private WheelControl[] wheels;
        private Rigidbody rigidBody;

        private bool nitroActive;
        private bool nitroOnCooldown;

        private CarInputComponent carInput;

        void Awake()
        {
            carInput = GetComponent<CarInputComponent>();
            rigidBody = GetComponent<Rigidbody>();
            wheels = GetComponentsInChildren<WheelControl>();
        }

        // Start is called before the first frame update
        void Start()
        {
            // Adjust center of mass to improve stability and prevent rolling
            Vector3 centerOfMass = rigidBody.centerOfMass;
            centerOfMass.y += centreOfGravityOffset;
            rigidBody.centerOfMass = centerOfMass;

            // Get all wheel components attached to the car
        }

        // FixedUpdate is called at a fixed time interval
        public override void FixedTick()
        {
            // Read the Vector2 input from the new Input System
            Vector2 inputVector = carInput.Inputs.MoveInput;

            // Reed nitro and break Input
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

            // Decrese the nitro timer if we are curently using nitro boost
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
                Debug.Log("Nitro is active and max speed is " + effectiveMaxSpeed);
            }

            foreach (var wheel in wheels)
            {
                // Apply steering to wheels that support steering
                if (wheel.steerable)
                {
                    wheel.WheelCollider.steerAngle = hInput * currentSteerRange;
                }

                // Apply breaking
                if (brakeInput)
                {
                    wheel.WheelCollider.brakeTorque = brakeTorque;
                    continue;
                }

                if (isAccelerating)
                {
                    // Apply torque to motorized wheels
                    if (wheel.motorized)
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
            }

            // Hard speed clamp
            Vector3 flatVelocity = Vector3.ProjectOnPlane(rigidBody.linearVelocity, transform.up);

            if (flatVelocity.magnitude > effectiveMaxSpeed)
            {
                rigidBody.linearVelocity =
                    flatVelocity.normalized * effectiveMaxSpeed +
                    Vector3.Project(rigidBody.linearVelocity, transform.up);
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

        // Activates the notro timer
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
    }
}