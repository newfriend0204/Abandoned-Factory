using UnityEngine;

public class ScenePlayerStateApplier : MonoBehaviour {
    public bool applyOnStart = true;

    private void Start() {
        if (!applyOnStart)
            return;

        var state = PersistentPlayerState.Instance;
        if (state == null)
            return;

        var player = FindFirstObjectByType<PlayerController>();
        var headlamp = FindFirstObjectByType<HeadlampController>();

        if (player != null || headlamp != null) {
            state.ApplyToScene(player, headlamp);
        }
    }
}