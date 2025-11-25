using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class OptionSliderRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [Header("ID")]
    public string actionId;

    [Header("UI")]
    public TextMeshProUGUI labelText;
    public Slider slider;
    public TextMeshProUGUI valueText;

    [Header("Format")]
    public bool useInteger = true;
    public int decimalPlaces = 1;

    [Header("Summary")]
    [TextArea]
    public string summaryTextForRow;

    PauseMenuController pauseMenu;
    bool isInitialized = false;

    void Awake() {
        pauseMenu = FindFirstObjectByType<PauseMenuController>();
    }

    void Start() {
        if (!string.IsNullOrEmpty(actionId) && slider != null && SettingsManager.Instance != null) {
            float defaultValue = slider.value;
            float loadedValue = SettingsManager.Instance.GetFloat(actionId, defaultValue);
            slider.value = loadedValue;
        }

        RefreshValueText();
        isInitialized = true;
    }

    public void OnSliderValueChanged(float value) {
        RefreshValueText();

        if (!string.IsNullOrEmpty(actionId) && SettingsManager.Instance != null) {
            SettingsManager.Instance.SetFloat(actionId, value);
        }

        if (isInitialized && pauseMenu != null) {
            pauseMenu.MarkSettingChanged();
        }
    }

    void RefreshValueText() {
        if (valueText == null || slider == null) {
            return;
        }

        if (useInteger) {
            valueText.text = Mathf.RoundToInt(slider.value).ToString();
        } else {
            float v = slider.value;
            valueText.text = v.ToString("F" + decimalPlaces);
        }
    }

    public void SetValue(float value, bool notifyChange = false) {
        if (slider == null) {
            return;
        }

        slider.value = value;
        RefreshValueText();

        if (!string.IsNullOrEmpty(actionId) && SettingsManager.Instance != null) {
            SettingsManager.Instance.SetFloat(actionId, value);
        }

        if (notifyChange && pauseMenu != null) {
            pauseMenu.MarkSettingChanged();
        }
    }

    public float GetValue() {
        if (slider == null) {
            return 0f;
        }

        return slider.value;
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (pauseMenu != null && !string.IsNullOrEmpty(summaryTextForRow)) {
            pauseMenu.ShowSummary(summaryTextForRow);
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (pauseMenu != null) {
            pauseMenu.ClearSummary();
        }
    }
}