using System.Collections;
using UnityEngine;

public class Chap2CenterModuleController : MonoBehaviour {
    [System.Serializable]
    private struct Pose {
        public Vector3 localPos;
        public Quaternion localRot;
    }

    public static bool IsSwapping { get; private set; }

    [Header("Refs")]
    [SerializeField] private Chap2YStepSequenceManager sequenceManager;

    [Header("Modules (1~7)")]
    [SerializeField] private Transform[] modules = new Transform[7];

    [Header("Anchors")]
    [SerializeField] private Transform activeAnchor;
    [SerializeField] private Transform inactiveAnchor;

    [Header("Motion")]
    [SerializeField] private float moveDurationPerModule = 0.9f;
    [SerializeField] private float swapCooldownSeconds = 1.0f;

    private Pose[] inactivePoses;
    private Coroutine swapRoutine;

    private void Awake() {
        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        inactivePoses = new Pose[modules.Length];
        CacheInactivePoses();
    }

    private void OnEnable() {
        TryBind();
        CacheInactivePoses();
        ApplyStepInstant(GetSafeStep());
    }

    private void OnDisable() {
        Unbind();

        if (swapRoutine != null) {
            StopCoroutine(swapRoutine);
            swapRoutine = null;
        }

        IsSwapping = false;

        CacheInactivePoses();
        ApplyAllInactiveInstant();
    }

    private void TryBind() {
        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (sequenceManager == null)
            return;

        sequenceManager.StepChanged -= OnStepChanged;
        sequenceManager.StepChanged += OnStepChanged;
    }

    private void Unbind() {
        if (sequenceManager == null)
            return;

        sequenceManager.StepChanged -= OnStepChanged;
    }

    private int GetSafeStep() {
        if (sequenceManager == null)
            return 1;

        return Mathf.Clamp(sequenceManager.CurrentStep, 1, modules.Length);
    }

    private void CacheInactivePoses() {
        for (int i = 0; i < modules.Length; i++) {
            if (modules[i] == null)
                continue;

            if (inactiveAnchor != null) {
                inactivePoses[i] = new Pose {
                    localPos = inactiveAnchor.localPosition,
                    localRot = inactiveAnchor.localRotation
                };
                continue;
            }

            inactivePoses[i] = new Pose {
                localPos = modules[i].localPosition,
                localRot = modules[i].localRotation
            };
        }
    }

    private void OnStepChanged(int oldStep, int newStep) {
        if (activeAnchor == null)
            return;

        oldStep = Mathf.Clamp(oldStep, 1, modules.Length);
        newStep = Mathf.Clamp(newStep, 1, modules.Length);

        if (oldStep == newStep)
            return;

        if (swapRoutine != null) {
            StopCoroutine(swapRoutine);
            swapRoutine = null;
            IsSwapping = false;
        }

        CacheInactivePoses();
        swapRoutine = StartCoroutine(CoSwapSequential(oldStep, newStep));
    }

    private IEnumerator CoSwapSequential(int oldStep, int newStep) {
        IsSwapping = true;

        int oldIdx = oldStep - 1;
        int newIdx = newStep - 1;

        Transform oldModule = modules[oldIdx];
        Transform newModule = modules[newIdx];

        for (int i = 0; i < modules.Length; i++) {
            if (modules[i] == null)
                continue;

            if (i == oldIdx || i == newIdx)
                continue;

            ApplyPose(modules[i], inactivePoses[i]);
        }

        Pose oldFrom = GetCurrentPose(oldModule);
        Pose oldTo = inactivePoses[oldIdx];

        Pose newTo = new Pose { localPos = activeAnchor.localPosition, localRot = activeAnchor.localRotation };

        float dur = Mathf.Max(0.01f, moveDurationPerModule);
        float half = Mathf.Max(0.01f, dur * 0.5f);

        if (oldModule != null) {
            yield return MoveZOnly(oldModule, oldFrom, oldTo, half);
            yield return MoveXYOnly(oldModule, GetCurrentPose(oldModule), oldTo, half);
            ApplyPose(oldModule, oldTo);
        }

        float cd = Mathf.Max(0f, swapCooldownSeconds);
        if (cd > 0f) {
            float t = 0f;
            while (t < cd) {
                float dt = Time.deltaTime;
                if (dt > 0f)
                    t += dt;

                yield return null;
            }
        }

        if (newModule != null) {
            Pose newFrom = GetCurrentPose(newModule);
            yield return MoveYOnly(newModule, newFrom, newTo, half);
            yield return MoveXZOnly(newModule, GetCurrentPose(newModule), newTo, half);
            ApplyPose(newModule, newTo);
        }

        swapRoutine = null;
        IsSwapping = false;
    }

    private IEnumerator MoveZOnly(Transform t, Pose from, Pose to, float dur) {
        float time = 0f;
        while (time < dur) {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / dur);

            Vector3 p = from.localPos;
            p.z = Mathf.Lerp(from.localPos.z, to.localPos.z, k);
            t.localPosition = p;

            t.localRotation = Quaternion.Slerp(from.localRot, to.localRot, k);

            yield return null;
        }
    }

    private IEnumerator MoveXYOnly(Transform t, Pose from, Pose to, float dur) {
        float time = 0f;
        while (time < dur) {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / dur);

            Vector3 p = from.localPos;
            p.x = Mathf.Lerp(from.localPos.x, to.localPos.x, k);
            p.y = Mathf.Lerp(from.localPos.y, to.localPos.y, k);
            t.localPosition = p;

            t.localRotation = Quaternion.Slerp(from.localRot, to.localRot, k);

            yield return null;
        }
    }

    private IEnumerator MoveYOnly(Transform t, Pose from, Pose to, float dur) {
        float time = 0f;
        while (time < dur) {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / dur);

            Vector3 p = from.localPos;
            p.y = Mathf.Lerp(from.localPos.y, to.localPos.y, k);
            t.localPosition = p;

            t.localRotation = Quaternion.Slerp(from.localRot, to.localRot, k);

            yield return null;
        }
    }

    private IEnumerator MoveXZOnly(Transform t, Pose from, Pose to, float dur) {
        float time = 0f;
        while (time < dur) {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / dur);

            Vector3 p = from.localPos;
            p.x = Mathf.Lerp(from.localPos.x, to.localPos.x, k);
            p.z = Mathf.Lerp(from.localPos.z, to.localPos.z, k);
            t.localPosition = p;

            t.localRotation = Quaternion.Slerp(from.localRot, to.localRot, k);

            yield return null;
        }
    }

    private void ApplyStepInstant(int step) {
        if (activeAnchor == null)
            return;

        step = Mathf.Clamp(step, 1, modules.Length);

        for (int i = 0; i < modules.Length; i++) {
            if (modules[i] == null)
                continue;

            if (i == step - 1) {
                modules[i].localPosition = activeAnchor.localPosition;
                modules[i].localRotation = activeAnchor.localRotation;
                continue;
            }

            ApplyPose(modules[i], inactivePoses[i]);
        }
    }

    private void ApplyAllInactiveInstant() {
        for (int i = 0; i < modules.Length; i++) {
            if (modules[i] == null)
                continue;

            ApplyPose(modules[i], inactivePoses[i]);
        }
    }

    private Pose GetCurrentPose(Transform t) {
        if (t == null)
            return new Pose();

        return new Pose { localPos = t.localPosition, localRot = t.localRotation };
    }

    private void ApplyPose(Transform t, Pose p) {
        if (t == null)
            return;

        t.localPosition = p.localPos;
        t.localRotation = p.localRot;
    }
}