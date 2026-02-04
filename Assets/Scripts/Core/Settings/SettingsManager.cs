using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;

public class SettingsManager : MonoBehaviour {
    public static SettingsManager Instance;

    [Header("File")]
    public string fileName = "settings.txt";

    readonly Dictionary<string, string> table = new Dictionary<string, string>();
    string filePath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateInstanceOnLoad() {
        if (Instance != null) {
            return;
        }

        GameObject go = new GameObject("SettingsManager");
        Instance = go.AddComponent<SettingsManager>();
        DontDestroyOnLoad(go);
    }

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        filePath = Path.Combine(Application.persistentDataPath, fileName);

        ResetToDefaults();

        if (File.Exists(filePath)) {
            MergeFromFile();
        } else {
            SaveToFile();
        }
    }

    public void ResetToDefaults() {
        table.Clear();

        SetInt("Language", 0);
        SetInt("InteractHint", 0);
        SetInt("RunMethod", 0);
        SetInt("WalkRunDefault", 0);
        SetInt("Crosshair", 0);
        SetInt("CameraShake", 0);

        SetInt("WindowMode", 0);
        SetInt("Resolution", 0);
        SetFloat("FOV", 60f);
        SetInt("FrameLimit", 0);
        SetFloat("Brightness", 1.0f);
        SetInt("VSync", 0);
        SetInt("MotionBlur", 0);
        SetInt("Bloom", 1);
        SetInt("ShadowQuality", 3);

        SetInt("MasterVolume", 100);
        SetInt("BgmVolume", 100);
        SetInt("SfxVolume", 100);
        SetInt("AmbientVolume", 100);
        SetInt("PlayInBackground", 1);

        SetFloat("MouseSensitivity", 1.0f);
        SetFloat("MouseSensitivityX", 1.0f);
        SetFloat("MouseSensitivityY", 1.0f);
        SetFloat("MouseAcceleration", 0.0f);
        SetInt("InvertMouseY", 0);
    }

    void MergeFromFile() {
        try {
            string[] lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) {
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (string.IsNullOrEmpty(key)) {
                    continue;
                }

                table[key] = value;
            }

            Debug.Log("[SettingsManager] MergeFromFile completed.");
        } catch (Exception e) {
            Debug.LogError("[SettingsManager] Failed to read file: " + e.Message);
        }
    }

    public void SaveToFile() {
        if (string.IsNullOrEmpty(filePath)) {
            filePath = Path.Combine(Application.persistentDataPath, fileName);
        }

        try {
            List<string> lines = new List<string>();
            List<string> keys = new List<string>(table.Keys);
            keys.Sort(StringComparer.Ordinal);

            Debug.Log("[SettingsManager] Saving settings:");

            for (int i = 0; i < keys.Count; i++) {
                string key = keys[i];
                string value = table[key];
                string line = key + "=" + value;
                lines.Add(line);

                Debug.Log("  " + line);
            }

            File.WriteAllLines(filePath, lines.ToArray());
            Debug.Log("[SettingsManager] Saved to " + filePath);
        } catch (Exception e) {
            Debug.LogError("[SettingsManager] Failed to write file: " + e.Message);
        }
    }

    public void ReloadFromFile() {
        if (string.IsNullOrEmpty(filePath)) {
            filePath = Path.Combine(Application.persistentDataPath, fileName);
        }

        ResetToDefaults();

        if (File.Exists(filePath)) {
            MergeFromFile();
        }

        Debug.Log("[SettingsManager] ReloadFromFile done.");
    }

    public int GetInt(string key, int defaultValue) {
        string valueStr;
        if (!table.TryGetValue(key, out valueStr)) {
            return defaultValue;
        }

        int v;
        if (int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) {
            return v;
        }

        float f;
        if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) {
            return Mathf.RoundToInt(f);
        }

        return defaultValue;
    }

    public float GetFloat(string key, float defaultValue) {
        string valueStr;
        if (!table.TryGetValue(key, out valueStr)) {
            return defaultValue;
        }

        float v;
        if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) {
            return v;
        }

        int i;
        if (int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) {
            return i;
        }

        return defaultValue;
    }

    public string GetString(string key, string defaultValue) {
        string valueStr;
        if (!table.TryGetValue(key, out valueStr)) {
            return defaultValue;
        }

        return valueStr;
    }

    public void SetInt(string key, int value) {
        table[key] = value.ToString(CultureInfo.InvariantCulture);
    }

    public void SetFloat(string key, float value) {
        table[key] = value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public void SetString(string key, string value) {
        if (value == null) {
            value = string.Empty;
        }

        table[key] = value;
    }
}