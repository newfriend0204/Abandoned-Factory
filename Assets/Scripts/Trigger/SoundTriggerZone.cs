using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(Collider))]
public class SoundTriggerZone : MonoBehaviour {
    [Header("Trigger Filter")]
    public string playerTag = "Player";

    [Header("Sound")]
    public AudioClip soundClip;
    public float soundDelay = 0f;
    public Transform soundOrigin;
    [Range(0f, 1f)]
    public float spatialBlend = 1f;
    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Mixer Group")]
    public AudioMixerGroup outputGroup;

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

    private IEnumerator PlaySoundRoutine() {
        if (soundDelay > 0f)
            yield return new WaitForSeconds(soundDelay);

        Vector3 pos = transform.position;

        if (soundOrigin != null) {
            pos = soundOrigin.position;
        }

        GameObject temp = new GameObject("TriggerSoundZone_Audio");
        temp.transform.position = pos;

        AudioSource src = temp.AddComponent<AudioSource>();
        src.clip = soundClip;
        src.volume = Mathf.Clamp01(volume);
        src.spatialBlend = Mathf.Clamp01(spatialBlend);
        src.playOnAwake = false;
        src.loop = false;

        if (outputGroup != null) {
            src.outputAudioMixerGroup = outputGroup;
        }

        src.Play();

        if (src.clip != null) {
            Destroy(temp, src.clip.length + 0.1f);
        } else {
            Destroy(temp, 1f);
        }
    }
}