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

    // ==== 런타임/공유 체크포인트 데이터 ====
    private static Chap2CheckpointData sharedData;
    private Chap2CheckpointData current;
    private bool isLoading = false;

    private Coroutine checkpointPopupRoutine;
    private HashSet<string> consumedCheckpointZoneIds = new HashSet<string>();

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

        // 메인 메뉴에서 LoadSharedDataFromFile()를 미리 호출했으면 들어오는 데이터
        if (sharedData != null)
            current = sharedData;

        // 혹시 SaveSystem.Current에 chap2 데이터가 이미 올라와 있는 경우 보정
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

        // Start() 들이 한 프레임 돌아가도록 잠깐 기다렸다가 적용
        yield return null;

        ApplyCheckpointToScene();

        isLoading = false;
    }

    // ==== SaveSystem 연동 (나중에 메인 메뉴에서 쓸 예정) ====

    public static bool HasSaveFile => SaveSystem.HasFile;

    public static void SetAutoLoadOnSceneStart(bool value) {
        autoLoadOnSceneStart = value;
    }

    public static void ClearSharedDataInMemory() {
        sharedData = null;

        if (Instance != null) {
            Instance.current = null;
            Instance.consumedCheckpointZoneIds.Clear();
        }
    }

    public static bool LoadSharedDataFromFile() {
        if (!SaveSystem.LoadFromDisk()) {
            Debug.Log("[Chap2CheckpointManager] 저장된 세이브 파일이 없습니다.");
            sharedData = null;
            return false;
        }

        if (SaveSystem.Current == null ||
            SaveSystem.Current.chap2 == null ||
            !SaveSystem.Current.chap2.hasCheckpoint ||
            SaveSystem.Current.chap2.last == null) {

            Debug.LogWarning("[Chap2CheckpointManager] Chap2 세이브 데이터가 없습니다.");
            sharedData = null;
            return false;
        }

        sharedData = SaveSystem.Current.chap2.last;
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

        data.currentChapter = 2; // Chap2 플레이 중

        data.chap2.hasCheckpoint = true;
        data.chap2.last = Instance.current;

        // 전역 플레이어 상태도 함께 저장
        data.player.hasHeadlamp = Instance.current.hasHeadlamp;
        data.player.savedSprintStamina = Instance.current.playerSprintStamina;
        data.player.savedIsExhausted = Instance.current.playerIsExhausted;

        SaveSystem.SaveToDisk();
    }

    // ==== 체크포인트 저장/로드 ====

    public void SaveCheckpointAtCurrentPosition() {
        SaveInternal(false, null);
    }

    public void SaveCheckpointAtSpawnPoint(Transform spawnPoint) {
        SaveInternal(true, spawnPoint);
    }

    private void SaveInternal(bool useSpawnPoint, Transform spawnPoint) {
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

        // SaveSystem에 반영
        SaveSharedDataToFile();

        Debug.Log($"[Chap2CheckpointManager] 체크포인트 저장 완료 (scene='{current.sceneName}')");

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
            head.canUseHeadlamp = current.hasHeadlamp;
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

    // 나중에 Chap2에서도 CheckpointZone을 쓸 수 있도록 준비해 둔 부분
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
}