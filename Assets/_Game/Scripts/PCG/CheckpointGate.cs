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
            
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;

            // Find player car
            var identity = rb.GetComponent<RacingGame._Game.Scripts.Runtime.CarIdentity>();
            if (identity == null) return;

            if (!identity.IsPlayerControlled)
                return;

            manager.NotifyCheckpointPassed(checkpointIndex, rb.transform);
        }

    }
}