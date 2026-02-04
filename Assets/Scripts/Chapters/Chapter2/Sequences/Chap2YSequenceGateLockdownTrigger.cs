using System.Collections;
using UnityEngine;

public class Chap2YSequenceGateLockdownTrigger : MonoBehaviour {
    [System.Serializable]
    private struct GateRig {
        [Header("Transform")]
        public Transform gate;
        public Transform openPose;
        public Transform closedPose;
        public bool useLocal;

        [Header("Audio (Optional)")]
        public AudioSource audio;
        public AudioClip slamClip;
        [Range(0f, 1f)] public float slamVolume;

        public AudioClip loopClip;
        [Range(0f, 1f)] public float loopVolume;
    }

    [System.Serializable]
    private struct LightCue {
        [Header("Light")]
        public Light light;

        [Header("Audio (Optional)")]
        public AudioSource audio;
        public AudioClip clipOverride;
        [Range(0f, 1f)] public float volume;
    }

    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;
    [SerializeField] private Chap2YStepSequenceManager sequenceManager;
    [SerializeField] private BroadcastAnnouncerUI announcer;

    [Header("Camera Impulse (Exit Slam)")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private bool enableExitSlamImpulse = true;
    [SerializeField] private float shakeDuration = 0.18f;
    [SerializeField] private float shakePosAmplitude = 0.04f;
    [SerializeField] private float shakeRotAmplitude = 1.2f;
    [SerializeField] private float shakeFrequency = 22f;

    [Header("Trigger")]
    [SerializeField] private bool oneShot = true;

    [Header("Gates")]
    [SerializeField] private GateRig entranceGate;
    [SerializeField] private GateRig exitGate;
    [SerializeField] private GateRig[] monsterGates = new GateRig[3];

    [Header("Timings")]
    [SerializeField] private float exitCloseDurationFast = 0.12f;
    [SerializeField] private float entranceCloseDurationFast = 0.12f;
    [SerializeField] private float monsterOpenDelaySeconds = 1.2f;
    [SerializeField] private float monsterOpenDurationNormal = 4.0f;

    [Header("VFX")]
    [SerializeField] private ParticleSystem[] exitCloseDustVfx;
    [SerializeField] private ParticleSystem[] entranceCloseDustVfx;

    [Header("Broadcast (Message 1)")]
    [SerializeField] private string lockdownMessage = "ACCESS RESTRICTED";
    [SerializeField] private Color lockdownBaseColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField] private float lockdownCharsPerSecond = 22f;
    [SerializeField] private float lockdownHoldSeconds = 4f;

    [Header("Broadcast Timing (Between 1 -> 2)")]
    [SerializeField] private float waitAfterMessage1Seconds = 1.0f;

    [Header("Broadcast (Message 2)")]
    [SerializeField] private bool enableSecondBroadcast = true;
    [SerializeField] private string lockdownMessage2 = "AREA UNDER LOCKDOWN";
    [SerializeField] private float lockdownCharsPerSecond2 = 22f;
    [SerializeField] private float lockdownHoldSeconds2 = 4f;

    [Header("Fog (YSequence Only)")]
    [SerializeField] private GameObject[] yFogObjects;

    [Header("Entrance Lockdown Lights")]
    [SerializeField] private LightCue[] lockdownLights = new LightCue[3];
    [SerializeField] private float lockdownLightStartDelaySeconds = 0.5f;
    [SerializeField] private float lockdownLightIntervalSeconds = 0.1f;
    [SerializeField] private bool keepLightsOnDuringPostYChase = true;

    [Header("Auto Bind AudioSource From Gate (if null)")]
    [SerializeField] private bool autoBindAudioFromGate = true;

    private bool triggered;
    private Coroutine routine;
    private Coroutine lightIntroRoutine;
    private Coroutine broadcastRoutine;

