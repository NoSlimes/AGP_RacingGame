using UnityEngine;
using TMPro;

namespace RacingGame
{
    public class SpeedometerUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI kmh_UI;
        [SerializeField] private TextMeshProUGUI mph_UI;
        [SerializeField] private TextMeshProUGUI ms_UI;

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
