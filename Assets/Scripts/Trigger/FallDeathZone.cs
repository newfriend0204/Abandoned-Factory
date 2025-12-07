using UnityEngine;

public class FallDeathZone : MonoBehaviour {
    private void Reset() {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other) {
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        if (Chap1DeathManager.Instance != null) {
            Chap1DeathManager.Instance.TriggerFallDeath();
        }
    }
}