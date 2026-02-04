using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class InteractionStation : MonoBehaviour {
    [Header("Station")]
    [Range(1, 7)]
    public int stepIndex = 1;

    [Header("Refs")]
    public Outline outline;
    public Collider targetCollider;

    [Header("Outline")]
    public bool outlineWhenNearby = false;
    public float outlineNearbyDistance = 7.0f;

    [Header("View")]
    public Transform cameraTargetPoint;

    [Header("Interaction")]
    public float interactDistance = 2.5f;
    public float elementRayDistance = 3.0f;

    [Header("Camera Motion")]
    public float enterMoveTime = 0.5f;
    public float exitMoveTime = 0.5f;

    [Header("Restricted Mode")]
    public Vector2 lookSensitivityMultiplier = Vector2.zero;

    [Header("Physics")]
    public bool freezePlayerPhysicsDuringInteraction = true;

    [Header("Options")]
    public bool allowHeadlampToggleDuringInteraction = true;

    [Header("Pressable Mode")]
    public InteractableElement stationPressableSource;
    public int stationPressableFallbackMode = 0;

    [Header("Optional: Pressable UI Hook")]
    public UnityEvent<int> onPressableMode;

    [Header("Optional: Chap2 Gating")]
    public bool blockWhileCenterModuleSwapping = false;
    public bool requireSequenceStepMatch = false;
    public Chap2YStepSequenceManager sequenceManager;

    [Header("Optional: Hint UI")]
    public bool showExitHint = true;
    public bool showTypingHintOnStep1 = true;

    private PlayerController playerController;
    private Camera playerCamera;
    private InputSettingsManager input;
    private TutorialHintUI tutorialHint;
    private GameSettingsApplier settingsApplier;

    private Vector3 savedPlayerPos;
    private Quaternion savedPlayerRot;

    private bool savedLockCursor;

    private struct RestrictSnapshot {
        public bool isMovementLocked;
        public bool useBodyRotation;
        public bool lockLookX;
        public bool lockLookY;
        public Vector2 sensMult;
        public bool yawClamp;
        public float minY;
        public float maxY;
        public bool freezePos;
        public bool kinematic;
        public bool headlampLocked;
    }

    private RestrictSnapshot savedRestrict;

    private bool isInInteractionMode;
    private Coroutine moveRoutine;

    private InteractableElement hoveredElement;
    private InteractableElement pressedElement;

    private static InteractionStation currentActive;

    private bool savedTargetColliderEnabled;
    private bool disabledTargetColliderForInteraction;

    private void Awake() {
        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
            playerCamera = playerController.playerCamera;

        input = InputSettingsManager.Instance;
        if (input == null)
            input = FindFirstObjectByType<InputSettingsManager>();

        tutorialHint = FindFirstObjectByType<TutorialHintUI>();

        settingsApplier = GameSettingsApplier.Instance;
        if (settingsApplier == null)
            settingsApplier = FindFirstObjectByType<GameSettingsApplier>();

        if (outline != null)
            outline.enabled = false;

        ResolveSequenceManagerIfNeeded();

        if (stationPressableSource == null && targetCollider != null)
            stationPressableSource = targetCollider.GetComponentInParent<InteractableElement>();
    }

    private void ResolveSequenceManagerIfNeeded() {
        if (!requireSequenceStepMatch)
            return;

        if (sequenceManager != null)
            return;

        sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();
    }

    private void Update() {
        if (Time.timeScale == 0f)
            return;

        if (isInInteractionMode) {
            UpdateInteractionMode();
            return;
        }

        UpdateIdleInteract();
    }

    private void OnDisable() {
        if (currentActive == this)
            currentActive = null;

        if (InteractionModeService.IsInInteractionMode)
            InteractionModeService.SetInteractionMode(false);

        if (disabledTargetColliderForInteraction && targetCollider != null)
            targetCollider.enabled = savedTargetColliderEnabled;

        if (isInInteractionMode)
            ApplyCrosshairFromSettings();

        disabledTargetColliderForInteraction = false;
    }

    private void UpdateIdleInteract() {
        if (blockWhileCenterModuleSwapping && Chap2CenterModuleController.IsSwapping) {
            if (outline != null)
                outline.enabled = false;
            return;
        }

        if (playerController == null || playerCamera == null || targetCollider == null || cameraTargetPoint == null) {
            if (outline != null)
                outline.enabled = false;
            return;
        }

        if (currentActive != null && currentActive != this) {
            if (outline != null)
                outline.enabled = false;
            return;
        }

        bool showHints = IsInteractHintOn();

        float dist = Vector3.Distance(playerController.transform.position, targetCollider.transform.position);

        float outlineDist = interactDistance;
        if (outlineWhenNearby) {
            if (outlineNearbyDistance > 0f)
                outlineDist = outlineNearbyDistance;
        }

        bool withinOutline = dist <= outlineDist;
        bool withinInteract = dist <= interactDistance;

        bool isLooking = false;
        if (withinInteract) {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance + 1f))
                isLooking = hit.collider == targetCollider;
        }

        if (outline != null)
            outline.enabled = showHints && withinOutline && (outlineWhenNearby || isLooking);

        if (showHints && withinInteract && isLooking) {
            int mode = stationPressableFallbackMode;
            if (stationPressableSource != null)
                mode = stationPressableSource.pressableMode;
            InvokePressableMode(mode);
        }

        if (!showHints || !withinInteract || !isLooking)
            return;

        if (!IsInteractDown())
            return;

        EnterInteractionMode();
    }

    private void UpdateInteractionMode() {
        ForceCursorUnlockedForInteraction();

        if (blockWhileCenterModuleSwapping && Chap2CenterModuleController.IsSwapping) {
            ExitInteractionMode();
            return;
        }

        ResolveSequenceManagerIfNeeded();
        if (requireSequenceStepMatch && sequenceManager != null && sequenceManager.CurrentStep != stepIndex) {
            ExitInteractionMode();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) {
            ExitInteractionMode();
            return;
        }

        if (IsElementClickUp())
            ReleasePressedElement();

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        InteractableElement newHover = null;
        float bestDist = float.PositiveInfinity;

        float dist = Mathf.Max(0.01f, elementRayDistance);
        RaycastHit[] hits = Physics.RaycastAll(ray, dist, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++) {
            InteractableElement e = hits[i].collider.GetComponentInParent<InteractableElement>();
            if (e == null)
                continue;

            if (hits[i].distance < bestDist) {
                bestDist = hits[i].distance;
                newHover = e;
            }
        }

        if (newHover != hoveredElement) {
            if (hoveredElement != null)
                hoveredElement.SetHovered(false);

            hoveredElement = newHover;

            if (hoveredElement != null)
                hoveredElement.SetHovered(true);
        }

        if (hoveredElement != null)
            InvokePressableMode(hoveredElement.pressableMode);

        if (hoveredElement == null)
            return;

        if (!IsElementClickDown())
            return;

        pressedElement = hoveredElement;
        pressedElement.PressDown();
        pressedElement.Interact();
    }

    private void ForceCursorUnlockedForInteraction() {
        if (playerController != null)
            playerController.lockCursor = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void InvokePressableMode(int mode) {
        if (onPressableMode == null)
            return;

        onPressableMode.Invoke(mode);
    }

    private void ReleasePressedElement() {
        if (pressedElement == null)
            return;

        pressedElement.PressUp();
        pressedElement = null;
    }

    private bool IsElementClickDown() {
        return Input.GetMouseButtonDown(0);
    }

    private bool IsElementClickUp() {
        return Input.GetMouseButtonUp(0);
    }

    private void EnterInteractionMode() {
        if (currentActive != null && currentActive != this)
            return;

        currentActive = this;

        SaveSnapshots();

        if (targetCollider != null) {
            targetCollider.enabled = false;
            disabledTargetColliderForInteraction = true;
        }

        isInInteractionMode = true;
        InteractionModeService.SetInteractionMode(true);

        if (outline != null)
            outline.enabled = false;

        ApplyInteractionRestrictedMode();
        ShowInteractionHintUI();

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(CoMovePlayerToCameraPoint(cameraTargetPoint, enterMoveTime));
    }

    private void ExitInteractionMode() {
        if (!isInInteractionMode)
            return;

        ReleasePressedElement();

        isInInteractionMode = false;
        InteractionModeService.SetInteractionMode(false);

        if (hoveredElement != null) {
            hoveredElement.SetHovered(false);
            hoveredElement = null;
        }

        HideInteractionHintUI();

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(CoRestorePlayer(exitMoveTime));
    }

    private IEnumerator CoMovePlayerToCameraPoint(Transform camPoint, float time) {
        if (playerController == null || playerCamera == null || camPoint == null)
            yield break;

        playerController.ResetCameraRotation(true);

        Vector3 camLocalPos = playerCamera.transform.localPosition;

        Vector3 startPos = playerController.transform.position;
        Quaternion startRot = playerController.transform.rotation;

        Quaternion targetRot = camPoint.rotation;
        Vector3 targetPos = camPoint.position - (targetRot * camLocalPos);

        float dur = Mathf.Max(0.01f, time);
        float t = 0f;

        while (t < dur) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float s = Mathf.SmoothStep(0f, 1f, k);

            playerController.transform.position = Vector3.Lerp(startPos, targetPos, s);
            playerController.transform.rotation = Quaternion.Slerp(startRot, targetRot, s);

            yield return null;
        }

        playerController.transform.position = targetPos;
        playerController.transform.rotation = targetRot;
    }

    private IEnumerator CoRestorePlayer(float time) {
        if (playerController == null || playerCamera == null)
            yield break;

        Vector3 startPos = playerController.transform.position;
        Quaternion startRot = playerController.transform.rotation;

        float dur = Mathf.Max(0.01f, time);
        float t = 0f;

        while (t < dur) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float s = Mathf.SmoothStep(0f, 1f, k);

            playerController.transform.position = Vector3.Lerp(startPos, savedPlayerPos, s);
            playerController.transform.rotation = Quaternion.Slerp(startRot, savedPlayerRot, s);

            yield return null;
        }

        playerController.transform.position = savedPlayerPos;
        playerController.transform.rotation = savedPlayerRot;

        RestoreSnapshots();

        if (disabledTargetColliderForInteraction && targetCollider != null)
            targetCollider.enabled = savedTargetColliderEnabled;

        disabledTargetColliderForInteraction = false;

        if (currentActive == this)
            currentActive = null;
    }

    private void SaveSnapshots() {
        savedPlayerPos = playerController.transform.position;
        savedPlayerRot = playerController.transform.rotation;

        savedLockCursor = playerController.lockCursor;

        savedTargetColliderEnabled = targetCollider != null && targetCollider.enabled;

        Rigidbody rb = playerController.GetComponent<Rigidbody>();

        savedRestrict = new RestrictSnapshot {
            isMovementLocked = playerController.isMovementLocked,
            useBodyRotation = playerController.useBodyRotation,
            lockLookX = playerController.lockLookX,
            lockLookY = playerController.lockLookY,
            sensMult = playerController.lookSensitivityMultiplier,
            yawClamp = playerController.useYawClamp,
            minY = playerController.minYawLimit,
            maxY = playerController.maxYawLimit,
            freezePos = rb != null && ((rb.constraints & RigidbodyConstraints.FreezePosition) != 0),
            kinematic = rb != null && rb.isKinematic,
            headlampLocked = playerController.isHeadlampInputLocked
        };
    }

    private void RestoreSnapshots() {
        playerController.lockCursor = savedLockCursor;

        if (savedLockCursor) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        } else {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        playerController.SetRestrictedMode(
            moveLocked: savedRestrict.isMovementLocked,
            bodyRot: savedRestrict.useBodyRotation,
            lockX: savedRestrict.lockLookX,
            lockY: savedRestrict.lockLookY,
            sensMult: savedRestrict.sensMult,
            yawClamp: savedRestrict.yawClamp,
            minY: savedRestrict.minY,
            maxY: savedRestrict.maxY,
            freezeRigidbodyPos: savedRestrict.freezePos,
            setKinematic: savedRestrict.kinematic,
            headlampLocked: savedRestrict.headlampLocked
        );

        ApplyCrosshairFromSettings();
    }

    private void ApplyInteractionRestrictedMode() {
        playerController.lockCursor = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetCrosshairVisible(false);

        bool freezePhysics = freezePlayerPhysicsDuringInteraction;

        playerController.SetRestrictedMode(
            moveLocked: true,
            bodyRot: true,
            lockX: true,
            lockY: true,
            sensMult: lookSensitivityMultiplier,
            yawClamp: false,
            minY: 0f,
            maxY: 0f,
            freezeRigidbodyPos: freezePhysics,
            setKinematic: freezePhysics,
            headlampLocked: !allowHeadlampToggleDuringInteraction
        );
    }

    private void ShowInteractionHintUI() {
        if (!showExitHint)
            return;

        if (tutorialHint == null)
            return;

        string msg = "나가기 : ESC";
        if (showTypingHintOnStep1 && stepIndex == 1)
            msg += "\n타자치기 : A-Z, a-z\n한/영키 주의";

        tutorialHint.ShowCustomPersistent(msg);
    }

    private void HideInteractionHintUI() {
        if (tutorialHint == null)
            return;

        tutorialHint.HideImmediate();
    }

    private bool IsInteractDown() {
        if (input != null)
            return input.GetKeyDown("Interact");

        return Input.GetKeyDown(KeyCode.F);
    }

    private bool IsInteractHintOn() {
        SettingsManager sm = SettingsManager.Instance;
        if (sm == null)
            return true;

        int v = sm.GetInt("InteractHint", 0);
        return v == 0;
    }

    private void SetCrosshairVisible(bool visible) {
        if (settingsApplier == null || settingsApplier.crosshairRoot == null)
            return;

        settingsApplier.crosshairRoot.SetActive(visible);
    }

    private void ApplyCrosshairFromSettings() {
        if (settingsApplier == null)
            return;

        settingsApplier.ApplyCrosshair();
    }
}