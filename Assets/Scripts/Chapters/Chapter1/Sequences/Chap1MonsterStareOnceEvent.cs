using System.Collections;
using UnityEngine;

public class Chap1MonsterStareOnceEvent : MonoBehaviour {
    enum Phase {
        MovingToStareSpot,
        WaitingMonsterStare,
        WaitingPlayerLook,
        Relocating,
        Completed
    }

    [Header("Save / Once Per Save (Per Instance)")]
    [SerializeField] string checkpointConsumeId = "Chap1_StareEvent_00";
    [SerializeField] bool disableSelfIfConsumed = true;
    [SerializeField] bool disableMonsterIfConsumed = false;

    [Header("Refs")]
    [SerializeField] Chap1EventMonsterActor monsterActor;
    [SerializeField] Transform stareSpot;
    [SerializeField] Transform retreatSpot;

    [Tooltip("Optional. If null, it will FindFirstObjectByType.")]
    [SerializeField] PlayerController player;

    [Tooltip("Optional. If null, it will FindFirstObjectByType.")]
    [SerializeField] GameManagerChap1 chap1Manager;

    [Tooltip("Optional. If null, it will use chap1Manager.monologue.")]
    [SerializeField] MonologueManager monologue;

    [Header("Shared Retreat Monologues")]
    [Tooltip("Optional. If null, it will try to find one on GameManager or in scene.")]
    [SerializeField] Chap1StareEventRetreatMonologueTable retreatMonologueTable;

    [TextArea(2, 6)][SerializeField] string retreatMonologueFallback = "";

    [Header("Start Placement")]
    [SerializeField] bool moveToStareSpotOnStart = false;
    [SerializeField] bool snapOnArrive = false;

    [Header("Relocate Movement")]
    [SerializeField] float retreatRunSpeed = 6.0f;

    [Header("Facing")]
    [SerializeField] bool rotateMonsterTowardPlayer = true;
    [SerializeField] float monsterTurnSpeedDegPerSec = 240f;

