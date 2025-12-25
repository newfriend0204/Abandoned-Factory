using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProtocolSequenceMonitorPresenter : MonoBehaviour {
    [System.Serializable]
    public class StepEntry {
        public string title;
        public string hint;
    }

    [Header("Refs")]
    [SerializeField] private TerminalCore terminal;
    [SerializeField] private Chap2YStepSequenceManager sequenceManager;

    [Header("Text")]
    [SerializeField] private int lineWidth = 64;
    [SerializeField] private string headerTitle = "P.R.O.T.O.C.O.L   S.E.Q.U.E.N.C.E   M.O.N.I.T.O.R";
    [SerializeField] private string redHex = "#FF3333";
    [SerializeField] private string greenHex = "#33FF66";

    [Header("Blink")]
    [SerializeField] private float waitingBlinkInterval = 0.55f;

    [Header("Steps (1~7)")]
    [SerializeField]
    private List<StepEntry> steps = new List<StepEntry>() {
        new StepEntry { title = "MANUAL DIAGNOSTICS LOG",           hint = "LIABILITY WAIVER SIGNED" },
        new StepEntry { title = "3-PHASE POWER SYNC",               hint = "AWAITING GENERATOR INPUT" },
        new StepEntry { title = "HYDRAULIC PRESSURE STABILIZATION", hint = "VALVE CONTROLS LOCKED" },
        new StepEntry { title = "TOXIC HAZARD SENSOR BYPASS",       hint = "SAFETY PROTOCOL ACTIVE" },
        new StepEntry { title = "INTERLOCK SIGNAL REGISTRATION",    hint = "NO SIGNAL DETECTED" },
        new StepEntry { title = "OFFLINE PATTERN AUTHENTICATION",   hint = "ENCRYPTION KEY MISSING" },
        new StepEntry { title = "MANUAL SHUTTER RELEASE",           hint = "SEQUENCE INCOMPLETE" },
    };

    [SerializeField] private GameManagerChap2 gameManager;
    private bool forcedDoneByPostState = false;

    private int lastStep = -1;
    private int lastRawStep = int.MinValue;
    private bool allDone = false;

    private bool blinkOn = true;
    private Coroutine blinkRoutine;

    private void Awake() {
        if (terminal == null)
            terminal = GetComponentInChildren<TerminalCore>();

        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();
    }

    private void OnEnable() {
        StartBlink();
        ForceRefresh(true);
    }

    private void OnDisable() {
        StopBlink();
    }

    private void Update() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        bool postDone = gameManager != null && gameManager.State == GameManagerChap2.Chap2State.PostYChase;

        if (postDone) {
            if (!forcedDoneByPostState) {
                forcedDoneByPostState = true;

                allDone = true;
                lastStep = 7;
                lastRawStep = int.MaxValue;

                StopBlink();
                ForceRefresh(true);
            }
            return;
        }

        forcedDoneByPostState = false;

        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (sequenceManager == null)
            return;

        int raw = sequenceManager.CurrentStep;
        if (raw == lastRawStep)
            return;

        lastRawStep = raw;
        allDone = raw > 7;

        int step = Mathf.Clamp(raw, 1, 7);
        if (step != lastStep)
            lastStep = step;

        if (allDone)
            StopBlink();
        else
            StartBlink();

        ForceRefresh(true);
    }

    public void ForceRefresh(bool useTypewriter) {
        if (terminal == null)
            return;

        int step = lastStep;
        if (step < 1)
            step = 1;

        string text = BuildBoardText(step, blinkOn, allDone);

        if (useTypewriter)
            terminal.RenderFull(text, true);
        else
            terminal.SetTextInstant(text);
    }

    private void StartBlink() {
        if (blinkRoutine != null)
            return;

        blinkRoutine = StartCoroutine(CoBlink());
    }

    private void StopBlink() {
        if (blinkRoutine == null)
            return;

        StopCoroutine(blinkRoutine);
        blinkRoutine = null;
    }

    private IEnumerator CoBlink() {
        float interval = Mathf.Max(0.05f, waitingBlinkInterval);
        float t = 0f;

        while (true) {
            if (allDone) {
                yield return null;
                continue;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) {
                yield return null;
                continue;
            }

            t += dt;
            if (t < interval) {
                yield return null;
                continue;
            }

            t = 0f;
            blinkOn = !blinkOn;

            if (terminal != null && lastStep >= 1 && !terminal.IsTyping)
                terminal.SetTextInstant(BuildBoardText(lastStep, blinkOn, allDone));

            yield return null;
        }
    }

    private string BuildBoardText(int currentStep, bool waitingVisible, bool done) {
        currentStep = Mathf.Clamp(currentStep, 1, 7);

        string line = new string('=', Mathf.Max(8, lineWidth));
        var sb = new System.Text.StringBuilder(512);

        sb.AppendLine(line);
        sb.AppendLine(Center(headerTitle, lineWidth));
        sb.AppendLine(line);
        sb.AppendLine("");

        int completed = done ? 7 : Mathf.Clamp(currentStep - 1, 0, 7);

        for (int i = 1; i <= 7; i++) {
            StepEntry entry = GetEntry(i);

            string status = GetStatusText(i, currentStep, done);
            bool blinkThis = (i == currentStep && status == "WAITING");

            string statusField = FormatStatusBracket(status, blinkThis && !waitingVisible);
            string numberField = ColorRed($"[{i:00}]");

            string title = entry.title != null ? entry.title.ToUpperInvariant() : $"STEP {i:00}";
            string hint = entry.hint != null ? entry.hint : "";

            string left = " " + numberField + " " + title + " ";
            int targetColumnsBeforeStatus = Mathf.Max(40, lineWidth - 16);
            string dotted = BuildDottedLeader(left, targetColumnsBeforeStatus);

            sb.AppendLine(left + dotted + " " + statusField);
            sb.AppendLine("      > " + hint);
            sb.AppendLine("");
        }

        sb.AppendLine(line);

        int progress = Mathf.FloorToInt((completed / 7f) * 100f);
        string progressLine = $"TOTAL PROGRESS: {progress}% COMPLETE";
        sb.AppendLine(ColorGreen(Center(progressLine, lineWidth)));

        sb.AppendLine(line);

        return sb.ToString().TrimEnd('\n', '\r');
    }

    private StepEntry GetEntry(int index) {
        int idx = index - 1;
        if (idx < 0 || idx >= steps.Count)
            return new StepEntry { title = $"STEP {index:00}", hint = "" };

        return steps[idx];
    }

    private string GetStatusText(int index, int currentStep, bool done) {
        if (done) {
            if (index < 7)
                return "VERIFIED";
            return "OPEN";
        }

        if (index < currentStep)
            return "VERIFIED";

        if (index == currentStep)
            return "WAITING";

        if (index == 7)
            return "LOCKED";

        return "PENDING";
    }

    private string FormatStatusBracket(string status, bool hide) {
        string inner = PadTo(status, 8);

        if (hide)
            return new string(' ', 12);

        string content = "[ " + inner + " ]";

        if (status == "VERIFIED" || status == "OPEN")
            return ColorGreen(content);

        return ColorRed(content);
    }

    private string PadTo(string s, int width) {
        if (s == null)
            s = "";

        if (s.Length > width)
            return s.Substring(0, width);

        return s.PadRight(width, ' ');
    }

    private string BuildDottedLeader(string left, int targetColumnsBeforeStatus) {
        int visibleLen = MeasureVisibleLength(left);
        int dots = Mathf.Clamp(targetColumnsBeforeStatus - visibleLen, 3, 80);
        return new string('.', dots);
    }

    private int MeasureVisibleLength(string s) {
        if (string.IsNullOrEmpty(s))
            return 0;

        int len = 0;
        bool inTag = false;

        for (int i = 0; i < s.Length; i++) {
            char c = s[i];

            if (c == '<') {
                inTag = true;
                continue;
            }

            if (c == '>') {
                inTag = false;
                continue;
            }

            if (!inTag)
                len++;
        }

        return len;
    }

    private string Center(string text, int width) {
        if (text == null)
            text = "";

        if (text.Length >= width)
            return text;

        int pad = (width - text.Length) / 2;
        return new string(' ', Mathf.Max(0, pad)) + text;
    }

    private string ColorRed(string s) {
        return $"<color={redHex}>{s}</color>";
    }

    private string ColorGreen(string s) {
        return $"<color={greenHex}>{s}</color>";
    }
}