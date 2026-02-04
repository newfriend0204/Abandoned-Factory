using UnityEngine;

[ExecuteAlways]
public class GlitchManager : MonoBehaviour {
    public enum StrengthMode {
        Manual,
        MonsterBased
    }

    [Header("Strength")]
    [SerializeField] StrengthMode strengthMode = StrengthMode.Manual;
    [Range(0f, 1f)][SerializeField] float strength = 0f;
    [Range(0f, 1f)][SerializeField] float computedStrength = 0f;

    public Camera PlayerCamera => playerCamera;

    [Header("Monster Sensor References")]
    [SerializeField] Camera playerCamera;
    [SerializeField] Transform monsterTarget;

    [Header("Monster Sensor - Target Offset")]
    [SerializeField] Vector3 monsterTargetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Monster Sensor - Distance (Far Tail Range)")]
    [SerializeField] float maxDistance = 20f;
    [SerializeField] float minDistance = 1.5f;

    [Header("Monster Sensor - Distance Tail")]
    [Range(0f, 1f)][SerializeField] float distanceTailMax = 0.12f;
    [Range(0.1f, 12f)][SerializeField] float distanceTailPower = 5f;

    [Header("Monster Sensor - Distance Spike")]
    [SerializeField] float distanceSpikeStart = 8f;
    [SerializeField] float distanceSpikeEnd = 4f;
    [Range(0.1f, 12f)][SerializeField] float distanceSpikePower = 4f;

    [Header("Monster Sensor - View (Screen Center)")]
    [InspectorName("Inner Center Radius (Viewport)")]
    [SerializeField] float innerFov = 0.08f;

    [InspectorName("Outer Center Radius (Viewport)")]
    [SerializeField] float outerFov = 0.35f;

    [Header("Monster Sensor - View Occlusion")]
    [SerializeField] bool useLineOfSightForView = true;
    [SerializeField] LayerMask occlusionMask = ~0;

    [Header("Monster Sensor - View Smoothing")]
    [Range(0.01f, 30f)][SerializeField] float viewRiseSpeed = 3f;
    [Range(0.01f, 30f)][SerializeField] float viewFallSpeed = 10f;

    [Header("Monster Sensor - Output")]
    [Range(0f, 1f)][SerializeField] float maxStrength = 1f;
    [Range(0.1f, 6f)][SerializeField] float responsePower = 1.6f;
    [Range(0.01f, 30f)][SerializeField] float followSpeed = 10f;
    [Range(0f, 0.2f)][SerializeField] float offThreshold = 0.01f;

    [Header("Monster Sensor - Mix")]
    [Range(0f, 2f)][SerializeField] float distanceInfluence = 1.0f;
    [Range(0f, 2f)][SerializeField] float viewInfluence = 0.35f;
    [Range(0.1f, 8f)][SerializeField] float viewCurvePower = 1.0f;

    [Header("Glitch Params (Inspector Sliders)")]
    [Range(0f, 1f)][SerializeField] float rgbSplit = 1f;
    [Range(0f, 1f)][SerializeField] float jitter = 0.8f;
    [Range(0f, 1f)][SerializeField] float scanline = 0.6f;
    [Min(0.01f)][SerializeField] float timeScale = 1f;

    [Header("Smear (Color Bleed / Spread)")]
    [Range(0f, 1f)][SerializeField] float smear = 0.8f;
    [Range(0f, 1f)][SerializeField] float smearRadius = 0.8f;

    [Header("Edge Boost (Stronger near screen edges)")]
    [Range(0f, 1f)][SerializeField] float edgeStart = 0.25f;
    [Range(0.5f, 8f)][SerializeField] float edgePower = 2.2f;
    [Range(0f, 8f)][SerializeField] float edgeSmearBoost = 4.0f;
    [Range(0f, 8f)][SerializeField] float edgeRadiusBoost = 3.0f;

    [Header("Edge Glow / Bleed (Outline-based)")]
    [Range(0f, 10f)][SerializeField] float edgeSensitivity = 4.0f;
    [Range(0f, 1f)][SerializeField] float edgeThreshold = 0.12f;
    [Range(0.001f, 1f)][SerializeField] float edgeSoftness = 0.20f;
    [Range(0f, 5f)][SerializeField] float glow = 1.8f;
    [Range(0f, 1f)][SerializeField] float bleed = 0.8f;

    [Header("Audio (Volume follows Strength)")]
    [SerializeField] AudioSource glitchAudioSource;
    [SerializeField] AudioClip glitchLoopClip;
    [Range(0f, 2f)][SerializeField] float volumeMultiplier = 1.0f;
    [Range(0.01f, 50f)][SerializeField] float volumeFollowSpeed = 12f;
    [Range(0f, 0.2f)][SerializeField] float stopThreshold = 0.01f;

    [Header("Editor")]
    [SerializeField] bool applyInEditMode = true;

    [Header("Runtime Control")]
    [SerializeField] bool suppressed = false;

