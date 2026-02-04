using UnityEngine;

public class Chap1LeverInteractTrigger : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private GameManagerChap1 gameManager;
    [SerializeField] private Transform player;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Behaviour outline;
    [SerializeField] private Collider targetCollider;
    [SerializeField] private Animator leverAnimator;
    [SerializeField] private Chap1EndLeverShutterSequence sequence;

    [Header("Config")]
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private float lookRayDistance = 3f;
    [SerializeField] private int pressableMode = 1;
    [SerializeField] private string pressTriggerName = "Press";

    [Header("Gate (Main Power Required)")]
    [SerializeField] private string notReadyBroadcastMessage = "주의: 아직 메인 전력을 가동하지 않았습니다.";
    [SerializeField] private float denyFeedbackCooldown = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip leverPressClip;
    [Range(0f, 1f)][SerializeField] private float leverPressVolume = 1f;

    [Header("Audio - Deny")]
    [SerializeField] private AudioClip wrongClip;
    [Range(0f, 1f)][SerializeField] private float wrongVolume = 1f;

    private bool triggered;
    private float lastDeniedAt = -999f;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap1>();

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
            targetCollider = GetComponent<Collider>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void Update() {
        if (triggered)
            return;
        if (gameManager == null || player == null || viewCamera == null || targetCollider == null)
            return;

        bool near = Vector3.Distance(player.position, transform.position) <= interactDistance;
        bool looked = near && IsLookingAtThis();
        bool showHints = IsInteractHintOn();

        if (outline != null)
            outline.enabled = showHints && looked;

        if (!looked)
            return;

        if (showHints)
            gameManager.Pressable(pressableMode);

        if (!IsInteractPressed())
            return;

        if (!IsLeverAllowed()) {
            DenyFeedback();
            return;
        }

        Trigger();
    }

    private bool IsLeverAllowed() {
        if (gameManager == null)
            return false;

        return gameManager.IsMainPowerFullyOnline();
    }

    private void DenyFeedback() {
        if (Time.time < lastDeniedAt + denyFeedbackCooldown)
            return;

        lastDeniedAt = Time.time;

        if (wrongClip != null && audioSource != null)
            audioSource.PlayOneShot(wrongClip, wrongVolume);

        if (gameManager != null && gameManager.announcer != null && !string.IsNullOrEmpty(notReadyBroadcastMessage))
            gameManager.announcer.ShowBroadcast(
                notReadyBroadcastMessage,
                20f,
                new Color(1f, 0.20f, 0.20f, 1f),
                BroadcastAnnouncerUI.QueueMode.Overwrite,
                4f
            );
    }

    private void Trigger() {
        triggered = true;

        if (outline != null)
            outline.enabled = false;

        if (leverAnimator != null && !string.IsNullOrEmpty(pressTriggerName))
            leverAnimator.SetTrigger(pressTriggerName);

        if (leverPressClip != null && audioSource != null)
            audioSource.PlayOneShot(leverPressClip, leverPressVolume);

        if (sequence != null)
            sequence.BeginSequence();
    }

    private bool IsLookingAtThis() {
        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, lookRayDistance))
            return hit.collider == targetCollider;
        return false;
    }

    private static Behaviour FindOutlineBehaviour(GameObject go) {
        var behaviours = go.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++) {
            var b = behaviours[i];
            if (b != null && b.GetType().Name == "Outline")
                return b;
        }
        return null;
    }

    private static bool IsInteractPressed() {
        if (Mathf.Approximately(Time.timeScale, 0f))
            return false;
        var input = InputSettingsManager.Instance;
        return input != null && input.GetKeyDown("Interact");
    }

    private static bool IsInteractHintOn() {
        var sm = SettingsManager.Instance;
        if (sm == null)
            return true;
        int v = sm.GetInt("InteractHint", 0);
        return v == 0;
    }
}