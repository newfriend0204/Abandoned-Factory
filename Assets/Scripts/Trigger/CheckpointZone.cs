using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CheckpointZone : MonoBehaviour {
    [Header("Trigger")]
    public string playerTag = "Player";
    public bool useTriggerEnter = true;

    [Header("Spawn Point")]
    public Transform respawnPoint;

    [Header("Options")]
    public bool saveOnlyOnce = true;

    [Header("ID")]
    public string checkpointId;

    private bool hasSaved = false;

    private void Awake() {
        if (string.IsNullOrEmpty(checkpointId))
            checkpointId = gameObject.name;
    }

    private void Start() {
        var mgr = Chap1CheckpointManager.Instance;
        if (mgr != null && mgr.IsCheckpointZoneConsumed(checkpointId)) {
            hasSaved = true;
        }
    }

    private void Reset() {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other) {
        if (!useTriggerEnter)
            return;

        if (!other.CompareTag(playerTag))
            return;

        TrySaveCheckpoint();
    }

    public void TrySaveCheckpoint() {
        if (saveOnlyOnce && hasSaved)
            return;

        var mgr = Chap1CheckpointManager.Instance;
        if (mgr == null)
            return;

        if (respawnPoint != null) {
            mgr.SaveCheckpointAtSpawnPoint(respawnPoint);
        } else {
            mgr.SaveCheckpointAtCurrentPosition();
        }

        hasSaved = true;

        mgr.MarkCheckpointZoneConsumed(checkpointId);
    }
}