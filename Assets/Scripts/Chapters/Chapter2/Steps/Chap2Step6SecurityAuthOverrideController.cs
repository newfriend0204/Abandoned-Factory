using System.Collections;
using UnityEngine;

public class Chap2Step6SecurityAuthOverrideController : MonoBehaviour {
    private enum StepState {
        WaitingStart,
        Playback,
        Input,
        Completed
    }

    [System.Serializable]
    private class Panel {
        public Renderer renderer;

        [HideInInspector] public bool hasEmission;
        [HideInInspector] public MaterialPropertyBlock mpb;
    }

    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;
    [SerializeField] private Chap2YStepSequenceManager sequenceManager;
    [SerializeField] private TerminalCore terminal;

    [Header("Panels (1~4)")]
    [SerializeField] private Panel[] panels = new Panel[4];

    [Header("Rules")]
    [SerializeField] private int targetPatternLength = 6;

    [Header("Timings")]
    [SerializeField] private float playbackOnSeconds = 0.20f;
    [SerializeField] private float playbackGapSeconds = 0.08f;
    [SerializeField] private float inputOnSeconds = 0.18f;
    [SerializeField] private float betweenRoundsDelaySeconds = 0.35f;
    [SerializeField] private float completeAfterLogsDelaySeconds = 4.0f;

    [Header("Emission")]
    [SerializeField] private string emissionColorProperty = "_EmissionColor";
    [SerializeField] private float idleIntensity = 4.333849f;
    [SerializeField] private float boostedIntensity = 5.333849f;
    [SerializeField]
    private Color[] panelBaseColors = new Color[4] {
        Color.green,
        Color.yellow,
        Color.red,
        Color.blue
    };

    [Header("Terminal Colors")]
    [SerializeField] private string colorHeader = "#9FE7FF";
    [SerializeField] private string colorInfo = "#C9F1FF";
    [SerializeField] private string colorWarn = "#FFD37A";
    [SerializeField] private string colorError = "#FF6B6B";
    [SerializeField] private string colorSuccess = "#8CFF9A";
    [SerializeField] private string colorDim = "#9AA0A6";

    [Header("Audio")]
    [SerializeField] private AudioSource toneSource;
    [SerializeField] private AudioSource buttonSource;
    [SerializeField] private AudioSource resultSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip toneClip;
    [SerializeField] private AudioClip buttonPressClip;
    [SerializeField] private AudioClip failClip;
    [SerializeField] private AudioClip successClip;

    [Header("Panel Pitches")]
    [SerializeField] private float[] panelPitches = new float[4] { 0.90f, 1.00f, 1.12f, 1.26f };

    [Header("Header Refresh")]
    [SerializeField] private float headerRefreshHz = 8.0f;

    private AudioSource[] toneVoices;

    private bool wasSessionActive;
    private bool introPrintedOnce;

    private StepState state = StepState.WaitingStart;

    private int[] pattern;
    private int roundLength;
    private int inputIndex;

    private Coroutine roundRoutine;
    private Coroutine failRoutine;
    private Coroutine completionRoutine;

    private bool headerDirty;
    private float nextHeaderAt;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();
        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();
        if (terminal == null)
            terminal = FindFirstObjectByType<TerminalCore>();

