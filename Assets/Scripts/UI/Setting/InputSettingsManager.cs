using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InputSettingsManager : MonoBehaviour {
    // -------------------------
    // 내부용 키 바인딩 데이터
    // -------------------------
    [Serializable]
    public class KeyBinding {
        public string actionId;     // 저장용 ID (예: "MoveForward")
        public KeyCode primary;     // Key1
        public KeyCode secondary;   // Key2
    }

    [Header("Key Bindings")]
    public List<KeyBinding> keyBindings = new List<KeyBinding>();

    [Header("Switch Key Screen")]
    public GameObject switchKeyScreenRoot;
    public TextMeshProUGUI keySummaryText;
    public TextMeshProUGUI currentKeyText;

    [Header("Switch Key Log")]
    public GameObject switchKeyLogRoot;
    public CanvasGroup switchKeyLogCanvasGroup;
    public TextMeshProUGUI switchKeyLogText;
    public float logVisibleDuration = 4f;
    public float logFadeDuration = 1f;

    readonly Dictionary<string, KeybindRow> rowByActionId =
        new Dictionary<string, KeybindRow>();

    bool isListening = false;
    string listeningActionId = null;
    int listeningSlotIndex = -1;

    float logTimer = 0f;

    PauseMenuController pauseMenu;

    public bool IsPopupOpen {
        get {
            return switchKeyScreenRoot != null && switchKeyScreenRoot.activeSelf;
        }
    }

    void Awake() {
        pauseMenu = FindFirstObjectByType<PauseMenuController>();
    }

    void Update() {
        if (isListening)
            ListenKeyInput();

        UpdateLogFade();
    }

    public void RegisterRow(KeybindRow row) {
        if (row == null || string.IsNullOrEmpty(row.actionId))
            return;

        rowByActionId[row.actionId] = row;

        KeyBinding binding = GetBinding(row.actionId);
        if (binding == null) {
            binding = new KeyBinding {
                actionId = row.actionId,
                primary = KeyCode.None,
                secondary = KeyCode.None
            };
            keyBindings.Add(binding);
        }

        row.RefreshDisplay(binding.primary, binding.secondary, this);
    }

    KeyBinding GetBinding(string actionId) {
        for (int i = 0; i < keyBindings.Count; i++) {
            if (keyBindings[i].actionId == actionId)
                return keyBindings[i];
        }
        return null;
    }

    void RefreshRow(string actionId) {
        KeyBinding binding = GetBinding(actionId);
        if (binding == null)
            return;

        if (rowByActionId.TryGetValue(actionId, out KeybindRow row) && row != null) {
            row.RefreshDisplay(binding.primary, binding.secondary, this);
        }
    }

    string GetLabelForAction(string actionId) {
        if (rowByActionId.TryGetValue(actionId, out KeybindRow row) && row != null && row.labelText != null)
            return row.labelText.text;

        return actionId;
    }

    public string FormatKeyName(KeyCode key) {
        if (key == KeyCode.None)
            return "(비어있음)";

        switch (key) {
            case KeyCode.Mouse0:
                return "Mouse Left";
            case KeyCode.Mouse1:
                return "Mouse Right";
            case KeyCode.Mouse2:
                return "Mouse Middle";
        }

        string name = key.ToString();

        if (name.Length == 1 && char.IsLetter(name[0])) {
            return name.ToUpperInvariant();
        }

        if (name.StartsWith("Left")) {
            return "L" + name.Substring("Left".Length);
        }
        if (name.StartsWith("Right")) {
            return "R" + name.Substring("Right".Length);
        }

        return name;
    }

    public void StartListeningKey(string actionId, int slotIndex) {
        KeyBinding binding = GetBinding(actionId);
        if (binding == null) {
            binding = new KeyBinding {
                actionId = actionId,
                primary = KeyCode.None,
                secondary = KeyCode.None
            };
            keyBindings.Add(binding);
        }

        listeningActionId = actionId;
        listeningSlotIndex = slotIndex;
        isListening = true;

        if (keySummaryText != null) {
            string summary = GetLabelForAction(actionId);
            keySummaryText.text = summary;
        }

        if (currentKeyText != null) {
            KeyCode currentKey = (slotIndex == 0) ? binding.primary : binding.secondary;
            currentKeyText.text = FormatKeyName(currentKey);
        }

        if (switchKeyScreenRoot != null)
            switchKeyScreenRoot.SetActive(true);
    }

    void EndListening(bool closePopup) {
        isListening = false;
        listeningActionId = null;
        listeningSlotIndex = -1;

        if (closePopup && switchKeyScreenRoot != null)
            switchKeyScreenRoot.SetActive(false);
    }

    void ListenKeyInput() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            EndListening(true);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) {
            ApplyNewKey(KeyCode.None);
            return;
        }

        if (!Input.anyKeyDown)
            return;

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) {
            if (Input.GetKeyDown(key)) {
                ApplyNewKey(key);
                break;
            }
        }
    }

    void ApplyNewKey(KeyCode newKey) {
        if (string.IsNullOrEmpty(listeningActionId))
            return;

        KeyBinding targetBinding = GetBinding(listeningActionId);
        if (targetBinding == null)
            return;

        if (newKey == KeyCode.None) {
            if (listeningSlotIndex == 0)
                targetBinding.primary = KeyCode.None;
            else
                targetBinding.secondary = KeyCode.None;

            RefreshRow(listeningActionId);

            if (pauseMenu != null)
                pauseMenu.MarkSettingChanged();

            EndListening(true);
            return;
        }

        HashSet<string> changedActions = new HashSet<string>();
        string removedFromOtherActionId = null;

        for (int i = 0; i < keyBindings.Count; i++) {
            KeyBinding kb = keyBindings[i];

            if (kb.primary == newKey) {
                kb.primary = KeyCode.None;
                changedActions.Add(kb.actionId);

                if (removedFromOtherActionId == null && kb.actionId != listeningActionId)
                    removedFromOtherActionId = kb.actionId;
            }

            if (kb.secondary == newKey) {
                kb.secondary = KeyCode.None;
                changedActions.Add(kb.actionId);

                if (removedFromOtherActionId == null && kb.actionId != listeningActionId)
                    removedFromOtherActionId = kb.actionId;
            }
        }

        if (listeningSlotIndex == 0)
            targetBinding.primary = newKey;
        else
            targetBinding.secondary = newKey;

        changedActions.Add(listeningActionId);

        foreach (string actionId in changedActions)
            RefreshRow(actionId);

        if (removedFromOtherActionId != null && switchKeyLogText != null && switchKeyLogRoot != null) {
            string keyName = FormatKeyName(newKey);
            string fromLabel = GetLabelForAction(removedFromOtherActionId);
            string toLabel = GetLabelForAction(targetBinding.actionId);

            string msg = $"\"{fromLabel}\"에 배치되어있던 \"{keyName}\"(이)가 \"{toLabel}\"로 이동됨.";
            ShowLog(msg);
        }

        if (pauseMenu != null)
            pauseMenu.MarkSettingChanged();

        EndListening(true);
    }

    void ShowLog(string message) {
        if (switchKeyLogText != null)
            switchKeyLogText.text = message;

        if (switchKeyLogCanvasGroup != null)
            switchKeyLogCanvasGroup.alpha = 1f;

        if (switchKeyLogRoot != null)
            switchKeyLogRoot.SetActive(true);

        logTimer = logVisibleDuration + logFadeDuration;
    }

    void UpdateLogFade() {
        if (switchKeyLogRoot == null || switchKeyLogCanvasGroup == null)
            return;

        if (!switchKeyLogRoot.activeSelf)
            return;

        if (logTimer <= 0f)
            return;

        logTimer -= Time.unscaledDeltaTime;
        if (logTimer <= 0f) {
            switchKeyLogCanvasGroup.alpha = 0f;
            switchKeyLogRoot.SetActive(false);
            return;
        }

        if (logTimer > logFadeDuration) {
            switchKeyLogCanvasGroup.alpha = 1f;
        } else {
            float t = logTimer / logFadeDuration;
            switchKeyLogCanvasGroup.alpha = t;
        }
    }
}