    [Header("Monster Target Override (Runtime)")]
    [SerializeField] Transform monsterTargetOverride;
    [SerializeField] Vector3 monsterTargetOffsetOverride = new Vector3(0f, 1.5f, 0f);
    [SerializeField] bool overrideOffset = false;

    float currentVolume = 0f;
    float smoothedView01 = 0f;

    void OnValidate() {
        if (innerFov > 2f || outerFov > 2f) {
            innerFov = 0.08f;
            outerFov = 0.35f;
        }

        innerFov = Mathf.Clamp(innerFov, 0f, 0.75f);
        outerFov = Mathf.Clamp(outerFov, 0f, 0.75f);

        if (outerFov < innerFov)
            outerFov = innerFov;
    }

    public void OverrideMonsterTarget(Transform target) {
        monsterTargetOverride = target;
        overrideOffset = false;
    }

    public void OverrideMonsterTarget(Transform target, Vector3 offset) {
        monsterTargetOverride = target;
        monsterTargetOffsetOverride = offset;
        overrideOffset = true;
    }

    public void ClearMonsterTargetOverride() {
        monsterTargetOverride = null;
        overrideOffset = false;
    }

    Transform GetActiveMonsterTarget(out Vector3 offset) {
        if (monsterTargetOverride != null) {
            if (overrideOffset)
                offset = monsterTargetOffsetOverride;
            else
                offset = monsterTargetOffset;

            return monsterTargetOverride;
        }

        offset = monsterTargetOffset;
        return monsterTarget;
    }

    public void SetSuppressed(bool value) {
        suppressed = value;

        if (!suppressed)
            return;

        Apply(0f);
        StopAudio();
    }

    public bool IsSuppressed => suppressed;

    void Reset() {
        if (playerCamera == null)
            playerCamera = GetComponent<Camera>();
    }