    [Header("Phase A - Monster watches player (Unique)")]
    [SerializeField] float monsterStareSeconds = 3.0f;
    [SerializeField] float monsterStareMaxDistance = 35f;
    [Range(1f, 179f)][SerializeField] float monsterStareFov = 120f;
    [SerializeField] bool monsterRequireLineOfSight = false;
    [SerializeField] LayerMask monsterLineOfSightMask = ~0;
    [SerializeField] Vector3 monsterLosOriginOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] Vector3 monsterLosTargetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Phase B - Player watches monster")]
    [SerializeField] float playerLookSeconds = 2.0f;
    [SerializeField] float playerLookMaxDistance = 18f;
    [Range(1f, 179f)][SerializeField] float playerLookFov = 75f;
    [SerializeField] bool playerRequireLineOfSight = true;
    [SerializeField] LayerMask playerLineOfSightMask = ~0;
    [SerializeField] Vector3 playerLosTargetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Phase B - Special Option")]
    [Tooltip("If true, the monster retreats immediately when the player is within Auto Retreat Distance (no view check).")]
    [SerializeField] bool autoRetreatWhenPlayerClose = false;

    [Tooltip("Distance threshold for Auto Retreat. If player is within this distance, monster retreats instantly.")]
    [SerializeField] float autoRetreatDistance = 2.0f;

    [Header("Monologue (Unique A)")]
    [TextArea(2, 6)][SerializeField] string monologueA = "...\n(He's watching me.)";

    Phase phase;
    float monsterStareTimer;
    float playerLookTimer;
    bool monologueAPlayed;
    Coroutine activeRoutine;

    Chap1CheckpointManager checkpointManager;

    void Awake() {
        checkpointManager = Chap1CheckpointManager.Instance;
        if (checkpointManager == null)
            checkpointManager = FindFirstObjectByType<Chap1CheckpointManager>();
    }

    void Start() {
        ResolveRefs();

        if (IsConsumed()) {
            HandleConsumed();
            return;
        }

        if (monsterActor == null) {
            enabled = false;
            return;
        }

        if (moveToStareSpotOnStart && stareSpot != null) {
            phase = Phase.MovingToStareSpot;
            activeRoutine = StartCoroutine(CoMoveToStareSpot());
            return;
        }

        phase = Phase.WaitingMonsterStare;
    }

    void OnDisable() {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = null;
    }

    void Update() {
        if (phase == Phase.Completed)
            return;

        ResolveRefs();

        UpdateMonsterFacing();

        if (IsConsumed()) {
            HandleConsumed();
            return;
        }

        if (phase == Phase.WaitingMonsterStare) {
            UpdateMonsterStareTimer();
            return;
        }

        if (phase == Phase.WaitingPlayerLook)
            UpdatePlayerLookTimer();
    }

    void ResolveRefs() {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (chap1Manager == null)
            chap1Manager = FindFirstObjectByType<GameManagerChap1>();

        if (monologue == null && chap1Manager != null)
            monologue = chap1Manager.monologue;

        if (retreatMonologueTable == null && chap1Manager != null)
            retreatMonologueTable = chap1Manager.GetComponent<Chap1StareEventRetreatMonologueTable>();

        if (retreatMonologueTable == null)
            retreatMonologueTable = FindFirstObjectByType<Chap1StareEventRetreatMonologueTable>();
    }

    bool IsConsumed() {
        if (checkpointManager == null)
            return false;
        if (string.IsNullOrEmpty(checkpointConsumeId))
            return false;

        return checkpointManager.IsCheckpointZoneConsumed(checkpointConsumeId);
    }

    void HandleConsumed() {
        phase = Phase.Completed;

        if (disableMonsterIfConsumed && monsterActor != null)
            monsterActor.gameObject.SetActive(false);

        if (disableSelfIfConsumed)
            enabled = false;
    }

    IEnumerator CoMoveToStareSpot() {
        yield return monsterActor.CoWalkToPoint(stareSpot, snapOnArrive);

        phase = Phase.WaitingMonsterStare;
        activeRoutine = null;
    }

    void UpdateMonsterStareTimer() {
        if (!IsMonsterWatchingPlayer()) {
            monsterStareTimer = 0f;
            return;
        }

        monsterStareTimer += Time.deltaTime;

        if (!monologueAPlayed && monsterStareTimer >= monsterStareSeconds) {
            monologueAPlayed = true;
            ShowMonologue(monologueA);
            phase = Phase.WaitingPlayerLook;
        }
    }

    void UpdatePlayerLookTimer() {
        if (activeRoutine != null)
            return;

        if (autoRetreatWhenPlayerClose && IsPlayerCloseToMonster()) {
            phase = Phase.Relocating;
            activeRoutine = StartCoroutine(CoRelocateThenFinish());
            return;
        }

        if (!IsPlayerWatchingMonster()) {
            playerLookTimer = 0f;
            return;
        }

        playerLookTimer += Time.deltaTime;

        if (playerLookTimer < playerLookSeconds)
            return;

        phase = Phase.Relocating;
        activeRoutine = StartCoroutine(CoRelocateThenFinish());
    }

    bool IsPlayerCloseToMonster() {
        if (monsterActor == null || player == null)
            return false;

        Transform root = monsterActor.Root;
        if (root == null)
            return false;

        float d = Mathf.Max(0.01f, autoRetreatDistance);
        float dSqr = d * d;

        Vector3 delta = player.transform.position - root.position;
        delta.y = 0f;

        return delta.sqrMagnitude <= dSqr;
    }

    IEnumerator CoRelocateThenFinish() {
        if (monsterActor != null && retreatSpot != null)
            yield return monsterActor.CoRunToPoint(retreatSpot, retreatRunSpeed, snapOnArrive);

        int discoveryIndex = -1;
        if (checkpointManager != null && !string.IsNullOrEmpty(checkpointConsumeId))
            discoveryIndex = checkpointManager.RegisterStareEventDiscovered(checkpointConsumeId);

        ShowMonologue(GetRetreatMonologueByDiscoveryIndex(discoveryIndex));

        phase = Phase.Completed;

        activeRoutine = null;

        if (disableSelfIfConsumed)
            enabled = false;
    }

    string GetRetreatMonologueByDiscoveryIndex(int discoveryIndex) {
        if (retreatMonologueTable != null && retreatMonologueTable.TryGet(discoveryIndex, out string msg))
            return msg;

        return retreatMonologueFallback;
    }

    void ShowMonologue(string msg) {
        if (monologue == null)
            return;
        if (string.IsNullOrEmpty(msg))
            return;

        monologue.ShowMessage(msg);
    }

    bool IsMonsterWatchingPlayer() {
        if (monsterActor == null || player == null)
            return false;

        Transform root = monsterActor.Root;
        if (root == null)
            return false;

        Vector3 toPlayer = player.transform.position - root.position;
        float maxDist = Mathf.Max(0.01f, monsterStareMaxDistance);
        if (toPlayer.sqrMagnitude > maxDist * maxDist)
            return false;

        Vector3 flat = toPlayer;
        flat.y = 0f;

        if (flat.sqrMagnitude > 0.0001f) {
            float cosHalf = Mathf.Cos(Mathf.Deg2Rad * (monsterStareFov * 0.5f));
            Vector3 fwd = root.forward;
            fwd.y = 0f;

            if (fwd.sqrMagnitude > 0.0001f) {
                if (Vector3.Dot(fwd.normalized, flat.normalized) < cosHalf)
                    return false;
            }
        }

        if (!monsterRequireLineOfSight)
            return true;

        Vector3 origin = root.position + monsterLosOriginOffset;
        Vector3 target = player.transform.position + monsterLosTargetOffset;
        return HasLineOfSight(origin, target, monsterLineOfSightMask, player.transform);
    }

    bool IsPlayerWatchingMonster() {
        if (monsterActor == null || player == null || player.playerCamera == null)
            return false;

        Transform root = monsterActor.Root;
        if (root == null)
            return false;

        Transform camTr = player.playerCamera.transform;
        Vector3 targetPos = root.position + playerLosTargetOffset;
        Vector3 toTarget = targetPos - camTr.position;

        float maxDist = Mathf.Max(0.01f, playerLookMaxDistance);
        if (toTarget.sqrMagnitude > maxDist * maxDist)
            return false;

        Vector3 dir = toTarget.normalized;
        float cosHalf = Mathf.Cos(Mathf.Deg2Rad * (playerLookFov * 0.5f));
        if (Vector3.Dot(camTr.forward, dir) < cosHalf)
            return false;

        if (!playerRequireLineOfSight)
            return true;

        return HasLineOfSight(camTr.position, targetPos, playerLineOfSightMask, root);
    }

    bool HasLineOfSight(Vector3 origin, Vector3 target, LayerMask mask, Transform targetRoot) {
        Vector3 delta = target - origin;
        float dist = delta.magnitude;
        if (dist <= 0.01f)
            return true;

        Vector3 dir = delta / dist;

        if (!Physics.Raycast(origin, dir, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
            return true;

        if (hit.transform == targetRoot)
            return true;

        return hit.transform.IsChildOf(targetRoot);
    }

    void UpdateMonsterFacing() {
        if (!rotateMonsterTowardPlayer)
            return;
        if (monsterActor == null || player == null)
            return;

        Transform root = monsterActor.Root;
        if (root == null)
            return;

        Vector3 to = player.transform.position - root.position;
        to.y = 0f;

        if (to.sqrMagnitude < 0.0001f)
            return;

        Quaternion target = Quaternion.LookRotation(to.normalized, Vector3.up);
        float maxDeg = Mathf.Max(0f, monsterTurnSpeedDegPerSec) * Time.deltaTime;
        root.rotation = Quaternion.RotateTowards(root.rotation, target, maxDeg);
    }
}