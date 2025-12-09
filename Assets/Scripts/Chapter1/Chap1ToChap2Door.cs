using UnityEngine;
using UnityEngine.SceneManagement;

public class Chap1ToChap2Door : MonoBehaviour {
    public string nextSceneName = "Chap2";

    private bool loading = false;

    private void OnTriggerEnter(Collider other) {
        if (loading)
            return;

        var player = other.GetComponent<PlayerController>();
        if (player != null) {
            loading = true;
            GoToNextScene(player);
        }
    }

    private void GoToNextScene(PlayerController player) {
        var state = PersistentPlayerState.Instance;
        if (state != null) {
            var headlamp = FindFirstObjectByType<HeadlampController>();
            state.CaptureFromScene(player, headlamp);
        }

        // 필요하면 여기서 페이드아웃 연출, 사운드 등 넣고
        SceneManager.LoadScene(nextSceneName);
    }
}