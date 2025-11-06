using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManagerChap1 : MonoBehaviour {
    public enum ChapState {
        Idle,
        Hunting,
        Completed,
        ShutterOpened
    }
    [SerializeField] private ChapState state = ChapState.Idle;
    public ChapState State => state;

    [Header("Progress")]
    [SerializeField] private int totalButtons = 0;

    [SerializeField] private int pressedCount = 0;
    public int PressedCount => pressedCount;

    [System.Serializable] public class IntEvent : UnityEvent<int> { }

    [Header("Events")]
    public IntEvent OnPressedCountChanged;
    public UnityEvent OnAllButtonsActivated;

    private readonly HashSet<ButtonInteract> pressedSet = new HashSet<ButtonInteract>();
    private List<ButtonInteract> allButtons = new List<ButtonInteract>();
    private ButtonChecker activeChecker;

    public void StartHunt(ButtonChecker originChecker) {
        if (state == ChapState.Hunting || state == ChapState.Completed || state == ChapState.ShutterOpened) {
            return;
        }

        activeChecker = originChecker;

        allButtons.Clear();
        allButtons.AddRange(FindObjectsByType<ButtonInteract>(FindObjectsSortMode.None));

        totalButtons = allButtons.Count;
        pressedSet.Clear();
        pressedCount = 0;
        OnPressedCountChanged.Invoke(pressedCount);

        foreach (var bi in allButtons) {
            bi.PrepareForHunt();
        }

        activeChecker.SetIndicatorHunting();
        state = ChapState.Hunting;
    }

    public void ReportPressed(ButtonInteract bi) {
        if (state != ChapState.Hunting)
            return;
        if (pressedSet.Contains(bi))
            return;

        pressedSet.Add(bi);
        pressedCount++;
        OnPressedCountChanged.Invoke(pressedCount);

        if (totalButtons > 0 && pressedCount >= totalButtons) {
            MarkCompleted();
        }
    }

    public void IncrementButtonCount() {
        if (state != ChapState.Hunting)
            return;
        pressedCount++;
        OnPressedCountChanged.Invoke(pressedCount);

        if (totalButtons > 0 && pressedCount >= totalButtons) {
            MarkCompleted();
        }
    }

    public void ResetPressedCount() {
        pressedSet.Clear();
        pressedCount = 0;
        totalButtons = 0;
        state = ChapState.Idle;
        OnPressedCountChanged.Invoke(pressedCount);

        activeChecker.SetIndicatorIdle();
        activeChecker = null;
    }

    private void MarkCompleted() {
        state = ChapState.Completed;

        activeChecker.SetIndicatorCompleted();
        OnAllButtonsActivated?.Invoke();
    }

    public void SealShutterOpened() {
        state = ChapState.ShutterOpened;
    }

    public void Pressable() {
        Debug.Log("누를 수 있어.");
    }

    public void Inspect(string sourceName) {
        Debug.Log(sourceName);
    }
}
