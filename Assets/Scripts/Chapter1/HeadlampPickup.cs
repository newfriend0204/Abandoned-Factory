using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class HeadlampPickup : MonoBehaviour {
    [Header("Refs")]
    public GameManagerChap1 gameManager;
    public Transform player;
    public Camera viewCamera;
    public HeadlampController headlampController;

    [Header("Outline")]
    [SerializeField] private Outline outline;

    [Header("Interact Distances")]
    public float outlineDistance = 7f;
    public float interactDistance = 2.5f;

    [Header("Monologue")]
    [TextArea] public string firstLine;
    public float firstDuration = 3f;
    [TextArea] public string secondLine;
    public float delayBetweenLines = 3f;
    public float secondDuration = 3f;

    [Header("Tutorial UI")]
    public TutorialHintUI tutorialUI;

    private Collider[] interactColliders;
    private bool pickedUp = false;

    void Awake() {
        if (player == null) {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.transform;
        }

        if (viewCamera == null)
            viewCamera = Camera.main;

        interactColliders = GetComponentsInChildren<Collider>(true);

        if (outline != null)
            outline.enabled = false;
    }

    void Update() {
        if (pickedUp)
            return;

        if (player == null || viewCamera == null || gameManager == null)
            TryResolveDynamicRefs();

        bool hintOn = IsInteractHintOn();

        float distance = float.MaxValue;
        bool inOutlineRange = false;

        if (player != null) {
            distance = Vector3.Distance(player.position, transform.position);
            inOutlineRange = distance <= outlineDistance;
        }

        if (outline != null)
            outline.enabled = hintOn && inOutlineRange;

        bool lookingAt = false;
        if (viewCamera != null && interactColliders != null && interactColliders.Length > 0) {
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance)) {
                for (int i = 0; i < interactColliders.Length; i++) {
                    if (hit.collider == interactColliders[i]) {
                        lookingAt = true;
                        break;
                    }
                }
            }
        }

        if (hintOn && inOutlineRange && lookingAt && gameManager != null)
            gameManager.Pressable(4);

        if (lookingAt && distance <= interactDistance && WasInteractPressedThisFrame())
            HandlePickup();
    }

    private void HandlePickup() {
        pickedUp = true;

        HideVisuals();

        if (headlampController != null)
            headlampController.canUseHeadlamp = true;

        if (tutorialUI != null)
            tutorialUI.ShowTutorial(1, 5f);

        if (gameManager != null && gameManager.monologue != null)
            StartCoroutine(MonologueSequence());
    }

    private IEnumerator MonologueSequence() {
        if (!string.IsNullOrEmpty(firstLine))
            gameManager.monologue.ShowMessage(firstLine, firstDuration, false);

        if (!string.IsNullOrEmpty(secondLine)) {
            yield return new WaitForSeconds(delayBetweenLines);
            gameManager.monologue.ShowMessage(secondLine, secondDuration, false);
        }
    }

    private void TryResolveDynamicRefs() {
        if (player == null) {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.transform;
        }

        if (viewCamera == null)
            viewCamera = Camera.main;
    }

    private bool WasInteractPressedThisFrame() {
        var input = InputSettingsManager.Instance;
        if (input == null)
            return false;

        return input.GetKeyDown("Interact");
    }

    private bool IsInteractHintOn() {
        var sm = SettingsManager.Instance;
        if (sm == null)
            return true;

        int v = sm.GetInt("InteractHint", 0);
        return v == 0;
    }

    private void HideVisuals() {
        if (outline != null)
            outline.enabled = false;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.enabled = false;

        var lights = GetComponentsInChildren<Light>(true);
        foreach (var l in lights)
            l.enabled = false;

        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
            c.enabled = false;
    }

    public void RestorePickedStateFromCheckpoint() {
        if (pickedUp)
            return;

        pickedUp = true;
        HideVisuals();
    }
}