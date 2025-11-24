using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class OptionToggleRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [Header("IDs")]
    public string actionId;

    [Header("UI")]
    public TextMeshProUGUI labelText;
    public Button[] optionButtons;

    [Header("State")]
    [Range(0, 10)]
    public int selectedIndex = 0;

    [Header("Summary")]
    [TextArea]
    public string summaryText;

    PauseMenuController pauseMenu;

    void Awake() {
        pauseMenu = FindFirstObjectByType<PauseMenuController>();
        ApplyVisual();
    }

    public void OnClickOption(int index) {
        if (index < 0 || index >= optionButtons.Length)
            return;

        if (selectedIndex == index)
            return;

        selectedIndex = index;
        ApplyVisual();

        if (pauseMenu != null)
            pauseMenu.MarkSettingChanged();
    }

    void ApplyVisual() {
        for (int i = 0; i < optionButtons.Length; i++) {
            Button btn = optionButtons[i];
            if (btn == null)
                continue;

            ColorBlock colors = btn.colors;
            Color normal = colors.normalColor;
            Color selected = colors.selectedColor;

            float aNormal = normal.a;
            float aSelected = selected.a;

            if (i == selectedIndex) {
                normal = new Color(1f, 1f, 1f, aNormal);
                selected = new Color(1f, 1f, 1f, aSelected);
            } else {
                normal = new Color(0f, 1f, 0f, aNormal);
                selected = new Color(0f, 1f, 0f, aSelected);
            }

            colors.normalColor = normal;
            colors.selectedColor = selected;
            btn.colors = colors;
        }
    }

    public void SetSelectedIndex(int index, bool notifyChange = false) {
        if (index < 0 || index >= optionButtons.Length)
            index = 0;

        selectedIndex = index;
        ApplyVisual();

        if (notifyChange && pauseMenu != null)
            pauseMenu.MarkSettingChanged();
    }

    public int GetSelectedIndex() {
        return selectedIndex;
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (pauseMenu != null && !string.IsNullOrEmpty(summaryText))
            pauseMenu.ShowSummary(summaryText);
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (pauseMenu != null)
            pauseMenu.ClearSummary();
    }
}