using UnityEngine;

namespace RacingGame._Game.Scripts.PCG
{
    [RequireComponent(typeof(BoxCollider))]
    public class CheckpointGate : MonoBehaviour
    {
        [HideInInspector] public CheckpointManager manager;
        [HideInInspector] public int checkpointIndex;

        private void Reset()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (manager == null) return;

            var rb = other.attachedRigidbody;
            if (rb == null) return;

            // Find car
            if (rb.TryGetComponent(out Car car))
            {
                manager.NotifyCheckpointPassed(checkpointIndex, car);
            }
        }

    }
}