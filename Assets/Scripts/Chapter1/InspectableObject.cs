using UnityEngine;

[DisallowMultipleComponent]
public class InspectableObject : MonoBehaviour {
    [Header("Refs")]
    public GameManagerChap1 gameManager;
    public Transform player;
    public Camera viewCamera;

    [Header("Settings")]
    public float range = 8f;
    public float interactRange = 2f;
    public bool disableOutlineOnStart = true;

    [Header("Raycast")]
    public LayerMask rayMask = ~0;
    public Transform playerRootToIgnore;

    [Header("Assign Manually")]
    public Transform _playerCached;
    public BoxCollider _childBox;
    public Behaviour _childOutline;

    private void Awake() {
        if (gameManager == null) {
            gameManager = FindFirstObjectByType<GameManagerChap1>();
        }
        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                player = pc.transform;
        }
        _playerCached = player;

        if (playerRootToIgnore == null) {
            playerRootToIgnore = _playerCached;
        }

        if (viewCamera == null) {
            if (_playerCached != null)
                viewCamera = _playerCached.GetComponentInChildren<Camera>();
            if (viewCamera == null)
                viewCamera = Camera.main;
        }
    }

    private void Start() {
        if (disableOutlineOnStart && _childOutline != null) {
            _childOutline.enabled = false;
        }
    }

    private void Update() {
        bool hintsOn = IsInteractHintOn();

        Vector3 playerPos = _playerCached.position;
        Vector3 targetPoint = (_childBox != null)
            ? _childBox.bounds.ClosestPoint(playerPos)
            : transform.position;

        float dist = Vector3.Distance(playerPos, targetPoint);
        bool within = dist <= range;

        if (_childOutline != null) {
            _childOutline.enabled = hintsOn && within;
        }

        bool withinInteract = dist <= interactRange;
        if (!withinInteract || viewCamera == null || _childBox == null || gameManager == null) {
            return;
        }

        bool isLooking = false;
        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);

        float camToBox = Vector3.Distance(viewCamera.transform.position, _childBox.bounds.center);
        float rayDistance = Mathf.Max(interactRange, camToBox + 0.25f);

        var hits = Physics.RaycastAll(ray, rayDistance, rayMask, QueryTriggerInteraction.Collide);
        if (hits != null && hits.Length > 0) {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++) {
                var hc = hits[i].collider;
                if (hc == null)
                    continue;

                if (playerRootToIgnore != null && hc.transform.IsChildOf(playerRootToIgnore))
                    continue;

                if (hc == _childBox || hc.transform == _childBox.transform) {
                    isLooking = true;
                    break;
                }

                isLooking = false;
                break;
            }
        }

        if (withinInteract && isLooking) {
            if (hintsOn)
                gameManager.Pressable(2);

            bool interactPressed = IsInteractPressed();

            if (interactPressed) {
                gameManager.Inspect(gameObject.name);
            }
        }
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