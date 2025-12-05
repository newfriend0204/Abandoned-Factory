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

    [Header("Camera (마우스 좌우 회전 + 충돌 쉐이크)")]
    public Transform cameraRoot;
    public float shakeIntensity = 0.5f;

    [Header("Camera Look (Mouse X)")]
    public float lookSensitivity = 80f;
    public float maxYawAngle = 15f;

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

    [Header("Engine Sound")]
    public AudioSource engineAudioSource;
    public AudioClip engineLoopClip;
    [Range(0f, 1f)]
    public float engineVolume = 0.6f;

    private Vignette vignette;
    private Chap0State state = Chap0State.DrivingDialogue;
    private bool sequenceStarted = false;
    private bool crashed = false;

    private Vector3 cameraBaseLocalPos;
    private Quaternion cameraBaseLocalRot;
    private float currentYaw = 0f;

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

        if (cameraRoot != null && carRoot != null) {
            cameraRoot.SetParent(carRoot, true);
            cameraBaseLocalPos = cameraRoot.localPosition;
            cameraBaseLocalRot = cameraRoot.localRotation;
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
    }

    private void Start() {
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

        UpdateCameraLook();
    }

    private void MoveCarForward() {
        if (carRoot == null)
            return;

        Vector3 dir = driveInLocalZ ? carRoot.forward : Vector3.forward;
        carRoot.position += dir * driveSpeed * Time.deltaTime;
    }

    private void UpdateCameraLook() {
        if (cameraRoot == null)
            return;

        float mouseX = Input.GetAxis("Mouse X");
        float deltaYaw = mouseX * lookSensitivity * Time.deltaTime;
        currentYaw += deltaYaw;

        currentYaw = Mathf.Clamp(currentYaw, -maxYawAngle, maxYawAngle);

        Quaternion yawRot = Quaternion.Euler(0f, currentYaw, 0f);
        cameraRoot.localRotation = cameraBaseLocalRot * yawRot;
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

    private IEnumerator CoCameraShake(float duration, float intensity) {
        if (cameraRoot == null)
            yield break;

        Vector3 origin = cameraBaseLocalPos;
        float elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float currentIntensity = Mathf.Lerp(intensity, 0f, t);
            float offsetX = (Random.value * 2f - 1f) * currentIntensity;
            float offsetY = (Random.value * 2f - 1f) * currentIntensity;

            cameraRoot.localPosition = origin + new Vector3(offsetX, offsetY, 0f);

            yield return null;
        }

        cameraRoot.localPosition = origin;
    }

    private void TriggerCrash() {
        if (crashed)
            return;
        crashed = true;
        state = Chap0State.Crashed;

        if (vignette != null) {
            vignette.intensity.Override(0f);
        }

        if (engineAudioSource != null && engineAudioSource.isPlaying) {
            engineAudioSource.Stop();
        }

        if (carRoot != null) {
            var meshColliders = carRoot.GetComponentsInChildren<MeshCollider>();
            for (int i = 0; i < meshColliders.Length; i++) {
                if (meshColliders[i] != null) {
                    meshColliders[i].convex = true;
                }
            }
        }

        if (carRigidbody != null) {
            carRigidbody.isKinematic = false;

            Vector3 forward = (carRoot != null) ? carRoot.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 force = forward * crashForwardForce + Vector3.up * crashUpForce;
            carRigidbody.AddForce(force, ForceMode.Impulse);

            Vector3 randomAxis = Random.onUnitSphere;
            carRigidbody.AddTorque(randomAxis * crashTorqueForce, ForceMode.Impulse);
        }

        if (sfxAudioSource != null) {
            if (crashInstantSfx != null) {
                sfxAudioSource.PlayOneShot(crashInstantSfx);
            }
            if (wheelSpinSfx != null) {
                sfxAudioSource.PlayOneShot(wheelSpinSfx);
            }
        }

        StartCoroutine(CoCrashAftermath());
    }

    private IEnumerator CoCrashAftermath() {
        if (cameraRoot != null && timeBeforeFade > 0f && shakeIntensity > 0f) {
            StartCoroutine(CoCameraShake(timeBeforeFade, shakeIntensity));
        }

        if (timeBeforeFade > 0f)
            yield return new WaitForSeconds(timeBeforeFade);

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
}