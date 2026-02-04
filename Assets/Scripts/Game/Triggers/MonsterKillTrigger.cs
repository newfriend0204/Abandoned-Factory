using UnityEngine;

public class MonsterKillTrigger : MonoBehaviour {
    [SerializeField] private bool killOnTouch = true;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other) {
        if (!killOnTouch)
            return;
        if (triggered)
            return;

        if (IsDeathPrevented())
            return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        triggered = true;
        PrepareMonsterForDeathCinematic();

        if (DeathManager.Instance != null)
            DeathManager.Instance.TriggerMonsterDeath();
    }

    private void OnCollisionEnter(Collision collision) {
        if (!killOnTouch)
            return;
        if (triggered)
            return;

        if (IsDeathPrevented())
            return;

        var player = collision.collider.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        triggered = true;
        PrepareMonsterForDeathCinematic();

        if (DeathManager.Instance != null)
            DeathManager.Instance.TriggerMonsterDeath();
    }

    private bool IsDeathPrevented() {
        var gm = FindFirstObjectByType<GameManagerChap2>();
        if (gm != null && gm.PreventPlayerDeath)
            return true;

        return false;
    }

    private void PrepareMonsterForDeathCinematic() {
        var monster = FindFirstObjectByType<Chap2MonsterController>();
        if (monster == null)
            return;

        monster.PrepareForDeathCinematic();
    }
}