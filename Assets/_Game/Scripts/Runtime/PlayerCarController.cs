using NoSlimes.UnityUtils.Input;
using UnityEngine;

namespace RacingGame
{
    public class PlayerCarController : MonoBehaviour, ICarInputs
    {
        public Vector2 MoveInput { get; private set; }
        public bool BrakeInput { get; private set; }
        public bool NitroInput { get; private set; }

        private void OnEnable()
        {
            InputManager.Instance.OnMove += OnMove;
            InputManager.Instance.RegisterActionCallback("Brake", OnBrake, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);
            InputManager.Instance.RegisterActionCallback("Nitro", OnNitro, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);
        }

        private void OnDisable()
        {
            if (!InputManager.Instance)
                return;

            InputManager.Instance.OnMove -= OnMove;
            InputManager.Instance.UnregisterActionCallback("Brake", OnBrake, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);
            InputManager.Instance.UnregisterActionCallback("Nitro", OnNitro, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);
        }

        private void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        private void OnBrake(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            BrakeInput = context.performed;
        }

        private void OnNitro(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            NitroInput = context.performed;
        }
    }
}
