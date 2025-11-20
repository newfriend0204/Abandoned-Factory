using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class BroadcastAnnouncerUI : MonoBehaviour {
    [Header("Required")]
    [Tooltip("TextMeshPro 폰트 에셋(.asset). 반드시 넣어줘야 함.")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Tooltip("이 UI가 올라갈 캔버스. 비워두면 부모에서 자동으로 찾음.")]
    [SerializeField] private Canvas parentCanvas;

    [Header("Global UI Scale")]
    [Tooltip("전체 UI 크기 배율. 1.5면 모든 치수 1.5배 커짐.")]
    [SerializeField] private float uiScale = 1.5f;

    [Header("Layout")]
    [SerializeField] private float topOffset = 40f;
    [SerializeField] private float panelHeight = 64f;
    [SerializeField] private float minPanelWidth = 200f;
    [SerializeField] private float horizontalPadding = 40f;

    [Header("Line Thickness")]
    [SerializeField] private float frameThickness = 2f;
    [SerializeField] private float verticalBarThickness = 4f;
    [SerializeField] private float verticalBarExtraHeight = 12f;
    [SerializeField] private float scanlineHeight = 4f;

    [Header("Timing")]
    [SerializeField] private float dropDuration = 0.18f;
    [SerializeField] private float frameExpandDuration = 0.22f;
    [SerializeField] private float boxFadeDuration = 0.14f;

    [Tooltip("한 글자마다 타이핑 간격 (초) – 0.1초에 한 글자")]
    [SerializeField] private float typeInterval = 0.1f;

    [SerializeField] private int glitchTailLength = 4;
    [SerializeField] private float extraHoldDuration = 0.7f;
    [SerializeField] private float scanlineDelay = 0.1f;
    [SerializeField] private float scanlineDuration = 0.8f;
    [SerializeField] private float closeDuration = 0.30f;

    [Header("Colors / Alpha")]
    [SerializeField] private Color laserColor = new Color(0.15f, 0.55f, 1.0f, 0.9f);
    [SerializeField] private Color boxColor = new Color(0.60f, 0.90f, 1.0f, 0.35f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.98f, 1.0f, 0.95f);
    [SerializeField] private Color scanlineColor = new Color(0.90f, 1.00f, 1.0f, 0.35f);

    // Core UI
    private RectTransform _rootRect;

    private Image _verticalBar;
    private RectTransform _verticalBarRect;

    private Image _frameTop;
    private Image _frameBottom;
    private Image _frameLeft;
    private Image _frameRight;

    private Image _backgroundBox;
    private TMP_Text _textLabel;

    private Image _scanline;
    private RectTransform _scanlineRect;

    // 애니메이션 상태
    private enum State {
        Idle,
        Opening,
        Typing,
        Hold,
        Closing
    }

    private State _state = State.Idle;
    private bool _built = false;
    private bool _glitchActive = false;
    private bool _scaleApplied = false;

    private string _currentMessage = "";
    private int _confirmedCharCount = 0;

    private Coroutine _sequenceCoroutine;

    private const string GLITCH_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789<>/\\+*#=@[]{}";

    // 최종 위치(화면 안) / 오프스크린 위치(위로 숨긴 상태)
    private Vector2 _finalAnchoredPos;
    private Vector2 _offscreenAnchoredPos;

    // ----------------------------------------------------------------------
    // Unity
    // ----------------------------------------------------------------------

    private void Awake() {
        ApplyScaleOnce();
        EnsureBuilt();
        ResetVisualImmediate();   // 세로바 포함해서 전부 안 보이게 + 화면 밖으로
    }

    private void Update() {
        if (_glitchActive && _state == State.Typing) {
            UpdateGlitchText();
        }
    }

    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------

    public void ShowBroadcast(string message) {
        if (string.IsNullOrEmpty(message)) return;
        if (fontAsset == null) {
            Debug.LogWarning("[BroadcastAnnouncerUI] Font Asset 이 비어있습니다.");
        }

        EnsureBuilt();

        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        _sequenceCoroutine = StartCoroutine(SequenceRoutine(message));
    }

    // ----------------------------------------------------------------------
    // Sequence
    // ----------------------------------------------------------------------

    private IEnumerator SequenceRoutine(string message) {
        _currentMessage = message;
        _state = State.Opening;
        ResetVisualImmediate();   // 항상 같은 초기 상태

        yield return DropPhase();
        yield return FrameExpandPhase();
        yield return BoxAppearPhase();
        yield return TypewriterPhase();
        yield return HoldAndScanPhase();

        _state = State.Closing;
        yield return ClosePhase();

        ResetVisualImmediate();
        _state = State.Idle;
        _sequenceCoroutine = null;
    }

    // ----------------------------------------------------------------------
    // Phase 1 – 세로 레이저 드롭 (화면 밖 → 안으로 내려오기 + 길이 증가)
    // ----------------------------------------------------------------------

    private IEnumerator DropPhase() {
        float t = 0f;

        // 시작: 오프스크린 위치, 세로바 길이 0
        _rootRect.anchoredPosition = _offscreenAnchoredPos;
        SetGraphicAlphaDeep(_verticalBar, laserColor.a);
        _verticalBarRect.sizeDelta = new Vector2(verticalBarThickness, 0f);

        while (t < 1f) {
            t += Time.deltaTime / dropDuration;
            float eased = EaseOutQuad(t);

            // 위치: 화면 위 → 최종 위치로 떨어지기
            _rootRect.anchoredPosition = Vector2.Lerp(_offscreenAnchoredPos, _finalAnchoredPos, eased);

            // 길이: 0 → 전체 높이까지 자라기
            float h = Mathf.Lerp(0f, panelHeight + verticalBarExtraHeight, eased);
            _verticalBarRect.sizeDelta = new Vector2(verticalBarThickness, h);

            yield return null;
        }

        _rootRect.anchoredPosition = _finalAnchoredPos;
        _verticalBarRect.sizeDelta = new Vector2(verticalBarThickness, panelHeight + verticalBarExtraHeight);
    }

    // ----------------------------------------------------------------------
    // Phase 2 – 세로줄이 프레임으로 펼쳐짐
    // ----------------------------------------------------------------------

    private IEnumerator FrameExpandPhase() {
        float t = 0f;

        float startWidth = 0f;
        float targetWidth = Mathf.Max(minPanelWidth, 10f);

        SetFrameAlpha(0f);

        while (t < 1f) {
            t += Time.deltaTime / frameExpandDuration;
            float eased = EaseOutQuad(t);

            float width = Mathf.Lerp(startWidth, targetWidth, eased);
            _rootRect.sizeDelta = new Vector2(width, panelHeight);

            float barAlpha = Mathf.Lerp(laserColor.a, 0f, eased);
            float frameAlpha = Mathf.Lerp(0f, laserColor.a, eased);

            SetGraphicAlphaDeep(_verticalBar, barAlpha);
            SetFrameAlpha(frameAlpha);

            yield return null;
        }

        _rootRect.sizeDelta = new Vector2(targetWidth, panelHeight);
        SetGraphicAlphaDeep(_verticalBar, 0f);
        SetFrameAlpha(laserColor.a);
    }

    // ----------------------------------------------------------------------
    // Phase 3 – 하늘색 박스 + 텍스트 페이드 인
    // ----------------------------------------------------------------------

    private IEnumerator BoxAppearPhase() {
        _textLabel.text = "";
        SetGraphicAlphaDeep(_textLabel, 0f);

        float t = 0f;
        float targetBoxAlpha = boxColor.a;
        float targetTextAlpha = textColor.a;

        while (t < 1f) {
            t += Time.deltaTime / boxFadeDuration;
            float eased = EaseOutQuad(t);

            float ba = Mathf.Lerp(0f, targetBoxAlpha, eased);
            float ta = Mathf.Lerp(0f, targetTextAlpha, eased);

            SetGraphicAlphaDeep(_backgroundBox, ba);
            SetGraphicAlphaDeep(_textLabel, ta);

            yield return null;
        }
    }

    // ----------------------------------------------------------------------
    // Phase 3 – 타자기 + 글리치
    // ----------------------------------------------------------------------

    private IEnumerator TypewriterPhase() {
        _state = State.Typing;
        _confirmedCharCount = 0;
        _glitchActive = true;

        while (_confirmedCharCount < _currentMessage.Length) {
            _confirmedCharCount++;
            yield return new WaitForSeconds(typeInterval); // 0.1초/글자
        }

        _glitchActive = false;
        _textLabel.text = _currentMessage;
        LayoutToCurrentText();
        SetGraphicAlphaDeep(_textLabel, textColor.a);
        _state = State.Hold;
    }

    // ----------------------------------------------------------------------
    // Phase 4 – 홀드 + 스캔라인
    // ----------------------------------------------------------------------

    private IEnumerator HoldAndScanPhase() {
        yield return ScanlinePhase();

        if (extraHoldDuration > 0f)
            yield return new WaitForSeconds(extraHoldDuration);
    }

    private IEnumerator ScanlinePhase() {
        if (scanlineDuration <= 0.01f) yield break;

        SetGraphicAlphaDeep(_scanline, 0f);

        if (scanlineDelay > 0f)
            yield return new WaitForSeconds(scanlineDelay);

        float t = 0f;
        float topY = 0f;
        float bottomY = -panelHeight;

        while (t < 1f) {
            t += Time.deltaTime / scanlineDuration;
            float eased = t;

            float y = Mathf.Lerp(topY, bottomY, eased);
            _scanlineRect.anchoredPosition = new Vector2(0f, y);

            float alphaFactor = Mathf.Sin(Mathf.Clamp01(eased) * Mathf.PI);
            float a = scanlineColor.a * alphaFactor;
            SetGraphicAlphaDeep(_scanline, a);

            yield return null;
        }

        SetGraphicAlphaDeep(_scanline, 0f);
    }

    // ----------------------------------------------------------------------
    // Phase 5 – 역상 클로징
    //   1) 네모 → 중앙으로 접히면서 세로줄로
    //   2) 세로줄이 위로 올라가며 사라지고, 완전히 화면 밖으로
    // ----------------------------------------------------------------------

    private IEnumerator ClosePhase() {
        float t = 0f;
        float startWidth = _rootRect.sizeDelta.x;

        float startBoxAlpha = _backgroundBox.color.a;
        float startTextAlpha = _textLabel.color.a;

        // 1단계: 프레임/박스/텍스트 접히면서 중앙으로 모이기
        while (t < 1f) {
            t += Time.deltaTime / closeDuration;
            float eased = EaseInQuad(t);

            float width = Mathf.Lerp(startWidth, 0f, eased);
            _rootRect.sizeDelta = new Vector2(width, panelHeight);

            float fadeFactor = 1f - eased;

            SetGraphicAlphaDeep(_backgroundBox, startBoxAlpha * fadeFactor);
            SetGraphicAlphaDeep(_textLabel, startTextAlpha * fadeFactor);
            SetFrameAlpha(laserColor.a * fadeFactor);

            float barAlpha = Mathf.Lerp(0f, laserColor.a, eased);
            SetGraphicAlphaDeep(_verticalBar, barAlpha);

            yield return null;
        }

        _rootRect.sizeDelta = new Vector2(0f, panelHeight);
        SetGraphicAlphaDeep(_backgroundBox, 0f);
        SetGraphicAlphaDeep(_textLabel, 0f);
        SetFrameAlpha(0f);

        SetGraphicAlphaDeep(_scanline, 0f);

        // 2단계: 세로줄이 위로 올라가며 사라지고, 오프스크린으로 이동
        float t2 = 0f;
        float startHeight = _verticalBarRect.sizeDelta.y;
        Vector2 startPos = _finalAnchoredPos;
        Vector2 endPos = _offscreenAnchoredPos;

        while (t2 < 1f) {
            t2 += Time.deltaTime / dropDuration;
            float eased = EaseInQuad(t2);

            float h = Mathf.Lerp(startHeight, 0f, eased);
            _verticalBarRect.sizeDelta = new Vector2(verticalBarThickness, h);

            float a = Mathf.Lerp(laserColor.a, 0f, eased);
            SetGraphicAlphaDeep(_verticalBar, a);

            _rootRect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);

            yield return null;
        }

        _verticalBarRect.sizeDelta = new Vector2(verticalBarThickness, 0f);
        SetGraphicAlphaDeep(_verticalBar, 0f);
        _rootRect.anchoredPosition = _offscreenAnchoredPos;
    }

    // ----------------------------------------------------------------------
    // Glitch text & layout
    // ----------------------------------------------------------------------

    private void UpdateGlitchText() {
        if (string.IsNullOrEmpty(_currentMessage)) return;

        int len = _currentMessage.Length;
        int confirmed = Mathf.Clamp(_confirmedCharCount, 0, len);

        string confirmedStr = _currentMessage.Substring(0, confirmed);

        int remainingForGlitch = Mathf.Clamp(len - confirmed, 0, glitchTailLength);

        if (remainingForGlitch <= 0) {
            _textLabel.text = confirmedStr;
            LayoutToCurrentText();
            return;
        }

        var sb = new StringBuilder();
        sb.Append(confirmedStr);

        for (int i = 0; i < remainingForGlitch; i++)
            sb.Append(RandomGlitchChar());

        string display = sb.ToString();

        _textLabel.text = display;
        LayoutToCurrentText();
    }

    private char RandomGlitchChar() {
        int idx = Random.Range(0, GLITCH_CHARS.Length);
        return GLITCH_CHARS[idx];
    }

    private void LayoutToCurrentText() {
        if (_textLabel == null) return;

        _textLabel.ForceMeshUpdate();
        float preferredWidth = _textLabel.preferredWidth;

        float targetWidth = Mathf.Max(minPanelWidth, preferredWidth + horizontalPadding * 2f);
        Vector2 size = _rootRect.sizeDelta;
        size.x = targetWidth;
        size.y = panelHeight;
        _rootRect.sizeDelta = size;
    }

    // ----------------------------------------------------------------------
    // Build UI hierarchy
    // ----------------------------------------------------------------------

    private void EnsureBuilt() {
        if (_built) return;

        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();

        _rootRect = GetComponent<RectTransform>();
        _rootRect.anchorMin = new Vector2(0.5f, 1f);
        _rootRect.anchorMax = new Vector2(0.5f, 1f);
        _rootRect.pivot = new Vector2(0.5f, 1f);

        _finalAnchoredPos = new Vector2(0f, -topOffset);                // 화면 안에서의 위치
        _offscreenAnchoredPos = new Vector2(0f, panelHeight + topOffset); // 화면 위, 숨겨진 위치

        _rootRect.anchoredPosition = _offscreenAnchoredPos;
        _rootRect.sizeDelta = new Vector2(0f, panelHeight);

        // Vertical Bar
        {
            GameObject barGo = new GameObject("VerticalBar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(_rootRect, false);

            _verticalBarRect = barGo.GetComponent<RectTransform>();
            _verticalBarRect.anchorMin = new Vector2(0.5f, 0.5f);
            _verticalBarRect.anchorMax = new Vector2(0.5f, 0.5f);
            _verticalBarRect.pivot = new Vector2(0.5f, 0.5f);
            _verticalBarRect.anchoredPosition = Vector2.zero;
            _verticalBarRect.sizeDelta = new Vector2(verticalBarThickness, 0f);

            _verticalBar = barGo.GetComponent<Image>();
            _verticalBar.raycastTarget = false;
            _verticalBar.color = laserColor;
            AddSimpleGlow(_verticalBar);
        }

        // Frame edges
        _frameTop = CreateFrameEdge("FrameTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        _frameBottom = CreateFrameEdge("FrameBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        _frameLeft = CreateFrameEdge("FrameLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        _frameRight = CreateFrameEdge("FrameRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));

        // Background box
        {
            GameObject bgGo = new GameObject("BackgroundBox", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(_rootRect, false);

            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(1f, 1f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = Vector2.zero;

            _backgroundBox = bgGo.GetComponent<Image>();
            _backgroundBox.raycastTarget = false;
            _backgroundBox.color = boxColor;
        }

        // Text
        {
            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(_rootRect, false);

            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.5f);
            textRect.anchorMax = new Vector2(1f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(-horizontalPadding * 2f, panelHeight - 16f * uiScale * 0.5f);

            _textLabel = textGo.GetComponent<TextMeshProUGUI>();
            _textLabel.font = fontAsset;
            _textLabel.alignment = TextAlignmentOptions.Midline;
            // ⚠ enableWordWrapping은 obsolete → textWrappingMode 사용
            _textLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _textLabel.color = textColor;
            _textLabel.raycastTarget = false;
            _textLabel.text = "";
        }

        // Scanline
        {
            GameObject scanGo = new GameObject("Scanline", typeof(RectTransform), typeof(Image));
            scanGo.transform.SetParent(_rootRect, false);

            _scanlineRect = scanGo.GetComponent<RectTransform>();
            _scanlineRect.anchorMin = new Vector2(0f, 1f);
            _scanlineRect.anchorMax = new Vector2(1f, 1f);
            _scanlineRect.pivot = new Vector2(0.5f, 1f);
            _scanlineRect.anchoredPosition = new Vector2(0f, 0f);
            _scanlineRect.sizeDelta = new Vector2(0f, scanlineHeight);

            _scanline = scanGo.GetComponent<Image>();
            _scanline.raycastTarget = false;
            _scanline.color = scanlineColor;
        }

        _built = true;
    }

    private Image CreateFrameEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot) {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_rootRect, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;

        if (anchorMin.y == anchorMax.y)
            rect.sizeDelta = new Vector2(0f, frameThickness); // top/bottom
        else
            rect.sizeDelta = new Vector2(frameThickness, 0f); // left/right

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = laserColor;
        AddSimpleGlow(img);

        return img;
    }

    // ----------------------------------------------------------------------
    // Utils
    // ----------------------------------------------------------------------

    private void ResetVisualImmediate() {
        if (!_built) return;

        _rootRect.sizeDelta = new Vector2(0f, panelHeight);
        _rootRect.anchoredPosition = _offscreenAnchoredPos; // 항상 화면 밖에서 시작

        SetGraphicAlphaDeep(_verticalBar, 0f);
        _verticalBarRect.sizeDelta = new Vector2(verticalBarThickness, 0f);

        SetFrameAlpha(0f);
        SetGraphicAlphaDeep(_backgroundBox, 0f);

        if (_textLabel != null) {
            _textLabel.text = "";
            SetGraphicAlphaDeep(_textLabel, 0f);
        }

        SetGraphicAlphaDeep(_scanline, 0f);

        _glitchActive = false;
        _confirmedCharCount = 0;
    }

    private void SetFrameAlpha(float a) {
        SetGraphicAlphaDeep(_frameTop, a);
        SetGraphicAlphaDeep(_frameBottom, a);
        SetGraphicAlphaDeep(_frameLeft, a);
        SetGraphicAlphaDeep(_frameRight, a);
    }

    /// <summary>
    /// base Graphic + 모든 자식 Graphic의 알파를 동시에 변경.
    /// 글로우용 자식 이미지까지 한 번에 꺼주기 위함.
    /// </summary>
    private void SetGraphicAlphaDeep(Graphic root, float a) {
        if (root == null) return;

        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++) {
            var g = graphics[i];
            Color c = g.color;
            c.a = a;
            g.color = c;
        }
    }

    /// <summary>
    /// Outline 대신 쓰는 간단 글로우:
    /// 원본 Image 아래에 조금 더 크고 알파 낮은 자식 Image 하나 추가.
    /// </summary>
    private void AddSimpleGlow(Image baseImage) {
        if (baseImage == null) return;

        RectTransform baseRect = baseImage.rectTransform;

        GameObject glowGo = new GameObject(baseImage.gameObject.name + "_Glow",
            typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(baseRect, false);

        RectTransform glowRect = glowGo.GetComponent<RectTransform>();
        glowRect.anchorMin = baseRect.anchorMin;
        glowRect.anchorMax = baseRect.anchorMax;
        glowRect.pivot = baseRect.pivot;
        glowRect.anchoredPosition = Vector2.zero;

        Vector2 baseSize = baseRect.sizeDelta;
        float extra = 6f * uiScale;

        if (baseSize == Vector2.zero)
            glowRect.sizeDelta = new Vector2(extra, extra);
        else
            glowRect.sizeDelta = baseSize + new Vector2(extra, extra);

        Image glowImg = glowGo.GetComponent<Image>();
        glowImg.raycastTarget = false;

        Color glowColor = laserColor;
        glowColor.a *= 0.35f;
        glowImg.color = glowColor;

        glowGo.transform.SetAsFirstSibling();
    }

    private void ApplyScaleOnce() {
        if (_scaleApplied) return;
        if (Mathf.Approximately(uiScale, 1f)) { _scaleApplied = true; return; }

        topOffset *= uiScale;
        panelHeight *= uiScale;
        minPanelWidth *= uiScale;
        horizontalPadding *= uiScale;

        frameThickness *= uiScale;
        verticalBarThickness *= uiScale;
        verticalBarExtraHeight *= uiScale;
        scanlineHeight *= uiScale;

        _scaleApplied = true;
    }

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private float EaseInQuad(float t) => t * t;
}
//AI 생성