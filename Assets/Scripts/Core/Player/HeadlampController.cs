using System.Collections;
using UnityEngine;

public class HeadlampController : MonoBehaviour {
    public Light headlamp;
    public Transform followTarget;

    public Vector3 localOffset = new Vector3(0f, 0f, 0.12f);
    public Vector3 localEulerOffset = Vector3.zero;

    public KeyCode toggleKey = KeyCode.R;
    public bool startOn = false;

    [Header("Gameplay")]
    public bool canUseHeadlamp = false;

    public float onIntensity = 2.5f;
    public float fadeTime = 0.12f;

    public float noiseAmp = 0.06f;
    public float noiseSpeed = 2.0f;

    public float microDipAmp = 0.25f;
    public float microDipDuration = 0.07f;
    public Vector2 microDipInterval = new Vector2(2.5f, 6.0f);

    [Header("Follow Smoothing")]
    public bool smoothFollow = true;
    public float positionLag = 0.08f;
    public float rotationLag = 0.06f;

    [Header("Auto Dimmer")]
    public bool autoDimByDistance = true;
    public float dimNear = 0.6f;
    public float dimFar = 3.0f;
    public float dimMinFactor = 0.25f;
    public float widenAngleClose = 10f;

    [Header("Hit Probe")]
    public LayerMask occluderMask = ~0;
    public float probeRadius = 0.03f;

    public AudioSource audioSource;
    public AudioClip clickOn;
    public AudioClip clickOff;

    [Header("Input Lock")]
    public bool inputLocked = false;
    public bool forceOffWhileLocked = true;
    public bool restoreAfterUnlock = true;

    bool isOn;
    float fadeBase;
    Coroutine fading;

    bool savedStateValid;
    bool savedIsOn;

    float noiseSeed;
    float nextDipTime;
    bool dipActive;
    float dipT;

    float baseSpotAngle;
    float baseInnerSpotAngle;
    bool innerAngleSupported;

    bool followInitialized;
    Vector3 followPos;
    Quaternion followRot;

    public void SetInputLocked(bool locked) {
        if (inputLocked == locked)
            return;

        inputLocked = locked;

        if (inputLocked) {
            savedStateValid = true;
            savedIsOn = isOn;
            if (forceOffWhileLocked)
                EnsureOffSilently();
        } else {
            if (restoreAfterUnlock && savedStateValid && savedIsOn)
                EnsureOnSilently();
            savedStateValid = false;
        }
    }

    private void EnsureOffSilently() {
        if (!isOn && fadeBase <= 0.001f)
            return;

        isOn = false;
        if (fading != null)
            StopCoroutine(fading);
        fading = StartCoroutine(FadeBase(0f, fadeTime));
    }

    private void EnsureOnSilently() {
        if (isOn && fadeBase >= onIntensity - 0.001f)
            return;

        isOn = true;
        if (fading != null)
            StopCoroutine(fading);
        fading = StartCoroutine(FadeBase(onIntensity, fadeTime));
        ScheduleNextDip();
    }

    void Start() {
        isOn = startOn;
        fadeBase = isOn ? onIntensity : 0f;

        if (headlamp != null) {
            headlamp.enabled = fadeBase > 0.001f;
            headlamp.intensity = fadeBase;
            baseSpotAngle = headlamp.spotAngle;

            innerAngleSupported = true;
            baseInnerSpotAngle = headlamp.innerSpotAngle;
        }

        noiseSeed = Random.value * 100f;
        ScheduleNextDip();

        InitializeFollowState(true);
    }

