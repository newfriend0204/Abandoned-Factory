using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MonologueTriggerZone : MonoBehaviour {
    [Header("Target Monologue")]
    public MonologueManager monologueManager;

    [Header("Trigger Filter")]
    public string playerTag = "Player";

    [Header("Message (Single)")]
    [TextArea(2, 4)]
    public string message;

    [Header("Random Messages")]
    [TextArea(2, 4)]
    public List<string> randomMessages = new List<string>();

    [Header("Visible Duration")]
    public float visibleDurationOverride = 0f;

    [Header("Typewriter")]
    public bool useTypewriter = true;
    public bool overrideTypewriterSpeed = false;
    public float typewriterCharsPerSecond = 40f;

    [Header("Delay")]
    public float triggerDelay = 0f;

    [Header("Repeat")]
    public bool allowRepeat = false;
    public float repeatCooldown = 3f;

    private bool hasTriggered = false;
    private float lastTriggerTime = -999f;

    private void Awake() {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        if (monologueManager == null) {
            monologueManager = FindFirstObjectByType<MonologueManager>();
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag(playerTag))
            return;

        string finalMessage = GetMessageToUse();
        if (string.IsNullOrEmpty(finalMessage))
            return;

        if (!allowRepeat) {
            if (hasTriggered)
                return;

            hasTriggered = true;
        } else {
            if (Time.time - lastTriggerTime < repeatCooldown)
                return;

            lastTriggerTime = Time.time;
        }

        if (monologueManager == null) {
            monologueManager = FindFirstObjectByType<MonologueManager>();
            if (monologueManager == null)
                return;
        }

        StartCoroutine(PlayMonologueRoutine(finalMessage));
    }

    private IEnumerator PlayMonologueRoutine(string finalMessage) {
        if (triggerDelay > 0f) {
            yield return new WaitForSeconds(triggerDelay);
        }

        if (monologueManager == null)
            yield break;

        float originalSpeed = 0f;
        bool changedSpeed = false;

        if (overrideTypewriterSpeed && useTypewriter && typewriterCharsPerSecond > 0f) {
            originalSpeed = monologueManager.typewriterCharsPerSecond;
            monologueManager.typewriterCharsPerSecond = typewriterCharsPerSecond;
            changedSpeed = true;
        }

        if (visibleDurationOverride > 0f) {
            monologueManager.ShowMessage(finalMessage, visibleDurationOverride, useTypewriter);
        } else {
            monologueManager.ShowMessage(finalMessage, monologueManager.defaultVisibleDuration, useTypewriter);
        }

        if (changedSpeed) {
            monologueManager.typewriterCharsPerSecond = originalSpeed;
        }
    }

    private string GetMessageToUse() {
        if (randomMessages != null && randomMessages.Count > 0) {
            var candidates = new List<string>();
            for (int i = 0; i < randomMessages.Count; i++) {
                if (!string.IsNullOrEmpty(randomMessages[i])) {
                    candidates.Add(randomMessages[i]);
                }
            }

            if (candidates.Count > 0) {
                int idx = Random.Range(0, candidates.Count);
                return candidates[idx];
            }
        }
        return message;
    }
}