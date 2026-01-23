using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    public class PauseUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        [SerializeField] private Button quitButton;

        private void OnEnable()
        {
            GameManager.Instance.StateChanged += OnStateChanged;

            quitButton.onClick.AddListener(() =>
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            });
        }

        private void OnDisable()
        {
            GameManager.Instance.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(StateMachine.State newState, StateMachine.State oldState)
        {
            root.SetActive(newState is PauseState);
        }
    }
}
