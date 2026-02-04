using UnityEngine;

public class PersistentPlayerState : MonoBehaviour {
    public static PersistentPlayerState Instance { get; private set; }

    [Header("Player State")]
    public bool hasHeadlamp;
    public float savedSprintStamina = -1f;
    public bool savedIsExhausted = false;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void CaptureFromScene(PlayerController player, HeadlampController headlamp) {
        if (player != null) {
            savedSprintStamina = player.sprintStamina;
            savedIsExhausted = player.isExhausted;
        }

        if (headlamp != null) {
            hasHeadlamp = headlamp.canUseHeadlamp;
        }
    }

    public void ApplyToScene(PlayerController player, HeadlampController headlamp) {
        if (player != null) {
            if (savedSprintStamina >= 0f) {
                player.sprintStamina = Mathf.Clamp(savedSprintStamina, 0f, player.sprintStaminaMax);
            }
            player.isExhausted = savedIsExhausted;
        }

        if (headlamp != null) {
            headlamp.canUseHeadlamp = hasHeadlamp;
        }
    }

    public void ResetForNewGame(float defaultStamina) {
        hasHeadlamp = false;
        savedIsExhausted = false;
        savedSprintStamina = defaultStamina;
    }
}