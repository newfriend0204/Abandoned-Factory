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
    [SerializeField] private GameManagerChap2 gameManager;

    [Header("Modules (1~7)")]
    [SerializeField] private Transform[] modules = new Transform[7];

    [Header("Anchors")]
    [SerializeField] private Transform activeAnchor;
    [SerializeField] private Transform inactiveAnchor;

    [Header("Motion")]
    [SerializeField] private float moveDurationPerModule = 0.9f;
    [SerializeField] private float swapCooldownSeconds = 1.0f;

    [Header("Enter YSequence")]
    [SerializeField] private bool animateEnterFromInactive = true;

    private Pose[] inactivePoses;
    private Coroutine swapRoutine;

    private int lastStateInt = int.MinValue;
    private bool initialized;

    private void Awake() {
        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        inactivePoses = new Pose[modules.Length];
        CacheInactivePoses();
    }

    private void OnEnable() {
        TryBind();
        CacheInactivePoses();
        StartCoroutine(CoRefreshNextFrame());
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

        initialized = false;
        lastStateInt = int.MinValue;
    }

    private IEnumerator CoRefreshNextFrame() {
        yield return null;
        ForceRefreshByState(false, true);
        initialized = true;
    }

    private void Update() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        int stateInt = gameManager != null ? (int)gameManager.State : 0;
        if (stateInt == lastStateInt)
            return;

        bool wasYSequence = lastStateInt == (int)GameManagerChap2.Chap2State.YSequence;
        lastStateInt = stateInt;

        ForceRefreshByState(wasYSequence, false);
    }

    private void ForceRefreshByState(bool wasYSequence, bool forceSnap) {
        bool isYSequence = gameManager != null && gameManager.State == GameManagerChap2.Chap2State.YSequence;

        if (!isYSequence) {
            StopSwapIfNeeded();
            CacheInactivePoses();
            ApplyAllInactiveInstant();
            return;
        }

        StopSwapIfNeeded();
        CacheInactivePoses();

        int step = GetSafeStep();

        if (forceSnap || !initialized || !animateEnterFromInactive) {
            ApplyStepInstant(step);
            return;
        }

        if (!wasYSequence) {
            swapRoutine = StartCoroutine(CoEnterStepFromInactive(step));
            return;
        }

        ApplyStepInstant(step);
    }

    private void StopSwapIfNeeded() {
        if (swapRoutine == null)
            return;

        StopCoroutine(swapRoutine);
        swapRoutine = null;
        IsSwapping = false;
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
        if (gameManager != null && gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return;

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

    private IEnumerator CoEnterStepFromInactive(int step) {
        if (activeAnchor == null) {
            ApplyStepInstant(step);
            yield break;
        }

        IsSwapping = true;

        step = Mathf.Clamp(step, 1, modules.Length);
        int idx = step - 1;

        Transform module = modules[idx];
        if (module == null) {
            swapRoutine = null;
            IsSwapping = false;
            yield break;
        }

        for (int i = 0; i < modules.Length; i++) {
            if (modules[i] == null)
                continue;

            if (i == idx)
                continue;

            ApplyPose(modules[i], inactivePoses[i]);
        }

        ApplyPose(module, inactivePoses[idx]);

        Pose from = GetCurrentPose(module);
        Pose to = new Pose { localPos = activeAnchor.localPosition, localRot = activeAnchor.localRotation };

        float dur = Mathf.Max(0.01f, moveDurationPerModule);
        float half = Mathf.Max(0.01f, dur * 0.5f);

        yield return MoveYOnly(module, from, to, half);
        yield return MoveXZOnly(module, GetCurrentPose(module), to, half);

        ApplyPose(module, to);

        swapRoutine = null;
        IsSwapping = false;
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