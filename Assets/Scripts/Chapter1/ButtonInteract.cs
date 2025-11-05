using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ButtonInteract : MonoBehaviour {
    [Header("Refs")]
    public Outline outline;
    public Light targetLight;
    public GameManagerChap1 gameManager;
    public Transform player;
    public AudioClip pressSfx;
    public float pressVolume = 1f;
    private AudioSource audioSource;

    [Header("Settings")]
    public float interactDistance = 2.0f;
    public Color idleColor = Color.red;
    public Color activeColor = Color.green;

    [Header("Light Control")]
    public bool lightOffBeforeHunt = true;

    private bool isActivated;
    private bool HuntActive => gameManager.State == GameManagerChap1.ChapState.Hunting;

    private void Awake() {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManagerChap1>();
        if (player == null) {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                player = pc.transform;
        }

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    private void Start() {
        outline.enabled = false;

        if (lightOffBeforeHunt && gameManager.State != GameManagerChap1.ChapState.Hunting) {
            targetLight.enabled = false;
        }
    }

    private void Update() {
        float dist = Vector3.Distance(player.position, transform.position);
        bool within = dist <= interactDistance;

        outline.enabled = within && !isActivated && HuntActive;

        bool fPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            fPressed = true;
#endif
        if (Input.GetKeyDown(KeyCode.F))
            fPressed = true;

        if (!HuntActive)
            return;

        if (!isActivated && within && fPressed) {
            targetLight.color = activeColor;
            isActivated = true;
            outline.enabled = false;
            gameManager.ReportPressed(this);
            audioSource.PlayOneShot(pressSfx, pressVolume);
        }
    }

    public void PrepareForHunt() {
        isActivated = false;
        outline.enabled = false;
        targetLight.enabled = true;
        targetLight.color = idleColor;
    }
}
