using System;
using UnityEngine;

namespace RacingGame
{
    public interface ICarInputs
    {
        Vector2 MoveInput { get; }
        bool BrakeInput { get; }
        public bool NitroInput { get; }

        void Initialize(Transform ownerTransform);
        void PostInitialize() { }
        void Deinitialize() { }
    }
}
