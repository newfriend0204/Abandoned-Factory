using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TerminalCore : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private TMP_Text targetText;

    [Header("Layout")]
    [SerializeField] private int maxVisibleLines = 24;

    [Header("Typewriter")]
    [SerializeField] private float charactersPerSecond = 220f;
    [SerializeField] private int maxCharsPerFrame = 48;

    [Range(0f, 1f)]
    [SerializeField] private float hitchChance = 0.04f;
    [SerializeField] private float hitchMinSeconds = 0.02f;
    [SerializeField] private float hitchMaxSeconds = 0.08f;
    [SerializeField] private int safetyMaxCharsPerFrame = 4096;

    [Header("Cursor")]
    [SerializeField] private bool enableCursor = true;
    [SerializeField] private float cursorBlinkInterval = 0.5f;

    [Header("Header (fixed)")]
    [TextArea(1, 6)]
    [SerializeField] private string headerText;

    [Header("Header Separator")]
    [SerializeField] private string headerSeparator = "================================================================";

    private readonly List<string> bodyLines = new List<string>();

    private Coroutine typingRoutine;
    private Coroutine cursorRoutine;

    private Coroutine firstLayoutRoutine;
    private bool pendingFirstLayoutRecalc = false;

    private string committedText = "";
    private bool rawMode = false;

    private bool cursorOn = true;
    private string liveLine = "";

    private string cachedComposedBase = "";
    private string cacheBaseText = "";
    private string cacheHeaderText = "";
    private string cacheLiveLine = "";
    private string cacheHeaderSeparator = "";
    private bool cacheRawMode;
    private int cacheMaxVisibleLines;
    private bool cacheDirty = true;

    private bool cachedCursorAllowed = true;

    public bool IsTyping => typingRoutine != null;

    private int MaxLines => Mathf.Max(1, maxVisibleLines);

    private float CharDelay {
        get {
            float cps = Mathf.Max(1f, charactersPerSecond);
            return 1f / cps;
        }
    }

    private int MaxCharsPerFrame => Mathf.Clamp(maxCharsPerFrame, 1, 65536);
    private int SafetyMaxCharsPerFrame => Mathf.Clamp(safetyMaxCharsPerFrame, 64, 65536);

    private void Awake() {
        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>();

        ApplyText(committedText, true);
    }

    private void OnEnable() {
        StartCursor();

        pendingFirstLayoutRecalc = true;
        if (firstLayoutRoutine != null)
            StopCoroutine(firstLayoutRoutine);
        firstLayoutRoutine = StartCoroutine(CoFirstLayoutRecalc());
    }

    private void OnDisable() {
        pendingFirstLayoutRecalc = false;
        if (firstLayoutRoutine != null) {
            StopCoroutine(firstLayoutRoutine);
            firstLayoutRoutine = null;
        }

        StopCursor();
        StopTyping();
    }

    private IEnumerator CoFirstLayoutRecalc() {
        yield return null;

        if (!pendingFirstLayoutRecalc)
            yield break;
        if (!isActiveAndEnabled)
            yield break;

        pendingFirstLayoutRecalc = false;
        firstLayoutRoutine = null;

        MarkDirty();
        ApplyText(committedText, true);
    }

    public void SetHeader(string text) {
        headerText = text != null ? text : "";
        MarkDirty();
        ApplyText(committedText, true);
    }

    public void ClearBody(bool keepHeader) {
        bodyLines.Clear();
        liveLine = "";
        rawMode = false;

        if (!keepHeader)
            headerText = "";

        StopTyping();
        committedText = "";
        MarkDirty();
        ApplyText(committedText, true);
    }

    public void SetLiveLine(string line) {
        liveLine = line != null ? line : "";
        MarkDirty();
        ApplyText(committedText, true);
    }

    public void ClearLiveLine() {
        if (string.IsNullOrEmpty(liveLine))
            return;

        liveLine = "";
        MarkDirty();
        ApplyText(committedText, true);
    }

    public void AppendLine(string line, bool useTypewriter) {
        rawMode = false;

        if (line == null)
            line = "";

        bodyLines.Add(line);

        string targetBody = ComposeBodyText(bodyLines);

        if (!useTypewriter) {
            StopTyping();
            committedText = targetBody;
            MarkDirty();
            ApplyText(committedText, true);
            return;
        }

        StartTypingTo(targetBody, false);
    }

    public void RenderFull(string fullText, bool useTypewriter) {
        rawMode = true;

        if (fullText == null)
            fullText = "";

        StopTyping();
        committedText = "";
        liveLine = "";

        if (!useTypewriter) {
            committedText = fullText;
            MarkDirty();
            ApplyText(committedText, true);
            return;
        }

        StartTypingTo(fullText, true);
    }

    public void SetTextInstant(string fullText) {
        rawMode = true;

        if (fullText == null)
            fullText = "";

        StopTyping();
        committedText = fullText;
        liveLine = "";
        MarkDirty();
        ApplyText(committedText, true);
    }

    private void MarkDirty() {
        cacheDirty = true;
    }

    private string ComposeBodyText(List<string> lines) {
        if (lines == null || lines.Count == 0)
            return "";

        return string.Join("\n", lines);
    }

    private void StartTypingTo(string target, bool clearFirst) {
        StopTyping();
        typingRoutine = StartCoroutine(CoTypeTo(target, clearFirst));
    }

    private void StopTyping() {
        if (typingRoutine == null)
            return;

        StopCoroutine(typingRoutine);
        typingRoutine = null;
    }

    private IEnumerator CoTypeTo(string target, bool clearFirst) {
        if (clearFirst)
            committedText = "";

        MarkDirty();
        ApplyText(committedText, true);

        if (!string.IsNullOrEmpty(committedText) && target.StartsWith(committedText)) {
            string diff = target.Substring(committedText.Length);
            yield return TypeTokensStrictCpsWithHitch(diff);

            committedText = target;
            MarkDirty();
            ApplyText(committedText, true);
            typingRoutine = null;
            yield break;
        }

        committedText = "";
        MarkDirty();
        ApplyText(committedText, true);

        yield return TypeTokensStrictCpsWithHitch(target);

        committedText = target;
        MarkDirty();
        ApplyText(committedText, true);
        typingRoutine = null;
    }

    private IEnumerator TypeTokensStrictCpsWithHitch(string text) {
        List<string> tokens = TokenizeRichText(text);

        float charDelay = CharDelay;
        float hitchP = Mathf.Clamp01(hitchChance);
        float hitchMin = Mathf.Max(0f, hitchMinSeconds);
        float hitchMax = Mathf.Max(hitchMin, hitchMaxSeconds);

        int idx = 0;
        float timeAcc = 0f;

        float hitchRemaining = 0f;
        bool wasHitching = false;

        while (idx < tokens.Count) {
            float dt = Time.deltaTime;
            if (dt <= 0f) {
                yield return null;
                continue;
            }

            if (hitchRemaining > 0f) {
                hitchRemaining -= dt;
                wasHitching = true;
                yield return null;
                continue;
            }

            timeAcc += dt;

            bool hitchJustEnded = wasHitching;
            wasHitching = false;

            int budget = Mathf.FloorToInt(timeAcc / charDelay);
            if (budget <= 0) {
                yield return null;
                continue;
            }

            int cap = hitchJustEnded ? SafetyMaxCharsPerFrame : MaxCharsPerFrame;
            if (budget > cap)
                budget = cap;

            int emitted = 0;
            bool changed2 = false;

            while (idx < tokens.Count && IsRichTextTag(tokens[idx])) {
                committedText += tokens[idx];
                idx++;
                changed2 = true;
            }

            while (idx < tokens.Count) {
                if (IsRichTextTag(tokens[idx]))
                    break;

                if (emitted >= budget)
                    break;

                committedText += tokens[idx];
                idx++;
                emitted++;
                changed2 = true;

                if (hitchP > 0f && Random.value < hitchP) {
                    hitchRemaining = Random.Range(hitchMin, hitchMax);
                    break;
                }
            }

            timeAcc -= emitted * charDelay;

            if (changed2) {
                MarkDirty();
                ApplyText(committedText, true);
            }

            yield return null;
        }
    }

    private List<string> TokenizeRichText(string s) {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(s))
            return tokens;

        int i = 0;
        while (i < s.Length) {
            char c = s[i];
            if (c == '<') {
                int end = s.IndexOf('>', i);
                if (end >= 0) {
                    tokens.Add(s.Substring(i, end - i + 1));
                    i = end + 1;
                    continue;
                }
            }

            tokens.Add(s[i].ToString());
            i++;
        }

        return tokens;
    }

    private bool IsRichTextTag(string token) {
        if (string.IsNullOrEmpty(token))
            return false;

        return token.Length >= 2 && token[0] == '<' && token[token.Length - 1] == '>';
    }

    private void StartCursor() {
        StopCursor();

        if (!enableCursor) {
            cursorOn = false;
            ApplyText(committedText, true);
            return;
        }

        cursorRoutine = StartCoroutine(CoCursorBlink());
    }

    private void StopCursor() {
        if (cursorRoutine == null)
            return;

        StopCoroutine(cursorRoutine);
        cursorRoutine = null;
    }

    private IEnumerator CoCursorBlink() {
        float interval = Mathf.Max(0.05f, cursorBlinkInterval);
        float t = 0f;

        while (true) {
            float dt = Time.deltaTime;
            if (dt <= 0f) {
                yield return null;
                continue;
            }

            t += dt;
            if (t >= interval) {
                t = 0f;
                cursorOn = !cursorOn;
                ApplyText(committedText, true);
            }

            yield return null;
        }
    }

    private int GetRenderedLineCount(string text) {
        if (targetText == null)
            return 0;

        TMP_TextInfo info = targetText.GetTextInfo(text != null ? text : "");
        if (info == null)
            return 0;

        return info.lineCount;
    }

    private bool IsCursorSafeForCurrentText(string baseText) {
        if (!enableCursor)
            return false;
        if (targetText == null)
            return false;

        int baseLines = GetRenderedLineCount(baseText);
        int cursorLines = GetRenderedLineCount(baseText + "_");
        return cursorLines == baseLines;
    }

    private int AdjustTrimStartForRichText(string s, int idx) {
        if (string.IsNullOrEmpty(s))
            return 0;

        int len = s.Length;
        if (idx < 0)
            idx = 0;
        if (idx > len)
            idx = len;

        while (idx < len && (s[idx] == '\n' || s[idx] == '\r'))
            idx++;

        if (idx >= len)
            return len;

        int lastLt = s.LastIndexOf('<', idx);
        int lastGt = s.LastIndexOf('>', idx);

        if (lastLt > lastGt) {
            int end = s.IndexOf('>', idx);
            if (end >= 0)
                idx = end + 1;
            else
                idx = len;

            while (idx < len && (s[idx] == '\n' || s[idx] == '\r'))
                idx++;
        }

        return idx;
    }

    private string ClampTextToRenderedLines(string text, int maxLines) {
        if (string.IsNullOrEmpty(text))
            return "";

        int lines = GetRenderedLineCount(text);
        if (lines <= maxLines)
            return text;

        int lo = 0;
        int hi = text.Length;

        while (lo < hi) {
            int mid = (lo + hi) / 2;
            int start = AdjustTrimStartForRichText(text, mid);

            string candidate = start > 0 ? text.Substring(start) : text;

            if (GetRenderedLineCount(candidate) <= maxLines)
                hi = mid;
            else
                lo = mid + 1;
        }

        int finalStart = AdjustTrimStartForRichText(text, lo);
        if (finalStart <= 0)
            return text;
        if (finalStart >= text.Length)
            return "";

        return text.Substring(finalStart);
    }

    private string ClampBodyToRenderedLines(string headerBlock, string body, string liveBlock, int maxLines) {
        if (string.IsNullOrEmpty(body))
            return "";

        string baseNoBody = ComposeWithBlocks(headerBlock, "", liveBlock);
        if (GetRenderedLineCount(baseNoBody) >= maxLines)
            return "";

        string test = ComposeWithBlocks(headerBlock, body, liveBlock);
        if (GetRenderedLineCount(test) <= maxLines)
            return body;

        int lo = 0;
        int hi = body.Length;

        while (lo < hi) {
            int mid = (lo + hi) / 2;
            int start = AdjustTrimStartForRichText(body, mid);

            string candidateBody = start > 0 ? body.Substring(start) : body;
            string composed = ComposeWithBlocks(headerBlock, candidateBody, liveBlock);

            if (GetRenderedLineCount(composed) <= maxLines)
                hi = mid;
            else
                lo = mid + 1;
        }

        int finalStart = AdjustTrimStartForRichText(body, lo);
        if (finalStart <= 0)
            return body;
        if (finalStart >= body.Length)
            return "";

        return body.Substring(finalStart);
    }

    private string BuildHeaderBlock() {
        if (string.IsNullOrEmpty(headerText))
            return "";

        string sep = string.IsNullOrEmpty(headerSeparator) ? "================================================================" : headerSeparator;
        return headerText + "\n" + sep;
    }

    private string TrimLiveLineToFitHeaderOnly(string headerBlock, string live, int maxLines) {
        if (string.IsNullOrEmpty(live))
            return "";

        string test = headerBlock;
        if (!string.IsNullOrEmpty(live)) {
            if (!string.IsNullOrEmpty(test))
                test += "\n";
            test += live;
        }

        if (GetRenderedLineCount(test) <= maxLines)
            return live;

        int lo = 0;
        int hi = live.Length;

        while (lo < hi) {
            int mid = (lo + hi) / 2;
            string tail = live.Substring(mid);
            string shown = tail;

            string test2 = headerBlock;
            if (!string.IsNullOrEmpty(test2))
                test2 += "\n";
            test2 += shown;

            if (GetRenderedLineCount(test2) <= maxLines)
                hi = mid;
            else
                lo = mid + 1;
        }

        if (lo <= 0)
            return live;

        return live.Substring(lo);
    }

    private string ComposeLineModeVisible(string bodyText) {
        string headerBlock = BuildHeaderBlock();
        string body = bodyText != null ? bodyText : "";

        int max = MaxLines;

        string liveForRender = liveLine;
        if (!string.IsNullOrEmpty(liveForRender)) {
            string test = ComposeWithBlocks(headerBlock, "", liveForRender);
            if (GetRenderedLineCount(test) > max)
                liveForRender = TrimLiveLineToFitHeaderOnly(headerBlock, liveForRender, max);
        }

        string clampedBody = ClampBodyToRenderedLines(headerBlock, body, liveForRender, max);
        return ComposeWithBlocks(headerBlock, clampedBody, liveForRender);
    }

    private string ComposeWithBlocks(string headerBlock, string bodyBlock, string liveBlock) {
        string composed = "";

        if (!string.IsNullOrEmpty(headerBlock))
            composed = headerBlock;

        if (!string.IsNullOrEmpty(bodyBlock)) {
            if (!string.IsNullOrEmpty(composed))
                composed += "\n";

            composed += bodyBlock;
        }

        if (!string.IsNullOrEmpty(liveBlock)) {
            if (!string.IsNullOrEmpty(composed))
                composed += "\n";

            composed += liveBlock;
        }

        return composed;
    }

    private void ApplyText(string baseText, bool applyCursor) {
        if (targetText == null)
            return;

        bool inputsChanged =
            cacheDirty ||
            cacheBaseText != (baseText ?? "") ||
            cacheHeaderText != (headerText ?? "") ||
            cacheLiveLine != (liveLine ?? "") ||
            cacheHeaderSeparator != (headerSeparator ?? "") ||
            cacheRawMode != rawMode ||
            cacheMaxVisibleLines != maxVisibleLines;

        if (inputsChanged) {
            string composed;

            if (rawMode) {
                composed = baseText != null ? baseText : "";
                if (!string.IsNullOrEmpty(liveLine)) {
                    if (!string.IsNullOrEmpty(composed))
                        composed += "\n";

                    composed += liveLine;
                }

                composed = ClampTextToRenderedLines(composed, MaxLines);
            } else {
                composed = ComposeLineModeVisible(baseText);
            }

            cachedComposedBase = composed;
            cachedCursorAllowed = IsCursorSafeForCurrentText(cachedComposedBase);

            cacheBaseText = baseText ?? "";
            cacheHeaderText = headerText ?? "";
            cacheLiveLine = liveLine ?? "";
            cacheHeaderSeparator = headerSeparator ?? "";
            cacheRawMode = rawMode;
            cacheMaxVisibleLines = maxVisibleLines;
            cacheDirty = false;
        }

        if (!enableCursor || !applyCursor || !cachedCursorAllowed) {
            targetText.text = cachedComposedBase;
            return;
        }

        targetText.text = cachedComposedBase + (cursorOn ? "_" : " ");
    }
}