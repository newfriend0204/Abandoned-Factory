using System.Collections;
using UnityEngine;

public class HeadlampController : MonoBehaviour {
    public Light headlamp;
    public Transform followTarget;

    public Vector3 localOffset = new Vector3(0f, 0f, 0.12f);
    public Vector3 localEulerOffset = Vector3.zero;

    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = false;

    public float onIntensity = 2.5f;
    public float fadeTime = 0.12f;

    public float noiseAmp = 0.06f;
    public float noiseSpeed = 2.0f;

    public float microDipAmp = 0.25f;
    public float microDipDuration = 0.07f;
    public Vector2 microDipInterval = new Vector2(2.5f, 6.0f);

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

    bool isOn;
    float fadeBase;
    Coroutine fading;

    float noiseSeed;
    float nextDipTime;
    bool dipActive;
    float dipT;

    float baseSpotAngle;
    float baseInnerSpotAngle;
    bool innerAngleSupported;

    void Start() {
        isOn = startOn;
        fadeBase = isOn ? onIntensity : 0f;

        headlamp.enabled = fadeBase > 0.001f;
        headlamp.intensity = fadeBase;
        baseSpotAngle = headlamp.spotAngle;

        innerAngleSupported = true;
        baseInnerSpotAngle = headlamp.innerSpotAngle;

        noiseSeed = Random.value * 100f;
        ScheduleNextDip();
    }

    void Update() {
        if (Input.GetKeyDown(toggleKey)) {
            isOn = !isOn;
            PlayClick(isOn);
            if (fading != null)
                StopCoroutine(fading);
            fading = StartCoroutine(FadeBase(isOn ? onIntensity : 0f, fadeTime));
            if (isOn)
                ScheduleNextDip();
        }

        headlamp.transform.SetPositionAndRotation(
            followTarget.TransformPoint(localOffset),
            followTarget.rotation * Quaternion.Euler(localEulerOffset)
        );

        float mod = 1f;

        if (autoDimByDistance && followTarget != null) {
            float distFactor = 1f;
            float hitDist = Mathf.Infinity;

            RaycastHit hit;
            Vector3 o = followTarget.position;
            Vector3 d = followTarget.forward;
            float maxProbe = Mathf.Max(dimFar * 1.2f, 0.5f);

            if (Physics.SphereCast(o, probeRadius, d, out hit, maxProbe, occluderMask, QueryTriggerInteraction.Ignore)) {
                hitDist = hit.distance;
            }

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
                dipActive = true; dipT = 0f;
            }
            if (dipActive) {
                dipT += Time.deltaTime / Mathf.Max(0.0001f, microDipDuration);
                float env = 1f - Mathf.Abs((dipT - 0.5f) * 2f);
                float dip = 1f - microDipAmp * env;
                mod *= Mathf.Clamp(dip, 0.2f, 1f);
                if (dipT >= 1f) { dipActive = false; ScheduleNextDip(); }
            }

            float finalIntensity = fadeBase * mod;
            headlamp.intensity = finalIntensity;

            if (!isOn && finalIntensity <= 0.001f)
                headlamp.enabled = false;
            else if (isOn && !headlamp.enabled && finalIntensity > 0.001f)
                headlamp.enabled = true;
        }
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
        var clip = on ? clickOn : clickOff;
        audioSource.PlayOneShot(clip);
    }
}