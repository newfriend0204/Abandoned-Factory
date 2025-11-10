using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour {
    [Header("Move")]
    public float moveSpeed = 6.0f;
    public float sprintMultiplier = 1.5f;

    [Header("Sprint Stamina")]
    public float sprintStaminaMax = 100f;
    public float sprintStamina = 100f;
    public float sprintDrainPerSecond = 25f;
    public float sprintRegenPerSecond = 20f;
    public float sprintRegenDelay = 0.2f;
    public bool isExhausted = false;

    [Header("Mouse Look")]
    public Camera playerCamera;
    public float sensX = 200f;
    public float sensY = 200f;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public bool lockCursor = true;

    [Header("Jump")]
    public float jumpHeight = 1.6f;
    public float timeToApex = 0.28f;
    public float postJumpIgnoreDuration = 0.08f;

    [Header("Jump Feel")]
    public float coyoteTime = 0.10f;
    public float jumpBuffer = 0.10f;
    public float startupIgnoreJumpTime = 0.20f;

    [Header("Ground / Slope")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.25f;
    public LayerMask groundMask = ~0;
    public float maxSlopeAngle = 45f;
    public float noSlideAngle = 25f;
    public float groundProbeDistance = 0.6f;
    public float stickToGroundAccel = 25f;

    [Header("Wall")]
    public float wallProbeDistance = 0.4f;
    public float wallCastBackOffset = 0.25f;
    [Range(0.7f, 0.99f)]
    public float headOnStopDot = 0.90f;

    [Header("Friction Swap (optional)")]
    public PhysicsMaterial groundMat;
    public PhysicsMaterial airMat;

    [Header("Acceleration")]
    public float groundAccel = 90f;
    public float airAccel = 40f;
    [Range(0f, 1f)]
    public float airControl = 0.85f;
    public float sprintAccelBonus = 60f;
    public float airAccelBoost = 80f;
    public float airAccelBoostTime = 0.22f;

    [Header("Damping")]
    public float groundDamping = 0.10f;
    public float airDamping = 0.02f;

    [Header("Camera FX")]
    public float fovKick = 5f;
    public float fovKickSpeed = 8f;
    public float headBobWalkFreq = 1.8f;
    public float headBobRunFreq = 2.6f;
    public float headBobWalkAmp = 0.02f;
    public float headBobRunAmp = 0.035f;
    public float landingKickThreshold = 6.0f;
    public float landingKickAmp = 0.06f;
    public float landingKickDuration = 0.12f;

    public float tiltRollMax = 1.3f;
    public float tiltRollSpeed = 6f;
    public float tiltRollReturnSpeed = 14f;
    public float tiltPitchAirMax = 4f;
    public float tiltPitchSpeed = 5f;

    public float landingRollAmp = 0.8f;
    public float landingRollDuration = 0.18f;

    public float rollClamp = 1.0f;

    [Header("Animation")]
    [SerializeField] private Animator handAnimator;
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimSprinting = Animator.StringToHash("Sprinting");

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private float pitch;
    private bool grounded, onTooSteep, nearWall;
    private Vector3 groundNormal = Vector3.up, wallNormal = Vector3.zero;
    private float slopeAngle;
    private bool jumpRequested;
    private float postJumpIgnoreTimer;

    private float customGravityY, jumpVelocityY;
    private float lastGroundedTime, lastJumpPressedTime;
    private bool wasGrounded;
    private float yVelPrev;
    private float sinceJump = 999f;

    private float baseFOV;
    private Vector3 camBaseLocalPos;
    private float bobTimer;
    private float landingKickT;
    private float fxRoll, fxPitch;

    private float landingRollT;
    private float landingRollSign;

    private float gameStartTime;

    private bool sprinting;
    private float timeSinceStoppedSprint;

    private readonly Collider[] _overlap = new Collider[8];

    private void Awake() {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        baseFOV = playerCamera.fieldOfView;
        camBaseLocalPos = playerCamera.transform.localPosition;
        RecomputeJumpPhysics();
        gameStartTime = Time.time;
        lastJumpPressedTime = -999f;
        lastGroundedTime = -999f;

        sprintStamina = Mathf.Clamp(sprintStamina, 0f, sprintStaminaMax);
        isExhausted = false;
        sprinting = false;
        timeSinceStoppedSprint = 999f;

        if (lockCursor) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update() {
        if (Time.timeScale == 0f) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        } else {
            if (lockCursor) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");
        pitch = Mathf.Clamp(pitch - my * sensY, minPitch, maxPitch);
        transform.Rotate(Vector3.up, mx * sensX, Space.Self);

        bool ignoreGroundNow = false;
        if (postJumpIgnoreTimer > 0f) {
            postJumpIgnoreTimer -= Time.deltaTime;
            ignoreGroundNow = true;
        }
        grounded = false; onTooSteep = false;
        groundNormal = Vector3.up; slopeAngle = 0f;
        if (!ignoreGroundNow && groundCheck != null) {
            int cnt = Physics.OverlapSphereNonAlloc(
                groundCheck.position, groundCheckRadius, _overlap, groundMask, QueryTriggerInteraction.Ignore
            );
            for (int i = 0; i < cnt; i++) {
                var c = _overlap[i];
                if (!c || c.attachedRigidbody == rb || c.transform.IsChildOf(transform))
                    continue;
                grounded = true;
                break;
            }
        }
        if (grounded) {
            float r = capsule ? capsule.radius : 0.5f;
            float skin = 0.02f;
            float bottomY = transform.position.y + (capsule ? capsule.center.y : 0f)
                            - (capsule ? (capsule.height * 0.5f - capsule.radius) : 0.5f)
                            + r + skin;
            Vector3 origin = new Vector3(transform.position.x, bottomY + 0.05f, transform.position.z);
            if (Physics.SphereCast(origin, r * 0.95f, Vector3.down,
                out RaycastHit hit, groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore)) {
                groundNormal = hit.normal;
                slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                if (slopeAngle > maxSlopeAngle) {
                    grounded = false;
                    onTooSteep = true;
                }
            }
        }

        nearWall = false; wallNormal = Vector3.zero;
        float ix = Input.GetAxisRaw("Horizontal");
        float iz = Input.GetAxisRaw("Vertical");
        Vector3 wishDirForWall = (transform.right * ix + transform.forward * iz).normalized;
        if (wishDirForWall.sqrMagnitude > 0.01f) {
            float halfH = capsule ? capsule.height * 0.5f : 1f;
            float r = capsule ? capsule.radius : 0.5f;
            Vector3 wallOrigin = transform.position + Vector3.up * halfH - wishDirForWall * (r * wallCastBackOffset);
            float castDist = wallProbeDistance + r * 0.6f;
            if (Physics.SphereCast(wallOrigin, r * 0.95f, wishDirForWall,
                out RaycastHit wh, castDist, groundMask, QueryTriggerInteraction.Ignore)) {
                if (Vector3.Angle(wh.normal, Vector3.up) > maxSlopeAngle) {
                    nearWall = true;
                    wallNormal = wh.normal;
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
            lastJumpPressedTime = Time.time;
        if (grounded)
            lastGroundedTime = Time.time;
        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        bool coyote = grounded || (Time.time - lastGroundedTime) <= coyoteTime;
        bool pastStartup = (Time.time - gameStartTime) >= startupIgnoreJumpTime;
        if (!jumpRequested && pastStartup && buffered && coyote)
            jumpRequested = true;

        float hzSpeed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
        float maxSpeed = moveSpeed * sprintMultiplier;
        float speed01 = grounded ? Mathf.Clamp01(hzSpeed / maxSpeed) : 0f;

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool hasMoveInput = (Mathf.Abs(Input.GetAxisRaw("Horizontal")) + Mathf.Abs(Input.GetAxisRaw("Vertical"))) > 0.001f;
        bool wantsSprint = shiftHeld && hasMoveInput;

        UpdateSprintStamina(wantsSprint);

        handAnimator.SetFloat(AnimSpeed, speed01, 0.1f, Time.deltaTime);
        handAnimator.SetBool(AnimSprinting, sprinting);
    }

    private void FixedUpdate() {
        if (Time.timeScale == 0f)
            return;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        bool hasMoveInput = (Mathf.Abs(x) + Mathf.Abs(z)) > 0.001f;

        bool sprint = sprinting && hasMoveInput;

        float currentSpeed = moveSpeed * (sprint ? sprintMultiplier : 1f);

        Vector3 wish = (transform.right * x + transform.forward * z);
        if (grounded && !onTooSteep) {
            wish = Vector3.ProjectOnPlane(wish, groundNormal);
            if (wish.sqrMagnitude > 0.0001f)
                wish = wish.normalized;
        }
        if (nearWall && wish.sqrMagnitude > 0.000001f) {
            Vector3 wishN = wish.normalized;
            float into = Vector3.Dot(wishN, -wallNormal);
            if (into >= headOnStopDot)
                wish = Vector3.zero;
            else
                wish = Vector3.ProjectOnPlane(wish, wallNormal);
        }
        if (wish.sqrMagnitude > 1f)
            wish.Normalize();
        wish *= currentSpeed;

        float accel;
        if (grounded) {
            accel = groundAccel + (sprint ? sprintAccelBonus : 0f);
            sinceJump = 999f;
        } else {
            sinceJump += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(sinceJump / airAccelBoostTime);
            float curBoost = Mathf.Lerp(airAccelBoost, airAccel, t);
            accel = curBoost;
            wish *= airControl;
        }

        Vector2 tgt = new Vector2(wish.x, wish.z);
        Vector2 cur = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        cur = Vector2.MoveTowards(cur, tgt, accel * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(cur.x, rb.linearVelocity.y, cur.y);

        if (jumpRequested) {
            Vector3 jv = rb.linearVelocity;
            if (jv.y < 0f) jv.y = 0f;
            jv.y = jumpVelocityY;
            rb.linearVelocity = jv;
            grounded = false; onTooSteep = false;
            rb.position += groundNormal * 0.01f;
            postJumpIgnoreTimer = postJumpIgnoreDuration;
            sinceJump = 0f;
            jumpRequested = false;
        }

        float extraGravity = customGravityY - Physics.gravity.y;
        Vector3 gravityAccel = new Vector3(0f, extraGravity, 0f);
        if (rb.linearVelocity.y < 0f)
            gravityAccel.y *= 2.2f;
        rb.AddForce(gravityAccel, ForceMode.Acceleration);

        if (grounded && !onTooSteep) {
            if (slopeAngle <= noSlideAngle) {
                Vector3 g = new Vector3(0f, customGravityY, 0f);
                Vector3 gParallel = Vector3.ProjectOnPlane(g, groundNormal);
                rb.AddForce(-gParallel, ForceMode.Acceleration);
            } else {
                rb.AddForce(-groundNormal * stickToGroundAccel, ForceMode.Acceleration);
            }
        }

        rb.linearDamping = grounded ? groundDamping : airDamping;

        if (!wasGrounded && grounded && Mathf.Abs(yVelPrev) > landingKickThreshold) {
            landingKickT = 1f;
            Vector3 velLocal = transform.InverseTransformDirection(rb.linearVelocity);
            landingRollSign = Mathf.Sign(Mathf.Abs(velLocal.x) < 0.01f ? Random.Range(-1f, 1f) : velLocal.x);
            landingRollT = 1f;
        }
        wasGrounded = grounded;
        yVelPrev = rb.linearVelocity.y;

        if (capsule != null && (groundMat != null || airMat != null)) {
            PhysicsMaterial targetMat = (grounded && !onTooSteep) ? groundMat : airMat;
            if (capsule.material != targetMat)
                capsule.material = targetMat;
        }
    }

    private void LateUpdate() {
        if (Time.timeScale == 0f)
            return;
        UpdateCameraFX();
    }

    private void UpdateCameraFX() {
        bool sprint = sprinting;

        float targetFov = baseFOV + (sprint ? fovKick : 0f);
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * fovKickSpeed);

        float hzSpeed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
        bool moving = hzSpeed > 0.1f;

        float freq = moving ? (sprint ? headBobRunFreq : headBobWalkFreq) : 0f;
        float amp = moving ? (sprint ? headBobRunAmp : headBobWalkAmp) : 0f;

        if (freq > 0f)
            bobTimer += Time.deltaTime * freq;
        else
            bobTimer = 0f;

        float bobOffset = amp * Mathf.Sin(bobTimer * Mathf.PI * 2f);

        float kickOffsetY = 0f;
        if (landingKickT > 0f) {
            float t = 1f - landingKickT;
            float curve = Mathf.Sin(t * Mathf.PI);
            kickOffsetY = -landingKickAmp * curve;
            landingKickT = Mathf.MoveTowards(landingKickT, 0f, Time.deltaTime / landingKickDuration);
        }

        Vector3 velLocal = transform.InverseTransformDirection(rb.linearVelocity);
        float targetRoll = Mathf.Clamp(-velLocal.x / (moveSpeed * sprintMultiplier) * tiltRollMax, -tiltRollMax, tiltRollMax);
        float targetPitch = grounded ? 0f :
            Mathf.Clamp(-velLocal.z / (moveSpeed * sprintMultiplier) * tiltPitchAirMax, -tiltPitchAirMax, tiltPitchAirMax);

        float horIn = Mathf.Abs(Input.GetAxisRaw("Horizontal"));
        float rollSpeed = (horIn < 0.001f) ? tiltRollReturnSpeed : tiltRollSpeed;

        fxRoll = Mathf.Lerp(fxRoll, targetRoll, Time.deltaTime * rollSpeed);
        fxPitch = Mathf.Lerp(fxPitch, targetPitch, Time.deltaTime * tiltPitchSpeed);

        float rollPulse = 0f;
        if (landingRollT > 0f) {
            float t = 1f - landingRollT;
            float osc = Mathf.Sin(t * Mathf.PI * 2f);
            float env = (1f - t);
            rollPulse = landingRollAmp * osc * env * landingRollSign;
            landingRollT = Mathf.MoveTowards(landingRollT, 0f, Time.deltaTime / landingRollDuration);
        }

        Vector3 pos = camBaseLocalPos;
        pos.y += bobOffset + kickOffsetY;
        playerCamera.transform.localPosition = pos;

        float finalRoll = Mathf.Clamp(fxRoll + rollPulse, -rollClamp, rollClamp);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch + fxPitch, 0f, finalRoll);
    }

    private void OnValidate() {
        RecomputeJumpPhysics();
        sprintStaminaMax = Mathf.Max(1f, sprintStaminaMax);
        sprintStamina = Mathf.Clamp(sprintStamina, 0f, sprintStaminaMax);
        sprintDrainPerSecond = Mathf.Max(0f, sprintDrainPerSecond);
        sprintRegenPerSecond = Mathf.Max(0f, sprintRegenPerSecond);
        sprintRegenDelay = Mathf.Max(0f, sprintRegenDelay);
    }

    private void RecomputeJumpPhysics() {
        float g = (2f * jumpHeight) / (timeToApex * timeToApex);
        customGravityY = -g;
        jumpVelocityY = g * timeToApex;
    }

    private void UpdateSprintStamina(bool wantsSprint) {
        bool canSprint =
            !isExhausted &&
            sprintStamina > 0f;

        bool willSprint = wantsSprint && canSprint;

        if (willSprint) {
            sprinting = true;
            timeSinceStoppedSprint = 0f;

            sprintStamina -= sprintDrainPerSecond * Time.deltaTime;
            if (sprintStamina <= 0f) {
                sprintStamina = 0f;
                sprinting = false;
                isExhausted = true;
                timeSinceStoppedSprint = 0f;
            }
        } else {
            if (sprinting) {
                sprinting = false;
                timeSinceStoppedSprint = 0f;
            } else {
                timeSinceStoppedSprint += Time.deltaTime;
            }

            if (timeSinceStoppedSprint >= sprintRegenDelay) {
                float prev = sprintStamina;
                sprintStamina += sprintRegenPerSecond * Time.deltaTime;
                sprintStamina = Mathf.Min(sprintStamina, sprintStaminaMax);

                if (isExhausted && Mathf.Approximately(sprintStamina, sprintStaminaMax)) {
                    isExhausted = false;
                }
            }
        }
    }
}
