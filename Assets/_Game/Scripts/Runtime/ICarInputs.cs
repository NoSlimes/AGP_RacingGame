using System;
using UnityEngine;

namespace RacingGame
{
    public interface ICarInputs
    {
        Vector2 MoveInput { get; }
        bool BrakeInput { get; }
    }
}
