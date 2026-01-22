using UnityEngine;

namespace RacingGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarTopleRecovery : MonoBehaviour, ICarComponent
    {
        [Header("Flip Detection Configuration")]
        [SerializeField] private float upsideDownDotThreshold = 0.7f; // Upside down limit
        [SerializeField] private float timeBeforeReset = 3f; // How long we allow the car to correct itself before we intervine

        [Header("Reset Configuration")]
        [SerializeField] private float upwardImpulse = 5f;

        [Header("Respawn")]
        public RacingGame._Game.Scripts.PCG.CheckpointManager checkpointManager;
        public float killY = -10f;
        public float respawnCooldown = 0.25f;
        private float _lastRespawnTime;

        private Car car;
        private float upsideDownTimer;

        public void Initialize(Car ownerCar)
        {
            car = ownerCar;
        }

        public void FixedTickComponent()
        {
            // Check orientation
            bool upsideDown = Vector3.Dot(transform.up, Vector3.down) > upsideDownDotThreshold;

            // Reset car rotasion after delay
            if (upsideDown)
            {
                upsideDownTimer += Time.fixedDeltaTime;

                if (upsideDownTimer >= timeBeforeReset)
                {
                    ResetCarRotasion();
                    upsideDownTimer = 0;
                    Debug.Log("Atempted recovery");
                }
            }
            else upsideDownTimer = 0f;

            if (transform.position.y < killY)
                TryRespawn();
        }

        public void TryRespawn()
        {
            if(Time.time - _lastRespawnTime < respawnCooldown)
                return;
            _lastRespawnTime = Time.time;
            RespawnToLastCheckpoint();
        }

        public void RespawnToLastCheckpoint()
        {
            if (!checkpointManager)
                checkpointManager = FindAnyObjectByType<RacingGame._Game.Scripts.PCG.CheckpointManager>();

            if (!checkpointManager)
            {
                Debug.LogWarning("[Player] No CheckpointManager found for respawn.");
                return;
            }

            checkpointManager.GetLastCheckpointPose(out var pos, out var rot);

            // Rigidbody reset
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = pos;
                rb.rotation = rot;
                return;
            }

            // CharacterController reset
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                transform.SetPositionAndRotation(pos, rot);
                cc.enabled = true;
                return;
            }

            // Fallback
            transform.SetPositionAndRotation(pos, rot);
        }


        private void ResetCarRotasion()
        {
            Rigidbody rb = car.Rigidbody;

            rb.angularVelocity = Vector3.zero; // Stop spinning

            rb.AddForce(Vector3.up * upwardImpulse, ForceMode.Impulse); // Lift car to avoid clipping

            float yaw = transform.eulerAngles.y; // Preserve heading
            transform.rotation = Quaternion.Euler(0f, yaw, 0f); // Reset car rotasion
        }
    }
}
