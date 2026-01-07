using System.Collections;
using UnityEngine;

public class Chap2Step1ManualDiagnosticsController : MonoBehaviour {
    private const int TotalSteps = 5;

    [Header("Refs")]
    [SerializeField] private TerminalCore terminal;

    [Header("Audio")]
    [SerializeField] private AudioSource fanSource;
    [SerializeField] private AudioClip fanLoopClip;
    [SerializeField] private AudioSource keySfxSource;
    [SerializeField] private AudioClip keyTypeClip;

    [Header("Timings")]
    [SerializeField] private float fanRampSeconds = 3f;

    [Header("Step Statements")]
    [TextArea(2, 5)]
    [SerializeField] private string step1Expected = "I Acknowledge that Automatic Safety Failsafes are currently Inoperative";
    [TextArea(2, 5)]
    [SerializeField] private string step2Expected = "Operator Accepts Full Liability for Unsupervised Manual Override";
    [TextArea(2, 5)]
    [SerializeField] private string step3Expected = "Waiving All Rights to Legal Action regarding Catastrophic Injury";
    [TextArea(2, 5)]
    [SerializeField] private string step4Expected = "Understanding that Emergency Rescue Services are Not Guaranteed";
    [TextArea(2, 5)]
    [SerializeField] private string step5Expected = "I Consent to Exposure to Hazardous Hydraulic Fluids and Radiation";

    private bool sessionStarted;
    private bool acceptingInput;
    private bool sessionLocked;

    private string inputBuffer = "";

    private int currentStepIdx = 0;
    private string[] expectedStatements;

    private const string HeaderInitial = "SYSTEM ALERT: AUTO-SHUTTER SEQUENCE FAILED.";

    private const string EqualsLine = "============================================================";

    private const string HeaderAfterStep1 = "WARNING: UNABLE TO CONTACT HEADQUARTERS";
    private const string HeaderAfterStep2 = "HARDWARE SCAN: EXTENSIVE RUST DETECTED ON CIRCUIT BOARD A";
    private const string HeaderAfterStep3 = "VENTILATION STATUS: AIR FILTERS 100% CLOGGED WITH DUST";
    private const string HeaderAfterStep4 = "MEMORY ERROR: USER DATABASE CORRUPTED OR UNREADABLE";

    private const string ColHeader = "#FFD166";
    private const string ColSystem = "#8ECAE6";
    private const string ColPrompt = "#CDB4DB";
    private const string ColDim = "#9AA0A6";
    private const string ColQuote = "#E6E6E6";
    private const string ColInputText = "#FFFFFF";
    private const string ColProc = "#4DD8FF";
    private const string ColOk = "#7CFC98";
    private const string ColWarn = "#FFB703";
    private const string ColErr = "#FF5A5A";
    private const string ColStep = "#FF9F1C";
    private const string ColAccent = "#00D4FF";

    [Header("Completion")]
    [SerializeField] private float step1ClearDelaySeconds = 3f;
    [SerializeField] private float fanFadeOutSeconds = 0.35f;

    public void StartSession() {
        if (sessionStarted)
            return;

        sessionStarted = true;
        StartCoroutine(CoSession());
    }

    private void Update() {
        if (!sessionStarted)
            return;

        if (sessionLocked)
            return;

        if (!InteractionModeService.IsInInteractionMode)
            return;

        if (!acceptingInput)
            return;

        if (terminal != null && terminal.IsTyping)
            return;

        HandleTypingInput();
    }

    private IEnumerator CoSession() {
        if (terminal == null)
            terminal = GetComponentInChildren<TerminalCore>();

        expectedStatements = new string[] {
            step1Expected,
            step2Expected,
            step3Expected,
            step4Expected,
            step5Expected
        };

        StartFanLoop();
        yield return RampFan();

        if (terminal == null)
            yield break;

        terminal.ClearBody(false);
        terminal.SetHeader(C(ColHeader, HeaderInitial));

        yield return TypeBlank();

        yield return TypeLine(Sys("> ERROR CODE: 0x000_UNKNOWN_CAUSE"));
        yield return TypeBlank();

        yield return TypeLine(Sys("> AUTOMATION SUBSYSTEM: OFFLINE"));
        yield return TypeBlank();

        yield return TypeLine(Sys("> INITIATING MANUAL CHECKLIST MODE..."));
        yield return TypeBlank();

        yield return TypeLine(Sys("> WAITING FOR OPERATOR INPUT..."));
        yield return TypeBlank();

        yield return TypeLine(Prompt("> PLEASE TYPE THE FOLLOWING STATEMENT EXACTLY:"));
        yield return TypeBlank();

        currentStepIdx = 0;
        yield return ShowStepPrompt(currentStepIdx);
        BeginInput();
    }

