using System.Collections;
using UnityEngine;

public class PipeValve : MonoBehaviour {
    [Header("Refs")]
    public GameManagerChap1 gameManager;
    public Transform player;
    public Camera viewCamera;
    public Behaviour outline;
    public BoxCollider targetCollider;
    public AudioSource audioSource;
    public AudioClip turnSfx;

    [Header("Config")]
    [Range(0, 2)] public int partIndex = 0;
    public float interactDistance = 2f;
    public float lookRayDistance = 3f;
    public float turnDuration = 2.0f;

    private bool _turning;
    private float _yAngle;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap1>();
        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
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
        outline.enabled = false;

        if (targetCollider == null)
            targetCollider = GetComponent<BoxCollider>();

        audioSource = GetComponent<AudioSource>();

        _yAngle = Normalize360(transform.localEulerAngles.y);
    }

    private void Update() {

        if (gameManager.IsPipePartSolved(partIndex)) {
            outline.enabled = false;
            enabled = false;
            return;
        }

        bool near = Vector3.Distance(player.position, transform.position) <= interactDistance;
        bool looked = near && IsLookingAtThis();

        outline.enabled = looked;

        if (looked) {
            gameManager.Pressable(3);
            if (!_turning && Input.GetKeyDown(KeyCode.F)) {
                StartCoroutine(CoTurnAndSubmitLinear());
            }
        }
    }

    private IEnumerator CoTurnAndSubmitLinear() {
        _turning = true;

        float sfxLen = (turnSfx != null) ? turnSfx.length : 0f;
       audioSource.PlayOneShot(turnSfx);

        float start = _yAngle;
        float end = Normalize360(start + 360f);

        float t = 0f;
        while (t < turnDuration) {
            t += Time.deltaTime;
            float r = Mathf.Clamp01(t / turnDuration);
            float yaw = Normalize360(start + 360f * r);
            ApplyY(yaw);
            yield return null;
        }

        _yAngle = end;
        ApplyY(end);

        float extraWait = Mathf.Max(0f, sfxLen - turnDuration);
        if (extraWait > 0f)
            yield return new WaitForSeconds(extraWait);

        gameManager.OnValveSubmitted(partIndex);
        _turning = false;
    }

    private bool IsLookingAtThis() {
        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, lookRayDistance)) {
            return hit.collider == targetCollider;
        }
        return false;
    }

    private void ApplyY(float deg) {
        var e = transform.localEulerAngles;
        e.y = deg;
        transform.localEulerAngles = e;
    }

    private float Normalize360(float deg) {
        deg %= 360f;
        if (deg < 0f)
            deg += 360f;
        return deg;
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