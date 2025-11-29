using UnityEngine;

[DisallowMultipleComponent]
public class PowerChecker : MonoBehaviour {
    [Header("Refs")]
    public Light indicator;
    public GameManagerChap1 gameManager;
    public Transform player;
    public Camera viewCamera;
    public AudioClip pressSfx;
    public float pressVolume = 1f;

    [Header("Outline")]
    [SerializeField] private Behaviour outline;

    [Header("Interact")]
    public float interactDistance = 2.0f;

    private AudioSource audioSource;
    private MeshCollider childMeshCollider;

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
        var all = GetComponentsInChildren<MeshCollider>(true);
        foreach (var mc in all) {
            if (mc.transform != transform) {
                childMeshCollider = mc;
                break;
            }
        }
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    private void Start() {
        indicator.color = Color.red;
        if (outline != null)
            outline.enabled = false;
    }

    private void Update() {
        bool within = Vector3.Distance(player.position, transform.position) <= interactDistance;
        bool isLooking = false;
        if (within && viewCamera != null && childMeshCollider != null) {
            if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit, interactDistance)) {
                isLooking = (hit.collider == childMeshCollider);
            }
        }

        bool showHints = IsInteractHintOn();

        if (within && isLooking) {
            if (showHints &&
                (gameManager.State == GameManagerChap1.ChapState.ShutterOpened ||
                 gameManager.State == GameManagerChap1.ChapState.PowerRestoring ||
                 gameManager.State == GameManagerChap1.ChapState.MainPowerRestored)) {
                gameManager.Pressable(1);
            }
        }

        bool interactableNow = gameManager.State != GameManagerChap1.ChapState.MainPowerRestored;
        if (outline != null)
            outline.enabled = showHints && within && interactableNow;

        indicator.color = gameManager.AreAllAuxOn() ? Color.blue : Color.red;

        bool interactPressed = IsInteractPressed();

        if (!within || !isLooking || !interactPressed)
            return;

        switch (gameManager.State) {
            case GameManagerChap1.ChapState.ShutterOpened:
            case GameManagerChap1.ChapState.PowerRestoring:
                PlayPress();
                gameManager.TryConfirmMainPower();
                break;
            case GameManagerChap1.ChapState.MainPowerRestored:
                PlayPress();
                break;
        }
    }

    private void PlayPress() {
        if (pressSfx != null)
            audioSource.PlayOneShot(pressSfx, pressVolume);
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