    private GameManagerChap2.Chap2State lastState = (GameManagerChap2.Chap2State)(-1);

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (announcer == null)
            announcer = FindFirstObjectByType<BroadcastAnnouncerUI>();

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        AutoBindGateAudioIfNeeded();
        ApplyStateVisualsIfChanged(true);
    }

    private void Start() {
        SnapGatesIfNeededForLoadedState();
        StopAllMonsterLoops();
        ApplyStateVisualsIfChanged(true);
    }

    private void OnDisable() {
        if (routine != null)
            StopCoroutine(routine);

        if (lightIntroRoutine != null)
            StopCoroutine(lightIntroRoutine);

        if (broadcastRoutine != null)
            StopCoroutine(broadcastRoutine);

        routine = null;
        lightIntroRoutine = null;
        broadcastRoutine = null;
    }

    private void Update() {
        ApplyStateVisualsIfChanged(false);
    }

    private void ApplyStateVisualsIfChanged(bool force) {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (gameManager == null)
            return;

        if (!force && gameManager.State == lastState)
            return;

        lastState = gameManager.State;

        ApplyFogFromState();
        ApplyLightsFromState(false);
    }

    private void OnTriggerEnter(Collider other) {
        if (oneShot && triggered)
            return;

        if (!IsPlayer(other))
            return;

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (gameManager != null && gameManager.State != GameManagerChap2.Chap2State.Idle)
            return;

        if (routine != null)
            StopCoroutine(routine);

        triggered = true;
        routine = StartCoroutine(CoLockdownSequence());
    }

    private IEnumerator CoLockdownSequence() {
        PlayExitCloseDust();
        PlayEntranceCloseDust();

        StartExitGateCloseFast();
        StartEntranceGateCloseFast();

        StartBroadcastSequence();
        StartLockdownLightIntro();

        if (monsterOpenDelaySeconds > 0f)
            yield return new WaitForSeconds(monsterOpenDelaySeconds);

        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (gameManager != null && gameManager.State != GameManagerChap2.Chap2State.Idle)
            yield break;

        if (sequenceManager != null)
            sequenceManager.BeginSequence();

        ApplyStateVisualsIfChanged(true);
        StartMonsterGateOpenNormal();

        float dur = Mathf.Max(0.01f, monsterOpenDurationNormal);
        yield return new WaitForSeconds(dur);

        StopAllMonsterLoops();
        routine = null;
    }

    private void StartBroadcastSequence() {
        if (broadcastRoutine != null)
            StopCoroutine(broadcastRoutine);

        broadcastRoutine = StartCoroutine(CoBroadcastSequence());
    }

    private IEnumerator CoBroadcastSequence() {
        if (announcer == null)
            yield break;

        if (!string.IsNullOrEmpty(lockdownMessage)) {
            announcer.ShowBroadcast(
                lockdownMessage,
                lockdownCharsPerSecond,
                lockdownBaseColor,
                BroadcastAnnouncerUI.QueueMode.Overwrite,
                lockdownHoldSeconds
            );

            while (announcer != null && announcer.IsBusy)
                yield return null;
        }

        float gap = Mathf.Max(0f, waitAfterMessage1Seconds);
        if (gap > 0f)
            yield return new WaitForSeconds(gap);

        if (!enableSecondBroadcast) {
            broadcastRoutine = null;
            yield break;
        }

        if (string.IsNullOrEmpty(lockdownMessage2)) {
            broadcastRoutine = null;
            yield break;
        }

        if (announcer == null) {
            broadcastRoutine = null;
            yield break;
        }

        announcer.ShowBroadcast(
            lockdownMessage2,
            lockdownCharsPerSecond2,
            lockdownBaseColor,
            BroadcastAnnouncerUI.QueueMode.Overwrite,
            lockdownHoldSeconds2
        );

        while (announcer != null && announcer.IsBusy)
            yield return null;

        broadcastRoutine = null;
    }

    private bool IsPlayer(Collider other) {
        if (other == null)
            return false;

        if (other.GetComponentInParent<PlayerController>() != null)
            return true;

        if (other.CompareTag("Player"))
            return true;

        return false;
    }

    private void StartExitGateCloseFast() {
        StartGateMove(exitGate, false, exitCloseDurationFast);
        PlayGateSlam(exitGate);

        if (enableExitSlamImpulse)
            PlayImpulse();
    }

    private void StartEntranceGateCloseFast() {
        StartGateMove(entranceGate, false, entranceCloseDurationFast);
        PlayGateSlam(entranceGate);
    }

    private void StartMonsterGateOpenNormal() {
        for (int i = 0; i < monsterGates.Length; i++)
            StartGateLoop(monsterGates[i]);

        for (int i = 0; i < monsterGates.Length; i++)
            StartGateMove(monsterGates[i], true, monsterOpenDurationNormal);
    }

    private void StartGateMove(GateRig rig, bool toOpen, float duration) {
        if (rig.gate == null)
            return;

        Transform pose = toOpen ? rig.openPose : rig.closedPose;
        if (pose == null)
            return;

        StartCoroutine(CoMovePose(rig.gate, pose, duration, rig.useLocal));
    }

    private void PlayGateSlam(GateRig rig) {
        if (rig.audio == null || rig.slamClip == null)
            return;

        float vol = rig.slamVolume <= 0f ? 1f : rig.slamVolume;
        rig.audio.PlayOneShot(rig.slamClip, Mathf.Clamp01(vol));
    }

    private void StartGateLoop(GateRig rig) {
        if (rig.audio == null || rig.loopClip == null)
            return;

        float vol = rig.loopVolume <= 0f ? 1f : rig.loopVolume;

        rig.audio.clip = rig.loopClip;
        rig.audio.loop = true;
        rig.audio.volume = Mathf.Clamp01(vol);
        rig.audio.Play();
    }

    private void StopGateLoop(GateRig rig) {
        if (rig.audio == null)
            return;

        if (!rig.audio.isPlaying)
            return;

        if (rig.audio.loop)
            rig.audio.Stop();

        rig.audio.loop = false;
    }

    private void StopAllMonsterLoops() {
        if (monsterGates == null)
            return;

        for (int i = 0; i < monsterGates.Length; i++)
            StopGateLoop(monsterGates[i]);
    }

    private void PlayExitCloseDust() {
        if (exitCloseDustVfx == null)
            return;

        for (int i = 0; i < exitCloseDustVfx.Length; i++) {
            if (exitCloseDustVfx[i] != null)
                exitCloseDustVfx[i].Play(true);
        }
    }

    private void PlayEntranceCloseDust() {
        if (entranceCloseDustVfx == null)
            return;

        for (int i = 0; i < entranceCloseDustVfx.Length; i++) {
            if (entranceCloseDustVfx[i] != null)
                entranceCloseDustVfx[i].Play(true);
        }
    }

    private void PlayImpulse() {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (playerController == null)
            return;

        playerController.PlayCameraImpulse(shakeDuration, shakePosAmplitude, shakeRotAmplitude, shakeFrequency);
    }

    private void ApplyFogFromState() {
        if (yFogObjects == null || yFogObjects.Length == 0)
            return;

        bool on = gameManager != null && gameManager.State == GameManagerChap2.Chap2State.YSequence;

        for (int i = 0; i < yFogObjects.Length; i++) {
            if (yFogObjects[i] == null)
                continue;

            if (yFogObjects[i].activeSelf != on)
                yFogObjects[i].SetActive(on);
        }
    }

    private void ApplyLightsFromState(bool playAudio) {
        if (lockdownLights == null || lockdownLights.Length == 0)
            return;

        bool on = false;

        if (gameManager != null) {
            if (gameManager.State == GameManagerChap2.Chap2State.YSequence)
                on = true;
            else if (keepLightsOnDuringPostYChase && gameManager.State == GameManagerChap2.Chap2State.PostYChase)
                on = true;
        }

        for (int i = 0; i < lockdownLights.Length; i++) {
            Light l = lockdownLights[i].light;
            if (l == null)
                continue;

            l.enabled = on;

            if (!on)
                continue;

            if (playAudio)
                PlayLightCueAudio(lockdownLights[i]);
        }
    }

    private void StartLockdownLightIntro() {
        if (lockdownLights == null || lockdownLights.Length == 0)
            return;

        ApplyLightsFromState(false);

        if (lightIntroRoutine != null)
            StopCoroutine(lightIntroRoutine);

        lightIntroRoutine = StartCoroutine(CoLockdownLightIntro());
    }

    private IEnumerator CoLockdownLightIntro() {
        float startDelay = Mathf.Max(0f, lockdownLightStartDelaySeconds);
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float interval = Mathf.Max(0f, lockdownLightIntervalSeconds);

        for (int i = 0; i < lockdownLights.Length; i++) {
            if (lockdownLights[i].light != null)
                lockdownLights[i].light.enabled = true;

            PlayLightCueAudio(lockdownLights[i]);

            if (interval > 0f)
                yield return new WaitForSeconds(interval);
        }

        lightIntroRoutine = null;
    }

    private void PlayLightCueAudio(LightCue cue) {
        if (cue.audio == null)
            return;

        AudioClip clip = cue.clipOverride;
        if (clip == null)
            clip = cue.audio.clip;

        if (clip == null)
            return;

        float vol = cue.volume <= 0f ? 1f : cue.volume;
        cue.audio.PlayOneShot(clip, Mathf.Clamp01(vol));
    }

    private void SnapGatesIfNeededForLoadedState() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (gameManager == null)
            return;

        if (gameManager.State == GameManagerChap2.Chap2State.Idle) {
            SnapGate(entranceGate, true);
            SnapGate(exitGate, true);
            SnapMonsterGates(false);
            return;
        }

        if (gameManager.State == GameManagerChap2.Chap2State.YSequence) {
            SnapGate(entranceGate, false);
            SnapGate(exitGate, false);
            SnapMonsterGates(true);
            return;
        }

        if (gameManager.State == GameManagerChap2.Chap2State.PostYChase) {
            SnapGate(entranceGate, false);
            SnapGate(exitGate, true);
            SnapMonsterGates(true);
        }
    }

    private void SnapMonsterGates(bool open) {
        if (monsterGates == null)
            return;

        for (int i = 0; i < monsterGates.Length; i++)
            SnapGate(monsterGates[i], open);
    }

    private void SnapGate(GateRig rig, bool open) {
        if (rig.gate == null)
            return;

        Transform pose = open ? rig.openPose : rig.closedPose;
        if (pose == null)
            return;

        if (rig.useLocal)
            rig.gate.localPosition = pose.localPosition;
        else
            rig.gate.position = pose.position;

        rig.gate.localScale = pose.localScale;
    }

    private void AutoBindGateAudioIfNeeded() {
        if (!autoBindAudioFromGate)
            return;

        entranceGate = AutoBindRigAudio(entranceGate);
        exitGate = AutoBindRigAudio(exitGate);

        if (monsterGates == null)
            return;

        for (int i = 0; i < monsterGates.Length; i++)
            monsterGates[i] = AutoBindRigAudio(monsterGates[i]);
    }

    private GateRig AutoBindRigAudio(GateRig rig) {
        if (rig.audio != null)
            return rig;

        if (rig.gate == null)
            return rig;

        AudioSource a = rig.gate.GetComponentInChildren<AudioSource>(true);
        if (a != null)
            rig.audio = a;

        return rig;
    }

    private static IEnumerator CoMovePose(Transform obj, Transform pose, float duration, bool useLocal) {
        if (obj == null || pose == null)
            yield break;

        Vector3 startPos = useLocal ? obj.localPosition : obj.position;
        Vector3 endPos = useLocal ? pose.localPosition : pose.position;

        Vector3 startScale = obj.localScale;
        Vector3 endScale = pose.localScale;

        if (duration <= 0.0001f) {
            if (useLocal)
                obj.localPosition = endPos;
            else
                obj.position = endPos;

            obj.localScale = endScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float e = u * u * (3f - 2f * u);

            Vector3 p = Vector3.LerpUnclamped(startPos, endPos, e);
            Vector3 s = Vector3.LerpUnclamped(startScale, endScale, e);

            if (useLocal)
                obj.localPosition = p;
            else
                obj.position = p;

            obj.localScale = s;

            yield return null;
        }

        if (useLocal)
            obj.localPosition = endPos;
        else
            obj.position = endPos;

        obj.localScale = endScale;
    }
}