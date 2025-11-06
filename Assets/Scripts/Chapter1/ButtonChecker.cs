using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class ButtonChecker : MonoBehaviour {
    [Header("Refs")]
    public Light indicator;
    public GameManagerChap1 gameManager;
    public Transform player;
    public Camera viewCamera;
    public AudioClip pressSfx;
    public float pressVolume = 1f;

    [Header("Outline")]
    [SerializeField] private Behaviour outline;

    [Header("Indicator Colors")]
    public Color idleColor = Color.red;
    public Color huntingColor = Color.yellow;
    public Color completedColor = Color.green;

    [Header("Interact")]
    public float interactDistance = 2.0f;

    [Header("Shutter Open")]
    public Transform shutter;
    public float openDuration = 4.0f;
    public AudioClip shutterSfx;
    [Range(0f, 1f)] public float shutterVolume = 1f;
    public AnimationCurve ease = AnimationCurve.Linear(0, 0, 1, 1);

    private AudioSource audioSource;
    private MeshCollider childMeshCollider;
    private bool isActivated = false;
    private bool shutterPlayed = false;

    private void Awake() {
        if (gameManager == null) {
            gameManager = FindFirstObjectByType<GameManagerChap1>();
        }
        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        if (viewCamera == null) {
            if (player != null) viewCamera = player.GetComponentInChildren<Camera>();
            if (viewCamera == null) viewCamera = Camera.main;
        }

        var all = GetComponentsInChildren<MeshCollider>(includeInactive: true);
        foreach (var mc in all) {
            if (mc.transform != transform) {
                childMeshCollider = mc;
                break;
            }
        }

        audioSource = GetComponent<AudioSource>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    private void Start() {
        SetIndicatorIdle();
        outline.enabled = false;
    }

    private void Update() {
        bool within = InRange();

        bool isLooking = false;
        if (within && viewCamera != null && childMeshCollider != null) {
            if (Physics.Raycast(viewCamera.transform.position,
                                viewCamera.transform.forward,
                                out var hit, interactDistance)) {
                isLooking = (hit.collider == childMeshCollider);
            }
        }

        if (within && isLooking && gameManager.State != GameManagerChap1.ChapState.Hunting) {
            gameManager.Pressable();
        }

        if (outline != null) {
            bool interactableNow = gameManager.State != GameManagerChap1.ChapState.ShutterOpened;
            outline.enabled = within && interactableNow && !isActivated;
        }

        bool fPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            fPressed = true;
#endif
        if (Input.GetKeyDown(KeyCode.F))
            fPressed = true;

        if (!within || !isLooking || !fPressed)
            return;

        switch (gameManager.State) {
            case GameManagerChap1.ChapState.Idle:
                PlayPress();
                gameManager.StartHunt(this);
                SetIndicatorHunting();
                isActivated = true;
                if (outline != null) outline.enabled = false;
                break;

            case GameManagerChap1.ChapState.Hunting:
                PlayPress();
                break;

            case GameManagerChap1.ChapState.Completed:
                if (!shutterPlayed) {
                    PlayPress();
                    StartCoroutine(OpenShutterRoutine());
                } else {
                    PlayPress();
                }
                break;

            case GameManagerChap1.ChapState.ShutterOpened:
                PlayPress();
                break;
        }
    }

    private bool InRange() {
        return Vector3.Distance(player.position, transform.position) <= interactDistance;
    }

    private void PlayPress() {
        audioSource.PlayOneShot(pressSfx, pressVolume);
    }

    public void SetIndicatorIdle() {
        indicator.color = idleColor;
    }
    public void SetIndicatorHunting() {
        indicator.color = huntingColor;
    }
    public void SetIndicatorCompleted() {
        indicator.color = completedColor;
    }

    private System.Collections.IEnumerator OpenShutterRoutine() {
        shutterPlayed = true;

        float t = 0f;
        float startScaleY = shutter.localScale.y;
        float targetScaleY = 0.1f;

        Vector3 startPos = shutter.localPosition;
        Vector3 targetPos = startPos + new Vector3(0f, 5f, 0f);

        AudioSource.PlayClipAtPoint(shutterSfx, shutter.position, shutterVolume);

        while (t < openDuration) {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / openDuration);
            float e = ease != null ? ease.Evaluate(u) : u;

            var s = shutter.localScale;
            s.y = Mathf.Lerp(startScaleY, targetScaleY, e);
            shutter.localScale = s;

            shutter.localPosition = Vector3.Lerp(startPos, targetPos, e);

            yield return null;
        }

        var sFinal = shutter.localScale;
        sFinal.y = targetScaleY;
        shutter.localScale = sFinal;
        shutter.localPosition = targetPos;

        gameManager.SealShutterOpened();
    }
}
