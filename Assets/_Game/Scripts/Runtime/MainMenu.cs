using NoSlimes.UnityUtils.Runtime.ActionStack;
using System.Collections;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RacingGame
{
    [System.Serializable]
    public struct SettingNameMapping
    {
        public string SettingName;
        public Selectable UIElement;
        public float DefaultValue;
    }

    public class MainMenu : ActionStack
    {
        [Header("References")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private Button resetSettingsButton;

        [SerializeField] private TMP_InputField playerNameInputField;

        [Header("Settings Mapping")]
        [SerializeField] private SettingNameMapping[] settingsMappings;


        private MainMenuCarInputs mainMenuCarInputs;
        private bool preventSaving = false;

        private void Start()
        {
            LoadSettings();
            var inputComp = FindFirstObjectByType<Car>().GetCarComponent<CarInputComponent>();
            if (inputComp != null)
            {
                mainMenuCarInputs = new();
                inputComp.SetInputs(mainMenuCarInputs);
            }
        }

        private void OnEnable()
        {
            foreach (var mapping in settingsMappings)
            {
                if (mapping.UIElement == null)
                    continue;
                if (mapping.UIElement is Toggle toggle)
                {
                    toggle.onValueChanged.AddListener(_ => SaveSettings());
                }
                else if (mapping.UIElement is Slider slider)
                {
                    slider.onValueChanged.AddListener(_ => SaveSettings());
                }
                // Add more UI element types as needed
            }

            string savedName = PlayerPrefs.GetString("PlayerName", "Player");
            playerNameInputField.text = savedName;
            playerNameInputField.onEndEdit.AddListener(name =>
            {
                PlayerPrefs.SetString("PlayerName", name);
                PlayerPrefs.Save();
            });

            startGameButton.onClick.AddListener(StartGame);
            quitGameButton.onClick.AddListener(Application.Quit);
            resetSettingsButton.onClick.AddListener(ResetSettings);
        }

        public void StartGame()
        {
            StartCoroutine(StartGameCoroutine());
        }
        private IEnumerator StartGameCoroutine()
        {
            bool skipIntro = PlayerPrefs.GetInt("SkipIntro", 0) == 1;

            if (!skipIntro)
            {
                // Start moving forward
                mainMenuCarInputs?.SetMoveInput(Vector2.up);

                float timer = 0f;
                float introDuration = 3.0f;

                // Horn hold tracking
                bool hornActive = false;
                float hornEndTime = 0f;

                while (timer < introDuration)
                {
                    // If horn is not active, randomly start a horn press
                    if (!hornActive && UnityEngine.Random.value < 0.02f) // 2% chance per frame to start
                    {
                        hornActive = true;
                        // Hold horn for 0.1 to 0.5 seconds
                        hornEndTime = timer + UnityEngine.Random.Range(0.1f, 0.5f);
                    }

                    // Release horn when hold time ends
                    if (hornActive && timer >= hornEndTime)
                    {
                        hornActive = false;
                    }

                    mainMenuCarInputs?.SetHornInput(hornActive);

                    // Nitro press toward the end
                    if (timer > introDuration - 0.8f)
                    {
                        bool nitroPress = UnityEngine.Random.value < 0.3f; // 30% chance per frame
                        mainMenuCarInputs?.SetNitroInput(nitroPress);
                    }

                    timer += Time.deltaTime;
                    yield return null;
                }

                // Ensure inputs are reset
                mainMenuCarInputs?.SetHornInput(false);
                mainMenuCarInputs?.SetNitroInput(false);
            }

            SceneManager.LoadScene("GameScene");
        }


        #region Settings Loading and Saving
        private void LoadSettings()
        {
            preventSaving = true; 

            foreach (var mapping in settingsMappings)
            {
                if (mapping.UIElement == null || string.IsNullOrEmpty(mapping.SettingName))
                    continue;

                if (mapping.UIElement is Toggle toggle)
                {
                    bool value = PlayerPrefs.GetInt(mapping.SettingName, (int)mapping.DefaultValue) == 1;
                    toggle.isOn = value;
                }
                else if (mapping.UIElement is Slider slider)
                {
                    float value = PlayerPrefs.GetFloat(mapping.SettingName, mapping.DefaultValue);
                    slider.value = value;
                }
            }

            preventSaving = false; 
        }

        public void SaveSettings()
        {
            if (preventSaving)
            {
                return;
            }

            foreach (var mapping in settingsMappings)
            {
                if (mapping.UIElement == null || string.IsNullOrEmpty(mapping.SettingName))
                    continue;
                if (mapping.UIElement is Toggle toggle)
                {
                    PlayerPrefs.SetInt(mapping.SettingName, toggle.isOn ? 1 : 0);
                }
                else if (mapping.UIElement is Slider slider)
                {
                    PlayerPrefs.SetFloat(mapping.SettingName, slider.value);
                }
                // Add more UI element types as needed
            }
            PlayerPrefs.Save();
        }

        public void ResetSettings()
        {
            preventSaving = true;

            foreach (var mapping in settingsMappings)
            {
                if (mapping.UIElement == null || string.IsNullOrEmpty(mapping.SettingName))
                    continue;
                PlayerPrefs.DeleteKey(mapping.SettingName);
            }

            LoadSettings();
            preventSaving = false;

            SaveSettings();
        }
        #endregion
    }
}
