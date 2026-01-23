using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace RacingGame
{
    public class VictoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private TMP_Text resultsText;
        [SerializeField] private TMP_Text playerFinishedText; // NEW: popup for player finish
        [SerializeField] private TMP_Text lapText;

        [SerializeField] private Button mainMenuButton;

        private GameState gameState;

        private void Awake()
        {
            if (root != null)
                root.SetActive(false);
            if (playerFinishedText != null)
                playerFinishedText.gameObject.SetActive(false);
        }

        private void Start()
        {
            gameState = GameManager.Instance.StateMachine.GetState<GameState>();
            if (gameState != null)
            {
                gameState.OnRaceFinished += OnRaceFinished;
                gameState.OnCarFinishedRace += OnCarFinishedRace;
                gameState.OnCarLapped += OnCarLapped;
            }

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"));
        }

        private void OnDestroy()
        {
            if (gameState != null)
            {
                gameState.OnRaceFinished -= OnRaceFinished;
                gameState.OnCarFinishedRace -= OnCarFinishedRace;
            }
        }

        private void OnCarLapped(Car car, int currentLap)
        {
            if (car == GameManager.Instance.PlayerCar && lapText != null)
            {
                var totalLaps = GameManager.Instance.LapsToComplete;
                lapText.text = $"LAP: {currentLap + 1}/{totalLaps}";
            }
        }

        private void OnRaceFinished(IReadOnlyList<(Car car, float time)> results)
        {
            if (root != null)
                root.SetActive(true);

            if (results.Count > 0 && winnerText != null)
                winnerText.text = $"WINNER: <color=green>{results[0].car.CarName}</color>";

            if (resultsText != null)
                resultsText.text = BuildResultsString(results);

            if (playerFinishedText != null)
                playerFinishedText.gameObject.SetActive(false);
        }

        private void OnCarFinishedRace(Car car, int placement)
        {
            // Only react if this is the player's car
            if (car == GameManager.Instance.PlayerCar && playerFinishedText != null)
            {
                playerFinishedText.gameObject.SetActive(true);
                playerFinishedText.text = $"You finished {GetOrdinal(placement)}!";
            }
        }

        private string BuildResultsString(IReadOnlyList<(Car car, float time)> results)
        {
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < results.Count; i++)
            {
                var (car, time) = results[i];
                string line = $"{i + 1}. {car.CarName} - ";
                line += time < 0f ? "DNF" : FormatTime(time);

                // Highlight first place
                if (i == 0)
                    sb.AppendLine($"<color=green>{line}</color>");
                else
                    sb.AppendLine(line);
            }

            return sb.ToString();
        }

        private string FormatTime(float time)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            float seconds = time % 60f;
            return $"{minutes:0}:{seconds:00.000}";
        }

        private string GetOrdinal(int number)
        {
            if (number <= 0) return number.ToString();

            int n100 = number % 100;
            if (n100 == 11 || n100 == 12 || n100 == 13)
                return number + "th";

            int n10 = number % 10;
            switch (n10)
            {
                case 1: return number + "st";
                case 2: return number + "nd";
                case 3: return number + "rd";
                default: return number + "th";
            }
        }
    }
}
