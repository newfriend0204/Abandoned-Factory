using System.Collections;
using UnityEngine;

public class LockerInteractable : MonoBehaviour {
    [Header("Refs")]
    public GameManagerChap2 gameManager;
    public Outline outline;

    [Header("Animator & Audio")]
    public Animator lockerAnimator;
    public AudioSource audioSource;
    public AudioClip openClip;
    public AudioClip closeClip;

    [Header("Animation States")]
    public string openStateName = "Open";
    public string closeStateName = "Close";
    public string idleStateName = "Idle";

    [Header("Timing")]
    public float doorOpenDuration = 0.8f;
    public float doorCloseDuration = 0.8f;

    [Header("Interaction Settings")]
    public float interactDistance = 2.5f;
    public Collider targetCollider;

    [Header("Positions")]
    public Transform outsidePoint;
    public Transform insidePoint;

    [Header("QTE Door Hinge (Manual)")]
    public Transform qteDoorHinge;

    [Header("Detailed Motion Settings")]
    public float duckOffset = 0.2f;
    public float stepInRatio = 0.5f;

    [Space(10)]
    public float approachTime = 0.4f;
    public float stepInTime = 0.4f;
    public float turnTime = 0.3f;
    public float settleTime = 0.3f;
    public float pitchResetTime = 0.3f;
    public float exitTime = 0.6f;

    [Header("Restricted Mode Settings")]
    public float mouseSensitivityScale = 0.3f;
    public float yawLimit = 45f;

    private bool isInside = false;
    private bool isAnimating = false;

    public bool IsHidden => isInside && !isAnimating;

    private PlayerController playerController;
    private Camera playerCamera;
    private InputSettingsManager inputManager;
    private Vector3 originalCamLocalPos;
    private Quaternion originalCamLocalRot;

    private LockerQTEManager lockerQTE;
    private Chap2MonsterController monsterController;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        playerController = FindFirstObjectByType<PlayerController>();

        if (playerController != null) {
            playerCamera = playerController.playerCamera;
            if (playerCamera != null) {
                originalCamLocalPos = playerCamera.transform.localPosition;
                originalCamLocalRot = playerCamera.transform.localRotation;
            }
        }

        inputManager = InputSettingsManager.Instance;
        if (inputManager == null)
            inputManager = FindFirstObjectByType<InputSettingsManager>();

        lockerQTE = LockerQTEManager.Instance;
        if (lockerQTE == null)
            lockerQTE = FindFirstObjectByType<LockerQTEManager>();

        if (gameManager != null)
            monsterController = gameManager.Monster;

        if (monsterController == null)
            monsterController = FindFirstObjectByType<Chap2MonsterController>();

        if (outline != null)
            outline.enabled = false;

        if (audioSource == null) {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Update() {
        if (isAnimating || playerController == null) {
            if (outline != null)
                outline.enabled = false;
            return;
        }

        bool qteRunningForMe = lockerQTE != null && lockerQTE.IsRunningFor(this);
        bool exitLockForMe = lockerQTE != null && lockerQTE.ShouldBlockExitFor(this);

        if (isInside) {
            if (outline != null)
                outline.enabled = false;

            if (qteRunningForMe)
                return;

            if (exitLockForMe) {
                if (IsMonsterCompletelyGone())
                    lockerQTE.NotifyMonsterGoneForLocker(this);
                else
                    return;
            }

            if (gameManager != null)
                gameManager.Pressable(2);

            if (IsInteractDown())
                StartCoroutine(CoExitSequence());

            return;
        }

        bool canEnter = CheckInteractable();
        if (canEnter) {
            if (outline != null)
                outline.enabled = true;

            if (gameManager != null)
                gameManager.Pressable(1);

            if (IsInteractDown())
                StartCoroutine(CoEnterSequence());
        } else {
            if (outline != null)
                outline.enabled = false;
        }
    }

    private bool IsMonsterCompletelyGone() {
        if (monsterController == null)
            return true;

        return monsterController.IsCompletelyGone;
    }

    private bool IsInteractDown() {
        if (inputManager != null)
            return inputManager.GetKeyDown("Interact");

        return Input.GetKeyDown(KeyCode.F);
    }

    private bool CheckInteractable() {
        if (playerCamera == null || targetCollider == null)
            return false;

        float dist = Vector3.Distance(playerController.transform.position, targetCollider.transform.position);
        if (dist > interactDistance)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance + 1f))
            return hit.collider == targetCollider;

