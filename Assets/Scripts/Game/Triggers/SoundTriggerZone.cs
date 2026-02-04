using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SoundTriggerZone : MonoBehaviour {
    [Header("Trigger Filter")]
    public string playerTag = "Player";

    [Header("Sound")]
    public AudioClip soundClip;
    public float soundDelay = 0f;

    [Tooltip("If set, uses the AudioSource on this object. Otherwise tries soundOrigin's AudioSource.")]
    public AudioSource targetSource;

    [Tooltip("Optional. If targetSource is null, we'll look for an AudioSource on this Transform.")]
    public Transform soundOrigin;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Repeat")]
    public bool allowRepeat = false;
    public float repeatCooldown = 3f;

    private bool hasPlayed = false;
    private float lastPlayTime = -999f;

    private void Awake() {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag(playerTag))
            return;

        if (!CanPlay())
            return;

        StartCoroutine(PlaySoundRoutine());
    }

    private bool CanPlay() {
        if (!allowRepeat) {
            if (hasPlayed)
                return false;

            hasPlayed = true;
            return true;
        }

        if (Time.time - lastPlayTime < repeatCooldown)
            return false;

        lastPlayTime = Time.time;
        return true;
    }

    private AudioSource ResolveSource() {
        if (targetSource != null)
            return targetSource;

        if (soundOrigin != null) {
            AudioSource found = soundOrigin.GetComponent<AudioSource>();
            if (found != null)
                return found;
        }

        return null;
    }

    private IEnumerator PlaySoundRoutine() {
        if (soundClip == null)
            yield break;

        if (soundDelay > 0f)
            yield return new WaitForSeconds(soundDelay);

        AudioSource src = ResolveSource();
        if (src == null) {
            yield break;
        }

        src.PlayOneShot(soundClip, Mathf.Clamp01(volume));
    }
}