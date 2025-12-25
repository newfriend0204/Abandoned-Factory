using System.Collections;
using UnityEngine;

public class Chap2Step4SignalDiagnosticsController : MonoBehaviour {
    private enum SubStage {
        A_Beep,
        B_Sine,
        C_Bars,
        Done
    }

    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;
    [SerializeField] private Chap2YStepSequenceManager sequenceManager;
    [SerializeField] private TerminalCore terminal;

    [Header("Optional Visual Roots")]
    [SerializeField] private GameObject sineVisualRoot;
    [SerializeField] private GameObject barsVisualRoot;

    [Header("Rules")]
    [SerializeField] private int consecutiveRepairsToClear = 3;
    [SerializeField] private float repairWindowSeconds = 2.0f;
    [SerializeField] private Vector2 nextAnomalyDelayRange = new Vector2(2.0f, 5.0f);

    [Header("Terminal Colors")]
    [SerializeField] private string colorHeader = "#9FE7FF";
    [SerializeField] private string colorInfo = "#C9F1FF";
    [SerializeField] private string colorWarn = "#FFD37A";
    [SerializeField] private string colorError = "#FF6B6B";
    [SerializeField] private string colorSuccess = "#8CFF9A";
    [SerializeField] private string colorDim = "#9AA0A6";

    [Header("Audio Outputs (different mixer outputs)")]
    [SerializeField] private AudioSource buttonSfxSource;
    [SerializeField] private AudioSource repairSfxSource;
    [SerializeField] private AudioSource beepSfxSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip buttonPressClip;
    [SerializeField] private AudioClip beepClip;
    [SerializeField] private AudioClip repairSuccessClip;
    [SerializeField] private AudioClip repairFailClip;

    [Header("Audio Timing")]
    [SerializeField] private float repairResultDelaySeconds = 0.5f;

    [Header("A / Beep Timing")]
    [SerializeField] private float beepNormalInterval = 0.55f;
    [SerializeField] private Vector2 beepAnomalyIntervalRange = new Vector2(0.08f, 0.22f);
    [Range(0f, 1f)]
    [SerializeField] private float beepAnomalyLongGapChance = 0.15f;
    [SerializeField] private Vector2 beepAnomalyLongGapRange = new Vector2(0.35f, 0.65f);

    [Header("B / Sine Graph (visual only)")]
    [SerializeField] private LineRenderer sineLine;
    [SerializeField] private int sinePoints = 72;
    [SerializeField] private float sineWidth = 1.6f;
    [SerializeField] private float sineAmplitude = 0.28f;
    [SerializeField] private float sineCyclesAcrossWidth = 2.2f;
    [SerializeField] private float sineScrollSpeed = 1.2f;
    [SerializeField] private float sineAnomalyDistortion = 0.25f;

    [Header("C / Spectrum Bars (visual only)")]
    [SerializeField] private Transform[] spectrumBars;
    [SerializeField] private float barMinScaleY = 0.12f;
    [SerializeField] private float barMaxScaleY = 1.00f;
    [SerializeField] private float barWobbleHz = 10.0f;
    [SerializeField] private float barWobbleAmount = 0.45f;

    [Header("C / Anomaly Spikes")]
    [SerializeField] private float barAnomalySpikeHz = 14.0f;
    [SerializeField] private int barAnomalySpikeCountMax = 2;

    private bool wasSessionActive;
    private bool wasInteractionActive;
    private bool introClearedOnce;

    private SubStage stage = SubStage.A_Beep;

    private bool scanning;
    private bool anomalyActive;
    private float anomalyDeadline;
    private float nextAnomalyAt;

    private int streak;
    private float nextBeepAt;

    private bool repairLockedUntilNextAnomaly;
    private Coroutine repairSoundRoutine;
    private Coroutine completionRoutine;

    private Vector3[] barBaseLocalPos;
    private Vector3[] barBaseLocalScale;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();
        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();
        if (terminal == null)
            terminal = FindFirstObjectByType<TerminalCore>();