    void Update() {
        if (headlamp == null || followTarget == null)
            return;

        if (!canUseHeadlamp) {
            InitializeFollowState(true);
            headlamp.enabled = false;
            headlamp.intensity = 0f;
            return;
        }

        if (!followInitialized)
            InitializeFollowState(true);

        UpdateFollowTransform(false);

        if (inputLocked) {
            if (forceOffWhileLocked)
                EnsureOffSilently();
        }

        if (!inputLocked && !Mathf.Approximately(Time.timeScale, 0f)) {
            bool togglePressed = false;

            var input = InputSettingsManager.Instance;
            if (input != null) {
                togglePressed = input.GetKeyDown("ToggleFlashlight");
            } else {
                togglePressed = Input.GetKeyDown(toggleKey);
            }

            if (togglePressed) {
                isOn = !isOn;
                PlayClick(isOn);
                if (fading != null)
                    StopCoroutine(fading);
                fading = StartCoroutine(FadeBase(isOn ? onIntensity : 0f, fadeTime));
                if (isOn)
                    ScheduleNextDip();
            }
        }

        float mod = 1f;

        if (autoDimByDistance) {
            float distFactor = 1f;
            float hitDist = Mathf.Infinity;

            RaycastHit hit;

            Vector3 o = headlamp.transform.position;
            Vector3 d = headlamp.transform.forward;

            float maxProbe = Mathf.Max(dimFar * 1.2f, 0.5f);

            if (Physics.SphereCast(o, probeRadius, d, out hit, maxProbe, occluderMask, QueryTriggerInteraction.Ignore))
                hitDist = hit.distance;

            distFactor = Mathf.InverseLerp(dimNear, dimFar, hitDist);
            mod *= Mathf.Lerp(dimMinFactor, 1f, distFactor);

            float extraAngle = (1f - distFactor) * Mathf.Max(0f, widenAngleClose);
            headlamp.spotAngle = baseSpotAngle + extraAngle;

            if (innerAngleSupported) {
                headlamp.innerSpotAngle = Mathf.Clamp(baseInnerSpotAngle + extraAngle * 0.5f, 0f, headlamp.spotAngle - 0.1f);
            } else {
                headlamp.spotAngle = baseSpotAngle;
                if (innerAngleSupported)
                    headlamp.innerSpotAngle = baseInnerSpotAngle;
            }

            if (isOn) {
                float n = Mathf.PerlinNoise(noiseSeed, Time.time * noiseSpeed) * 2f - 1f;
                mod *= Mathf.Clamp01(1f + n * noiseAmp);
            }

            if (isOn && Time.time >= nextDipTime && !dipActive) {
                dipActive = true;
                dipT = 0f;
            }
            if (dipActive) {
                dipT += Time.deltaTime / Mathf.Max(0.0001f, microDipDuration);
                float env = 1f - Mathf.Abs((dipT - 0.5f) * 2f);
                float dip = 1f - microDipAmp * env;
                mod *= Mathf.Clamp(dip, 0.2f, 1f);
                if (dipT >= 1f) {
                    dipActive = false;
                    ScheduleNextDip();
                }
            }

            float finalIntensity = fadeBase * mod;
            headlamp.intensity = finalIntensity;

            if (!isOn && finalIntensity <= 0.001f)
                headlamp.enabled = false;
            else if (isOn && !headlamp.enabled && finalIntensity > 0.001f)
                headlamp.enabled = true;
        }
    }

    void InitializeFollowState(bool snap) {
        if (headlamp == null || followTarget == null)
            return;

        Vector3 targetPos = followTarget.TransformPoint(localOffset);
        Quaternion targetRot = followTarget.rotation * Quaternion.Euler(localEulerOffset);

        followPos = targetPos;
        followRot = targetRot;
        headlamp.transform.SetPositionAndRotation(followPos, followRot);

        followInitialized = true;
    }

    void UpdateFollowTransform(bool snap) {
        Vector3 targetPos = followTarget.TransformPoint(localOffset);
        Quaternion targetRot = followTarget.rotation * Quaternion.Euler(localEulerOffset);

        if (!smoothFollow || snap) {
            followPos = targetPos;
            followRot = targetRot;
            headlamp.transform.SetPositionAndRotation(followPos, followRot);
            return;
        }

        float dt = Time.deltaTime;

        if (positionLag <= 0.0001f) {
            followPos = targetPos;
        } else {
            float kPos = 1f - Mathf.Exp(-dt / positionLag);
            followPos = Vector3.Lerp(followPos, targetPos, kPos);
        }

        if (rotationLag <= 0.0001f) {
            followRot = targetRot;
        } else {
            float kRot = 1f - Mathf.Exp(-dt / rotationLag);
            followRot = Quaternion.Slerp(followRot, targetRot, kRot);
        }

        headlamp.transform.SetPositionAndRotation(followPos, followRot);
    }

    IEnumerator FadeBase(float target, float time) {
        float start = fadeBase;
        float t = 0f;
        if (target > 0 && !headlamp.enabled)
            headlamp.enabled = true;

        while (t < 1f) {
            t += Time.deltaTime / Mathf.Max(0.0001f, time);
            float k = Mathf.SmoothStep(0f, 1f, t);
            fadeBase = Mathf.Lerp(start, target, k);
            yield return null;
        }
        fadeBase = target;
        if (fadeBase <= 0.001f)
            headlamp.enabled = false;
        fading = null;
    }

    void ScheduleNextDip() {
        nextDipTime = Time.time + Random.Range(microDipInterval.x, microDipInterval.y);
    }

    void PlayClick(bool on) {
        if (audioSource == null)
            return;

        var clip = on ? clickOn : clickOff;
        if (clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }
}