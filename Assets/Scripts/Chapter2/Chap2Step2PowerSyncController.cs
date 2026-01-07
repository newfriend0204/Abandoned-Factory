using System.Collections;
using UnityEngine;

public class Chap2Step2PowerSyncController : MonoBehaviour {
    private enum SyncState {
        Idle,
        Calibrating,
        Checking,
        Completed
    }

    [System.Serializable]
    private class PhaseNeedle {
        public string label = "A";
        public Transform needle;

        [Header("Runtime")]
        public int speedSteps;
        public float angleDeg;

        [HideInInspector] public Quaternion baseLocalRot;
    }

    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;
    [SerializeField] private Chap2YStepSequenceManager sequenceManager;
    [SerializeField] private TerminalCore terminal;

    [Header("Needles")]
    [SerializeField] private Transform refNeedle;
    [SerializeField] private Vector3 localRotationAxis = Vector3.forward;
    [SerializeField] private PhaseNeedle phaseA = new PhaseNeedle { label = "A" };
    [SerializeField] private PhaseNeedle phaseB = new PhaseNeedle { label = "B" };
    [SerializeField] private PhaseNeedle phaseC = new PhaseNeedle { label = "C" };

    [Header("Speed Steps")]
    [SerializeField] private float degreesPerSecondPerStep = 30f;
    [SerializeField] private int refSpeedSteps = 6;
    [SerializeField] private int trimStepDelta = 1;

    [Header("Initial Phase Speed Steps")]
    [SerializeField] private int initialASteps = 4;
    [SerializeField] private int initialBSteps = 7;
    [SerializeField] private int initialCSteps = 5;

    [Header("Initial Angle")]
    [SerializeField] private bool randomizeInitialAngles = true;
    [SerializeField] private float initialAngleMin = 15f;
    [SerializeField] private float initialAngleMax = 345f;

    [Header("Clamp")]
    [SerializeField] private int minSpeedSteps = 0;
    [SerializeField] private int maxSpeedSteps = 18;

    [Header("Lock Check")]
    [SerializeField] private float lockResultDelaySeconds = 2.5f;
    [SerializeField] private float completeAfterLogsDelaySeconds = 4f;

    [Header("Header Randomization")]
    [SerializeField] private float headerMinIntervalSeconds = 3f;
    [SerializeField] private float headerMaxIntervalSeconds = 7f;

    [TextArea(1, 3)]
    [SerializeField]
    private string[] headerPhrases = new string[] {
        "POWER SYNC / MANUAL TRIM",
        "3-PHASE INPUT REQUIRED",
        "AUTO-SYNC OFFLINE",
        "REF STABLE / PHASES UNLOCKED",
        "LOCK WHEN READY",
        "KEEP IT CLEAN",
        "NO DRIFT TOLERATED"
    };

    [Header("Audio Sources")]
    [SerializeField] private AudioSource buttonSfxSource;
    [SerializeField] private AudioSource systemSfxSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip validateBeepClip;
    [SerializeField] private AudioClip zeroTickClip;

    [Header("Terminal Colors")]
    [SerializeField] private string colorHeader = "#9FE7FF";
    [SerializeField] private string colorInfo = "#C9F1FF";
    [SerializeField] private string colorWarn = "#FFD37A";
    [SerializeField] private string colorError = "#FF6B6B";
    [SerializeField] private string colorSuccess = "#8CFF9A";

    private SyncState state = SyncState.Idle;

    private Quaternion refBaseLocalRot;
    private float refAngleDeg;

    private bool wasSessionActive = false;
    private bool wasInteractionActive = false;
    private bool introPrinted = false;

    private Coroutine headerRoutine;
    private Coroutine lockRoutine;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (terminal == null)
            terminal = FindFirstObjectByType<TerminalCore>();

        if (refNeedle != null)
            refBaseLocalRot = refNeedle.localRotation;

