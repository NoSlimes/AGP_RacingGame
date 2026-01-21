using System.Linq;
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
        [SerializeField] private float antiRollStiffness = 5000f;
        [SerializeField] private float downforce = 50f;

        [Header("Nitro Configurations")]
        [SerializeField] private float nitroMultiplier; // How strong the nitro boost is
        [SerializeField] private float nitroDuration; // For how long is the boost going to last
        [SerializeField] private float nitroCooldown; // how long is the cooldown before we can boost again

        private WheelControl[] wheels;
        private WheelControl[] frontWheels;
        private WheelControl[] rearWheels;
        private Rigidbody rigidBody;
        private CarInputComponent carInput;

        private bool nitroActive;
        private bool nitroOnCooldown;
        private float nitroTimer;
        private float nitroCooldownTimer;

        public float MaxSpeed => maxSpeed;
        public float SteeringRange => steeringRange;
        public float SteeringRangeAtMaxSpeed => steeringRangeAtMaxSpeed;

        public float NitroMultiplier => nitroMultiplier;
        public float NitroDuration => nitroDuration;
        public float NitroCooldown => nitroCooldown;
        public bool NitroActive => nitroActive;

        public void Initialize(Car ownerCar)
        {
            carInput = ownerCar.GetCarComponent<CarInputComponent>();
            rigidBody = ownerCar.Rigidbody;
            wheels = ownerCar.GetCarComponents<WheelControl>();

            frontWheels = wheels.Where(w => w.IsFront).ToArray();
            rearWheels = wheels.Where(w => !w.IsFront).ToArray();
        }

        // FixedUpdate is called at a fixed time interval
        public void FixedTickComponent()
        {
            // Read the Vector2 input from the new Input System
            Vector2 inputVector = carInput.Inputs.MoveInput;

            // Read nitro and break Input
            bool nitroInput = carInput.Inputs.NitroInput;
            bool handBrakeInput = carInput.Inputs.HandBrakeInput;

            // Get player input for acceleration and steering
            float accInput = inputVector.y; 
            float steerInput = inputVector.x; 

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
            bool isAccelerating = !handBrakeInput && Mathf.Sign(accInput) == Mathf.Sign(forwardSpeed);

            // Decrease the nitro timer if we are curently using nitro boost
            if (nitroActive)
            {
                nitroTimer -= Time.fixedDeltaTime;

                if (nitroTimer <= 0f)
                {
                    nitroActive = false;
                    nitroOnCooldown = true;
                    nitroCooldownTimer = nitroCooldown;
                }
            }
            else if (nitroOnCooldown)
            {
                nitroCooldownTimer -= Time.fixedDeltaTime;

                if (nitroCooldownTimer <= 0f)
                    nitroOnCooldown = false;
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


            rigidBody.AddForce(-transform.up * downforce * rigidBody.linearVelocity.magnitude);

            ApplyAntiRoll(frontWheels);
            ApplyAntiRoll(rearWheels);

            int wheelsOnGrass = 0;
            foreach (WheelControl wheel in wheels)
            {
                float surfaceGrip = 1f;
                float surfacePower = 1f;

                if (wheel.WheelCollider.GetGroundHit(out var hit))
                {
                    if (hit.collider.sharedMaterial != null && hit.collider.sharedMaterial.name.Contains("Grass"))
                    {
                        surfaceGrip = 0.3f;
                        surfacePower = 0.4f;
                        wheelsOnGrass++;
                    }
                }

                float targetStiffness = 2.0f * surfaceGrip;
                if (!Mathf.Approximately(wheel.WheelCollider.sidewaysFriction.stiffness, targetStiffness))
                {
                    WheelFrictionCurve sFric = wheel.WheelCollider.sidewaysFriction;
                    sFric.stiffness = targetStiffness;
                    wheel.WheelCollider.sidewaysFriction = sFric;

                    WheelFrictionCurve fFric = wheel.WheelCollider.forwardFriction;
                    fFric.stiffness = targetStiffness;
                    wheel.WheelCollider.forwardFriction = fFric;
                }

                // Apply steering to wheels that support steering
                if (wheel.Steerable)
                {
                    //wheel.WheelCollider.steerAngle = hInput * currentSteerRange; // without steering damping
                    // Added damping to steeriing
                    float targetAngle = steerInput * currentSteerRange;
                    wheel.WheelCollider.steerAngle = Mathf.MoveTowards(
                        wheel.WheelCollider.steerAngle,
                        targetAngle,
                        120f * Time.fixedDeltaTime
                    );
                }

                if (isAccelerating)
                {
                    // Apply torque to motorized wheels
                    if (wheel.Motorized)
                    {
                        wheel.WheelCollider.motorTorque = accInput * currentMotorTorque * surfacePower;
                    }
                    // Release brakes when accelerating
                    wheel.WheelCollider.brakeTorque = 0f;
                }
                else
                {
                    // Apply brakes when reversing direction
                    wheel.WheelCollider.motorTorque = 0f;
                    wheel.WheelCollider.brakeTorque = Mathf.Abs(accInput) * brakeTorque;
                }
            }

            // Apply breaking
            if (handBrakeInput)
            {
                foreach (WheelControl wheel in rearWheels)
                    wheel.WheelCollider.brakeTorque = brakeTorque;
            }

            // Speed limiting 
            float excessSpeed = Mathf.Abs(forwardSpeed) - effectiveMaxSpeed;
            if (excessSpeed > 0)
            {
                // Applies a strong backwards force to match air resistance at high speeds
                rigidBody.AddForce(-transform.forward * excessSpeed * 500f);
            }

            // Apply additional resistance if any wheels are on grass
            if (wheelsOnGrass > 0)
            {
                float grassDrag = (wheelsOnGrass / (float)wheels.Length) * forwardSpeed * 200f;
                rigidBody.AddForce(-transform.forward * grassDrag);
            }

            // Engine Drag
            if (Mathf.Abs(accInput) < 0.1f && !handBrakeInput)
            {
                rigidBody.AddForce(
                    -transform.forward * forwardSpeed * 0.5f,
                    ForceMode.Acceleration
                );
            }
        }

        private void ApplyAntiRoll(WheelControl[] wheelPair)
        {
            if (wheelPair.Length < 2)
                return;

            WheelHit hit;
            float travelL = 1.0f;
            float travelR = 1.0f;

            if (wheelPair[0].WheelCollider.GetGroundHit(out hit))
                travelL = (-wheelPair[0].transform.InverseTransformPoint(hit.point).y
                    - wheelPair[0].WheelCollider.radius)
                    / wheelPair[0].WheelCollider.suspensionDistance;

            if (wheelPair[1].WheelCollider.GetGroundHit(out hit))
                travelR = (-wheelPair[1].transform.InverseTransformPoint(hit.point).y
                    - wheelPair[1].WheelCollider.radius)
                    / wheelPair[1].WheelCollider.suspensionDistance;

            float antiRollForce = (travelL - travelR) * antiRollStiffness;

            if (wheelPair[0].WheelCollider.isGrounded)
                rigidBody.AddForceAtPosition(wheelPair[0].transform.up * -antiRollForce, wheelPair[0].transform.position);

            if (wheelPair[1].WheelCollider.isGrounded)
                rigidBody.AddForceAtPosition(wheelPair[1].transform.up * antiRollForce, wheelPair[1].transform.position);
        }

        private void OnDrawGizmos()
        {
            // Visualize the center of mass in the editor
            if (TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                Gizmos.color = Color.green;
                Vector3 comPosition = transform.TransformPoint(rb.centerOfMass);
                Gizmos.DrawSphere(comPosition, 0.15f);
            }

            // Visualize the input direction
            if (carInput != null)
            {
                Vector2 inputVector = carInput.Inputs.MoveInput;
                Gizmos.color = Color.green;
                Vector3 inputDirection = (transform.right * inputVector.x) + (transform.forward * inputVector.y);
                Gizmos.DrawLine(transform.position, transform.position + (inputDirection * 2f));
            }

            // Visualize the movement direction
            if (rigidBody != null)
            {
                Gizmos.color = Color.blue;
                Vector3 velocityDirection = rigidBody.linearVelocity.normalized;
                Gizmos.DrawLine(transform.position, transform.position + (velocityDirection * 2f));
            }
        }
    }
}