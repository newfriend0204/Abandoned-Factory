using UnityEngine;

public class GlitchMonsterTargetSwitcher : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] GlitchManager glitchManager;
    [SerializeField] PlayerController player;

    [Header("Targets (Preferred: Array)")]
    [SerializeField] Transform[] overrideMonsterTargets;

    [Tooltip("Legacy single target. Used only if Override Monster Targets is empty.")]
    [SerializeField] Transform overrideMonsterTarget;

    [Header("Switch Condition")]
    [SerializeField] bool requireOverrideMonsterActive = true;

    [Tooltip("If player is within this distance, override is kept (distance-based).")]
    [SerializeField] float activationDistance = 20f;

    [Header("Optional: Require In-View (FOV/LOS)")]
    [Tooltip("If true, override only when the monster is inside camera FOV (and optional LOS). If false, distance only.")]
    [SerializeField] bool requireInViewForOverride = false;

    [Range(1f, 179f)][SerializeField] float activationFov = 120f;

    [Header("Optional LOS (only used when Require In-View is true)")]
    [SerializeField] bool requireLineOfSight = false;
    [SerializeField] LayerMask lineOfSightMask = ~0;

    Transform currentOverrideTarget;

    void Update() {
        if (glitchManager == null)
            return;

        Camera cam = GetCamera();
        if (cam == null)
            return;

        Transform best = FindBestCandidate(cam);
        if (best == currentOverrideTarget)
            return;

        currentOverrideTarget = best;

        if (currentOverrideTarget != null)
            glitchManager.OverrideMonsterTarget(currentOverrideTarget);
        else
            glitchManager.ClearMonsterTargetOverride();
    }

    Camera GetCamera() {
        if (player != null && player.playerCamera != null)
            return player.playerCamera;

        if (glitchManager != null && glitchManager.PlayerCamera != null)
            return glitchManager.PlayerCamera;

        return Camera.main;
    }

    Transform FindBestCandidate(Camera cam) {
        float maxDist = Mathf.Max(0.01f, activationDistance);
        float maxDistSqr = maxDist * maxDist;

        Transform camTr = cam.transform;
        Vector3 camPos = camTr.position;

        float cosHalfFov = 0f;
        if (requireInViewForOverride)
            cosHalfFov = Mathf.Cos(Mathf.Deg2Rad * (activationFov * 0.5f));

        Transform best = null;
        float bestDistSqr = float.PositiveInfinity;

        if (overrideMonsterTargets != null && overrideMonsterTargets.Length > 0) {
            for (int i = 0; i < overrideMonsterTargets.Length; i++) {
                Transform t = overrideMonsterTargets[i];
                if (!IsCandidateValid(t))
                    continue;

                if (!IsCandidatePassing(camTr, camPos, t, maxDistSqr, cosHalfFov, out float distSqr))
                    continue;

                if (distSqr < bestDistSqr) {
                    bestDistSqr = distSqr;
                    best = t;
                }
            }

            return best;
        }

        if (!IsCandidateValid(overrideMonsterTarget))
            return null;

        if (!IsCandidatePassing(camTr, camPos, overrideMonsterTarget, maxDistSqr, cosHalfFov, out float legacyDistSqr))
            return null;

        return overrideMonsterTarget;
    }

    bool IsCandidateValid(Transform t) {
        if (t == null)
            return false;

        if (requireOverrideMonsterActive && !t.gameObject.activeInHierarchy)
            return false;

        return true;
    }

    bool IsCandidatePassing(Transform camTr, Vector3 camPos, Transform target, float maxDistSqr, float cosHalfFov, out float distSqr) {
        Vector3 toTarget = target.position - camPos;
        distSqr = toTarget.sqrMagnitude;

        if (distSqr > maxDistSqr)
            return false;

        if (!requireInViewForOverride)
            return true;

        if (distSqr <= 0.0001f)
            return true;

        Vector3 dir = toTarget.normalized;
        if (Vector3.Dot(camTr.forward, dir) < cosHalfFov)
            return false;

        if (!requireLineOfSight)
            return true;

        float dist = Mathf.Sqrt(distSqr);
        if (!Physics.Raycast(camPos, dir, out RaycastHit hit, dist, lineOfSightMask, QueryTriggerInteraction.Ignore))
            return true;

        if (hit.transform == target)
            return true;

        return hit.transform.IsChildOf(target);
    }
}