using System;
using UnityEngine;

namespace RacingGame
{
    [Serializable]
    public class MainMenuCarInputs : ICarInputs
    {
        public Vector2 MoveInput { get; private set; }
        public bool HandBrakeInput { get; private set; }
        public bool NitroInput { get; private set; }
        public bool HornInput { get; private set; }
        public void Initialize(Transform ownerTransform)
        {
        }
        public void Deinitialize()
        {
        }

        public void SetMoveInput(Vector2 input)
        {
            MoveInput = input;
        }

        public void SetHornInput(bool input)
        {
            HornInput = input;
        }

        public void SetNitroInput(bool input)
        {
            NitroInput = input;
        }
    }
}
