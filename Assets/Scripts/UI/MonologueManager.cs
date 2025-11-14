using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MonologueManager : MonoBehaviour {
    [Header("Panel / Container")]
    public RectTransform panelRoot;
    public CanvasGroup panelCanvasGroup;
    public RectTransform messageContainer;
    public MonologueEntry messagePrefab;

    [Header("Config")]
    public int maxMessages = 3;
    public bool useTypewriterEffect = true;
    public float typewriterCharsPerSecond = 40f;
    public float defaultVisibleDuration = 5.0f;
    public float fadeInDuration = 0.2f;
    public float fadeOutDuration = 0.3f;
    public float panelFadeDuration = 0.2f;

    [Header("SFX")]
    public AudioSource audioSource;
    public AudioClip showMessageClip;

    private Coroutine panelFadeCoroutine;

    public int MessageCount {
        get {
            if (messageContainer == null) {
                return 0;
            }

            return messageContainer.childCount;
        }
    }

    private void Awake() {
        panelCanvasGroup.alpha = 0f;
        panelRoot.gameObject.SetActive(false);
    }

    public void ShowMessage(string message) {
        ShowMessageInternal(message, defaultVisibleDuration, useTypewriterEffect);
    }

    public void ShowMessage(string message, float visibleDurationAfterTyping) {
        ShowMessageInternal(message, visibleDurationAfterTyping, useTypewriterEffect);
    }

    public void ShowMessage(string message, float visibleDurationAfterTyping, bool useTypewriter) {
        ShowMessageInternal(message, visibleDurationAfterTyping, useTypewriter);
    }

    private void ShowMessageInternal(string message, float visibleDurationAfterTyping, bool useTypewriter) {
        if (string.IsNullOrEmpty(message))
            return;

        if (visibleDurationAfterTyping <= 0f)
            visibleDurationAfterTyping = defaultVisibleDuration;

        if (!panelRoot.gameObject.activeSelf) {
            panelRoot.gameObject.SetActive(true);
            panelCanvasGroup.alpha = 0f;
        }

        FadePanelTo(1f);

        if (MessageCount >= maxMessages) {
            Transform oldest = messageContainer.GetChild(0);
            MonologueEntry oldestEntry = oldest.GetComponent<MonologueEntry>();

            if (oldestEntry != null)
                oldestEntry.ForceHide();
            else
                Destroy(oldest.gameObject);
        }

        MonologueEntry newEntry = Instantiate(messagePrefab, messageContainer);

        newEntry.Initialize(this);
        newEntry.Play(
            message,
            visibleDurationAfterTyping,
            useTypewriter,
            typewriterCharsPerSecond,
            fadeInDuration,
            fadeOutDuration
        );

        audioSource.PlayOneShot(showMessageClip);
    }

    public void FadeOutPanel() {
        FadePanelTo(0f);
    }

    private void FadePanelTo(float targetAlpha) {
        if (panelFadeCoroutine != null) {
            StopCoroutine(panelFadeCoroutine);
        }

        panelFadeCoroutine = StartCoroutine(PanelFadeRoutine(targetAlpha));
    }

    private IEnumerator PanelFadeRoutine(float targetAlpha) {
        float startAlpha = panelCanvasGroup.alpha;
        float duration = panelFadeDuration;

        if (duration <= 0f) {
            panelCanvasGroup.alpha = targetAlpha;

            if (targetAlpha <= 0f)
                panelRoot.gameObject.SetActive(false);

            yield break;
        }

        float time = 0f;

        while (time < duration) {
            time += Time.deltaTime;

            float t = time / duration;

            if (t > 1f)
                t = 1f;

            float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            panelCanvasGroup.alpha = alpha;

            yield return null;
        }

        panelCanvasGroup.alpha = targetAlpha;

        if (targetAlpha <= 0f)
            panelRoot.gameObject.SetActive(false);
    }
}