    private void StartFanLoop() {
        if (fanSource == null || fanLoopClip == null)
            return;

        fanSource.clip = fanLoopClip;
        fanSource.loop = true;
        fanSource.volume = 0f;
        fanSource.Play();
    }

    private IEnumerator RampFan() {
        float ramp = Mathf.Max(0.01f, fanRampSeconds);
        float t = 0f;

        while (t < ramp) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / ramp);

            if (fanSource != null)
                fanSource.volume = k;

            yield return null;
        }

        if (fanSource != null)
            fanSource.volume = 1f;
    }

    private IEnumerator FadeOutFanAndStop(float seconds) {
        if (fanSource == null)
            yield break;

        float dur = Mathf.Max(0.01f, seconds);
        float start = fanSource.volume;

        float t = 0f;
        while (t < dur) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);

            fanSource.volume = Mathf.Lerp(start, 0f, k);

            yield return null;
        }

        fanSource.volume = 0f;
        fanSource.Stop();
    }

    private IEnumerator ShowStepPrompt(int idx) {
        if (terminal == null)
            yield break;

        yield return TypeLine(Step("[STEP " + (idx + 1) + "/" + TotalSteps + "]"));
        yield return TypeLine(Quote(expectedStatements[idx]));
        yield return TypeBlank();
    }

    private void BeginInput() {
        inputBuffer = "";
        acceptingInput = true;

        if (terminal != null)
            terminal.SetLiveLine(UserInputLive(inputBuffer));
    }

    private void EndInput() {
        acceptingInput = false;

        if (terminal != null)
            terminal.ClearLiveLine();
    }

    private void HandleTypingInput() {
        string s = Input.inputString;

        if (string.IsNullOrEmpty(s)) {
            if (Input.GetKeyDown(KeyCode.Backspace))
                ApplyBackspace();

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Submit();

            return;
        }

        for (int i = 0; i < s.Length; i++) {
            char c = s[i];

            if (c == '\b') {
                ApplyBackspace();
                continue;
            }

            if (c == '\n' || c == '\r') {
                Submit();
                continue;
            }

            if (!IsAllowedChar(c))
                continue;

            inputBuffer += c;
            PlayKeySfx();
            UpdateLiveLine();
        }
    }

    private bool IsAllowedChar(char c) {
        if (c >= 'A' && c <= 'Z')
            return true;

        if (c >= 'a' && c <= 'z')
            return true;

        if (c == ' ')
            return true;

        return false;
    }

    private void ApplyBackspace() {
        if (inputBuffer.Length == 0)
            return;

        inputBuffer = inputBuffer.Substring(0, inputBuffer.Length - 1);
        PlayKeySfx();
        UpdateLiveLine();
    }

    private void UpdateLiveLine() {
        if (terminal == null)
            return;

        terminal.SetLiveLine(UserInputLive(inputBuffer));
    }

    private void Submit() {
        if (!acceptingInput)
            return;

        EndInput();
        StartCoroutine(CoSubmitAndValidate());
    }

    private IEnumerator CoSubmitAndValidate() {
        if (terminal == null)
            yield break;

        terminal.AppendLine(UserInputCommitted(inputBuffer), false);

        bool ok = inputBuffer == expectedStatements[currentStepIdx];

        if (!ok) {
            yield return TypeBlank();
            yield return TypeLine(Err("> ERROR: SYNTAX MISMATCH."));
            yield return TypeLine(Err("> LEGAL COMPLIANCE FAILED."));
            yield return TypeBlank();

            yield return TypeLine(Warn("> SYSTEM AUDIT: RETRY REQUIRED."));
            yield return TypeLine(Prompt("> PLEASE RE-TYPE THE STATEMENT:"));
            yield return TypeBlank();

            yield return TypeLine(Quote(expectedStatements[currentStepIdx]));
            yield return TypeBlank();

            BeginInput();
            yield break;
        }

        yield return TypeLine(Proc("> PROCESSING..."));
        yield return TypeLine(Ok("> INPUT RECEIVED. PARSING LEGAL SYNTAX... OK."));
        yield return TypeLine(Ok(GetStepSuccessLine(currentStepIdx)));
        yield return TypeBlank();

        yield return TypeLine(Accent("> STEP " + (currentStepIdx + 1) + "/" + TotalSteps + " COMPLETE."));
        yield return TypeBlank();

        if (currentStepIdx >= expectedStatements.Length - 1) {
            yield return TypeLine(Dim(EqualsLine));
            yield return TypeBlank();

            yield return TypeLine(Sys("> DIAGNOSTIC CHECKLIST: FINALIZED."));
            yield return TypeBlank();

            yield return TypeLine(Sys("> UPDATE: OBJECTIVE STATUS"));
            yield return TypeLine(Accent("> [STEP 1] MANUAL DIAGNOSTICS... COMPLETE."));
            yield return TypeBlank();

            yield return TypeLine(Sys("> TERMINATING SESSION."));
            yield return TypeLine(Sys("> HAVE A PRODUCTIVE DAY."));

            sessionLocked = true;

            float wait = Mathf.Max(0f, step1ClearDelaySeconds);
            if (wait > 0f)
                yield return new WaitForSeconds(wait);

            yield return FadeOutFanAndStop(fanFadeOutSeconds);

            Chap2YStepSequenceManager seq = Chap2YStepSequenceManager.Instance;
            if (seq != null)
                seq.CompleteStep(1);

            yield break;
        }

        string nextHeader = GetHeaderAfterStep(currentStepIdx);
        if (!string.IsNullOrEmpty(nextHeader)) {
            terminal.SetHeader(C(ColHeader, nextHeader));
            yield return TypeBlank();
        }

        currentStepIdx++;
        yield return ShowStepPrompt(currentStepIdx);
        BeginInput();
    }

    private string GetStepSuccessLine(int idx) {
        if (idx == 0)
            return "> BIOMETRIC SIGNATURE ARCHIVED.";
        if (idx == 1)
            return "> LIABILITY TRANSFER COMPLETE.";
        if (idx == 2)
            return "> WAIVER ACCEPTED.";
        if (idx == 3)
            return "> RISK ACKNOWLEDGMENT SAVED.";
        return "> HAZARD ACKNOWLEDGMENT RECORDED.";
    }

    private string GetHeaderAfterStep(int idx) {
        if (idx == 0)
            return HeaderAfterStep1;
        if (idx == 1)
            return HeaderAfterStep2;
        if (idx == 2)
            return HeaderAfterStep3;
        if (idx == 3)
            return HeaderAfterStep4;

        return "";
    }

    private IEnumerator TypeLine(string line) {
        if (terminal == null)
            yield break;

        terminal.AppendLine(line, true);

        while (terminal.IsTyping)
            yield return null;
    }

    private IEnumerator TypeBlank() {
        yield return TypeLine("");
    }

    private void PlayKeySfx() {
        if (keySfxSource == null || keyTypeClip == null)
            return;

        keySfxSource.PlayOneShot(keyTypeClip);
    }

    private string C(string hex, string s) {
        if (string.IsNullOrEmpty(s))
            return s;

        return "<color=" + hex + ">" + s + "</color>";
    }

    private string Dim(string s) => C(ColDim, s);
    private string Sys(string s) => C(ColSystem, s);
    private string Prompt(string s) => C(ColPrompt, s);
    private string Proc(string s) => C(ColProc, s);
    private string Ok(string s) => C(ColOk, s);
    private string Warn(string s) => C(ColWarn, s);
    private string Err(string s) => C(ColErr, s);
    private string Step(string s) => C(ColStep, s);
    private string Accent(string s) => C(ColAccent, s);

    private string Quote(string statement) {
        return C(ColQuote, "\"" + statement + "\"");
    }

    private string UserInputLive(string userText) {
        string prefix = C(ColDim, "> USER_INPUT: ");
        string body = C(ColInputText, userText);
        return prefix + body;
    }

    private string UserInputCommitted(string userText) {
        string prefix = C(ColDim, "> USER_INPUT: ");
        string body = C(ColInputText, userText);
        return prefix + body;
    }
}