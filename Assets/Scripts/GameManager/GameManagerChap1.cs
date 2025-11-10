using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public class GameManagerChap1 : MonoBehaviour {
    public enum ChapState {
        Idle,
        Hunting,
        Completed,
        ShutterOpened,
        PowerRestoring,
        MainPowerRestored
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

    [Header("GET UI")]
    public GameObject getObject;
    public Image getImage;
    public float getAnimDuration = 0.1f;
    [Range(0, 255)] public int getTargetAlphaByte = 150;
    public float getMoveOffsetY = 70f;

    private RectTransform getRect;
    private Vector2 getBaseAnchoredPos;
    private bool getVisible = false;
    private bool pressablePinged = false;
    private Coroutine getAnimRoutine;
    private TextMeshProUGUI getText;

    [Header("Inspect UI")]
    public GameObject inspectRoot;
    public List<GameObject> inspectItems = new List<GameObject>();
    private float prevTimeScale = 1f;

    [Header("Aux Power")]
    public List<Light> auxPowerLights = new List<Light>();
    [SerializeField] private List<int> auxPowerStates = new List<int> { 0, 0, 0, 0 };

    [System.Serializable]
    private struct StreetLampNode {
        public Light light;
        public AudioSource audio;
        public Transform t;
        public float dist;
    }
    [Header("Street Lamps")]
    [SerializeField] private float lampStepInterval = 0.10f;
    private List<StreetLampNode> streetLamps = new List<StreetLampNode>();

    private void Awake() {
        getRect = getObject.GetComponent<RectTransform>();
        getBaseAnchoredPos = getRect.anchoredPosition;
        getObject.SetActive(false);
        var c = getImage.color;
        c.a = 0f;
        getImage.color = c;
        getRect.anchoredPosition = getBaseAnchoredPos;
        getText = getObject.transform.Find("GetText").GetComponent<TextMeshProUGUI>();
        inspectRoot.SetActive(false);
        for (int i = 0; i < inspectItems.Count; i++)
            inspectItems[i].SetActive(false);
        NormalizeAuxStates();
        ApplyAuxColors();

        CollectAndPrepareStreetLamps();
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

    private void OnDisable() {
        StopCoroutine(getAnimRoutine);
        getAnimRoutine = null;
        getVisible = false;
        getObject.SetActive(false);
    }

    public void StartHunt(ButtonChecker originChecker) {
        if (state == ChapState.Hunting || state == ChapState.Completed || state == ChapState.ShutterOpened || state == ChapState.PowerRestoring || state == ChapState.MainPowerRestored) return;
        activeChecker = originChecker;
        allButtons.Clear();
        allButtons.AddRange(FindObjectsByType<ButtonInteract>(FindObjectsSortMode.None));
        totalButtons = allButtons.Count;
        pressedSet.Clear();
        pressedCount = 0;
        OnPressedCountChanged.Invoke(pressedCount);
        foreach (var bi in allButtons)
            bi.PrepareForHunt();
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
        if (totalButtons > 0 && pressedCount >= totalButtons)
            MarkCompleted();
    }

    public void IncrementButtonCount() {
        if (state != ChapState.Hunting)
            return;
        pressedCount++;
        OnPressedCountChanged.Invoke(pressedCount);
        if (totalButtons > 0 && pressedCount >= totalButtons)
            MarkCompleted();
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

    public void Pressable(int mode) {
        if (mode == 1)
            getText.text = "누르기(F)";
        else if (mode == 2)
            getText.text = "조사하기(F)";
        pressablePinged = true;
    }

    public void Inspect(string sourceName) {
        for (int i = 0; i < inspectItems.Count; i++)
            inspectItems[i].SetActive(false);
        GameObject target = null;
        for (int i = 0; i < inspectItems.Count; i++) {
            var go = inspectItems[i];
            if (go.name == sourceName) {
                target = go;
                break;
            }
        }
        target.SetActive(true);
        inspectRoot.SetActive(true);
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseInspect() {
        for (int i = 0; i < inspectItems.Count; i++)
            inspectItems[i].SetActive(false);
        inspectRoot.SetActive(false);
        Time.timeScale = prevTimeScale == 0f ? 1f : prevTimeScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ShowGetOnce() {
        if (getAnimRoutine != null)
            StopCoroutine(getAnimRoutine);
        getAnimRoutine = StartCoroutine(CoShowGet());
    }

    private void HideGetOnce() {
        if (getAnimRoutine != null)
            StopCoroutine(getAnimRoutine);
        getAnimRoutine = StartCoroutine(CoHideGet());
    }

    private IEnumerator CoShowGet() {
        getObject.SetActive(true);
        getVisible = true;
        float dur = Mathf.Max(0.0001f, getAnimDuration);
        float t = 0f;
        var col = getImage.color;
        col.a = 0f;
        getImage.color = col;
        getRect.anchoredPosition = getBaseAnchoredPos;
        float targetA = getTargetAlphaByte / 255f;
        Vector2 from = getBaseAnchoredPos;
        Vector2 to = getBaseAnchoredPos + new Vector2(0f, getMoveOffsetY);
        while (t < dur) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            var c = getImage.color;
            c.a = Mathf.Lerp(0f, targetA, e);
            getImage.color = c;
            getRect.anchoredPosition = Vector2.Lerp(from, to, e);
            yield return null;
        }
        var c2 = getImage.color;
        c2.a = targetA;
        getImage.color = c2;
        getRect.anchoredPosition = to;
        getAnimRoutine = null;
    }

    private IEnumerator CoHideGet() {
        float dur = Mathf.Max(0.0001f, getAnimDuration);
        float t = 0f;
        getVisible = false;
        float startA = getImage.color.a;
        Vector2 from = getRect.anchoredPosition;
        Vector2 to = getBaseAnchoredPos;
        while (t < dur) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            var c = getImage.color;
            c.a = Mathf.Lerp(startA, 0f, e);
            getImage.color = c;
            getRect.anchoredPosition = Vector2.Lerp(from, to, e);
            yield return null;
        }
        var c2 = getImage.color;
        c2.a = 0f;
        getImage.color = c2;
        getRect.anchoredPosition = to;
        yield return new WaitForEndOfFrame();
        getObject.SetActive(false);
        getAnimRoutine = null;
    }

    private void NormalizeAuxStates() {
        if (auxPowerStates == null)
            auxPowerStates = new List<int>();
        while (auxPowerStates.Count < 4)
            auxPowerStates.Add(0);
        if (auxPowerStates.Count > 4)
            auxPowerStates.RemoveRange(4, auxPowerStates.Count - 4);
    }

    private void ApplyAuxColors() {
        int n = Mathf.Min(auxPowerLights.Count, auxPowerStates.Count);
        for (int i = 0; i < n; i++) {
            if (auxPowerLights[i] == null)
                continue;
            auxPowerLights[i].color = auxPowerStates[i] == 1 ? Color.green : Color.red;
        }
    }

    public void SetAuxPowerState(int index, int value) {
        SetAuxState(index, value);
    }

    public bool AreAllAuxOn() {
        if (auxPowerStates == null || auxPowerStates.Count < 4)
            return false;
        for (int i = 0; i < 4; i++) {
            if (auxPowerStates[i] != 1)
                return false;
        }
        return true;
    }

    public void TryConfirmMainPower() {
        if (state == ChapState.ShutterOpened)
            state = ChapState.PowerRestoring;
        if (AreAllAuxOn() && state != ChapState.MainPowerRestored) {
            StartCoroutine(CoLightUpStreetLamps());
            state = ChapState.MainPowerRestored;
        }
    }

    public bool IsAuxOn(int index) {
        if (index < 0 || index >= auxPowerStates.Count)
            return false;
        return auxPowerStates[index] == 1;
    }

    public void SetAuxState(int index, int value) {
        if (index < 0 || index >= auxPowerStates.Count)
            return;
        int v = (value != 0) ? 1 : 0;
        if (auxPowerStates[index] == v)
            return;
        auxPowerStates[index] = v;
        if (index < auxPowerLights.Count && auxPowerLights[index] != null) {
            auxPowerLights[index].color = (v == 1) ? Color.green : Color.red;
        } else {
            ApplyAuxColors();
        }
    }

    private void Update() {
        ApplyAuxColors();
    }

    private void Start() {
        state = ChapState.PowerRestoring;
        Debug.Log($"[DEBUG] GameManagerChap1: 초기 상태를 {state} 로 설정");
    }

    public void NorthEasternAreaHintAvailable() {
        Debug.Log("힌트 사용 가능!");
    }

    private void CollectAndPrepareStreetLamps() {
        streetLamps.Clear();
        var origin = transform.position;

        var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < allLights.Length; i++) {
            var l = allLights[i];
            if (l.gameObject.name == "StreetLamp") {
                var a = l.GetComponent<AudioSource>();
                a.spatialBlend = 1f;
                l.enabled = false;

                StreetLampNode node = new StreetLampNode {
                    light = l,
                    audio = a,
                    t = l.transform,
                    dist = Vector3.Distance(origin, l.transform.position)
                };
                streetLamps.Add(node);
            }
        }

        streetLamps.Sort((x, y) => x.dist.CompareTo(y.dist));
    }

    private IEnumerator CoLightUpStreetLamps() {
        yield return new WaitForSeconds(3f);

        for (int i = 0; i < streetLamps.Count; i++) {
            var node = streetLamps[i];
            node.light.enabled = true;
            node.audio.Play();
            yield return new WaitForSeconds(lampStepInterval);
        }
    }
}