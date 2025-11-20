using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsTabsTerminal : MonoBehaviour {
    public enum TabType { General, Graphics, Audio, Controls }

    [System.Serializable]
    public class TabRow {
        public Button button;
        public TextMeshProUGUI leftCursor;
        public TextMeshProUGUI label;
        public TextMeshProUGUI rightCursor;
    }

    [Header("Tab Rows")]
    public TabRow generalRow;
    public TabRow graphicsRow;
    public TabRow audioRow;
    public TabRow controlsRow;

    [Header("Pages")]
    public GameObject generalPage;
    public GameObject graphicsPage;
    public GameObject audioPage;
    public GameObject controlsPage;

    [Header("Colors")]
    public Color normalColor = new Color(0.4f, 0.8f, 0.6f, 1f);
    public Color selectedColor = new Color(0.9f, 1f, 0.9f, 1f);

    [Header("Back Label")]
    public TextMeshProUGUI backLabel;
    public string backIdleText = "  BACK  ";
    public string backHoverText = "> BACK <";

    private TabType currentTab = TabType.General;
    private TabType cursorTab = TabType.General;

    private void Start() {
        if (generalRow.button != null) generalRow.button.onClick.AddListener(() => OnClickTab(TabType.General));
        if (graphicsRow.button != null) graphicsRow.button.onClick.AddListener(() => OnClickTab(TabType.Graphics));
        if (audioRow.button != null) audioRow.button.onClick.AddListener(() => OnClickTab(TabType.Audio));
        if (controlsRow.button != null) controlsRow.button.onClick.AddListener(() => OnClickTab(TabType.Controls));

        currentTab = TabType.General;
        cursorTab = TabType.General;
        ApplyTabState();
        ApplyPageState();

        if (backLabel != null) {
            backLabel.text = backIdleText;
        }
    }

    public void OnClickTab(TabType tab) {
        currentTab = tab;
        cursorTab = tab;
        ApplyTabState();
        ApplyPageState();
    }

    public void OnHoverGeneral() => OnHoverTab(TabType.General);
    public void OnHoverGraphics() => OnHoverTab(TabType.Graphics);
    public void OnHoverAudio() => OnHoverTab(TabType.Audio);
    public void OnHoverControls() => OnHoverTab(TabType.Controls);

    private void OnHoverTab(TabType tab) {
        cursorTab = tab;
        ApplyTabState();
    }

    public void OnHoverExit() {
        cursorTab = currentTab;
        ApplyTabState();
    }

    public void OnHoverBack() {
        if (backLabel != null) {
            backLabel.text = backHoverText;
        }
    }

    public void OnExitBack() {
        if (backLabel != null) {
            backLabel.text = backIdleText;
        }
    }

    private void ApplyTabState() {
        UpdateRow(generalRow, TabType.General);
        UpdateRow(graphicsRow, TabType.Graphics);
        UpdateRow(audioRow, TabType.Audio);
        UpdateRow(controlsRow, TabType.Controls);
    }

    private void UpdateRow(TabRow row, TabType thisTab) {
        if (row == null) return;

        bool isSelected = (thisTab == currentTab);
        bool hasCursor = (thisTab == cursorTab);

        if (row.leftCursor != null) row.leftCursor.text = hasCursor ? ">" : " ";
        if (row.rightCursor != null) row.rightCursor.text = hasCursor ? "<" : " ";

        if (row.label != null)
            row.label.color = isSelected ? selectedColor : normalColor;
    }

    private void ApplyPageState() {
        if (generalPage != null) generalPage.SetActive(currentTab == TabType.General);
        if (graphicsPage != null) graphicsPage.SetActive(currentTab == TabType.Graphics);
        if (audioPage != null) audioPage.SetActive(currentTab == TabType.Audio);
        if (controlsPage != null) controlsPage.SetActive(currentTab == TabType.Controls);
    }
}