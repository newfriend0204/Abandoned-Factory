using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GameManagerChap0 : MonoBehaviour {
    private enum Chap0State {
        DrivingDialogue,
        Drowsy,
        Crashed,
        Finished
    }

    [Header("Car")]
    public Transform carRoot;
    public Rigidbody carRigidbody;
    public float driveSpeed = 5f;
    public bool driveInLocalZ = true;

    [Header("Hand Animator")]
    public Animator handAnimator;
    private int hashIsDriving;

    [Header("Monologue")]
    public MonologueManager monologue;

    [TextArea]
    public string line1;
    [TextArea]
    public string line2;
    [TextArea]
    public string line3;

    public float dialogueStartDelay = 0.5f;
    public float extraWaitAfterLine = 0.5f;

    [Header("Drowsy Vignette")]
    public Volume globalVolume;
    public float baseIntensity = 0.4f;
    public float intensityStep1 = 0.9f;
    public float intensityStep2 = 0.6f;
    public float intensityStep3 = 1.0f;

    public float step1Duration = 2.0f;
    public float step2Duration = 2.0f;
    public float step3Duration = 2.0f;
    public float holdAtStep3 = 0.5f;

    [Header("Look Control Settings")]
    public float maxLookAngle = 45f;
    public float lookSensitivityScale = 0.5f;

    [Header("Crash Physics")]
    public float crashForwardForce = 8f;
    public float crashUpForce = 5f;
    public float crashTorqueForce = 5f;

    [Header("Fade & SFX After Crash")]
    public CanvasGroup fadeCanvasGroup;
    public float timeBeforeFade = 1.0f;
    public float extraDelayBeforeCrashSfx = 0.3f;
    public AudioSource sfxAudioSource;
    public AudioClip crashInstantSfx;
    public AudioClip wheelSpinSfx;
    public AudioClip crashDelayedSfx;

    [Header("Camera Shake (Post-Crash)")]
    public float shakeIntensity = 0.5f;

    [Header("Engine Sound")]
    public AudioSource engineAudioSource;
    public AudioClip engineLoopClip;
    [Range(0f, 1f)]
    public float engineVolume = 0.6f;

    private Vignette vignette;
    private Chap0State state = Chap0State.DrivingDialogue;
    private bool sequenceStarted = false;
    private bool crashed = false;

    private PlayerController playerController;

    private void Awake() {
        hashIsDriving = Animator.StringToHash("IsDriving");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (globalVolume != null && globalVolume.profile != null) {
            globalVolume.profile.TryGet(out vignette);
            if (vignette != null) {
                vignette.intensity.Override(baseIntensity);
            }
        }

        if (carRoot != null && carRigidbody == null) {
            carRigidbody = carRoot.GetComponent<Rigidbody>();
        }

        if (carRigidbody != null) {
            carRigidbody.isKinematic = true;
        }

        if (fadeCanvasGroup != null) {
            fadeCanvasGroup.alpha = 0f;
        }

        if (handAnimator != null) {
            handAnimator.SetBool(hashIsDriving, true);
        }

        if (engineAudioSource != null && engineLoopClip != null) {
            engineAudioSource.clip = engineLoopClip;
            engineAudioSource.loop = true;
            engineAudioSource.volume = engineVolume;
        }

        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null) {
            playerController.transform.SetParent(carRoot, true);

            playerController.SetRestrictedMode(
                moveLocked: true,
                bodyRot: false,
                lockX: false,
                lockY: true,
                sensMult: new Vector2(lookSensitivityScale, lookSensitivityScale),
                yawClamp: true,
                minY: -maxLookAngle,
                maxY: maxLookAngle,
                freezeRigidbodyPos: false,
                setKinematic: true
            );
        }
    }

    private void Start() {
        if (playerController != null) {
            playerController.SetOverrideBaseFOV(85f);
        }

        state = Chap0State.DrivingDialogue;
        sequenceStarted = true;

        if (engineAudioSource != null && engineAudioSource.clip != null) {
            engineAudioSource.Play();
        }

        StartCoroutine(CoSequence());
    }

    private void Update() {
        if (!sequenceStarted)
            return;

        if (state == Chap0State.DrivingDialogue || state == Chap0State.Drowsy) {
            MoveCarForward();
        }
    }

    private void MoveCarForward() {
        if (carRoot == null)
            return;

        Vector3 dir = driveInLocalZ ? carRoot.forward : Vector3.forward;
        carRoot.position += dir * driveSpeed * Time.deltaTime;
    }

    private IEnumerator CoSequence() {
        if (dialogueStartDelay > 0f)
            yield return new WaitForSeconds(dialogueStartDelay);

        yield return StartCoroutine(CoPlayDialogues());

        state = Chap0State.Drowsy;
        yield return StartCoroutine(CoDrowsyVignette());

        TriggerCrash();
        state = Chap0State.Finished;
    }

    private IEnumerator CoPlayDialogues() {
        if (monologue == null)
            yield break;

        if (!string.IsNullOrWhiteSpace(line1)) {
            monologue.ShowMessage(line1, monologue.defaultVisibleDuration, false);
            yield return new WaitForSeconds(monologue.defaultVisibleDuration + extraWaitAfterLine);
        }

        if (!string.IsNullOrWhiteSpace(line2)) {
            monologue.ShowMessage(line2, monologue.defaultVisibleDuration, false);
            yield return new WaitForSeconds(monologue.defaultVisibleDuration + extraWaitAfterLine);
        }

        if (!string.IsNullOrWhiteSpace(line3)) {
            monologue.ShowMessage(line3, monologue.defaultVisibleDuration, false);
            yield return new WaitForSeconds(monologue.defaultVisibleDuration + extraWaitAfterLine);
        }
    }

    private IEnumerator CoDrowsyVignette() {
        if (vignette == null)
            yield break;

        yield return StartCoroutine(CoLerpVignette(baseIntensity, intensityStep1, step1Duration));
        yield return StartCoroutine(CoLerpVignette(intensityStep1, intensityStep2, step2Duration));
        yield return StartCoroutine(CoLerpVignette(intensityStep2, intensityStep3, step3Duration));

        if (holdAtStep3 > 0f)
            yield return new WaitForSeconds(holdAtStep3);
    }

    private IEnumerator CoLerpVignette(float from, float to, float duration) {
        if (vignette == null)
            yield break;

        duration = Mathf.Max(0.0001f, duration);
        float t = 0f;

        while (t < duration) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = k * k * (3f - 2f * k);

            float val = Mathf.Lerp(from, to, e);
            vignette.intensity.Override(val);

            yield return null;
        }

        vignette.intensity.Override(to);
    }

    private void TriggerCrash() {
        if (crashed) return;
        crashed = true;
        state = Chap0State.Crashed;

        if (playerController != null) {
            playerController.enabled = false;

            var pcCol = playerController.GetComponent<Collider>();
            if (pcCol != null) pcCol.enabled = false;
        }

        if (vignette != null) vignette.intensity.Override(0f);
        if (engineAudioSource != null && engineAudioSource.isPlaying) engineAudioSource.Stop();

        if (carRoot != null) {
            var meshColliders = carRoot.GetComponentsInChildren<MeshCollider>();
            for (int i = 0; i < meshColliders.Length; i++) {
                if (meshColliders[i] != null) meshColliders[i].convex = true;
            }
        }

        if (carRigidbody != null) {
            carRigidbody.isKinematic = false;

            Vector3 forward = (carRoot != null) ? carRoot.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 force = forward * crashForwardForce + Vector3.up * crashUpForce;
            carRigidbody.AddForce(force, ForceMode.VelocityChange);

            Vector3 randomAxis = Random.onUnitSphere;
            carRigidbody.AddTorque(randomAxis * crashTorqueForce, ForceMode.Impulse);
        }

        if (sfxAudioSource != null) {
            if (crashInstantSfx != null) sfxAudioSource.PlayOneShot(crashInstantSfx);
            if (wheelSpinSfx != null) sfxAudioSource.PlayOneShot(wheelSpinSfx);
        }

        StartCoroutine(CoCrashAftermath());
    }

    private IEnumerator CoCrashAftermath() {
        if (playerController != null && timeBeforeFade > 0f && shakeIntensity > 0f) {
            yield return StartCoroutine(CoCameraShake(playerController.playerCamera.transform, timeBeforeFade, shakeIntensity));
        } else if (timeBeforeFade > 0f) {
            yield return new WaitForSeconds(timeBeforeFade);
        }

        if (fadeCanvasGroup != null) {
            fadeCanvasGroup.alpha = 1f;
        }

        if (extraDelayBeforeCrashSfx > 0f)
            yield return new WaitForSeconds(extraDelayBeforeCrashSfx);

        if (sfxAudioSource != null && crashDelayedSfx != null) {
            sfxAudioSource.Stop();
            sfxAudioSource.PlayOneShot(crashDelayedSfx);
        }

        yield return new WaitForSeconds(5f);
        SceneManager.LoadScene("Chap1");
    }

    private IEnumerator CoCameraShake(Transform target, float duration, float intensity) {
        if (target == null)
            yield break;

        Vector3 origin = target.localPosition;
        float elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float currentIntensity = Mathf.Lerp(intensity, 0f, t);
            float offsetX = (Random.value * 2f - 1f) * currentIntensity;
            float offsetY = (Random.value * 2f - 1f) * currentIntensity;

            target.localPosition = origin + new Vector3(offsetX, offsetY, 0f);

            yield return null;
        }

        target.localPosition = origin;
    }
}