        CachePanels();
        SetupToneVoices();
        ApplyIdleVisuals(true);
    }

    private void OnEnable() {
        CachePanels();
        SetupToneVoices();
        ResetRuntime();
        ApplyIdleVisuals(true);
    }

    private void OnDisable() {
        StopAllRoutines();
        ApplyIdleVisuals(true);

        if (toneVoices != null) {
            for (int i = 0; i < toneVoices.Length; i++) {
                if (toneVoices[i] == null)
                    continue;
                if (toneVoices[i] == toneSource)
                    continue;

                Destroy(toneVoices[i]);
            }
        }

        toneVoices = null;
        wasSessionActive = false;
        introPrintedOnce = false;
    }

    private void Update() {
        bool sessionActive = IsStep6SessionActive();

        if (sessionActive && !wasSessionActive)
            OnSessionStart();

        if (!sessionActive && wasSessionActive)
            OnSessionEnd();

        wasSessionActive = sessionActive;

        if (!sessionActive)
            return;

        TickHeader();
    }

    private bool IsStep6SessionActive() {
        if (gameManager == null || sequenceManager == null)
            return false;
        if (gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return false;
        if (sequenceManager.CurrentStep != 6)
            return false;

        return true;
    }

    private void OnSessionStart() {
        StopAllRoutines();
        ApplyIdleVisuals(true);
        ResetRuntime();

        MarkHeaderDirty();
        TickHeader(true);

        if (terminal == null)
            return;

        if (!introPrintedOnce) {
            introPrintedOnce = true;
            terminal.ClearBody(true);
            WriteInfo("STEP 6 ONLINE. OFFLINE AUTH READY.");
            WriteWarn("PRESS START TO BEGIN.");
        } else {
            WriteWarn("PRESS START TO BEGIN.");
        }
    }

    private void OnSessionEnd() {
        StopAllRoutines();
        ApplyIdleVisuals(true);
        ResetRuntime();
    }

    private void ResetRuntime() {
        state = StepState.WaitingStart;

        roundLength = 0;
        inputIndex = 0;

        pattern = null;
        headerDirty = true;
        nextHeaderAt = 0f;
    }

    private void StopAllRoutines() {
        if (roundRoutine != null) {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
        }

        if (failRoutine != null) {
            StopCoroutine(failRoutine);
            failRoutine = null;
        }

        if (completionRoutine != null) {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }
    }

    public void PressStart() {
        if (!IsStep6SessionActive())
            return;
        if (state != StepState.WaitingStart)
            return;
        if (roundRoutine != null || failRoutine != null || completionRoutine != null)
            return;

        GenerateNewPattern();
        roundLength = 1;
        inputIndex = 0;

        state = StepState.Playback;
        MarkHeaderDirty();

        if (terminal != null)
            WriteInfo("CHALLENGE STARTED.");

        roundRoutine = StartCoroutine(CoRunRound());
    }

    public void PressPanel1() { PressPanel(1); }
    public void PressPanel2() { PressPanel(2); }
    public void PressPanel3() { PressPanel(3); }
    public void PressPanel4() { PressPanel(4); }

    private void PressPanel(int panelIndex1Based) {
        if (!IsStep6SessionActive())
            return;
        if (state != StepState.Input)
            return;
        if (failRoutine != null || completionRoutine != null)
            return;

        int idx = panelIndex1Based - 1;
        if (idx < 0 || idx >= 4)
            return;
        if (pattern == null || pattern.Length == 0)
            return;
        if (inputIndex < 0 || inputIndex >= roundLength)
            return;

        PlayButtonPress();
        StartCoroutine(CoFlashSingle(idx, inputOnSeconds, true));

        int expected = pattern[inputIndex];
        if (idx != expected) {
            TriggerFail();
            return;
        }

        inputIndex++;
        MarkHeaderDirty();

        if (inputIndex < roundLength)
            return;

        if (roundLength < GetSafeTargetLength()) {
            roundLength++;
            inputIndex = 0;

            if (roundRoutine != null) {
                StopCoroutine(roundRoutine);
                roundRoutine = null;
            }

            state = StepState.Playback;
            MarkHeaderDirty();

            roundRoutine = StartCoroutine(CoRunRoundAfterDelay());
            return;
        }

        TriggerCompletion();
    }

    private int GetSafeTargetLength() {
        return Mathf.Clamp(targetPatternLength, 1, 64);
    }

    private void GenerateNewPattern() {
        int len = GetSafeTargetLength();

        if (pattern == null || pattern.Length != len)
            pattern = new int[len];

        for (int i = 0; i < len; i++)
            pattern[i] = Random.Range(0, 4);
    }

    private IEnumerator CoRunRound() {
        yield return null;

        if (!IsStep6SessionActive()) {
            roundRoutine = null;
            yield break;
        }

        if (pattern == null || pattern.Length == 0) {
            roundRoutine = null;
            yield break;
        }

        state = StepState.Playback;
        inputIndex = 0;
        MarkHeaderDirty();

        float onSec = Mathf.Max(0.01f, playbackOnSeconds);
        float gapSec = Mathf.Max(0f, playbackGapSeconds);

        int steps = Mathf.Clamp(roundLength, 1, pattern.Length);
        for (int i = 0; i < steps; i++) {
            if (!IsStep6SessionActive()) {
                roundRoutine = null;
                yield break;
            }

            int p = Mathf.Clamp(pattern[i], 0, 3);
            yield return CoFlashSingle(p, onSec, true);

            if (gapSec > 0f)
                yield return new WaitForSeconds(gapSec);
        }

        if (!IsStep6SessionActive()) {
            roundRoutine = null;
            yield break;
        }

        state = StepState.Input;
        inputIndex = 0;
        MarkHeaderDirty();

        roundRoutine = null;
    }

    private IEnumerator CoRunRoundAfterDelay() {
        float delay = Mathf.Max(0f, betweenRoundsDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        yield return CoRunRound();
    }

    private void TriggerFail() {
        if (failRoutine != null)
            return;

        if (roundRoutine != null) {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
        }

        state = StepState.WaitingStart;
        MarkHeaderDirty();

        if (terminal != null) {
            WriteError("SEQUENCE MISMATCH.");
            WriteWarn("PRESS START TO RETRY.");
        }

        failRoutine = StartCoroutine(CoFailFlash());
    }

    private IEnumerator CoFailFlash() {
        PlayFail();

        float onSec = Mathf.Max(0.04f, playbackOnSeconds * 0.65f);
        float gapSec = Mathf.Max(0.02f, playbackGapSeconds * 0.75f);

        for (int n = 0; n < 2; n++) {
            SetAllPanelsBoosted(true);
            yield return new WaitForSeconds(onSec);
            SetAllPanelsBoosted(false);
            yield return new WaitForSeconds(gapSec);
        }

        ApplyIdleVisuals(true);

        GenerateNewPattern();
        roundLength = 0;
        inputIndex = 0;

        failRoutine = null;
    }

    private void TriggerCompletion() {
        if (completionRoutine != null)
            return;

        if (roundRoutine != null) {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
        }

        state = StepState.Completed;
        MarkHeaderDirty();

        PlaySuccess();

        if (terminal != null) {
            WriteSuccess("AUTH OVERRIDDEN.");
            WriteInfo("LOCAL SECURITY CONTROLS UNLOCKED.");
        }

        completionRoutine = StartCoroutine(CoCompletion());
    }

    private IEnumerator CoCompletion() {
        yield return WaitForTerminalTyping();

        float delay = Mathf.Max(0f, completeAfterLogsDelaySeconds);
        float t = 0f;

        while (t < delay) {
            if (!IsStep6SessionActive()) {
                completionRoutine = null;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (sequenceManager != null)
            sequenceManager.CompleteStep(6);

        completionRoutine = null;
    }

    private void CachePanels() {
        if (panels == null)
            return;

        for (int i = 0; i < panels.Length; i++) {
            if (panels[i] == null)
                continue;

            if (panels[i].mpb == null)
                panels[i].mpb = new MaterialPropertyBlock();

            panels[i].hasEmission = false;

            Renderer r = panels[i].renderer;
            if (r == null)
                continue;
            if (r.sharedMaterial == null)
                continue;
            if (string.IsNullOrEmpty(emissionColorProperty))
                continue;
            if (!r.sharedMaterial.HasProperty(emissionColorProperty))
                continue;

            panels[i].hasEmission = true;
        }
    }

    private void ApplyIdleVisuals(bool force) {
        for (int i = 0; i < 4; i++)
            SetPanelIntensity(i, idleIntensity, force);
    }

    private IEnumerator CoFlashSingle(int panelIdx, float onSeconds, bool playTone) {
        SetPanelIntensity(panelIdx, boostedIntensity, false);
        if (playTone)
            PlayTone(panelIdx);

        yield return new WaitForSeconds(Mathf.Max(0.01f, onSeconds));

        SetPanelIntensity(panelIdx, idleIntensity, false);
    }

    private void SetAllPanelsBoosted(bool boosted) {
        float intensity = boosted ? boostedIntensity : idleIntensity;
        for (int i = 0; i < 4; i++)
            SetPanelIntensity(i, intensity, false);
    }

    private void SetPanelIntensity(int idx, float intensity, bool force) {
        if (panels == null || idx < 0 || idx >= panels.Length)
            return;

        Panel p = panels[idx];
        if (p == null || p.renderer == null)
            return;
        if (!p.hasEmission)
            return;

        if (p.mpb == null)
            p.mpb = new MaterialPropertyBlock();

        Color baseC = GetPanelBaseColor(idx);
        Color c = baseC * intensity;

        p.renderer.GetPropertyBlock(p.mpb);

        if (!force) {
            Color existing = p.mpb.GetVector(emissionColorProperty);
            if (ApproximatelyColor(existing, c))
                return;
        }

        p.mpb.SetColor(emissionColorProperty, c);
        p.renderer.SetPropertyBlock(p.mpb);
    }

    private bool ApproximatelyColor(Color a, Color b) {
        const float eps = 0.0001f;
        if (Mathf.Abs(a.r - b.r) > eps)
            return false;
        if (Mathf.Abs(a.g - b.g) > eps)
            return false;
        if (Mathf.Abs(a.b - b.b) > eps)
            return false;
        if (Mathf.Abs(a.a - b.a) > eps)
            return false;

        return true;
    }

    private Color GetPanelBaseColor(int idx) {
        if (panelBaseColors != null && idx >= 0 && idx < panelBaseColors.Length)
            return panelBaseColors[idx];

        if (idx == 0)
            return Color.green;
        if (idx == 1)
            return Color.yellow;
        if (idx == 2)
            return Color.red;

        return Color.blue;
    }

    private void PlayTone(int panelIdx) {
        if (toneClip == null)
            return;

        AudioSource src = GetToneVoice(panelIdx);
        if (src == null)
            return;

        src.PlayOneShot(toneClip);
    }

    private AudioSource GetToneVoice(int panelIdx) {
        if (toneVoices == null || toneVoices.Length != 4)
            return toneSource;

        int idx = Mathf.Clamp(panelIdx, 0, 3);
        return toneVoices[idx];
    }

    private void SetupToneVoices() {
        if (toneSource == null)
            return;

        if (toneVoices != null && toneVoices.Length == 4)
            return;

        toneVoices = new AudioSource[4];
        toneVoices[0] = toneSource;

        for (int i = 1; i < 4; i++)
            toneVoices[i] = CreateToneVoiceFromTemplate(toneSource);

        for (int i = 0; i < 4; i++) {
            AudioSource src = toneVoices[i];
            if (src == null)
                continue;

            float pitch = 1f;
            if (panelPitches != null && i >= 0 && i < panelPitches.Length)
                pitch = panelPitches[i];

            src.pitch = pitch;
        }
    }

    private AudioSource CreateToneVoiceFromTemplate(AudioSource template) {
        if (template == null)
            return null;

        AudioSource src = gameObject.AddComponent<AudioSource>();

        src.outputAudioMixerGroup = template.outputAudioMixerGroup;
        src.mute = template.mute;
        src.bypassEffects = template.bypassEffects;
        src.bypassListenerEffects = template.bypassListenerEffects;
        src.bypassReverbZones = template.bypassReverbZones;
        src.priority = template.priority;
        src.volume = template.volume;
        src.panStereo = template.panStereo;
        src.spatialBlend = template.spatialBlend;
        src.reverbZoneMix = template.reverbZoneMix;
        src.dopplerLevel = template.dopplerLevel;
        src.spread = template.spread;
        src.rolloffMode = template.rolloffMode;
        src.minDistance = template.minDistance;
        src.maxDistance = template.maxDistance;
        src.spatialize = template.spatialize;
        src.spatializePostEffects = template.spatializePostEffects;

        src.playOnAwake = false;
        src.loop = false;

        return src;
    }

    private void PlayButtonPress() {
        if (buttonSource == null || buttonPressClip == null)
            return;

        buttonSource.PlayOneShot(buttonPressClip);
    }

    private void PlayFail() {
        if (resultSource == null || failClip == null)
            return;

        resultSource.PlayOneShot(failClip);
    }

    private void PlaySuccess() {
        if (resultSource == null || successClip == null)
            return;

        resultSource.PlayOneShot(successClip);
    }

    private void MarkHeaderDirty() {
        headerDirty = true;
    }

    private void TickHeader() {
        TickHeader(false);
    }

    private void TickHeader(bool force) {
        if (terminal == null)
            return;

        float hz = Mathf.Max(1f, headerRefreshHz);
        float interval = 1f / hz;

        if (!force) {
            if (!headerDirty && Time.time < nextHeaderAt)
                return;
        }

        nextHeaderAt = Time.time + interval;
        headerDirty = false;

        string title = $"<color={colorHeader}>STEP 6 — SECURITY AUTH OVERRIDE</color>";

        int tgt = GetSafeTargetLength();
        int shownRound = Mathf.Clamp(roundLength, 0, tgt);

        string phase;
        if (state == StepState.WaitingStart)
            phase = $"<color={colorDim}>PHASE: WAIT_START</color>";
        else if (state == StepState.Playback)
            phase = $"<color={colorDim}>PHASE: PLAYBACK</color>";
        else if (state == StepState.Input)
            phase = $"<color={colorDim}>PHASE: INPUT</color>";
        else
            phase = $"<color={colorDim}>PHASE: COMPLETE</color>";

        string roundLine = $"<color={colorDim}>ROUND: {shownRound}/{tgt}</color>";
        string progLine;

        if (state == StepState.Input)
            progLine = $"<color={colorDim}>INPUT: {Mathf.Clamp(inputIndex, 0, shownRound)}/{shownRound}</color>";
        else
            progLine = $"<color={colorDim}>INPUT: --</color>";

        terminal.SetHeader(title + "\n" + roundLine + " | " + phase + "\n" + progLine);
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