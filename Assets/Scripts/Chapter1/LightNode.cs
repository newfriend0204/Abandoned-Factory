using System.Collections;
using UnityEngine;

public class LightNode : MonoBehaviour {
    [Header("Light")]
    public Light lightComp;
    public bool isOn;
    public float onIntensity = 4f;
    public float offIntensity = 0f;
    public float tweenTime = 0.08f;

    [Header("Gaze / Outline / Pressable")]
    public Transform player;
    public Camera viewCamera;
    public GameManagerChap1 gameManager;
    public BoxCollider targetBox;
    public Outline outline;
    public float gazeDistance = 2f;

    private bool interactionLocked = false;

    void Awake() {
        if (lightComp == null)
            lightComp = transform.Find("Light").GetComponent<Light>();
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap1>();
        if (player == null) {
            var pc = FindFirstObjectByType<PlayerController>();
            player = pc.transform;
        }
        if (viewCamera == null)
            viewCamera = Camera.main;

        if (targetBox == null) {
            var boxes = GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < boxes.Length; i++) {
                if (boxes[i].transform != transform) {
                    targetBox = boxes[i];
                    break;
                }
            }
        }
        outline = targetBox.GetComponent<Outline>();

        lightComp.intensity = isOn ? onIntensity : offIntensity;
        outline.enabled = false;
    }

    void Update() {
        if (interactionLocked)
            return;

        bool looking = IsInteractable(viewCamera);
        if (outline != null)
            outline.enabled = looking;
        if (looking)
            gameManager.Pressable(1);
    }

    public bool IsInteractable(Camera cam) {
        if (interactionLocked || cam == null || targetBox == null)
            return false;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)) {
            if (hit.collider == targetBox) {
                Vector3 closest = targetBox.bounds.ClosestPoint(player.position);
                float dist = Vector3.Distance(player.position, closest);
                return dist <= gazeDistance;
            }
        }
        return false;
    }

    public void LockInteraction(bool locked) {
        interactionLocked = locked;
        outline.enabled = false;
        targetBox.enabled = !locked;
    }

    public void SetState(bool on, bool animate = true) {
        isOn = on;
        if (animate) {
            StopAllCoroutines();
            StartCoroutine(TweenLight(on ? onIntensity : offIntensity));
        } else {
            lightComp.intensity = on ? onIntensity : offIntensity;
        }
    }

    public void Toggle() {
        SetState(!isOn, animate: true);
        StopCoroutine(nameof(PunchScale));
        StartCoroutine(nameof(PunchScale));
    }

    public void Blink(int times = 2, float oneBlink = 0.05f) {
        StopCoroutine(nameof(BlinkRoutine));
        StartCoroutine(BlinkRoutine(times, oneBlink));
    }

    IEnumerator BlinkRoutine(int times, float oneBlink) {
        float onVal = onIntensity;
        float offVal = offIntensity;
        for (int i = 0; i < times; i++) {
            lightComp.intensity = isOn ? offVal : onVal;
            yield return new WaitForSeconds(oneBlink);
            lightComp.intensity = isOn ? onVal : offVal;
            yield return new WaitForSeconds(oneBlink);
        }
    }

    IEnumerator TweenLight(float target) {
        float start = lightComp.intensity;
        float t = 0f;
        while (t < tweenTime) {
            t += Time.deltaTime;
            lightComp.intensity = Mathf.Lerp(start, target, t / tweenTime);
            yield return null;
        }
        lightComp.intensity = target;
    }

    IEnumerator PunchScale() {
        Transform tr = transform;
        Vector3 baseS = tr.localScale;
        Vector3 upS = baseS * 1.05f;
        float half = tweenTime * 0.5f;
        float t = 0f;
        while (t < half) {
            t += Time.deltaTime;
            tr.localScale = Vector3.Lerp(baseS, upS, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half) {
            t += Time.deltaTime;
            tr.localScale = Vector3.Lerp(upS, baseS, t / half);
            yield return null;
        }
        tr.localScale = baseS;
    }
}