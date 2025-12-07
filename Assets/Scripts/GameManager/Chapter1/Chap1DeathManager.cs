using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Chap1DeathManager : MonoBehaviour {
    public static Chap1DeathManager Instance { get; private set; }

    [Header("Fade Overlay")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("Death UI")]
    [SerializeField] private GameObject deathUIRoot;
    [SerializeField] private Chap1DeathUIController deathUIController;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip fallCrackClip;
    public AudioClip deathEnterClip;

    [Header("Timings")]
    public float delayBeforeDeathUI = 0.3f;

    private bool isDead = false;
    public bool IsDead => isDead;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (deathUIRoot != null)
            deathUIRoot.SetActive(false);
    }

    private void OnDestroy() {
        if (Instance == this)
            Instance = null;
    }

    public void TriggerFallDeath() {
        if (isDead)
            return;

        StartCoroutine(CoFallDeath());
    }

    public void OnClickRetry() {
        if (!isDead)
            return;

        if (deathUIRoot != null)
            deathUIRoot.SetActive(false);

        var cp = Chap1CheckpointManager.Instance;
        if (cp != null && cp.HasCheckpoint) {
            cp.LoadLastCheckpoint();
        } else {
            Scene current = SceneManager.GetActiveScene();
            Time.timeScale = 1f;
            SceneManager.LoadScene(current.name);
        }
    }

    public void OnClickQuitToMain() {
        if (!isDead)
            return;

        if (deathUIRoot != null)
            deathUIRoot.SetActive(false);

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainScreen");
    }

    public void PlayDeathEnterSfx() {
        if (sfxSource != null && deathEnterClip != null) {
            sfxSource.PlayOneShot(deathEnterClip);
        }
    }

    private IEnumerator CoFallDeath() {
        isDead = true;

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureFadeCanvasGroup();

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 1f;

        if (sfxSource != null && fallCrackClip != null)
            sfxSource.PlayOneShot(fallCrackClip);

        if (delayBeforeDeathUI > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeDeathUI);

        EnsureDeathUIReferences();

        if (deathUIRoot != null) {
            deathUIRoot.SetActive(true);
        }
    }

    private void EnsureFadeCanvasGroup() {
        if (fadeCanvasGroup != null)
            return;

        if (Chap1CheckpointManager.Instance != null && Chap1CheckpointManager.Instance.fadeCanvasGroup != null) {
            fadeCanvasGroup = Chap1CheckpointManager.Instance.fadeCanvasGroup;
            return;
        }

        var go = GameObject.Find("FadeOverlay");
        if (go != null) {
            fadeCanvasGroup = go.GetComponent<CanvasGroup>();
        }
    }

    private void EnsureDeathUIReferences() {
        if (deathUIRoot != null && deathUIController != null)
            return;

        var root = GameObject.Find("Death");
        if (root != null) {
            deathUIRoot = root;
            deathUIController = root.GetComponent<Chap1DeathUIController>();
        }
    }
}