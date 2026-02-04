using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Chap2MonsterController : MonoBehaviour {
    public enum MonsterBranch { Left = 0, Center = 1, Right = 2 }

    public enum MonsterState {
        Inactive,
        Approaching,
        AtCenterIdle,
        GoingToLocker,
        InvestigatingAtLocker,
        Retreating,
        Chasing,
        Vanishing
    }

    private enum MonsterExitReason {
        None,
        LookAwayRetreat,
        QteWinVanish
    }

    [Header("Monster Refs")]
    [SerializeField] private Transform monsterRoot;
    [SerializeField] private Animator monsterAnimator;
    [SerializeField] private NavMeshAgent navAgent;

    [Header("Game Refs")]
    [SerializeField] private GameManagerChap2 gameManager;

    [Header("QTE")]
    [SerializeField] private LockerQTEManager lockerQTE;

    [Header("Speed Multiplier (Global)")]
    [SerializeField, Range(1f, 1.75f)] private float SpeedMultiplier = 1f;

    [Header("Animator State Fix")]
    [SerializeField] private string locomotionStateName = "Locomotion";

    [Header("Y Path Points")]
    [SerializeField] private Transform[] branchStartPoints = new Transform[3];
    [SerializeField] private Transform centerPoint;

    [Header("Approach Settings")]
    [SerializeField] private AnimationCurve approachSpeedCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 1f);
    [SerializeField] private float maxApproachSpeed = 4.0f;

    [Header("Player View Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float viewAngle = 25f;
    [SerializeField] private float noRetreatAfterT = 0.6f;
    [SerializeField] private float lookTimeToScare = 1.2f;
    [SerializeField] private float maxViewDistance = 40f;

    [Header("Look-Away Line Of Sight")]
    [SerializeField] private bool useLineOfSightForLookAway = true;
    [SerializeField] private LayerMask lookAwayOcclusionMask = ~0;
    [SerializeField] private float lookAwayTargetHeight = 1.4f;

    [Header("Player & Spawn Proximity")]
    [SerializeField] private Transform player;
    [SerializeField] private float spawnPointProximityRadius = 4f;

    [Header("Chase Settings")]
    [SerializeField] private float chaseTriggerDistance = 6f;
    [SerializeField] private float chaseSpeedGrowthPerSec = 2f;
    [SerializeField] private float chaseMaxSpeed = 20f;
    [SerializeField] private float postLookAroundChaseMultiplier = 2f;

    [Header("Retreat Settings")]
    [SerializeField] private float retreatStartSpeed = 3.0f;
    [SerializeField] private float retreatAcceleration = 5.0f;

    [Header("Vanish Settings")]
    [SerializeField] private float vanishRunDistance = 10f;

    [Header("Locker Settings")]
    [SerializeField] private float lockerRevealDistance = 15f;
    [SerializeField] private float lockerApproachNavSpeed = 3.0f;
    [SerializeField] private float lockerArriveDist = 0.25f;

    [Header("Speed Normalization Rules")]
    [SerializeField] private float walkNormMax = 0.8f;
    [SerializeField] private float runNormMin = 0.8f;

    [Header("Audio - Footsteps (Frame Based)")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private List<AudioClip> footstepClips = new List<AudioClip>();
    [SerializeField] private float footstepMoveThreshold = 0.1f;
    [SerializeField, Range(0f, 2f)] private float runNormThreshold = 0.8f;
    [SerializeField] private float footstepAnimFps = 60f;
    [SerializeField] private int walkStepFrames = 30;
    [SerializeField] private int runStepFrames = 17;

    [Header("Respawn (Normal Feature)")]
    [SerializeField] private bool autoRespawnWhileYSequence = true;
    [SerializeField] private float respawnDelaySeconds = 3.0f;
    [SerializeField] private bool respawnIgnorePlayerView = true;
    [SerializeField] private float respawnDelayAfterLookAwaySeconds = 2.0f;
    [SerializeField] private float respawnDelayAfterQteWinSeconds = 6.0f;

    [Header("View Reaction (Normal Feature)")]
    [SerializeField] private bool defaultIgnorePlayerViewReaction = false;

    [SerializeField] private MonsterState monsterState = MonsterState.Inactive;
    private MonsterBranch currentBranch = MonsterBranch.Center;

    private float approachT = 0f;
    private float currentMoveSpeed = 0f;
    private float currentMaxDistance = 0f;

    private float approachPathLength = 0f;
    private bool approachPathReady = false;
    private Vector3 approachCenterDest = Vector3.zero;

    private Vector3 branchStartPos;
    private Vector3 centerPos;
    private float groundY = 0f;

    private float lookTimer = 0f;
    private float cosViewThreshold;

    private bool hasPendingRespawn = false;
    private MonsterBranch pendingRespawnBranch;
    private bool ignoreViewForCurrentRun = false;

    private float chaseTimer = 0f;
    private float chaseBaseSpeed = 0f;
    private float chaseSpeedMultiplier = 1f;

    private float respawnTimer = 0f;
    private Coroutine waitCoroutine;
    private MonsterExitReason lastExitReason = MonsterExitReason.None;

    private bool forceAllowViewReactionThisSpawn = false;

    private bool hasScheduledSpawn = false;
    private bool scheduledSpawnUseBranch = false;
    private MonsterBranch scheduledSpawnBranch = MonsterBranch.Center;
    private bool scheduledSpawnIgnoreView = false;

    private LockerInteractable currentLocker;
    private Transform currentLockerPoint;
    private bool playerCurrentlyInLocker = false;
    private bool lockerWasRevealedOnHide = false;

    private float hideDistanceRecord = 999f;
    private bool qteStartedForThisLocker = false;

    private bool hasDoneCenterLookAround = false;

    private float footstepTimer = 0f;
    private int lastFootstepIndex = -1;
    private bool wasRunningForSteps = false;

    private float retreatDesiredSpeed = 0f;

    private Vector3 vanishTargetPos;
    private float vanishRunSpeed = 0f;

    private float ClampedSpeedMultiplier => Mathf.Clamp(SpeedMultiplier, 1f, 1.75f);

    void Start() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                player = pc.transform;
        }

        if (playerCamera == null) {
            if (Camera.main != null)
                playerCamera = Camera.main;
        }

        if (centerPoint != null)
            centerPos = centerPoint.position;

        cosViewThreshold = Mathf.Cos(viewAngle * Mathf.Deg2Rad);

        if (navAgent != null)
            navAgent.enabled = false;

        if (lockerQTE == null)
            lockerQTE = LockerQTEManager.Instance;
    }

    void Update() {
        UpdateMonster();
        UpdateFootstepAudio(currentMoveSpeed);

        if (monsterState != MonsterState.Chasing &&
            monsterState != MonsterState.GoingToLocker &&
            monsterState != MonsterState.Retreating &&
            monsterState != MonsterState.InvestigatingAtLocker &&
            monsterState != MonsterState.Vanishing) {
            CheckPlayerNearSpawnPoints();
            TryStartChaseByDistance();
        }

        UpdateAutoRespawn();
    }

    private float GetRespawnDelaySeconds() {
        if (lastExitReason == MonsterExitReason.LookAwayRetreat)
            return Mathf.Max(0f, respawnDelayAfterLookAwaySeconds);

        if (lastExitReason == MonsterExitReason.QteWinVanish)
            return Mathf.Max(0f, respawnDelayAfterQteWinSeconds);

        return Mathf.Max(0f, respawnDelaySeconds);
    }

    private void UpdateAutoRespawn() {
        if (!autoRespawnWhileYSequence)
            return;

        if (monsterState != MonsterState.Inactive) {
            respawnTimer = 0f;
            return;
        }

        if (gameManager == null || gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return;

        if (gameManager.IsMonsterSpawnSuppressed)
            return;

        respawnTimer += Time.deltaTime;
        if (respawnTimer < GetRespawnDelaySeconds())
            return;

        respawnTimer = 0f;

        if (hasScheduledSpawn) {
            bool useBranch = scheduledSpawnUseBranch;
            MonsterBranch b = scheduledSpawnBranch;
            bool ignoreView = scheduledSpawnIgnoreView;

            hasScheduledSpawn = false;
            scheduledSpawnUseBranch = false;

            if (useBranch)
                StartFromBranch(b, ignoreView);
            else
                StartFromRandomBranch(ignoreView);

            return;
        }

        if (lastExitReason == MonsterExitReason.LookAwayRetreat)
            forceAllowViewReactionThisSpawn = true;
        
        StartFromRandomBranch(false);
    }

    public bool IsCompletelyGone {
        get {
            if (monsterState == MonsterState.Inactive)
                return true;

            if (monsterRoot == null)
                return true;

            return !monsterRoot.gameObject.activeSelf;
        }
    }

    public MonsterState State => monsterState;

    public void StartFromRandomBranch(bool ignoreView) {
        int idx = Random.Range(0, 3);
        StartFromBranch((MonsterBranch)idx, ignoreView);
    }

    public void StartFromBranch(MonsterBranch branch, bool ignoreView) {
        if (monsterRoot == null || centerPoint == null)
            return;

        int index = (int)branch;
        if (branchStartPoints == null || index >= branchStartPoints.Length || branchStartPoints[index] == null)
            return;

        currentBranch = branch;
        lastExitReason = MonsterExitReason.None;
        respawnTimer = 0f;
        if (forceAllowViewReactionThisSpawn)
            ignoreViewForCurrentRun = false;
        else
            ignoreViewForCurrentRun = defaultIgnorePlayerViewReaction || ignoreView;

        forceAllowViewReactionThisSpawn = false;
        hasScheduledSpawn = false;
        scheduledSpawnUseBranch = false;

        branchStartPos = branchStartPoints[index].position;
        groundY = branchStartPos.y;
        branchStartPos.y = groundY;

        centerPos = centerPoint.position;
        centerPos.y = groundY;

        DisableNavAgentHard();

        monsterRoot.position = branchStartPos;
        LookAt(centerPos);

        currentMaxDistance = Vector3.Distance(branchStartPos, centerPos);
        if (currentMaxDistance < 0.01f)
            currentMaxDistance = 0.01f;

        approachT = 0f;
        currentMoveSpeed = 0f;
        SetAnimSpeedNorm(0f);

        lookTimer = 0f;
        hasPendingRespawn = false;
        chaseTimer = 0f;
        chaseSpeedMultiplier = 1f;

        currentLocker = null;
        currentLockerPoint = null;
        playerCurrentlyInLocker = false;
        lockerWasRevealedOnHide = false;

        hideDistanceRecord = 999f;
        qteStartedForThisLocker = false;

        footstepTimer = 0f;
        wasRunningForSteps = false;
        lastFootstepIndex = -1;

        retreatDesiredSpeed = 0f;
        vanishTargetPos = Vector3.zero;
        vanishRunSpeed = 0f;

        if (!monsterRoot.gameObject.activeSelf)
            monsterRoot.gameObject.SetActive(true);

        approachCenterDest = centerPos;
        approachPathReady = false;
        approachPathLength = 0f;

        if (navAgent != null)
            BeginApproachNav();

        monsterState = MonsterState.Approaching;
    }

    public void StartFromCustomSpawnPoint(Transform spawnPoint, bool ignoreView) {
        if (monsterRoot == null || centerPoint == null)
            return;

        if (spawnPoint == null)
            return;

        currentBranch = MonsterBranch.Center;
        lastExitReason = MonsterExitReason.None;
        respawnTimer = 0f;

        if (forceAllowViewReactionThisSpawn)
            ignoreViewForCurrentRun = false;
        else
            ignoreViewForCurrentRun = defaultIgnorePlayerViewReaction || ignoreView;

        forceAllowViewReactionThisSpawn = false;
        hasScheduledSpawn = false;
        scheduledSpawnUseBranch = false;

        branchStartPos = spawnPoint.position;
        groundY = branchStartPos.y;
        branchStartPos.y = groundY;

        centerPos = centerPoint.position;
        centerPos.y = groundY;

        DisableNavAgentHard();

        monsterRoot.position = branchStartPos;
        LookAt(centerPos);

        currentMaxDistance = Vector3.Distance(branchStartPos, centerPos);
        if (currentMaxDistance < 0.01f)
            currentMaxDistance = 0.01f;

        approachT = 0f;
        currentMoveSpeed = 0f;
        SetAnimSpeedNorm(0f);

        hasDoneCenterLookAround = false;
        lookTimer = 0f;
        hasPendingRespawn = false;
        chaseTimer = 0f;
        chaseSpeedMultiplier = 1f;

        currentLocker = null;
        currentLockerPoint = null;
        playerCurrentlyInLocker = false;
        lockerWasRevealedOnHide = false;

        hideDistanceRecord = 999f;
        qteStartedForThisLocker = false;

        footstepTimer = 0f;
        wasRunningForSteps = false;
        lastFootstepIndex = -1;

        retreatDesiredSpeed = 0f;
        vanishTargetPos = Vector3.zero;
        vanishRunSpeed = 0f;

        if (!monsterRoot.gameObject.activeSelf)
            monsterRoot.gameObject.SetActive(true);

        approachCenterDest = centerPos;
        approachPathReady = false;
        approachPathLength = 0f;

        if (navAgent != null)
            BeginApproachNav();

        monsterState = MonsterState.Approaching;
    }

    public void ForceStartChase(float speedMult = 1f) {
        StartChase(speedMult);
    }

    public void BeginYSequenceSpawnDelay() {
        ForceHide();

        lastExitReason = MonsterExitReason.QteWinVanish;
        respawnTimer = 0f;

        hasScheduledSpawn = true;
        scheduledSpawnUseBranch = false;
        scheduledSpawnIgnoreView = false;
    }

    public void BeginYSequenceSpawnDelayFromBranch(MonsterBranch branch, bool ignoreView = false) {
        ForceHide();

        lastExitReason = MonsterExitReason.QteWinVanish;
        respawnTimer = 0f;

        hasScheduledSpawn = true;
        scheduledSpawnUseBranch = true;
        scheduledSpawnBranch = branch;
        scheduledSpawnIgnoreView = ignoreView;
    }

    public void ForceHide() {
        if (waitCoroutine != null)
            StopCoroutine(waitCoroutine);

        DisableNavAgentHard();

        if (monsterRoot != null)
            monsterRoot.gameObject.SetActive(false);

        monsterState = MonsterState.Inactive;
        hasPendingRespawn = false;
        respawnTimer = 0f;

        currentLocker = null;
        currentLockerPoint = null;
        playerCurrentlyInLocker = false;
        lockerWasRevealedOnHide = false;

        hideDistanceRecord = 999f;
        qteStartedForThisLocker = false;

        footstepTimer = 0f;
        retreatDesiredSpeed = 0f;

        vanishTargetPos = Vector3.zero;
        vanishRunSpeed = 0f;

        approachPathReady = false;
        approachPathLength = 0f;
        approachCenterDest = Vector3.zero;
    }

    public void NotifyPlayerHiding(LockerInteractable locker, Transform lockerOutsidePoint) {
        if (monsterState == MonsterState.Inactive || monsterState == MonsterState.Retreating || monsterState == MonsterState.Vanishing)
            return;

        currentLocker = locker;
        currentLockerPoint = lockerOutsidePoint;
        playerCurrentlyInLocker = true;

        if (monsterRoot != null && currentLockerPoint != null) {
            float dist = Vector3.Distance(monsterRoot.position, currentLockerPoint.position);
            hideDistanceRecord = dist;

            if (dist <= lockerRevealDistance)
                lockerWasRevealedOnHide = true;
        }

        if (monsterState == MonsterState.Chasing)
            StartGoToLockerNav(true);
    }

    public void NotifyPlayerExiting() {
        playerCurrentlyInLocker = false;
        qteStartedForThisLocker = false;

        if (monsterState == MonsterState.GoingToLocker ||
            monsterState == MonsterState.InvestigatingAtLocker ||
            monsterState == MonsterState.AtCenterIdle)
            StartChase(1f);

        currentLocker = null;
        currentLockerPoint = null;
        lockerWasRevealedOnHide = false;
        hideDistanceRecord = 999f;
    }

    public void OnLockerQTESuccess() {
        if (waitCoroutine != null)
            StopCoroutine(waitCoroutine);

        lastExitReason = MonsterExitReason.QteWinVanish;
        waitCoroutine = StartCoroutine(CoStareThenVanish());
    }

    public void OnLockerQTEFail() {
        if (gameManager != null && gameManager.PreventPlayerDeath)
            return;

        if (DeathManager.Instance == null)
            return;

        if (!DeathManager.Instance.EnableDeath)
            return;

        PrepareForDeathCinematic();
    }

    public void PrepareForDeathCinematic() {
        if (gameManager != null && gameManager.PreventPlayerDeath)
            return;

        if (waitCoroutine != null)
            StopCoroutine(waitCoroutine);

        waitCoroutine = null;
        DisableNavAgentHard();

        currentMoveSpeed = 0f;
        footstepTimer = 0f;

        if (footstepSource != null)
            footstepSource.Stop();

        if (monsterAnimator != null)
            monsterAnimator.enabled = false;

        enabled = false;
    }

    private IEnumerator CoStareThenVanish() {
        if (currentLockerPoint != null)
            LookAt(currentLockerPoint.position);

        yield return new WaitForSeconds(1f);
        StartVanishLeftRun();
    }

    private void UpdateMonster() {
        switch (monsterState) {
            case MonsterState.Approaching: UpdateApproach(); break;
            case MonsterState.AtCenterIdle: break;
            case MonsterState.GoingToLocker: UpdateGoToLocker(); break;
            case MonsterState.InvestigatingAtLocker: UpdateInvestigatingAtLocker(); break;
            case MonsterState.Retreating: UpdateRetreat(); break;
            case MonsterState.Chasing: UpdateChase(); break;
            case MonsterState.Vanishing: UpdateVanish(); break;
        }
    }

    private void UpdateApproach() {
        if (monsterRoot == null)
            return;

        if (navAgent != null) {
            if (!navAgent.enabled)
                BeginApproachNav();

            if (navAgent.enabled) {
                if (navAgent.pathPending) {
                    currentMoveSpeed = 0f;
                    SetAnimSpeedNorm(0f);
                    LookAt(centerPos);
                    CheckPlayerViewAndMaybeRetreat();
                    return;
                }

                if (!approachPathReady) {
                    approachPathLength = CalculatePathLength(navAgent.path);
                    if (approachPathLength < 0.01f)
                        approachPathLength = Mathf.Max(0.01f, Vector3.Distance(monsterRoot.position, approachCenterDest));

                    currentMaxDistance = approachPathLength;
                    approachPathReady = true;
                }

                float remain = navAgent.remainingDistance;
                if (float.IsInfinity(remain) || float.IsNaN(remain))
                    remain = Vector3.Distance(monsterRoot.position, approachCenterDest);

                approachT = 1f - Mathf.Clamp01(remain / Mathf.Max(0.01f, approachPathLength));

                float speedRatio = Mathf.Clamp01(approachSpeedCurve.Evaluate(approachT));
                float baseSpeed = maxApproachSpeed * speedRatio;
                float targetSpeed = baseSpeed * ClampedSpeedMultiplier;

                navAgent.speed = targetSpeed;
                navAgent.acceleration = Mathf.Max(navAgent.acceleration, navAgent.speed * 6f);

                if ((navAgent.destination - approachCenterDest).sqrMagnitude > 0.01f)
                    navAgent.SetDestination(approachCenterDest);

                Vector3 vel = navAgent.velocity;
                vel.y = 0f;
                currentMoveSpeed = vel.magnitude;

                if (vel.sqrMagnitude > 0.001f)
                    monsterRoot.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
                else
                    LookAt(centerPos);

                SetAnimSpeedFromWorldSpeed(currentMoveSpeed);
                CheckPlayerViewAndMaybeRetreat();

                if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.05f) {
                    Vector3 p = centerPos;
                    p.y = groundY;
                    monsterRoot.position = p;

                    DisableNavAgentHard();
                    OnReachedCenter();
                }

                return;
            }
        }

        float speedRatioFallback = Mathf.Clamp01(approachSpeedCurve.Evaluate(approachT));
        float baseSpeedFallback = maxApproachSpeed * speedRatioFallback;
        currentMoveSpeed = baseSpeedFallback * ClampedSpeedMultiplier;

        float deltaDist = currentMoveSpeed * Time.deltaTime;
        approachT += deltaDist / Mathf.Max(0.01f, currentMaxDistance);

        if (approachT >= 1f) {
            approachT = 1f;
            monsterRoot.position = centerPos;
            OnReachedCenter();
        } else {
            Vector3 newPos = Vector3.Lerp(branchStartPos, centerPos, approachT);
            newPos.y = groundY;
            monsterRoot.position = newPos;
            LookAt(centerPos);
        }

        SetAnimSpeedFromWorldSpeed(currentMoveSpeed);
        CheckPlayerViewAndMaybeRetreat();
    }

    private float CalculatePathLength(NavMeshPath path) {
        if (path == null)
            return 0f;

        var corners = path.corners;
        if (corners == null || corners.Length < 2)
            return 0f;

        float sum = 0f;
        for (int i = 1; i < corners.Length; i++)
            sum += Vector3.Distance(corners[i - 1], corners[i]);

        return sum;
    }

    private void BeginApproachNav() {
        if (navAgent == null || monsterRoot == null)
            return;

        approachCenterDest = centerPos;
        if (NavMesh.SamplePosition(centerPos, out NavMeshHit centerHit, 2.0f, NavMesh.AllAreas))
            approachCenterDest = centerHit.position;

        if (!navAgent.enabled)
            navAgent.enabled = true;

        Vector3 start = monsterRoot.position;
        if (NavMesh.SamplePosition(start, out NavMeshHit startHit, 2.0f, NavMesh.AllAreas))
            navAgent.Warp(startHit.position);
        else
            navAgent.Warp(start);

        navAgent.updatePosition = true;
        navAgent.updateRotation = false;
        navAgent.isStopped = false;
        navAgent.stoppingDistance = 0.05f;

        navAgent.speed = 0.01f;
        navAgent.acceleration = Mathf.Max(navAgent.acceleration, maxApproachSpeed * 6f);

        navAgent.SetDestination(approachCenterDest);

        approachPathReady = false;
        approachPathLength = 0f;
    }

    private void OnReachedCenter() {
        DisableNavAgentHard();
        currentMoveSpeed = 0f;
        SetAnimSpeedNorm(0f);

        bool playerHiddenNow = (playerCurrentlyInLocker && currentLocker != null && currentLocker.IsHidden);

        if (hasDoneCenterLookAround) {
            if (!playerHiddenNow) {
                StartChase(1f);
                return;
            }

            if (lockerWasRevealedOnHide)
                StartGoToLockerNav(false);
            else
                StartVanishLeftRun();

            return;
        }

        hasDoneCenterLookAround = true;

        monsterState = MonsterState.AtCenterIdle;
        lookTimer = 0f;

        if (monsterAnimator != null) {
            monsterAnimator.ResetTrigger("LookAround");
            monsterAnimator.SetTrigger("LookAround");
        }

        if (waitCoroutine != null)
            StopCoroutine(waitCoroutine);

        waitCoroutine = StartCoroutine(CoWaitAndDecide(3.0f));
    }

    private IEnumerator CoWaitAndDecide(float delay) {
        yield return new WaitForSeconds(delay);

        if (monsterState != MonsterState.AtCenterIdle)
            yield break;

        bool playerHiddenNow = (playerCurrentlyInLocker && currentLocker != null && currentLocker.IsHidden);

        if (!playerHiddenNow) {
            StartChase(postLookAroundChaseMultiplier);
            yield break;
        }

        if (lockerWasRevealedOnHide)
            StartGoToLockerNav(false);
        else
            StartVanishLeftRun();
    }

    private void StartGoToLockerNav(bool preserveSpeedFromChase) {
        if (currentLockerPoint == null || navAgent == null) {
            StartChase(1f);
            return;
        }

        ForceLocomotion();

        if (waitCoroutine != null)
            StopCoroutine(waitCoroutine);

        bool alreadyEnabled = navAgent.enabled;
        float prevSpeed = navAgent.speed;

        if (!alreadyEnabled) {
            navAgent.enabled = true;
            navAgent.Warp(monsterRoot.position);
            navAgent.updatePosition = true;
            navAgent.updateRotation = false;
        }

        navAgent.stoppingDistance = lockerArriveDist;

        if (preserveSpeedFromChase && alreadyEnabled) {
            navAgent.speed = prevSpeed;
            navAgent.acceleration = Mathf.Max(navAgent.acceleration, navAgent.speed * 4f);
        } else {
            float maxWalkSpeed = maxApproachSpeed * walkNormMax;
            float baseNavSpeed = Mathf.Min(lockerApproachNavSpeed, maxWalkSpeed);

            navAgent.speed = baseNavSpeed * ClampedSpeedMultiplier;
            navAgent.acceleration = navAgent.speed * 6f;
        }

        navAgent.SetDestination(currentLockerPoint.position);
        monsterState = MonsterState.GoingToLocker;
    }

    private void UpdateGoToLocker() {
        if (navAgent == null || !navAgent.enabled) {
            monsterState = MonsterState.Inactive;
            return;
        }

        if (!playerCurrentlyInLocker || currentLocker == null || !currentLocker.IsHidden) {
            StartChase(1f);
            return;
        }

        if (currentLockerPoint == null) {
            StartChase(1f);
            return;
        }

        navAgent.SetDestination(currentLockerPoint.position);

        Vector3 vel = navAgent.velocity;
        vel.y = 0f;
        currentMoveSpeed = vel.magnitude;

        SetAnimSpeedFromWorldSpeedClamped(currentMoveSpeed, 0f, walkNormMax);

        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.05f) {
            Vector3 p = currentLockerPoint.position;
            p.y = groundY;
            monsterRoot.position = p;
            monsterRoot.rotation = currentLockerPoint.rotation;

            DisableNavAgentHard();
            StartInvestigatingAtLocker();
            return;
        }

        if (vel.sqrMagnitude > 0.001f)
            monsterRoot.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
    }

    private void StartInvestigatingAtLocker() {
        monsterState = MonsterState.InvestigatingAtLocker;
        currentMoveSpeed = 0f;
        SetAnimSpeedNorm(0f);
        qteStartedForThisLocker = false;
    }

    private void UpdateInvestigatingAtLocker() {
        if (!playerCurrentlyInLocker || currentLocker == null || !currentLocker.IsHidden) {
            StartChase(1f);
            return;
        }

        if (!qteStartedForThisLocker && lockerQTE != null) {
            qteStartedForThisLocker = true;
            lockerQTE.BeginQTE(currentLocker, hideDistanceRecord, OnLockerQTESuccess, OnLockerQTEFail);
        }
    }

    private void StartChase(float speedMult) {
        if (playerCurrentlyInLocker)
            return;

        ForceLocomotion();

        if (waitCoroutine != null)
            StopCoroutine(waitCoroutine);

        monsterState = MonsterState.Chasing;
        hasPendingRespawn = false;

        chaseSpeedMultiplier = Mathf.Max(0.1f, speedMult);

        if (navAgent != null) {
            navAgent.enabled = true;
            navAgent.Warp(monsterRoot.position);
            navAgent.updatePosition = true;
            navAgent.updateRotation = false;

            chaseBaseSpeed = Mathf.Max(currentMoveSpeed, 0.1f);
            chaseTimer = 0f;

            navAgent.speed = chaseBaseSpeed * chaseSpeedMultiplier * ClampedSpeedMultiplier;
        }
    }

    private void UpdateChase() {
        if (navAgent == null || !navAgent.enabled || player == null)
            return;

        chaseTimer += Time.deltaTime;

        float targetSpeed = chaseBaseSpeed + chaseSpeedGrowthPerSec * chaseTimer;
        if (chaseMaxSpeed > 0f)
            targetSpeed = Mathf.Min(targetSpeed, chaseMaxSpeed);

        targetSpeed *= chaseSpeedMultiplier * ClampedSpeedMultiplier;

        navAgent.speed = targetSpeed;
        navAgent.acceleration = navAgent.speed * 4f;
        navAgent.SetDestination(player.position);

        Vector3 vel = navAgent.velocity;
        vel.y = 0f;
        currentMoveSpeed = vel.magnitude;

        if (currentMoveSpeed > 0.01f)
            monsterRoot.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);

        SetAnimSpeedFromWorldSpeed(currentMoveSpeed);
    }

    private void StartRetreat(bool useAcceleration) {
        ForceLocomotion();
        monsterState = MonsterState.Retreating;

        int index = (int)currentBranch;
        if (branchStartPoints == null || index < 0 || index >= branchStartPoints.Length || branchStartPoints[index] == null) {
            ForceHide();
            return;
        }

        float minRunSpeed = maxApproachSpeed * runNormMin * ClampedSpeedMultiplier;

        if (useAcceleration)
            retreatDesiredSpeed = Mathf.Max(retreatStartSpeed * ClampedSpeedMultiplier, minRunSpeed);
        else
            retreatDesiredSpeed = Mathf.Max(maxApproachSpeed * 2.5f * ClampedSpeedMultiplier, minRunSpeed);

        if (navAgent == null) {
            currentMoveSpeed = retreatDesiredSpeed;
            SetAnimSpeedFromWorldSpeedClamped(currentMoveSpeed, runNormMin, 999f);
            return;
        }

        navAgent.enabled = true;
        navAgent.Warp(monsterRoot.position);
        navAgent.updatePosition = true;
        navAgent.updateRotation = false;

        navAgent.stoppingDistance = 0.05f;
        navAgent.speed = retreatDesiredSpeed;
        navAgent.acceleration = navAgent.speed * 6f;

        navAgent.SetDestination(branchStartPoints[index].position);

        currentMoveSpeed = retreatDesiredSpeed;
        SetAnimSpeedFromWorldSpeedClamped(currentMoveSpeed, runNormMin, 999f);
    }

    private void UpdateRetreat() {
        int index = (int)currentBranch;
        if (branchStartPoints == null || index < 0 || index >= branchStartPoints.Length || branchStartPoints[index] == null) {
            ForceHide();
            return;
        }

        retreatDesiredSpeed += retreatAcceleration * ClampedSpeedMultiplier * Time.deltaTime;

        if (navAgent == null || !navAgent.enabled) {
            Vector3 target = branchStartPoints[index].position;
            target.y = groundY;

            Vector3 dir = target - monsterRoot.position;
            dir.y = 0f;

            if (dir.magnitude < 0.2f) {
                ForceHide();

                if (hasPendingRespawn) {
                    var branch = pendingRespawnBranch;
                    hasPendingRespawn = false;
                    StartFromBranch(branch, true);
                }
                return;
            }

            dir.Normalize();
            monsterRoot.position += dir * retreatDesiredSpeed * Time.deltaTime;
            LookAt(target);

            currentMoveSpeed = retreatDesiredSpeed;
            SetAnimSpeedFromWorldSpeedClamped(currentMoveSpeed, runNormMin, 999f);
            return;
        }

        navAgent.speed = retreatDesiredSpeed;
        navAgent.acceleration = navAgent.speed * 6f;
        navAgent.SetDestination(branchStartPoints[index].position);

        Vector3 vel = navAgent.velocity;
        vel.y = 0f;
        currentMoveSpeed = vel.magnitude;

        if (vel.sqrMagnitude > 0.001f)
            monsterRoot.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
        else
            LookAt(branchStartPoints[index].position);

        SetAnimSpeedFromWorldSpeedClamped(Mathf.Max(retreatDesiredSpeed, currentMoveSpeed), runNormMin, 999f);

        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.12f) {
            ForceHide();

            if (hasPendingRespawn) {
                var branch = pendingRespawnBranch;
                hasPendingRespawn = false;
                StartFromBranch(branch, true);
            }
        }
    }

    private void StartVanishLeftRun() {
        if (monsterRoot == null) {
            ForceHide();
            return;
        }

        ForceLocomotion();

        Vector3 leftDir = -monsterRoot.right;
        leftDir.y = 0f;

        if (leftDir.sqrMagnitude < 0.0001f)
            leftDir = -monsterRoot.forward;

        leftDir.Normalize();

        vanishTargetPos = monsterRoot.position + leftDir * Mathf.Max(0.1f, vanishRunDistance);
        vanishTargetPos.y = groundY;

        float minRunSpeed = maxApproachSpeed * runNormMin * ClampedSpeedMultiplier;
        vanishRunSpeed = Mathf.Max(maxApproachSpeed * 1f * ClampedSpeedMultiplier, minRunSpeed);

        monsterRoot.rotation = Quaternion.LookRotation(leftDir, Vector3.up);

        if (navAgent != null) {
            navAgent.enabled = true;
            navAgent.Warp(monsterRoot.position);
            navAgent.updatePosition = true;
            navAgent.updateRotation = false;

            navAgent.stoppingDistance = 0.05f;
            navAgent.speed = vanishRunSpeed;
            navAgent.acceleration = navAgent.speed * 6f;

            navAgent.SetDestination(vanishTargetPos);
        }

        currentMoveSpeed = vanishRunSpeed;
        SetAnimSpeedFromWorldSpeedClamped(currentMoveSpeed, runNormMin, 999f);

        monsterState = MonsterState.Vanishing;
    }

    private void UpdateVanish() {
        if (monsterRoot == null) {
            ForceHide();
            return;
        }

        if (navAgent != null && navAgent.enabled) {
            navAgent.speed = vanishRunSpeed;
            navAgent.acceleration = navAgent.speed * 6f;
            navAgent.SetDestination(vanishTargetPos);

            Vector3 vel = navAgent.velocity;
            vel.y = 0f;
            currentMoveSpeed = vel.magnitude;

            if (vel.sqrMagnitude > 0.001f)
                monsterRoot.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);

            SetAnimSpeedFromWorldSpeedClamped(Mathf.Max(vanishRunSpeed, currentMoveSpeed), runNormMin, 999f);

            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.12f) {
                ForceHide();
                return;
            }

            return;
        }

        Vector3 dir = vanishTargetPos - monsterRoot.position;
        dir.y = 0f;

        if (dir.magnitude < 0.15f) {
            ForceHide();
            return;
        }

        dir.Normalize();
        monsterRoot.position += dir * vanishRunSpeed * Time.deltaTime;
        monsterRoot.rotation = Quaternion.LookRotation(dir, Vector3.up);

        currentMoveSpeed = vanishRunSpeed;
        SetAnimSpeedFromWorldSpeedClamped(currentMoveSpeed, runNormMin, 999f);
    }

    private void UpdateFootstepAudio(float horizontalSpeed) {
        bool stateAllowsFootsteps =
            monsterState == MonsterState.Approaching ||
            monsterState == MonsterState.GoingToLocker ||
            monsterState == MonsterState.Retreating ||
            monsterState == MonsterState.Chasing ||
            monsterState == MonsterState.Vanishing;

        bool moving =
            stateAllowsFootsteps &&
            monsterRoot != null &&
            monsterRoot.gameObject.activeSelf &&
            horizontalSpeed > footstepMoveThreshold;

        if (!moving) {
            footstepTimer = 0f;
            float norm0 = (maxApproachSpeed > 0f) ? (horizontalSpeed / maxApproachSpeed) : 0f;
            wasRunningForSteps = norm0 >= runNormThreshold;
            return;
        }

        float norm = (maxApproachSpeed > 0f) ? (horizontalSpeed / maxApproachSpeed) : 0f;
        bool running = norm >= runNormThreshold;

        float baseInterval = GetBaseFootstepIntervalSeconds(running);

        float tempoMult = ClampedSpeedMultiplier;
        if (monsterState == MonsterState.Chasing)
            tempoMult *= Mathf.Max(0.1f, chaseSpeedMultiplier);

        float interval = baseInterval / Mathf.Max(0.01f, tempoMult);

        if (running != wasRunningForSteps) {
            PlayFootstep();
            footstepTimer = interval;
            wasRunningForSteps = running;
            return;
        }

        if (footstepTimer > interval)
            footstepTimer = interval;

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f) {
            PlayFootstep();
            footstepTimer = interval;
        }
    }

    private float GetBaseFootstepIntervalSeconds(bool running) {
        float fps = Mathf.Max(1f, footstepAnimFps);

        if (!running) {
            int frames = Mathf.Max(1, walkStepFrames);
            return frames / fps;
        }

        int rFrames = Mathf.Max(1, runStepFrames);
        return rFrames / fps;
    }

    private void PlayFootstep() {
        if (footstepSource == null)
            return;

        if (footstepClips == null || footstepClips.Count == 0)
            return;

        int count = footstepClips.Count;
        int index = Random.Range(0, count);

        if (count > 1 && index == lastFootstepIndex)
            index = (index + 1) % count;

        lastFootstepIndex = index;
        footstepSource.PlayOneShot(footstepClips[index]);
    }

    private void DisableNavAgentHard() {
        if (navAgent == null)
            return;

        if (navAgent.enabled) {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }
        navAgent.enabled = false;
    }

    private void ForceLocomotion() {
        if (monsterAnimator == null)
            return;

        monsterAnimator.ResetTrigger("LookAround");

        if (!string.IsNullOrEmpty(locomotionStateName))
            monsterAnimator.CrossFadeInFixedTime(locomotionStateName, 0.05f, 0);
    }

    private void LookAt(Vector3 target) {
        if (monsterRoot == null)
            return;

        Vector3 dir = target - monsterRoot.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
            monsterRoot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void SetAnimSpeedNorm(float norm) {
        if (monsterAnimator == null)
            return;

        monsterAnimator.SetFloat("Speed", norm);
    }

    private void SetAnimSpeedFromWorldSpeed(float worldSpeed) {
        float norm = (maxApproachSpeed > 0f) ? (worldSpeed / maxApproachSpeed) : 0f;
        SetAnimSpeedNorm(norm);
    }

    private void SetAnimSpeedFromWorldSpeedClamped(float worldSpeed, float minNorm, float maxNorm) {
        float norm = (maxApproachSpeed > 0f) ? (worldSpeed / maxApproachSpeed) : 0f;
        norm = Mathf.Clamp(norm, minNorm, maxNorm);
        SetAnimSpeedNorm(norm);
    }

    private void TryStartChaseByDistance() {
        if (player == null || monsterRoot == null)
            return;

        if (playerCurrentlyInLocker)
            return;

        if (monsterState != MonsterState.Approaching &&
            monsterState != MonsterState.AtCenterIdle)
            return;

        float dist = Vector3.Distance(player.position, monsterRoot.position);
        if (dist <= chaseTriggerDistance)
            StartChase(1f);
    }

    private void CheckPlayerViewAndMaybeRetreat() {
        if (playerCamera == null || monsterState != MonsterState.Approaching)
            return;

        if (ignoreViewForCurrentRun || approachT >= noRetreatAfterT)
            return;

        Vector3 camPos = playerCamera.transform.position;
        Vector3 camFwd = playerCamera.transform.forward;
        Vector3 toMonster = monsterRoot.position - camPos;

        if (toMonster.sqrMagnitude > maxViewDistance * maxViewDistance) {
            lookTimer = Mathf.Max(0f, lookTimer - Time.deltaTime * 0.5f);
            return;
        }

        if (useLineOfSightForLookAway) {
            Vector3 losTargetPos = monsterRoot.position + Vector3.up * lookAwayTargetHeight;
            if (!HasLookAwayLineOfSight(camPos, losTargetPos)) {
                lookTimer = Mathf.Max(0f, lookTimer - Time.deltaTime * 0.5f);
                return;
            }
        }

        toMonster.y = 0f;
        if (toMonster.sqrMagnitude < 0.0001f)
            return;

        toMonster.Normalize();

        if (Vector3.Dot(camFwd, toMonster) >= cosViewThreshold) {
            lookTimer += Time.deltaTime;
            if (lookTimer >= lookTimeToScare) {
                lastExitReason = MonsterExitReason.LookAwayRetreat;
                StartRetreat(false);
                lookTimer = 0f;
            }
        } else {
            lookTimer = Mathf.Max(0f, lookTimer - Time.deltaTime * 0.5f);
        }
    }

    bool HasLookAwayLineOfSight(Vector3 from, Vector3 to) {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.001f)
            return true;

        dir /= dist;

        RaycastHit[] hits = Physics.RaycastAll(from, dir, dist, lookAwayOcclusionMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return true;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++) {
            Transform hitTr = hits[i].transform;
            if (hitTr == null)
                continue;

            if (player != null && hitTr.IsChildOf(player))
                continue;

            if (monsterRoot != null && hitTr.IsChildOf(monsterRoot))
                return true;

            return false;
        }

        return true;
    }

    private void CheckPlayerNearSpawnPoints() {
        if (player == null || branchStartPoints == null)
            return;

        float radiusSqr = spawnPointProximityRadius * spawnPointProximityRadius;
        int nearIndex = -1;
        float bestDistSqr = float.MaxValue;
        Vector3 playerPos = player.position;

        for (int i = 0; i < branchStartPoints.Length; i++) {
            if (branchStartPoints[i] == null)
                continue;

            float dSqr = (playerPos - branchStartPoints[i].position).sqrMagnitude;
            if (dSqr <= radiusSqr && dSqr < bestDistSqr) {
                bestDistSqr = dSqr;
                nearIndex = i;
            }
        }

        if (nearIndex < 0)
            return;

        MonsterBranch nearBranch = (MonsterBranch)nearIndex;

        if (monsterState == MonsterState.Inactive || monsterRoot == null || !monsterRoot.gameObject.activeSelf) {
            StartFromBranch(nearBranch, true);
            return;
        }

        if (monsterState == MonsterState.Approaching && ignoreViewForCurrentRun && nearBranch == currentBranch)
            return;

        if (hasPendingRespawn)
            return;

        hasPendingRespawn = true;
        pendingRespawnBranch = nearBranch;

        float fastSpeed = maxApproachSpeed * 5f * ClampedSpeedMultiplier;

        if (monsterState == MonsterState.Retreating) {
            retreatDesiredSpeed = Mathf.Max(retreatDesiredSpeed, fastSpeed);

            if (navAgent != null && navAgent.enabled) {
                navAgent.speed = retreatDesiredSpeed;
                navAgent.acceleration = navAgent.speed * 6f;
            }
            return;
        }

        StartRetreat(false);
        retreatDesiredSpeed = Mathf.Max(retreatDesiredSpeed, fastSpeed);

        if (navAgent != null && navAgent.enabled) {
            navAgent.speed = retreatDesiredSpeed;
            navAgent.acceleration = navAgent.speed * 6f;
        }
    }
}