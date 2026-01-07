using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BroadcastAnnouncerUI : MonoBehaviour {
    public enum QueueMode {
        Overwrite,
        Queue
    }

    private struct BroadcastRequest {
        public string message;
        public float charsPerSecond;
        public Color baseColor;
        public QueueMode mode;
        public float holdSeconds;

        public BroadcastRequest(string message, float charsPerSecond, Color baseColor, QueueMode mode, float holdSeconds) {
            this.message = message;
            this.charsPerSecond = charsPerSecond;
            this.baseColor = baseColor;
            this.mode = mode;
            this.holdSeconds = holdSeconds;
        }
    }

    [Header("Required")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Material glowMaterial;

    [Header("Behavior")]
    [SerializeField] private QueueMode defaultQueueMode = QueueMode.Overwrite;
    [SerializeField] private float defaultCharsPerSecond = 22f;
    [SerializeField] private float defaultHoldSeconds = 4f;

    [Header("Layout")]
    [SerializeField] private float topOffset = 40f;
    [SerializeField] private float panelHeight = 64f;
    [SerializeField] private float minPanelWidth = 260f;
    [SerializeField] private float horizontalPadding = 40f;
    [SerializeField] private float frameThickness = 2f;
    [SerializeField] private float uiScale = 1.3f;

    [Header("Animation")]
    [SerializeField] private float openDuration = 0.18f;
    [SerializeField] private float closeDuration = 0.22f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Glitch Tail")]
    [SerializeField] private int glitchTailLength = 10;
    [SerializeField] private float glitchRefreshInterval = 0.03f;
    [SerializeField] private string glitchCharset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#$%&@*+-=<>?";

    [Header("Scanline")]
    [SerializeField] private bool enableScanline = true;
    [SerializeField] private int maxConcurrentScanlines = 3;
    [SerializeField] private float scanlineThickness = 3f;
    [SerializeField] private float scanlineAlpha = 0.35f;
    [SerializeField] private float scanlineTravelSeconds = 0.35f;
    [SerializeField] private Vector2 scanlineIntervalRange = new Vector2(0.25f, 0.9f);

    [Range(0f, 1f)]
    [SerializeField] private float scanlineChance = 0.75f;

    [Header("Plus Pattern")]
    [SerializeField] private bool enablePlusPattern = true;
    [SerializeField] private int plusSpriteSize = 24;
    [SerializeField] private int plusStroke = 3;
    [SerializeField] private float plusSpacing = 28f;
    [SerializeField] private float plusAlpha = 0.10f;

    [Header("Color Derivation")]
    [SerializeField] private Color defaultBaseColor = new Color(0.15f, 0.55f, 1.0f, 1.0f);
    [SerializeField] private float frameAlpha = 0.95f;
    [SerializeField] private float backgroundAlpha = 0.28f;
    [SerializeField] private float textAlpha = 0.95f;

    [Range(0f, 1f)]
    [SerializeField] private float backgroundWhiten = 0.20f;

    [Range(0f, 1f)]
    [SerializeField] private float textWhiten = 0.80f;

    private RectTransform _rootRect;
    private RectTransform _maskRect;
    private RectMask2D _rectMask;
    private RectTransform _contentRect;
    private CanvasGroup _group;

    private Image _bg;
    private Image _frameTop;
    private Image _frameBottom;
    private Image _frameLeft;
    private Image _frameRight;

    private RectTransform _plusRoot;
    private readonly List<Image> _plusPool = new List<Image>();
    private Sprite _plusSprite;

    private readonly List<Image> _scanlinePool = new List<Image>();
    private readonly List<Coroutine> _scanlineRoutines = new List<Coroutine>();
    private Coroutine _scanlineSpawner;
    private int _activeScanlineCount;

    private TextMeshProUGUI _text;

    private readonly Queue<BroadcastRequest> _queue = new Queue<BroadcastRequest>();
    private Coroutine _runner;

    private bool _built;
    private float _targetWidth;

    private System.Random _rng;

    private void Awake() {
        _rng = new System.Random();
        EnsureBuilt();
        HideImmediate();
    }

    public void ShowBroadcast(string message) {
        ShowBroadcast(message, defaultCharsPerSecond, defaultBaseColor, defaultQueueMode, defaultHoldSeconds);
    }

    public void ShowBroadcast(string message, float charsPerSecond, Color baseColor) {
        ShowBroadcast(message, charsPerSecond, baseColor, defaultQueueMode, defaultHoldSeconds);
    }

    public void ShowBroadcast(string message, float charsPerSecond, Color baseColor, QueueMode mode) {
        ShowBroadcast(message, charsPerSecond, baseColor, mode, defaultHoldSeconds);
    }

    public void ShowBroadcast(string message, float charsPerSecond, Color baseColor, QueueMode mode, float holdSeconds) {
        if (string.IsNullOrEmpty(message))
            return;

        EnsureBuilt();
        ForceLayerSync();

        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        BroadcastRequest req = new BroadcastRequest(message, charsPerSecond, baseColor, mode, holdSeconds);

        if (mode == QueueMode.Overwrite) {
            _queue.Clear();

            if (_runner != null)
                StopCoroutine(_runner);

            StopScanline();

            _runner = StartCoroutine(RunSingle(req));
            return;
        }

        _queue.Enqueue(req);

        if (_runner == null)
            _runner = StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue() {
        while (_queue.Count > 0) {
            BroadcastRequest req = _queue.Dequeue();
            yield return RunSingle(req);
        }

        _runner = null;
    }

    private IEnumerator RunSingle(BroadcastRequest req) {
        ApplyLayoutAndStyle(req);
        ForceLayerSync();

        _group.alpha = 1f;

        float fromW = 0f;
        float toW = _targetWidth;

        yield return AnimateOpen(fromW, toW);
        yield return TypewriterWithGlitch(req.message, req.charsPerSecond);

        if (req.holdSeconds > 0f) {
            StartScanline(req.baseColor);
            yield return Wait(req.holdSeconds);
            StopScanline();
        }

        yield return AnimateClose(toW, 0f);

        HideImmediate();
    }

    private IEnumerator AnimateOpen(float fromW, float toW) {
        if (openDuration <= 0f) {
            SetMaskWidth(toW);
            yield break;
        }

        float t = 0f;
        while (t < 1f) {
            t += DeltaTime() / openDuration;
            float eased = openCurve != null ? openCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            float w = Mathf.Lerp(fromW, toW, eased);
            SetMaskWidth(w);

            yield return null;
        }

        SetMaskWidth(toW);
    }

    private IEnumerator AnimateClose(float fromW, float toW) {
        if (closeDuration <= 0f) {
            SetMaskWidth(toW);
            yield break;
        }

        float t = 0f;
        while (t < 1f) {
            t += DeltaTime() / closeDuration;
            float eased = closeCurve != null ? closeCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            float w = Mathf.Lerp(fromW, toW, eased);
            SetMaskWidth(w);

            yield return null;
        }

        SetMaskWidth(toW);
    }

    private void ApplyLayoutAndStyle(BroadcastRequest req) {
        gameObject.SetActive(true);

        if (targetCanvas != null && transform.parent != targetCanvas.transform)
            transform.SetParent(targetCanvas.transform, false);

        _rootRect.localScale = Vector3.one * Mathf.Max(0.01f, uiScale);

        _text.text = req.message;
        _text.ForceMeshUpdate();
        float preferred = _text.preferredWidth;

        _targetWidth = Mathf.Max(minPanelWidth, preferred + horizontalPadding * 2f);

        _rootRect.anchorMin = new Vector2(0.5f, 1f);
        _rootRect.anchorMax = new Vector2(0.5f, 1f);
        _rootRect.pivot = new Vector2(0.5f, 1f);
        _rootRect.anchoredPosition = new Vector2(0f, -topOffset);
        _rootRect.sizeDelta = new Vector2(_targetWidth, panelHeight);

        _contentRect.anchorMin = new Vector2(0.5f, 1f);
        _contentRect.anchorMax = new Vector2(0.5f, 1f);
        _contentRect.pivot = new Vector2(0.5f, 1f);
        _contentRect.anchoredPosition = Vector2.zero;
        _contentRect.sizeDelta = new Vector2(_targetWidth, panelHeight);

        _maskRect.anchorMin = new Vector2(0.5f, 1f);
        _maskRect.anchorMax = new Vector2(0.5f, 1f);
        _maskRect.pivot = new Vector2(0.5f, 1f);
        _maskRect.anchoredPosition = Vector2.zero;
        _maskRect.sizeDelta = new Vector2(0f, panelHeight);

        Color baseC = req.baseColor;
        baseC.a = 1f;

        Color frameC = baseC;
        frameC.a = frameAlpha;

        Color bgC = Color.Lerp(baseC, Color.white, backgroundWhiten);
        bgC.a = backgroundAlpha;

        Color textC = Color.Lerp(baseC, Color.white, textWhiten);
        textC.a = textAlpha;

        _bg.color = bgC;

        _frameTop.color = frameC;
        _frameBottom.color = frameC;
        _frameLeft.color = frameC;
        _frameRight.color = frameC;

        _text.color = textC;

        _text.text = "";

        _frameTop.material = glowMaterial;
        _frameBottom.material = glowMaterial;
        _frameLeft.material = glowMaterial;
        _frameRight.material = glowMaterial;

        _bg.raycastTarget = false;
        _frameTop.raycastTarget = false;
        _frameBottom.raycastTarget = false;
        _frameLeft.raycastTarget = false;
        _frameRight.raycastTarget = false;
        _text.raycastTarget = false;

        int scanCount = _scanlinePool.Count;
        for (int i = 0; i < scanCount; i++) {
            Image img = _scanlinePool[i];
            if (img == null)
                continue;

            img.material = glowMaterial;
            img.raycastTarget = false;
        }

        ApplyFrameThickness();
        SetupPlusPattern(baseC);
    }

    private void ApplyFrameThickness() {
        RectTransform rt = _frameTop.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, frameThickness);

        rt = _frameBottom.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, frameThickness);

        rt = _frameLeft.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(frameThickness, 0f);

        rt = _frameRight.rectTransform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(frameThickness, 0f);

        RectTransform bgRt = _bg.rectTransform;
        bgRt.anchorMin = new Vector2(0f, 0f);
        bgRt.anchorMax = new Vector2(1f, 1f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(-frameThickness * 2f, -frameThickness * 2f);

        int scanCount = _scanlinePool.Count;
        for (int i = 0; i < scanCount; i++) {
            Image img = _scanlinePool[i];
            RectTransform scanRt = img.rectTransform;

            scanRt.anchorMin = new Vector2(0f, 1f);
            scanRt.anchorMax = new Vector2(1f, 1f);
            scanRt.pivot = new Vector2(0.5f, 1f);
            scanRt.anchoredPosition = new Vector2(0f, 0f);
            scanRt.sizeDelta = new Vector2(-frameThickness * 2f, scanlineThickness);
        }

        RectTransform plusRt = _plusRoot;
        plusRt.anchorMin = new Vector2(0f, 0f);
        plusRt.anchorMax = new Vector2(1f, 1f);
        plusRt.pivot = new Vector2(0.5f, 0.5f);
        plusRt.anchoredPosition = Vector2.zero;
        plusRt.sizeDelta = new Vector2(-frameThickness * 2f, -frameThickness * 2f);

        RectTransform textRt = _text.rectTransform;
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = new Vector2(-horizontalPadding * 2f, -frameThickness * 2f);
    }

    private void SetupPlusPattern(Color baseC) {
        if (!enablePlusPattern) {
            if (_plusRoot != null)
                _plusRoot.gameObject.SetActive(false);
            return;
        }

        _plusRoot.gameObject.SetActive(true);

        if (_plusSprite == null)
            _plusSprite = CreatePlusSprite(plusSpriteSize, plusStroke);

        float insetX = frameThickness;
        float insetY = frameThickness;

        float width = Mathf.Max(0f, _targetWidth - insetX * 2f);
        float height = Mathf.Max(0f, panelHeight - insetY * 2f);

        int cols = Mathf.Max(1, Mathf.FloorToInt(width / plusSpacing));
        int rows = Mathf.Max(1, Mathf.FloorToInt(height / plusSpacing));
        int needed = cols * rows;

        EnsurePlusPool(needed);

        Color c = baseC;
        c.a = plusAlpha;

        float startX = -width * 0.5f + plusSpacing * 0.5f;
        float startY = -height * 0.5f + plusSpacing * 0.5f;

        int idx = 0;
        for (int r = 0; r < rows; r++) {
            for (int col = 0; col < cols; col++) {
                Image img = _plusPool[idx];
                img.gameObject.SetActive(true);

                img.sprite = _plusSprite;
                img.material = glowMaterial;
                img.color = c;
                img.raycastTarget = false;

                RectTransform rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(plusSpriteSize, plusSpriteSize);

                float x = startX + col * plusSpacing;
                float y = startY + r * plusSpacing;

                rt.anchoredPosition = new Vector2(x, y);

                idx++;
            }
        }

        for (int i = idx; i < _plusPool.Count; i++)
            _plusPool[i].gameObject.SetActive(false);

        _bg.transform.SetSiblingIndex(0);
        _plusRoot.transform.SetSiblingIndex(1);
        _text.transform.SetSiblingIndex(2);

        int scanCount = _scanlinePool.Count;
        for (int i = 0; i < scanCount; i++)
            _scanlinePool[i].transform.SetSiblingIndex(3);

        _frameTop.transform.SetSiblingIndex(4);
        _frameBottom.transform.SetSiblingIndex(5);
        _frameLeft.transform.SetSiblingIndex(6);
        _frameRight.transform.SetSiblingIndex(7);
    }

    private void EnsurePlusPool(int needed) {
        while (_plusPool.Count < needed) {
            GameObject go = new GameObject("Plus", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_plusRoot, false);

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;

            _plusPool.Add(img);
        }
    }

    private Sprite CreatePlusSprite(int size, int stroke) {
        size = Mathf.Clamp(size, 8, 128);
        stroke = Mathf.Clamp(stroke, 1, size / 3);

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color white = new Color(1f, 1f, 1f, 1f);

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        int mid = size / 2;
        int halfStroke = stroke / 2;

        for (int y = 0; y < size; y++) {
            for (int x = mid - halfStroke; x <= mid + halfStroke; x++) {
                if (x < 0 || x >= size)
                    continue;
                pixels[y * size + x] = white;
            }
        }

        for (int x = 0; x < size; x++) {
            for (int y = mid - halfStroke; y <= mid + halfStroke; y++) {
                if (y < 0 || y >= size)
                    continue;
                pixels[y * size + x] = white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void SetMaskWidth(float width) {
        Vector2 size = _maskRect.sizeDelta;
        size.x = Mathf.Max(0f, width);
        size.y = panelHeight;
        _maskRect.sizeDelta = size;
    }

    private IEnumerator TypewriterWithGlitch(string message, float charsPerSecond) {
        if (string.IsNullOrEmpty(message))
            yield break;

        if (charsPerSecond <= 0f) {
            _text.text = message;
            yield break;
        }

        float interval = 1f / Mathf.Max(0.0001f, charsPerSecond);
        float charTimer = 0f;

        float glitchTimer = 0f;
        string currentGlitch = "";

        int index = 0;

        while (index < message.Length) {
            charTimer += DeltaTime();
            glitchTimer += DeltaTime();

            if (charTimer >= interval) {
                int advance = Mathf.FloorToInt(charTimer / interval);
                charTimer -= advance * interval;
                index = Mathf.Min(message.Length, index + advance);
            }

            if (glitchTimer >= glitchRefreshInterval) {
                glitchTimer = 0f;
                currentGlitch = BuildGlitchTail(message, index);
            }

            _text.text = message.Substring(0, index) + currentGlitch;
            yield return null;
        }

        _text.text = message;
    }

    private string BuildGlitchTail(string message, int typedCount) {
        if (glitchTailLength <= 0)
            return "";

        int remain = message.Length - typedCount;
        if (remain <= 0)
            return "";

        int len = Mathf.Min(glitchTailLength, remain);

        char[] buf = new char[len];
        for (int i = 0; i < len; i++)
            buf[i] = RandomGlitchChar();

        return new string(buf);
    }

    private char RandomGlitchChar() {
        if (string.IsNullOrEmpty(glitchCharset))
            return '#';

        int idx = _rng.Next(0, glitchCharset.Length);
        return glitchCharset[idx];
    }

    private void StartScanline(Color baseColor) {
        if (!enableScanline)
            return;

        if (glowMaterial == null)
            return;

        if (_scanlineSpawner != null)
            return;

        EnsureScanlinePool();
        DeactivateAllScanlines();
        _activeScanlineCount = 0;

        _scanlineSpawner = StartCoroutine(ScanlineSpawner(baseColor));
    }

    private void StopScanline() {
        if (_scanlineSpawner != null) {
            StopCoroutine(_scanlineSpawner);
            _scanlineSpawner = null;
        }

        int count = _scanlineRoutines.Count;
        for (int i = 0; i < count; i++) {
            Coroutine c = _scanlineRoutines[i];
            if (c != null)
                StopCoroutine(c);
            _scanlineRoutines[i] = null;
        }

        DeactivateAllScanlines();
        _activeScanlineCount = 0;
    }

    private float RandomRange(float min, float max) {
        if (max <= min)
            return min;

        float t = (float)_rng.NextDouble();
        return Mathf.Lerp(min, max, t);
    }

    private void EnsureScanlinePool() {
        int target = Mathf.Max(1, maxConcurrentScanlines);

        while (_scanlinePool.Count < target) {
            string name = "Scanline_" + _scanlinePool.Count;
            Image img = CreateImage(name, _contentRect, glowMaterial);
            img.gameObject.SetActive(false);

            _scanlinePool.Add(img);
            _scanlineRoutines.Add(null);
        }

        while (_scanlinePool.Count > target) {
            int last = _scanlinePool.Count - 1;

            Image img = _scanlinePool[last];
            if (img != null)
                Destroy(img.gameObject);

            _scanlinePool.RemoveAt(last);
            _scanlineRoutines.RemoveAt(last);
        }
    }

    private void DeactivateAllScanlines() {
        int count = _scanlinePool.Count;
        for (int i = 0; i < count; i++) {
            Image img = _scanlinePool[i];
            if (img != null)
                img.gameObject.SetActive(false);
        }
    }

    private int FindFreeScanlineIndex() {
        int count = _scanlinePool.Count;
        for (int i = 0; i < count; i++) {
            if (_scanlineRoutines[i] == null)
                return i;
        }

        return -1;
    }

    private IEnumerator ScanlineSpawner(Color baseColor) {
        while (_group.alpha > 0.5f) {
            float wait = RandomRange(scanlineIntervalRange.x, scanlineIntervalRange.y);
            yield return Wait(wait);

            float roll = (float)_rng.NextDouble();
            if (roll > scanlineChance)
                continue;

            if (_activeScanlineCount >= Mathf.Max(1, maxConcurrentScanlines))
                continue;

            int idx = FindFreeScanlineIndex();
            if (idx < 0)
                continue;

            _activeScanlineCount++;
            _scanlineRoutines[idx] = StartCoroutine(RunSingleScanline(idx, baseColor));
        }

        _scanlineSpawner = null;
    }

    private IEnumerator RunSingleScanline(int index, Color baseColor) {
        Image img = _scanlinePool[index];
        if (img == null) {
            _scanlineRoutines[index] = null;
            _activeScanlineCount = Mathf.Max(0, _activeScanlineCount - 1);
            yield break;
        }

        Color c = baseColor;
        c.a = scanlineAlpha;

        img.color = c;
        img.material = glowMaterial;
        img.raycastTarget = false;
        img.gameObject.SetActive(true);

        float topY = -frameThickness;
        float bottomY = -(panelHeight - frameThickness - scanlineThickness);

        float startY = topY + RandomRange(0f, 10f);
        float endY = bottomY - RandomRange(0f, 10f);

        float t = 0f;
        float dur = Mathf.Max(0.01f, scanlineTravelSeconds);

        while (t < 1f) {
            t += DeltaTime() / dur;

            float y = Mathf.Lerp(startY, endY, Mathf.Clamp01(t));
            Vector2 pos = img.rectTransform.anchoredPosition;
            pos.y = y;
            img.rectTransform.anchoredPosition = pos;

            yield return null;
        }

        img.gameObject.SetActive(false);

        _scanlineRoutines[index] = null;
        _activeScanlineCount = Mathf.Max(0, _activeScanlineCount - 1);
    }

    private IEnumerator Wait(float seconds) {
        if (seconds <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < seconds) {
            elapsed += DeltaTime();
            yield return null;
        }
    }

    private float DeltaTime() {
        return Time.deltaTime;
    }

    private void ForceLayerSync() {
        SetLayerRecursively(transform, gameObject.layer);
    }

    private void SetLayerRecursively(Transform root, int layer) {
        root.gameObject.layer = layer;

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private void EnsureBuilt() {
        if (_built)
            return;

        _rootRect = GetComponent<RectTransform>();

        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();

        GameObject maskGo = new GameObject("BroadcastMask", typeof(RectTransform), typeof(RectMask2D));
        maskGo.transform.SetParent(_rootRect, false);

        _maskRect = maskGo.GetComponent<RectTransform>();
        _rectMask = maskGo.GetComponent<RectMask2D>();

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(_maskRect, false);
        _contentRect = contentGo.GetComponent<RectTransform>();

        _bg = CreateImage("Background", _contentRect, null);

        GameObject plusGo = new GameObject("PlusPattern", typeof(RectTransform));
        plusGo.transform.SetParent(_contentRect, false);
        _plusRoot = plusGo.GetComponent<RectTransform>();

        EnsureScanlinePool();
        DeactivateAllScanlines();

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(_contentRect, false);
        _text = textGo.GetComponent<TextMeshProUGUI>();

        _text.font = fontAsset;
        _text.alignment = TextAlignmentOptions.Midline;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.richText = true;

        _frameTop = CreateImage("FrameTop", _contentRect, glowMaterial);
        _frameBottom = CreateImage("FrameBottom", _contentRect, glowMaterial);
        _frameLeft = CreateImage("FrameLeft", _contentRect, glowMaterial);
        _frameRight = CreateImage("FrameRight", _contentRect, glowMaterial);

        _built = true;
        ForceLayerSync();
    }

    private Image CreateImage(string name, RectTransform parent, Material mat) {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.material = mat;
        img.raycastTarget = false;

        return img;
    }

    private void HideImmediate() {
        if (!_built)
            return;

        StopScanline();

        _text.text = "";
        SetMaskWidth(0f);

        _group.alpha = 0f;

        if (_plusRoot != null)
            _plusRoot.gameObject.SetActive(enablePlusPattern);
    }
}