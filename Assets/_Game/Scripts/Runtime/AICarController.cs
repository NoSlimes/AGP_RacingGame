using UnityEngine;

namespace RacingGame
{
    public class AICarController : MonoBehaviour, ICarInputs
    {
        public Vector2 MoveInput { get; private set; }
        public bool BrakeInput { get; private set; }
        public bool NitroInput { get; private set; }

        private void Update()
        {
            MoveInput = new Vector2(UnityEngine.Random.Range(-1f, 1f), 1f);
            BrakeInput = UnityEngine.Random.value < 0.1f;
        }
    }
}
