using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace RacingGame
{
    public class CarPlacementsUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text placementsText;

        private void OnEnable()
        {
            GameManager.Instance.StateMachine.GetState<GameState>().OnCarPlacementsChanged += OnCarPlacementsChanged;
        }

        private void OnDisable()
        {
            GameManager.Instance.StateMachine.GetState<GameState>().OnCarPlacementsChanged -= OnCarPlacementsChanged;
        }

        private void OnCarPlacementsChanged(IReadOnlyList<Car> list)
        {
            StringBuilder sb = new();

            foreach (Car car in list)
            {
                sb.AppendLine(car.CarName);
            }

            placementsText.text = sb.ToString();
        }
    }
}
