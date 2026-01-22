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
        [Tooltip("The constant acceleration force applied while holding Nitro (m/s^2)")]
        [SerializeField] private float nitroAcceleration = 25f;
        [Tooltip("How much the top speed is multiplied by while Nitro is active")]
        [SerializeField] private float nitroMaxSpeedMultiplier = 1.5f;
        [Tooltip("Maximum amount of time the nitro can be held before depleting")]
        [SerializeField] private float nitroDuration = 2.5f;
        [Tooltip("Time required for the nitro tank to refill after depletion or release")]
        [SerializeField] private float nitroCooldown = 3f;

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
        public bool NitroActive => nitroActive;

        public void Initialize(Car ownerCar)
        {
            carInput = ownerCar.GetCarComponent<CarInputComponent>();
            rigidBody = ownerCar.Rigidbody;
            wheels = ownerCar.GetCarComponents<WheelControl>();

            frontWheels = wheels.Where(w => w.IsFront).ToArray();
            rearWheels = wheels.Where(w => !w.IsFront).ToArray();

            // Initialize the nitro tank to full
            nitroTimer = nitroDuration;
        }

        public void FixedTickComponent()
        {
            // Read inputs
            Vector2 inputVector = carInput.Inputs.MoveInput;
            bool nitroInput = carInput.Inputs.NitroInput;
            bool handBrakeInput = carInput.Inputs.HandBrakeInput;

            float accInput = inputVector.y;
            float steerInput = inputVector.x;

            // 1. HANDLE NITRO LOGIC (Held down + Acceleration)
            HandleNitroUsage(nitroInput);

            // 2. SPEED CALCULATIONS
            float forwardSpeed = Vector3.Dot(transform.forward, rigidBody.linearVelocity);
            float effectiveMaxSpeed = nitroActive ? maxSpeed * nitroMaxSpeedMultiplier : maxSpeed;
            float speedFactor = Mathf.InverseLerp(0, effectiveMaxSpeed, Mathf.Abs(forwardSpeed));

            // Reduce torque as we reach max speed to prevent infinite acceleration
            float torqueFade = speedFactor;
            float baseTorque = Mathf.Lerp(motorTorque, 0f, torqueFade);

            // Boost the base motor torque slightly if nitro is on for better uphill/resistance feel
            float currentMotorTorque = nitroActive ? baseTorque * 1.5f : baseTorque;
            float currentSteerRange = Mathf.Lerp(steeringRange, steeringRangeAtMaxSpeed, speedFactor);

            bool isAccelerating = !handBrakeInput && Mathf.Sign(accInput) == Mathf.Sign(forwardSpeed);

            // 3. APPLY PHYSICAL FORCES
            // Downforce
            rigidBody.AddForce(-transform.up * downforce * rigidBody.linearVelocity.magnitude);

            // Anti-Roll
            ApplyAntiRoll(frontWheels);
            ApplyAntiRoll(rearWheels);

            int wheelsOnGrass = 0;
            foreach (WheelControl wheel in wheels)
            {
                float surfaceGrip = 1f;
                float surfacePower = 1f;

                // Ground Detection / Surface Physics
                if (wheel.WheelCollider.GetGroundHit(out var hit))
                {
                    if (hit.collider.sharedMaterial != null && hit.collider.sharedMaterial.name.Contains("Grass"))
                    {
                        surfaceGrip = 0.3f;
                        surfacePower = 0.4f;
                        wheelsOnGrass++;
                    }
                }

                // Update Friction based on surface
                float targetStiffness = 2.0f * surfaceGrip;
                WheelFrictionCurve sFric = wheel.WheelCollider.sidewaysFriction;
                sFric.stiffness = targetStiffness;
                wheel.WheelCollider.sidewaysFriction = sFric;

                WheelFrictionCurve fFric = wheel.WheelCollider.forwardFriction;
                fFric.stiffness = targetStiffness;
                wheel.WheelCollider.forwardFriction = fFric;

                // Steering
                if (wheel.Steerable)
                {
                    float targetAngle = steerInput * currentSteerRange;
                    wheel.WheelCollider.steerAngle = Mathf.MoveTowards(
                        wheel.WheelCollider.steerAngle,
                        targetAngle,
                        120f * Time.fixedDeltaTime
                    );
                }

                // Acceleration and Braking
                if (isAccelerating)
                {
                    if (wheel.Motorized)
                    {
                        wheel.WheelCollider.motorTorque = accInput * currentMotorTorque * surfacePower;
                    }
                    wheel.WheelCollider.brakeTorque = 0f;
                }
                else
                {
                    wheel.WheelCollider.motorTorque = 0f;
                    wheel.WheelCollider.brakeTorque = Mathf.Abs(accInput) * brakeTorque;
                }
            }

            // Handbrake Logic
            if (handBrakeInput)
            {
                foreach (WheelControl wheel in rearWheels)
                    wheel.WheelCollider.brakeTorque = brakeTorque;
            }

            // 4. LIMITING AND RESISTANCE
            // Speed limiting 
            float excessSpeed = Mathf.Abs(forwardSpeed) - effectiveMaxSpeed;
            if (excessSpeed > 0)
            {
                rigidBody.AddForce(-transform.forward * excessSpeed * 500f);
            }

            // Grass resistance
            if (wheelsOnGrass > 0)
            {
                float grassDrag = (wheelsOnGrass / (float)wheels.Length) * forwardSpeed * 200f;
                rigidBody.AddForce(-transform.forward * grassDrag);
            }

            // Engine Drag (Neutral)
            if (Mathf.Abs(accInput) < 0.1f && !handBrakeInput)
            {
                rigidBody.AddForce(-transform.forward * forwardSpeed * 0.5f, ForceMode.Acceleration);
            }
        }

        private void HandleNitroUsage(bool nitroInput)
        {
            // Default to inactive unless conditions are met
            nitroActive = false;

            if (nitroOnCooldown)
            {
                nitroCooldownTimer -= Time.fixedDeltaTime;
                if (nitroCooldownTimer <= 0)
                {
                    nitroOnCooldown = false;
                    nitroTimer = nitroDuration; // Refill the tank
                }
            }
            else
            {
                // Active only while button is held AND we have 'fuel'
                if (nitroInput && nitroTimer > 0)
                {
                    nitroActive = true;
                    nitroTimer -= Time.fixedDeltaTime;

                    // Apply continuous Acceleration force
                    rigidBody.AddForce(transform.forward * nitroAcceleration, ForceMode.Acceleration);

                    // If tank empties while holding, trigger cooldown
                    if (nitroTimer <= 0)
                    {
                        StartNitroCooldown();
                    }
                }
                // If user releases the button before it's empty, we could either refill or just stop. 
                // Currently: Refills only after a full depletion or if you want it to recharge slowly, 
                // you would add recharge logic here.
            }
        }

        private void StartNitroCooldown()
        {
            nitroActive = false;
            nitroOnCooldown = true;
            nitroCooldownTimer = nitroCooldown;
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
            if (TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                Gizmos.color = Color.green;
                Vector3 comPosition = transform.TransformPoint(rb.centerOfMass);
                Gizmos.DrawSphere(comPosition, 0.15f);
            }

            if (carInput != null)
            {
                Vector2 inputVector = carInput.Inputs.MoveInput;
                Gizmos.color = Color.green;
                Vector3 inputDirection = (transform.right * inputVector.x) + (transform.forward * inputVector.y);
                Gizmos.DrawLine(transform.position, transform.position + (inputDirection * 2f));
            }

            if (rigidBody != null)
            {
                Gizmos.color = Color.blue;
                Vector3 velocityDirection = rigidBody.linearVelocity.normalized;
                Gizmos.DrawLine(transform.position, transform.position + (velocityDirection * 2f));
            }
        }
    }
}