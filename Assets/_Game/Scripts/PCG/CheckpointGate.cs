using UnityEngine;

namespace RacingGame._Game.Scripts.PCG
{
    [RequireComponent(typeof(BoxCollider))]
    public class CheckpointGate : MonoBehaviour
    {
        [HideInInspector] public CheckpointManager manager;
        [HideInInspector] public int checkpointIndex;

        [Header("Filter")]
        public string playerTag = "Player";

        private void Reset()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform.root;

            if (!string.IsNullOrEmpty(playerTag) && !root.CompareTag(playerTag))
                return;

            if (manager == null) return;

            manager.NotifyCheckpointPassed(checkpointIndex, root);
        }
    }
}