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

    [Header("Shutter (Pose Only)")]
    [SerializeField] private Transform shutter;
    [SerializeField] private Transform shutterClosedPose;
    [SerializeField] private Transform shutterOpenPose;
    [SerializeField] private bool shutterUseLocal = true;
    [SerializeField] private bool shutterLerpRotation = true;
    [SerializeField] private bool snapClosedOnStartIfNotCompleted = true;

    [Header("Shutter Motion")]
    [SerializeField] private float openDuration = 4f;
    [SerializeField] private AnimationCurve ease;

    [Header("Shutter SFX")]
    [SerializeField] private AudioSource shutterSfxSource;
    [SerializeField] private AudioClip shutterSfx;
    [SerializeField] private float shutterVolume = 1f;

    private bool completionStarted;
    private bool shutterOpened;

    private float lastShownPercent = -999f;
    private Color lastShownColor = default;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (percentText == null)
            percentText = GetComponentInChildren<TMP_Text>(true);
    }

    private void Start() {
        if (IsStep7AlreadyCompletedFromSaveOrState()) {
            SnapToCompletedState();
            return;
        }

        if (snapClosedOnStartIfNotCompleted)
            ApplyShutterClosedInstant();

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

    private void SnapToCompletedState() {
        completionStarted = true;
        shutterOpened = true;

        RenderPercent(1f);
        LockAllValves(true);
        ForceAllValvesVisualComplete();

        ApplyShutterOpenInstant();
    }

    private void ApplyShutterClosedInstant() {
        if (!HasShutterPoses())
            return;

        ApplyPoseInstant(shutterClosedPose);
    }

    private void ApplyShutterOpenInstant() {
        if (!HasShutterPoses())
            return;

        ApplyPoseInstant(shutterOpenPose);
    }

    private bool HasShutterPoses() {
        if (shutter == null)
            return false;

        if (shutterClosedPose == null)
            return false;

        if (shutterOpenPose == null)
            return false;

        return true;
    }

    private void ApplyPoseInstant(Transform pose) {
        if (shutter == null || pose == null)
            return;

        if (shutterUseLocal) {
            shutter.localPosition = pose.localPosition;
            shutter.localRotation = pose.localRotation;
        } else {
            shutter.position = pose.position;
            shutter.rotation = pose.rotation;
        }

        shutter.localScale = pose.localScale;
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

        if (!HasShutterPoses())
            yield break;

        PlayShutterSfx();

        float t = 0f;
        float dur = Mathf.Max(0.01f, openDuration);

        Vector3 startPos;
        Vector3 endPos;
        Quaternion startRot = Quaternion.identity;
        Quaternion endRot = Quaternion.identity;

        if (shutterUseLocal) {
            startPos = shutterClosedPose.localPosition;
            endPos = shutterOpenPose.localPosition;
            startRot = shutterClosedPose.localRotation;
            endRot = shutterOpenPose.localRotation;
        } else {
            startPos = shutterClosedPose.position;
            endPos = shutterOpenPose.position;
            startRot = shutterClosedPose.rotation;
            endRot = shutterOpenPose.rotation;
        }

        Vector3 startScale = shutterClosedPose.localScale;
        Vector3 endScale = shutterOpenPose.localScale;

        ApplyPoseInstant(shutterClosedPose);

        while (t < dur) {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = ease != null ? ease.Evaluate(u) : u;

            Vector3 p = Vector3.Lerp(startPos, endPos, e);
            Vector3 s = Vector3.Lerp(startScale, endScale, e);

            if (shutterUseLocal)
                shutter.localPosition = p;
            else
                shutter.position = p;

            shutter.localScale = s;

            if (shutterLerpRotation) {
                Quaternion r = Quaternion.Slerp(startRot, endRot, e);
                if (shutterUseLocal)
                    shutter.localRotation = r;
                else
                    shutter.rotation = r;
            }

            yield return null;
        }

        ApplyPoseInstant(shutterOpenPose);
    }

    private void PlayShutterSfx() {
        if (shutterSfx == null)
            return;

        float vol = Mathf.Clamp01(shutterVolume);

        if (shutterSfxSource != null) {
            shutterSfxSource.PlayOneShot(shutterSfx, vol);
            return;
        }

        if (shutter != null)
            AudioSource.PlayClipAtPoint(shutterSfx, shutter.position, vol);
    }
}