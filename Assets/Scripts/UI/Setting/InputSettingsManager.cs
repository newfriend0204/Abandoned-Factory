using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class InputSettingsManager : MonoBehaviour {
    [Serializable]
    public class KeyBinding {
        public string actionId;
        public KeyCode primary;
        public KeyCode secondary;
    }

    [Header("Key Bindings")]
    public List<KeyBinding> keyBindings = new List<KeyBinding>();
    public LocalizedString emptyText;
    public LocalizedString movedKeyLogFormat;

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

    [Header("Save")]
    public string keyBindingFileName = "keybindings.txt";

    [Header("Pause Menu")]
    public PauseMenuController pauseMenu;

    string keyBindingFilePath;

    readonly Dictionary<string, KeybindRow> rowByActionId = new Dictionary<string, KeybindRow>();

    bool isListening = false;
    string listeningActionId = null;
    int listeningSlotIndex = -1;

    float logTimer = 0f;

    public bool IsPopupOpen {
        get {
            return switchKeyScreenRoot != null && switchKeyScreenRoot.activeSelf;
        }
    }

    void Awake() {
        keyBindingFilePath = Path.Combine(Application.persistentDataPath, keyBindingFileName);

        keyBindings.Clear();
        BuildDefaultBindings();

        if (File.Exists(keyBindingFilePath)) {
            LoadKeyBindingsFromFile();
        } else {
            SaveKeyBindingsToFile();
        }

        foreach (var kvp in rowByActionId) {
            RefreshRow(kvp.Key);
        }
    }

    void OnEnable() {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    void OnDisable() {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    void OnLocaleChanged(Locale locale) {
        foreach (var kvp in rowByActionId) {
            RefreshRow(kvp.Key);
        }
    }

    void BuildDefaultBindings() {
        EnsureBinding("MoveForward", KeyCode.W);
        EnsureBinding("MoveBackward", KeyCode.S);
        EnsureBinding("MoveLeft", KeyCode.A);
        EnsureBinding("MoveRight", KeyCode.D);
        EnsureBinding("Run", KeyCode.LeftShift);
        EnsureBinding("Jump", KeyCode.Space);
        EnsureBinding("Interact", KeyCode.F);
        EnsureBinding("ToggleFlashlight", KeyCode.R);
        EnsureBinding("ShowHint", KeyCode.H);
        EnsureBinding("ShowSolution", KeyCode.G);
    }

    void EnsureBinding(string actionId, KeyCode defaultPrimary) {
        KeyBinding binding = GetBinding(actionId);
        if (binding == null) {
            binding = new KeyBinding();
            binding.actionId = actionId;
            binding.primary = defaultPrimary;
            binding.secondary = KeyCode.None;
            keyBindings.Add(binding);
        }
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
            KeyBinding b = keyBindings[i];
            if (b != null && b.actionId == actionId)
                return b;
        }
        return null;
    }

    void RefreshRow(string actionId) {
        KeyBinding binding = GetBinding(actionId);
        if (binding == null)
            return;

        KeybindRow row;
        if (rowByActionId.TryGetValue(actionId, out row) && row != null) {
            row.RefreshDisplay(binding.primary, binding.secondary, this);
        }
    }

    string GetLabelForAction(string actionId) {
        KeybindRow row;
        if (rowByActionId.TryGetValue(actionId, out row) && row != null) {
            if (row.labelText != null)
                return row.labelText.text;
        }
        return actionId;
    }

    public string FormatKeyName(KeyCode key) {
        if (key == KeyCode.None)
            return emptyText.GetLocalizedString();

        if (key == KeyCode.Mouse0) return "Mouse L";
        if (key == KeyCode.Mouse1) return "Mouse R";
        if (key == KeyCode.Mouse2) return "Mouse M";

        string name = key.ToString();

        if (name.Length == 1)
            return name.ToUpper();

        if (name.StartsWith("Alpha"))
            return name.Substring("Alpha".Length);

        if (name.StartsWith("Keypad"))
            return "Num" + name.Substring("Keypad".Length);

        if (name.StartsWith("Left"))
            return "L" + name.Substring("Left".Length);

        if (name.StartsWith("Right"))
            return "R" + name.Substring("Right".Length);

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

        if (switchKeyScreenRoot != null)
            switchKeyScreenRoot.SetActive(true);

        string label = GetLabelForAction(actionId);
        if (keySummaryText != null)
            keySummaryText.text = label;

        KeyCode currentKey = slotIndex == 0 ? binding.primary : binding.secondary;
        if (currentKeyText != null)
            currentKeyText.text = FormatKeyName(currentKey);
    }

    void StopListening() {
        isListening = false;
        listeningActionId = null;
        listeningSlotIndex = -1;

        if (switchKeyScreenRoot != null)
            switchKeyScreenRoot.SetActive(false);
    }

    void ListenKeyInput() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            StopListening();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Delete)) {
            ApplyNewKey(listeningActionId, listeningSlotIndex, KeyCode.None);
            StopListening();
            return;
        }

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) {
            if (Input.GetKeyDown(key)) {
                ApplyNewKey(listeningActionId, listeningSlotIndex, key);
                StopListening();
                return;
            }
        }
    }

    void ApplyNewKey(string actionId, int slotIndex, KeyCode newKey) {
        if (string.IsNullOrEmpty(actionId))
            return;

        KeyBinding target = GetBinding(actionId);
        if (target == null)
            return;

        if (slotIndex == 0 && target.primary == newKey && newKey != KeyCode.None) {
            RefreshRow(actionId);
            return;
        }
        if (slotIndex == 1 && target.secondary == newKey && newKey != KeyCode.None) {
            RefreshRow(actionId);
            return;
        }

        string movedFromAction = null;
        KeyCode conflictOldKey = KeyCode.None;

        if (newKey != KeyCode.None) {
            for (int i = 0; i < keyBindings.Count; i++) {
                KeyBinding other = keyBindings[i];
                if (other == null || other.actionId == actionId)
                    continue;

                if (other.primary == newKey) {
                    other.primary = KeyCode.None;
                    movedFromAction = other.actionId;
                    conflictOldKey = newKey;
                    RefreshRow(other.actionId);
                    break;
                }

                if (other.secondary == newKey) {
                    other.secondary = KeyCode.None;
                    movedFromAction = other.actionId;
                    conflictOldKey = newKey;
                    RefreshRow(other.actionId);
                    break;
                }
            }
        }

        if (slotIndex == 0)
            target.primary = newKey;
        else if (slotIndex == 1)
            target.secondary = newKey;

        RefreshRow(actionId);

        if (!string.IsNullOrEmpty(movedFromAction) && newKey != KeyCode.None) {
            string fromLabel = GetLabelForAction(movedFromAction);
            string toLabel = GetLabelForAction(actionId);
            string keyName = FormatKeyName(newKey);

            string msg = "";
            if (movedKeyLogFormat != null) {
                msg = movedKeyLogFormat.GetLocalizedString(fromLabel, keyName, toLabel);
            }
            ShowLog(msg);
        }

        pauseMenu.MarkSettingChanged();
    }

    void ShowLog(string message) {
        if (switchKeyLogRoot == null || switchKeyLogCanvasGroup == null || switchKeyLogText == null)
            return;

        switchKeyLogRoot.SetActive(true);
        switchKeyLogCanvasGroup.alpha = 1f;
        switchKeyLogText.text = message;
        logTimer = logVisibleDuration + logFadeDuration;
    }

    void UpdateLogFade() {
        if (switchKeyLogRoot == null || switchKeyLogCanvasGroup == null)
            return;

        if (logTimer <= 0f) {
            switchKeyLogCanvasGroup.alpha = 0f;
            switchKeyLogRoot.SetActive(false);
            return;
        }

        logTimer -= Time.unscaledDeltaTime;

        float visibleTime = logVisibleDuration;
        if (logTimer > visibleTime + logFadeDuration) {
            switchKeyLogCanvasGroup.alpha = 1f;
            return;
        }

        if (logTimer > logFadeDuration) {
            switchKeyLogCanvasGroup.alpha = 1f;
        } else {
            float t = logTimer / logFadeDuration;
            switchKeyLogCanvasGroup.alpha = t;
        }
    }

    void LoadKeyBindingsFromFile() {
        if (string.IsNullOrEmpty(keyBindingFilePath))
            keyBindingFilePath = Path.Combine(Application.persistentDataPath, keyBindingFileName);

        if (!File.Exists(keyBindingFilePath))
            return;

        try {
            string[] lines = File.ReadAllLines(keyBindingFilePath);
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string actionId = line.Substring(0, eq).Trim();
                string rest = line.Substring(eq + 1).Trim();

                string primaryStr = rest;
                string secondaryStr = "None";

                int comma = rest.IndexOf(',');
                if (comma >= 0) {
                    primaryStr = rest.Substring(0, comma).Trim();
                    secondaryStr = rest.Substring(comma + 1).Trim();
                }

                KeyBinding binding = GetBinding(actionId);
                if (binding == null) {
                    binding = new KeyBinding();
                    binding.actionId = actionId;
                    keyBindings.Add(binding);
                }

                binding.primary = DeserializeKeyCode(primaryStr);
                binding.secondary = DeserializeKeyCode(secondaryStr);
            }
        } catch (Exception e) {
            Debug.LogError("[InputSettingsManager] Failed to read keybindings: " + e.Message);
        }
    }

    public void ReloadKeyBindingsFromFileFully() {
        keyBindings.Clear();
        BuildDefaultBindings();
        LoadKeyBindingsFromFile();

        foreach (var kvp in rowByActionId) {
            RefreshRow(kvp.Key);
        }
    }

    public void SaveKeyBindingsToFile() {
        if (string.IsNullOrEmpty(keyBindingFilePath))
            keyBindingFilePath = Path.Combine(Application.persistentDataPath, keyBindingFileName);

        try {
            List<string> lines = new List<string>();
            for (int i = 0; i < keyBindings.Count; i++) {
                KeyBinding binding = keyBindings[i];
                if (binding == null || string.IsNullOrEmpty(binding.actionId))
                    continue;

                string primaryStr = SerializeKeyCode(binding.primary);
                string secondaryStr = SerializeKeyCode(binding.secondary);

                string line = binding.actionId + "=" + primaryStr + "," + secondaryStr;
                lines.Add(line);
            }

            File.WriteAllLines(keyBindingFilePath, lines.ToArray());
        } catch (Exception e) {
            Debug.LogError("[InputSettingsManager] Failed to write keybindings: " + e.Message);
        }
    }

    string SerializeKeyCode(KeyCode key) {
        if (key == KeyCode.None)
            return "None";

        return key.ToString();
    }

    KeyCode DeserializeKeyCode(string text) {
        if (string.IsNullOrEmpty(text) || text == "None")
            return KeyCode.None;

        KeyCode key;
        if (Enum.TryParse(text, out key))
            return key;

        return KeyCode.None;
    }

    public void ResetKeyBindingsToDefault() {
        keyBindings.Clear();
        BuildDefaultBindings();

        foreach (var kvp in rowByActionId) {
            RefreshRow(kvp.Key);
        }
    }
}