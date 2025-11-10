using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RunGauge : MonoBehaviour {
    private PlayerController pc;
    private Image img;
    private RectTransform rt;

    private Image bg;
    private RectTransform bgRt;

    private float maxWidth;
    private float basePosX;

    [SerializeField] private float fadeOutSpeed = 1.2f;
    private float uiAlpha = 1f;
    private float prevRatio = 1f;

    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Red = new Color32(255, 0, 0, 255);
    private static readonly Color32 Black = new Color32(0, 0, 0, 255);

    private void Start() {
        pc = FindFirstObjectByType<PlayerController>();
        img = GetComponent<Image>();
        rt = GetComponent<RectTransform>();

        rt.pivot = new Vector2(0.5f, rt.pivot.y);

        maxWidth = rt.sizeDelta.x;
        basePosX = rt.anchoredPosition.x;

        var go = new GameObject("RunGaugeEmptyBG", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform.parent, false);
        bg = go.GetComponent<Image>();
        bgRt = go.GetComponent<RectTransform>();

        bgRt.anchorMin = rt.anchorMin;
        bgRt.anchorMax = rt.anchorMax;
        bgRt.pivot = new Vector2(0.5f, rt.pivot.y);
        bgRt.anchoredPosition = rt.anchoredPosition;
        bgRt.sizeDelta = rt.sizeDelta;
        bg.color = Black;
        bg.raycastTarget = false;

        int me = transform.GetSiblingIndex();
        bg.transform.SetSiblingIndex(me);
        transform.SetSiblingIndex(me + 1);
    }

    private void Update() {
        float ratio = pc.sprintStamina / pc.sprintStaminaMax;

        Color32 baseFillColor = pc.isExhausted ? Red : White;

        if (prevRatio >= 1f && ratio < 1f) {
            uiAlpha = 1f;
        } else if (ratio >= 1f) {
            uiAlpha = Mathf.MoveTowards(uiAlpha, 0f, fadeOutSpeed * Time.deltaTime);
        } else {
            uiAlpha = 1f;
        }

        byte a = (byte)Mathf.RoundToInt(255f * uiAlpha);
        img.color = new Color32(baseFillColor.r, baseFillColor.g, baseFillColor.b, a);
        bg.color = new Color32(Black.r, Black.g, Black.b, a);

        float w = maxWidth * Mathf.Clamp01(ratio);
        var size = rt.sizeDelta; size.x = w; rt.sizeDelta = size;

        var pos = rt.anchoredPosition; pos.x = basePosX; rt.anchoredPosition = pos;

        bgRt.sizeDelta = new Vector2(maxWidth, bgRt.sizeDelta.y);
        var bgPos = bgRt.anchoredPosition; bgPos.x = basePosX; bgRt.anchoredPosition = bgPos;

        prevRatio = ratio;
    }
}
