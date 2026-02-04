using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MonologueEntry : MonoBehaviour {
    [Header("UI")]
    public TextMeshProUGUI text;
    public CanvasGroup canvasGroup;

    [Header("Backdrop Auto Width")]
    [Tooltip("If enabled, noise/background width follows text width. Height stays fixed (current inspector height).")]
    public bool autoResizeBackdropWidth = true;

    [Tooltip("Optional background image. If assigned, it will resize together with noiseImage.")]
    public Image backgroundImage;

    [Tooltip("Extra width added to the text preferred width.")]
    public float backdropPaddingX = 60f;

    [Tooltip("Minimum width for backdrop/noise.")]
    public float backdropMinWidth = 140f;

    [Tooltip("If true, update backdrop width during typewriter (every character).")]
    public bool updateBackdropDuringTypewriter = true;

    [Tooltip("Force noise/background anchors & pivot to center and position to (0,0) so it expands from center.")]
    public bool forceCenterAnchors = true;

    [Header("Noise")]
    public Image noiseImage;
    public bool enableNoise = true;
    public float noiseRandomInterval = 0.05f;
    public Vector2 noiseOffsetAmplitude = new Vector2(0.03f, 0.03f);

    private MonologueManager owner;
    private Coroutine playRoutine;

    private Material noiseRuntimeMaterial;
    private Vector2 noiseBaseOffset;
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private float noiseTimer = 0f;
    private Vector2 currentRandomOffset = Vector2.zero;

    private RectTransform noiseRt;
    private RectTransform backgroundRt;
    private float fixedBackdropHeight = 0f;

    private void Awake() {
        if (noiseImage != null) {
            noiseRt = noiseImage.rectTransform;
            fixedBackdropHeight = noiseRt.sizeDelta.y;

            if (noiseImage.material != null) {
                noiseRuntimeMaterial = Instantiate(noiseImage.material);
                noiseImage.material = noiseRuntimeMaterial;

                if (noiseRuntimeMaterial.HasProperty(MainTexId))
                    noiseBaseOffset = noiseRuntimeMaterial.GetTextureOffset(MainTexId);
            }
        }

        if (backgroundImage != null) {
            backgroundRt = backgroundImage.rectTransform;

            if (fixedBackdropHeight <= 0f)
                fixedBackdropHeight = backgroundRt.sizeDelta.y;
        }

        if (forceCenterAnchors)
            ForceCenterBackdrop();

        if (autoResizeBackdropWidth)
            UpdateBackdropWidth();
    }

    private void Update() {
        UpdateNoise();
    }

    private void UpdateNoise() {
        if (!enableNoise)
            return;

        if (noiseRuntimeMaterial == null)
            return;

        noiseTimer += Time.unscaledDeltaTime;

        if (noiseTimer >= noiseRandomInterval) {
            noiseTimer = 0f;

            float rx = (Random.value * 2f - 1f) * noiseOffsetAmplitude.x;
            float ry = (Random.value * 2f - 1f) * noiseOffsetAmplitude.y;
            currentRandomOffset = new Vector2(rx, ry);
        }

        Vector2 finalOffset = noiseBaseOffset + currentRandomOffset;
        noiseRuntimeMaterial.SetTextureOffset(MainTexId, finalOffset);
    }

    public void Initialize(MonologueManager owner) {
        this.owner = owner;
    }

    public void Play(
        string message,
        float visibleDurationAfterTyping,
        bool useTypewriter,
        float charsPerSecond,
        float fadeInDuration,
        float fadeOutDuration
    ) {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(
            PlayRoutine(
                message,
                visibleDurationAfterTyping,
                useTypewriter,
                charsPerSecond,
                fadeInDuration,
                fadeOutDuration
            )
        );
    }

    public void ForceHide() {
        if (playRoutine != null) {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        bool isLastMessage = false;

        if (owner != null && owner.MessageCount <= 1)
            isLastMessage = true;

        if (isLastMessage && owner != null)
            owner.FadeOutPanel();

        Destroy(gameObject);
    }

    private IEnumerator PlayRoutine(
        string message,
        float visibleDurationAfterTyping,
        bool useTypewriter,
        float charsPerSecond,
        float fadeInDuration,
        float fadeOutDuration
    ) {
        canvasGroup.alpha = 0f;

        if (useTypewriter)
            text.text = string.Empty;
        else
            text.text = message;

        if (autoResizeBackdropWidth)
            UpdateBackdropWidth();

        float time = 0f;

        if (fadeInDuration > 0f) {
            while (time < fadeInDuration) {
                time += Time.deltaTime;

                float t = time / fadeInDuration;
                if (t > 1f)
                    t = 1f;

                canvasGroup.alpha = t;
                yield return null;
            }
        }

        canvasGroup.alpha = 1f;

        if (useTypewriter && text != null && !string.IsNullOrEmpty(message)) {
            float delayPerChar = 0.02f;

            if (charsPerSecond > 0f)
                delayPerChar = 1f / charsPerSecond;

            text.text = string.Empty;

            if (autoResizeBackdropWidth && updateBackdropDuringTypewriter)
                UpdateBackdropWidth();

            for (int i = 0; i < message.Length; i++) {
                text.text = message.Substring(0, i + 1);

                if (autoResizeBackdropWidth && updateBackdropDuringTypewriter)
                    UpdateBackdropWidth();

                if (delayPerChar > 0f)
                    yield return new WaitForSeconds(delayPerChar);
                else
                    yield return null;
            }
        } else {
            text.text = message;

            if (autoResizeBackdropWidth)
                UpdateBackdropWidth();
        }

        if (visibleDurationAfterTyping > 0f)
            yield return new WaitForSeconds(visibleDurationAfterTyping);

        time = 0f;

        if (fadeOutDuration > 0f) {
            while (time < fadeOutDuration) {
                time += Time.deltaTime;

                float t = time / fadeOutDuration;
                if (t > 1f)
                    t = 1f;

                canvasGroup.alpha = 1f - t;
                yield return null;
            }
        }

        bool isLast = false;

        if (owner != null && owner.MessageCount <= 1)
            isLast = true;

        if (isLast && owner != null)
            owner.FadeOutPanel();

        Destroy(gameObject);
    }

    private void ForceCenterBackdrop() {
        if (noiseRt != null)
            ForceCenterRect(noiseRt);

        if (backgroundRt != null)
            ForceCenterRect(backgroundRt);
    }

    private void ForceCenterRect(RectTransform rt) {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private void UpdateBackdropWidth() {
        if (text == null)
            return;

        float preferredWidth = GetPreferredWidth(text);
        float width = preferredWidth + backdropPaddingX;

        if (width < backdropMinWidth)
            width = backdropMinWidth;

        if (fixedBackdropHeight <= 0f) {
            if (noiseRt != null)
                fixedBackdropHeight = noiseRt.sizeDelta.y;
            else if (backgroundRt != null)
                fixedBackdropHeight = backgroundRt.sizeDelta.y;
        }

        if (noiseRt != null) {
            noiseRt.sizeDelta = new Vector2(width, fixedBackdropHeight);
            noiseRt.anchoredPosition = Vector2.zero;
        }

        if (backgroundRt != null) {
            backgroundRt.sizeDelta = new Vector2(width, fixedBackdropHeight);
            backgroundRt.anchoredPosition = Vector2.zero;
        }
    }

    private float GetPreferredWidth(TextMeshProUGUI tmp) {
        tmp.ForceMeshUpdate();

        Vector2 v = tmp.GetPreferredValues(tmp.text, Mathf.Infinity, Mathf.Infinity);
        if (v.x < 0f)
            return 0f;

        return v.x;
    }
}