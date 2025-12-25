using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

public class Chap2Step7ValveProgressController : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;

    [Header("Percent Text")]
    [SerializeField] private TMP_Text percentText;

    [Header("Valves")]
    [SerializeField] private Chap2Step7Valve[] valves;

    [Header("Colors")]
    [SerializeField] private Color lowColor = Color.red;
    [SerializeField] private Color midColor = Color.yellow;
    [SerializeField] private Color highColor = Color.green;
    [SerializeField] private float midThreshold = 0.5f;
    [SerializeField] private float highThreshold = 0.9f;

    [Header("Completion")]
    [SerializeField] private int stepNumber = 7;
    [SerializeField] private float delayBeforeShutterOpen = 4f;

    [Header("Shutter Open")]
    [SerializeField] private Transform shutter;
    [SerializeField] private float openDuration = 4f;
    [SerializeField] private float targetScaleY = 0.1f;
    [SerializeField] private float raiseAmountY = 5f;
    [SerializeField] private AnimationCurve ease;
    [SerializeField] private AudioClip shutterSfx;
    [SerializeField] private float shutterVolume = 1f;

    private bool completionStarted = false;
    private bool shutterOpened = false;

    private Vector3 shutterClosedLocalPos;
    private float shutterClosedScaleY;
    private bool shutterBaselineCached = false;

    private float lastShownPercent = -999f;
    private Color lastShownColor = default;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (percentText == null)
            percentText = GetComponentInChildren<TMP_Text>(true);

        CacheShutterBaselineIfNeeded();
    }

    private void Start() {
        CacheShutterBaselineIfNeeded();

        if (IsStep7AlreadyCompletedFromSaveOrState()) {
            SnapToCompletedState();
            return;
        }

        RenderPercent(GetTotalProgress01());
    }

    private void Update() {
        if (IsStep7AlreadyCompletedFromSaveOrState()) {
            if (!shutterOpened)
                SnapToCompletedState();

            return;
        }

        if (!IsStep7Active())
            return;

        float t01 = GetTotalProgress01();
        RenderPercent(t01);

        if (!completionStarted && t01 >= 1f)
            StartCoroutine(CompletionRoutine());
    }

    private bool IsStep7Active() {
        if (gameManager == null)
            return false;

        if (gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return false;

        var y = Chap2YStepSequenceManager.Instance;
        if (y == null)
            return false;

        if (y.CurrentStep != stepNumber)
            return false;

        return true;
    }

    private bool IsStep7AlreadyCompletedFromSaveOrState() {
        if (gameManager != null && gameManager.State == GameManagerChap2.Chap2State.PostYChase)
            return true;

        int saved = Chap2CheckpointManager.GetSavedChap2StateIntOrDefault(0);
        if (saved == (int)GameManagerChap2.Chap2State.PostYChase)
            return true;

        return false;
    }

    private void CacheShutterBaselineIfNeeded() {
        if (shutterBaselineCached)
            return;

        if (shutter == null)
            return;

        shutterClosedLocalPos = shutter.localPosition;
        shutterClosedScaleY = shutter.localScale.y;
        shutterBaselineCached = true;
    }

    private void SnapToCompletedState() {
        completionStarted = true;
        shutterOpened = true;

        RenderPercent(1f);
        LockAllValves(true);
        ForceAllValvesVisualComplete();

        ApplyShutterOpenInstant();
    }

    private void ApplyShutterOpenInstant() {
        CacheShutterBaselineIfNeeded();
        if (!shutterBaselineCached)
            return;

        if (shutter == null)
            return;

        Vector3 s = shutter.localScale;
        s.y = targetScaleY;
        shutter.localScale = s;

        shutter.localPosition = shutterClosedLocalPos + new Vector3(0f, raiseAmountY, 0f);
    }

    private float GetTotalProgress01() {
        if (valves == null || valves.Length == 0)
            return 0f;

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < valves.Length; i++) {
            if (valves[i] == null)
                continue;

            sum += Mathf.Clamp01(valves[i].Progress01);
            count++;
        }

        if (count <= 0)
            return 0f;

        float avg = sum / count;
        if (avg > 0.99999f)
            avg = 1f;

        return Mathf.Clamp01(avg);
    }

    private void RenderPercent(float total01) {
        if (percentText == null)
            return;

        total01 = Mathf.Clamp01(total01);

        float percent = total01 * 100f;
        Color c = PickColor(total01);

        bool samePercent = Mathf.Abs(percent - lastShownPercent) < 0.001f;
        bool sameColor = c.Equals(lastShownColor);

        if (samePercent && sameColor)
            return;

        lastShownPercent = percent;
        lastShownColor = c;

        string num = percent.ToString("000.00", CultureInfo.InvariantCulture) + "%";
        percentText.text = num;
        percentText.color = c;
    }

    private Color PickColor(float t01) {
        if (t01 >= highThreshold)
            return highColor;

        if (t01 >= midThreshold)
            return midColor;

        return lowColor;
    }

    private IEnumerator CompletionRoutine() {
        completionStarted = true;
        LockAllValves(true);

        float delay = Mathf.Max(0f, delayBeforeShutterOpen);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!IsStep7Active())
            yield break;

        RenderPercent(1f);

        if (!shutterOpened)
            yield return StartCoroutine(OpenShutterRoutine());

        if (!IsStep7Active())
            yield break;

        var y = Chap2YStepSequenceManager.Instance;
        if (y != null)
            y.CompleteStep(stepNumber);
    }

    private void LockAllValves(bool locked) {
        if (valves == null)
            return;

        for (int i = 0; i < valves.Length; i++) {
            if (valves[i] == null)
                continue;

            valves[i].SetLocked(locked);
        }
    }

    private void ForceAllValvesVisualComplete() {
        if (valves == null)
            return;

        for (int i = 0; i < valves.Length; i++) {
            if (valves[i] == null)
                continue;

            valves[i].ForceSetProgress01(1f, true);
        }
    }

    private IEnumerator OpenShutterRoutine() {
        shutterOpened = true;
        CacheShutterBaselineIfNeeded();

        if (shutter == null)
            yield break;

        float t = 0f;
        float dur = Mathf.Max(0.01f, openDuration);

        Vector3 startPos = shutterBaselineCached ? shutterClosedLocalPos : shutter.localPosition;
        float startScaleY = shutterBaselineCached ? shutterClosedScaleY : shutter.localScale.y;

        Vector3 targetPos = startPos + new Vector3(0f, raiseAmountY, 0f);

        if (shutterSfx != null)
            AudioSource.PlayClipAtPoint(shutterSfx, shutter.position, shutterVolume);

        while (t < dur) {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = ease != null ? ease.Evaluate(u) : u;

            Vector3 s = shutter.localScale;
            s.y = Mathf.Lerp(startScaleY, targetScaleY, e);
            shutter.localScale = s;

            shutter.localPosition = Vector3.Lerp(startPos, targetPos, e);

            yield return null;
        }

        Vector3 sFinal = shutter.localScale;
        sFinal.y = targetScaleY;
        shutter.localScale = sFinal;
        shutter.localPosition = targetPos;
    }
}