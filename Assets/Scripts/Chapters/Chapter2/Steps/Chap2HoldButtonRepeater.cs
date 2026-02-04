using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Chap2HoldButtonRepeater : MonoBehaviour {
    [Header("Tick Event")]
    public UnityEvent onTick;

    [Header("Timing (Hold Repeat)")]
    [SerializeField] private float initialDelaySeconds = 0.25f;
    [SerializeField] private float startIntervalSeconds = 0.18f;
    [SerializeField] private float minIntervalSeconds = 0.045f;
    [SerializeField] private float rampSeconds = 0.90f;

    [Header("Optional SFX (If you want tick here)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip tickClip;
    [SerializeField] private bool playTickOnEachStep = false;
    [Range(0f, 1f)]
    [SerializeField] private float tickVolume = 1.0f;

    private bool pressed;
    private float pressStartTime;
    private Coroutine repeatRoutine;

    public void PressDown() {
        if (pressed)
            return;

        pressed = true;
        pressStartTime = Time.time;

        FireOnce();

        if (repeatRoutine != null)
            StopCoroutine(repeatRoutine);

        repeatRoutine = StartCoroutine(CoRepeat());
    }

    public void PressUp() {
        pressed = false;

        if (repeatRoutine != null) {
            StopCoroutine(repeatRoutine);
            repeatRoutine = null;
        }
    }

    private IEnumerator CoRepeat() {
        float start = Time.time;

        float d = Mathf.Max(0f, initialDelaySeconds);
        if (d > 0f)
            yield return new WaitForSeconds(d);

        while (pressed) {
            float elapsed = Time.time - start;
            float t = Mathf.Clamp01(rampSeconds <= 0f ? 1f : elapsed / rampSeconds);

            float interval = Mathf.Lerp(startIntervalSeconds, minIntervalSeconds, t);
            interval = Mathf.Max(0.005f, interval);

            yield return new WaitForSeconds(interval);

            if (!pressed)
                yield break;

            FireOnce();
        }
    }

    private void FireOnce() {
        if (onTick != null)
            onTick.Invoke();

        if (!playTickOnEachStep)
            return;

        if (sfxSource == null || tickClip == null)
            return;

        sfxSource.PlayOneShot(tickClip, tickVolume);
    }
}