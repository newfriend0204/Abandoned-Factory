using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class KeybindRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [Header("ID")]
    public string actionId;

    [Header("UI")]
    public TextMeshProUGUI labelText;
    public TextMeshProUGUI key1Text;
    public TextMeshProUGUI key2Text;

    [Header("Summary")]
    [TextArea]
    public string summaryTextForRow;

    InputSettingsManager inputSettingsManager;
    PauseMenuController pauseMenu;

    void Awake() {
        inputSettingsManager = FindFirstObjectByType<InputSettingsManager>();
        pauseMenu = FindFirstObjectByType<PauseMenuController>();

        if (inputSettingsManager != null) {
            inputSettingsManager.RegisterRow(this);
        }
    }

    public void RefreshDisplay(KeyCode primary, KeyCode secondary, InputSettingsManager manager) {
        if (key1Text != null) {
            key1Text.text = manager.FormatKeyName(primary);
        }

        if (key2Text != null) {
            key2Text.text = manager.FormatKeyName(secondary);
        }
    }

    public void OnClickKey1() {
        if (inputSettingsManager == null || string.IsNullOrEmpty(actionId)) {
            return;
        }

        inputSettingsManager.StartListeningKey(actionId, 0);
    }

    public void OnClickKey2() {
        if (inputSettingsManager == null || string.IsNullOrEmpty(actionId)) {
            return;
        }

        inputSettingsManager.StartListeningKey(actionId, 1);
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