        CacheBarsBase();
    }

    private void OnEnable() {
        CacheBarsBase();
    }

    private void OnDisable() {
        StopAllCoroutines();

        wasSessionActive = false;
        wasInteractionActive = false;
        introClearedOnce = false;

        stage = SubStage.A_Beep;
        scanning = false;
        anomalyActive = false;
        streak = 0;

        nextBeepAt = 0f;

        repairLockedUntilNextAnomaly = false;
        repairSoundRoutine = null;
        completionRoutine = null;
    }

    private void Update() {
        bool sessionActive = IsStep4SessionActive();
        bool interactionActive = sessionActive && Chap2StepInteractionService.IsInStepMode;

        if (sessionActive && !wasSessionActive)
            OnSessionStart();

        if (!sessionActive && wasSessionActive)
            OnSessionEnd();

        if (interactionActive && !wasInteractionActive)
            OnInteractionEnter();

        wasSessionActive = sessionActive;
        wasInteractionActive = interactionActive;

        if (!sessionActive)
            return;

        TickAnomalyAndTimers();

        if (scanning)
            TickBeep();

        TickVisuals();
    }

    private bool IsStep4SessionActive() {
        if (gameManager == null || sequenceManager == null)
            return false;

        if (gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return false;

        if (sequenceManager.CurrentStep != 4)
            return false;

        return true;
    }

    private void OnSessionStart() {
        StopCompletionRoutine();
        StopRepairSoundRoutine();

        stage = SubStage.A_Beep;

        scanning = false;
        anomalyActive = false;
        streak = 0;

        nextBeepAt = 0f;

        repairLockedUntilNextAnomaly = false;

        introClearedOnce = false;

        ForceVisualRootsVisible();
        InitializeVisualsStatic();

        ScheduleNextAnomaly(true);
        ApplyHeader();
    }

    private void OnSessionEnd() {
        StopCompletionRoutine();
        StopRepairSoundRoutine();

        scanning = false;
        anomalyActive = false;
        streak = 0;

        nextBeepAt = 0f;

        repairLockedUntilNextAnomaly = false;
    }

    private void OnInteractionEnter() {
        ForceVisualRootsVisible();
        ApplyHeader();

        if (terminal == null)
            return;

        if (!introClearedOnce) {
            introClearedOnce = true;
            terminal.ClearBody(true);
        }

        PrintStatusLine();
    }

    private void ForceVisualRootsVisible() {
        if (sineVisualRoot != null && !sineVisualRoot.activeSelf)
            sineVisualRoot.SetActive(true);

        if (barsVisualRoot != null && !barsVisualRoot.activeSelf)
            barsVisualRoot.SetActive(true);
    }

    private void ApplyHeader() {
        if (terminal == null)
            return;

        string title = $"<color={colorHeader}>STEP 4 / SIGNAL DIAGNOSTICS</color>";
        string mode = $"<color={colorDim}>MODE: {StageLabel(stage)} | SCAN: {(scanning ? "RUN" : "STOP")}</color>";
        terminal.SetHeader(title + "\n" + mode);
    }

    private string StageLabel(SubStage s) {
        if (s == SubStage.A_Beep)
            return "A - BEEP TIMING";
        if (s == SubStage.B_Sine)
            return "B - SINE SHAPE";
        if (s == SubStage.C_Bars)
            return "C - SPECTRUM BARS";
        if (s == SubStage.Done)
            return "DONE";

        return "UNKNOWN";
    }

    public void PressStart() {
        if (!IsStep4SessionActive())
            return;
        if (!Chap2StepInteractionService.IsInStepMode)
            return;
        if (stage == SubStage.Done)
            return;

        PlayButton();

        if (scanning) {
            WriteWarn("SCAN ALREADY RUNNING.");
            PrintStatusLine();
            return;
        }

        scanning = true;

        if (stage == SubStage.A_Beep)
            nextBeepAt = Time.time;

        if (!anomalyActive)
            ScheduleNextAnomaly(true);

        ApplyHeader();
        WriteInfo("SCAN STARTED.");
        PrintStatusLine();
    }

    public void PressStop() {
        if (!IsStep4SessionActive())
            return;
        if (!Chap2StepInteractionService.IsInStepMode)
            return;
        if (stage == SubStage.Done)
            return;

        PlayButton();

        if (!scanning) {
            WriteWarn("SCAN ALREADY STOPPED.");
            PrintStatusLine();
            return;
        }

        scanning = false;
        ApplyHeader();
        WriteWarn("SCAN STOPPED.");
        PrintStatusLine();
    }

    public void PressRepair() {
        if (!IsStep4SessionActive())
            return;
        if (!Chap2StepInteractionService.IsInStepMode)
            return;
        if (stage == SubStage.Done)
            return;

        PlayButton();

        if (repairLockedUntilNextAnomaly) {
            RegisterImmediateFail("REPAIR FAILED.");
            return;
        }

        if (!anomalyActive) {
            RegisterImmediateFail("REPAIR FAILED.");
            return;
        }

        float now = Time.time;
        if (now > anomalyDeadline) {
            RegisterImmediateFail("TOO LATE.");
            return;
        }

        anomalyActive = false;
        repairLockedUntilNextAnomaly = true;

        int target = TargetStreak();
        streak = Mathf.Clamp(streak + 1, 0, target);

        ApplyHeader();
        WriteInfo($"REPAIR ACCEPTED ({streak}/{target}).");
        PrintStatusLine();

        ScheduleNextAnomaly(false);
        PlayRepairSuccessDelayed();

        if (streak >= target)
            OnStageCleared();
    }

    private int TargetStreak() {
        return Mathf.Max(1, consecutiveRepairsToClear);
    }

    private void TickAnomalyAndTimers() {
        float now = Time.time;

        if (!anomalyActive) {
            if (!scanning)
                return;

            if (now >= nextAnomalyAt)
                BeginAnomaly();

            return;
        }

        if (now <= anomalyDeadline)
            return;

        OnRepairWindowFailed();
    }

    private void BeginAnomaly() {
        anomalyActive = true;
        anomalyDeadline = Time.time + Mathf.Max(0.01f, repairWindowSeconds);
        repairLockedUntilNextAnomaly = false;
    }

    private void OnRepairWindowFailed() {
        anomalyActive = false;
        scanning = false;
        streak = 0;

        repairLockedUntilNextAnomaly = false;

        ApplyHeader();

        WriteError("REPAIR FAILED.");
        WriteError("STREAK RESET.");
        PrintStatusLine();

        PlayRepairFailImmediate();
    }

    private void RegisterImmediateFail(string msg) {
        anomalyActive = false;
        scanning = false;
        streak = 0;

        repairLockedUntilNextAnomaly = false;

        ApplyHeader();

        WriteError(msg);
        WriteError("STREAK RESET.");
        PrintStatusLine();

        PlayRepairFailImmediate();
    }

    private void ScheduleNextAnomaly(bool soon) {
        float min = Mathf.Max(0.1f, nextAnomalyDelayRange.x);
        float max = Mathf.Max(min, nextAnomalyDelayRange.y);

        float delay = Random.Range(min, max);
        if (soon)
            delay = Mathf.Min(delay, 1.2f);

        nextAnomalyAt = Time.time + delay;
    }

    private void OnStageCleared() {
        scanning = false;
        anomalyActive = false;
        repairLockedUntilNextAnomaly = false;

        WriteSuccess($"{StageLetter(stage)} VERIFIED.");
        AdvanceStage();
    }

    private void AdvanceStage() {
        streak = 0;
        ScheduleNextAnomaly(false);

        if (stage == SubStage.A_Beep) {
            stage = SubStage.B_Sine;
            ApplyHeader();
            PrintStatusLine();
            return;
        }

        if (stage == SubStage.B_Sine) {
            stage = SubStage.C_Bars;
            ApplyHeader();
            PrintStatusLine();
            return;
        }

        if (stage == SubStage.C_Bars) {
            stage = SubStage.Done;
            ApplyHeader();
            TriggerCompletion();
        }
    }

    private void TriggerCompletion() {
        StopCompletionRoutine();
        completionRoutine = StartCoroutine(CoCompletion());
    }

    private IEnumerator CoCompletion() {
        WriteSuccess("STEP 4 VERIFIED.");
        yield return WaitForTerminalTyping();

        float delay = 4.0f;
        float t = 0f;

        while (t < delay) {
            if (!IsStep4SessionActive()) {
                completionRoutine = null;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (sequenceManager != null)
            sequenceManager.CompleteStep(4);

        completionRoutine = null;
    }

    private void StopCompletionRoutine() {
        if (completionRoutine == null)
            return;

        StopCoroutine(completionRoutine);
        completionRoutine = null;
    }

    private void PlayButton() {
        if (buttonSfxSource == null || buttonPressClip == null)
            return;

        buttonSfxSource.PlayOneShot(buttonPressClip);
    }

    private void PlayRepairFailImmediate() {
        if (repairSfxSource == null || repairFailClip == null)
            return;

        repairSfxSource.PlayOneShot(repairFailClip);
    }

    private void PlayRepairSuccessDelayed() {
        if (repairSfxSource == null || repairSuccessClip == null)
            return;

        StopRepairSoundRoutine();
        repairSoundRoutine = StartCoroutine(CoPlayRepairSuccessAfterDelay());
    }

    private IEnumerator CoPlayRepairSuccessAfterDelay() {
        float d = Mathf.Max(0f, repairResultDelaySeconds);
        if (d > 0f)
            yield return new WaitForSeconds(d);

        if (!IsStep4SessionActive()) {
            repairSoundRoutine = null;
            yield break;
        }

        repairSfxSource.PlayOneShot(repairSuccessClip);
        repairSoundRoutine = null;
    }

    private void StopRepairSoundRoutine() {
        if (repairSoundRoutine == null)
            return;

        StopCoroutine(repairSoundRoutine);
        repairSoundRoutine = null;
    }

    private void TickBeep() {
        if (stage != SubStage.A_Beep)
            return;

        if (beepSfxSource == null || beepClip == null)
            return;

        float now = Time.time;

        if (nextBeepAt <= 0f)
            nextBeepAt = now;

        if (now < nextBeepAt)
            return;

        beepSfxSource.PlayOneShot(beepClip);

        float next = beepNormalInterval;

        if (anomalyActive) {
            float min = Mathf.Max(0.02f, beepAnomalyIntervalRange.x);
            float max = Mathf.Max(min, beepAnomalyIntervalRange.y);
            next = Random.Range(min, max);

            if (Random.value < beepAnomalyLongGapChance) {
                float lgMin = Mathf.Max(0.05f, beepAnomalyLongGapRange.x);
                float lgMax = Mathf.Max(lgMin, beepAnomalyLongGapRange.y);
                next = Random.Range(lgMin, lgMax);
            }
        }

        nextBeepAt = now + Mathf.Max(0.02f, next);
    }

    private void InitializeVisualsStatic() {
        RenderSineFrame(true);
        ApplyBarsFrame(true);
    }

    private void TickVisuals() {
        if (!scanning)
            return;

        RenderSineFrame(false);
        ApplyBarsFrame(false);
    }

    private void RenderSineFrame(bool forceNormal) {
        if (sineLine == null)
            return;

        int n = Mathf.Clamp(sinePoints, 8, 256);
        sineLine.positionCount = n;

        float w = Mathf.Max(0.01f, sineWidth);
        float amp = Mathf.Max(0f, sineAmplitude);
        float cycles = Mathf.Max(0.1f, sineCyclesAcrossWidth);
        float scroll = Time.time * sineScrollSpeed;

        bool distort = !forceNormal && anomalyActive && stage == SubStage.B_Sine;

        for (int i = 0; i < n; i++) {
            float u = (n == 1) ? 0f : (i / (float)(n - 1));
            float x = (u - 0.5f) * w;

            float phase = (u * cycles + scroll) * Mathf.PI * 2f;
            float y = Mathf.Sin(phase) * amp;

            if (distort) {
                float d = sineAnomalyDistortion;
                float noise = (Mathf.PerlinNoise(u * 7.3f, Time.time * 6.2f) - 0.5f) * 2f;
                y += noise * d;

                float clip = amp * 0.65f;
                y = Mathf.Clamp(y, -clip, clip);
            }

            sineLine.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private void CacheBarsBase() {
        if (spectrumBars == null || spectrumBars.Length == 0)
            return;

        int len = spectrumBars.Length;

        if (barBaseLocalPos == null || barBaseLocalPos.Length != len)
            barBaseLocalPos = new Vector3[len];

        if (barBaseLocalScale == null || barBaseLocalScale.Length != len)
            barBaseLocalScale = new Vector3[len];

        for (int i = 0; i < len; i++) {
            Transform b = spectrumBars[i];
            if (b == null)
                continue;

            barBaseLocalPos[i] = b.localPosition;
            barBaseLocalScale[i] = b.localScale;
        }
    }

    private void ApplyBarsFrame(bool forceNormal) {
        if (spectrumBars == null || spectrumBars.Length == 0)
            return;

        if (barBaseLocalPos == null || barBaseLocalPos.Length != spectrumBars.Length)
            CacheBarsBase();

        float minY = Mathf.Max(0.001f, barMinScaleY);
        float maxY = Mathf.Max(minY, barMaxScaleY);

        float hz = Mathf.Max(0.1f, barWobbleHz);
        int tick = Mathf.FloorToInt(Time.time * hz);

        bool isCAnomaly = !forceNormal && anomalyActive && stage == SubStage.C_Bars;

        int spikeA = -1;
        int spikeB = -1;

        if (isCAnomaly) {
            float shz = Mathf.Max(1f, barAnomalySpikeHz);
            int stick = Mathf.FloorToInt(Time.time * shz);
            PickSpikeBars(stick, spectrumBars.Length, Mathf.Clamp(barAnomalySpikeCountMax, 1, 2), out spikeA, out spikeB);
        }

        for (int i = 0; i < spectrumBars.Length; i++) {
            Transform b = spectrumBars[i];
            if (b == null)
                continue;

            float y01 = 0.35f;

            if (!forceNormal) {
                float r = Hash01((uint)(tick * 928371u + i * 611953u + 0xB5297A4Du));
                float centered = (r - 0.5f) * 2f;
                float wob = centered * Mathf.Clamp01(barWobbleAmount);
                y01 = Mathf.Clamp01(0.55f + wob * 0.35f);
            }

            if (isCAnomaly && (i == spikeA || i == spikeB))
                y01 = 1.0f;

            float newY = Mathf.Lerp(minY, maxY, y01);
            newY = Mathf.Clamp(newY, minY, maxY);

            Vector3 baseS = barBaseLocalScale != null && i < barBaseLocalScale.Length ? barBaseLocalScale[i] : b.localScale;
            Vector3 baseP = barBaseLocalPos != null && i < barBaseLocalPos.Length ? barBaseLocalPos[i] : b.localPosition;

            Vector3 s = baseS;
            s.y = newY;
            b.localScale = s;

            float baseScaleY = baseS.y;
            Vector3 p = baseP;
            p.y = baseP.y + (newY - baseScaleY) * 0.5f;
            b.localPosition = p;
        }
    }

    private void PickSpikeBars(int spikeTick, int len, int count, out int a, out int b) {
        a = -1;
        b = -1;

        if (len <= 0)
            return;

        uint seed = (uint)(spikeTick * 2654435761u) ^ 0x9E3779B9u;

        a = (int)(Hash01(seed) * len);
        if (count < 2 || len < 2)
            return;

        uint seed2 = seed ^ 0x85EBCA6Bu;
        b = (int)(Hash01(seed2) * len);

        if (b == a)
            b = (a + 1) % len;
    }

    private float Hash01(uint x) {
        x ^= x >> 16;
        x *= 0x7FEB352Du;
        x ^= x >> 15;
        x *= 0x846CA68Bu;
        x ^= x >> 16;

        return (x & 0x00FFFFFFu) / 16777215f;
    }

    private void PrintStatusLine() {
        if (terminal == null)
            return;

        string s = StageLetter(stage);
        int t = TargetStreak();

        terminal.AppendLine($"> <color={colorDim}>STAGE {s} | STREAK {streak}/{t}</color>", true);
    }

    private string StageLetter(SubStage s) {
        if (s == SubStage.A_Beep)
            return "A";
        if (s == SubStage.B_Sine)
            return "B";
        if (s == SubStage.C_Bars)
            return "C";

        return "?";
    }

    private void WriteInfo(string msg) { AppendColored(colorInfo, msg); }
    private void WriteWarn(string msg) { AppendColored(colorWarn, msg); }
    private void WriteError(string msg) { AppendColored(colorError, msg); }
    private void WriteSuccess(string msg) { AppendColored(colorSuccess, msg); }

    private void AppendColored(string hex, string msg) {
        if (terminal == null)
            return;

        terminal.AppendLine($"> <color={hex}>{msg}</color>", true);
    }

    private IEnumerator WaitForTerminalTyping() {
        if (terminal == null)
            yield break;

        yield return null;

        while (terminal != null && terminal.IsTyping)
            yield return null;
    }
}