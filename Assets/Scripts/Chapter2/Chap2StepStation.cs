using System.Collections;
using UnityEngine;

public class Chap2StepStation : MonoBehaviour {
    [Header("Step")]
    [Range(1, 7)]
    public int stepIndex = 1;

    [Header("Refs")]
    public GameManagerChap2 gameManager;
    public Outline outline;
    public Collider targetCollider;
    public Chap2YStepSequenceManager sequenceManager;

    [Header("View")]
    public Transform cameraTargetPoint;

    [Header("Interaction")]
    public float interactDistance = 2.5f;
    public float elementRayDistance = 3.0f;

    [Header("Camera Motion")]
    public float enterMoveTime = 0.5f;
    public float exitMoveTime = 0.5f;

    [Header("Restricted Mode")]
    public Vector2 stepLookSensitivityMultiplier = Vector2.zero;

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

    private bool isInStepMode = false;
    private Coroutine moveRoutine;

    private Chap2StepElement hoveredElement;
    private Chap2StepElement pressedElement;

    private static Chap2StepStation currentActive;

    private bool savedTargetColliderEnabled;
    private bool disabledTargetColliderForStepMode;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

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
    }

    private void Update() {
        if (Time.timeScale == 0f)
            return;

        if (isInStepMode) {
            UpdateStepMode();
            return;
        }

        UpdateIdleInteract();
    }

    private void OnEnable() {
        if (currentActive == null) currentActive = null;
    }

    private void OnDisable() {
        if (currentActive == this)
            currentActive = null;

        if (Chap2StepInteractionService.IsInStepMode)
            Chap2StepInteractionService.SetStepMode(false);

        if (disabledTargetColliderForStepMode && targetCollider != null)
            targetCollider.enabled = savedTargetColliderEnabled;

        if (isInStepMode)
            ApplyCrosshairFromSettings();

        disabledTargetColliderForStepMode = false;
    }

    private void UpdateIdleInteract() {
        if (Chap2CenterModuleController.IsSwapping) {
            if (outline != null) outline.enabled = false;
            return;
        }

        if (playerController == null || playerCamera == null || targetCollider == null || cameraTargetPoint == null) {
            if (outline != null) outline.enabled = false;
            return;
        }

        if (currentActive != null && currentActive != this) {
            if (outline != null) outline.enabled = false;
            return;
        }

        bool showHints = IsInteractHintOn();

        float dist = Vector3.Distance(playerController.transform.position, targetCollider.transform.position);
        bool within = dist <= interactDistance;

        bool isLooking = false;
        if (within) {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance + 1f))
                isLooking = (hit.collider == targetCollider);
        }

        if (outline != null)
            outline.enabled = showHints && within && isLooking;

        if (showHints && within && isLooking && gameManager != null)
            gameManager.Pressable(0);

        if (!showHints || !within || !isLooking)
            return;

        if (!IsInteractDown())
            return;

        EnterStepMode();
    }

    private void UpdateStepMode() {
        if (gameManager != null && gameManager.State != GameManagerChap2.Chap2State.YSequence) {
            ExitStepMode();
            return;
        }

        if (sequenceManager != null && sequenceManager.CurrentStep != stepIndex) {
            ExitStepMode();
            return;
        }

        if (Chap2CenterModuleController.IsSwapping) {
            ExitStepMode();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) {
            ExitStepMode();
            return;
        }

        if (IsElementClickUp())
            ReleasePressedElement();

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        bool hitAny = Physics.Raycast(ray, out RaycastHit hit, elementRayDistance);

        Chap2StepElement newHover = null;
        if (hitAny)
            newHover = hit.collider.GetComponentInParent<Chap2StepElement>();

        if (newHover != hoveredElement) {
            if (hoveredElement != null)
                hoveredElement.SetHovered(false);

            hoveredElement = newHover;

            if (hoveredElement != null)
                hoveredElement.SetHovered(true);
        }

        if (hoveredElement != null && gameManager != null)
            gameManager.Pressable(hoveredElement.pressableMode);

        if (hoveredElement == null)
            return;

        if (!IsElementClickDown())
            return;

        pressedElement = hoveredElement;
        pressedElement.PressDown();
        pressedElement.Interact();
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

    private void EnterStepMode() {
        if (currentActive != null && currentActive != this)
            return;

        currentActive = this;

        SaveSnapshots();

        if (targetCollider != null) {
            targetCollider.enabled = false;
            disabledTargetColliderForStepMode = true;
        }

        isInStepMode = true;
        Chap2StepInteractionService.SetStepMode(true);

        if (outline != null)
            outline.enabled = false;

        ApplyStepRestrictedMode(true);
        ShowStepHintUI();

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(CoMovePlayerToCameraPoint(cameraTargetPoint, enterMoveTime));
    }

    private void ExitStepMode() {
        if (!isInStepMode)
            return;

        ReleasePressedElement();

        isInStepMode = false;
        Chap2StepInteractionService.SetStepMode(false);

        if (hoveredElement != null) {
            hoveredElement.SetHovered(false);
            hoveredElement = null;
        }

        HideStepHintUI();

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

        if (disabledTargetColliderForStepMode && targetCollider != null)
            targetCollider.enabled = savedTargetColliderEnabled;

        disabledTargetColliderForStepMode = false;

        if (currentActive == this)
            currentActive = null;
    }

    private void SaveSnapshots() {
        savedPlayerPos = playerController.transform.position;
        savedPlayerRot = playerController.transform.rotation;

        savedLockCursor = playerController.lockCursor;

        savedTargetColliderEnabled = (targetCollider != null) && targetCollider.enabled;

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
            freezePos = (rb != null) && ((rb.constraints & RigidbodyConstraints.FreezePosition) != 0),
            kinematic = (rb != null) && rb.isKinematic,
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

    private void ApplyStepRestrictedMode(bool entering) {
        playerController.lockCursor = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetCrosshairVisible(false);

        playerController.SetRestrictedMode(
            moveLocked: true,
            bodyRot: true,
            lockX: true,
            lockY: true,
            sensMult: stepLookSensitivityMultiplier,
            yawClamp: false,
            minY: 0f,
            maxY: 0f,
            freezeRigidbodyPos: false,
            setKinematic: false,
            headlampLocked: true
        );
    }

    private void ShowStepHintUI() {
        if (tutorialHint == null)
            return;

        string msg = "나가기 : ESC";
        if (stepIndex == 1)
            msg += "\n타자치기 : A-Z, a-z\n한/영키 주의";

        tutorialHint.ShowCustomPersistent(msg);
    }

    private void HideStepHintUI() {
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
        var sm = SettingsManager.Instance;
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