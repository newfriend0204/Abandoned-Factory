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

    private bool hasSaved = false;

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

        if (respawnPoint != null) {
            mgr.SaveCheckpointAtSpawnPoint(respawnPoint);
        } else {
            mgr.SaveCheckpointAtCurrentPosition();
        }

        hasSaved = true;
    }
}