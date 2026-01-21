using UnityEngine;
using System;

namespace RacingGame
{
    public class Speedometer : MonoBehaviour, ICarComponent
    {
        private Car targetCar;

        [Header("Settings")]
        [SerializeField] private bool showDebugLogs = true;

        public float MetersPerSecond { get; private set; }
        public float KilometersPerHour { get; private set; }
        public float MilesPerHour { get; private set; }

        public void Initialize(Car ownerCar)
        {
            targetCar = ownerCar;
        }

        public void FixedTickComponent()
        {
            if (targetCar == null) return;

            // get magnitude of velocity vector
            MetersPerSecond = targetCar.Rigidbody.linearVelocity.magnitude;

            // convert units
            KilometersPerHour = MetersPerSecond * 3.6f;
            MilesPerHour = MetersPerSecond * 2.23694f;

            // debug to console
            if (showDebugLogs)
            {
                Debug.Log($"Speed: {MetersPerSecond:F2} m/s | {KilometersPerHour:F1} km/h | {MilesPerHour:F2} mph");
            }
        }
    }
}
