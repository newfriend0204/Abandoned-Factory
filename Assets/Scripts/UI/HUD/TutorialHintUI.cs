using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialHintUI : MonoBehaviour {
    [Header("UI Refs")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI tutorialText;

    [Header("Fade")]
    public float fadeDuration = 0.2f;

    private Coroutine currentRoutine;

    void Awake() {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (tutorialText == null)
            tutorialText = GetComponentInChildren<TextMeshProUGUI>();

        if (canvasGroup != null) {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void ShowTutorial(int type, float visibleDuration) {
        if (visibleDuration < 0f)
            visibleDuration = 0f;

        string msg = BuildMessage(type);
        if (string.IsNullOrEmpty(msg) || tutorialText == null || canvasGroup == null) {
            return;
        }

        tutorialText.text = msg;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(CoShowAndHide(visibleDuration));
    }

    private string BuildMessage(int type) {
        InputSettingsManager ism = InputSettingsManager.Instance;
        if (ism == null) {
            return string.Empty;
        }

        switch (type) {
            case 0:
                return
                    GetActionKeyLabel(ism, "MoveForward") + " : 앞으로 이동\n" +
                    GetActionKeyLabel(ism, "MoveBackward") + " : 뒤로 이동\n" +
                    GetActionKeyLabel(ism, "MoveLeft") + " : 왼쪽으로 이동\n" +
                    GetActionKeyLabel(ism, "MoveRight") + " : 오른쪽으로 이동\n" +
                    GetActionKeyLabel(ism, "Jump") + " : 점프\n" +
                    GetActionKeyLabel(ism, "Run") + " : 달리기\n" +
                    GetActionKeyLabel(ism, "Interact") + " : 상호작용\n" +
                    "ESC : 일시정지\n";

            case 1:
                return
                    GetActionKeyLabel(ism, "ToggleFlashlight") + " : 손전등 키기";

            case 2:
                return
                    GetActionKeyLabel(ism, "ShowSolution") + " : 자동으로 퍼즐 풀기\n" +
                    GetActionKeyLabel(ism, "ShowHint") + " : 다음 눌러야 할 버튼 알기";

            default:
                return string.Empty;
        }
    }

    private string GetActionKeyLabel(InputSettingsManager ism, string actionId) {
        KeyCode primary = ism.GetPrimaryKey(actionId);
        KeyCode secondary = ism.GetSecondaryKey(actionId);

        if (primary == KeyCode.None && secondary == KeyCode.None)
            return "(키 미설정)";

        List<string> parts = new List<string>();

        if (primary != KeyCode.None)
            parts.Add(ism.FormatKeyName(primary));

        if (secondary != KeyCode.None)
            parts.Add(ism.FormatKeyName(secondary));

        return string.Join(", ", parts);
    }

    private IEnumerator CoShowAndHide(float visibleDuration) {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeDuration);

        while (t < dur) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            canvasGroup.alpha = k;
            yield return null;
        }
        canvasGroup.alpha = 1f;

        if (visibleDuration > 0f) {
            yield return new WaitForSecondsRealtime(visibleDuration);
        }

        t = 0f;
        while (t < dur) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            canvasGroup.alpha = 1f - k;
            yield return null;
        }
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        currentRoutine = null;
    }

    public void ShowCustomPersistent(string message) {
        if (tutorialText == null || canvasGroup == null)
            return;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        tutorialText.text = message;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        currentRoutine = null;
    }

    public void HideImmediate() {
        if (tutorialText == null || canvasGroup == null)
            return;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        currentRoutine = null;
    }
}