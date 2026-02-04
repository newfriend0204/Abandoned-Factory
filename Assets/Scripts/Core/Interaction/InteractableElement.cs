using UnityEngine;
using UnityEngine.Events;

public class InteractableElement : MonoBehaviour {
    [Header("Refs")]
    public Outline outline;

    [Header("UI")]
    public int pressableMode = 3;

    [Header("Action (Click)")]
    public UnityEvent onInteract;

    [Header("Press & Hold")]
    public UnityEvent onPressDown;
    public UnityEvent onPressUp;

    private void Awake() {
        if (outline != null)
            outline.enabled = false;
    }

    public void SetHovered(bool hovered) {
        if (outline != null)
            outline.enabled = hovered;
    }

    public void Interact() {
        if (onInteract != null)
            onInteract.Invoke();
    }

    public void PressDown() {
        if (onPressDown != null)
            onPressDown.Invoke();
    }

    public void PressUp() {
        if (onPressUp != null)
            onPressUp.Invoke();
    }
}