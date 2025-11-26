using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using TMPro;

public class PauseMenuController : MonoBehaviour {
    [Header("Roots")]
    public GameObject pauseRoot;
    public GameObject pauseScreenRoot;
    public GameObject settingsScreenRoot;

    [Header("Setting Panels")]
    public GameObject generalPanel;
    public GameObject graphicsPanel;
    public GameObject audioPanel;
    public GameObject controlsPanel;

    [Header("Tab Buttons")]
    public Button generalTabButton;
    public Button graphicsTabButton;
    public Button audioTabButton;
    public Button controlsTabButton;

    [Header("Input Settings")]
    public InputSettingsManager inputSettingsManager;

    [Header("Summary UI")]
    public TextMeshProUGUI summaryText;
    public LocalizedString defaultSummaryText;

    [Header("Save Screen")]
    public GameObject saveScreenRoot;

    bool isPaused = false;
    bool hasUnsavedChanges = false;

    Action pendingAction = null;

    void Start() {
        Time.timeScale = 1f;

        pauseRoot.SetActive(false);
        pauseScreenRoot.SetActive(false);
        settingsScreenRoot.SetActive(false);

        if (saveScreenRoot != null)
            saveScreenRoot.SetActive(false);

        ShowGeneralPanel();
        SetActiveTab(generalTabButton);

        ClearSummary();
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape))
            OnPressEscape();
    }

    public void MarkSettingChanged() {
        hasUnsavedChanges = true;
    }

    public void ShowSummary(string text) {
        if (summaryText == null)
            return;

        summaryText.text = text;
    }

    public void ClearSummary() {
        if (summaryText == null)
            return;

        summaryText.text = defaultSummaryText.GetLocalizedString();
    }

    void OnPressEscape() {
        if (isPaused && inputSettingsManager != null && inputSettingsManager.IsPopupOpen)
            return;

        if (isPaused && saveScreenRoot != null && saveScreenRoot.activeSelf) {
            OnClickSaveScreenCancel();
            return;
        }

        if (!isPaused) {
            TogglePause();
        } else {
            if (settingsScreenRoot.activeSelf) {
                RequestLeaveSettings();
            } else {
                TogglePause();
            }
        }
    }

    void TogglePause() {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    void PauseGame() {
        isPaused = true;
        Time.timeScale = 0f;

        pauseRoot.SetActive(true);
        pauseScreenRoot.SetActive(true);
        settingsScreenRoot.SetActive(false);
    }

    void ResumeGame() {
        isPaused = false;
        Time.timeScale = 1f;

        pauseRoot.SetActive(false);
        pauseScreenRoot.SetActive(false);
        settingsScreenRoot.SetActive(false);

        if (saveScreenRoot != null)
            saveScreenRoot.SetActive(false);

        pendingAction = null;
    }

    public void OnClickContinue() {
        ResumeGame();
    }

    public void OnClickSettings() {
        pauseScreenRoot.SetActive(false);
        settingsScreenRoot.SetActive(true);

        ShowGeneralPanel();
        SetActiveTab(generalTabButton);

        hasUnsavedChanges = false;
        ClearSummary();
    }

    public void OnClickCheckpoint() {
        Debug.Log("[PauseMenu] Checkpoint button pressed");
    }

    public void OnClickQuit() {
        Debug.Log("[PauseMenu] Quit button pressed");
    }

    public void OnClickBack() {
        RequestLeaveSettings();
    }

    void RequestLeaveSettings() {
        RequestActionWithSaveCheck(() => {
            settingsScreenRoot.SetActive(false);
            pauseScreenRoot.SetActive(true);
        });
    }

    public void OnClickSave() {

        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SaveToFile();

        if (inputSettingsManager != null)
            inputSettingsManager.SaveKeyBindingsToFile();

        hasUnsavedChanges = false;
    }

    public void OnClickTabGeneral() {
        RequestActionWithSaveCheck(() => {
            ShowGeneralPanel();
            SetActiveTab(generalTabButton);
            ClearSummary();
        });
    }

    public void OnClickTabGraphics() {
        RequestActionWithSaveCheck(() => {
            generalPanel.SetActive(false);
            graphicsPanel.SetActive(true);
            audioPanel.SetActive(false);
            controlsPanel.SetActive(false);

            SetActiveTab(graphicsTabButton);
            ClearSummary();
        });
    }

    public void OnClickTabAudio() {
        RequestActionWithSaveCheck(() => {
            generalPanel.SetActive(false);
            graphicsPanel.SetActive(false);
            audioPanel.SetActive(true);
            controlsPanel.SetActive(false);

            SetActiveTab(audioTabButton);
            ClearSummary();
        });
    }

    public void OnClickTabControls() {
        RequestActionWithSaveCheck(() => {
            generalPanel.SetActive(false);
            graphicsPanel.SetActive(false);
            audioPanel.SetActive(false);
            controlsPanel.SetActive(true);

            SetActiveTab(controlsTabButton);
            ClearSummary();
        });
    }

    void RequestActionWithSaveCheck(Action action) {
        if (hasUnsavedChanges && saveScreenRoot != null) {
            pendingAction = action;
            saveScreenRoot.SetActive(true);
        } else {
            if (action != null)
                action.Invoke();
        }
    }

    void ShowGeneralPanel() {
        generalPanel.SetActive(true);
        graphicsPanel.SetActive(false);
        audioPanel.SetActive(false);
        controlsPanel.SetActive(false);
    }

    void SetActiveTab(Button activeButton) {
        SetTabButtonColors(generalTabButton, activeButton == generalTabButton);
        SetTabButtonColors(graphicsTabButton, activeButton == graphicsTabButton);
        SetTabButtonColors(audioTabButton, activeButton == audioTabButton);
        SetTabButtonColors(controlsTabButton, activeButton == controlsTabButton);
    }

    void SetTabButtonColors(Button button, bool isActive) {
        ColorBlock colors = button.colors;
        Color normal = colors.normalColor;
        Color selected = colors.selectedColor;

        float aNormal = normal.a;
        float aSelected = selected.a;

        if (isActive) {
            normal = new Color(1f, 1f, 1f, aNormal);
            selected = new Color(1f, 1f, 1f, aSelected);
        } else {
            normal = new Color(0f, 1f, 0f, aNormal);
            selected = new Color(0f, 1f, 0f, aSelected);
        }

        colors.normalColor = normal;
        colors.selectedColor = selected;
        button.colors = colors;
    }

    public void ToggleColor() {
        GameObject go = EventSystem.current.currentSelectedGameObject;
        Button button = go.GetComponent<Button>();

        ColorBlock colors = button.colors;
        Color normal = colors.normalColor;
        Color selected = colors.selectedColor;

        float aNormal = normal.a;
        float aSelected = selected.a;

        if (normal.r < 0.5f && normal.g > 0.5f && normal.b < 0.5f) {
            normal = new Color(1f, 1f, 1f, aNormal);
            selected = new Color(1f, 1f, 1f, aSelected);
        } else {
            normal = new Color(0f, 1f, 0f, aNormal);
            selected = new Color(0f, 1f, 0f, aSelected);
        }

        colors.normalColor = normal;
        colors.selectedColor = selected;
        button.colors = colors;
    }

    public void OnClickResetToDefaults() {
        if (SettingsManager.Instance != null) {
            SettingsManager.Instance.ResetToDefaults();
        }

        OptionSliderRow[] sliderRows = FindObjectsByType<OptionSliderRow>(FindObjectsSortMode.None);
        for (int i = 0; i < sliderRows.Length; i++) {
            OptionSliderRow row = sliderRows[i];
            if (row == null || string.IsNullOrEmpty(row.actionId) || row.slider == null)
                continue;

            float defaultValue = row.slider.value;
            float v = SettingsManager.Instance != null
                ? SettingsManager.Instance.GetFloat(row.actionId, defaultValue)
                : defaultValue;

            row.SetValue(v, false);
        }

        OptionToggleRow[] toggleRows = FindObjectsByType<OptionToggleRow>(FindObjectsSortMode.None);
        for (int i = 0; i < toggleRows.Length; i++) {
            OptionToggleRow row = toggleRows[i];
            if (row == null || string.IsNullOrEmpty(row.actionId))
                continue;

            int defaultIndex = row.selectedIndex;
            int idx = SettingsManager.Instance != null
                ? SettingsManager.Instance.GetInt(row.actionId, defaultIndex)
                : defaultIndex;

            row.SetSelectedIndex(idx, false);
        }

        if (inputSettingsManager != null) {
            inputSettingsManager.ResetKeyBindingsToDefault();
        }

        MarkSettingChanged();
    }

    public void OnClickSaveScreenSaveAndExit() {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SaveToFile();

        if (inputSettingsManager != null)
            inputSettingsManager.SaveKeyBindingsToFile();

        hasUnsavedChanges = false;

        if (saveScreenRoot != null)
            saveScreenRoot.SetActive(false);

        if (pendingAction != null) {
            Action act = pendingAction;
            pendingAction = null;
            act.Invoke();
        }
    }

    public void OnClickSaveScreenDontSaveAndExit() {
        if (SettingsManager.Instance != null) {
            SettingsManager.Instance.ReloadFromFile();
        }

        OptionSliderRow[] sliderRows = FindObjectsByType<OptionSliderRow>(FindObjectsSortMode.None);
        for (int i = 0; i < sliderRows.Length; i++) {
            OptionSliderRow row = sliderRows[i];
            if (row == null || string.IsNullOrEmpty(row.actionId) || row.slider == null) {
                continue;
            }

            float defaultValue = row.slider.value;
            float v = SettingsManager.Instance != null
                ? SettingsManager.Instance.GetFloat(row.actionId, defaultValue)
                : defaultValue;

            row.SetValue(v, false);
        }

        OptionToggleRow[] toggleRows = FindObjectsByType<OptionToggleRow>(FindObjectsSortMode.None);
        for (int i = 0; i < toggleRows.Length; i++) {
            OptionToggleRow row = toggleRows[i];
            if (row == null || string.IsNullOrEmpty(row.actionId)) {
                continue;
            }

            int defaultIndex = row.selectedIndex;
            int idx = SettingsManager.Instance != null
                ? SettingsManager.Instance.GetInt(row.actionId, defaultIndex)
                : defaultIndex;

            row.SetSelectedIndex(idx, false);
        }

        if (inputSettingsManager != null) {
            inputSettingsManager.ReloadKeyBindingsFromFileFully();
        }

        hasUnsavedChanges = false;

        if (saveScreenRoot != null) {
            saveScreenRoot.SetActive(false);
        }

        if (pendingAction != null) {
            Action act = pendingAction;
            pendingAction = null;
            act.Invoke();
        }
    }

    public void OnClickSaveScreenCancel() {
        if (saveScreenRoot != null)
            saveScreenRoot.SetActive(false);

        pendingAction = null;
    }
}