using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chap1EndLeverShutterSequence : MonoBehaviour {
    [Header("Event Blocker (A Collider)")]
    [SerializeField] private Collider blockerCollider;

    [Header("Shutter A Timing")]
    [SerializeField] private float preOpenSfxDelay = 0.5f;
    [SerializeField] private float raiseStartDelay = 1.5f;

    [Header("Monster Footstep Delay")]
    [SerializeField] private float monsterFootstepDelay = 2f;

    [Header("Pre-Footstep Lamp Sequence")]
    [SerializeField] private float lampSequenceLeadSeconds = 3f;
    [SerializeField] private List<GameObject> preFootstepLampObjects = new List<GameObject>();
    [SerializeField] private float lampStepInterval = 0.10f;
    [SerializeField] private bool forceLampsOffBeforeSequence = true;

    [Header("Shutter A (Raise: shrink + up)")]
    [SerializeField] private Transform shutterA;
    [SerializeField] private Transform shutterAEndPose;
    [SerializeField] private float shutterARaiseDuration = 6f;
    [SerializeField] private bool shutterAUseLocal = true;

    [Header("Shutter A - Audio")]
    [SerializeField] private AudioSource shutterASource;
    [SerializeField] private AudioClip shutterAPreOpenClip;
    [Range(0f, 1f)][SerializeField] private float shutterAPreOpenVolume = 1f;
    [SerializeField] private AudioClip shutterARaiseLoop;
    [Range(0f, 1f)][SerializeField] private float shutterALoopVolume = 1f;

    [Header("Monster")]
    [SerializeField] private Chap1EventMonsterActor monster;
    [SerializeField] private Transform monsterSpawnPoint;
    [SerializeField] private Transform monsterLoopPoint;
    [SerializeField] private Transform monsterSlamTriggerPoint;

    [Header("Shutter B (Slam: restore scale + down)")]
    [SerializeField] private Transform shutterB;
    [SerializeField] private Transform shutterBEndPose;
    [SerializeField] private float shutterBSlamDuration = 0.12f;
    [SerializeField] private bool shutterBUseLocal = true;

    [Header("Shutter B - Audio")]
    [SerializeField] private AudioSource shutterBSfxSource;
    [SerializeField] private AudioClip shutterBSlamClip;
    [Range(0f, 1f)][SerializeField] private float shutterBSlamVolume = 1f;

    [Header("Slam FX")]
    [SerializeField] private ParticleSystem slamDustLeft;
    [SerializeField] private ParticleSystem slamDustRight;

    [Header("Camera Impulse")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private float shakeDuration = 0.18f;
    [SerializeField] private float shakePosAmplitude = 0.04f;
    [SerializeField] private float shakeRotAmplitude = 1.2f;
    [SerializeField] private float shakeFrequency = 22f;

    private struct LampNode {
        public Light light;
        public AudioSource audio;
    }

    private bool started;
    private Coroutine routine;
    private readonly List<LampNode> preFootstepLamps = new List<LampNode>();

    private void Awake() {
        RebuildPreFootstepLampNodes();
    }

#if UNITY_EDITOR
    private void OnValidate() {
        RebuildPreFootstepLampNodes();
    }
#endif

    public void BeginSequence() {
        if (started)
            return;

        started = true;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(CoSequence());
    }

    private IEnumerator CoSequence() {
        if (blockerCollider != null)
            blockerCollider.enabled = true;

        if (playerController == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                playerController = pc;
        }

        if (monster != null) {
            if (!monster.gameObject.activeSelf)
                monster.gameObject.SetActive(true);

            monster.SetFootstepsEnabled(false);
        }

        Coroutine raiseRoutine = null;

        StartPreFootstepLampSequenceIfAny();

        if (preOpenSfxDelay > 0f)
            yield return new WaitForSeconds(preOpenSfxDelay);

        PlayShutterAPreOpen();

        float remainToRaise = Mathf.Max(0f, raiseStartDelay - preOpenSfxDelay);
        if (remainToRaise > 0f)
            yield return new WaitForSeconds(remainToRaise);

        if (shutterA != null && shutterAEndPose != null) {
            raiseRoutine = StartCoroutine(CoMovePose(shutterA, shutterAEndPose, shutterARaiseDuration, shutterAUseLocal));
            StartShutterALoop();
        }

        float remainToFootsteps = Mathf.Max(0f, monsterFootstepDelay - raiseStartDelay);
        if (remainToFootsteps > 0f)
            yield return new WaitForSeconds(remainToFootsteps);

        if (monster != null)
            monster.SetFootstepsEnabled(true);

        if (monster != null && monsterSpawnPoint != null && monsterLoopPoint != null && monsterSlamTriggerPoint != null)
            yield return StartCoroutine(monster.CoRunEvent(monsterSpawnPoint, monsterLoopPoint, monsterSlamTriggerPoint));

        yield return StartCoroutine(CoSlamShutterB());

        if (raiseRoutine != null)
            yield return raiseRoutine;

        StopShutterALoop();

        if (blockerCollider != null)
            blockerCollider.enabled = false;
    }

    private void StartPreFootstepLampSequenceIfAny() {
        RebuildPreFootstepLampNodes();
        if (preFootstepLamps.Count == 0)
            return;

        float lead = Mathf.Max(0f, lampSequenceLeadSeconds);
        float startAfter = Mathf.Max(0f, monsterFootstepDelay - lead);

        StartCoroutine(CoPreFootstepLampSequence(startAfter));
    }

    private IEnumerator CoPreFootstepLampSequence(float startAfterSeconds) {
        if (startAfterSeconds > 0f)
            yield return new WaitForSeconds(startAfterSeconds);

        if (preFootstepLamps.Count == 0)
            yield break;

        if (forceLampsOffBeforeSequence) {
            for (int i = 0; i < preFootstepLamps.Count; i++) {
                var n = preFootstepLamps[i];

                if (n.light != null)
                    n.light.enabled = false;

                if (n.audio != null && n.audio.isPlaying)
                    n.audio.Stop();
            }
        }

        float interval = Mathf.Max(0f, lampStepInterval);

        for (int i = 0; i < preFootstepLamps.Count; i++) {
            var n = preFootstepLamps[i];

            if (n.light != null)
                n.light.enabled = true;

            if (n.audio != null) {
                n.audio.spatialBlend = 1f;
                n.audio.Play();
            }

            if (interval > 0f)
                yield return new WaitForSeconds(interval);
            else
                yield return null;
        }
    }

    private void RebuildPreFootstepLampNodes() {
        preFootstepLamps.Clear();

        if (preFootstepLampObjects == null || preFootstepLampObjects.Count == 0)
            return;

        for (int i = 0; i < preFootstepLampObjects.Count; i++) {
            var go = preFootstepLampObjects[i];
            if (go == null)
                continue;

            Light l = go.GetComponentInChildren<Light>(true);
            AudioSource a = go.GetComponentInChildren<AudioSource>(true);

            if (l == null && a == null)
                continue;

            preFootstepLamps.Add(new LampNode {
                light = l,
                audio = a
            });
        }
    }

    private void PlayShutterAPreOpen() {
        if (shutterASource == null || shutterAPreOpenClip == null)
            return;

        shutterASource.PlayOneShot(shutterAPreOpenClip, shutterAPreOpenVolume);
    }

    private void StartShutterALoop() {
        if (shutterASource == null || shutterARaiseLoop == null)
            return;

        shutterASource.clip = shutterARaiseLoop;
        shutterASource.loop = true;
        shutterASource.volume = shutterALoopVolume;
        shutterASource.Play();
    }

    private void StopShutterALoop() {
        if (shutterASource == null)
            return;
        if (!shutterASource.isPlaying)
            return;

        shutterASource.Stop();
        shutterASource.loop = false;
    }

    private IEnumerator CoSlamShutterB() {
        if (shutterB == null || shutterBEndPose == null)
            yield break;

        if (shutterBSfxSource != null && shutterBSlamClip != null)
            shutterBSfxSource.PlayOneShot(shutterBSlamClip, shutterBSlamVolume);

        if (slamDustLeft != null)
            slamDustLeft.Play(true);

        if (slamDustRight != null)
            slamDustRight.Play(true);

        if (playerController != null)
            playerController.PlayCameraImpulse(shakeDuration, shakePosAmplitude, shakeRotAmplitude, shakeFrequency);

        yield return StartCoroutine(CoMovePose(shutterB, shutterBEndPose, shutterBSlamDuration, shutterBUseLocal));
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