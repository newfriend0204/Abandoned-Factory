using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class Keypad : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private TMP_Text display;
    [SerializeField] private Light statusLight;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;

    [SerializeField] private AudioClip successSfx;
    [SerializeField] private AudioClip wrongSfx;
    [SerializeField] private AudioClip buttonSfx;

    [Range(0f, 1f)] public float successVol = 1f;
    [Range(0f, 1f)] public float wrongVol = 1f;
    [Range(0f, 1f)] public float buttonVol = 1f;

    [Header("Settings")]
    [SerializeField] private string correctCode = "1378";
    [SerializeField] private int codeLength = 4;
    [SerializeField] private bool clearOnSuccess = true;

    private GameManagerChap1 gm;
    private List<int> auxList;
    private readonly List<char> buffer = new List<char>(4);

    private bool checkpointSaved = false;

    private void Awake() {
        gm = FindFirstObjectByType<GameManagerChap1>();

        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        if (gm != null) {
            var field = typeof(GameManagerChap1).GetField("auxPowerStates", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                auxList = (List<int>)field.GetValue(gm);
        }

        UpdateLightByState();
        UpdateDisplay();
    }

    private void UpdateDisplay() {
        if (display == null)
            return;

        string current = new string(buffer.ToArray());
        display.text = current.PadLeft(codeLength, ' ');
    }

    private void UpdateLightByState() {
        if (statusLight == null || auxList == null || auxList.Count <= 1)
            return;

        statusLight.color = (auxList[1] == 0) ? Color.red : Color.green;
    }

    private void PlayOneShotSafe(AudioClip clip, float vol) {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, vol);
    }

    private void PlayButtonClick() {
        PlayOneShotSafe(buttonSfx, buttonVol);
    }

    private void InputDigit(int d) {
        if (buffer.Count >= codeLength)
            return;

        buffer.Add((char)('0' + d));
        UpdateDisplay();
    }

    public void OnPress1() { PlayButtonClick(); InputDigit(1); }
    public void OnPress2() { PlayButtonClick(); InputDigit(2); }
    public void OnPress3() { PlayButtonClick(); InputDigit(3); }
    public void OnPress4() { PlayButtonClick(); InputDigit(4); }
    public void OnPress5() { PlayButtonClick(); InputDigit(5); }
    public void OnPress6() { PlayButtonClick(); InputDigit(6); }
    public void OnPress7() { PlayButtonClick(); InputDigit(7); }
    public void OnPress8() { PlayButtonClick(); InputDigit(8); }
    public void OnPress9() { PlayButtonClick(); InputDigit(9); }
    public void OnPress0() { PlayButtonClick(); InputDigit(0); }

    public void Clear() {
        PlayButtonClick();
        buffer.Clear();
        UpdateDisplay();
    }

    public void Enter() {
        PlayButtonClick();

        string typed = new string(buffer.ToArray());
        if (typed == correctCode) {
            if (auxList != null && auxList.Count > 1) {
                if (auxList[1] == 0)
                    auxList[1] = 1;

                UpdateLightByState();
            }

            PlayOneShotSafe(successSfx, successVol);

            if (clearOnSuccess)
                Clear();

            if (!checkpointSaved) {
                var cpMgr = Chap1CheckpointManager.Instance;
                if (cpMgr != null)
                    cpMgr.SaveCheckpointAtCurrentPosition();
                checkpointSaved = true;
            }
        } else {
            PlayOneShotSafe(wrongSfx, wrongVol);
            Clear();
        }
    }
}