using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.AdaptivePerformance;
using UnityEngine.UIElements;

namespace RacingGame
{
    public class CarController : MonoBehaviour
    {
        private CarInputComponent CarInput;

        public Transform CarMesh;
        public Transform CarNormal;
        public Rigidbody Sphere;

        float speed, CurrentSpeed;
        float Rotate, CurrentRotate;
        int DriftDirection;
        float DriftPower;
        int DriftMode = 0;
        bool First, Second, Third;
        Color c;

        [HideInInspector] public bool Drifting;
        [HideInInspector] public bool Breaking;

        [Header("Car model parts")]
        public Transform WheelLF;
        public Transform WheelRF;
        public Transform WheelLR;
        public Transform WheelRR;

        [Header("Parameters")]
        public float Acceleration = 30f;
        public float Steering = 80f;
        public float BrakeStrength = 4f;
        public float Gravity = 10;
        public LayerMask LayerMask;

        [Header("Nitro Configuration")]
        public float nitroMultiplier = 1.8f;
        public float nitroDuration = 1.2f;
        public float nitroCooldown = 2.0f;

        bool nitroActive;
        bool nitroOnCooldown;
        float nitroTimer;

        private void Awake()
        {
            CarInput = GetComponent<CarInputComponent>();
        }

        private void Update()
        {
            // Brakes
            if (CarInput.Inputs.HandBrakeInput)
                Breaking = true;          
            else
                Breaking = false;

            // follow collider
            transform.position = Sphere.transform.position - new Vector3(0, 0.4f, 0);

            // Acceleration and Nitro
            if (CarInput.Inputs.MoveInput.y > 0)
            {
                if (nitroActive)
                    Acceleration *= nitroMultiplier;

                speed = Acceleration;
            }

            // Steering
            if (Mathf.Abs(CarInput.Inputs.MoveInput.x) > 0.01f)
            {
                int dir = CarInput.Inputs.MoveInput.x > 0 ? 1 : -1;
                float amount = Mathf.Abs((CarInput.Inputs.MoveInput.x));
                Steer(dir, amount);
            }

            // Nitro
            if (CarInput.Inputs.NitroInput)
                NitroBoost();

            CurrentSpeed = Mathf.SmoothStep(CurrentSpeed, speed, Time.deltaTime * 12f); speed = 0;
            CurrentRotate = Mathf.Lerp(CurrentRotate, Rotate, Time.deltaTime * 4f); Rotate = 0f;

            // NitroBoost
            if (nitroActive)
            {
                nitroTimer -= Time.deltaTime;

                if(nitroTimer <= 0f)
                {
                    nitroActive = false;
                    nitroOnCooldown = true;
                    Invoke(nameof(ResetNitroCooldown), nitroCooldown);
                }
            }

            // Anims

            //Wheels
            UpdateWheelVisuals(); //I see this in other scripts so I deactivate them here for now
        }

        private void FixedUpdate()
        {
            // Forward Acceleration
            if(!Breaking)
                Sphere.AddForce(CarMesh.transform.forward * CurrentSpeed, ForceMode.Acceleration);
            else
                Sphere.linearVelocity = Vector3.Lerp(Sphere.linearVelocity, Vector3.zero, BrakeStrength * Time.fixedDeltaTime);

            //Gravity
            Sphere.AddForce(Vector3.down * Gravity, ForceMode.Acceleration);

            //Steering
            transform.eulerAngles = Vector3.Lerp(transform.eulerAngles, new Vector3(0, transform.eulerAngles.y + CurrentRotate, 0), Time.deltaTime * 5f);

            // Ground Alignment
            RaycastHit hitOn;
            RaycastHit hitNear;

            Physics.Raycast(transform.position + (transform.up * 1f), Vector3.down, out hitOn, 1.1f, LayerMask);
            Physics.Raycast(transform.position + (transform.up * 0.1f), Vector3.down, out hitNear, 2.0f, LayerMask);

            // Normal Rotation
            CarNormal.up = Vector3.Lerp(CarNormal.up, hitNear.normal, Time.deltaTime * 8.0f);
            CarNormal.Rotate(0, transform.eulerAngles.y, 0);

            // Brakes

            
        }

        public void Boost()
        {
            // Drifting boost (Like Mario :D)
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

        public void Steer(int direction, float amount)
        {
            Rotate = (Steering * direction) * amount;
        }

        void UpdateWheelVisuals()
        {
            float SteerAngle = CarInput.Inputs.MoveInput.x * 15f;
            float roll = Sphere.linearVelocity.magnitude * Time.deltaTime * 50f;

            ApplyWheel(WheelLF, SteerAngle, roll);
            ApplyWheel(WheelLF, SteerAngle, roll);

            ApplyWheel(WheelLR, 0f, roll);
            ApplyWheel(WheelRR, 0f, roll);

        }

        void ApplyWheel(Transform wheel, float steer, float roll)
        {
            Vector3 angles = wheel.localEulerAngles;

            angles.z = steer;      // steering
            angles.y += roll;      // rolling

            wheel.localEulerAngles = angles;
        }

    }
}
