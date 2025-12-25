using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Chap2Step5SafetyInterlockController : MonoBehaviour {
    [System.Serializable]
    private struct InterlockSignal {
        [Range(-1f, 1f)]
        public float center;

        [Min(0f)]
        public float strength;
    }

    private enum StepState {
        Idle,
        Running,
        Completed
    }

    private struct BarVisual {
        public Transform t;
        public SpriteRenderer sr;
        public Graphic g;
        public Renderer r;
        public bool hasMaterialColor;
    }

    private class BarSet {
        public Transform[] bars;
        public Vector3[] basePos;
        public Vector3[] baseScale;
        public float[] phase;
        public BarVisual[] visuals;

        public void BindBars(Transform[] newBars) {
            bars = newBars;
            CacheBase();
            CachePhase();
            CacheVisuals();
        }

        public void CacheBase() {
            if (bars == null || bars.Length == 0)
                return;

            int len = bars.Length;

            if (basePos == null || basePos.Length != len)
                basePos = new Vector3[len];

            if (baseScale == null || baseScale.Length != len)
                baseScale = new Vector3[len];

            for (int i = 0; i < len; i++) {
                Transform b = bars[i];
                if (b == null)
                    continue;

                basePos[i] = b.localPosition;
                baseScale[i] = b.localScale;
            }
        }

        public void CachePhase() {
            if (bars == null || bars.Length == 0)
                return;

            int len = bars.Length;
            if (phase == null || phase.Length != len)
                phase = new float[len];

            for (int i = 0; i < len; i++)
                phase[i] = Random.value * Mathf.PI * 2f;
        }

        public void CacheVisuals() {
            if (bars == null || bars.Length == 0)
                return;

            int len = bars.Length;
            if (visuals == null || visuals.Length != len)
                visuals = new BarVisual[len];

            for (int i = 0; i < len; i++) {
                Transform b = bars[i];

                BarVisual v = new BarVisual();
                v.t = b;

                if (b != null) {
                    v.sr = b.GetComponentInChildren<SpriteRenderer>();
                    if (v.sr == null)
                        v.g = b.GetComponentInChildren<Graphic>();
                    if (v.sr == null && v.g == null) {
                        v.r = b.GetComponentInChildren<Renderer>();
                        if (v.r != null && v.r.material != null && v.r.material.HasProperty("_Color"))
                            v.hasMaterialColor = true;
                    }
                }

                visuals[i] = v;
            }
        }
    }

    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;
    [SerializeField] private Chap2YStepSequenceManager sequenceManager;
    [SerializeField] private TerminalCore terminal;

    [Header("Spectrum Visual (TWO DISPLAYS)")]
    [SerializeField] private Transform[] targetSpectrumBars;
    [SerializeField] private Transform[] liveSpectrumBars;

    [Header("Auto Collect Bars")]
    [SerializeField] private bool autoCollectBars = true;
    [SerializeField] private Transform targetSpectrumRoot;
    [SerializeField] private Transform liveSpectrumRoot;
    [SerializeField] private bool directChildrenOnly = true;
    [SerializeField] private bool includeInactiveBars;
    [SerializeField] private bool reverseCollectedOrder = true;

    [Header("Spectrum Shape")]
    [SerializeField] private float barMinScaleY = 0.10f;
    [SerializeField] private float barMaxScaleY = 1.10f;
    [SerializeField] private float peakWidth01 = 0.18f;
    [SerializeField] private float wobbleHz = 2.2f;
    [SerializeField] private float wobbleAmount01 = 0.20f;
    [SerializeField] private float targetWobbleMultiplier = 0.55f;
    [SerializeField] private float liveWobbleMultiplier = 1.00f;

    [Header("Strength Visual Mapping")]
    [SerializeField] private float displayStrengthMin = 0.50f;
    [SerializeField] private float displayStrengthMax = 1.70f;
    [Range(0f, 0.25f)]
    [SerializeField] private float peakFloor01 = 0.00f;

    [Header("Bar Colors")]
    [SerializeField] private string barColorTarget = "#74D9FF";
    [SerializeField] private string barColorLiveNormal = "#A7B8FF";
    [SerializeField] private string barColorLiveNearMatch = "#8CFF9A";
    [SerializeField] private string barColorLiveLockout = "#FF6B6B";

    [Header("Signals")]
    [SerializeField] private int totalCaptures = 4;
    [SerializeField] private Vector2 randomCenterRange = new Vector2(-0.75f, 0.75f);
    [SerializeField] private Vector2 randomStrengthRange = new Vector2(0.70f, 1.30f);

    [Header("Controls")]
    [SerializeField] private float tuneStep = 0.08f;
    [SerializeField] private float gainStep = 0.06f;
    [SerializeField] private float tuneMin = -1.0f;
    [SerializeField] private float tuneMax = 1.0f;
    [SerializeField] private float gainMin = 0.50f;
    [SerializeField] private float gainMax = 1.70f;

    [Header("Success Tolerances (eyeballing)")]
    [SerializeField] private float centerTolerance = 0.18f;
    [SerializeField] private float strengthTolerance = 0.22f;

    [Header("Timings")]
    [SerializeField] private float confirmResultDelaySeconds = 0.5f;
    [SerializeField] private float lockoutSeconds = 2.5f;
    [SerializeField] private float completeAfterLogsDelaySeconds = 4.0f;

    [Header("Header Refresh")]
    [SerializeField] private float headerRefreshHz = 10f;

    [Header("Audio")]
    [SerializeField] private AudioSource dialSfxSource;
    [SerializeField] private AudioSource buttonSfxSource;
    [SerializeField] private AudioSource resultSfxSource;
    [SerializeField] private AudioSource noiseLoopSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip dialTickClip;
    [SerializeField] private AudioClip buttonPressClip;
    [SerializeField] private AudioClip confirmSuccessClip;
    [SerializeField] private AudioClip confirmFailClip;
    [SerializeField] private AudioClip spectrumNoiseLoopClip;

    [Header("Noise Fade")]
    [SerializeField] private float noiseFadeInSeconds = 0.35f;
    [SerializeField] private float noiseFadeOutSeconds = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float noiseMaxVolume = 1.0f;

    [Header("Terminal Colors")]
    [SerializeField] private string colorHeader = "#9FE7FF";
    [SerializeField] private string colorInfo = "#C9F1FF";
    [SerializeField] private string colorWarn = "#FFD37A";
    [SerializeField] private string colorError = "#FF6B6B";
    [SerializeField] private string colorSuccess = "#8CFF9A";
    [SerializeField] private string colorDim = "#9AA0A6";

    private bool wasSessionActive;
    private bool wasInteractionActive;
    private bool introClearedOnce;

    private StepState state = StepState.Idle;

    private InterlockSignal[] signals;
    private bool signalsGenerated;

    private int captureIndex;

    private float tune;
    private float gain = 1f;

    private float lockoutUntil;
    private bool confirmPending;
    private float pendingPressedAt;
    private float pendingTune;
    private float pendingGain;
    private int pendingCaptureIndex;
    private Coroutine confirmRoutine;
    private Coroutine completionRoutine;

    private bool headerDirty = true;
    private float nextHeaderAt;

    private BarSet targetSet = new BarSet();
    private BarSet liveSet = new BarSet();

    private Color cTarget;
    private Color cLiveNormal;
    private Color cLiveNear;
    private Color cLiveLockout;
    private bool cachedColors;

    private Transform lastTargetRoot;
    private Transform lastLiveRoot;
    private int lastTargetChildCount = -1;
    private int lastLiveChildCount = -1;
    private bool lastDirectChildrenOnly;
    private bool lastIncludeInactiveBars;
    private bool lastReverseCollectedOrder;
    private bool lastAutoCollectBars;

    private Coroutine noiseFadeRoutine;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();
        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();
        if (terminal == null)
            terminal = FindFirstObjectByType<TerminalCore>();

        RefreshBarListsIfNeeded(true);
        targetSet.BindBars(targetSpectrumBars);
        liveSet.BindBars(liveSpectrumBars);

        CacheColors();
        EnsureSignalsGenerated();
    }

    private void OnEnable() {
        RefreshBarListsIfNeeded(true);
        targetSet.BindBars(targetSpectrumBars);
        liveSet.BindBars(liveSpectrumBars);

        CacheColors();
        EnsureSignalsGenerated();
    }

    private void OnDisable() {
        StopAllCoroutines();

        wasSessionActive = false;
        wasInteractionActive = false;
        introClearedOnce = false;

        state = StepState.Idle;

        confirmPending = false;
        pendingPressedAt = 0f;
        pendingTune = 0f;
        pendingGain = 1f;
        pendingCaptureIndex = 0;
        lockoutUntil = 0f;

        confirmRoutine = null;
        completionRoutine = null;

        noiseFadeRoutine = null;

        StopNoiseImmediate();
    }

    private void Update() {
        bool sessionActive = IsStep5SessionActive();
        bool interactionActive = sessionActive && Chap2StepInteractionService.IsInStepMode;

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

        RefreshBarListsIfNeeded(false);
        TickVisuals();
        TickHeader();
    }

    private bool IsStep5SessionActive() {
        if (gameManager == null || sequenceManager == null)
            return false;
        if (gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return false;
        if (sequenceManager.CurrentStep != 5)
            return false;

        return true;
    }

    private void OnSessionStart() {
        StopCompletionRoutine();
        StopConfirmRoutine();

        state = StepState.Running;
        captureIndex = 0;

        tune = 0f;
        gain = 1f;

        confirmPending = false;
        lockoutUntil = 0f;
        pendingPressedAt = 0f;
        pendingTune = 0f;
        pendingGain = 1f;
        pendingCaptureIndex = 0;

        EnsureSignalsGenerated();
        InitializeBarsFrame();

        introClearedOnce = false;

        StartNoiseWithFadeIn();

        MarkHeaderDirty();
        TickHeader();
    }

    private void OnSessionEnd() {
        StopCompletionRoutine();
        StopConfirmRoutine();

        confirmPending = false;
        lockoutUntil = 0f;
        pendingPressedAt = 0f;

        FadeOutAndStopNoise();

        introClearedOnce = false;
    }

    private void OnInteractionEnter() {
        EnsureSignalsGenerated();
        MarkHeaderDirty();
        TickHeader();

        if (terminal == null)
            return;

        if (!introClearedOnce) {
            introClearedOnce = true;
            terminal.ClearBody(true);
            WriteInfo("SAFETY INTERLOCK CAPTURE ONLINE.");
            PrintStatusLine();
        }

        MarkHeaderDirty();
        TickHeader();
    }

    private void OnInteractionExit() {
    }

    public void PressTuneMinus() {
        if (!CanAdjust())
            return;

        tune = Mathf.Clamp(tune - Mathf.Abs(tuneStep), tuneMin, tuneMax);
        PlayDialTick();
        MarkHeaderDirty();
    }

    public void PressTunePlus() {
        if (!CanAdjust())
            return;

        tune = Mathf.Clamp(tune + Mathf.Abs(tuneStep), tuneMin, tuneMax);
        PlayDialTick();
        MarkHeaderDirty();
    }

    public void PressGainMinus() {
        if (!CanAdjust())
            return;

        gain = Mathf.Clamp(gain - Mathf.Abs(gainStep), gainMin, gainMax);
        PlayDialTick();
        MarkHeaderDirty();
    }

    public void PressGainPlus() {
        if (!CanAdjust())
            return;

        gain = Mathf.Clamp(gain + Mathf.Abs(gainStep), gainMin, gainMax);
        PlayDialTick();
        MarkHeaderDirty();
    }

    public void PressConfirm() {
        if (!IsStep5SessionActive())
            return;
        if (!Chap2StepInteractionService.IsInStepMode)
            return;
        if (state != StepState.Running)
            return;
        if (confirmPending)
            return;

        float now = Time.time;
        if (now < lockoutUntil)
            return;

        PlayButtonPress();

        confirmPending = true;
        pendingPressedAt = now;
        pendingTune = tune;
        pendingGain = gain;
        pendingCaptureIndex = captureIndex;
        MarkHeaderDirty();

        StopConfirmRoutine();
        confirmRoutine = StartCoroutine(CoConfirmResult());
    }

    private bool CanAdjust() {
        if (!IsStep5SessionActive())
            return false;
        if (!Chap2StepInteractionService.IsInStepMode)
            return false;
        if (state != StepState.Running)
            return false;

        return true;
    }

    private IEnumerator CoConfirmResult() {
        float delay = Mathf.Max(0f, confirmResultDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!IsStep5SessionActive()) {
            confirmRoutine = null;
            yield break;
        }

        bool ok = EvaluateSnapshotAsSuccess(pendingCaptureIndex, pendingTune, pendingGain);

        if (ok) {
            PlayResult(true);
            OnCaptureSuccess();
        } else {
            PlayResult(false);
            OnCaptureFail();
        }

        confirmPending = false;
        MarkHeaderDirty();
        confirmRoutine = null;
    }

    private void StopConfirmRoutine() {
        if (confirmRoutine == null)
            return;

        StopCoroutine(confirmRoutine);
        confirmRoutine = null;
    }

    private bool EvaluateSnapshotAsSuccess(int index, float tuneSnapshot, float gainSnapshot) {
        EnsureSignalsGenerated();

        if (signals == null || signals.Length == 0)
            return false;
        if (index < 0 || index >= signals.Length)
            return false;

        InterlockSignal target = signals[index];

        float cTol = Mathf.Max(0.01f, centerTolerance);
        float gTol = Mathf.Max(0.01f, strengthTolerance);

        if (Mathf.Abs(tuneSnapshot - target.center) > cTol)
            return false;
        if (Mathf.Abs(gainSnapshot - target.strength) > gTol)
            return false;

        return true;
    }

    private void OnCaptureSuccess() {
        int total = Mathf.Max(1, totalCaptures);
        int shown = Mathf.Clamp(captureIndex + 1, 1, total);

        WriteSuccess($"CAPTURE {shown}/{total} OK.");
        PrintStatusLine();

        captureIndex++;

        MarkHeaderDirty();

        if (captureIndex < total)
            return;

        state = StepState.Completed;
        MarkHeaderDirty();
        TriggerCompletion();
    }

    private void OnCaptureFail() {
        float endAt = pendingPressedAt + Mathf.Max(0f, lockoutSeconds);
        lockoutUntil = Mathf.Max(lockoutUntil, endAt);

        WriteError("CAPTURE FAILED.");
        WriteWarn("LOCKOUT.");
        PrintStatusLine();

        MarkHeaderDirty();
    }

    private void TriggerCompletion() {
        StopCompletionRoutine();
        completionRoutine = StartCoroutine(CoCompletion());
    }

    private IEnumerator CoCompletion() {
        WriteSuccess("ALL INTERLOCKS CLEAR.");
        yield return WaitForTerminalTyping();

        float delay = Mathf.Max(0f, completeAfterLogsDelaySeconds);
        float t = 0f;

        while (t < delay) {
            if (!IsStep5SessionActive()) {
                completionRoutine = null;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (sequenceManager != null)
            sequenceManager.CompleteStep(5);

        completionRoutine = null;
    }

    private void StopCompletionRoutine() {
        if (completionRoutine == null)
            return;

        StopCoroutine(completionRoutine);
        completionRoutine = null;
    }

    private void CacheColors() {
        cTarget = ParseColorOr(barColorTarget, new Color(0.45f, 0.85f, 1.0f, 1f));
        cLiveNormal = ParseColorOr(barColorLiveNormal, new Color(0.65f, 0.70f, 1.0f, 1f));
        cLiveNear = ParseColorOr(barColorLiveNearMatch, new Color(0.55f, 1.0f, 0.60f, 1f));
        cLiveLockout = ParseColorOr(barColorLiveLockout, new Color(1.0f, 0.45f, 0.45f, 1f));
        cachedColors = true;
    }

    private Color ParseColorOr(string html, Color fallback) {
        if (string.IsNullOrEmpty(html))
            return fallback;

        if (ColorUtility.TryParseHtmlString(html, out Color c))
            return c;

        return fallback;
    }

    private void InitializeBarsFrame() {
        ApplyBarsFrame(true);
    }

    private void TickVisuals() {
        ApplyBarsFrame(false);
    }

    private void ApplyBarsFrame(bool forceNeutral) {
        if (!cachedColors)
            CacheColors();

        EnsureSignalsGenerated();

        int total = Mathf.Max(1, totalCaptures);
        int idx = Mathf.Clamp(captureIndex, 0, total - 1);

        InterlockSignal target = default;
        if (signals != null && signals.Length > idx)
            target = signals[idx];

        float tCenter = Mathf.Clamp(target.center, -1f, 1f);
        float tStrength = Mathf.Clamp(target.strength, 0f, 2f);

        float lCenter = Mathf.Clamp(tune, tuneMin, tuneMax);
        float lStrength = Mathf.Clamp(gain, gainMin, gainMax);

        float minY = Mathf.Max(0.0001f, barMinScaleY);
        float maxY = Mathf.Max(minY, barMaxScaleY);

        float w = Mathf.Max(0.02f, peakWidth01);
        float hz = Mathf.Max(0.1f, wobbleHz);
        float wobA = Mathf.Clamp01(wobbleAmount01);

        float targetWob = wobA * Mathf.Max(0f, targetWobbleMultiplier);
        float liveWob = wobA * Mathf.Max(0f, liveWobbleMultiplier);

        float now = Time.time;

        bool lockout = !forceNeutral && Time.time < lockoutUntil;

        float cTol = Mathf.Max(0.01f, centerTolerance);
        float gTol = Mathf.Max(0.01f, strengthTolerance);

        float centerErr = Mathf.Abs(lCenter - tCenter);
        float gainErr = Mathf.Abs(lStrength - tStrength);

        float match01 = 1f - Mathf.Max(centerErr / cTol, gainErr / gTol);
        match01 = Mathf.Clamp01(match01);

        Color liveColor = lockout ? cLiveLockout : Color.Lerp(cLiveNormal, cLiveNear, match01);

        RenderBars(targetSet, tCenter, tStrength, cTarget, true, targetWob, forceNeutral, minY, maxY, w, hz, now);
        RenderBars(liveSet, lCenter, lStrength, liveColor, true, liveWob, forceNeutral, minY, maxY, w, hz, now);
    }

    private void RenderBars(BarSet set, float center, float strength, Color color, bool allowWobble, float wobbleAmount, bool forceNeutral, float minY, float maxY, float width01, float hz, float now) {
        if (set == null)
            return;
        if (set.bars == null || set.bars.Length == 0)
            return;

        if (set.basePos == null || set.basePos.Length != set.bars.Length)
            set.CacheBase();
        if (set.phase == null || set.phase.Length != set.bars.Length)
            set.CachePhase();
        if (set.visuals == null || set.visuals.Length != set.bars.Length)
            set.CacheVisuals();

        float c = forceNeutral ? 0f : Mathf.Clamp(center, -1f, 1f);
        float s = forceNeutral ? 1f : strength;

        float dsMin = Mathf.Min(displayStrengthMin, displayStrengthMax);
        float dsMax = Mathf.Max(displayStrengthMin, displayStrengthMax);
        if (Mathf.Abs(dsMax - dsMin) < 0.0001f)
            dsMax = dsMin + 0.0001f;

        float amp = Mathf.InverseLerp(dsMin, dsMax, s);
        amp = Mathf.Clamp01(amp);
        amp = Mathf.Clamp01(Mathf.Max(amp, peakFloor01));

        float span = maxY - minY;

        for (int i = 0; i < set.bars.Length; i++) {
            Transform b = set.bars[i];
            if (b == null)
                continue;

            float u = set.bars.Length == 1 ? 0.5f : (i / (float)(set.bars.Length - 1));
            float x = Mathf.Lerp(-1f, 1f, u);

            float d = (x - c) / width01;
            float peak = Mathf.Exp(-0.5f * d * d);

            float wob = 0f;
            if (!forceNeutral && allowWobble) {
                float wobAmp = wobbleAmount * amp;
                if (wobAmp > 0f) {
                    float tt = now * (hz * Mathf.PI * 2f) + set.phase[i];
                    wob = Mathf.Sin(tt) * wobAmp;
                }
            }

            float height01 = amp * peak + wob;
            height01 = Mathf.Clamp01(height01);

            float newY = minY + span * height01;
            if (newY > maxY)
                newY = maxY;
            if (newY < minY)
                newY = minY;

            Vector3 baseS = set.baseScale[i];
            Vector3 baseP = set.basePos[i];

            Vector3 sc = baseS;
            sc.y = newY;
            b.localScale = sc;

            float baseScaleY = baseS.y;
            Vector3 p = baseP;
            p.y = baseP.y + (newY - baseScaleY) * 0.5f;
            b.localPosition = p;

            ApplyBarColor(set, i, color);
        }
    }

    private void ApplyBarColor(BarSet set, int index, Color c) {
        if (set.visuals == null || index < 0 || index >= set.visuals.Length)
            return;

        BarVisual v = set.visuals[index];
        if (v.sr != null) {
            v.sr.color = c;
            return;
        }

        if (v.g != null) {
            v.g.color = c;
            return;
        }

        if (v.r != null && v.hasMaterialColor)
            v.r.material.color = c;
    }

    private void RefreshBarListsIfNeeded(bool force) {
        if (!autoCollectBars) {
            if (force)
                BindSetsFromArrays();

            lastAutoCollectBars = autoCollectBars;
            return;
        }

        bool settingsChanged = force;
        if (lastAutoCollectBars != autoCollectBars)
            settingsChanged = true;
        if (lastDirectChildrenOnly != directChildrenOnly)
            settingsChanged = true;
        if (lastIncludeInactiveBars != includeInactiveBars)
            settingsChanged = true;
        if (lastReverseCollectedOrder != reverseCollectedOrder)
            settingsChanged = true;

        bool targetChanged = settingsChanged;
        bool liveChanged = settingsChanged;

        if (!targetChanged) {
            if (lastTargetRoot != targetSpectrumRoot)
                targetChanged = true;
            else if (targetSpectrumRoot != null && lastTargetChildCount != targetSpectrumRoot.childCount)
                targetChanged = true;
        }

        if (!liveChanged) {
            if (lastLiveRoot != liveSpectrumRoot)
                liveChanged = true;
            else if (liveSpectrumRoot != null && lastLiveChildCount != liveSpectrumRoot.childCount)
                liveChanged = true;
        }

        if (!targetChanged && !liveChanged)
            return;

        if (targetChanged) {
            if (targetSpectrumRoot != null)
                targetSpectrumBars = CollectBars(targetSpectrumRoot);
            lastTargetRoot = targetSpectrumRoot;
            lastTargetChildCount = targetSpectrumRoot != null ? targetSpectrumRoot.childCount : -1;
        }

        if (liveChanged) {
            if (liveSpectrumRoot != null)
                liveSpectrumBars = CollectBars(liveSpectrumRoot);
            lastLiveRoot = liveSpectrumRoot;
            lastLiveChildCount = liveSpectrumRoot != null ? liveSpectrumRoot.childCount : -1;
        }

        lastAutoCollectBars = autoCollectBars;
        lastDirectChildrenOnly = directChildrenOnly;
        lastIncludeInactiveBars = includeInactiveBars;
        lastReverseCollectedOrder = reverseCollectedOrder;

        BindSetsFromArrays();
    }

    private void BindSetsFromArrays() {
        if (targetSet == null)
            targetSet = new BarSet();
        if (liveSet == null)
            liveSet = new BarSet();

        if (targetSet.bars != targetSpectrumBars)
            targetSet.BindBars(targetSpectrumBars);

        if (liveSet.bars != liveSpectrumBars)
            liveSet.BindBars(liveSpectrumBars);
    }

    private Transform[] CollectBars(Transform root) {
        if (root == null)
            return null;

        Transform[] arr;
        if (directChildrenOnly)
            arr = CollectDirectChildren(root);
        else
            arr = CollectAllDescendants(root);

        if (arr == null)
            return null;

        if (reverseCollectedOrder)
            System.Array.Reverse(arr);

        return arr;
    }

    private Transform[] CollectDirectChildren(Transform root) {
        int n = root.childCount;
        if (n <= 0)
            return new Transform[0];

        Transform[] arr = new Transform[n];
        for (int i = 0; i < n; i++)
            arr[i] = root.GetChild(i);

        return arr;
    }

    private Transform[] CollectAllDescendants(Transform root) {
        Transform[] all = root.GetComponentsInChildren<Transform>(includeInactiveBars);
        if (all == null || all.Length == 0)
            return new Transform[0];

        int count = 0;
        for (int i = 0; i < all.Length; i++) {
            if (all[i] == root)
                continue;
            count++;
        }

        Transform[] arr = new Transform[count];
        int w = 0;

        for (int i = 0; i < all.Length; i++) {
            if (all[i] == root)
                continue;

            arr[w] = all[i];
            w++;
        }

        return arr;
    }

    private void EnsureSignalsGenerated() {
        if (signalsGenerated)
            return;

        int n = Mathf.Max(1, totalCaptures);
        signals = new InterlockSignal[n];

        float cMin = Mathf.Min(randomCenterRange.x, randomCenterRange.y);
        float cMax = Mathf.Max(randomCenterRange.x, randomCenterRange.y);

        float sMin = Mathf.Min(randomStrengthRange.x, randomStrengthRange.y);
        float sMax = Mathf.Max(randomStrengthRange.x, randomStrengthRange.y);

        for (int i = 0; i < n; i++) {
            signals[i] = new InterlockSignal {
                center = Random.Range(cMin, cMax),
                strength = Random.Range(sMin, sMax)
            };
        }

        signalsGenerated = true;
    }

    private void StartNoiseWithFadeIn() {
        if (noiseLoopSource == null || spectrumNoiseLoopClip == null)
            return;

        if (noiseFadeRoutine != null)
            StopCoroutine(noiseFadeRoutine);

        if (noiseLoopSource.clip != spectrumNoiseLoopClip)
            noiseLoopSource.clip = spectrumNoiseLoopClip;

        noiseLoopSource.loop = true;

        if (!noiseLoopSource.isPlaying) {
            noiseLoopSource.volume = 0f;
            noiseLoopSource.Play();
        }

        float targetVol = Mathf.Clamp01(noiseMaxVolume);
        noiseFadeRoutine = StartCoroutine(CoFadeNoise(noiseLoopSource.volume, targetVol, Mathf.Max(0f, noiseFadeInSeconds), false));
    }

    private void FadeOutAndStopNoise() {
        if (noiseLoopSource == null)
            return;

        if (!noiseLoopSource.isPlaying) {
            return;
        }

        if (noiseFadeRoutine != null)
            StopCoroutine(noiseFadeRoutine);

        noiseFadeRoutine = StartCoroutine(CoFadeNoise(noiseLoopSource.volume, 0f, Mathf.Max(0f, noiseFadeOutSeconds), true));
    }

    private IEnumerator CoFadeNoise(float from, float to, float seconds, bool stopAfter) {
        if (noiseLoopSource == null)
            yield break;

        if (seconds <= 0f) {
            noiseLoopSource.volume = to;
            if (stopAfter)
                StopNoiseImmediate();
            noiseFadeRoutine = null;
            yield break;
        }

        float t = 0f;

        while (t < seconds) {
            if (noiseLoopSource == null)
                yield break;

            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / seconds);
            noiseLoopSource.volume = Mathf.Lerp(from, to, a);
            yield return null;
        }

        noiseLoopSource.volume = to;

        if (stopAfter)
            StopNoiseImmediate();

        noiseFadeRoutine = null;
    }

    private void StopNoiseImmediate() {
        if (noiseLoopSource == null)
            return;

        noiseLoopSource.volume = 0f;
        if (noiseLoopSource.isPlaying)
            noiseLoopSource.Stop();
    }

    private void PlayDialTick() {
        if (dialSfxSource == null || dialTickClip == null)
            return;

        dialSfxSource.PlayOneShot(dialTickClip);
    }

    private void PlayButtonPress() {
        if (buttonSfxSource == null || buttonPressClip == null)
            return;

        buttonSfxSource.PlayOneShot(buttonPressClip);
    }

    private void PlayResult(bool success) {
        if (resultSfxSource == null)
            return;

        AudioClip clip = success ? confirmSuccessClip : confirmFailClip;
        if (clip == null)
            return;

        resultSfxSource.PlayOneShot(clip);
    }

    private void MarkHeaderDirty() {
        headerDirty = true;
    }

    private void TickHeader() {
        if (terminal == null)
            return;

        bool live = state == StepState.Running && (confirmPending || Time.time < lockoutUntil);

        if (!headerDirty && !live)
            return;

        float hz = Mathf.Max(1f, headerRefreshHz);
        float interval = 1f / hz;

        float now = Time.time;
        if (!headerDirty && now < nextHeaderAt)
            return;

        ApplyHeader();
        headerDirty = false;
        nextHeaderAt = now + interval;
    }

    private void ApplyHeader() {
        if (terminal == null)
            return;

        int total = Mathf.Max(1, totalCaptures);
        int shown = Mathf.Clamp(captureIndex + 1, 1, total);

        string title = $"<color={colorHeader}>STEP 5 / SAFETY INTERLOCK SIGNAL CAPTURE</color>";

        string cap = $"<color={colorDim}>CAPTURE {shown}/{total}</color>";
        string tg = $"<color={colorDim}>TUNE {tune:+0.00;-0.00;0.00} | GAIN {gain:0.00}</color>";

        string lockLine;
        if (state == StepState.Completed)
            lockLine = $"<color={colorDim}>STATE: COMPLETE</color>";
        else if (confirmPending)
            lockLine = $"<color={colorDim}>CONFIRM: PENDING</color>";
        else if (Time.time < lockoutUntil) {
            float left = Mathf.Max(0f, lockoutUntil - Time.time);
            lockLine = $"<color={colorDim}>LOCKOUT: {left:0.0}s</color>";
        } else
            lockLine = $"<color={colorDim}>CONFIRM: READY</color>";

        terminal.SetHeader(title + "\n" + cap + " | " + tg + "\n" + lockLine);
    }

    private void PrintStatusLine() {
        if (terminal == null)
            return;

        int total = Mathf.Max(1, totalCaptures);
        int shown = Mathf.Clamp(captureIndex + 1, 1, total);
        terminal.AppendLine($"> <color={colorDim}>CAPTURE {shown}/{total}</color>", true);
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