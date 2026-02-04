using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class DeathManager : MonoBehaviour {
    public static DeathManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool enableDeath = true;

    [Header("Fade Overlay")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("Death UI")]
    [SerializeField] private GameObject deathUIRoot;
    [SerializeField] private DeathUIController deathUIController;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip fallCrackClip;
    public AudioClip deathEnterClip;

    [Header("Timings")]
    public float delayBeforeDeathUI = 0.3f;

    [Header("Monster Death Cinematic")]
    private GlitchManager cachedGlitchManager;
    [SerializeField] private float monsterDeathDuration = 2f;
    [SerializeField] private Transform monsterKillCamPoint;
    [SerializeField] private Transform monsterHeadRig;
    [SerializeField] private AudioClip monsterDeathClip;
    [SerializeField] private AudioSource monsterDeathSource;

    [Header("Head Shake")]
    [SerializeField] private float headRotAmplitude = 22f;
    [SerializeField] private float headShakeFrequency = 22f;

    [Header("Camera Shake")]
    [SerializeField] private float camPosAmplitude = 0.05f;
    [SerializeField] private float camYawAmplitude = 10f;
    [SerializeField] private float camPitchAmplitude = 4f;
    [SerializeField] private float camRollAmplitude = 6f;
    [SerializeField] private float camShakeFrequency = 28f;
    [SerializeField] private float camShakeSmoothTime = 0.04f;

    private bool isDead = false;
    public bool IsDead => isDead;
    public bool EnableDeath => enableDeath;

    private PlayerController cachedPlayer;
    private bool cachedPlayerEnabled = true;

    private Transform cachedCamTr;
    private Transform cachedCamParent;
    private Vector3 cachedCamLocalPos;
    private Quaternion cachedCamLocalRot;

    private Rigidbody cachedPlayerRb;
    private bool cachedPlayerRbKinematic = false;
    private RigidbodyConstraints cachedPlayerRbConstraints;

    private Chap2MonsterController cachedMonsterController;
    private bool cachedMonsterControllerEnabled = true;

    private NavMeshAgent cachedMonsterAgent;
    private bool cachedMonsterAgentEnabled = false;

    private readonly List<Animator> cachedMonsterAnimators = new List<Animator>();
    private readonly List<bool> cachedMonsterAnimatorsEnabled = new List<bool>();

    private Quaternion cachedHeadLocalRot;

    private Vector3 camPosOffset = Vector3.zero;
    private Vector3 camPosVel = Vector3.zero;
    private Vector3 camRotOffset = Vector3.zero;
    private Vector3 camRotVel = Vector3.zero;

    private Vector3 camTargetPosOffset = Vector3.zero;
    private Vector3 camTargetRotOffset = Vector3.zero;
    private float camNextTargetTime = 0f;

    private bool cinematicRunning = false;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (deathUIRoot != null)
            deathUIRoot.SetActive(false);
    }

    private void OnDestroy() {
        if (Instance == this)
            Instance = null;
    }

    public void TriggerFallDeath() {
        if (!enableDeath) {
            Debug.Log("[DeathManager] enableDeath=false (FallDeath ignored)");
            return;
        }

        if (isDead)
            return;

        StartCoroutine(CoFallDeath());
    }

    public void TriggerMonsterDeath() {
        if (!enableDeath) {
            Debug.Log("[DeathManager] enableDeath=false (MonsterDeath ignored)");
            return;
        }

        if (isDead)
            return;

        StartCoroutine(CoMonsterDeath());
    }

    public void OnClickRetry() {
        if (!isDead)
            return;

        if (deathUIRoot != null)
            deathUIRoot.SetActive(false);

        RestoreAfterMonsterCinematicIfNeeded();

        SetGlitchSuppressed(false);

        isDead = false;
        Time.timeScale = 1f;

        var cp = CheckpointService.Current;
        if (cp != null && cp.HasCheckpoint) {
            cp.LoadLastCheckpoint();
        } else {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.name);
        }
    }

    public void OnClickQuitToMain() {
        if (!isDead)
            return;

        if (deathUIRoot != null)
            deathUIRoot.SetActive(false);

        RestoreAfterMonsterCinematicIfNeeded();

        SetGlitchSuppressed(false);

        isDead = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainScreen");
    }

    public void PlayDeathEnterSfx() {
        if (sfxSource != null && deathEnterClip != null)
            sfxSource.PlayOneShot(deathEnterClip);
    }

    private IEnumerator CoFallDeath() {
        isDead = true;

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureFadeCanvasGroup();

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 1f;

        if (sfxSource != null && fallCrackClip != null)
            sfxSource.PlayOneShot(fallCrackClip);

        if (delayBeforeDeathUI > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeDeathUI);

        EnsureDeathUIReferences();

        SetGlitchSuppressed(true);
        deathUIRoot.SetActive(true);
    }

    private IEnumerator CoMonsterDeath() {
        isDead = true;
        cinematicRunning = true;

        var qte = LockerQTEManager.Instance;
        if (qte == null)
            qte = FindFirstObjectByType<LockerQTEManager>();

        if (qte != null)
            qte.ForceEndForDeath();

        CacheAndFreezePlayer();
        CacheAndFreezeMonster();

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        MoveCameraToKillPoint();
        PlayMonsterDeathSfx();

        float dur = Mathf.Max(0.01f, monsterDeathDuration);
        float t = 0f;

        while (t < dur) {
            t += Time.unscaledDeltaTime;
            UpdateHeadShake();
            UpdateCameraShake();
            yield return null;
        }

        EnsureFadeCanvasGroup();
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 1f;

        if (delayBeforeDeathUI > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeDeathUI);

        EnsureDeathUIReferences();

        SetGlitchSuppressed(true);
        deathUIRoot.SetActive(true);

        cinematicRunning = false;
    }

    private void CacheAndFreezePlayer() {
        if (cachedPlayer == null)
            cachedPlayer = FindFirstObjectByType<PlayerController>();

        if (cachedPlayer == null)
            return;

        cachedPlayerEnabled = cachedPlayer.enabled;

        cachedPlayerRb = cachedPlayer.GetComponent<Rigidbody>();
        if (cachedPlayerRb != null) {
            cachedPlayerRbKinematic = cachedPlayerRb.isKinematic;
            cachedPlayerRbConstraints = cachedPlayerRb.constraints;
            cachedPlayerRb.isKinematic = true;
            cachedPlayerRb.constraints = RigidbodyConstraints.FreezeAll;
        }

        cachedPlayer.SetRestrictedMode(true, true, true, true, Vector2.zero, false, 0f, 0f, true, true);
        cachedPlayer.enabled = false;

        if (cachedPlayer.playerCamera != null)
            cachedCamTr = cachedPlayer.playerCamera.transform;
    }

    private void CacheAndFreezeMonster() {
        if (cachedMonsterController == null)
            cachedMonsterController = FindFirstObjectByType<Chap2MonsterController>();

        if (cachedMonsterController == null)
            return;

        cachedMonsterControllerEnabled = cachedMonsterController.enabled;
        cachedMonsterController.enabled = false;

        cachedMonsterAgent = cachedMonsterController.GetComponentInChildren<NavMeshAgent>(true);
        if (cachedMonsterAgent != null) {
            cachedMonsterAgentEnabled = cachedMonsterAgent.enabled;
            cachedMonsterAgent.enabled = false;
        }

        cachedMonsterAnimators.Clear();
        cachedMonsterAnimatorsEnabled.Clear();

        var unique = new HashSet<Animator>();

        Animator[] anims = cachedMonsterController.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++) {
            Animator a = anims[i];
            if (a == null)
                continue;
            if (!unique.Add(a))
                continue;

            cachedMonsterAnimators.Add(a);
            cachedMonsterAnimatorsEnabled.Add(a.enabled);
            a.enabled = false;
        }

        if (monsterHeadRig != null)
            cachedHeadLocalRot = monsterHeadRig.localRotation;
    }

    private void MoveCameraToKillPoint() {
        if (cachedCamTr == null || monsterKillCamPoint == null)
            return;

        cachedCamParent = cachedCamTr.parent;
        cachedCamLocalPos = cachedCamTr.localPosition;
        cachedCamLocalRot = cachedCamTr.localRotation;

        cachedCamTr.SetParent(null, true);
        cachedCamTr.position = monsterKillCamPoint.position;
        cachedCamTr.rotation = monsterKillCamPoint.rotation;

        camPosOffset = Vector3.zero;
        camPosVel = Vector3.zero;
        camRotOffset = Vector3.zero;
        camRotVel = Vector3.zero;

        camTargetPosOffset = Vector3.zero;
        camTargetRotOffset = Vector3.zero;
        camNextTargetTime = Time.unscaledTime;
    }

    private void PlayMonsterDeathSfx() {
        AudioSource src = monsterDeathSource != null ? monsterDeathSource : sfxSource;
        if (src == null)
            return;

        if (monsterDeathClip != null) {
            src.PlayOneShot(monsterDeathClip);
            return;
        }

        if (deathEnterClip != null)
            src.PlayOneShot(deathEnterClip);
    }

    private void UpdateHeadShake() {
        if (monsterHeadRig == null)
            return;

        float f = Mathf.Max(0.01f, headShakeFrequency);
        float a = Mathf.Max(0f, headRotAmplitude);

        float nx = (Mathf.PerlinNoise(Time.unscaledTime * f, 0.15f) * 2f) - 1f;
        float ny = (Mathf.PerlinNoise(0.25f, Time.unscaledTime * f) * 2f) - 1f;
        float nz = (Mathf.PerlinNoise(Time.unscaledTime * f, 0.75f) * 2f) - 1f;

        Vector3 euler = new Vector3(nx, ny, nz) * a;
        monsterHeadRig.localRotation = cachedHeadLocalRot * Quaternion.Euler(euler);
    }

    private void UpdateCameraShake() {
        if (cachedCamTr == null || monsterKillCamPoint == null)
            return;

        float f = Mathf.Max(1f, camShakeFrequency);
        float smooth = Mathf.Max(0.001f, camShakeSmoothTime);

        if (Time.unscaledTime >= camNextTargetTime) {
            camNextTargetTime = Time.unscaledTime + (1f / f);
            camTargetPosOffset = Random.insideUnitSphere * camPosAmplitude;

            float pitch = Random.Range(-camPitchAmplitude, camPitchAmplitude);
            float yaw = Random.Range(-camYawAmplitude, camYawAmplitude);
            float roll = Random.Range(-camRollAmplitude, camRollAmplitude);
            camTargetRotOffset = new Vector3(pitch, yaw, roll);
        }

        camPosOffset = Vector3.SmoothDamp(camPosOffset, camTargetPosOffset, ref camPosVel, smooth, Mathf.Infinity, Time.unscaledDeltaTime);
        camRotOffset = Vector3.SmoothDamp(camRotOffset, camTargetRotOffset, ref camRotVel, smooth, Mathf.Infinity, Time.unscaledDeltaTime);

        cachedCamTr.position = monsterKillCamPoint.position + camPosOffset;
        cachedCamTr.rotation = monsterKillCamPoint.rotation * Quaternion.Euler(camRotOffset);
    }

    private void RestoreAfterMonsterCinematicIfNeeded() {
        if (!cinematicRunning && cachedCamTr == null && cachedPlayer == null)
            return;

        if (monsterHeadRig != null)
            monsterHeadRig.localRotation = cachedHeadLocalRot;

        if (cachedCamTr != null && cachedCamParent != null) {
            cachedCamTr.SetParent(cachedCamParent, true);
            cachedCamTr.localPosition = cachedCamLocalPos;
            cachedCamTr.localRotation = cachedCamLocalRot;
        }

        if (cachedPlayer != null) {
            cachedPlayer.enabled = cachedPlayerEnabled;
            cachedPlayer.SetRestrictedMode(false, true, false, false, Vector2.one, false, 0f, 0f, false, cachedPlayerRbKinematic);

            if (cachedPlayerRb != null) {
                cachedPlayerRb.isKinematic = cachedPlayerRbKinematic;
                cachedPlayerRb.constraints = cachedPlayerRbConstraints;
            }
        }

        if (cachedMonsterController != null)
            cachedMonsterController.enabled = cachedMonsterControllerEnabled;

        if (cachedMonsterAgent != null)
            cachedMonsterAgent.enabled = cachedMonsterAgentEnabled;

        for (int i = 0; i < cachedMonsterAnimators.Count; i++) {
            Animator a = cachedMonsterAnimators[i];
            if (a == null)
                continue;

            bool wasEnabled = true;
            if (i >= 0 && i < cachedMonsterAnimatorsEnabled.Count)
                wasEnabled = cachedMonsterAnimatorsEnabled[i];

            a.enabled = wasEnabled;
        }

        cachedMonsterAnimators.Clear();
        cachedMonsterAnimatorsEnabled.Clear();

        cachedCamTr = null;
        cachedCamParent = null;
        cachedPlayer = null;
        cachedMonsterController = null;
        cachedMonsterAgent = null;

        camTargetPosOffset = Vector3.zero;
        camTargetRotOffset = Vector3.zero;
        camNextTargetTime = 0f;

        cinematicRunning = false;
        isDead = false;
    }

    private void EnsureGlitchManager() {
        if (cachedGlitchManager != null)
            return;

        cachedGlitchManager = FindFirstObjectByType<GlitchManager>();
    }

    private void SetGlitchSuppressed(bool suppressed) {
        EnsureGlitchManager();

        if (cachedGlitchManager == null)
            return;

        cachedGlitchManager.SetSuppressed(suppressed);
    }

    private void EnsureFadeCanvasGroup() {
        if (fadeCanvasGroup != null)
            return;

        if (Chap1CheckpointManager.Instance != null && Chap1CheckpointManager.Instance.fadeCanvasGroup != null) {
            fadeCanvasGroup = Chap1CheckpointManager.Instance.fadeCanvasGroup;
            return;
        }

        var go = GameObject.Find("FadeOverlay");
        if (go != null)
            fadeCanvasGroup = go.GetComponent<CanvasGroup>();
    }

    private void EnsureDeathUIReferences() {
        if (deathUIRoot != null && deathUIController != null)
            return;

        var root = GameObject.Find("Death");
        if (root != null) {
            deathUIRoot = root;
            deathUIController = root.GetComponent<DeathUIController>();
        }
    }
}