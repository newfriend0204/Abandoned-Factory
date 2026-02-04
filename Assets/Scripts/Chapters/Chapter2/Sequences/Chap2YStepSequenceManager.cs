using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Chap2YStepSequenceManager : MonoBehaviour, ICheckpointService {
    public static Chap2YStepSequenceManager Instance { get; private set; }

    public event Action<int, int> StepChanged;

    [Header("Refs")]
    [SerializeField] private GameManagerChap2 gameManager;
    [SerializeField] private Chap2CheckpointManager checkpointManager;

    [Header("Step Spawn (Optional)")]
    [SerializeField] private Transform step1SpawnPoint;

    [Header("Rules")]
    [SerializeField] private int maxStep = 7;
    [SerializeField] private int checkpointStartStep = 2;
    [SerializeField] private int rollbackStepPenalty = 2;

    [Header("Input")]
    [SerializeField] private KeyCode startSequenceKey = KeyCode.F3;

    [SerializeField] private int currentStep = 1;
    public int CurrentStep => currentStep;

    public bool HasCheckpoint => checkpointManager != null && checkpointManager.HasCheckpoint;

    private bool sequenceActive = false;
    private Coroutine rehookRoutine;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RefreshSceneBindings();
        RehookCheckpointServiceSoon();
    }

    private void OnEnable() {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start() {
        RefreshSceneBindings();
        RehookCheckpointServiceSoon();
        StartCoroutine(CoRestoreNextFrame());
    }

    private IEnumerator CoRestoreNextFrame() {
        yield return null;
        TryRestoreFromSave();
    }

    private void Update() {
        if (Input.GetKeyDown(startSequenceKey) && !sequenceActive)
            BeginSequence();

        if (Input.GetKeyDown(KeyCode.F1))
            CompleteCurrentStep();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        RefreshSceneBindings();
        RehookCheckpointServiceSoon();
    }

    private void RefreshSceneBindings() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();
        if (checkpointManager == null)
            checkpointManager = Chap2CheckpointManager.Instance != null ? Chap2CheckpointManager.Instance : FindFirstObjectByType<Chap2CheckpointManager>();
    }

    private void RehookCheckpointServiceSoon() {
        if (rehookRoutine != null)
            StopCoroutine(rehookRoutine);

        rehookRoutine = StartCoroutine(CoRehookAfterOneFrame());
    }

    private IEnumerator CoRehookAfterOneFrame() {
        yield return null;

        CheckpointService.Register(this);

        rehookRoutine = null;
    }

    private void TryRestoreFromSave() {
        RefreshSceneBindings();

        if (gameManager == null)
            return;

        if (gameManager.State != GameManagerChap2.Chap2State.YSequence)
            return;

        sequenceActive = true;

        int savedStep = Chap2CheckpointManager.GetSavedYCurrentStepOrDefault(1);
        SetStep(savedStep);

        if (checkpointManager != null && !checkpointManager.HasStepCheckpoint(1))
            CacheStep1RuntimeOnly();
    }

    private void SetStep(int newStep) {
        newStep = Mathf.Clamp(newStep, 1, Mathf.Max(1, maxStep));
        if (newStep == currentStep)
            return;

        int old = currentStep;
        currentStep = newStep;
        StepChanged?.Invoke(old, currentStep);
    }

    public void BeginSequence() {
        RefreshSceneBindings();

        if (gameManager == null) {
            Debug.LogWarning("[Chap2YStepSequenceManager] GameManagerChap2를 찾지 못해 Y시퀀스 시작 불가");
            return;
        }

        sequenceActive = true;
        SetStep(1);

        gameManager.StartYSequence();

        if (checkpointManager != null) {
            if (step1SpawnPoint != null)
                checkpointManager.SaveStepCheckpointFromSpawnPoint(1, step1SpawnPoint, false, true);
            else
                checkpointManager.SaveStepCheckpointFromCurrentPosition(1, false);
        }
    }

    private void CacheStep1RuntimeOnly() {
        if (checkpointManager == null)
            return;

        if (step1SpawnPoint != null)
            checkpointManager.SaveCheckpointAtSpawnPointSilent(step1SpawnPoint, false);
        else
            checkpointManager.SaveCheckpointAtCurrentPositionSilent(false);

        checkpointManager.CacheCurrentAsStepCheckpoint(1);
    }

    public void CompleteCurrentStep() {
        CompleteStep(currentStep);
    }

    public void CompleteStep(int step) {
        if (!sequenceActive)
            return;
        if (step != currentStep)
            return;

        if (currentStep >= maxStep) {
            int old = currentStep;
            currentStep = Mathf.Max(maxStep + 1, maxStep);
            StepChanged?.Invoke(old, currentStep);

            OnAllStepsCompleted();
            return;
        }

        int next = Mathf.Clamp(currentStep + 1, 1, Mathf.Max(1, maxStep));
        SetStep(next);

        if (checkpointManager != null && currentStep >= checkpointStartStep)
            checkpointManager.SaveStepCheckpointFromCurrentPosition(currentStep, true);
    }

    private void OnAllStepsCompleted() {
        RefreshSceneBindings();

        sequenceActive = false;

        if (gameManager != null)
            gameManager.EndYSequenceAndEnterPostYChase();

        if (checkpointManager != null)
            checkpointManager.SaveStepCheckpointFromCurrentPosition(currentStep, true);
    }

    public void MoveToPreviousCheckpoint() {
        RollbackAndRespawn();
    }

    public void LoadLastCheckpoint() {
        if (sequenceActive) {
            RollbackAndRespawn();
            return;
        }

        if (checkpointManager != null)
            checkpointManager.LoadLastCheckpoint();
    }

    private void RollbackAndRespawn() {
        if (!sequenceActive) {
            if (checkpointManager != null)
                checkpointManager.LoadLastCheckpoint();
            return;
        }

        int penalty = Mathf.Max(0, rollbackStepPenalty);
        int target = Mathf.Max(1, currentStep - penalty);
        SetStep(target);

        if (checkpointManager == null) {
            Debug.LogWarning("[Chap2YStepSequenceManager] checkpointManager가 없어 되감기 불가");
            return;
        }

        if (!checkpointManager.HasStepCheckpoint(target) && target == 1)
            CacheStep1RuntimeOnly();

        if (checkpointManager.HasStepCheckpoint(target))
            checkpointManager.LoadStepCheckpoint(target);
        else
            checkpointManager.LoadLastCheckpoint();
    }
}