        return false;
    }

    private IEnumerator CoEnterSequence() {
        isAnimating = true;

        if (outline != null)
            outline.enabled = false;

        if (gameManager != null)
            gameManager.ReportPlayerHiding(this);

        playerController.enabled = false;

        Rigidbody rb = playerController.GetComponent<Rigidbody>();
        if (rb != null) {
            if (!rb.isKinematic)
                rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        PlayAnimation(openStateName);
        PlaySound(openClip);
        yield return new WaitForSeconds(doorOpenDuration * 0.6f);

        Transform rootTr = playerController.transform;

        Vector3 pStart = rootTr.position;
        Quaternion rStart = rootTr.rotation;

        Quaternion rOutside = outsidePoint.rotation;
        Vector3 pOutside = outsidePoint.position - (rOutside * originalCamLocalPos);

        Quaternion rInside = insidePoint.rotation;
        Vector3 pInside = insidePoint.position - (rInside * originalCamLocalPos);

        yield return StartCoroutine(MoveTransform(rootTr, pStart, pOutside, rStart, rOutside, approachTime));

        Vector3 pMid = Vector3.Lerp(pOutside, pInside, stepInRatio);
        pMid.y -= duckOffset;
        yield return StartCoroutine(MoveTransform(rootTr, pOutside, pMid, rOutside, rOutside, stepInTime));

        yield return StartCoroutine(MoveTransform(rootTr, pMid, pMid, rOutside, rInside, turnTime));
        yield return StartCoroutine(MoveTransform(rootTr, pMid, pInside, rInside, rInside, settleTime));

        if (playerCamera != null) {
            Quaternion startCamRot = playerCamera.transform.localRotation;
            Quaternion targetCamRot = Quaternion.Euler(10f, 0f, 0f);

            float t = 0f;
            while (t < pitchResetTime) {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / pitchResetTime);
                playerCamera.transform.localRotation = Quaternion.Slerp(startCamRot, targetCamRot, k);
                yield return null;
            }

            playerCamera.transform.localRotation = targetCamRot;
        }

        playerController.ResetCameraRotation(false);

        PlayAnimation(closeStateName);
        PlaySound(closeClip);
        yield return new WaitForSeconds(doorCloseDuration * 0.5f);

        playerController.enabled = true;
        playerController.SetRestrictedMode(
            moveLocked: true,
            bodyRot: false,
            lockX: false,
            lockY: true,
            sensMult: new Vector2(mouseSensitivityScale, mouseSensitivityScale),
            yawClamp: true,
            minY: -yawLimit,
            maxY: yawLimit,
            freezeRigidbodyPos: true,
            setKinematic: true
        );

        isInside = true;
        isAnimating = false;
    }

    private IEnumerator CoExitSequence() {
        bool qteRunningForMe = lockerQTE != null && lockerQTE.IsRunningFor(this);
        if (qteRunningForMe)
            yield break;

        bool exitLockForMe = lockerQTE != null && lockerQTE.ShouldBlockExitFor(this);
        if (exitLockForMe) {
            if (!IsMonsterCompletelyGone())
                yield break;

            lockerQTE.NotifyMonsterGoneForLocker(this);
        }

        isAnimating = true;
        playerController.enabled = false;

        if (gameManager != null)
            gameManager.ReportPlayerExiting();

        Transform rootTr = playerController.transform;

        Vector3 pCurrent = rootTr.position;
        Quaternion rCurrent = rootTr.rotation;

        Quaternion rOutside = outsidePoint.rotation;
        Vector3 pOutside = outsidePoint.position - (rOutside * originalCamLocalPos);
        Vector3 pInside = insidePoint.position - (insidePoint.rotation * originalCamLocalPos);

        Vector3 pLean = Vector3.Lerp(pInside, pOutside, 0.2f);

        yield return StartCoroutine(MoveTransform(rootTr, pCurrent, pLean, rCurrent, rCurrent, 0.2f));

        PlayAnimation(openStateName);
        PlaySound(openClip);
        yield return new WaitForSeconds(doorOpenDuration * 0.5f);

        yield return StartCoroutine(MoveTransform(rootTr, pLean, pOutside, rCurrent, rOutside, exitTime));

        playerController.ResetCameraRotation(false);

        Rigidbody rb = playerController.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = false;

        PlayAnimation(closeStateName);
        PlaySound(closeClip);

        playerController.enabled = true;
        playerController.SetRestrictedMode(
            moveLocked: false,
            bodyRot: true,
            lockX: false,
            lockY: false,
            sensMult: Vector2.one,
            yawClamp: false,
            minY: 0f,
            maxY: 0f,
            freezeRigidbodyPos: false,
            setKinematic: false
        );

        isInside = false;
        isAnimating = false;
    }

    private IEnumerator MoveTransform(Transform target, Vector3 startPos, Vector3 endPos, Quaternion startRot, Quaternion endRot, float duration) {
        float t = 0f;
        while (t < duration) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = k * k * (3f - 2f * k);

            target.position = Vector3.Lerp(startPos, endPos, k);
            target.rotation = Quaternion.Slerp(startRot, endRot, k);
            yield return null;
        }

        target.position = endPos;
        target.rotation = endRot;
    }

    private void PlayAnimation(string stateName) {
        if (lockerAnimator == null)
            return;

        lockerAnimator.Play(stateName);
    }

    private void PlaySound(AudioClip clip) {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }
}