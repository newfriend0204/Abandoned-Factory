using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CheckpointZone : MonoBehaviour {
    public enum Chapter {
        Chap1 = 1,
        Chap2 = 2
    }

    [Header("Trigger")]
    public string playerTag = "Player";
    public bool useTriggerEnter = true;

    [Header("Spawn Point")]
    public Transform respawnPoint;

    [Header("Options")]
    public bool saveOnlyOnce = true;

    [Header("ID")]
    public string checkpointId;

    [Header("Chapter")]
    public Chapter chapter = Chapter.Chap1;

    bool hasSaved = false;

    void Reset() {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other) {
        if (!useTriggerEnter)
            return;

        TrySave(other);
    }

    void OnTriggerStay(Collider other) {
        if (useTriggerEnter)
            return;

        TrySave(other);
    }

    void TrySave(Collider other) {
        if (hasSaved && saveOnlyOnce)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (string.IsNullOrEmpty(checkpointId)) {
            checkpointId = gameObject.name;
        }

        switch (chapter) {
            case Chapter.Chap1:
                SaveForChap1();
                break;
            case Chapter.Chap2:
                SaveForChap2();
                break;
        }
    }

    void SaveForChap1() {
        var mgr = Chap1CheckpointManager.Instance;
        if (mgr == null)
            return;

        if (saveOnlyOnce && mgr.IsCheckpointZoneConsumed(checkpointId)) {
            hasSaved = true;
            return;
        }

        if (respawnPoint != null) {
            mgr.SaveCheckpointAtSpawnPoint(respawnPoint);
        } else {
            mgr.SaveCheckpointAtCurrentPosition();
        }

        hasSaved = true;
        mgr.MarkCheckpointZoneConsumed(checkpointId);
    }

    void SaveForChap2() {
        var mgr = Chap2CheckpointManager.Instance;
        if (mgr == null)
            return;

        if (saveOnlyOnce && mgr.IsCheckpointZoneConsumed(checkpointId)) {
            hasSaved = true;
            return;
        }

        if (respawnPoint != null) {
            mgr.SaveCheckpointAtSpawnPoint(respawnPoint);
        } else {
            mgr.SaveCheckpointAtCurrentPosition();
        }

        hasSaved = true;
        mgr.MarkCheckpointZoneConsumed(checkpointId);
    }
}