using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PrototypeVolumeControl : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text volumeLabel;

    private void OnEnable()
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(SetVolume);
        Refresh();
    }

    private void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(SetVolume);
    }

    public void Refresh()
    {
        if (slider != null) slider.SetValueWithoutNotify(AudioListener.volume);
        RefreshLabel();
    }

    private void SetVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (volumeLabel != null) volumeLabel.text = $"Volume  {Mathf.RoundToInt(AudioListener.volume * 100f)}%";
    }
}
