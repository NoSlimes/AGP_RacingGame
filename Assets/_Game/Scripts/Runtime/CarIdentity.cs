using UnityEngine;

namespace RacingGame._Game.Scripts.Runtime
{
    public class CarIdentity : MonoBehaviour
    {
        [Header("Ownership")]
        public bool IsPlayerControlled = false;
        
        // For multiplayer maybe?
        public int PlayerId = 0;

        [Header("Debug")]
        public bool logOwnershipOnEnable = false;

        private void OnEnable()
        {
            if (logOwnershipOnEnable)
            {
                Debug.Log($"[CarIdentity] {name} IsPlayerControlled={IsPlayerControlled} PlayerId={PlayerId}");
            }
        }
    }
}