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
            InputManager.Instance.RegisterActionCallback("Brake", OnBrake, UnityEngine.InputSystem.InputActionPhase.Performed);
            InputManager.Instance.RegisterActionCallback("Brake", OnBrake, UnityEngine.InputSystem.InputActionPhase.Canceled);
            InputManager.Instance.RegisterActionCallback("Nitro", OnNitro, UnityEngine.InputSystem.InputActionPhase.Performed);
            InputManager.Instance.RegisterActionCallback("Nitro", OnNitro, UnityEngine.InputSystem.InputActionPhase.Canceled);
        }

        private void OnDisable()
        {
            InputManager.Instance.OnMove -= OnMove;
            InputManager.Instance.UnregisterActionCallback("Brake", OnBrake, UnityEngine.InputSystem.InputActionPhase.Performed);
            InputManager.Instance.UnregisterActionCallback("Brake", OnBrake, UnityEngine.InputSystem.InputActionPhase.Canceled);
            InputManager.Instance.UnregisterActionCallback("Nitro", OnNitro, UnityEngine.InputSystem.InputActionPhase.Performed);
            InputManager.Instance.UnregisterActionCallback("Nitro", OnNitro, UnityEngine.InputSystem.InputActionPhase.Canceled);
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
