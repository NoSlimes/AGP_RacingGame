using UnityEngine;

namespace RacingGame
{
    public class SliderTextUpdater : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Slider slider;
        [SerializeField] private TMPro.TextMeshProUGUI text;

        [SerializeField] private string prefix = "";

        private void Start()
        {
            slider.onValueChanged.AddListener(OnSliderValueChanged);

            OnSliderValueChanged(slider.value);
        }

        private void OnSliderValueChanged(float value)
        {
            if (slider != null && text != null)
            {
                text.text = $"{prefix} {value:0.##}";
            }
        }
    }
}
