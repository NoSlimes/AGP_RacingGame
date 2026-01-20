using UnityEngine;

namespace RacingGame
{

    /*
     
    Implementation Steps

    1. Attach a Rigidbody: Ensure the object you want to track (like a car or a ball) has a Rigidbody component attached.

    2. Add this script as component to the object.

    3. Check the console / Dlog console: You can watch the metersPerSecond and other variables update in real-time in the Inspector window while the game is running.

    */


    [RequireComponent(typeof(Rigidbody))]
    public class Speedometer : MonoBehaviour
    {
        private Rigidbody rb;

        [Header("Settings")]
        public bool showDebugLogs = true;

        private float metersPerSecond;
        private float kilometersPerHour;
        private float milesPerHour;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }
        void FixedUpdate()
        {
            // get magnitude of velocity vector
            metersPerSecond = rb.linearVelocity.magnitude;

            // convert units
            kilometersPerHour = metersPerSecond * 3.6f;
            milesPerHour = metersPerSecond * 2.23694f;

            // debug to console
            if (showDebugLogs)
            {
                Debug.Log($"Speed: {metersPerSecond:F2} m/s | {kilometersPerHour:F1} km/h | {milesPerHour:F2} mph");
            }
        }
    }
}
