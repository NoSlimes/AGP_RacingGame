using UnityEngine;

namespace RacingGame
{
    public class TickableBehaviour : MonoBehaviour, ITickable
    {
        private bool fallbackToUpdate = false;

        private void OnEnable()
        {
            if(!GameManager.Instance)
            {
                fallbackToUpdate = true;

                Debug.LogWarning($"[{nameof(TickableBehaviour)}] No GameManager instance found in the scene. Falling back to Update loop for ticking. This may have performance implications.", this);
                return;
            }

            GameManager.Instance.RegisterTickable(this);
        }

        private void OnDisable()
        {
            if (!GameManager.Instance)
            {
                return;
            }

            GameManager.Instance.UnregisterTickable(this);
        }

        private void Update()
        {
            if (fallbackToUpdate)
            {
                Tick();
            }
        }

        private void LateUpdate()
        {
            if (fallbackToUpdate)
            {
                LateTick();
            }
        }

        private void FixedUpdate()
        {
            if (fallbackToUpdate)
            {
                FixedTick();
            }
        }

        public virtual void Tick()
        {
        }

        public virtual void LateTick()
        {
        }

        public virtual void FixedTick()
        {
        }
    }
}
