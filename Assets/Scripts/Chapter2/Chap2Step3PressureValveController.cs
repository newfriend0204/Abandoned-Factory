using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class Chap2Step3PressureValveController : MonoBehaviour {
    private enum ValveZone {
        Low,
        Stable,
        High
    }

    [System.Serializable]
    private class PressureLine {
        public string label = "A";

        [Header("Refs")]
        public Chap2StepElement valveElement;
        public Transform valveVisual;

        public TMP_Text gaugeText;

        [Header("Indicator")]
        public Transform indicatorCube;
        public float indicatorMinScaleY = 0.10f;
        public float indicatorMaxScaleY = 1.00f;

        [Header("Audio (Valve Loop)")]
        public AudioSource valveLoopSource;

        [Header("Runtime")]
        [HideInInspector] public float pressure01;
        [HideInInspector] public float stableTimer;
        [HideInInspector] public bool verified;
        [HideInInspector] public bool holding;

        [HideInInspector] public ValveZone lastZone;
        [HideInInspector] public float lastZoneLogTime;

        [HideInInspector] public Vector3 indicatorBaseLocalPos;
        [HideInInspector] public Vector3 indicatorBaseLocalScale;
        [HideInInspector] public Renderer indicatorRenderer;
        [HideInInspector] public MaterialPropertyBlock mpb;

        [HideInInspector] public float wobbleSeed;

        [HideInInspector] public float stableMin;
        [HideInInspector] public float stableMax;

        [HideInInspector] public UnityAction pressDownAction;
        [HideInInspector] public UnityAction pressUpAction;
    }

    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;
    [SerializeField] private Chap2YStepSequenceManager sequenceManager;
    [SerializeField] private TerminalCore terminal;

    [Header("Lines")]
    [SerializeField] private PressureLine lineA = new PressureLine { label = "A" };
    [SerializeField] private PressureLine lineB = new PressureLine { label = "B" };
    [SerializeField] private PressureLine lineC = new PressureLine { label = "C" };

    [Header("Stable Window (Randomized per Session, Per Line)")]
    [SerializeField] private float stableWindowWidth = 0.14f;
    [SerializeField] private float stableCenterMin = 0.25f;
    [SerializeField] private float stableCenterMax = 0.75f;

    [Header("Hold Time")]
    [SerializeField] private float stableHoldSeconds = 2.0f;

    [Header("Rates")]
    [SerializeField] private float risePerSecond = 0.38f;
    [SerializeField] private float fallPerSecond = 0.22f;

    [Header("Valve Rotation")]
    [SerializeField] private float valveSpinDegreesPerSecond = 240f;

    [Header("Initial Pressure")]
    [SerializeField] private bool randomizeInitialPressure = true;
    [SerializeField] private float initialMin = 0.10f;
    [SerializeField] private float initialMax = 0.90f;

    [Header("Audio (Hiss Loop)")]
    [SerializeField] private AudioSource hissLoopSource;
    [SerializeField] private Vector2 hissPitchRange = new Vector2(0.85f, 1.20f);
    [SerializeField] private float hissPitchSmoothing = 6.0f;

    [Header("Audio (Hiss Fade Out)")]
    [SerializeField] private float hissFadeOutSeconds = 0.8f;

    [Header("Audio (Valve Loop Pitch Optional)")]
    [SerializeField] private Vector2 valvePitchRange = new Vector2(0.98f, 1.08f);

    [Header("Indicator Wobble (Visual Only)")]
    [SerializeField] private float wobbleStepHz = 22.0f;
    [SerializeField] private float wobbleScaleRangeRatio = 0.10f;
    [SerializeField] private float wobblePressureInfluence = 0.8f;
    [SerializeField] private float headerMinIntervalSeconds = 2.5f;
    [SerializeField] private float headerMaxIntervalSeconds = 6.0f;

    [TextArea(1, 3)]
    [SerializeField]
    private string[] headerPhrases = new string[] {
        "PRESSURE STABILIZATION / MANUAL VALVES",
        "PIPELINE OSCILLATION DETECTED",
        "AUTO-REGULATOR OFFLINE",
        "HOLD STABLE WINDOW 2.0s PER LINE",
        "KEEP IT WITHIN STABLE",
        "DONT OVERPRESSURIZE",
        "MANIFOLD CONTROL ACTIVE"
    };

    [Header("Terminal Colors")]
    [SerializeField] private string colorHeader = "#9FE7FF";
    [SerializeField] private string colorInfo = "#C9F1FF";
    [SerializeField] private string colorWarn = "#FFD37A";
    [SerializeField] private string colorError = "#FF6B6B";
    [SerializeField] private string colorSuccess = "#8CFF9A";

    [Header("Indicator Colors")]
    [SerializeField] private Color lowColor = new Color(0.25f, 0.65f, 1.0f, 1.0f);
    [SerializeField] private Color stableColor = new Color(0.20f, 1.0f, 0.35f, 1.0f);
    [SerializeField] private Color highColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);
    [SerializeField] private Color verifiedColor = new Color(0.20f, 1.0f, 0.35f, 1.0f);

    [Header("Logging (Reduced)")]
    [SerializeField] private float zoneLogCooldownSeconds = 1.2f;

    [Header("Completion")]
    [SerializeField] private float completionLogStepSeconds = 1.0f;
    [SerializeField] private float delayAfterVerifiedSeconds = 4.0f;

    private PressureLine[] lines;
    private bool wasSessionActive = false;
    private Coroutine headerRoutine;
    private Coroutine completionRoutine;

    private bool completionTriggered = false;

    private float hissPitchCurrent = 1.0f;
    private float hissBaseVolume = 1.0f;
    private Coroutine hissFadeRoutine;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (terminal == null)
            terminal = FindFirstObjectByType<TerminalCore>();

        lines = new PressureLine[] { lineA, lineB, lineC };

        if (hissLoopSource != null)
            hissBaseVolume = hissLoopSource.volume;

        CacheRefs();
        BindValveEvents();
    }

    private void OnEnable() {
        CacheRefs();
        BindValveEvents();
    }

    private void OnDisable() {
        StopHeaderLoop();
        StopCompletionRoutine();
        UnbindValveEvents();

        wasSessionActive = false;
        completionTriggered = false;

        StopAllValveLoops();
        StopHissLoopImmediate();

        for (int i = 0; i < lines.Length; i++)
            lines[i].holding = false;
    }

    private void Update() {
        bool sessionActive = IsStep3SessionActive();

        if (sessionActive && !wasSessionActive)
            OnSessionStart();

        if (!sessionActive && wasSessionActive)
            OnSessionEnd();

        wasSessionActive = sessionActive;

        if (!sessionActive)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        if (!completionTriggered)
            TickLines(dt);
        else
            TickVerifiedVisuals();

        UpdateHissAudio(dt);
    }

    private bool IsStep3SessionActive() {
        if (gameManager == null || sequenceManager == null)
            return false;
        if (gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return false;
        if (sequenceManager.CurrentStep != 3)
            return false;

        return true;
    }

    private void OnSessionStart() {
        StopCompletionRoutine();
        completionTriggered = false;

        RandomizeStableWindowsPerLine();
        StartHeaderLoop();

        ResetPuzzle();
        StartHissLoop();

        WriteInfo("STEP 3 / PRESSURE VALVE ALIGNMENT ONLINE.");
    }

    private void OnSessionEnd() {
        StopHeaderLoop();
        StopCompletionRoutine();

        StopAllValveLoops();
        FadeOutHiss();

        for (int i = 0; i < lines.Length; i++)
            lines[i].holding = false;
    }

    private void RandomizeStableWindowsPerLine() {
        float width = Mathf.Clamp01(stableWindowWidth);
        width = Mathf.Clamp(width, 0.04f, 0.40f);

        float cMin = Mathf.Clamp01(stableCenterMin);
        float cMax = Mathf.Clamp01(stableCenterMax);
        if (cMax < cMin)
            Swap(ref cMin, ref cMax);

        float half = width * 0.5f;

        float safeMin = Mathf.Clamp01(cMin);
        float safeMax = Mathf.Clamp01(cMax);

        safeMin = Mathf.Clamp(safeMin, half, 1f - half);
        safeMax = Mathf.Clamp(safeMax, half, 1f - half);

        if (safeMax < safeMin)
            safeMax = safeMin;

        for (int i = 0; i < lines.Length; i++) {
            float center = Random.Range(safeMin, safeMax);

            lines[i].stableMin = Mathf.Clamp01(center - half);
            lines[i].stableMax = Mathf.Clamp01(center + half);

            if (lines[i].stableMax < lines[i].stableMin)
                Swap(ref lines[i].stableMin, ref lines[i].stableMax);
        }
    }

    private void ResetPuzzle() {
        for (int i = 0; i < lines.Length; i++) {
            PressureLine l = lines[i];

            l.verified = false;
            l.holding = false;
            l.stableTimer = 0f;
            l.lastZoneLogTime = -999f;

            if (randomizeInitialPressure)
                l.pressure01 = GetInitialPressureAvoidStable(l);
            else
                l.pressure01 = Mathf.Clamp01(0.2f);

            l.lastZone = GetZone(l, l.pressure01);

            ApplyGaugeText(l);
            ApplyIndicatorVisual(l);
            ApplyValveLoopPitch(l);
        }

        hissPitchCurrent = Mathf.Lerp(hissPitchRange.x, hissPitchRange.y, GetAveragePressure01());
        if (hissLoopSource != null)
            hissLoopSource.pitch = hissPitchCurrent;
    }

    private float GetInitialPressureAvoidStable(PressureLine l) {
        float p = Random.Range(Mathf.Clamp01(initialMin), Mathf.Clamp01(initialMax));

        if (p >= l.stableMin && p <= l.stableMax) {
            float mid = (l.stableMin + l.stableMax) * 0.5f;
            if (p < mid)
                p = Mathf.Max(0f, l.stableMin - 0.10f);
            else
                p = Mathf.Min(1f, l.stableMax + 0.10f);
        }

        return Mathf.Clamp01(p);
    }

    private void TickLines(float dt) {
        bool inStepMode = Chap2StepInteractionService.IsInStepMode;

        for (int i = 0; i < lines.Length; i++) {
            PressureLine l = lines[i];

            if (l.verified) {
                l.holding = false;
                StopValveLoop(l);
                ApplyGaugeText(l);
                ApplyIndicatorVisual(l);
                continue;
            }

            if (!inStepMode && l.holding) {
                l.holding = false;
                StopValveLoop(l);
            }

            if (l.holding) {
                SpinValveY(l, dt);
                EnsureValveLoop(l);
            } else {
                StopValveLoop(l);
            }

            float delta = l.holding ? risePerSecond : -fallPerSecond;
            l.pressure01 = Mathf.Clamp01(l.pressure01 + delta * dt);

            ValveZone zone = GetZone(l, l.pressure01);

            if (zone != l.lastZone)
                OnZoneChangedReduced(l, l.lastZone, zone);

            l.lastZone = zone;

            if (zone == ValveZone.Stable) {
                l.stableTimer += dt;

                if (l.stableTimer >= stableHoldSeconds)
                    VerifyLine(l);
            } else {
                l.stableTimer = 0f;
            }

            ApplyGaugeText(l);
            ApplyIndicatorVisual(l);
            ApplyValveLoopPitch(l);
        }

        if (!completionTriggered && AllVerified())
            TriggerCompletion();
    }

    private void TickVerifiedVisuals() {
        for (int i = 0; i < lines.Length; i++) {
            PressureLine l = lines[i];

            l.holding = false;
            StopValveLoop(l);

            ApplyGaugeText(l);
            ApplyIndicatorVisual(l);
            ApplyValveLoopPitch(l);
        }
    }

    private void SpinValveY(PressureLine l, float dt) {
        if (valveSpinDegreesPerSecond == 0f)
            return;

        Transform t = l.valveVisual;
        if (t == null && l.valveElement != null)
            t = l.valveElement.transform;

        if (t == null)
            return;

        t.Rotate(Vector3.up, valveSpinDegreesPerSecond * dt, Space.Self);
    }

    private ValveZone GetZone(PressureLine l, float p01) {
        if (p01 < l.stableMin)
            return ValveZone.Low;
        if (p01 > l.stableMax)
            return ValveZone.High;

        return ValveZone.Stable;
    }

    private void OnZoneChangedReduced(PressureLine l, ValveZone from, ValveZone to) {
        float now = Time.time;
        if (now - l.lastZoneLogTime < zoneLogCooldownSeconds)
            return;

        l.lastZoneLogTime = now;

        if (to == ValveZone.Stable)
            WriteInfo($"LINE {l.label}: STABLE WINDOW.");
        else if (to == ValveZone.High)
            WriteWarn($"LINE {l.label}: HIGH PRESSURE.");
    }

    private void VerifyLine(PressureLine l) {
        if (l.verified)
            return;

        l.verified = true;
        l.holding = false;

        float center = (l.stableMin + l.stableMax) * 0.5f;
        l.pressure01 = Mathf.Clamp01(center);

        StopValveLoop(l);
        ApplyValveLoopPitch(l);

        WriteSuccess($"LINE {l.label}: VERIFIED.");
    }

    private bool AllVerified() {
        for (int i = 0; i < lines.Length; i++) {
            if (!lines[i].verified)
                return false;
        }

        return true;
    }

    private void TriggerCompletion() {
        completionTriggered = true;

        StopAllValveLoops();

        if (completionRoutine == null)
            completionRoutine = StartCoroutine(CoCompletionSequence());
    }

    private IEnumerator CoCompletionSequence() {
        float step = Mathf.Max(0f, completionLogStepSeconds);

        WriteInfo("FINAL CHECK IN PROGRESS...");
        if (step > 0f)
            yield return new WaitForSeconds(step);

        WriteSuccess("A COMPLETE");
        if (step > 0f)
            yield return new WaitForSeconds(step);

        WriteSuccess("B COMPLETE");
        if (step > 0f)
            yield return new WaitForSeconds(step);

        WriteSuccess("C COMPLETE");
        if (step > 0f)
            yield return new WaitForSeconds(step);

        WriteSuccess("STEP 3 VERIFIED.");

        float delay = Mathf.Max(0f, delayAfterVerifiedSeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (sequenceManager != null)
            sequenceManager.CompleteStep(3);

        completionRoutine = null;
    }

    private void StopCompletionRoutine() {
        if (completionRoutine == null)
            return;

        StopCoroutine(completionRoutine);
        completionRoutine = null;
    }

    private void BindValveEvents() {
        for (int i = 0; i < lines.Length; i++) {
            PressureLine l = lines[i];
            if (l.valveElement == null)
                continue;

            UnbindValveEventsFor(l);

            l.pressDownAction = () => OnValvePress(l, true);
            l.pressUpAction = () => OnValvePress(l, false);

            if (l.valveElement.onPressDown != null)
                l.valveElement.onPressDown.AddListener(l.pressDownAction);

            if (l.valveElement.onPressUp != null)
                l.valveElement.onPressUp.AddListener(l.pressUpAction);
        }
    }

    private void UnbindValveEvents() {
        for (int i = 0; i < lines.Length; i++)
            UnbindValveEventsFor(lines[i]);
    }

    private void UnbindValveEventsFor(PressureLine l) {
        if (l == null || l.valveElement == null)
            return;

        if (l.pressDownAction != null && l.valveElement.onPressDown != null)
            l.valveElement.onPressDown.RemoveListener(l.pressDownAction);

        if (l.pressUpAction != null && l.valveElement.onPressUp != null)
            l.valveElement.onPressUp.RemoveListener(l.pressUpAction);

        l.pressDownAction = null;
        l.pressUpAction = null;
    }

    private void OnValvePress(PressureLine l, bool down) {
        if (!IsStep3SessionActive())
            return;

        if (!Chap2StepInteractionService.IsInStepMode)
            return;

        if (completionTriggered)
            return;

        if (l.verified)
            return;

        l.holding = down;

        if (down)
            EnsureValveLoop(l);
        else
            StopValveLoop(l);
    }

    private void CacheRefs() {
        for (int i = 0; i < lines.Length; i++) {
            PressureLine l = lines[i];

            if (l.valveVisual == null && l.valveElement != null)
                l.valveVisual = l.valveElement.transform;

            if (l.indicatorCube != null) {
                l.indicatorBaseLocalPos = l.indicatorCube.localPosition;
                l.indicatorBaseLocalScale = l.indicatorCube.localScale;

                l.indicatorRenderer = l.indicatorCube.GetComponentInChildren<Renderer>();
                if (l.indicatorRenderer == null)
                    l.indicatorRenderer = l.indicatorCube.GetComponent<Renderer>();

                if (l.mpb == null)
                    l.mpb = new MaterialPropertyBlock();

                if (l.wobbleSeed == 0f)
                    l.wobbleSeed = Random.Range(10f, 9999f);
            }
        }
    }

    private void ApplyGaugeText(PressureLine l) {
        if (l.gaugeText == null)
            return;

        if (l.verified) {
            l.gaugeText.text = $"<color={colorSuccess}>VERIFIED</color>";
            return;
        }

        ValveZone zone = GetZone(l, l.pressure01);

        if (zone == ValveZone.Low)
            l.gaugeText.text = $"<color={colorWarn}>LOW</color>";
        else if (zone == ValveZone.Stable)
            l.gaugeText.text = $"<color={colorSuccess}>STABLE</color>";
        else
            l.gaugeText.text = $"<color={colorError}>HIGH</color>";
    }

    private void ApplyIndicatorVisual(PressureLine l) {
        if (l.indicatorCube == null)
            return;

        float baseY = Mathf.Lerp(l.indicatorMinScaleY, l.indicatorMaxScaleY, Mathf.Clamp01(l.pressure01));
        float wobbleY = GetWobbleScaleY(l, l.pressure01);

        float maxY = Mathf.Max(l.indicatorMaxScaleY, 0f);
        float newY = Mathf.Clamp(baseY + wobbleY, 0f, maxY);

        Vector3 s = l.indicatorBaseLocalScale;
        s.y = newY;
        l.indicatorCube.localScale = s;

        float baseScaleY = l.indicatorBaseLocalScale.y;
        Vector3 p = l.indicatorBaseLocalPos;
        p.y = l.indicatorBaseLocalPos.y + (newY - baseScaleY) * 0.5f;
        l.indicatorCube.localPosition = p;

        Color c = GetIndicatorColor(l);
        ApplyGlowColor(l, c);
    }

    private float GetWobbleScaleY(PressureLine l, float pressure01) {
        int tick = Mathf.FloorToInt(Time.time * Mathf.Max(1f, wobbleStepHz));

        uint seed = (uint)(l.wobbleSeed * 100000f) + 0x9E3779B9u;
        uint x = (uint)tick;
        x ^= seed;
        x *= 1664525u;
        x += 1013904223u;
        x ^= (x >> 16);

        float r01 = (x & 0x00FFFFFFu) / 16777215f;
        float centered = (r01 - 0.5f) * 2f;

        float rangeY = Mathf.Abs(l.indicatorMaxScaleY - l.indicatorMinScaleY);
        float ratio = Mathf.Max(0f, wobbleScaleRangeRatio);
        float amp = rangeY * ratio * Mathf.Lerp(1f, 1f + wobblePressureInfluence, Mathf.Clamp01(pressure01));

        return centered * amp;
    }

    private Color GetIndicatorColor(PressureLine l) {
        if (l.verified)
            return verifiedColor;

        float p = Mathf.Clamp01(l.pressure01);

        if (p <= l.stableMin) {
            float denom = Mathf.Max(0.0001f, l.stableMin);
            float t = Mathf.Clamp01(p / denom);
            return Color.Lerp(lowColor, stableColor, t);
        }

        if (p >= l.stableMax) {
            float denom = Mathf.Max(0.0001f, 1f - l.stableMax);
            float t = Mathf.Clamp01((p - l.stableMax) / denom);
            return Color.Lerp(stableColor, highColor, t);
        }

        return stableColor;
    }

    private void ApplyGlowColor(PressureLine l, Color c) {
        if (l.indicatorRenderer == null)
            return;

        if (l.mpb == null)
            l.mpb = new MaterialPropertyBlock();

        l.indicatorRenderer.GetPropertyBlock(l.mpb);

        l.mpb.SetColor("_BaseColor", c);
        l.mpb.SetColor("_Color", c);
        l.mpb.SetColor("_EmissionColor", c);

        l.indicatorRenderer.SetPropertyBlock(l.mpb);
    }

    private void StartHissLoop() {
        if (hissLoopSource == null)
            return;

        StopHissFadeRoutine();

        hissLoopSource.volume = hissBaseVolume;

        if (!hissLoopSource.isPlaying)
            hissLoopSource.Play();
    }

    private void FadeOutHiss() {
        if (hissLoopSource == null)
            return;

        if (!hissLoopSource.isPlaying)
            return;

        StopHissFadeRoutine();
        hissFadeRoutine = StartCoroutine(CoFadeOutHiss());
    }

    private IEnumerator CoFadeOutHiss() {
        float dur = Mathf.Max(0.01f, hissFadeOutSeconds);
        float startVol = hissLoopSource.volume;

        float t = 0f;
        while (t < dur) {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);
            hissLoopSource.volume = Mathf.Lerp(startVol, 0f, a);
            yield return null;
        }

        hissLoopSource.volume = 0f;
        hissLoopSource.Stop();
        hissLoopSource.volume = hissBaseVolume;

        hissFadeRoutine = null;
    }

    private void StopHissFadeRoutine() {
        if (hissFadeRoutine == null)
            return;

        StopCoroutine(hissFadeRoutine);
        hissFadeRoutine = null;
    }

    private void StopHissLoopImmediate() {
        if (hissLoopSource == null)
            return;

        StopHissFadeRoutine();

        if (hissLoopSource.isPlaying)
            hissLoopSource.Stop();

        hissLoopSource.volume = hissBaseVolume;
    }

    private void UpdateHissAudio(float dt) {
        if (hissLoopSource == null)
            return;

        if (IsStep3SessionActive() && !hissLoopSource.isPlaying) {
            hissLoopSource.volume = hissBaseVolume;
            hissLoopSource.Play();
        }

        float p = GetAveragePressure01();
        float target = Mathf.Lerp(hissPitchRange.x, hissPitchRange.y, p);

        float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, hissPitchSmoothing) * dt);
        hissPitchCurrent = Mathf.Lerp(hissPitchCurrent, target, k);

        hissLoopSource.pitch = Mathf.Clamp(hissPitchCurrent, 0.1f, 3.0f);
    }

    private float GetAveragePressure01() {
        float sum = 0f;
        for (int i = 0; i < lines.Length; i++)
            sum += Mathf.Clamp01(lines[i].pressure01);

        return sum / lines.Length;
    }

    private void EnsureValveLoop(PressureLine l) {
        if (l == null || l.valveLoopSource == null)
            return;

        if (!l.valveLoopSource.isPlaying)
            l.valveLoopSource.Play();
    }

    private void StopValveLoop(PressureLine l) {
        if (l == null || l.valveLoopSource == null)
            return;

        if (l.valveLoopSource.isPlaying)
            l.valveLoopSource.Stop();
    }

    private void StopAllValveLoops() {
        for (int i = 0; i < lines.Length; i++)
            StopValveLoop(lines[i]);
    }

    private void ApplyValveLoopPitch(PressureLine l) {
        if (l == null || l.valveLoopSource == null)
            return;

        float pitch = Mathf.Lerp(valvePitchRange.x, valvePitchRange.y, Mathf.Clamp01(l.pressure01));
        l.valveLoopSource.pitch = Mathf.Clamp(pitch, 0.1f, 3.0f);
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
        while (true) {
            if (terminal != null && headerPhrases != null && headerPhrases.Length > 0) {
                string phrase = headerPhrases[Random.Range(0, headerPhrases.Length)];
                terminal.SetHeader($"<color={colorHeader}>{phrase}</color>");
            }

            float wait = Random.Range(Mathf.Max(0.1f, headerMinIntervalSeconds), Mathf.Max(0.2f, headerMaxIntervalSeconds));
            yield return new WaitForSeconds(wait);
        }
    }

    private void WriteInfo(string msg) {
        if (terminal == null)
            return;

        terminal.AppendLine($"<color={colorInfo}>{msg}</color>", true);
    }

    private void WriteWarn(string msg) {
        if (terminal == null)
            return;

        terminal.AppendLine($"<color={colorWarn}>{msg}</color>", true);
    }

    private void WriteSuccess(string msg) {
        if (terminal == null)
            return;

        terminal.AppendLine($"<color={colorSuccess}>{msg}</color>", true);
    }

    private static void Swap(ref float a, ref float b) {
        float t = a;
        a = b;
        b = t;
    }
}