using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LightOut : MonoBehaviour {
    [Header("Board")]
    public List<LightNode> nodes = new List<LightNode>(25);
    public int width = 5;
    public int height = 5;

    [Header("Input")]
    public Camera viewCamera;

    [Header("Status Light")]
    public Light statusLight;
    public Color notClearedColor = Color.red;
    public Color clearedColor = Color.green;
    public float statusLightIntensity = 3.5f;

    [Header("SFX")]
    public AudioSource audioSource;
    public AudioClip clickSfx;
    public AudioClip successSfx;
    public AudioClip powerOnSfx;

    [Header("GameManager")]
    public GameManagerChap1 gameManager;

    [Header("FX")]
    public float neighborRippleDelay = 0.03f;

    [Header("Start Generation")]
    public int minOnAtStart = 3;
    public int maxOnAtStart = 5;
    public int scrambleMovesMin = 8;
    public int scrambleMovesMax = 16;

    [Header("Assist")]
    public float hintUnlockDelay = 10f;

    private bool isCleared = false;
    private bool firstPressed = false;
    private bool hintUnlocked = false;

    void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap1>();
        if (viewCamera == null)
            viewCamera = Camera.main;
    }

    void Start() {
        GenerateSolvableStart();
        UpdateStatusLight(AreAllOn());
    }

    void Update() {
        if (isCleared)
            return;

        // 일시정지 상태에서는 입력(클릭/힌트/정답) 전부 무시
        if (Mathf.Approximately(Time.timeScale, 0f))
            return;

        var input = InputSettingsManager.Instance;

        bool interactPressed = false;
        bool showHintPressed = false;
        bool showSolutionPressed = false;

        if (input != null) {
            interactPressed = input.GetKeyDown("Interact");
            showHintPressed = input.GetKeyDown("ShowHint");
            showSolutionPressed = input.GetKeyDown("ShowSolution");
        }

        if (interactPressed) {
            Ray centerRay = viewCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
            );
            if (Physics.Raycast(centerRay, out var hitF, 100f, ~0, QueryTriggerInteraction.Ignore)) {
                var nodeF = hitF.collider.GetComponentInParent<LightNode>();
                if (nodeF != null && nodeF.IsInteractable(viewCamera)) {
                    int idx = nodes.IndexOf(nodeF);
                    if (idx >= 0) {
                        PlayClick();
                        StartCoroutine(ToggleWithRipple(idx));
                        if (!firstPressed) {
                            firstPressed = true;
                            StartCoroutine(UnlockHintsAfterDelay(hintUnlockDelay));
                        }
                    }
                }
            }
        }

        if (hintUnlocked && showHintPressed) {
            var sol = ComputeSolutionIndices();
            if (sol != null && sol.Count > 0) {
                int next = sol[0];
                nodes[next].Blink(2, 0.05f);
            }
        }

        if (hintUnlocked && showSolutionPressed) {
            var sol = ComputeSolutionIndices();
            if (sol != null && sol.Count > 0) {
                StartCoroutine(PlaySolution(sol));
            }
        }
    }

    IEnumerator ToggleWithRipple(int index) {
        ToggleIndex(index);

        int r = index / width;
        int c = index % width;

        yield return new WaitForSeconds(neighborRippleDelay);
        ToggleRC(r - 1, c);
        yield return new WaitForSeconds(neighborRippleDelay);
        ToggleRC(r, c + 1);
        yield return new WaitForSeconds(neighborRippleDelay);
        ToggleRC(r + 1, c);
        yield return new WaitForSeconds(neighborRippleDelay);
        ToggleRC(r, c - 1);

        bool allOn = AreAllOn();
        UpdateStatusLight(allOn);

        if (allOn && !isCleared) {
            isCleared = true;

            gameManager.SetAuxPowerState(2, 1);

            audioSource.PlayOneShot(successSfx);
            audioSource.PlayOneShot(powerOnSfx);

            LockAllNodesInteraction(true);
            StartCoroutine(CameraShake(0.2f, 0.05f));
        }
    }

    IEnumerator UnlockHintsAfterDelay(float sec) {
        yield return new WaitForSeconds(sec);
        hintUnlocked = true;
        gameManager.NorthEasternAreaHintAvailable();
    }

    void PlayClick() {
        audioSource.PlayOneShot(clickSfx);
    }

    void ToggleIndex(int i) {
        nodes[i].Toggle();
    }

    void ToggleRC(int r, int c) {
        if (r < 0 || r >= height || c < 0 || c >= width)
            return;
        nodes[r * width + c].Toggle();
    }

    bool AreAllOn() {
        return nodes.All(n => n.isOn);
    }

    int CountOn() {
        int cnt = 0;
        for (int i = 0; i < nodes.Count; i++) {
            if (nodes[i].isOn)
                cnt++;
        }
        return cnt;
    }

    void ResetAll(bool on) {
        for (int i = 0; i < nodes.Count; i++)
            nodes[i].SetState(on, animate: false);
    }

    void ApplyMoveInstant(int index) {
        int r = index / width;
        int c = index % width;
        ToggleInstant(index);
        ToggleInstantRC(r - 1, c);
        ToggleInstantRC(r + 1, c);
        ToggleInstantRC(r, c - 1);
        ToggleInstantRC(r, c + 1);
    }

    void ToggleInstant(int i) {
        nodes[i].SetState(!nodes[i].isOn, animate: false);
    }

    void ToggleInstantRC(int r, int c) {
        if (r < 0 || r >= height || c < 0 || c >= width) return;
        int i = r * width + c;
        nodes[i].SetState(!nodes[i].isOn, animate: false);
    }

    void GenerateSolvableStart() {
        bool ok = false;
        while (!ok) {
            ResetAll(false);
            int moves = Random.Range(scrambleMovesMin, scrambleMovesMax + 1);
            for (int k = 0; k < moves; k++) {
                int idx = Random.Range(0, nodes.Count);
                ApplyMoveInstant(idx);
            }
            int on = CountOn();
            ok = (on >= minOnAtStart && on <= maxOnAtStart);
        }
    }

    void UpdateStatusLight(bool cleared) {
        statusLight.color = cleared ? clearedColor : notClearedColor;
        statusLight.intensity = statusLightIntensity;
    }

    void LockAllNodesInteraction(bool locked) {
        for (int i = 0; i < nodes.Count; i++) {
            nodes[i].LockInteraction(locked);
        }
    }

    IEnumerator CameraShake(float duration, float magnitude) {
        var cam = viewCamera.transform;
        Vector3 origin = cam.localPosition;
        float t = 0f;
        while (t < duration) {
            cam.localPosition = origin + (Vector3)Random.insideUnitCircle * magnitude;
            t += Time.deltaTime;
            yield return null;
        }
        cam.localPosition = origin;
    }

    List<int> ComputeSolutionIndices() {
        bool[,] s = new bool[height, width];
        for (int r = 0; r < height; r++) {
            for (int c = 0; c < width; c++) {
                s[r, c] = nodes[r * width + c].isOn;
            }
        }
        bool[,] need = new bool[height, width];
        for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
                need[r, c] = !s[r, c];

        List<int> best = null;
        int bestCount = int.MaxValue;

        for (int mask = 0; mask < (1 << width); mask++) {
            bool[,] grid = Copy(need);
            List<int> presses = new List<int>();

            for (int c = 0; c < width; c++) {
                if (((mask >> c) & 1) != 0) {
                    Press(grid, 0, c);
                    presses.Add(0 * width + c);
                }
            }
            for (int r = 1; r < height; r++) {
                for (int c = 0; c < width; c++) {
                    if (grid[r - 1, c]) {
                        Press(grid, r, c);
                        presses.Add(r * width + c);
                    }
                }
            }
            bool ok = true;
            for (int c = 0; c < width; c++) {
                if (grid[height - 1, c]) { ok = false; break; }
            }
            if (ok && presses.Count < bestCount) {
                best = presses;
                bestCount = presses.Count;
                if (bestCount == 0) break;
            }
        }
        return best;
    }

    void Press(bool[,] grid, int r, int c) {
        ToggleBit(grid, r, c);
        ToggleBit(grid, r - 1, c);
        ToggleBit(grid, r + 1, c);
        ToggleBit(grid, r, c - 1);
        ToggleBit(grid, r, c + 1);
    }
    void ToggleBit(bool[,] grid, int r, int c) {
        if (r < 0 || r >= height || c < 0 || c >= width)
            return;
        grid[r, c] = !grid[r, c];
    }
    bool[,] Copy(bool[,] src) {
        bool[,] dst = new bool[height, width];
        for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
                dst[r, c] = src[r, c];
        return dst;
    }

    IEnumerator PlaySolution(List<int> solution) {
        foreach (var idx in solution) {
            if (isCleared)
                yield break;
            PlayClick();
            yield return ToggleWithRipple(idx);
            yield return new WaitForSeconds(0.3f);
        }
    }
}