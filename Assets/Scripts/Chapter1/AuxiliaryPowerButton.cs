using System.Collections.Generic;
using UnityEngine;

public class AuxiliaryPowerButton : MonoBehaviour {
    [Header("Refs")]
    public Light indicator;
    public GameManagerChap1 gameManager;
    public Transform player;
    public Camera viewCamera;
    public AudioClip pressSfx;
    public float pressVolume = 1f;

    [Header("Interact")]
    public float interactDistance = 2.0f;
    public int auxIndex = 0;
    public bool requireLook = true;
    public LayerMask lookMask = ~0;

    [Header("Outline")]
    [SerializeField] private Behaviour outline;

    private AudioSource audioSource;
    private readonly List<Collider> ownedColliders = new List<Collider>();

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap1>();
        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        if (viewCamera == null) {
            if (player != null)
                viewCamera = player.GetComponentInChildren<Camera>();
            if (viewCamera == null)
                viewCamera = Camera.main;
        }

        GetComponentsInChildren(true, ownedColliders);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        if (outline == null) {
            var behaviours = GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++) {
                var b = behaviours[i];
                if (b != null && b.GetType().Name == "Outline") {
                    outline = b;
                    break;
                }
            }
        }
    }

    private void Start() {
        if (outline != null)
            outline.enabled = false;
        SyncIndicator();
    }

    private void Update() {
        if (gameManager == null || player == null)
            return;

        bool inPowerRestoring = (gameManager.State == GameManagerChap1.ChapState.PowerRestoring);
        float distPlayerToBtn = Vector3.Distance(player.position, transform.position);
        bool within = distPlayerToBtn <= interactDistance;

        bool isLooking = true;
        if (requireLook) {
            isLooking = false;
            float rayLen = Mathf.Max(Vector3.Distance(viewCamera.transform.position, transform.position) + 0.75f, 3f);
            RaycastHit hit;
            if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out hit, rayLen, lookMask, QueryTriggerInteraction.Ignore)) {
                isLooking = IsOurCollider(hit.collider);
            }
        }

        bool auxOn = gameManager.IsAuxOn(auxIndex);
        bool canPress = inPowerRestoring && !auxOn && within && (!requireLook || isLooking);
        bool showHints = IsInteractHintOn();

        if (outline != null)
            outline.enabled = showHints && inPowerRestoring && !auxOn && within;

        if (canPress && showHints)
            gameManager.Pressable(1);

        SyncIndicator();

        if (!IsInteractPressed())
            return;

        if (canPress) {
            gameManager.SetAuxState(auxIndex, 1);
            SyncIndicator();
            PlayPress();
        } else if (auxOn && within && (!requireLook || isLooking)) {
            PlayPress();
        }
    }

    private bool IsOurCollider(Collider c) {
        if (c.transform == transform || c.transform.IsChildOf(transform))
            return true;
        for (int i = 0; i < ownedColliders.Count; i++) {
            if (c == ownedColliders[i])
                return true;
        }
        return false;
    }

    private void SyncIndicator() {
        indicator.color = gameManager.IsAuxOn(auxIndex) ? Color.green : Color.red;
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