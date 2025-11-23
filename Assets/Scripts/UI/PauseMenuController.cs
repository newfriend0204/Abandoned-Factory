using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    [TextArea]
    public string defaultSummaryText = "";

    bool isPaused = false;

    bool hasUnsavedChanges = false;

    void Start() {
        Time.timeScale = 1f;

        pauseRoot.SetActive(false);
        pauseScreenRoot.SetActive(false);
        settingsScreenRoot.SetActive(false);

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
        Debug.Log("[Settings] 변경 사항 감지됨");
    }

    public void ShowSummary(string text) {
        if (summaryText == null)
            return;

        summaryText.text = text;
    }

    public void ClearSummary() {
        if (summaryText == null)
            return;

        summaryText.text = defaultSummaryText;
    }

    void OnPressEscape() {
        if (isPaused && inputSettingsManager != null && inputSettingsManager.IsPopupOpen)
            return;

        if (!isPaused)
            TogglePause();
        else {
            if (settingsScreenRoot.activeSelf)
                OnClickBack();
            else
                TogglePause();
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
    }

    public void OnClickContinue() {
        Debug.Log("[PauseMenu] Continue button pressed");
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
        Debug.Log("[Settings] Back button pressed");

        settingsScreenRoot.SetActive(false);
        pauseScreenRoot.SetActive(true);
    }

    public void OnClickSave() {
        Debug.Log("[Settings] Save button pressed (txt 저장은 나중에)");
        hasUnsavedChanges = false;
    }

    public void OnClickTabGeneral() {
        Debug.Log("[Settings] Tab: General");
        ShowGeneralPanel();
        SetActiveTab(generalTabButton);
        ClearSummary();
    }

    public void OnClickTabGraphics() {
        Debug.Log("[Settings] Tab: Graphics");
        generalPanel.SetActive(false);
        graphicsPanel.SetActive(true);
        audioPanel.SetActive(false);
        controlsPanel.SetActive(false);

        SetActiveTab(graphicsTabButton);
        ClearSummary();
    }

    public void OnClickTabAudio() {
        Debug.Log("[Settings] Tab: Audio");
        generalPanel.SetActive(false);
        graphicsPanel.SetActive(false);
        audioPanel.SetActive(true);
        controlsPanel.SetActive(false);

        SetActiveTab(audioTabButton);
        ClearSummary();
    }

    public void OnClickTabControls() {
        Debug.Log("[Settings] Tab: Controls");
        generalPanel.SetActive(false);
        graphicsPanel.SetActive(false);
        audioPanel.SetActive(false);
        controlsPanel.SetActive(true);

        SetActiveTab(controlsTabButton);
        ClearSummary();
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
}