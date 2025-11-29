using UnityEngine;

public class ButtonInteract : MonoBehaviour {
    [Header("Refs")]
    public Outline outline;
    public Light targetLight;
    public GameManagerChap1 gameManager;
    public Transform player;
    public Camera viewCamera;
    public AudioClip pressSfx;
    public float pressVolume = 1f;
    private AudioSource audioSource;

    [Header("Settings")]
    public float interactDistance = 2.0f;
    public Color idleColor = Color.red;
    public Color activeColor = Color.green;

    [Header("Light Control")]
    public bool lightOffBeforeHunt = true;

    private bool isActivated;
    private MeshCollider childMeshCollider;
    private bool HuntActive => gameManager.State == GameManagerChap1.ChapState.Hunting;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap1>();
        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                player = pc.transform;
        }
        if (viewCamera == null) {
            if (player != null)
                viewCamera = player.GetComponentInChildren<Camera>();
            if (viewCamera == null)
                viewCamera = Camera.main;
        }

        var all = GetComponentsInChildren<MeshCollider>(includeInactive: true);
        foreach (var mc in all) {
            if (mc.transform != transform) {
                childMeshCollider = mc;
                break;
            }
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    private void Start() {
        if (outline != null)
            outline.enabled = false;
        if (lightOffBeforeHunt && gameManager.State != GameManagerChap1.ChapState.Hunting) {
            targetLight.enabled = false;
        }
    }

    private void Update() {
        if (!HuntActive)
            return;

        bool showHints = IsInteractHintOn();

        float dist = Vector3.Distance(player.position, transform.position);
        bool within = dist <= interactDistance;

        if (outline != null)
            outline.enabled = showHints && within && !isActivated;

        bool isLooking = false;
        if (within) {
            if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit, interactDistance)) {
                isLooking = (hit.collider == childMeshCollider);
            }
        }

        if (!isActivated && within && isLooking && showHints) {
            gameManager.Pressable(1);
        }

        bool interactPressed = IsInteractPressed();

        if (!isActivated && within && isLooking && interactPressed) {
            targetLight.color = activeColor;
            isActivated = true;
            if (outline != null)
                outline.enabled = false;
            gameManager.ReportPressed(this);
            if (pressSfx != null)
                audioSource.PlayOneShot(pressSfx, pressVolume);
        }
    }

    public void PrepareForHunt() {
        isActivated = false;
        if (outline != null)
            outline.enabled = false;
        targetLight.enabled = true;
        targetLight.color = idleColor;
    }

    private bool IsInteractPressed() {
        if (Mathf.Approximately(Time.timeScale, 0f))
            return false;

        var input = InputSettingsManager.Instance;
        return input != null && input.GetKeyDown("Interact");
    }

    private bool IsInteractHintOn() {
        var sm = SettingsManager.Instance;
        if (sm == null)
            return true;

        int v = sm.GetInt("InteractHint", 0);
        return v == 0;
    }
}