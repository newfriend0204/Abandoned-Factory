using System.Collections;
using UnityEngine;

public class Chap2EntranceDoorTutorialAmbushController : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;
    [SerializeField] private Chap2MonsterController monster;
    [SerializeField] private LockerQTEManager lockerQte;
    [SerializeField] private Chap2CheckpointManager checkpointManager;

    [Header("Entrance Lockers (Tutorial Only)")]
    [SerializeField] private LockerInteractable[] entranceLockers;

    [Header("Monster Spawn")]
    [SerializeField] private Transform monsterSpawnPoint;
    [SerializeField] private bool monsterIgnorePlayerViewReaction = true;
    [SerializeField] private float forceChaseSpeedMultiplier = 1f;

    [Header("Save")]
    [SerializeField] private string consumedId = "Chap2.EntranceDoorTutorialDone";

    [Header("Entrance Door (Pose Only)")]
    [SerializeField] private Transform entranceDoor;
    [SerializeField] private Transform entranceDoorClosedPose;
    [SerializeField] private Transform entranceDoorOpenPose;
    [SerializeField] private bool doorUseLocal = true;
    [SerializeField] private bool doorLerpRotation = true;
    [SerializeField] private bool snapClosedOnStartIfNotCompleted = true;

    [Header("Door Motion")]
    [SerializeField] private float openDuration = 4f;
    [SerializeField] private AnimationCurve ease;

    [Header("Door SFX")]
    [SerializeField] private AudioSource doorSfxSource;
    [SerializeField] private AudioClip doorOpenSfx;
    [SerializeField] private float doorVolume = 1f;

    private LockerInteractable activeLocker;
    private bool eventStarted;
    private bool qteWon;
    private bool doorOpened;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (checkpointManager == null)
            checkpointManager = Chap2CheckpointManager.Instance;

        if (checkpointManager == null)
            checkpointManager = FindFirstObjectByType<Chap2CheckpointManager>();

        if (lockerQte == null)
            lockerQte = LockerQTEManager.Instance;

        if (lockerQte == null)
            lockerQte = FindFirstObjectByType<LockerQTEManager>();

        if (monster == null && gameManager != null)
            monster = gameManager.Monster;

        if (monster == null)
            monster = FindFirstObjectByType<Chap2MonsterController>();
    }

    private void Start() {
        if (IsConsumed()) {
            ApplyDoorOpenInstant();
            doorOpened = true;
            enabled = false;
            return;
        }

        if (snapClosedOnStartIfNotCompleted)
            ApplyDoorClosedInstant();
    }

    private void Update() {
        if (doorOpened)
            return;

        if (!eventStarted) {
            TryStartEventByLockerEnter();
            return;
        }

        if (activeLocker == null)
            return;

        if (!qteWon) {
            if (lockerQte != null && lockerQte.ShouldBlockExitFor(activeLocker))
                qteWon = true;

            return;
        }

        if (monster != null && !monster.IsCompletelyGone)
            return;

        MarkConsumed();

        if (!doorOpened)
            StartCoroutine(OpenDoorRoutine());
    }

    private void TryStartEventByLockerEnter() {
        if (entranceLockers == null || entranceLockers.Length == 0)
            return;

        for (int i = 0; i < entranceLockers.Length; i++) {
            var l = entranceLockers[i];
            if (l == null)
                continue;

            if (!l.IsHidden)
                continue;

            StartEvent(l);
            return;
        }
    }

    private void StartEvent(LockerInteractable locker) {
        if (eventStarted)
            return;

        if (locker == null)
            return;

        if (monsterSpawnPoint == null)
            return;

        eventStarted = true;
        activeLocker = locker;

        if (monster != null) {
            monster.ForceHide();
            monster.StartFromCustomSpawnPoint(monsterSpawnPoint, monsterIgnorePlayerViewReaction);
            monster.ForceStartChase(forceChaseSpeedMultiplier);
            monster.NotifyPlayerHiding(locker, locker.outsidePoint);
        }
    }

    private bool IsConsumed() {
        if (checkpointManager == null)
            return false;

        return checkpointManager.IsCheckpointZoneConsumed(consumedId);
    }

    private void MarkConsumed() {
        if (checkpointManager == null)
            return;

        checkpointManager.MarkCheckpointZoneConsumed(consumedId);
    }

    private bool HasDoorPoses() {
        if (entranceDoor == null)
            return false;
        if (entranceDoorClosedPose == null)
            return false;
        if (entranceDoorOpenPose == null)
            return false;
        return true;
    }

    private void ApplyDoorClosedInstant() {
        if (!HasDoorPoses())
            return;

        ApplyPoseInstant(entranceDoorClosedPose);
    }

    private void ApplyDoorOpenInstant() {
        if (!HasDoorPoses())
            return;

        ApplyPoseInstant(entranceDoorOpenPose);
    }

    private void ApplyPoseInstant(Transform pose) {
        if (entranceDoor == null || pose == null)
            return;

        if (doorUseLocal) {
            entranceDoor.localPosition = pose.localPosition;
            entranceDoor.localRotation = pose.localRotation;
        } else {
            entranceDoor.position = pose.position;
            entranceDoor.rotation = pose.rotation;
        }

        entranceDoor.localScale = pose.localScale;
    }

    private IEnumerator OpenDoorRoutine() {
        doorOpened = true;

        if (!HasDoorPoses())
            yield break;

        PlayDoorSfx();

        float t = 0f;
        float dur = Mathf.Max(0.01f, openDuration);

        Vector3 startPos;
        Vector3 endPos;
        Quaternion startRot = Quaternion.identity;
        Quaternion endRot = Quaternion.identity;

        if (doorUseLocal) {
            startPos = entranceDoorClosedPose.localPosition;
            endPos = entranceDoorOpenPose.localPosition;
            startRot = entranceDoorClosedPose.localRotation;
            endRot = entranceDoorOpenPose.localRotation;
        } else {
            startPos = entranceDoorClosedPose.position;
            endPos = entranceDoorOpenPose.position;
            startRot = entranceDoorClosedPose.rotation;
            endRot = entranceDoorOpenPose.rotation;
        }

        Vector3 startScale = entranceDoorClosedPose.localScale;
        Vector3 endScale = entranceDoorOpenPose.localScale;

        ApplyPoseInstant(entranceDoorClosedPose);

        while (t < dur) {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = (ease != null) ? ease.Evaluate(u) : u;

            Vector3 p = Vector3.Lerp(startPos, endPos, e);
            Vector3 s = Vector3.Lerp(startScale, endScale, e);

            if (doorUseLocal)
                entranceDoor.localPosition = p;
            else
                entranceDoor.position = p;

            entranceDoor.localScale = s;

            if (doorLerpRotation) {
                Quaternion r = Quaternion.Slerp(startRot, endRot, e);
                if (doorUseLocal)
                    entranceDoor.localRotation = r;
                else
                    entranceDoor.rotation = r;
            }

            yield return null;
        }

        ApplyPoseInstant(entranceDoorOpenPose);
    }

    private void PlayDoorSfx() {
        if (doorOpenSfx == null)
            return;

        float vol = Mathf.Clamp01(doorVolume);

        if (doorSfxSource != null) {
            doorSfxSource.PlayOneShot(doorOpenSfx, vol);
            return;
        }

        if (entranceDoor != null)
            AudioSource.PlayClipAtPoint(doorOpenSfx, entranceDoor.position, vol);
    }
}