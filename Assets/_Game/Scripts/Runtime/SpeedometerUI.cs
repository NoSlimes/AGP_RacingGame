using UnityEngine;
using TMPro;

namespace RacingGame
{
    public class SpeedometerUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI kmh_UI;
        [SerializeField] private TextMeshProUGUI mph_UI;
        [SerializeField] private TextMeshProUGUI ms_UI;

        [SerializeField] private float maxSpeed = 200f; // The max speed on your gauge

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI digitalText;
        [SerializeField] private RectTransform needle; // The needle's Transform

        [Header("Needle Settings")]
        [SerializeField] private float zeroSpeedAngle = 90f; // Angle when speed is 0
        [SerializeField] private float maxSpeedAngle = -90f;  // Angle when speed is max

        private Speedometer speedometer;

        private void OnEnable() => GameManager.Instance.OnPlayerCarAssigned += HandlePlayerCarAssigned;
        private void OnDisable() => GameManager.Instance.OnPlayerCarAssigned -= HandlePlayerCarAssigned;

        private void HandlePlayerCarAssigned(Car car)
        {
            speedometer = car.GetCarComponent<Speedometer>();

            if (speedometer == null)
                enabled = false;
        }

        private void Update()
        {
            if(speedometer == null) 
                return;

            if (digitalText != null)
            {
                digitalText.text = $"{speedometer.KilometersPerHour:F1} km/h";
                // digitalText.text = speedometer.MetersPerSecond.ToString("F0");
            }

            if (needle !=null)
            {
                float speedNormalized = speedometer.KilometersPerHour / maxSpeed;
                float targetAngle = Mathf.Lerp(zeroSpeedAngle, maxSpeedAngle, speedNormalized);

                needle.eulerAngles = new Vector3(0, 0, targetAngle);
            }

            // update UI text if assigned
            if (kmh_UI != null)
            {
                kmh_UI.text = $"{speedometer.KilometersPerHour:F1} km/h";       
            }

            if (mph_UI != null)
            {
                mph_UI.text = $"{speedometer.MilesPerHour:F2} mph";
            }

            if (ms_UI != null)
            {
                ms_UI.text = $"{speedometer.MetersPerSecond:F2} m/s";
            }
        }
    }
}