        CachePhaseBaseRotations();
    }

    private void OnEnable() {
        CachePhaseBaseRotations();
    }

    private void OnDisable() {
        StopHeaderLoop();
        StopLockRoutine();

        wasSessionActive = false;
        wasInteractionActive = false;

        state = SyncState.Idle;
        introPrinted = false;
    }

    private void Update() {
        bool sessionActive = IsStep2SessionActive();
        bool interactionActive = sessionActive && IsStep2InteractionActive();

        if (sessionActive && !wasSessionActive)
            OnSessionStart();

        if (!sessionActive && wasSessionActive)
            OnSessionEnd();

        if (interactionActive && !wasInteractionActive)
            OnInteractionEnter();

        if (!interactionActive && wasInteractionActive)
            OnInteractionExit();

        wasSessionActive = sessionActive;
        wasInteractionActive = interactionActive;

        if (!sessionActive)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        TickRef(dt);
        TickPhases(dt);
    }

    private bool IsStep2SessionActive() {
        if (gameManager == null || sequenceManager == null)
            return false;
        if (gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return false;
        if (sequenceManager.CurrentStep != 2)
            return false;

        return true;
    }

    private bool IsStep2InteractionActive() {
        return InteractionModeService.IsInInteractionMode;
    }

    private void OnSessionStart() {
        StopHeaderLoop();
        StopLockRoutine();

        state = SyncState.Calibrating;
        introPrinted = false;

        ResetPuzzle();
    }

    private void OnSessionEnd() {
        StopHeaderLoop();
        StopLockRoutine();

        state = SyncState.Idle;
        introPrinted = false;
    }

    private void OnInteractionEnter() {
        StartHeaderLoop();

        if (introPrinted)
            return;

        introPrinted = true;

        WriteInfo("POWER SYNC ONLINE.");
        WriteWarn("TRIM PHASE A/B/C TO MATCH REF.");
        WriteInfo("PRESS LOCK TO ENGAGE.");
    }

    private void OnInteractionExit() {
        StopHeaderLoop();
    }

    private void ResetPuzzle() {
        float a0 = GetInitialAngle();
        float b0 = GetInitialAngle();
        float c0 = GetInitialAngle();

        refAngleDeg = GetInitialAngle();
        ApplyNeedleRotation(refNeedle, refBaseLocalRot, refAngleDeg);

        phaseA.speedSteps = initialASteps;
        phaseB.speedSteps = initialBSteps;
        phaseC.speedSteps = initialCSteps;

        phaseA.angleDeg = a0;
        phaseB.angleDeg = b0;
        phaseC.angleDeg = c0;

        ApplyNeedleRotation(phaseA.needle, phaseA.baseLocalRot, phaseA.angleDeg);
        ApplyNeedleRotation(phaseB.needle, phaseB.baseLocalRot, phaseB.angleDeg);
        ApplyNeedleRotation(phaseC.needle, phaseC.baseLocalRot, phaseC.angleDeg);
    }

    private float GetInitialAngle() {
        if (!randomizeInitialAngles)
            return 0f;

        float min = Mathf.Clamp(initialAngleMin, 0f, 360f);
        float max = Mathf.Clamp(initialAngleMax, 0f, 360f);
        if (max < min) {
            float tmp = min;
            min = max;
            max = tmp;
        }

        return Random.Range(min, max);
    }

    private void CachePhaseBaseRotations() {
        if (phaseA != null && phaseA.needle != null)
            phaseA.baseLocalRot = phaseA.needle.localRotation;
        if (phaseB != null && phaseB.needle != null)
            phaseB.baseLocalRot = phaseB.needle.localRotation;
        if (phaseC != null && phaseC.needle != null)
            phaseC.baseLocalRot = phaseC.needle.localRotation;
    }

    private void TickRef(float dt) {
        if (refNeedle == null)
            return;

        float degPerSec = refSpeedSteps * degreesPerSecondPerStep;
        refAngleDeg = WrapAngle(refAngleDeg + degPerSec * dt);
        ApplyNeedleRotation(refNeedle, refBaseLocalRot, refAngleDeg);
    }

    private void TickPhases(float dt) {
        TickPhase(phaseA, dt);
        TickPhase(phaseB, dt);
        TickPhase(phaseC, dt);
    }

    private void TickPhase(PhaseNeedle p, float dt) {
        if (p == null || p.needle == null)
            return;

        float prev = p.angleDeg;

        float degPerSec = p.speedSteps * degreesPerSecondPerStep;
        float delta = degPerSec * dt;
        if (delta <= 0f) {
            ApplyNeedleRotation(p.needle, p.baseLocalRot, p.angleDeg);
            return;
        }

        p.angleDeg = WrapAngle(p.angleDeg + delta);

        if (DidWrapThroughZero(prev, p.angleDeg))
            PlaySystem(zeroTickClip);

        ApplyNeedleRotation(p.needle, p.baseLocalRot, p.angleDeg);
    }

    private bool AreAllPhasesSynced() {
        int refSteps = refSpeedSteps;
        if (phaseA.speedSteps != refSteps)
            return false;
        if (phaseB.speedSteps != refSteps)
            return false;
        if (phaseC.speedSteps != refSteps)
            return false;

        return true;
    }

    private string GetBadPhaseLabelList() {
        bool badA = phaseA.speedSteps != refSpeedSteps;
        bool badB = phaseB.speedSteps != refSpeedSteps;
        bool badC = phaseC.speedSteps != refSpeedSteps;

        string s = "";
        if (badA)
            s += "A";
        if (badB)
            s += s.Length > 0 ? "/B" : "B";
        if (badC)
            s += s.Length > 0 ? "/C" : "C";

        return s;
    }

    public void TrimAPlus() { TrimPhase(phaseA, +trimStepDelta); }
    public void TrimAMinus() { TrimPhase(phaseA, -trimStepDelta); }

    public void TrimBPlus() { TrimPhase(phaseB, +trimStepDelta); }
    public void TrimBMinus() { TrimPhase(phaseB, -trimStepDelta); }

    public void TrimCPlus() { TrimPhase(phaseC, +trimStepDelta); }
    public void TrimCMinus() { TrimPhase(phaseC, -trimStepDelta); }

    public void PressLock() {
        if (state != SyncState.Calibrating)
            return;
        if (!IsStep2SessionActive())
            return;
        if (!IsStep2InteractionActive())
            return;

        state = SyncState.Checking;

        PlayButton(buttonClickClip);
        PlaySystem(validateBeepClip);

        WriteWarn("LOCK ENGAGED.");
        WriteWarn("VALIDATING...");

        StartLockRoutine();
    }

    private void TrimPhase(PhaseNeedle p, int deltaSteps) {
        if (p == null)
            return;
        if (state != SyncState.Calibrating)
            return;
        if (!IsStep2SessionActive())
            return;
        if (!IsStep2InteractionActive())
            return;

        PlayButton(buttonClickClip);

        int next = Mathf.Clamp(p.speedSteps + deltaSteps, minSpeedSteps, maxSpeedSteps);
        p.speedSteps = next;
    }

    private void StartLockRoutine() {
        StopLockRoutine();
        lockRoutine = StartCoroutine(CoLockResult());
    }

    private void StopLockRoutine() {
        if (lockRoutine == null)
            return;

        StopCoroutine(lockRoutine);
        lockRoutine = null;
    }

    private IEnumerator CoLockResult() {
        float wait = Mathf.Max(0f, lockResultDelaySeconds);
        float t = 0f;

        while (t < wait) {
            if (!IsStep2SessionActive()) {
                lockRoutine = null;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        bool success = AreAllPhasesSynced();
        if (success) {
            state = SyncState.Completed;

            WriteSuccess("POWER SYNC CONFIRMED.");
            WriteSuccess("STEP 2 COMPLETE.");

            yield return WaitForTerminalTyping();

            float extra = Mathf.Max(0f, completeAfterLogsDelaySeconds);
            if (extra > 0f)
                yield return WaitSecondsWhileSessionActive(extra);

            if (IsStep2SessionActive() && sequenceManager != null)
                sequenceManager.CompleteStep(2);
        } else {
            string bad = GetBadPhaseLabelList();
            if (string.IsNullOrEmpty(bad))
                bad = "A/B/C";

            WriteError($"SYNC ERROR: PHASE {bad} UNSTABLE.");
            WriteInfo("TRIM AND TRY AGAIN.");

            yield return WaitForTerminalTyping();

            if (IsStep2SessionActive())
                state = SyncState.Calibrating;
        }

        lockRoutine = null;
    }

    private IEnumerator WaitSecondsWhileSessionActive(float seconds) {
        float t = 0f;
        while (t < seconds) {
            if (!IsStep2SessionActive())
                yield break;

            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForTerminalTyping() {
        if (terminal == null)
            yield break;

        yield return null;

        while (terminal != null && terminal.IsTyping)
            yield return null;
    }

    private void WriteInfo(string msg) { AppendColored(colorInfo, msg); }
    private void WriteWarn(string msg) { AppendColored(colorWarn, msg); }
    private void WriteError(string msg) { AppendColored(colorError, msg); }
    private void WriteSuccess(string msg) { AppendColored(colorSuccess, msg); }

    private void AppendColored(string hex, string msg) {
        if (terminal == null)
            return;

        string line = $"> <color={hex}>{msg}</color>";
        terminal.AppendLine(line, true);
    }

    private void StartHeaderLoop() {
        StopHeaderLoop();
        headerRoutine = StartCoroutine(CoHeaderLoop());
    }

    private void StopHeaderLoop() {
        if (headerRoutine == null)
            return;

        StopCoroutine(headerRoutine);
        headerRoutine = null;
    }

    private IEnumerator CoHeaderLoop() {
        while (IsStep2SessionActive() && IsStep2InteractionActive()) {
            ApplyRandomHeader();

            float min = Mathf.Max(0.1f, headerMinIntervalSeconds);
            float max = Mathf.Max(min, headerMaxIntervalSeconds);
            float wait = Random.Range(min, max);

            float t = 0f;
            while (t < wait) {
                if (!IsStep2SessionActive() || !IsStep2InteractionActive()) {
                    headerRoutine = null;
                    yield break;
                }

                t += Time.deltaTime;
                yield return null;
            }
        }

        headerRoutine = null;
    }

    private void ApplyRandomHeader() {
        if (terminal == null)
            return;

        if (headerPhrases == null || headerPhrases.Length == 0) {
            terminal.SetHeader(WrapHeader("POWER SYNC"));
            return;
        }

        int idx = Random.Range(0, headerPhrases.Length);
        string phrase = headerPhrases[idx];
        if (string.IsNullOrEmpty(phrase))
            phrase = "POWER SYNC";

        terminal.SetHeader(WrapHeader(phrase));
    }

    private string WrapHeader(string plain) {
        return $"<color={colorHeader}>{plain}</color>";
    }

    private float WrapAngle(float deg) {
        deg %= 360f;
        if (deg < 0f)
            deg += 360f;
        return deg;
    }

    private bool DidWrapThroughZero(float prevAngle, float nowAngle) {
        return nowAngle < prevAngle;
    }

    private void ApplyNeedleRotation(Transform needle, Quaternion baseLocal, float angleDeg) {
        if (needle == null)
            return;

        Vector3 axis = localRotationAxis;
        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.forward;

        needle.localRotation = baseLocal * Quaternion.AngleAxis(angleDeg, axis.normalized);
    }

    private void PlayButton(AudioClip clip) {
        if (clip == null)
            return;
        if (buttonSfxSource == null)
            return;

        buttonSfxSource.PlayOneShot(clip);
    }

    private void PlaySystem(AudioClip clip) {
        if (clip == null)
            return;
        if (systemSfxSource == null)
            return;

        systemSfxSource.PlayOneShot(clip);
    }
}