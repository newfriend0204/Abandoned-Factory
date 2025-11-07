using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        if (within && isLooking) {
            if (gameManager.State == GameManagerChap1.ChapState.ShutterOpened ||
                gameManager.State == GameManagerChap1.ChapState.PowerRestoring ||
                gameManager.State == GameManagerChap1.ChapState.MainPowerRestored) {
                gameManager.Pressable(1);
            }
        }

        bool interactableNow = gameManager.State != GameManagerChap1.ChapState.MainPowerRestored;
        outline.enabled = within && interactableNow;

        indicator.color = gameManager.AreAllAuxOn() ? Color.blue : Color.red;

        bool fPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current.fKey.wasPressedThisFrame)
            fPressed = true;
#endif
        if (Input.GetKeyDown(KeyCode.F))
            fPressed = true;

        if (!within || !isLooking || !fPressed)
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
        audioSource.PlayOneShot(pressSfx, pressVolume);
    }
}
