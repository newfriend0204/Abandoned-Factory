using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class Chap1DeathUIController : MonoBehaviour {
    [Header("Root & Panel")]
    public RectTransform panelRoot;
    public CanvasGroup buttonsGroup;

    [Header("Texts")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;

    [Header("Typewriter")]
    public float titleCharsPerSecond = 20f;
    public float subtitleCharsPerSecond = 20f;

    public int typeSoundEveryNChars = 2;
    public AudioSource typeAudioSource;
    public AudioClip typeSoundClip;

    [Header("Fade Animation")]
    public float fadeInDuration = 0.25f;

    [Header("Noise Background")]
    public Image noiseImage;
    public bool enableNoise = true;
    public float noiseRandomInterval = 0.05f;
    public Vector2 noiseOffsetAmplitude = new Vector2(0.03f, 0.03f);

    [Header("Background Audio (Noise BGM)")]
    public AudioSource backgroundAudioSource;
    public bool fadeBackgroundAudioWithUI = true;

    [Header("Strings")]
    [TextArea] public string titleString = "사망";
    [TextArea] public string subtitleString = "공장에서 탈출하지 못했습니다.";

    private CanvasGroup rootCanvasGroup;
    private Coroutine sequenceRoutine;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private Material noiseRuntimeMaterial;
    private Vector2 noiseBaseOffset;
    private float noiseTimer = 0f;
    private Vector2 currentRandomOffset = Vector2.zero;

    private float backgroundAudioInitialVolume = 1f;

    private void Awake() {
        rootCanvasGroup = GetComponent<CanvasGroup>();

        if (panelRoot == null)
            panelRoot = transform as RectTransform;

        if (noiseImage != null && noiseImage.material != null) {
            noiseRuntimeMaterial = Instantiate(noiseImage.material);
            noiseImage.material = noiseRuntimeMaterial;

            if (noiseRuntimeMaterial.HasProperty(MainTexId)) {
                noiseBaseOffset = noiseRuntimeMaterial.GetTextureOffset(MainTexId);
            }
        }

        if (backgroundAudioSource == null && noiseImage != null) {
            backgroundAudioSource = noiseImage.GetComponent<AudioSource>();
        }

        if (backgroundAudioSource != null) {
            backgroundAudioInitialVolume = backgroundAudioSource.volume;
            backgroundAudioSource.volume = 0f;
        }
    }

    private void OnEnable() {
        PlayDeathSequence();

        if (backgroundAudioSource != null && !backgroundAudioSource.isPlaying) {
            backgroundAudioSource.Play();
        }
    }

    private void Update() {
        UpdateNoise();
    }

    private void UpdateNoise() {
        if (!enableNoise || noiseRuntimeMaterial == null)
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

    public void PlayDeathSequence() {
        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);

        sequenceRoutine = StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine() {
        rootCanvasGroup.alpha = 0f;

        if (buttonsGroup != null) {
            buttonsGroup.alpha = 0f;
            buttonsGroup.interactable = false;
            buttonsGroup.blocksRaycasts = false;
        }

        if (titleText != null)
            titleText.text = string.Empty;
        if (subtitleText != null)
            subtitleText.text = string.Empty;

        if (backgroundAudioSource != null) {
            backgroundAudioSource.volume = 0f;
        }

        yield return StartCoroutine(FadeInRoutine());

        if (titleText != null)
            yield return StartCoroutine(TypeTextRoutine(titleText, titleString, titleCharsPerSecond));

        yield return new WaitForSecondsRealtime(0.2f);

        if (subtitleText != null)
            yield return StartCoroutine(TypeTextRoutine(subtitleText, subtitleString, subtitleCharsPerSecond));

        if (buttonsGroup != null)
            yield return StartCoroutine(ShowButtonsRoutine());

        sequenceRoutine = null;
    }

    private IEnumerator FadeInRoutine() {
        float t = 0f;

        while (t < fadeInDuration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeInDuration);

            rootCanvasGroup.alpha = k;

            if (fadeBackgroundAudioWithUI && backgroundAudioSource != null) {
                backgroundAudioSource.volume = Mathf.Lerp(0f, backgroundAudioInitialVolume, k);
            }

            yield return null;
        }

        rootCanvasGroup.alpha = 1f;

        if (fadeBackgroundAudioWithUI && backgroundAudioSource != null) {
            backgroundAudioSource.volume = backgroundAudioInitialVolume;
        }
    }

    private IEnumerator TypeTextRoutine(TextMeshProUGUI target, string message, float charsPerSecond) {
        if (target == null)
            yield break;

        if (string.IsNullOrEmpty(message) || charsPerSecond <= 0f) {
            target.text = message;
            yield break;
        }

        target.text = string.Empty;

        float delay = 1f / charsPerSecond;
        int typedCount = 0;

        for (int i = 0; i < message.Length; i++) {
            char c = message[i];

            target.text = message.Substring(0, i + 1);
            typedCount++;

            if (typeAudioSource != null && typeSoundClip != null && !char.IsWhiteSpace(c)) {
                if (typeSoundEveryNChars <= 1 || (typedCount % typeSoundEveryNChars) == 0) {
                    typeAudioSource.PlayOneShot(typeSoundClip);
                }
            }

            yield return new WaitForSecondsRealtime(delay);
        }
    }

    private IEnumerator ShowButtonsRoutine() {
        if (Chap1DeathManager.Instance != null) {
            Chap1DeathManager.Instance.PlayDeathEnterSfx();
        }

        float duration = 0.25f;
        float t = 0f;

        if (buttonsGroup != null) {
            buttonsGroup.alpha = 0f;
            buttonsGroup.gameObject.SetActive(true);
        }

        while (t < duration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            if (buttonsGroup != null)
                buttonsGroup.alpha = k;

            yield return null;
        }

        if (buttonsGroup != null) {
            buttonsGroup.alpha = 1f;
            buttonsGroup.interactable = true;
            buttonsGroup.blocksRaycasts = true;
        }
    }
}