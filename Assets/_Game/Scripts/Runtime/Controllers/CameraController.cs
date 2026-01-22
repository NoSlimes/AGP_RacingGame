using NUnit.Framework.Interfaces;
using UnityEngine;

namespace RacingGame
{
    public class CameraController : TickableBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Vector3 offset = new(0, 2.5f, -6f);
        [SerializeField] private float positionSmoothTime = 0.12f;
        [SerializeField] private float rotationSmoothSpeed = 10f;

        [Header("Speed Effects")]
        [SerializeField] private float minFOV = 60f;
        [SerializeField] private float maxFOV = 80f;
        [SerializeField] private float fovSpeedThreshold = 30f;
        [SerializeField] private float fovTargetSmoothSpeed = 6f;

        [Header("Nitro Camera FOV THING")]
        [SerializeField] private float nitroMaxFOVMultiplier = 1.25f;

        private Transform followTarget;
        private CarControl followCarC;

        private Rigidbody targetRigidbody;
        private Camera cam;

        private Vector3 currentVelocity = Vector3.zero;

        private float targetFOV;

        private void Awake()
        {
            cam = GetComponentInChildren<Camera>();
            targetFOV = cam.fieldOfView;
        }

        private void OnEnable()
        {
            Car playerCar = GameManager.Instance.PlayerCar;
            if (playerCar != null)
            {
                SetFollowTarget(playerCar.transform);
            }
            else
            {
                GameManager.Instance.OnPlayerCarAssigned += SetFollowTarget;
            }

        }

        private void OnDisable()
        {
            GameManager.Instance.OnPlayerCarAssigned -= SetFollowTarget;
        }

        private void SetFollowTarget(Car car)
        {
            SetFollowTarget(car.transform);

            followCarC = car.GetCarComponent<CarControl>();
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            targetRigidbody = target.GetComponent<Rigidbody>();
        }

        public override void Tick()
        {
            if (followTarget == null) return;

            HandlePosition();
            HandleRotation();
            HandleFOV();
        }

        private void HandlePosition()
        {
            Vector3 desiredPosition = followTarget.TransformPoint(offset);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref currentVelocity,
                positionSmoothTime
            );
        }

        private void HandleRotation()
        {
            Vector3 lookAtTarget = followTarget.position + (followTarget.up * 1.5f);
            Vector3 direction = lookAtTarget - transform.position;
            Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                lookRotation,
                rotationSmoothSpeed * Time.deltaTime
            );
        }

        private void HandleFOV()
        {
            float desiredFOV = minFOV;

            if (targetRigidbody != null)
            {
                float speed = targetRigidbody.linearVelocity.magnitude;
                desiredFOV = Mathf.Lerp(minFOV, maxFOV, speed / fovSpeedThreshold);
            }

            if (followCarC && followCarC.NitroActive)
                    desiredFOV = maxFOV * nitroMaxFOVMultiplier;

            // targetFOV moves toward equilibrium
            targetFOV = Mathf.Lerp(targetFOV, desiredFOV, Time.deltaTime * fovTargetSmoothSpeed);

            // camera follows targetFOV
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 8f);
        }
    }
}