using UnityEngine;

namespace RacingGame
{
    public class TickableBehaviour : MonoBehaviour, ITickable
    {
        private void OnEnable()
        {
            GameManager.Instance.RegisterTickable(this);
        }

        private void OnDisable()
        {
            GameManager.Instance.UnregisterTickable(this);
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
