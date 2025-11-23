using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class KeybindRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [Header("IDs")]
    public string actionId;

    [Header("Texts")]
    public TextMeshProUGUI labelText;
    public TextMeshProUGUI primaryKeyText;
    public TextMeshProUGUI secondaryKeyText;

    [Header("Buttons")]
    public Button primaryButton;
    public Button secondaryButton;

    [Header("Summary")]
    [TextArea]
    public string summaryText;

    InputSettingsManager manager;
    PauseMenuController pauseMenu;

    void Awake() {
        if (primaryButton != null)
            primaryButton.onClick.AddListener(OnClickPrimary);

        if (secondaryButton != null)
            secondaryButton.onClick.AddListener(OnClickSecondary);

        pauseMenu = FindFirstObjectByType<PauseMenuController>();
    }

    void Start() {
        manager = FindFirstObjectByType<InputSettingsManager>();
        if (manager != null)
            manager.RegisterRow(this);
    }

    public void RefreshDisplay(KeyCode primary, KeyCode secondary, InputSettingsManager mgr) {
        manager = mgr;

        if (primaryKeyText != null)
            primaryKeyText.text = manager != null ? manager.FormatKeyName(primary) : primary.ToString();

        if (secondaryKeyText != null)
            secondaryKeyText.text = manager != null ? manager.FormatKeyName(secondary) : secondary.ToString();
    }

    void OnClickPrimary() {
        if (manager != null)
            manager.StartListeningKey(actionId, 0);
    }

    void OnClickSecondary() {
        if (manager != null)
            manager.StartListeningKey(actionId, 1);
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