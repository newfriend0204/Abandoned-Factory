using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Chap2StepDescriptionPresenter : MonoBehaviour {
    [System.Serializable]
    public class StepEntry {
        [TextArea(2, 10)]
        public string description;
    }

    [Header("Refs")]
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private Chap2YStepSequenceManager sequenceManager;
    [SerializeField] private GameManagerChap2 gameManager;

    [Header("Rules")]
    [SerializeField] private int maxStep = 7;

    [Header("Formatting")]
    [SerializeField] private bool showStepPrefix = true;
    [SerializeField] private string stepPrefixFormat = "STEP {0:00}\n";
    [TextArea(1, 6)]
    [SerializeField] private string idleText = "";
    [TextArea(1, 6)]
    [SerializeField] private string doneText = "SEQUENCE COMPLETE.";

    [Header("Step Descriptions (1~7)")]
    [SerializeField]
    private List<StepEntry> steps = new List<StepEntry>() {
        new StepEntry { description = "Step 01 description..." },
        new StepEntry { description = "Step 02 description..." },
        new StepEntry { description = "Step 03 description..." },
        new StepEntry { description = "Step 04 description..." },
        new StepEntry { description = "Step 05 description..." },
        new StepEntry { description = "Step 06 description..." },
        new StepEntry { description = "Step 07 description..." },
    };

    private int lastRawStep = int.MinValue;
    private int lastStateInt = int.MinValue;

    private void Awake() {
        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>();

        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();
    }

    private void OnEnable() {
        TryBind();
        StartCoroutine(CoRefreshNextFrame());
    }

    private void OnDisable() {
        Unbind();
    }

    private IEnumerator CoRefreshNextFrame() {
        yield return null;
        ForceRefresh();
    }

    private void Update() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap2>();

        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        int raw = sequenceManager != null ? sequenceManager.CurrentStep : 1;
        int stateInt = gameManager != null ? (int)gameManager.State : 0;

        if (raw == lastRawStep && stateInt == lastStateInt)
            return;

        lastRawStep = raw;
        lastStateInt = stateInt;

        ForceRefresh();
    }

    private void TryBind() {
        if (sequenceManager == null)
            sequenceManager = FindFirstObjectByType<Chap2YStepSequenceManager>();

        if (sequenceManager == null)
            return;

        sequenceManager.StepChanged -= OnStepChanged;
        sequenceManager.StepChanged += OnStepChanged;
    }

    private void Unbind() {
        if (sequenceManager == null)
            return;

        sequenceManager.StepChanged -= OnStepChanged;
    }

    private void OnStepChanged(int oldStep, int newStep) {
        ForceRefresh();
    }

    public void ForceRefresh() {
        if (targetText == null)
            return;

        bool isPostDone = gameManager != null && gameManager.State == GameManagerChap2.Chap2State.PostYChase;
        bool isYSequence = gameManager != null && gameManager.State == GameManagerChap2.Chap2State.YSequence;

        if (!isYSequence && !isPostDone) {
            targetText.text = idleText != null ? idleText : "";
            return;
        }

        if (isPostDone) {
            targetText.text = doneText != null ? doneText : "";
            return;
        }

        int raw = sequenceManager != null ? sequenceManager.CurrentStep : 1;
        int step = Mathf.Clamp(raw, 1, Mathf.Max(1, maxStep));

        string desc = GetStepDescription(step);

        if (showStepPrefix) {
            string prefix = string.IsNullOrEmpty(stepPrefixFormat) ? "" : string.Format(stepPrefixFormat, step);
            targetText.text = prefix + desc;
            return;
        }

        targetText.text = desc;
    }

    private string GetStepDescription(int step) {
        int idx = step - 1;
        if (idx < 0)
            return "";

        if (idx >= steps.Count || steps[idx] == null || string.IsNullOrEmpty(steps[idx].description))
            return "(NO DESCRIPTION SET)";

        return steps[idx].description;
    }
}