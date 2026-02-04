using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuGameStartController : MonoBehaviour {
    [Header("Chapter Scene Names")]
    public string firstChapterSceneName = "Chap1";
    public string chap2FirstSceneName = "Chap2";

    void Start() {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OnClickStartFromSave() {
        if (!SaveSystem.LoadFromDisk() || SaveSystem.Current == null) {
            Debug.Log("[MainMenu] 저장된 세이브가 없어 처음부터 시작합니다.");
            OnClickStartFromBeginning();
            return;
        }

        int chapter = SaveSystem.Current.currentChapter;
        if (chapter <= 0)
            chapter = 1;

        if (chapter == 1) {
            bool loaded = Chap1CheckpointManager.LoadSharedDataFromFile();
            if (!loaded) {
                Debug.LogWarning("[MainMenu] Chap1 세이브 데이터를 찾지 못해 처음부터 시작합니다.");
                OnClickStartFromBeginning();
                return;
            }

            Chap1CheckpointManager.SetAutoLoadOnSceneStart(true);
            string sceneName = Chap1CheckpointManager.GetSavedSceneNameOrDefault(firstChapterSceneName);
            Debug.Log("[MainMenu] Chap1 이어하기: " + sceneName);

            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        } else if (chapter == 2) {
            bool loaded = Chap2CheckpointManager.LoadSharedDataFromFile();
            if (!loaded) {
                Debug.LogWarning("[MainMenu] Chap2 세이브 데이터를 찾지 못해 처음부터 시작합니다.");
                OnClickStartFromBeginning();
                return;
            }

            Chap2CheckpointManager.SetAutoLoadOnSceneStart(true);
            string sceneName = Chap2CheckpointManager.GetSavedSceneNameOrDefault(chap2FirstSceneName);
            Debug.Log("[MainMenu] Chap2 이어하기: " + sceneName);

            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        } else {
            Debug.LogWarning("[MainMenu] 알 수 없는 챕터 번호(" + chapter + ")라 처음부터 시작합니다.");
            OnClickStartFromBeginning();
        }
    }

    public void OnClickStartIgnoringSave() {
        Debug.Log("[MainMenu] 세이브 무시하고 Chap1부터 시작합니다.");

        Chap1CheckpointManager.ClearSharedDataInMemory();
        Chap2CheckpointManager.ClearSharedDataInMemory();
        Chap1CheckpointManager.SetAutoLoadOnSceneStart(false);
        Chap2CheckpointManager.SetAutoLoadOnSceneStart(false);

        SaveSystem.ResetToNewGame();

        Time.timeScale = 1f;
        SceneManager.LoadScene(firstChapterSceneName);
    }

    public void OnClickStartFromBeginning() {
        Debug.Log("[MainMenu] 완전히 처음부터 시작합니다.");

        Chap1CheckpointManager.ClearSharedDataInMemory();
        Chap2CheckpointManager.ClearSharedDataInMemory();
        Chap1CheckpointManager.SetAutoLoadOnSceneStart(false);
        Chap2CheckpointManager.SetAutoLoadOnSceneStart(false);

        SaveSystem.DeleteFile();
        SaveSystem.ResetToNewGame();

        Time.timeScale = 1f;
        SceneManager.LoadScene(firstChapterSceneName);
    }

    public void OnClickQuitGame() {
        Debug.Log("[MainMenu] 게임 종료");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}