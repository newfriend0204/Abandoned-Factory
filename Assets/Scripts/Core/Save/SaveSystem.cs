using System.IO;
using UnityEngine;

public static class SaveSystem {
    private const string SaveFileName = "save_slot_0.json";
    private static string saveFilePath;

    public static SaveGameData Current { get; private set; }

    private static void EnsurePath() {
        if (string.IsNullOrEmpty(saveFilePath)) {
            saveFilePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        }
    }

    public static bool HasFile {
        get {
            EnsurePath();
            return File.Exists(saveFilePath);
        }
    }

    public static bool LoadFromDisk() {
        EnsurePath();

        if (!File.Exists(saveFilePath)) {
            Debug.Log("[SaveSystem] 세이브 파일이 없습니다. (" + saveFilePath + ")");
            Current = null;
            return false;
        }

        try {
            string json = File.ReadAllText(saveFilePath);
            if (string.IsNullOrEmpty(json)) {
                Debug.LogWarning("[SaveSystem] 세이브 파일이 비어 있습니다.");
                Current = null;
                return false;
            }

            var data = JsonUtility.FromJson<SaveGameData>(json);
            if (data == null) {
                Debug.LogWarning("[SaveSystem] 세이브 파일 파싱 실패.");
                Current = null;
                return false;
            }

            if (data.player == null) data.player = new PlayerGlobalData();
            if (data.chap1 == null) data.chap1 = new Chap1SaveData();
            if (data.chap2 == null) data.chap2 = new Chap2SaveData();
            if (data.slotVersion <= 0) data.slotVersion = 1;

            Current = data;
            Debug.Log("[SaveSystem] 세이브 파일 로드 완료: " + saveFilePath);
            return true;
        } catch (System.Exception e) {
            Debug.LogError("[SaveSystem] 세이브 파일 로드 실패: " + e.Message);
            Current = null;
            return false;
        }
    }

    public static void SaveToDisk() {
        if (Current == null) {
            Current = new SaveGameData();
        }

        EnsurePath();

        try {
            string json = JsonUtility.ToJson(Current, true);
            File.WriteAllText(saveFilePath, json);
            Debug.Log("[SaveSystem] 세이브 저장 완료: " + saveFilePath);
        } catch (System.Exception e) {
            Debug.LogError("[SaveSystem] 세이브 저장 실패: " + e.Message);
        }
    }

    public static void DeleteFile() {
        EnsurePath();
        if (File.Exists(saveFilePath)) {
            try {
                File.Delete(saveFilePath);
                Debug.Log("[SaveSystem] 세이브 파일 삭제: " + saveFilePath);
            } catch (System.Exception e) {
                Debug.LogError("[SaveSystem] 세이브 파일 삭제 실패: " + e.Message);
            }
        }
        Current = null;
    }

    public static void ResetToNewGame() {
        Current = new SaveGameData();
    }
}