using System;
using UnityEngine;

namespace RacingGame
{
    public interface ICarInputs
    {
        Vector2 MoveInput { get; }
        bool HandBrakeInput { get; }
        public bool NitroInput { get; }
        public bool HornInput { get; }

        void Initialize(Transform ownerTransform);
        void PostInitialize() { }
        void Deinitialize() { }
    }
}
