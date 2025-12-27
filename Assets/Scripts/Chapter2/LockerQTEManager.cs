using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LockerQTEManager : MonoBehaviour {
    public enum Dir { Forward, Back, Left, Right }

    public static LockerQTEManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject qteRoot;
    [SerializeField] private Image barFillImage;
    [SerializeField] private TextMeshProUGUI keyText;

    [Header("UI FX")]
    [SerializeField] private RectTransform barShakeTarget;
    [SerializeField] private float keyPunchScale = 1.18f;
    [SerializeField] private float barPunchScale = 1.06f;
    [SerializeField] private float punchDuration = 0.12f;

    [SerializeField] private float barShakeThreshold = 0.20f;
    [SerializeField] private float barShakeAmplitude = 10f;
    [SerializeField] private float barShakeFrequency = 18f;

    [SerializeField] private Color keyNormalColor = Color.white;
    [SerializeField] private Color keyWrongColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private float keyWrongFlashTime = 0.12f;

    [Header("Camera Shake")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float camPosAmplitude = 0.03f;
    [SerializeField] private float camRotAmplitude = 1.2f;
    [SerializeField] private float camShakeFrequency = 12f;
    [SerializeField] private float camShakeSmoothTime = 0.06f;
    [SerializeField] private float camDangerExponent = 1.2f;

    [Header("Hit Impulse (Locker Rattle)")]
    [SerializeField, Range(0f, 1f)] private float hitImpulseAdd = 0.55f;
    [SerializeField] private float hitImpulseDecayTime = 0.18f;
    [SerializeField, Range(0f, 2f)] private float hitImpulseStrengthMultiplier = 1.0f;

    [Header("Timing")]
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private float surviveTime = 2.5f;
    [SerializeField] private float startFill = 1f;

    [Header("Input Push")]
    [SerializeField, Range(0.01f, 1f)] private float correctPush = 0.10f;
    [SerializeField, Range(1f, 4f)] private float wrongMultiplier = 2f;

    [Header("Drain By Distance (Linear)")]
    [SerializeField] private float hardDistance = 4f;
    [SerializeField] private float easyDistance = 18f;
    [SerializeField] private float drainHardPerSec = 0.55f;
    [SerializeField] private float drainEasyPerSec = 0.18f;

    [Header("Door (Manual Y Rotation)")]
    [SerializeField] private Transform doorHingeOverride;
    [SerializeField] private float closedY = -80.862f;
    [SerializeField] private float openY = -35.071f;
    [SerializeField] private float failY = 97.023f;
    [SerializeField] private float doorSmoothTime = 0.08f;
    [SerializeField] private float doorCloseSpeed = 18f;

    [Header("Door Kickback")]
    [SerializeField] private float doorKickAmount = 3.0f;
    [SerializeField] private float doorKickReturnTime = 0.06f;

    [Header("Fail Slam (No Teleport)")]
    [SerializeField] private float failSlamDuration = 0.18f;
    [SerializeField] private float failSlamOvershoot = 10f;
    [SerializeField] private bool restoreAnimatorOnFail = false;

    [Header("Monster Hit SFX (Interval)")]
    [SerializeField] private AudioSource hitSource;
    [SerializeField] private List<AudioClip> hitClips = new List<AudioClip>();
    [SerializeField] private float hitIntervalMin = 0.25f;
    [SerializeField] private float hitIntervalMax = 0.5f;

    [Header("Monologue (Shared)")]
    [SerializeField] private MonologueManager monologueManager;
    [TextArea(2, 4)]
    [SerializeField] private List<string> monsterLeaveMonologues = new List<string>();
    [SerializeField] private float visibleDurationOverride = 0f;
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private bool overrideTypewriterSpeed = false;
    [SerializeField] private float typewriterCharsPerSecond = 40f;

    private LockerInteractable locker;
    private InputSettingsManager ism;

    private bool running = false;
    private bool inputEnabled = false;

    private float fill = 1f;
    private float surviveTimer = 0f;
    private float doorVel = 0f;

    private float recordedDistance = 999f;
    private Dir currentDir;

    private bool prevLockerAnimatorEnabled = false;

    private Coroutine startRoutine;
    private Coroutine hitRoutine;
    private Coroutine closeRoutine;
    private Coroutine failRoutine;

    private System.Action onSuccess;
    private System.Action onFail;

    private bool exitLockAfterQteWin = false;
    private LockerInteractable exitLockLocker;

    private RectTransform keyRect;
    private RectTransform barRect;
    private Vector3 keyBaseScale = Vector3.one;
    private Vector3 barBaseScale = Vector3.one;
    private Vector2 barBaseAnchoredPos = Vector2.zero;

    private Coroutine keyPunchRoutine;
    private Coroutine barPunchRoutine;
    private Coroutine keyFlashRoutine;

    private PlayerController playerController;
    private Transform camTr;
    private Vector3 camBaseLocalPos;
    private Quaternion camBaseLocalRot;
    private bool camBaseCaptured = false;
    private float hitImpulse01 = 0f;

    private Vector3 camPosVel = Vector3.zero;
    private Vector3 camRotVel = Vector3.zero;
    private Vector3 camPosOffset = Vector3.zero;
    private Vector3 camRotOffset = Vector3.zero;

    private float doorKickOffset = 0f;
    private float doorKickVel = 0f;

    private bool wasRunningLastFrame = false;

    public bool IsRunning => running;
    public LockerInteractable CurrentLocker => locker;

    public bool IsRunningFor(LockerInteractable target) {
        if (!running)
            return false;
        if (locker == null)
            return false;
        return locker == target;
    }

    public bool ShouldBlockExitFor(LockerInteractable target) {
        if (!exitLockAfterQteWin)
            return false;
        if (exitLockLocker == null)
            return false;
        return exitLockLocker == target;
    }

    public void NotifyMonsterGoneForLocker(LockerInteractable target) {
        if (!ShouldBlockExitFor(target))
            return;

        exitLockAfterQteWin = false;
        exitLockLocker = null;

        PlayRandomLeaveMonologue();
    }

    void Awake() {
        if (Instance != null && Instance != this) {
            gameObject.SetActive(false);
            return;
        }

        Instance = this;

        ism = InputSettingsManager.Instance;
        if (ism == null)
            ism = FindFirstObjectByType<InputSettingsManager>();

        if (monologueManager == null)
            monologueManager = FindFirstObjectByType<MonologueManager>();

        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null && playerController.playerCamera != null)
            camTr = playerController.playerCamera.transform;

        if (keyText != null) {
            keyRect = keyText.rectTransform;
            keyBaseScale = keyRect.localScale;
            keyText.color = keyNormalColor;
        }

        if (barFillImage != null) {
            barRect = (barShakeTarget != null) ? barShakeTarget : barFillImage.rectTransform;
            barBaseScale = barRect.localScale;
            barBaseAnchoredPos = barRect.anchoredPosition;
        }

        if (qteRoot != null)
            qteRoot.SetActive(false);

        UpdateUI();
    }

    void Update() {
        if (!running) {
            if (wasRunningLastFrame)
                ApplyCameraShake(false);

            wasRunningLastFrame = false;
            return;
        }

        wasRunningLastFrame = true;

        if (hitImpulse01 > 0f) {
            float decay = Mathf.Max(0.01f, hitImpulseDecayTime);
            hitImpulse01 = Mathf.MoveTowards(hitImpulse01, 0f, Time.deltaTime / decay);
        }

        ApplyCameraShake(true);
        UpdateBarShake();

        if (!inputEnabled) {
            UpdateDoorByFill();
            return;
        }

        surviveTimer += Time.deltaTime;

        float drain = GetDrainPerSec(recordedDistance);
        fill -= drain * Time.deltaTime;
        fill = Mathf.Clamp01(fill);

        HandleInput();

        if (fill <= 0f) {
            Fail();
            return;
        }

        if (surviveTimer >= surviveTime) {
            Success();
            return;
        }

        UpdateUI();
        UpdateDoorByFill();
    }

    public void BeginQTE(LockerInteractable targetLocker, float hideDistance, System.Action onSucceeded = null, System.Action onFailed = null) {
        if (targetLocker == null)
            return;

        if (running)
            return;

        locker = targetLocker;
        recordedDistance = hideDistance;

        onSuccess = onSucceeded;
        onFail = onFailed;

        running = true;
        inputEnabled = false;

        fill = Mathf.Clamp01(startFill);
        surviveTimer = 0f;

        currentDir = GetRandomDir();
        UpdateUI();

        doorKickOffset = 0f;
        doorKickVel = 0f;

        if (keyText != null)
            keyText.color = keyNormalColor;

        CaptureCameraBase();

        if (barRect != null) {
            barRect.localScale = barBaseScale;
            barRect.anchoredPosition = barBaseAnchoredPos;
        }

        if (keyRect != null)
            keyRect.localScale = keyBaseScale;

        ApplyLockerAnimatorOverride(true);

        if (qteRoot != null)
            qteRoot.SetActive(true);

        if (startRoutine != null)
            StopCoroutine(startRoutine);

        startRoutine = StartCoroutine(CoStartAfterDelay());

        if (hitRoutine != null)
            StopCoroutine(hitRoutine);

        hitRoutine = StartCoroutine(CoPlayHitSounds());
    }

    public void CancelQTE() {
        if (!running)
            return;

        StopQteRuntimeAndHideUI();

        exitLockAfterQteWin = false;
        exitLockLocker = null;

        ApplyLockerAnimatorOverride(false);
        ClearQteRefs();
    }

    public void ForceEndForDeath() {
        if (!running) {
            if (qteRoot != null)
                qteRoot.SetActive(false);
            return;
        }

        StopQteRuntimeAndHideUI();

        exitLockAfterQteWin = false;
        exitLockLocker = null;

        ClearQteRefs();
    }

    IEnumerator CoStartAfterDelay() {
        float t = 0f;
        while (t < startDelay) {
            t += Time.deltaTime;
            UpdateDoorByFill();
            yield return null;
        }

        inputEnabled = true;
        startRoutine = null;
    }

    IEnumerator CoPlayHitSounds() {
        while (running) {
            float wait = Random.Range(Mathf.Max(0.01f, hitIntervalMin), Mathf.Max(0.02f, hitIntervalMax));
            yield return new WaitForSeconds(wait);

            if (!running)
                break;

            PlayHit();
        }

        hitRoutine = null;
    }

    private void HandleInput() {
        Dir? pressed = GetPressedDir();
        if (!pressed.HasValue)
            return;

        if (pressed.Value == currentDir) {
            fill += correctPush;
            fill = Mathf.Clamp01(fill);

            doorKickOffset -= Mathf.Abs(doorKickAmount);

            PunchKey();
            PunchBar();

            currentDir = GetRandomDir();
            UpdateUI();
            return;
        }

        fill -= correctPush * wrongMultiplier;
        fill = Mathf.Clamp01(fill);

        FlashKeyWrong();
        PunchKey();
    }

    private Dir? GetPressedDir() {
        if (ism != null) {
            if (ism.GetKeyDown("MoveForward")) return Dir.Forward;
            if (ism.GetKeyDown("MoveBackward")) return Dir.Back;
            if (ism.GetKeyDown("MoveLeft")) return Dir.Left;
            if (ism.GetKeyDown("MoveRight")) return Dir.Right;
            return null;
        }

        if (Input.GetKeyDown(KeyCode.W)) return Dir.Forward;
        if (Input.GetKeyDown(KeyCode.S)) return Dir.Back;
        if (Input.GetKeyDown(KeyCode.A)) return Dir.Left;
        if (Input.GetKeyDown(KeyCode.D)) return Dir.Right;
        return null;
    }

    private void Success() {
        StopQteRuntimeAndHideUI();

        exitLockAfterQteWin = true;
        exitLockLocker = locker;

        System.Action cb = onSuccess;
        onSuccess = null;
        onFail = null;

        if (closeRoutine != null)
            StopCoroutine(closeRoutine);

        closeRoutine = StartCoroutine(CoCloseDoorFastThenRestoreAnimatorAndFinalize());

        cb?.Invoke();
    }

    private void Fail() {
        StopQteRuntimeAndHideUI();

        if (failRoutine != null)
            StopCoroutine(failRoutine);

        failRoutine = StartCoroutine(CoFailSlamThenFinalize());
    }

    private IEnumerator CoFailSlamThenFinalize() {
        Transform hinge = GetDoorHinge();
        float dur = Mathf.Max(0f, failSlamDuration);

        if (hinge != null && dur > 0.001f) {
            float startY = hinge.localEulerAngles.y;
            float overshootY = failY + Mathf.Max(0f, failSlamOvershoot);

            float t1 = dur * 0.65f;
            float t2 = dur - t1;

            float t = 0f;
            while (t < t1) {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / t1);
                k = EaseOutCubic(k);

                float y = Mathf.LerpAngle(startY, overshootY, k);
                Vector3 e = hinge.localEulerAngles;
                e.y = y;
                hinge.localEulerAngles = e;

                yield return null;
            }

            if (t2 > 0.001f) {
                t = 0f;
                while (t < t2) {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / t2);
                    k = EaseOutCubic(k);

                    float y = Mathf.LerpAngle(overshootY, failY, k);
                    Vector3 e = hinge.localEulerAngles;
                    e.y = y;
                    hinge.localEulerAngles = e;

                    yield return null;
                }
            }

            Vector3 e2 = hinge.localEulerAngles;
            e2.y = failY;
            hinge.localEulerAngles = e2;
        } else if (hinge != null) {
            Vector3 e = hinge.localEulerAngles;
            e.y = failY;
            hinge.localEulerAngles = e;
        }

        exitLockAfterQteWin = false;
        exitLockLocker = null;

        bool doDeath = DeathManager.Instance != null && DeathManager.Instance.EnableDeath;

        if (doDeath)
            PrepareMonsterForDeathCinematic();

        if (!doDeath && restoreAnimatorOnFail)
            ApplyLockerAnimatorOverride(false);

        onFail?.Invoke();
        ClearQteRefs();

        if (doDeath)
            DeathManager.Instance.TriggerMonsterDeath();

        failRoutine = null;
    }

    private void PrepareMonsterForDeathCinematic() {
        var monster = FindFirstObjectByType<Chap2MonsterController>();
        if (monster == null)
            return;

        monster.PrepareForDeathCinematic();
    }

    private void StopQteRuntimeAndHideUI() {
        running = false;
        inputEnabled = false;

        if (startRoutine != null)
            StopCoroutine(startRoutine);

        if (hitRoutine != null)
            StopCoroutine(hitRoutine);

        startRoutine = null;
        hitRoutine = null;

        if (qteRoot != null)
            qteRoot.SetActive(false);

        if (keyPunchRoutine != null)
            StopCoroutine(keyPunchRoutine);

        if (barPunchRoutine != null)
            StopCoroutine(barPunchRoutine);

        if (keyFlashRoutine != null)
            StopCoroutine(keyFlashRoutine);

        keyPunchRoutine = null;
        barPunchRoutine = null;
        keyFlashRoutine = null;

        ResetUIFxImmediate();
        ApplyCameraShake(false);

        wasRunningLastFrame = false;
    }

    IEnumerator CoCloseDoorFastThenRestoreAnimatorAndFinalize() {
        Transform hinge = GetDoorHinge();

        if (hinge != null) {
            float curY = hinge.localEulerAngles.y;
            float t = 0f;

            while (t < 1f) {
                t += Time.deltaTime * Mathf.Max(0.01f, doorCloseSpeed);
                float y = Mathf.LerpAngle(curY, closedY, Mathf.Clamp01(t));

                Vector3 e = hinge.localEulerAngles;
                e.y = y;
                hinge.localEulerAngles = e;

                yield return null;
            }

            Vector3 e2 = hinge.localEulerAngles;
            e2.y = closedY;
            hinge.localEulerAngles = e2;
        }

        ApplyLockerAnimatorOverride(false);

        closeRoutine = null;
        ClearQteRefs();
    }

    private void ClearQteRefs() {
        locker = null;
        onSuccess = null;
        onFail = null;
        recordedDistance = 999f;
    }

    private float GetDrainPerSec(float dist) {
        float t = 0f;

        if (easyDistance > hardDistance)
            t = Mathf.InverseLerp(hardDistance, easyDistance, dist);

        return Mathf.Lerp(drainHardPerSec, drainEasyPerSec, t);
    }

    private void UpdateUI() {
        if (barFillImage != null)
            barFillImage.fillAmount = fill;

        if (keyText != null)
            keyText.text = GetDirLabel(currentDir);
    }

    private void UpdateDoorByFill() {
        Transform hinge = GetDoorHinge();
        if (hinge == null)
            return;

        float baseTargetY = Mathf.Lerp(openY, closedY, fill);

        doorKickOffset = Mathf.SmoothDamp(doorKickOffset, 0f, ref doorKickVel, Mathf.Max(0.001f, doorKickReturnTime));
        float targetY = baseTargetY + doorKickOffset;

        float currentY = hinge.localEulerAngles.y;
        float newY = Mathf.SmoothDampAngle(currentY, targetY, ref doorVel, Mathf.Max(0.001f, doorSmoothTime));

        Vector3 e = hinge.localEulerAngles;
        e.y = newY;
        hinge.localEulerAngles = e;
    }

    private Transform GetDoorHinge() {
        if (doorHingeOverride != null)
            return doorHingeOverride;

        if (locker != null && locker.qteDoorHinge != null)
            return locker.qteDoorHinge;

        return null;
    }

    private void ApplyLockerAnimatorOverride(bool disableAnimator) {
        if (locker == null)
            return;

        if (locker.lockerAnimator == null)
            return;

        if (disableAnimator) {
            prevLockerAnimatorEnabled = locker.lockerAnimator.enabled;
            locker.lockerAnimator.enabled = false;
            return;
        }

        locker.lockerAnimator.enabled = prevLockerAnimatorEnabled;
    }

    private void PlayHit() {
        if (hitSource == null)
            return;

        if (hitClips == null || hitClips.Count == 0)
            return;

        int idx = Random.Range(0, hitClips.Count);
        AudioClip clip = hitClips[idx];
        if (clip == null)
            return;

        hitSource.PlayOneShot(clip);
        TriggerHitImpulse();
    }

    private void TriggerHitImpulse() {
        if (!enableCameraShake)
            return;

        hitImpulse01 = Mathf.Clamp01(hitImpulse01 + Mathf.Clamp01(hitImpulseAdd));
    }

    public void PlayRandomLeaveMonologue() {
        if (monsterLeaveMonologues == null || monsterLeaveMonologues.Count == 0)
            return;

        if (monologueManager == null)
            monologueManager = FindFirstObjectByType<MonologueManager>();

        if (monologueManager == null)
            return;

        List<string> candidates = new List<string>();
        for (int i = 0; i < monsterLeaveMonologues.Count; i++) {
            if (!string.IsNullOrEmpty(monsterLeaveMonologues[i]))
                candidates.Add(monsterLeaveMonologues[i]);
        }

        if (candidates.Count == 0)
            return;

        int idx = Random.Range(0, candidates.Count);
        string msg = candidates[idx];

        float originalSpeed = 0f;
        bool changedSpeed = false;

        if (overrideTypewriterSpeed && useTypewriter && typewriterCharsPerSecond > 0f) {
            originalSpeed = monologueManager.typewriterCharsPerSecond;
            monologueManager.typewriterCharsPerSecond = typewriterCharsPerSecond;
            changedSpeed = true;
        }

        float dur = visibleDurationOverride > 0f ? visibleDurationOverride : monologueManager.defaultVisibleDuration;
        monologueManager.ShowMessage(msg, dur, useTypewriter);

        if (changedSpeed)
            monologueManager.typewriterCharsPerSecond = originalSpeed;
    }

    private void PunchKey() {
        if (keyRect == null)
            return;

        if (keyPunchRoutine != null)
            StopCoroutine(keyPunchRoutine);

        keyPunchRoutine = StartCoroutine(CoPunchScale(keyRect, keyBaseScale, Mathf.Max(1.01f, keyPunchScale), Mathf.Max(0.01f, punchDuration)));
    }

    private void PunchBar() {
        if (barRect == null)
            return;

        if (barPunchRoutine != null)
            StopCoroutine(barPunchRoutine);

        barPunchRoutine = StartCoroutine(CoPunchScale(barRect, barBaseScale, Mathf.Max(1.01f, barPunchScale), Mathf.Max(0.01f, punchDuration)));
    }

    IEnumerator CoPunchScale(RectTransform rt, Vector3 baseScale, float scaleMult, float duration) {
        float half = duration * 0.5f;
        float t = 0f;

        while (t < half) {
            t += Time.deltaTime;
            float k = t / half;
            if (k > 1f)
                k = 1f;

            float s = Mathf.Lerp(1f, scaleMult, k);
            rt.localScale = baseScale * s;

            yield return null;
        }

        t = 0f;

        while (t < half) {
            t += Time.deltaTime;
            float k = t / half;
            if (k > 1f)
                k = 1f;

            float s = Mathf.Lerp(scaleMult, 1f, k);
            rt.localScale = baseScale * s;

            yield return null;
        }

        rt.localScale = baseScale;

        if (rt == keyRect)
            keyPunchRoutine = null;
        else if (rt == barRect)
            barPunchRoutine = null;
    }

    private void FlashKeyWrong() {
        if (keyText == null)
            return;

        if (keyFlashRoutine != null)
            StopCoroutine(keyFlashRoutine);

        keyFlashRoutine = StartCoroutine(CoFlashKeyWrong());
    }

    IEnumerator CoFlashKeyWrong() {
        keyText.color = keyWrongColor;

        float t = 0f;
        float dur = Mathf.Max(0.01f, keyWrongFlashTime);

        while (t < dur) {
            t += Time.deltaTime;
            yield return null;
        }

        keyText.color = keyNormalColor;
        keyFlashRoutine = null;
    }

    private void UpdateBarShake() {
        if (barRect == null)
            return;

        if (!running) {
            barRect.anchoredPosition = barBaseAnchoredPos;
            return;
        }

        float danger = Mathf.Clamp01((barShakeThreshold - fill) / Mathf.Max(0.0001f, barShakeThreshold));
        if (danger <= 0f) {
            barRect.anchoredPosition = barBaseAnchoredPos;
            return;
        }

        float a = barShakeAmplitude * danger;
        float f = Mathf.Max(0.01f, barShakeFrequency);

        float x = Mathf.Sin(Time.time * f) * a;
        float y = Mathf.Cos(Time.time * (f * 0.85f)) * (a * 0.35f);

        barRect.anchoredPosition = barBaseAnchoredPos + new Vector2(x, y);
    }

    private void ResetUIFxImmediate() {
        if (keyRect != null)
            keyRect.localScale = keyBaseScale;

        if (barRect != null) {
            barRect.localScale = barBaseScale;
            barRect.anchoredPosition = barBaseAnchoredPos;
        }

        if (keyText != null)
            keyText.color = keyNormalColor;
    }

    private void CaptureCameraBase() {
        if (!enableCameraShake)
            return;

        if (camTr == null) {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();

            if (playerController != null && playerController.playerCamera != null)
                camTr = playerController.playerCamera.transform;
        }

        if (camTr == null)
            return;

        camBaseLocalPos = camTr.localPosition;
        camBaseLocalRot = camTr.localRotation;
        camBaseCaptured = true;

        camPosVel = Vector3.zero;
        camRotVel = Vector3.zero;
        camPosOffset = Vector3.zero;
        camRotOffset = Vector3.zero;
    }

    private void ApplyCameraShake(bool active) {
        if (!enableCameraShake)
            return;

        if (!camBaseCaptured || camTr == null)
            return;

        if (!active) {
            camTr.localPosition = camBaseLocalPos;
            camTr.localRotation = camBaseLocalRot;
            camPosOffset = Vector3.zero;
            camRotOffset = Vector3.zero;
            camPosVel = Vector3.zero;
            camRotVel = Vector3.zero;
            hitImpulse01 = 0f;
            return;
        }

        float baseDanger = Mathf.Clamp01(1f - fill);
        baseDanger = Mathf.Pow(baseDanger, Mathf.Max(0.01f, camDangerExponent));

        float hitDanger = Mathf.Clamp01(hitImpulse01) * Mathf.Max(0f, hitImpulseStrengthMultiplier);
        float danger = Mathf.Clamp01(Mathf.Max(baseDanger, hitDanger));

        float freq = Mathf.Max(0.01f, camShakeFrequency);

        float nx = (Mathf.PerlinNoise(Time.time * freq, 0.1f) * 2f) - 1f;
        float ny = (Mathf.PerlinNoise(0.2f, Time.time * freq) * 2f) - 1f;
        float nr = (Mathf.PerlinNoise(Time.time * freq, 0.7f) * 2f) - 1f;

        Vector3 targetPos = new Vector3(nx, ny, 0f) * (camPosAmplitude * danger);
        Vector3 targetRot = new Vector3(ny, nx, nr) * (camRotAmplitude * danger);

        float smooth = Mathf.Max(0.001f, camShakeSmoothTime);

        camPosOffset = Vector3.SmoothDamp(camPosOffset, targetPos, ref camPosVel, smooth);
        camRotOffset = Vector3.SmoothDamp(camRotOffset, targetRot, ref camRotVel, smooth);

        camTr.localPosition = camBaseLocalPos + camPosOffset;
        camTr.localRotation = camBaseLocalRot * Quaternion.Euler(camRotOffset);
    }

    private Dir GetRandomDir() {
        int v = Random.Range(0, 4);
        return (Dir)v;
    }

    private string GetDirLabel(Dir dir) {
        string actionId = GetActionId(dir);
        return GetKeyLabel(actionId);
    }

    private string GetActionId(Dir dir) {
        if (dir == Dir.Forward) return "MoveForward";
        if (dir == Dir.Back) return "MoveBackward";
        if (dir == Dir.Left) return "MoveLeft";
        return "MoveRight";
    }

    private string GetKeyLabel(string actionId) {
        string fallback = GetFallbackKeyLabel(actionId);

        if (ism == null)
            return fallback;

        KeyCode primary = ism.GetPrimaryKey(actionId);
        KeyCode secondary = ism.GetSecondaryKey(actionId);

        if (primary == KeyCode.None && secondary == KeyCode.None)
            return fallback;

        List<string> parts = new List<string>();
        if (primary != KeyCode.None) parts.Add(ism.FormatKeyName(primary));
        if (secondary != KeyCode.None) parts.Add(ism.FormatKeyName(secondary));

        return string.Join(", ", parts);
    }

    private string GetFallbackKeyLabel(string actionId) {
        if (actionId == "MoveForward") return "W";
        if (actionId == "MoveBackward") return "S";
        if (actionId == "MoveLeft") return "A";
        if (actionId == "MoveRight") return "D";
        return "?";
    }

    private float EaseOutCubic(float t) {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return 1f - u * u * u;
    }
}