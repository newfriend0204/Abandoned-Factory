using UnityEngine;

public enum PipeAxisMode { X, Y, Z }

public class PipePiece : MonoBehaviour {
    [Header("Refs")]
    public GameManagerChap1 gameManager;
    public Transform player;
    public Camera viewCamera;
    public Behaviour outline;
    public BoxCollider targetCollider;

    [Header("Config")]
    public PipeAxisMode mode = PipeAxisMode.X;
    [Range(0, 2)] public int partIndex = 0;
    public float interactDistance = 2f;
    public float lookRayDistance = 3f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip rotateSfx;
    [Range(0f, 1f)] public float rotateVolume = 1f;

    [Header("Symmetry")]
    public bool twoStateSymmetry = false;

    private Quaternion _baseRot;
    private int _step;
    private int _correctStep;
    private bool _captured;

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
            targetCollider = GetComponent<BoxCollider>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (!twoStateSymmetry && name.Contains("L100"))
            twoStateSymmetry = true;
    }

    private void Update() {
        if (gameManager.IsPipePartSolved(partIndex)) {
            if (outline != null)
                outline.enabled = false;
            enabled = false;
            return;
        }

        bool near = Vector3.Distance(player.position, transform.position) <= interactDistance;
        bool looked = near && IsLookingAtThis();

        bool showHints = IsInteractHintOn();

        if (outline != null)
            outline.enabled = showHints && looked;

        if (looked) {
            if (showHints)
                gameManager.Pressable(3);
            if (IsInteractPressed()) {
                SnapRotate90();
            }
        }
    }

    public void CaptureCorrectAndRandomize() {
        _baseRot = transform.localRotation;
        _correctStep = 0;
        _captured = true;

        int k = Random.Range(0, 4);
        _step = (_correctStep + k) & 3;
        ApplyStepToTransform(_step);
    }

    public bool IsCorrect() {
        if (!_captured) {
            _baseRot = transform.localRotation;
            _correctStep = 0;
            _captured = true;
            _step = 0;
            return true;
        }

        int cur = GetNearestStepIndex(transform.localRotation);

        if (twoStateSymmetry) {
            return (cur & 1) == (_correctStep & 1);
        }
        return cur == _correctStep;
    }

    public int PartIndex => partIndex;

    public int DebugCurrentDeg => StepToDeg(GetNearestStepIndex(transform.localRotation));
    public int DebugCorrectDeg => StepToDeg(_correctStep);
    public PipeAxisMode DebugMode => mode;

    private void SnapRotate90() {
        if (rotateSfx != null)
            audioSource.PlayOneShot(rotateSfx, rotateVolume);
        _step = (_step + 1) & 3;
        ApplyStepToTransform(_step);
    }

    private void ApplyStepToTransform(int step) {
        transform.localRotation = _baseRot * Quaternion.AngleAxis(step * 90f, AxisVector());
    }

    private int GetNearestStepIndex(Quaternion q) {
        int best = 0;
        float bestAngle = float.MaxValue;
        var axis = AxisVector();
        for (int i = 0; i < 4; i++) {
            Quaternion cand = _baseRot * Quaternion.AngleAxis(i * 90f, axis);
            float a = Quaternion.Angle(q, cand);
            if (a < bestAngle) {
                bestAngle = a;
                best = i;
            }
        }
        return best;
    }

    private Vector3 AxisVector() {
        if (mode == PipeAxisMode.X)
            return Vector3.right;
        if (mode == PipeAxisMode.Y)
            return Vector3.up;
        return Vector3.forward;
    }

    private bool IsLookingAtThis() {
        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, lookRayDistance)) {
            return hit.collider == targetCollider;
        }
        return false;
    }

    private static int StepToDeg(int s) => (s & 3) * 90;

    private Behaviour FindOutlineBehaviour(GameObject go) {
        var behaviours = go.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++) {
            var b = behaviours[i];
            if (b != null && b.GetType().Name == "Outline")
                return b;
        }
        return null;
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