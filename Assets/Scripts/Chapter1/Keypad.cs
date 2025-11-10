using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class Keypad : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private TextMeshProUGUI display;
    [SerializeField] private Light statusLight;
    [SerializeField] private AudioClip successSfx;
    [SerializeField] private AudioClip wrongSfx;
    [SerializeField] private AudioClip buttonSfx;
    public float successVol = 1f;
    public float wrongVol = 1f;
    public float buttonVol = 1f;

    [Header("Settings")]
    [SerializeField] private string correctCode = "1378";
    [SerializeField] private int codeLength = 4;
    [SerializeField] private bool clearOnSuccess = true;

    private GameManagerChap1 gm;
    private AudioSource audioSource;
    private List<int> auxList;
    private readonly List<char> buffer = new List<char>(4);

    private void Awake() {
        gm = FindFirstObjectByType<GameManagerChap1>();
        audioSource = GetComponent<AudioSource>();

        var field = typeof(GameManagerChap1).GetField("auxPowerStates", BindingFlags.Instance | BindingFlags.NonPublic);
        auxList = (List<int>)field.GetValue(gm);

        UpdateLightByState();
        UpdateDisplay();
    }

    private void UpdateDisplay() {
        string current = new string(buffer.ToArray());
        display.text = current.PadLeft(codeLength, ' ');
    }

    private void UpdateLightByState() {
        statusLight.color = (auxList[1] == 0) ? Color.red : Color.green;
    }

    private void PlayButtonClick() {
        audioSource.PlayOneShot(buttonSfx, buttonVol);
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
            if (auxList[1] == 0)
                auxList[1] = 1;
            UpdateLightByState();
            if (successSfx)
                audioSource.PlayOneShot(successSfx, successVol);
            if (clearOnSuccess)
                Clear();
        } else {
            if (wrongSfx)
                audioSource.PlayOneShot(wrongSfx, wrongVol);
            Clear();
        }
    }
}
