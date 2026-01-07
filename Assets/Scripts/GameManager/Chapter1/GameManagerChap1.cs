using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public class GameManagerChap1 : MonoBehaviour {
    public enum ChapState {
        Idle,
        Hunting,
        Completed,
        ShutterOpened,
        PowerRestoring,
        MainPowerRestored
    }

    [SerializeField] private ChapState state = ChapState.Idle;
    public ChapState State => state;

    [Header("Progress")]
    [SerializeField] private int totalButtons = 0;
    [SerializeField] private int pressedCount = 0;
    public int PressedCount => pressedCount;

    [System.Serializable] public class IntEvent : UnityEvent<int> { }

    [Header("Events")]
    public IntEvent OnPressedCountChanged;
    public UnityEvent OnAllButtonsActivated;

    private readonly HashSet<ButtonInteract> pressedSet = new HashSet<ButtonInteract>();
    private List<ButtonInteract> allButtons = new List<ButtonInteract>();
    private ButtonChecker activeChecker;

    [Header("GET UI")]
    public GameObject getObject;
    public Image getImage;
    public float getAnimDuration = 0.1f;
    [Range(0, 255)] public int getTargetAlphaByte = 150;
    public float getMoveOffsetY = 70f;

    private RectTransform getRect;
    private Vector2 getBaseAnchoredPos;
    private bool getVisible = false;
    private bool pressablePinged = false;
    private Coroutine getAnimRoutine;
    private TextMeshProUGUI getText;

    [Header("Aux Power")]
    public List<Light> auxPowerLights = new List<Light>();
    [SerializeField] private List<int> auxPowerStates = new List<int> { 0, 0, 0, 0 };

    [Header("Pipe Puzzle")]
    public List<Light> pipePartLights = new List<Light>(3);
    public AudioClip pipeSubmitSuccessSfx;
    public AudioClip pipeSubmitFailSfx;
    [Range(0f, 1f)] public float pipeSubmitVolume = 1f;

    [Header("Monologue")]
    public MonologueManager monologue;

    [Header("Broadcast Announcer UI")]
    public BroadcastAnnouncerUI announcer;

    [Header("Tutorial")]
    public TutorialHintUI tutorialUI;
    public float puzzleTutorialDuration = 6f;

    private readonly Dictionary<int, List<PipePiece>> piecesByPart = new Dictionary<int, List<PipePiece>>();
    private readonly bool[] partSolved = new bool[3];

    private AudioSource _pipeAudio;

    private bool pipeAllCheckpointSaved = false;
    private bool streetLampCheckpointSaved = false;

    [System.Serializable]
    private struct StreetLampNode {
        public Light light;
        public AudioSource audio;
        public Transform t;
        public float dist;
    }

    [Header("Street Lamps")]
    [SerializeField] private float lampStepInterval = 0.10f;
    private List<StreetLampNode> streetLamps = new List<StreetLampNode>();

    [Header("Main Fan")]
    [SerializeField] private Animator mainFanAnimator;
    [SerializeField] private AudioSource mainFanAudio;
    [SerializeField] private float fanBaseSpeed = 1f;
    [SerializeField] private float fanBoostSpeed = 2f;
    [SerializeField] private float fanBaseVolume = 0.3f;
    [SerializeField] private float fanBoostVolume = 1f;
    [SerializeField] private float fanBasePitch = 1f;
    [SerializeField] private float fanBoostPitch = 1.15f;

    [SerializeField] private float fanRampDuration = 2f;
    private Coroutine fanRampRoutine;

    private void Awake() {
        getRect = getObject.GetComponent<RectTransform>();
        getBaseAnchoredPos = getRect.anchoredPosition;
        getObject.SetActive(false);
        var c = getImage.color;
        c.a = 0f;
        getImage.color = c;
        getRect.anchoredPosition = getBaseAnchoredPos;
        getText = getObject.transform.Find("GetText").GetComponent<TextMeshProUGUI>();
        NormalizeAuxStates();
        ApplyAuxColors();

        CollectAndPrepareStreetLamps();
        InitializePipePuzzle();

        ApplyFanImmediateForCurrentState();
    }

    private void LateUpdate() {
        if (pressablePinged) {
            if (!getVisible)
                ShowGetOnce();
        } else {
            if (getVisible)
                HideGetOnce();
        }
        pressablePinged = false;
    }

    private void OnDisable() {
        if (getAnimRoutine != null) {
            StopCoroutine(getAnimRoutine);
            getAnimRoutine = null;
        }
        getVisible = false;
        getObject.SetActive(false);

        if (fanRampRoutine != null) {
            StopCoroutine(fanRampRoutine);
            fanRampRoutine = null;
        }
    }

    public void StartHunt(ButtonChecker originChecker) {
        if (state == ChapState.Hunting || state == ChapState.Completed || state == ChapState.ShutterOpened || state == ChapState.PowerRestoring || state == ChapState.MainPowerRestored) return;
        activeChecker = originChecker;
        allButtons.Clear();
        allButtons.AddRange(FindObjectsByType<ButtonInteract>(FindObjectsSortMode.None));
        totalButtons = allButtons.Count;
        pressedSet.Clear();
        pressedCount = 0;
        OnPressedCountChanged.Invoke(pressedCount);
        foreach (var bi in allButtons)
            bi.PrepareForHunt();
        activeChecker.SetIndicatorHunting();
        state = ChapState.Hunting;
    }

    public void ReportPressed(ButtonInteract bi) {
        if (state != ChapState.Hunting)
            return;
        if (pressedSet.Contains(bi))
            return;
        pressedSet.Add(bi);
        pressedCount++;
        OnPressedCountChanged.Invoke(pressedCount);
        if (totalButtons > 0 && pressedCount >= totalButtons)
            MarkCompleted();
    }

    public void IncrementButtonCount() {
        if (state != ChapState.Hunting)
            return;
        pressedCount++;
        OnPressedCountChanged.Invoke(pressedCount);
        if (totalButtons > 0 && pressedCount >= totalButtons)
            MarkCompleted();
    }

    public void ResetPressedCount() {
        pressedSet.Clear();
        pressedCount = 0;
        totalButtons = 0;
        state = ChapState.Idle;
        OnPressedCountChanged.Invoke(pressedCount);
        activeChecker.SetIndicatorIdle();
        activeChecker = null;
    }

    private void MarkCompleted() {
        state = ChapState.Completed;
        activeChecker.SetIndicatorCompleted();
        OnAllButtonsActivated?.Invoke();
    }

    public void SealShutterOpened() {
        state = ChapState.ShutterOpened;

        var cpMgr = Chap1CheckpointManager.Instance;
        if (cpMgr != null) {
            cpMgr.SaveCheckpointAtCurrentPosition();
        }
    }

    public void Pressable(int mode) {
        string keyLabel = GetInteractKeyLabel();

        if (mode == 1)
            getText.text = $"누르기({keyLabel})";
        else if (mode == 2)
            getText.text = $"조사하기({keyLabel})";
        else if (mode == 3)
            getText.text = $"돌리기({keyLabel})";
        else if (mode == 4)
            getText.text = $"줍기({keyLabel})";
        else if (mode == 5)
            getText.text = $"누르기(좌클릭)";
        else if (mode == 6)
            getText.text = $"내리기({keyLabel})";
        pressablePinged = true;
    }

    private string GetInteractKeyLabel() {
        string fallback = "F";

        var ism = InputSettingsManager.Instance;
        if (ism == null)
            return fallback;

        KeyCode primary = ism.GetPrimaryKey("Interact");
        KeyCode secondary = ism.GetSecondaryKey("Interact");

        if (primary == KeyCode.None && secondary == KeyCode.None)
            return fallback;

        System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
        if (primary != KeyCode.None)
            parts.Add(ism.FormatKeyName(primary));
        if (secondary != KeyCode.None)
            parts.Add(ism.FormatKeyName(secondary));

        if (parts.Count == 0)
            return fallback;

        return string.Join(", ", parts);
    }

    private void ShowGetOnce() {
        if (getAnimRoutine != null)
            StopCoroutine(getAnimRoutine);
        getAnimRoutine = StartCoroutine(CoShowGet());
    }

    private void HideGetOnce() {
        if (getAnimRoutine != null)
            StopCoroutine(getAnimRoutine);
        getAnimRoutine = StartCoroutine(CoHideGet());
    }

    private IEnumerator CoShowGet() {
        getObject.SetActive(true);
        getVisible = true;
        float dur = Mathf.Max(0.0001f, getAnimDuration);
        float t = 0f;
        var col = getImage.color;
        col.a = 0f;
        getImage.color = col;
        getRect.anchoredPosition = getBaseAnchoredPos;
        float targetA = getTargetAlphaByte / 255f;
        Vector2 from = getBaseAnchoredPos;
        Vector2 to = getBaseAnchoredPos + new Vector2(0f, getMoveOffsetY);
        while (t < dur) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            var c = getImage.color;
            c.a = Mathf.Lerp(0f, targetA, e);
            getImage.color = c;
            getRect.anchoredPosition = Vector2.Lerp(from, to, e);
            yield return null;
        }
        var c2 = getImage.color;
        c2.a = targetA;
        getImage.color = c2;
        getRect.anchoredPosition = to;
        getAnimRoutine = null;
    }

    private IEnumerator CoHideGet() {
        float dur = Mathf.Max(0.0001f, getAnimDuration);
        float t = 0f;
        getVisible = false;
        float startA = getImage.color.a;
        Vector2 from = getRect.anchoredPosition;
        Vector2 to = getBaseAnchoredPos;
        while (t < dur) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            var c = getImage.color;
            c.a = Mathf.Lerp(startA, 0f, e);
            getImage.color = c;
            getRect.anchoredPosition = Vector2.Lerp(from, to, e);
            yield return null;
        }
        var c2 = getImage.color;
        c2.a = 0f;
        getImage.color = c2;
        getRect.anchoredPosition = to;
        yield return new WaitForEndOfFrame();
        getObject.SetActive(false);
        getAnimRoutine = null;
    }

    private void NormalizeAuxStates() {
        if (auxPowerStates == null)
            auxPowerStates = new List<int>();
        while (auxPowerStates.Count < 4)
            auxPowerStates.Add(0);
        if (auxPowerStates.Count > 4)
            auxPowerStates.RemoveRange(4, auxPowerStates.Count - 4);
    }

    private void ApplyAuxColors() {
        int n = Mathf.Min(auxPowerLights.Count, auxPowerStates.Count);
        for (int i = 0; i < n; i++) {
            if (auxPowerLights[i] == null)
                continue;
            auxPowerLights[i].color = auxPowerStates[i] == 1 ? Color.green : Color.red;
        }
    }

    public void SetAuxPowerState(int index, int value) {
        SetAuxState(index, value);
    }

    public bool AreAllAuxOn() {
        if (auxPowerStates == null || auxPowerStates.Count < 4)
            return false;
        for (int i = 0; i < 4; i++) {
            if (auxPowerStates[i] != 1)
                return false;
        }
        return true;
    }

    public void TryConfirmMainPower() {
        if (state == ChapState.ShutterOpened)
            state = ChapState.PowerRestoring;
        if (AreAllAuxOn() && state != ChapState.MainPowerRestored) {
            StartFanRampToBoost();
            StartCoroutine(CoLightUpStreetLamps());
            state = ChapState.MainPowerRestored;
        }
    }

    public bool IsAuxOn(int index) {
        if (index < 0 || index >= auxPowerStates.Count)
            return false;
        return auxPowerStates[index] == 1;
    }

    public void SetAuxState(int index, int value) {
        if (index < 0 || index >= auxPowerStates.Count)
            return;
        int v = (value != 0) ? 1 : 0;
        if (auxPowerStates[index] == v)
            return;
        auxPowerStates[index] = v;
        if (index < auxPowerLights.Count && auxPowerLights[index] != null) {
            auxPowerLights[index].color = (v == 1) ? Color.green : Color.red;
        } else {
            ApplyAuxColors();
        }
    }

    public int GetAuxPowerState(int index) {
        NormalizeAuxStates();
        if (index < 0 || index >= auxPowerStates.Count)
            return 0;
        return auxPowerStates[index];
    }

    private void Update() {
        ApplyAuxColors();
    }

    private void Start() {
        //state = ChapState.PowerRestoring;
        //Debug.Log($"[DEBUG] GameManagerChap1: 초기 상태를 {state} 로 설정");
        ApplyFanImmediateForCurrentState();
    }

    public void NorthEasternAreaHintAvailable() {
        announcer.ShowBroadcast("주의: 서부 남쪽 구역에서 이상 신호 감지.");
        tutorialUI.ShowTutorial(2, puzzleTutorialDuration);
    }

    private void InitializePipePuzzle() {
        _pipeAudio = GetComponent<AudioSource>();

        piecesByPart.Clear();
        for (int i = 0; i < partSolved.Length; i++) {
            partSolved[i] = false;
        }

        PipePiece[] pieces;

        pieces = FindObjectsByType<PipePiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < pieces.Length; i++) {
            var p = pieces[i];

            p.CaptureCorrectAndRandomize();

            int partIndex = p.PartIndex;
            if (!piecesByPart.ContainsKey(partIndex)) {
                piecesByPart[partIndex] = new List<PipePiece>();
            }
            piecesByPart[partIndex].Add(p);
        }

        ApplyPipePartLights();
    }

    public void OnValveSubmitted(int partIndex) {
        bool allCorrect = IsPartAllCorrect(partIndex);

        if (allCorrect) {
            partSolved[partIndex] = true;
            _pipeAudio.PlayOneShot(pipeSubmitSuccessSfx, pipeSubmitVolume);
        } else {
            _pipeAudio.PlayOneShot(pipeSubmitFailSfx, pipeSubmitVolume);
        }

        ApplyPipePartLights();

        if (AllPartsSolved()) {
            auxPowerStates[3] = 1;
            ApplyAuxColors();

            if (!pipeAllCheckpointSaved) {
                var cpMgr = Chap1CheckpointManager.Instance;
                if (cpMgr != null)
                    cpMgr.SaveCheckpointAtCurrentPosition();
                pipeAllCheckpointSaved = true;
            }
        }
    }

    private bool IsPartAllCorrect(int partIndex) {
        var list = piecesByPart[partIndex];
        for (int i = 0; i < list.Count; i++) {
            if (!list[i].IsCorrect())
                return false;
        }
        return true;
    }

    private bool AllPartsSolved() {
        for (int i = 0; i < partSolved.Length; i++) {
            if (!partSolved[i])
                return false;
        }
        return true;
    }

    private void ApplyPipePartLights() {
        for (int i = 0; i < pipePartLights.Count; i++) {
            var lt = pipePartLights[i];
            if (lt == null)
                continue;
            lt.color = (i < partSolved.Length && partSolved[i]) ? Color.green : Color.red;
            lt.enabled = true;
        }
    }

    public bool IsPipePartSolved(int partIndex) {
        if (partIndex < 0 || partIndex >= partSolved.Length)
            return false;
        return partSolved[partIndex];
    }

    private void CollectAndPrepareStreetLamps() {
        streetLamps.Clear();
        var origin = transform.position;

        var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < allLights.Length; i++) {
            var l = allLights[i];
            if (l.gameObject.name == "StreetLamp") {
                var a = l.GetComponent<AudioSource>();
                a.spatialBlend = 1f;
                l.enabled = false;

                StreetLampNode node = new StreetLampNode {
                    light = l,
                    audio = a,
                    t = l.transform,
                    dist = Vector3.Distance(origin, l.transform.position)
                };
                streetLamps.Add(node);
            }
        }

        streetLamps.Sort((x, y) => x.dist.CompareTo(y.dist));
    }

    private IEnumerator CoLightUpStreetLamps() {
        yield return new WaitForSeconds(3f);

        for (int i = 0; i < streetLamps.Count; i++) {
            var node = streetLamps[i];
            node.light.enabled = true;
            node.audio.Play();
            yield return new WaitForSeconds(lampStepInterval);
        }

        if (!streetLampCheckpointSaved) {
            var cpMgr = Chap1CheckpointManager.Instance;
            if (cpMgr != null)
                cpMgr.SaveCheckpointAtCurrentPosition();
            streetLampCheckpointSaved = true;
        }
    }

    private void ForceAllStreetLampsOnInstant() {
        for (int i = 0; i < streetLamps.Count; i++) {
            var node = streetLamps[i];

            if (node.light != null)
                node.light.enabled = true;

            if (node.audio != null && node.audio.isPlaying)
                node.audio.Stop();
        }
    }

    public bool AreAllStreetLampsOn() {
        if (streetLamps == null || streetLamps.Count == 0)
            return true;

        for (int i = 0; i < streetLamps.Count; i++) {
            var node = streetLamps[i];
            if (node.light == null)
                continue;
            if (!node.light.enabled)
                return false;
        }

        return true;
    }

    public bool IsMainPowerFullyOnline() {
        return state == ChapState.MainPowerRestored && AreAllAuxOn() && AreAllStreetLampsOn();
    }

    public void ImportCheckpointData(int chapStateInt, int[] auxStates, bool[] pipeSolvedFlags) {
        state = (ChapState)chapStateInt;

        NormalizeAuxStates();
        if (auxStates != null) {
            int n = Mathf.Min(4, auxStates.Length);
            for (int i = 0; i < n; i++) {
                auxPowerStates[i] = auxStates[i];
            }
        }

        for (int i = 0; i < partSolved.Length; i++) {
            partSolved[i] = false;
        }

        if (pipeSolvedFlags != null) {
            int n = Mathf.Min(partSolved.Length, pipeSolvedFlags.Length);
            for (int i = 0; i < n; i++) {
                partSolved[i] = pipeSolvedFlags[i];
            }
        }

        bool allPuzzleSolved = AllPartsSolved();

        if (auxPowerStates[3] == 1 && !allPuzzleSolved) {
            for (int i = 0; i < partSolved.Length; i++) {
                partSolved[i] = true;
            }
            allPuzzleSolved = true;
        } else if (auxPowerStates[3] == 0 && allPuzzleSolved) {
            auxPowerStates[3] = 1;
        }

        ApplyAuxColors();
        ApplyPipePartLights();
        RestorePipePuzzlePiecesFromSolvedState();

        if (state == ChapState.MainPowerRestored) {
            ForceAllStreetLampsOnInstant();
        }

        var checker = FindFirstObjectByType<ButtonChecker>();
        if (checker != null) {
            checker.RestoreFromCheckpoint(state);
        }

        ApplyFanImmediateForCurrentState();
    }

    public void ExportCheckpointData(out int chapStateInt, out int[] auxStates, out bool[] pipeSolvedFlags) {
        chapStateInt = (int)state;

        NormalizeAuxStates();
        auxStates = new int[4];
        for (int i = 0; i < 4; i++) {
            auxStates[i] = auxPowerStates[i];
        }

        pipeSolvedFlags = new bool[partSolved.Length];
        for (int i = 0; i < partSolved.Length; i++) {
            pipeSolvedFlags[i] = partSolved[i];
        }
    }

    public void RestorePipePuzzlePiecesFromSolvedState() {
        foreach (var kvp in piecesByPart) {
            int partIndex = kvp.Key;
            bool solved = IsPipePartSolved(partIndex);
            if (!solved)
                continue;

            var list = kvp.Value;
            for (int i = 0; i < list.Count; i++) {
                var piece = list[i];
                if (piece != null) {
                    piece.CaptureCorrectWithoutRandomize();
                }
            }
        }
    }

    private void ApplyFanImmediateForCurrentState() {
        if (fanRampRoutine != null) {
            StopCoroutine(fanRampRoutine);
            fanRampRoutine = null;
        }

        bool boosted = state == ChapState.MainPowerRestored;

        if (mainFanAnimator != null)
            mainFanAnimator.speed = boosted ? fanBoostSpeed : fanBaseSpeed;

        if (mainFanAudio != null) {
            mainFanAudio.volume = boosted ? fanBoostVolume : fanBaseVolume;
            mainFanAudio.pitch = boosted ? fanBoostPitch : fanBasePitch;
        }
    }

    private void StartFanRampToBoost() {
        if (mainFanAnimator == null && mainFanAudio == null)
            return;

        if (fanRampRoutine != null) {
            StopCoroutine(fanRampRoutine);
            fanRampRoutine = null;
        }

        float startSpeed = mainFanAnimator != null ? mainFanAnimator.speed : fanBaseSpeed;

        float startVolume = fanBaseVolume;
        float startPitch = fanBasePitch;

        if (mainFanAudio != null) {
            startVolume = mainFanAudio.volume;
            startPitch = mainFanAudio.pitch;
        }

        fanRampRoutine = StartCoroutine(CoRampFan(startSpeed, fanBoostSpeed, startVolume, fanBoostVolume, startPitch, fanBoostPitch, fanRampDuration));
    }

    private IEnumerator CoRampFan(float startSpeed, float targetSpeed, float startVolume, float targetVolume, float startPitch, float targetPitch, float duration) {
        float t = 0f;

        while (t < 1f) {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float k = Mathf.SmoothStep(0f, 1f, t);

            if (mainFanAnimator != null)
                mainFanAnimator.speed = Mathf.Lerp(startSpeed, targetSpeed, k);

            if (mainFanAudio != null) {
                mainFanAudio.volume = Mathf.Lerp(startVolume, targetVolume, k);
                mainFanAudio.pitch = Mathf.Lerp(startPitch, targetPitch, k);
            }

            yield return null;
        }

        if (mainFanAnimator != null)
            mainFanAnimator.speed = targetSpeed;

        if (mainFanAudio != null) {
            mainFanAudio.volume = targetVolume;
            mainFanAudio.pitch = targetPitch;
        }

        fanRampRoutine = null;
    }
}