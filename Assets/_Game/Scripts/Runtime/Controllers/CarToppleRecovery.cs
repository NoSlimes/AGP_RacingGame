using NUnit.Framework.Interfaces;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RacingGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarTopleRecovery : MonoBehaviour
    {
        [Header("Flip Detection Configuration")]
        [SerializeField] private float upsideDownDotThreshold = 0.7f;
        [SerializeField] private float timeBeforeReset = 3f;

        [Header("Reset Configuration")]
        [SerializeField] private float upwardImpulse = 5f;
        [SerializeField] private float velociityDamping = 0.5f;

        private Rigidbody rigidbody;
        private float upsideDownTimer;

        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
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
        }

        private void ResetCarRotasion()
        {
            rigidbody.angularVelocity = Vector3.zero; // Stop spinning
            rigidbody.linearVelocity *= velociityDamping; // Reduce Speed

            rigidbody.AddForce(Vector3.up * upwardImpulse, ForceMode.Impulse); // Lift car to avoid clipping

            float yaw = transform.eulerAngles.y; // Preserve heading
            transform.rotation = Quaternion.Euler(0f, yaw, 0f); // Reset car rotasion
        }
    }
}
