using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManagerChap2 : MonoBehaviour {
    public enum Chap2State { Idle = 0, YSequence = 1, PostYChase = 2 }

    [Header("Core Refs")]
    [SerializeField] private Chap2MonsterController monsterController;

    [Header("Debug")]
    [SerializeField] private bool debugDisableMonsterSpawnOnYSequence = false;
    [SerializeField] private bool debugPreventPlayerDeath = false;
    [SerializeField] private bool debugSpawnMonsterOnPostYChase = false;

    [Header("Interaction UI")]
    public GameObject getObject;
    public Image getImage;
    public TextMeshProUGUI getText;
    public float getAnimDuration = 0.1f;
    [Range(0, 255)] public int getTargetAlphaByte = 150;
    public float getMoveOffsetY = 70f;

    private RectTransform getRect;
    private Vector2 getBaseAnchoredPos;
    private bool getVisible = false;
    private bool pressablePinged = false;
    private Coroutine getAnimRoutine;

    public Chap2State State { get; private set; } = Chap2State.Idle;

    public bool PreventPlayerDeath => debugPreventPlayerDeath;
    public bool IsMonsterSpawnSuppressed => debugDisableMonsterSpawnOnYSequence;

    private void Awake() {
        if (getObject != null) {
            getRect = getObject.GetComponent<RectTransform>();
            getBaseAnchoredPos = getRect.anchoredPosition;
            getObject.SetActive(false);

            if (getImage != null) {
                Color c = getImage.color;
                c.a = 0f;
                getImage.color = c;
            }

            if (getText != null) {
                Color c2 = getText.color;
                c2.a = 0f;
                getText.color = c2;
            }

            getRect.anchoredPosition = getBaseAnchoredPos;
        }
    }

    void Start() {
        TryRestoreStateFromSave();
    }

    private void TryRestoreStateFromSave() {
        int savedStateInt = Chap2CheckpointManager.GetSavedChap2StateIntOrDefault((int)Chap2State.Idle);
        if (savedStateInt < 0)
            savedStateInt = 0;
        
        if (savedStateInt > (int)Chap2State.PostYChase)
            savedStateInt = (int)Chap2State.PostYChase;

        Chap2State saved = (Chap2State)savedStateInt;

        if (saved == Chap2State.YSequence) {
            StartYSequence();
            return;
        }

        State = saved;

        if (monsterController != null && State != Chap2State.YSequence) {
            if (State == Chap2State.PostYChase && debugSpawnMonsterOnPostYChase)
                return;

            monsterController.ForceHide();
        }
    }

    private void LateUpdate() {
        if (pressablePinged) {
            if (!getVisible)
                ShowGetOnce();
        } else {
            if (getVisible)
                HideGetOnce();
        }

        pressablePinged = false;
    }

    public void Pressable(int mode = 0) {
        if (getText == null)
            return;

        string keyLabel = GetInteractKeyLabel();

        switch (mode) {
            case 1: getText.text = $"들어가기({keyLabel})"; break;
            case 2: getText.text = $"나가기({keyLabel})"; break;
            case 3: getText.text = $"누르기(좌클릭)"; break;
            case 4: getText.text = $"돌리기({keyLabel})"; break;
            default: getText.text = $"상호작용({keyLabel})"; break;
        }

        pressablePinged = true;
    }

    private string GetInteractKeyLabel() {
        string fallback = "F";

        var ism = InputSettingsManager.Instance;
        if (ism == null)
            return fallback;

        KeyCode primary = ism.GetPrimaryKey("Interact");
        KeyCode secondary = ism.GetSecondaryKey("Interact");

        if (primary == KeyCode.None && secondary == KeyCode.None)
            return fallback;

        var parts = new List<string>();
        if (primary != KeyCode.None)
            parts.Add(ism.FormatKeyName(primary));
        if (secondary != KeyCode.None)
            parts.Add(ism.FormatKeyName(secondary));

        if (parts.Count == 0)
            return fallback;

        return string.Join(", ", parts);
    }

    private void ShowGetOnce() {
        if (getObject == null)
            return;

        if (getAnimRoutine != null)
            StopCoroutine(getAnimRoutine);

        getAnimRoutine = StartCoroutine(CoShowGet());
    }

    private void HideGetOnce() {
        if (getObject == null)
            return;

        if (getAnimRoutine != null)
            StopCoroutine(getAnimRoutine);

        getAnimRoutine = StartCoroutine(CoHideGet());
    }

    private IEnumerator CoShowGet() {
        getObject.SetActive(true);

        Vector2 from = getBaseAnchoredPos + new Vector2(0f, -getMoveOffsetY);
        Vector2 to = getBaseAnchoredPos;

        float targetAlpha = getTargetAlphaByte / 255f;

        float t = 0f;
        getVisible = true;

        while (t < getAnimDuration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / getAnimDuration);

            if (getRect != null)
                getRect.anchoredPosition = Vector2.Lerp(from, to, k);

            if (getImage != null) {
                Color c = getImage.color;
                c.a = Mathf.Lerp(0f, targetAlpha, k);
                getImage.color = c;
            }

            if (getText != null) {
                Color c2 = getText.color;
                c2.a = Mathf.Lerp(0f, 1f, k);
                getText.color = c2;
            }

            yield return null;
        }

        if (getRect != null)
            getRect.anchoredPosition = to;

        if (getImage != null) {
            Color c = getImage.color;
            c.a = targetAlpha;
            getImage.color = c;
        }

        if (getText != null) {
            Color c2 = getText.color;
            c2.a = 1f;
            getText.color = c2;
        }

        getAnimRoutine = null;
    }

    private IEnumerator CoHideGet() {
        Vector2 from = getBaseAnchoredPos;
        Vector2 to = getBaseAnchoredPos + new Vector2(0f, -getMoveOffsetY);

        float targetAlpha = getTargetAlphaByte / 255f;

        float t = 0f;
        getVisible = false;

        while (t < getAnimDuration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / getAnimDuration);

            if (getRect != null)
                getRect.anchoredPosition = Vector2.Lerp(from, to, k);

            if (getImage != null) {
                Color c = getImage.color;
                c.a = Mathf.Lerp(targetAlpha, 0f, k);
                getImage.color = c;
            }

            if (getText != null) {
                Color c2 = getText.color;
                c2.a = Mathf.Lerp(1f, 0f, k);
                getText.color = c2;
            }

            yield return null;
        }

        if (getRect != null)
            getRect.anchoredPosition = to;

        if (getImage != null) {
            Color c = getImage.color;
            c.a = 0f;
            getImage.color = c;
        }

        if (getText != null) {
            Color c2 = getText.color;
            c2.a = 0f;
            getText.color = c2;
        }

        getObject.SetActive(false);
        getAnimRoutine = null;
    }

    public void StartYSequence() {
        State = Chap2State.YSequence;

        if (monsterController == null)
            return;

        if (debugDisableMonsterSpawnOnYSequence) {
            monsterController.ForceHide();
            return;
        }

        monsterController.BeginYSequenceSpawnDelay();
    }

    public void StartYSequenceFromBranch(Chap2MonsterController.MonsterBranch branch, bool ignoreView = false) {
        State = Chap2State.YSequence;

        if (monsterController == null)
            return;

        if (debugDisableMonsterSpawnOnYSequence) {
            monsterController.ForceHide();
            return;
        }

        monsterController.BeginYSequenceSpawnDelayFromBranch(branch, ignoreView);
    }

    public void ReportPlayerHiding(LockerInteractable locker) {
        if (monsterController != null)
            monsterController.NotifyPlayerHiding(locker, locker.outsidePoint);
    }

    public void ReportPlayerExiting() {
        if (monsterController != null)
            monsterController.NotifyPlayerExiting();
    }

    public void EndYSequenceAndEnterPostYChase() {
        State = Chap2State.PostYChase;

        if (monsterController != null && !debugSpawnMonsterOnPostYChase)
            monsterController.ForceHide();
    }

    public Chap2MonsterController Monster => monsterController;
}