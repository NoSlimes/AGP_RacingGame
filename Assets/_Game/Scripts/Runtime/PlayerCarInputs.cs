using NoSlimes.UnityUtils.Input;
using System;
using UnityEngine;

namespace RacingGame
{
    [Serializable]
    public class PlayerCarInputs : ICarInputs, IDisposable
    {
        public Vector2 MoveInput { get; private set; }
        public bool HandBrakeInput { get; private set; }
        public bool NitroInput { get; private set; }
        public bool HornInput { get; private set; }

        public void Initialize(Transform ownerTransform)
        {
            InputManager.Instance.OnMove += OnMove;
            InputManager.Instance.RegisterActionCallback("Brake", OnBrake, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);
            InputManager.Instance.RegisterActionCallback("Nitro", OnNitro, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);  
            InputManager.Instance.RegisterActionCallback("Horn", OnHorn, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);
        }

        public void Deinitialize()
        {
            if (InputManager.Instance == null)
                return;

            InputManager.Instance.OnMove -= OnMove;
            InputManager.Instance.UnregisterActionCallback("Brake", OnBrake, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);
            InputManager.Instance.UnregisterActionCallback("Nitro", OnNitro, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);
            InputManager.Instance.UnregisterActionCallback("Horn", OnHorn, InputManager.InputEventType.Performed | InputManager.InputEventType.Canceled);
        }

        public void Dispose() => Deinitialize();

        private void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        private void OnBrake(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            HandBrakeInput = context.performed;
        }

        private void OnNitro(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            NitroInput = context.performed;
        }

        private void OnHorn(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            HornInput = context.performed;
        }
    }
}