    void Awake() {
        if (playerCamera == null)
            playerCamera = GetComponent<Camera>();

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void OnEnable() {
        float s = GetFinalStrength01();
        Apply(s);
        UpdateAudio(true, s);
    }

    void OnDisable() {
        Shader.SetGlobalFloat("_GlitchStrength", 0f);
        Shader.SetGlobalFloat("_GlitchUnscaledTime", 0f);
        smoothedView01 = 0f;
        StopAudio();
    }

    void Update() {
        if (!Application.isPlaying && !applyInEditMode)
            return;

        if (suppressed) {
            Apply(0f);
            UpdateAudio(false, 0f);
            return;
        }

        float s = GetFinalStrength01();
        Apply(s);
        UpdateAudio(false, s);
    }

    float GetFinalStrength01() {
        if (strengthMode == StrengthMode.Manual)
            return Mathf.Clamp01(strength);

        return ComputeMonsterStrength01();
    }

    float ComputeMonsterStrength01() {
        Vector3 offset;
        Transform activeTarget = GetActiveMonsterTarget(out offset);

        if (playerCamera == null || activeTarget == null) {
            computedStrength = 0f;
            smoothedView01 = 0f;
            return 0f;
        }

        Vector3 camPos = playerCamera.transform.position;
        Vector3 targetPos = activeTarget.position + offset;

        Vector3 to = targetPos - camPos;
        float dist = to.magnitude;

        float safeMin = Mathf.Max(0.01f, minDistance);
        float safeMax = Mathf.Max(safeMin + 0.01f, maxDistance);

        float baseLinear01 = 1f - Mathf.InverseLerp(safeMin, safeMax, dist);
        baseLinear01 = Mathf.Clamp01(baseLinear01);

        float tail01 = Mathf.Pow(baseLinear01, Mathf.Max(0.001f, distanceTailPower));
        tail01 *= Mathf.Clamp01(distanceTailMax);

        float spikeStart = Mathf.Max(distanceSpikeStart, distanceSpikeEnd + 0.01f);
        float spikeEnd = Mathf.Max(0.01f, distanceSpikeEnd);

        float spike01 = 1f - Mathf.InverseLerp(spikeEnd, spikeStart, dist);
        spike01 = Mathf.Clamp01(spike01);

        spike01 = spike01 * spike01 * (3f - 2f * spike01);
        spike01 = Mathf.Pow(spike01, Mathf.Max(0.001f, distanceSpikePower));

        float dist01 = Mathf.Max(tail01, spike01);

        float center01 = GetCenterProximity01(targetPos, dist, activeTarget);

        float viewTarget01 = Mathf.Pow(center01, Mathf.Max(0.001f, viewCurvePower));

        float rise = Mathf.Max(0.01f, viewRiseSpeed);
        float fall = Mathf.Max(0.01f, viewFallSpeed);

        float speed = viewTarget01 > smoothedView01 ? rise : fall;
        smoothedView01 = Mathf.MoveTowards(smoothedView01, viewTarget01, Time.unscaledDeltaTime * speed);

        float visible01 = smoothedView01;
        float raw = visible01 * (
            Mathf.Max(0f, viewInfluence) +
            (dist01 * Mathf.Max(0f, distanceInfluence))
        );

        raw = Mathf.Clamp01(raw) * Mathf.Clamp01(maxStrength);

        float target = Mathf.Pow(raw, Mathf.Max(0.001f, responsePower));

        computedStrength = Mathf.MoveTowards(computedStrength, target, Time.unscaledDeltaTime * followSpeed);

        if (computedStrength <= offThreshold)
            computedStrength = 0f;

        return computedStrength;
    }

    float GetCenterProximity01(Vector3 targetPos, float dist, Transform targetRoot) {
        Vector3 vp = playerCamera.WorldToViewportPoint(targetPos);

        if (vp.z <= 0f)
            return 0f;

        Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        float r = d.magnitude;

        float inner = Mathf.Clamp(innerFov, 0f, 0.75f);
        float outer = Mathf.Clamp(outerFov, 0f, 0.75f);

        if (outer < inner)
            outer = inner;

        float v = 1f - Mathf.InverseLerp(inner, outer, r);
        v = Mathf.Clamp01(v);

        if (useLineOfSightForView) {
            Vector3 origin = playerCamera.transform.position;
            if (!HasLineOfSight(origin, targetPos, dist, targetRoot))
                v = 0f;
        }

        return v;
    }

    bool HasLineOfSight(Vector3 origin, Vector3 targetPos, float dist, Transform targetRoot) {
        Vector3 dir = (targetPos - origin) / Mathf.Max(dist, 0.0001f);

        if (!Physics.Raycast(origin, dir, out RaycastHit hit, dist, occlusionMask, QueryTriggerInteraction.Ignore))
            return true;

        if (hit.transform == targetRoot)
            return true;

        return hit.transform.IsChildOf(targetRoot);
    }

    void Apply(float s) {
        Shader.SetGlobalFloat("_GlitchUnscaledTime", Time.realtimeSinceStartup);

        Shader.SetGlobalFloat("_GlitchStrength", Mathf.Clamp01(s));
        Shader.SetGlobalFloat("_GlitchRGBSplit", Mathf.Clamp01(rgbSplit));
        Shader.SetGlobalFloat("_GlitchJitter", Mathf.Clamp01(jitter));
        Shader.SetGlobalFloat("_GlitchScanline", Mathf.Clamp01(scanline));
        Shader.SetGlobalFloat("_GlitchTimeScale", Mathf.Max(0.01f, timeScale));

        Shader.SetGlobalFloat("_GlitchSmear", Mathf.Clamp01(smear));
        Shader.SetGlobalFloat("_GlitchSmearRadius", Mathf.Clamp01(smearRadius));

        Shader.SetGlobalFloat("_GlitchEdgeStart", Mathf.Clamp01(edgeStart));
        Shader.SetGlobalFloat("_GlitchEdgePower", Mathf.Max(0.5f, edgePower));
        Shader.SetGlobalFloat("_GlitchEdgeSmearBoost", Mathf.Max(0f, edgeSmearBoost));
        Shader.SetGlobalFloat("_GlitchEdgeRadiusBoost", Mathf.Max(0f, edgeRadiusBoost));

        Shader.SetGlobalFloat("_GlitchEdgeSensitivity", Mathf.Max(0f, edgeSensitivity));
        Shader.SetGlobalFloat("_GlitchEdgeThreshold", Mathf.Clamp01(edgeThreshold));
        Shader.SetGlobalFloat("_GlitchEdgeSoftness", Mathf.Clamp(edgeSoftness, 0.001f, 1f));
        Shader.SetGlobalFloat("_GlitchGlow", Mathf.Max(0f, glow));
        Shader.SetGlobalFloat("_GlitchBleed", Mathf.Clamp01(bleed));
    }

    void UpdateAudio(bool instant, float strength01) {
        if (glitchAudioSource == null)
            return;

        float target = Mathf.Clamp01(strength01) * Mathf.Max(0f, volumeMultiplier);

        if (instant)
            currentVolume = target;
        else
            currentVolume = Mathf.MoveTowards(currentVolume, target, Time.unscaledDeltaTime * volumeFollowSpeed);

        if (!Application.isPlaying) {
            glitchAudioSource.volume = currentVolume;
            return;
        }

        if (currentVolume <= stopThreshold) {
            StopAudio();
            return;
        }

        EnsureLoopPlaying();
        glitchAudioSource.volume = currentVolume;
    }

    void EnsureLoopPlaying() {
        if (glitchLoopClip != null && glitchAudioSource.clip != glitchLoopClip) {
            glitchAudioSource.clip = glitchLoopClip;
            glitchAudioSource.loop = true;
        }

        if (!glitchAudioSource.isPlaying)
            glitchAudioSource.Play();
    }

    void StopAudio() {
        if (glitchAudioSource == null)
            return;

        if (glitchAudioSource.isPlaying)
            glitchAudioSource.Stop();

        glitchAudioSource.volume = 0f;
        currentVolume = 0f;
    }
}