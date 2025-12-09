using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Chap1IntroSequence : MonoBehaviour {
    [Header("Required References")]
    public Camera mainCamera;
    public MonoBehaviour playerController;
    public Volume volume;
    public CanvasGroup fadeOverlay;

    [Header("Options")]
    public bool playIntroOnSceneStart = true;

    public static bool skipIntroOnce = false;

    [Header("Floor Position Setup")]
    public float floorDropHeight = 1.2f;
    public float floorForwardOffset = 0.1f;
    public float layRollAngle = 90f;

    [Header("Getting Up Motion")]
    public float moveToFloorDuration = 1.0f;
    public float unconsciousWaitDuration = 0.7f;
    public float headRaiseDuration = 1.8f;
    public float standUpToOvershootDuration = 1.0f;
    public float settleFromOvershootDuration = 0.35f;
    public float headRaiseHeight = 0.35f;
    public float headRaiseForwardFactor = 0.15f;

    [Header("Look Around Motion")]
    public float lookLeftDuration = 0.9f;
    public float lookRightDuration = 1.0f;
    public float lookCenterDuration = 0.6f;
    public float lookYawAngle = 40f;
    public float lookSideTilt = 4f;

    [Header("Vignette / Fade Effects")]
    public float initialVignetteIntensity = 1.0f;
    public float finalVignetteIntensity = 0.4f;
    public float vignetteStep1Target = 0.6f;
    public float vignetteStep2Target = 0.8f;
    public float vignetteStep3Target = 0.4f;
    public float vignetteStep1Duration = 0.4f;
    public float vignetteStep2Duration = 0.3f;
    public float vignetteStep3Duration = 0.6f;
    public float fadeOutDuration = 1.8f;

    [Header("Tutorial")]
    public TutorialHintUI tutorialUI;
    public float moveTutorialDuration = 5f;

    // === 새로 추가된 부분: 인트로 독백 설정 ===
    [Header("Intro Monologue")]
    public MonologueManager introMonologueManager;   // 비워두면 자동으로 씬에서 찾아봄
    [TextArea]
    public string introMessage;                      // 예: "여기는... 어디지?"
    public float introMessageDelay = 5f;             // 플레이어 조작 가능해진 뒤, 몇 초 후에 띄울지
    public bool introUseTypewriter = true;

    public bool overrideIntroVisibleDuration = false;
    public float introVisibleDuration = 3f;

    public bool overrideIntroTypewriterSpeed = false;
    public float introTypewriterCharsPerSecond = 5f;

    Vector3 originalCamPos;
    Quaternion originalCamRot;

    Vignette vignette;

    bool sequencePlayed = false;

    void Start() {
        bool shouldSkip = skipIntroOnce;
        if (skipIntroOnce)
            skipIntroOnce = false;

        // 이어하기로 들어오거나, 인트로를 사용하지 않는 경우
        if (!playIntroOnSceneStart || shouldSkip) {
            if (playerController != null)
                playerController.enabled = true;

            if (volume != null && volume.profile != null) {
                volume.profile.TryGet(out vignette);
            }

            if (vignette != null) {
                vignette.intensity.value = finalVignetteIntensity;
            }

            if (fadeOverlay != null) {
                fadeOverlay.alpha = 0f;
            }

            // 이 경우에는 인트로 연출도, 인트로 독백도 모두 스킵
            return;
        }

        // 처음부터 시작할 때만 인트로 연출 + 인트로 독백 실행
        if (playerController != null)
            playerController.enabled = false;

        originalCamPos = mainCamera.transform.position;
        originalCamRot = mainCamera.transform.rotation;

        if (volume != null && volume.profile != null) {
            volume.profile.TryGet(out vignette);
        }

        if (vignette != null) {
            vignette.intensity.value = initialVignetteIntensity;
        }

        if (fadeOverlay != null) {
            fadeOverlay.alpha = 1f;
        }

        if (!sequencePlayed) {
            sequencePlayed = true;
            StartCoroutine(PlaySequence());
        }
    }

    IEnumerator PlaySequence() {
        Transform cam = mainCamera.transform;

        Vector3 floorPos = originalCamPos
                           + cam.forward * floorForwardOffset
                           - Vector3.up * floorDropHeight;
        Quaternion floorRot = originalCamRot * Quaternion.Euler(0f, 0f, layRollAngle);

        yield return MoveCamera(cam, originalCamPos, floorPos, originalCamRot, floorRot, moveToFloorDuration);

        yield return new WaitForSeconds(unconsciousWaitDuration);

        Vector3 headRaisePos = Vector3.Lerp(floorPos, originalCamPos, 0.35f)
                               + Vector3.up * headRaiseHeight
                               + cam.forward * headRaiseForwardFactor;

        Quaternion headRaiseRot = Quaternion.Slerp(floorRot, originalCamRot, 0.3f)
                                   * Quaternion.Euler(-5f, 0f, -layRollAngle * 0.3f);

        yield return HeadRaiseWithFade(cam, floorPos, headRaisePos, floorRot, headRaiseRot, headRaiseDuration);

        if (vignette != null) {
            StartCoroutine(AnimateVignette());
        }

        Vector3 overshootPos = originalCamPos + Vector3.up * 0.08f;
        Quaternion overshootRot = originalCamRot * Quaternion.Euler(-3f, 0f, 0f);

        yield return MoveCamera(cam, headRaisePos, overshootPos, headRaiseRot, overshootRot, standUpToOvershootDuration);
        yield return MoveCamera(cam, overshootPos, originalCamPos, overshootRot, originalCamRot, settleFromOvershootDuration);

        yield return LookAround(cam, originalCamRot);

        if (playerController != null)
            playerController.enabled = true;

        if (tutorialUI != null)
            tutorialUI.ShowTutorial(0, moveTutorialDuration);

        // 인트로 연출이 끝난 뒤, 플레이어가 움직일 수 있는 상태에서
        // 5초(기본값) 기다렸다가 독백 출력
        StartCoroutine(PlayIntroMonologue());
    }

    IEnumerator PlayIntroMonologue() {
        // 메시지가 비어 있으면 아무 것도 하지 않음
        if (string.IsNullOrWhiteSpace(introMessage))
            yield break;

        MonologueManager mgr = introMonologueManager;
        if (mgr == null)
            mgr = FindFirstObjectByType<MonologueManager>();

        if (mgr == null)
            yield break;

        if (introMessageDelay > 0f)
            yield return new WaitForSeconds(introMessageDelay);

        // 표시 시간 결정
        float visibleDuration = mgr.defaultVisibleDuration;
        if (overrideIntroVisibleDuration)
            visibleDuration = Mathf.Max(0f, introVisibleDuration);

        // 타자기 속도 임시 변경
        bool changedSpeed = false;
        float originalSpeed = mgr.typewriterCharsPerSecond;

        if (overrideIntroTypewriterSpeed && introUseTypewriter && introTypewriterCharsPerSecond > 0f) {
            mgr.typewriterCharsPerSecond = introTypewriterCharsPerSecond;
            changedSpeed = true;
        }

        mgr.ShowMessage(introMessage, visibleDuration, introUseTypewriter);

        // 원래 속도로 복원
        if (changedSpeed)
            mgr.typewriterCharsPerSecond = originalSpeed;
    }

    IEnumerator MoveCamera(Transform cam,
                           Vector3 fromPos, Vector3 toPos,
                           Quaternion fromRot, Quaternion toRot,
                           float duration) {
        if (duration <= 0f) {
            cam.position = toPos;
            cam.rotation = toRot;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInOutCubic(t);

            cam.position = Vector3.Lerp(fromPos, toPos, eased);
            cam.rotation = Quaternion.Slerp(fromRot, toRot, eased);

            yield return null;
        }

        cam.position = toPos;
        cam.rotation = toRot;
    }

    IEnumerator HeadRaiseWithFade(Transform cam,
                                  Vector3 fromPos, Vector3 toPos,
                                  Quaternion fromRot, Quaternion toRot,
                                  float duration) {
        if (duration <= 0f) {
            cam.position = toPos;
            cam.rotation = toRot;
            if (fadeOverlay != null) fadeOverlay.alpha = 0f;
            yield break;
        }

        float elapsed = 0f;
        float fadeDurationLocal = Mathf.Max(fadeOutDuration, 0.01f);

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);

            cam.position = Vector3.Lerp(fromPos, toPos, eased);
            cam.rotation = Quaternion.Slerp(fromRot, toRot, eased);

            if (fadeOverlay != null) {
                float fadeT = Mathf.Clamp01(elapsed / fadeDurationLocal);
                fadeOverlay.alpha = Mathf.Lerp(1f, 0f, fadeT);
            }

            yield return null;
        }

        cam.position = toPos;
        cam.rotation = toRot;

        if (fadeOverlay != null) {
            fadeOverlay.alpha = 0f;
        }
    }

    IEnumerator LookAround(Transform cam, Quaternion centerRot) {
        Quaternion leftRot = centerRot * Quaternion.Euler(0f, -lookYawAngle, lookSideTilt);
        Quaternion rightRot = centerRot * Quaternion.Euler(0f, lookYawAngle, -lookSideTilt);

        yield return RotateCamera(cam, centerRot, leftRot, lookLeftDuration);
        yield return RotateCamera(cam, leftRot, rightRot, lookRightDuration);
        yield return RotateCamera(cam, rightRot, centerRot, lookCenterDuration);
    }

    IEnumerator RotateCamera(Transform cam,
                             Quaternion fromRot, Quaternion toRot,
                             float duration) {
        if (duration <= 0f) {
            cam.rotation = toRot;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInOutCubic(t);

            cam.rotation = Quaternion.Slerp(fromRot, toRot, eased);
            yield return null;
        }

        cam.rotation = toRot;
    }

    IEnumerator AnimateVignette() {
        // Step 1
        yield return AnimateVignetteIntensity(vignette.intensity.value, vignetteStep1Target, vignetteStep1Duration);
        // Step 2
        yield return AnimateVignetteIntensity(vignetteStep1Target, vignetteStep2Target, vignetteStep2Duration);
        // Step 3
        yield return AnimateVignetteIntensity(vignetteStep2Target, vignetteStep3Target, vignetteStep3Duration);
    }

    IEnumerator AnimateVignetteIntensity(float from, float to, float duration) {
        if (vignette == null || duration <= 0f) {
            if (vignette != null)
                vignette.intensity.value = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInOutCubic(t);

            vignette.intensity.value = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        vignette.intensity.value = to;
    }

    float EaseInOutCubic(float t) {
        if (t < 0.5f)
            return 4f * t * t * t;
        t -= 1f;
        return 1f + 4f * t * t * t;
    }

    float EaseOutCubic(float t) {
        t -= 1f;
        return 1f + t * t * t;
    }
}