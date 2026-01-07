using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Chap1EventMonsterActor : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private Transform monsterRoot;
    [SerializeField] private Animator monsterAnimator;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private Rigidbody monsterRigidbody;

    [Header("Rigidbody Safety")]
    [SerializeField] private bool forceKinematicRigidbody = true;

    [Header("Animator State Names")]
    [SerializeField] private string locomotionStateName = "Locomotion";
    [SerializeField] private string idleStateName = "Idle";

    [Header("Player Ref")]
    [SerializeField] private Transform player;

    [Header("Arrive")]
    [SerializeField] private float arriveDistance = 0.35f;
    [SerializeField] private float legTimeoutSeconds = 15f;

    [Header("Arrival Stabilizer")]
    [SerializeField] private float destinationSampleRadius = 2.0f;
    [SerializeField] private float minStoppingDistance = 0.6f;

    [Header("Turn / Stare")]
    [SerializeField] private float turn90Duration = 0.45f;
    [SerializeField] private float stareDuration = 1.2f;
    [SerializeField] private float stareTurnSpeed = 9f;
    [SerializeField] private float stareSfxDelayAfterTurn = 0.5f;

    [Header("Speed Normalization Rules")]
    [SerializeField] private float maxApproachSpeed = 4.0f;
    [SerializeField] private float walkNormMax = 0.8f;
    [SerializeField] private float runNormMin = 0.8f;

    [Header("Event Speeds")]
    [SerializeField] private float approachWalkSpeed = 2.4f;
    [SerializeField] private float slamRunSpeed = 8.5f;

    [Header("Slam Acceleration (Fixed)")]
    [SerializeField] private float slamRunAcceleration = 120f;

    [Header("Audio - Footsteps (Frame Based)")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private List<AudioClip> footstepClips = new List<AudioClip>();
    [SerializeField] private float footstepMoveThreshold = 0.1f;
    [SerializeField, Range(0f, 2f)] private float runNormThreshold = 0.8f;
    [SerializeField] private float footstepAnimFps = 60f;
    [SerializeField] private int walkStepFrames = 30;
    [SerializeField] private int runStepFrames = 17;

    [Header("Audio - Footstep Fade In")]
    [SerializeField] private float footstepFadeInSeconds = 3f;

    [Header("Audio - Charge (Slam Run)")]
    [SerializeField] private AudioSource chargeSource;
    [SerializeField] private AudioClip chargeClip;
    [SerializeField] private bool chargeLoop = true;
    [Range(0f, 1f)][SerializeField] private float chargeVolume = 1f;

    [Header("Audio - Stare One Shot")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip stareOneShotClip;
    [Range(0f, 1f)][SerializeField] private float stareOneShotVolume = 1f;

    private bool footstepsEnabled = true;
    private float footstepTimer;
    private int lastFootstepIndex = -1;
    private bool wasRunningForSteps;

    private float currentMoveSpeed;

    private float footstepBaseVolume = 1f;
    private Coroutine footstepFadeRoutine;

    private void Awake() {
        if (monsterRoot == null)
            monsterRoot = transform;

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (monsterRigidbody == null)
            monsterRigidbody = GetComponent<Rigidbody>();

        if (forceKinematicRigidbody && monsterRigidbody != null) {
            monsterRigidbody.isKinematic = true;
            monsterRigidbody.useGravity = false;
        }

        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                player = pc.transform;
        }

        if (footstepSource != null)
            footstepBaseVolume = footstepSource.volume;

        if (chargeSource != null)
            chargeSource.loop = chargeLoop;
    }

    private void Update() {
        UpdateFootstepAudio(currentMoveSpeed);
    }

    public void SetFootstepsEnabled(bool enabled) {
        footstepsEnabled = enabled;

        if (!footstepsEnabled) {
            footstepTimer = 0f;
            wasRunningForSteps = false;
            lastFootstepIndex = -1;

            if (footstepFadeRoutine != null)
                StopCoroutine(footstepFadeRoutine);

            footstepFadeRoutine = null;

            if (footstepSource != null) {
                footstepSource.volume = footstepBaseVolume;
                footstepSource.Stop();
            }

            return;
        }

        StartFootstepFadeIn();
    }

    public void StopChargeSfx() {
        if (chargeSource == null)
            return;

        if (chargeSource.isPlaying)
            chargeSource.Stop();
    }

    private void PlayChargeSfx() {
        if (chargeSource == null || chargeClip == null)
            return;

        chargeSource.loop = chargeLoop;
        chargeSource.clip = chargeClip;
        chargeSource.volume = chargeVolume;

        if (!chargeSource.isPlaying)
            chargeSource.Play();
    }

    private void StartFootstepFadeIn() {
        if (footstepSource == null)
            return;

        if (footstepFadeInSeconds <= 0.01f) {
            footstepSource.volume = footstepBaseVolume;
            return;
        }

        if (footstepFadeRoutine != null)
            StopCoroutine(footstepFadeRoutine);

        footstepFadeRoutine = StartCoroutine(CoFootstepFadeIn());
    }

    private IEnumerator CoFootstepFadeIn() {
        if (footstepSource == null)
            yield break;

        footstepSource.volume = 0f;

        float dur = Mathf.Max(0.01f, footstepFadeInSeconds);
        float t = 0f;

        while (t < dur) {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            footstepSource.volume = Mathf.Lerp(0f, footstepBaseVolume, u);
            yield return null;
        }

        footstepSource.volume = footstepBaseVolume;
        footstepFadeRoutine = null;
    }

    public IEnumerator CoRunEvent(Transform spawnPoint, Transform loopPoint, Transform slamTriggerPoint) {
        if (spawnPoint == null || loopPoint == null || slamTriggerPoint == null)
            yield break;
        if (navAgent == null || monsterRoot == null)
            yield break;

        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                player = pc.transform;
        }

        StopChargeSfx();
        WarpTo(spawnPoint.position, spawnPoint.rotation);

        ForceLocomotion();
        yield return StartCoroutine(CoMoveTo(loopPoint.position, approachWalkSpeed, 0f, walkNormMax, false, false));

        StopNav();
        SetFootstepsEnabled(false);
        ForceIdle();
        SetAnimSpeedNorm(0f);

        yield return StartCoroutine(CoTurnYawRelative(90f, turn90Duration));

        if (stareSfxDelayAfterTurn > 0f)
            yield return new WaitForSeconds(stareSfxDelayAfterTurn);

        PlayStareOneShot();
        yield return StartCoroutine(CoStareAtPlayer(stareDuration));

        SetFootstepsEnabled(true);
        ForceLocomotion();

        PlayChargeSfx();
        yield return StartCoroutine(CoMoveTo(slamTriggerPoint.position, slamRunSpeed, runNormMin, 999f, true, true));
        StopChargeSfx();

        StopNav();
        currentMoveSpeed = 0f;
        SetAnimSpeedNorm(0f);
    }

    private void WarpTo(Vector3 pos, Quaternion rot) {
        if (navAgent != null && navAgent.enabled)
            navAgent.Warp(pos);
        else
            monsterRoot.position = pos;

        monsterRoot.rotation = rot;
    }

    private IEnumerator CoMoveTo(Vector3 dest, float speed, float animNormMin, float animNormMax, bool isSlamRun, bool snapOnArrive) {
        if (navAgent == null)
            yield break;

        if (!navAgent.enabled)
            navAgent.enabled = true;

        Vector3 navDest = GetNavDestination(dest);

        navAgent.updatePosition = true;
        navAgent.updateRotation = false;
        navAgent.isStopped = false;

        float stopDist = Mathf.Max(minStoppingDistance, arriveDistance);
        navAgent.stoppingDistance = stopDist;

        navAgent.speed = speed;
        navAgent.autoBraking = true;

        if (isSlamRun)
            navAgent.acceleration = Mathf.Max(0.1f, slamRunAcceleration);
        else
            navAgent.acceleration = Mathf.Max(navAgent.acceleration, navAgent.speed * 4f);

        navAgent.SetDestination(navDest);

        float timeoutAt = Time.time + Mathf.Max(0.1f, legTimeoutSeconds);
        while (Time.time < timeoutAt) {
            Vector3 vel = navAgent.velocity;
            vel.y = 0f;
            currentMoveSpeed = vel.magnitude;

            if (vel.sqrMagnitude > 0.001f)
                monsterRoot.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);

            SetAnimSpeedFromWorldSpeedClamped(currentMoveSpeed, animNormMin, animNormMax);

            if (!navAgent.pathPending) {
                float remain = navAgent.remainingDistance;

                bool closeByRemain = !float.IsInfinity(remain) && !float.IsNaN(remain) && remain <= navAgent.stoppingDistance + 0.05f;
                bool closeByPos = Vector3.Distance(monsterRoot.position, navDest) <= navAgent.stoppingDistance + 0.10f;

                if (closeByRemain || closeByPos) {
                    StopNav();

                    if (snapOnArrive)
                        SafeSnapTo(navDest);

                    yield break;
                }
            }

            yield return null;
        }

        StopNav();
    }

    private Vector3 GetNavDestination(Vector3 dest) {
        if (destinationSampleRadius <= 0f)
            return dest;

        int mask = navAgent != null ? navAgent.areaMask : NavMesh.AllAreas;
        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, destinationSampleRadius, mask))
            return hit.position;

        return dest;
    }

    private void SafeSnapTo(Vector3 pos) {
        if (navAgent != null && navAgent.enabled)
            navAgent.Warp(pos);
        else
            monsterRoot.position = pos;
    }

    private void StopNav() {
        if (navAgent == null)
            return;

        if (navAgent.enabled) {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }
    }

    private IEnumerator CoTurnYawRelative(float degrees, float duration) {
        Quaternion start = monsterRoot.rotation;
        Quaternion end = start * Quaternion.Euler(0f, degrees, 0f);

        if (duration <= 0.0001f) {
            monsterRoot.rotation = end;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t01 = Mathf.Clamp01(elapsed / duration);
            float eased = t01 * t01 * (3f - 2f * t01);
            monsterRoot.rotation = Quaternion.SlerpUnclamped(start, end, eased);
            yield return null;
        }

        monsterRoot.rotation = end;
    }

    private IEnumerator CoStareAtPlayer(float duration) {
        float endAt = Time.time + Mathf.Max(0f, duration);
        while (Time.time < endAt) {
            if (player != null) {
                Vector3 to = player.position - monsterRoot.position;
                to.y = 0f;

                if (to.sqrMagnitude > 0.0001f) {
                    Quaternion target = Quaternion.LookRotation(to.normalized, Vector3.up);
                    monsterRoot.rotation = Quaternion.Slerp(monsterRoot.rotation, target, Time.deltaTime * stareTurnSpeed);
                }
            }

            yield return null;
        }
    }

    private void ForceLocomotion() {
        if (monsterAnimator == null)
            return;
        if (string.IsNullOrEmpty(locomotionStateName))
            return;

        monsterAnimator.CrossFadeInFixedTime(locomotionStateName, 0.05f, 0);
    }

    private void ForceIdle() {
        if (monsterAnimator == null)
            return;
        if (string.IsNullOrEmpty(idleStateName))
            return;

        monsterAnimator.CrossFadeInFixedTime(idleStateName, 0.05f, 0);
    }

    private void SetAnimSpeedNorm(float norm) {
        if (monsterAnimator == null)
            return;

        monsterAnimator.SetFloat("Speed", norm);
    }

    private void SetAnimSpeedFromWorldSpeedClamped(float worldSpeed, float minNorm, float maxNorm) {
        float norm = (maxApproachSpeed > 0f) ? (worldSpeed / maxApproachSpeed) : 0f;
        norm = Mathf.Clamp(norm, minNorm, maxNorm);
        SetAnimSpeedNorm(norm);
    }

    private void UpdateFootstepAudio(float horizontalSpeed) {
        if (!footstepsEnabled)
            return;

        bool moving = monsterRoot != null && monsterRoot.gameObject.activeSelf && horizontalSpeed > footstepMoveThreshold;
        if (!moving) {
            footstepTimer = 0f;
            float norm0 = (maxApproachSpeed > 0f) ? (horizontalSpeed / maxApproachSpeed) : 0f;
            wasRunningForSteps = norm0 >= runNormThreshold;
            return;
        }

        float norm = (maxApproachSpeed > 0f) ? (horizontalSpeed / maxApproachSpeed) : 0f;
        bool running = norm >= runNormThreshold;

        float interval = GetBaseFootstepIntervalSeconds(running);

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

    private void PlayStareOneShot() {
        if (sfxSource == null || stareOneShotClip == null)
            return;

        sfxSource.PlayOneShot(stareOneShotClip, stareOneShotVolume);
    }
}