using System.Collections;
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
    }

    public static Chap1CheckpointManager Instance { get; private set; }

    [Header("Fade")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;

    private static CheckpointData sharedData;
    private CheckpointData current;
    private bool isLoading = false;

    public bool HasCheckpoint => current != null;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sharedData != null)
            current = sharedData;

        FindFadeOverlayInScene();
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

        sharedData = current;

        Debug.Log($"[Chap1CheckpointManager] 체크포인트 저장 완료 (scene='{current.sceneName}', aux=[{string.Join(",", current.auxPowerStates ?? new int[0])}])");
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

        Time.timeScale = 1f;

        if (fadeCanvasGroup != null)
            yield return StartCoroutine(CoFade(0f, 1f));
        else
            yield return null;

        string targetScene = current.sceneName;
        SceneManager.LoadScene(targetScene);

        yield return null;

        FindFadeOverlayInScene();

        ApplyCheckpointToScene();

        if (fadeCanvasGroup != null)
            yield return StartCoroutine(CoFade(1f, 0f));
        else
            yield return null;

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

        Debug.Log($"[Chap1CheckpointManager] 체크포인트 상태 적용 완료 (aux=[{string.Join(",", current.auxPowerStates ?? new int[0])}])");
    }

    private void FindFadeOverlayInScene() {
        if (fadeCanvasGroup != null) return;

        var go = GameObject.Find("FadeOverlay");
        if (go != null) {
            fadeCanvasGroup = go.GetComponent<CanvasGroup>();
            if (fadeCanvasGroup != null)
                fadeCanvasGroup.alpha = 0f;
        }
    }
}