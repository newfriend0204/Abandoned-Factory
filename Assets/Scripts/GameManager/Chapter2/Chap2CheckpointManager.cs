using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Chap2CheckpointManager : MonoBehaviour, ICheckpointService {
    public static Chap2CheckpointManager Instance { get; private set; }

    [Header("Fade")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;

    [Header("Checkpoint Popup")]
    public CanvasGroup checkpointCanvasGroup;
    public float checkpointFadeDuration = 0.3f;
    public float checkpointHoldDuration = 4f;

    private static Chap2CheckpointData sharedData;
    private static int sharedChap2StateInt = 0;
    private static int sharedYCurrentStep = 1;
    private static List<Chap2StepCheckpointEntry> sharedStepCheckpoints;

    private Chap2CheckpointData current;
    private bool isLoading = false;

    private Coroutine checkpointPopupRoutine;
    private HashSet<string> consumedCheckpointZoneIds = new HashSet<string>();

    private Dictionary<int, Chap2CheckpointData> stepCheckpointCache = new Dictionary<int, Chap2CheckpointData>();

    public bool HasCheckpoint => current != null;

    private static bool autoLoadOnSceneStart = false;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CheckpointService.Register(this);

        DontDestroyOnLoad(gameObject);

        if (SaveSystem.Current == null && SaveSystem.HasFile)
            SaveSystem.LoadFromDisk();

        if (sharedData != null)
            current = sharedData;

        if (current == null &&
            SaveSystem.Current != null &&
            SaveSystem.Current.chap2 != null &&
            SaveSystem.Current.chap2.hasCheckpoint &&
            SaveSystem.Current.chap2.last != null) {

            current = SaveSystem.Current.chap2.last;
        }

        if (current != null && current.consumedCheckpointZoneIds != null) {
            consumedCheckpointZoneIds = new HashSet<string>(current.consumedCheckpointZoneIds);
        }

        RestoreStepCacheFromSaveDataIfExists();
        FindSceneOverlays();
    }

    private void OnEnable() {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable() {
        if (Instance == this) {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnDestroy() {
        if (Instance == this)
            Instance = null;

        if (ReferenceEquals(CheckpointService.Current, this))
            CheckpointService.Register(null);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (Instance != this)
            return;

        FindSceneOverlays();

        if (autoLoadOnSceneStart && current != null && scene.name == current.sceneName) {
            autoLoadOnSceneStart = false;
            StartCoroutine(CoApplyCheckpointOnSceneLoaded());
        }
    }

    private IEnumerator CoApplyCheckpointOnSceneLoaded() {
        isLoading = true;

        yield return null;

        ApplyCheckpointToScene();

        InteractionModeService.SetInteractionMode(false);
        Time.timeScale = 1f;

        isLoading = false;
    }

    public static bool HasSaveFile => SaveSystem.HasFile;

    public static void SetAutoLoadOnSceneStart(bool value) {
        autoLoadOnSceneStart = value;
    }

    public static void ClearSharedDataInMemory() {
        sharedData = null;
        sharedChap2StateInt = 0;
        sharedYCurrentStep = 1;
        sharedStepCheckpoints = null;

        if (Instance != null) {
            Instance.current = null;
            Instance.consumedCheckpointZoneIds.Clear();
            Instance.stepCheckpointCache.Clear();
        }
    }

    public static bool LoadSharedDataFromFile() {
        if (!SaveSystem.LoadFromDisk()) {
            Debug.Log("[Chap2CheckpointManager] 저장된 세이브 파일이 없습니다.");
            sharedData = null;
            sharedStepCheckpoints = null;
            sharedChap2StateInt = 0;
            sharedYCurrentStep = 1;
            return false;
        }

        if (SaveSystem.Current == null ||
            SaveSystem.Current.chap2 == null ||
            !SaveSystem.Current.chap2.hasCheckpoint ||
            SaveSystem.Current.chap2.last == null) {

            Debug.LogWarning("[Chap2CheckpointManager] Chap2 세이브 데이터가 없습니다.");
            sharedData = null;
            sharedStepCheckpoints = null;
            sharedChap2StateInt = 0;
            sharedYCurrentStep = 1;
            return false;
        }

        sharedData = SaveSystem.Current.chap2.last;
        sharedChap2StateInt = SaveSystem.Current.chap2.chap2StateInt;
        sharedYCurrentStep = SaveSystem.Current.chap2.yCurrentStep;
        sharedStepCheckpoints = SaveSystem.Current.chap2.stepCheckpoints;

        Debug.Log("[Chap2CheckpointManager] 세이브 파일에서 Chap2 체크포인트 로드 완료.");
        return true;
    }

    public static void DeleteSaveFile() {
        SaveSystem.DeleteFile();
    }

    public static string GetSavedSceneNameOrDefault(string defaultSceneName) {
        if (sharedData != null && !string.IsNullOrEmpty(sharedData.sceneName)) {
            return sharedData.sceneName;
        }

        if (SaveSystem.Current != null &&
            SaveSystem.Current.chap2 != null &&
            SaveSystem.Current.chap2.hasCheckpoint &&
            SaveSystem.Current.chap2.last != null &&
            !string.IsNullOrEmpty(SaveSystem.Current.chap2.last.sceneName)) {

            return SaveSystem.Current.chap2.last.sceneName;
        }

        return defaultSceneName;
    }

    public static int GetSavedChap2StateIntOrDefault(int defaultStateInt) {
        if (sharedData != null)
            return sharedChap2StateInt;

        if (SaveSystem.Current != null && SaveSystem.Current.chap2 != null)
            return SaveSystem.Current.chap2.chap2StateInt;

        return defaultStateInt;
    }

    public static int GetSavedYCurrentStepOrDefault(int defaultStep) {
        if (sharedData != null)
            return sharedYCurrentStep;

        if (SaveSystem.Current != null && SaveSystem.Current.chap2 != null)
            return SaveSystem.Current.chap2.yCurrentStep;

        return defaultStep;
    }

    private static void SaveSharedDataToFile() {
        if (Instance == null || Instance.current == null)
            return;

        if (SaveSystem.Current == null) {
            SaveSystem.ResetToNewGame();
        }

        var data = SaveSystem.Current;

        if (data.chap2 == null)
            data.chap2 = new Chap2SaveData();
        if (data.player == null)
            data.player = new PlayerGlobalData();

        data.currentChapter = 2;

        data.chap2.hasCheckpoint = true;
        data.chap2.last = Instance.current;

        data.player.hasHeadlamp = Instance.current.hasHeadlamp;
        data.player.savedSprintStamina = Instance.current.playerSprintStamina;
        data.player.savedIsExhausted = Instance.current.playerIsExhausted;

        var gm = FindFirstObjectByType<GameManagerChap2>();
        if (gm != null)
            data.chap2.chap2StateInt = (int)gm.State;

        var yseq = FindFirstObjectByType<Chap2YStepSequenceManager>();
        if (yseq != null)
            data.chap2.yCurrentStep = yseq.CurrentStep;

        if (data.chap2.stepCheckpoints == null)
            data.chap2.stepCheckpoints = new List<Chap2StepCheckpointEntry>();
        else
            data.chap2.stepCheckpoints.Clear();

        foreach (var kv in Instance.stepCheckpointCache) {
            var entry = new Chap2StepCheckpointEntry();
            entry.step = kv.Key;
            entry.data = Instance.CloneCheckpointData(kv.Value);
            data.chap2.stepCheckpoints.Add(entry);
        }

        sharedData = Instance.current;
        sharedChap2StateInt = data.chap2.chap2StateInt;
        sharedYCurrentStep = data.chap2.yCurrentStep;
        sharedStepCheckpoints = data.chap2.stepCheckpoints;

        SaveSystem.SaveToDisk();
    }

    private void RestoreStepCacheFromSaveDataIfExists() {
        stepCheckpointCache.Clear();

        List<Chap2StepCheckpointEntry> src = null;

        if (sharedStepCheckpoints != null && sharedStepCheckpoints.Count > 0)
            src = sharedStepCheckpoints;
        else if (SaveSystem.Current != null &&
            SaveSystem.Current.chap2 != null &&
            SaveSystem.Current.chap2.stepCheckpoints != null &&
            SaveSystem.Current.chap2.stepCheckpoints.Count > 0)
            src = SaveSystem.Current.chap2.stepCheckpoints;

        if (src == null)
            return;

        for (int i = 0; i < src.Count; i++) {
            var e = src[i];
            if (e == null || e.data == null)
                continue;

            int step = e.step;
            if (step < 1)
                step = 1;

            stepCheckpointCache[step] = CloneCheckpointData(e.data);
        }
    }

    public void SaveCheckpointAtCurrentPosition() {
        SaveInternal(false, null, true, true);
    }

    public void SaveCheckpointAtSpawnPoint(Transform spawnPoint) {
        SaveInternal(true, spawnPoint, true, true);
    }

    public void SaveCheckpointAtCurrentPositionSilent(bool writeToFile) {
        SaveInternal(false, null, false, writeToFile);
    }

    public void SaveCheckpointAtSpawnPointSilent(Transform spawnPoint, bool writeToFile) {
        SaveInternal(true, spawnPoint, false, writeToFile);
    }

    private void SaveInternal(bool useSpawnPoint, Transform spawnPoint, bool showPopup, bool writeToFile) {
        var player = FindFirstObjectByType<PlayerController>();
        var head = FindFirstObjectByType<HeadlampController>();

        if (player == null) {
            Debug.LogWarning("[Chap2CheckpointManager] PlayerController를 찾지 못해 체크포인트 저장 실패");
            return;
        }

        if (current == null)
            current = new Chap2CheckpointData();

        current.sceneName = SceneManager.GetActiveScene().name;

        Vector3 pos;
        Quaternion rot;
        float pitch;
        float stamina;
        bool exhausted;
        player.ExportCheckpointData(out pos, out rot, out pitch, out stamina, out exhausted);

        if (useSpawnPoint && spawnPoint != null) {
            current.playerPosition = spawnPoint.position;
            current.playerRotation = spawnPoint.rotation;
            current.playerPitch = 0f;
        } else {
            current.playerPosition = pos;
            current.playerRotation = rot;
            current.playerPitch = pitch;
        }

        current.playerSprintStamina = stamina;
        current.playerIsExhausted = exhausted;

        current.hasHeadlamp = (head != null && head.canUseHeadlamp);

        if (current.consumedCheckpointZoneIds == null)
            current.consumedCheckpointZoneIds = new List<string>(consumedCheckpointZoneIds);
        else {
            current.consumedCheckpointZoneIds.Clear();
            current.consumedCheckpointZoneIds.AddRange(consumedCheckpointZoneIds);
        }

        sharedData = current;

        if (writeToFile)
            SaveSharedDataToFile();

        Debug.Log($"[Chap2CheckpointManager] 체크포인트 저장 완료 (scene='{current.sceneName}', writeToFile={writeToFile})");

        if (showPopup)
            StartCheckpointPopup();
    }

    public void LoadLastCheckpoint() {
        if (current == null) {
            Debug.LogWarning("[Chap2CheckpointManager] 저장된 체크포인트가 없습니다.");
            return;
        }

        if (!isLoading)
            StartCoroutine(CoLoadLastCheckpoint());
    }

    private IEnumerator CoLoadLastCheckpoint() {
        isLoading = true;

        float prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (fadeCanvasGroup != null)
            yield return StartCoroutine(CoFade(0f, 1f));
        else
            yield return null;

        string targetScene = current.sceneName;
        SceneManager.LoadScene(targetScene);

        yield return null;

        FindSceneOverlays();
        ApplyCheckpointToScene();

        if (fadeCanvasGroup != null)
            yield return StartCoroutine(CoFade(1f, 0f));
        else
            yield return null;

        if (prevTimeScale <= 0f)
            Time.timeScale = 1f;
        else
            Time.timeScale = prevTimeScale;

        isLoading = false;
    }

    private IEnumerator CoFade(float from, float to) {
        if (fadeCanvasGroup == null)
            yield break;

        float t = 0f;
        fadeCanvasGroup.alpha = from;

        while (t < fadeDuration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
    }

    private void ApplyCheckpointToScene() {
        if (current == null) {
            Debug.LogWarning("[Chap2CheckpointManager] ApplyCheckpointToScene: current가 null");
            return;
        }

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) {
            player.ImportCheckpointData(
                current.playerPosition,
                current.playerRotation,
                current.playerPitch,
                current.playerSprintStamina,
                current.playerIsExhausted
            );
        } else {
            Debug.LogWarning("[Chap2CheckpointManager] PlayerController를 찾지 못해 위치 복원 실패");
        }

        var head = FindFirstObjectByType<HeadlampController>();
        if (head != null) {
            head.canUseHeadlamp = true;// head.canUseHeadlamp = current.hasHeadlamp; 디버그, 나중에 주석한걸로 교체하기!
        }

        if (current.consumedCheckpointZoneIds != null) {
            consumedCheckpointZoneIds = new HashSet<string>(current.consumedCheckpointZoneIds);
        }
    }

    private void FindSceneOverlays() {
        if (fadeCanvasGroup == null) {
            var go = GameObject.Find("FadeOverlay");
            if (go != null) {
                fadeCanvasGroup = go.GetComponent<CanvasGroup>();
                if (fadeCanvasGroup != null)
                    fadeCanvasGroup.alpha = 0f;
            }
        }

        if (checkpointCanvasGroup == null) {
            var cpGo = GameObject.Find("SaveCheckPoint");
            if (cpGo != null) {
                checkpointCanvasGroup = cpGo.GetComponent<CanvasGroup>();
                if (checkpointCanvasGroup != null)
                    checkpointCanvasGroup.alpha = 0f;
            }
        }
    }

    private void StartCheckpointPopup() {
        if (checkpointCanvasGroup == null) {
            FindSceneOverlays();
        }

        if (checkpointCanvasGroup == null)
            return;

        if (!gameObject.activeInHierarchy)
            return;

        if (checkpointPopupRoutine != null)
            StopCoroutine(checkpointPopupRoutine);

        checkpointPopupRoutine = StartCoroutine(CoCheckpointPopup());
    }

    private IEnumerator CoCheckpointPopup() {
        checkpointCanvasGroup.gameObject.SetActive(true);

        float t = 0f;
        checkpointCanvasGroup.alpha = 0f;
        while (t < checkpointFadeDuration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / checkpointFadeDuration);
            checkpointCanvasGroup.alpha = Mathf.Lerp(0f, 1f, k);
            yield return null;
        }
        checkpointCanvasGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(checkpointHoldDuration);

        t = 0f;
        while (t < checkpointFadeDuration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / checkpointFadeDuration);
            checkpointCanvasGroup.alpha = Mathf.Lerp(1f, 0f, k);
            yield return null;
        }
        checkpointCanvasGroup.alpha = 0f;

        checkpointPopupRoutine = null;
    }

    public bool IsCheckpointZoneConsumed(string id) {
        if (string.IsNullOrEmpty(id))
            return false;
        return consumedCheckpointZoneIds.Contains(id);
    }

    public void MarkCheckpointZoneConsumed(string id) {
        if (string.IsNullOrEmpty(id))
            return;

        if (!consumedCheckpointZoneIds.Add(id))
            return;

        if (current == null)
            current = new Chap2CheckpointData();

        if (current.consumedCheckpointZoneIds == null)
            current.consumedCheckpointZoneIds = new List<string>();

        if (!current.consumedCheckpointZoneIds.Contains(id))
            current.consumedCheckpointZoneIds.Add(id);

        sharedData = current;
    }

    public bool HasStepCheckpoint(int step) {
        return stepCheckpointCache.ContainsKey(step);
    }

    public void CacheCurrentAsStepCheckpoint(int step) {
        if (current == null)
            return;

        if (step < 1)
            step = 1;

        stepCheckpointCache[step] = CloneCheckpointData(current);
    }

    public void SaveStepCheckpointFromCurrentPosition(int step, bool showPopup) {
        SaveInternal(false, null, showPopup, true);
        CacheCurrentAsStepCheckpoint(step);
        SaveSharedDataToFile();
    }

    public void SaveStepCheckpointFromSpawnPoint(int step, Transform spawnPoint, bool showPopup, bool writeToFile) {
        SaveInternal(true, spawnPoint, showPopup, writeToFile);
        CacheCurrentAsStepCheckpoint(step);

        if (writeToFile)
            SaveSharedDataToFile();
    }

    public void LoadStepCheckpoint(int step) {
        if (step < 1)
            step = 1;

        if (!stepCheckpointCache.TryGetValue(step, out Chap2CheckpointData data) || data == null) {
            Debug.LogWarning($"[Chap2CheckpointManager] LoadStepCheckpoint 실패: step {step} 캐시가 없음");
            return;
        }

        current = CloneCheckpointData(data);
        sharedData = current;

        SaveSharedDataToFile();

        LoadLastCheckpoint();
    }

    private Chap2CheckpointData CloneCheckpointData(Chap2CheckpointData src) {
        if (src == null)
            return null;

        var dst = new Chap2CheckpointData();
        dst.sceneName = src.sceneName;

        dst.playerPosition = src.playerPosition;
        dst.playerRotation = src.playerRotation;
        dst.playerPitch = src.playerPitch;

        dst.playerSprintStamina = src.playerSprintStamina;
        dst.playerIsExhausted = src.playerIsExhausted;

        dst.hasHeadlamp = src.hasHeadlamp;

        if (src.consumedCheckpointZoneIds != null)
            dst.consumedCheckpointZoneIds = new List<string>(src.consumedCheckpointZoneIds);
        else
            dst.consumedCheckpointZoneIds = null;

        return dst;
    }
}