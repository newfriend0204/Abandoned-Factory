using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MonologueEntry : MonoBehaviour {
    [Header("UI")]
    public TextMeshProUGUI text;
    public CanvasGroup canvasGroup;

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

    private void Awake() {
        if (noiseImage != null && noiseImage.material != null) {
            noiseRuntimeMaterial = Instantiate(noiseImage.material);
            noiseImage.material = noiseRuntimeMaterial;

            if (noiseRuntimeMaterial.HasProperty(MainTexId)) {
                noiseBaseOffset = noiseRuntimeMaterial.GetTextureOffset(MainTexId);
            }
        }
    }

    private void Update() {
        UpdateNoise();
    }

    private void UpdateNoise() {
        if (!enableNoise)
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

        if (owner != null) {
            if (owner.MessageCount <= 1)
                isLastMessage = true;
        }

        if (isLastMessage && owner != null) {
            owner.FadeOutPanel();
        }

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

            for (int i = 0; i < message.Length; i++) {
                text.text = message.Substring(0, i + 1);

                if (delayPerChar > 0f)
                    yield return new WaitForSeconds(delayPerChar);
                else
                    yield return null;
            }
        } else {
            text.text = message;
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

        if (owner != null) {
            if (owner.MessageCount <= 1) {
                isLast = true;
            }
        }

        if (isLast && owner != null) {
            owner.FadeOutPanel();
        }

        Destroy(gameObject);
    }
}