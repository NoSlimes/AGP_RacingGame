using NoSlimes.UnityUtils.Input;
using System;
using UnityEngine;

namespace RacingGame
{
    public interface ICarInputs
    {
        Vector2 MoveInput { get; }
        bool BrakeInput { get; }
    }

    public class AIController : MonoBehaviour, ICarInputs
    {
        public Vector2 MoveInput { get; private set; }
        public bool BrakeInput { get; private set; }

        private void Update()
        {
            MoveInput = new Vector2(UnityEngine.Random.Range(-1f, 1f), 1f);
            BrakeInput = UnityEngine.Random.value < 0.1f;
        }
    }

    public class PlayerController : MonoBehaviour, ICarInputs
    {


        public Vector2 MoveInput { get; private set; }
        public bool BrakeInput { get; private set; }

        private void OnEnable()
        {
            InputManager.Instance.OnMove += OnMove;
            InputManager.Instance.RegisterActionCallback("Brake", OnBrake);
        }

        private void OnDisable()
        {
            InputManager.Instance.OnMove -= OnMove;
            InputManager.Instance.UnregisterActionCallback("Brake", OnBrake);
        }

        private void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        private void OnBrake(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            BrakeInput = context.performed;
        }
    }
}
