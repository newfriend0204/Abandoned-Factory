using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Chap1CheckpointManager : MonoBehaviour {
    [System.Serializable]
    private class CheckpointData {
        public string sceneName;

        public Vector3 playerPosition;
        public Quaternion playerRotation;
        public float playerPitch;
        public float playerSprintStamina;
        public bool playerIsExhausted;

        public int chapStateInt;
        public int[] auxPowerStates;
        public bool[] pipeSolved;

        public List<string> consumedCheckpointZoneIds;
    }

    public static Chap1CheckpointManager Instance { get; private set; }

    [Header("Fade")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;

    [Header("Checkpoint Popup")]
    public CanvasGroup checkpointCanvasGroup;
    public float checkpointFadeDuration = 0.3f;
    public float checkpointHoldDuration = 4f;

    private static CheckpointData sharedData;
    private CheckpointData current;
    private bool isLoading = false;

    private Coroutine checkpointPopupRoutine;

    private HashSet<string> consumedCheckpointZoneIds = new HashSet<string>();

    public bool HasCheckpoint => current != null;

    private void Update() {
        if (Time.timeScale == 0f) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        } else {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sharedData != null)
            current = sharedData;

        if (current != null && current.consumedCheckpointZoneIds != null) {
            consumedCheckpointZoneIds = new HashSet<string>(current.consumedCheckpointZoneIds);
        }

        FindSceneOverlays();
    }

    public void SaveCheckpointAtCurrentPosition() {
        SaveInternal(false, null);
    }

    public void SaveCheckpointAtSpawnPoint(Transform spawnPoint) {
        SaveInternal(true, spawnPoint);
    }

    private void SaveInternal(bool useSpawnPoint, Transform spawnPoint) {
        var player = FindFirstObjectByType<PlayerController>();
        var gm = FindFirstObjectByType<GameManagerChap1>();

        if (current == null)
            current = new CheckpointData();

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

        int chapStateInt;
        int[] auxStates;
        bool[] pipeSolved;
        gm.ExportCheckpointData(out chapStateInt, out auxStates, out pipeSolved);

        current.chapStateInt = chapStateInt;
        current.auxPowerStates = auxStates;
        current.pipeSolved = pipeSolved;

        if (current.consumedCheckpointZoneIds == null)
            current.consumedCheckpointZoneIds = new List<string>(consumedCheckpointZoneIds);
        else {
            current.consumedCheckpointZoneIds.Clear();
            current.consumedCheckpointZoneIds.AddRange(consumedCheckpointZoneIds);
        }

        sharedData = current;

        Debug.Log($"[Chap1CheckpointManager] 체크포인트 저장 완료 (scene='{current.sceneName}')");

        StartCheckpointPopup();
    }

    public void LoadLastCheckpoint() {
        if (current == null) {
            Debug.LogWarning("[Chap1CheckpointManager] 저장된 체크포인트가 없습니다.");
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
        Chap1IntroSequence.skipIntroOnce = true;
        SceneManager.LoadScene(targetScene);

        yield return null;
        TryDisableIntroSequenceOnGameManager();

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
            Debug.LogWarning("[Chap1CheckpointManager] ApplyCheckpointToScene: current 가 null");
            return;
        }

        var player = FindFirstObjectByType<PlayerController>();
        var gm = FindFirstObjectByType<GameManagerChap1>();

        if (player != null) {
            player.ImportCheckpointData(
                current.playerPosition,
                current.playerRotation,
                current.playerPitch,
                current.playerSprintStamina,
                current.playerIsExhausted
            );
        } else {
            Debug.LogWarning("[Chap1CheckpointManager] Player 를 찾지 못해 위치 복원 실패");
        }

        if (gm != null) {
            gm.ImportCheckpointData(
                current.chapStateInt,
                current.auxPowerStates,
                current.pipeSolved
            );
        } else {
            Debug.LogWarning("[Chap1CheckpointManager] GameManagerChap1 를 찾지 못해 진행도 복원 실패");
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

    private void TryDisableIntroSequenceOnGameManager() {
        var gmGo = GameObject.Find("GameManager");
        if (gmGo == null)
            return;

        var intro = gmGo.GetComponent<Chap1IntroSequence>();
        if (intro != null) {
            intro.enabled = false;
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
            current = new CheckpointData();

        if (current.consumedCheckpointZoneIds == null)
            current.consumedCheckpointZoneIds = new List<string>();

        if (!current.consumedCheckpointZoneIds.Contains(id))
            current.consumedCheckpointZoneIds.Add(id);

        sharedData = current;
    }
}