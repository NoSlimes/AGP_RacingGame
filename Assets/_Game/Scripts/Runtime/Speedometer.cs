using UnityEngine;
using TMPro;

namespace RacingGame
{

    /*
     
    Implementation Steps

    1. Lägg till this script as SpeedManager or speedometer or some shit.

    2. Attach ett objekt to track in targetObject: Ensure the object you want to track (like a car or a ball) has a Rigidbody component attached.

    3. Check the console / Dlog console: You can watch the metersPerSecond and other variables update in real-time in the Inspector window while the game is running.

    4. (Optional) Link UI Text Elements from Assets/_Game/Prefabs/UI/Speedometer in a canvas with the SpeedManager: If you want to display the speed on the screen, 
        link the TextMeshProUGUI fields (kmh_UI, mph_UI, ms_UI)

    */

    public class Speedometer : MonoBehaviour
    {
        // ----------------------
        //
        // There is an event in GameManager to get the player car when it spawns. Use that.
        // Also UI should preferably be event driven and not be referenced directly with SerializeField.
        //
        // ----------------------
        [SerializeField] private Transform targetObject;
        [SerializeField] private TextMeshProUGUI kmh_UI;
        [SerializeField] private TextMeshProUGUI mph_UI;
        [SerializeField] private TextMeshProUGUI ms_UI;

        private Rigidbody targetRb;

        [Header("Settings")]
        [SerializeField] private bool showDebugLogs = true;

        private float metersPerSecond;
        private float kilometersPerHour;
        private float milesPerHour;

        void FixedUpdate()
        {
            if (targetObject == null) return;

            if (targetRb == null)
            {
                targetRb = targetObject.GetComponent<Rigidbody>();

                if (targetRb == null)
                {
                    Debug.LogError("Target object does not have a Rigidbody component.");
                    return;
                }
            }

            // get magnitude of velocity vector
            metersPerSecond = targetRb.linearVelocity.magnitude;

            // convert units
            kilometersPerHour = metersPerSecond * 3.6f;
            milesPerHour = metersPerSecond * 2.23694f;

            // update UI text if assigned
            if (kmh_UI != null)
            {
                kmh_UI.text = $"{kilometersPerHour:F1} km/h";
            }

            if (mph_UI != null)
            {
                mph_UI.text = $"{milesPerHour:F2} mph";
            }

            if (ms_UI != null)
            {
                ms_UI.text = $"{metersPerSecond:F2} m/s";
            }

            // debug to console
            if (showDebugLogs)
            {
                Debug.Log($"Speed: {metersPerSecond:F2} m/s | {kilometersPerHour:F1} km/h | {milesPerHour:F2} mph");
            }
        }
    }
}
