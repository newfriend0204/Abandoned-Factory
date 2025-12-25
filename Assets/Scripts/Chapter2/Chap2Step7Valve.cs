using UnityEngine;

public class Chap2Step7Valve : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;
    [SerializeField] private Transform player;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Behaviour outline;
    [SerializeField] private Collider targetCollider;
    [SerializeField] private Transform valveTransform;

    [Header("Config")]
    [SerializeField] private int stepNumber = 7;
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private float lookRayDistance = 3f;
    [SerializeField] private bool requireLookAt = true;

    [Header("Hold")]
    [SerializeField] private float holdSeconds = 7.5f;

    [Header("Rotation")]
    [SerializeField] private float rotationDegreesPerSecond = 180f;
    [SerializeField] private bool captureInitialAsStart = true;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip turningLoopClip;
    [SerializeField, Range(0f, 1f)] private float turningLoopVolume = 0.8f;

    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float progress01 = 0f;

    private Vector3 baseLocalEuler;
    private float startY;
    private bool completed = false;
    private bool locked = false;

    public float Progress01 => progress01;
    public bool IsCompleted => completed;

    public void SetLocked(bool value) {
        locked = value;
        if (locked)
            StopLoop();
    }

    public void ForceSetProgress01(float value01, bool markCompleted) {
        progress01 = Mathf.Clamp01(value01);
        if (markCompleted)
            completed = true;

        StopLoop();
        ApplyRotation();
    }

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                player = pc.transform;
        }

        if (viewCamera == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null && pc.playerCamera != null)
                viewCamera = pc.playerCamera;

            if (viewCamera == null)
                viewCamera = Camera.main;
        }

        if (outline == null)
            outline = FindOutlineBehaviour(gameObject);
        if (outline != null)
            outline.enabled = false;

        if (targetCollider == null)
            targetCollider = GetComponentInChildren<Collider>();

        if (valveTransform == null)
            valveTransform = transform;

        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        baseLocalEuler = valveTransform.localEulerAngles;

        if (captureInitialAsStart)
            startY = baseLocalEuler.y;
        else
            startY = baseLocalEuler.y;

        ApplyRotation();
    }

    private void Update() {
        if (!IsStep7Active()) {
            if (outline != null)
                outline.enabled = false;

            StopLoop();
            return;
        }

        if (locked || completed) {
            if (outline != null)
                outline.enabled = false;

            StopLoop();
            ApplyRotation();
            return;
        }

        bool near = player != null && Vector3.Distance(player.position, transform.position) <= interactDistance;
        bool looked = near && (!requireLookAt || IsLookingAtThis());
        bool showHints = IsInteractHintOn();

        if (outline != null)
            outline.enabled = showHints && looked;

        if (!looked) {
            StopLoop();
            return;
        }

        if (showHints && gameManager != null)
            gameManager.Pressable(4);

        if (!IsInteractHeld()) {
            StopLoop();
            return;
        }

        float delta01 = Time.deltaTime / Mathf.Max(0.01f, holdSeconds);
        progress01 = Mathf.Clamp01(progress01 + delta01);

        ApplyRotation();
        PlayLoop();

        if (progress01 < 1f)
            return;

        completed = true;
        StopLoop();
    }

    private bool IsStep7Active() {
        if (gameManager == null)
            return false;

        if (gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return false;

        var y = Chap2YStepSequenceManager.Instance;
        if (y == null)
            return false;

        if (y.CurrentStep != stepNumber)
            return false;

        return true;
    }

    private bool IsLookingAtThis() {
        if (viewCamera == null || targetCollider == null)
            return false;

        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, lookRayDistance, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider == targetCollider;

        return false;
    }

    private bool IsInteractHeld() {
        if (Mathf.Approximately(Time.timeScale, 0f))
            return false;

        var input = InputSettingsManager.Instance;
        if (input != null)
            return input.GetKey("Interact");

        return Input.GetKey(KeyCode.F);
    }

    private bool IsInteractHintOn() {
        var sm = SettingsManager.Instance;
        if (sm == null)
            return true;

        int v = sm.GetInt("InteractHint", 0);
        return v == 0;
    }

    private void ApplyRotation() {
        float heldSeconds = progress01 * holdSeconds;
        float y = startY + rotationDegreesPerSecond * heldSeconds;

        Quaternion rot = Quaternion.Euler(baseLocalEuler.x, y, baseLocalEuler.z);
        valveTransform.localRotation = rot;
    }

    private void PlayLoop() {
        if (sfxSource == null || turningLoopClip == null)
            return;

        if (sfxSource.isPlaying && sfxSource.clip == turningLoopClip)
            return;

        sfxSource.clip = turningLoopClip;
        sfxSource.loop = true;
        sfxSource.volume = turningLoopVolume;
        sfxSource.Play();
    }

    private void StopLoop() {
        if (sfxSource == null)
            return;

        if (!sfxSource.isPlaying)
            return;

        if (sfxSource.loop && sfxSource.clip == turningLoopClip)
            sfxSource.Stop();
    }

    private Behaviour FindOutlineBehaviour(GameObject go) {
        var behaviours = go.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++) {
            var b = behaviours[i];
            if (b != null && b.GetType().Name == "Outline")
                return b;
        }
        return null;
    }
}