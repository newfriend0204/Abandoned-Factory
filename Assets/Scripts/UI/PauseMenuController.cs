using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PauseMenuController : MonoBehaviour {
    [Header("Root")]
    public GameObject pauseRoot;
    public CanvasGroup pauseCanvas;

    [System.Serializable]
    public class CommandRow {
        public Button button;
        public TextMeshProUGUI leftCursor;
        public TextMeshProUGUI label;
        public TextMeshProUGUI rightCursor;
    }

    [Header("Command Rows")]
    public CommandRow resumeRow;
    public CommandRow settingsRow;
    public CommandRow checkpointRow;
    public CommandRow quitRow;

    [Header("Roots")]
    public GameObject terminalRoot;
    public GameObject settingsRoot;

    [Header("Colors")]
    public Color normalColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public Color highlightColor = new Color(0.6f, 1f, 0.6f, 1f);

    private bool isPaused = false;

    private enum CommandType { Resume, Settings, Checkpoint, Quit }
    private CommandType current = CommandType.Resume;

    void Start() {
        if (pauseRoot != null)
            pauseRoot.SetActive(false);

        if (pauseCanvas != null)
            pauseCanvas.alpha = 0f;

        if (resumeRow.button != null) resumeRow.button.onClick.AddListener(OnClickResume);
        if (settingsRow.button != null) settingsRow.button.onClick.AddListener(OnClickSettings);
        if (checkpointRow.button != null) checkpointRow.button.onClick.AddListener(OnClickCheckpoint);
        if (quitRow.button != null) quitRow.button.onClick.AddListener(OnClickQuit);

        UpdateCommandVisuals();
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            if (!isPaused) {
                ShowPauseMenu();
            } else {
                if (settingsRoot != null && settingsRoot.activeSelf) {
                    settingsRoot.SetActive(false);
                    terminalRoot.SetActive(true);
                } else {
                    HidePauseMenu();
                }
            }
        }
    }

    public void ShowPauseMenu() {
        isPaused = true;

        if (pauseRoot != null)
            pauseRoot.SetActive(true);

        if (pauseCanvas != null)
            pauseCanvas.alpha = 1f;

        if (terminalRoot != null) terminalRoot.SetActive(true);
        if (settingsRoot != null) settingsRoot.SetActive(false);

        Time.timeScale = 0f;

        current = CommandType.Resume;
        UpdateCommandVisuals();
    }

    public void HidePauseMenu() {
        isPaused = false;

        if (pauseCanvas != null)
            pauseCanvas.alpha = 0f;

        if (pauseRoot != null)
            pauseRoot.SetActive(false);

        Time.timeScale = 1f;
    }

    private void UpdateCommandVisuals() {
        UpdateRow(resumeRow, current == CommandType.Resume);
        UpdateRow(settingsRow, current == CommandType.Settings);
        UpdateRow(checkpointRow, current == CommandType.Checkpoint);
        UpdateRow(quitRow, current == CommandType.Quit);
    }

    private void UpdateRow(CommandRow row, bool selected) {
        if (row == null) return;

        if (row.leftCursor != null)
            row.leftCursor.text = selected ? ">" : " ";

        if (row.rightCursor != null)
            row.rightCursor.text = selected ? "<" : " ";

        if (row.label != null)
            row.label.color = selected ? highlightColor : normalColor;
    }

    private void SetCurrent(CommandType cmd) {
        current = cmd;
        UpdateCommandVisuals();
    }


    public void OnHoverResume() => SetCurrent(CommandType.Resume);
    public void OnHoverSettings() => SetCurrent(CommandType.Settings);
    public void OnHoverCheckpoint() => SetCurrent(CommandType.Checkpoint);
    public void OnHoverQuit() => SetCurrent(CommandType.Quit);


    public void OnClickResume() {
        Debug.Log("Resume clicked");
        HidePauseMenu();
    }

    public void OnClickSettings() {
        Debug.Log("Settings clicked");

        terminalRoot.SetActive(false);
        settingsRoot.SetActive(true);
    }

    public void OnClickCheckpoint() {
        Debug.Log("Load Last Checkpoint clicked");
        // 나중에 체크포인트 로드 구현
    }

    public void OnClickQuit() {
        Debug.Log("Quit clicked");
        // 나중에 메인메뉴로 이동 구현
    }

    public void OnClickSettingsBack() {
        settingsRoot.SetActive(false);
        terminalRoot.SetActive(true);
    }
}