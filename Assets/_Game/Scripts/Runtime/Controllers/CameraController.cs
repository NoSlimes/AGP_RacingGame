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

        [Header("Nitro Camera Shake")]
        [SerializeField] private float nitroShakeStrength = 0.08f;
        [SerializeField] private float nitroShakeSpeed = 25f;

        private Transform followTarget;
        private Rigidbody targetRigidbody;
        private Camera cam;

        private Vector3 currentVelocity = Vector3.zero;

        private void Awake()
        {
            cam = GetComponentInChildren<Camera>();
        }

        private void OnEnable()
        {
            Car playerCar = GameManager.Instance.GetPlayerCar();
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

            bool nitro = followTarget.GetComponent<CarControl>().NitroActive;

            if (nitro)
            {
                Vector3 shake = transform.right * Mathf.Sin(Time.time * nitroShakeSpeed) * nitroShakeStrength;
                transform.position += shake;
            }
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
            if (targetRigidbody == null) return;

            // Increase FOV based on speed
            float speed = targetRigidbody.linearVelocity.magnitude;
            float targetFOV = Mathf.Lerp(minFOV, maxFOV, speed / fovSpeedThreshold);

            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 2f);
        }
    }
}