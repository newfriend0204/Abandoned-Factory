using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuGameStartController : MonoBehaviour {
    [Header("Chapter Scene Names")]
    public string firstChapterSceneName = "Chap1";   // Chap1 시작 씬 이름
    public string chap2FirstSceneName = "Chap2";     // Chap2 시작 씬 이름 (나중에 씬 이름에 맞게 수정)

    void Start() {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 1) 이어하기
    public void OnClickStartFromSave() {
        // 세이브 파일을 먼저 읽어옴
        if (!SaveSystem.LoadFromDisk() || SaveSystem.Current == null) {
            Debug.Log("[MainMenu] 저장된 세이브가 없어 처음부터 시작합니다.");
            OnClickStartFromBeginning();
            return;
        }

        int chapter = SaveSystem.Current.currentChapter;
        if (chapter <= 0)
            chapter = 1; // 옛날 세이브 호환용

        if (chapter == 1) {
            // Chap1 이어하기
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
            // Chap2 이어하기
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

    // 2) 세이브 무시 (파일은 남겨두고, 새 게임처럼 Chap1부터 시작)
    public void OnClickStartIgnoringSave() {
        Debug.Log("[MainMenu] 세이브 무시하고 Chap1부터 시작합니다.");

        Chap1CheckpointManager.ClearSharedDataInMemory();
        Chap2CheckpointManager.ClearSharedDataInMemory();
        Chap1CheckpointManager.SetAutoLoadOnSceneStart(false);
        Chap2CheckpointManager.SetAutoLoadOnSceneStart(false);

        // 메모리상의 세이브 상태 초기화 (파일은 유지)
        SaveSystem.ResetToNewGame();

        Time.timeScale = 1f;
        SceneManager.LoadScene(firstChapterSceneName);
    }

    // 3) 완전 처음부터 (파일까지 삭제)
    public void OnClickStartFromBeginning() {
        Debug.Log("[MainMenu] 완전히 처음부터 시작합니다.");

        Chap1CheckpointManager.ClearSharedDataInMemory();
        Chap2CheckpointManager.ClearSharedDataInMemory();
        Chap1CheckpointManager.SetAutoLoadOnSceneStart(false);
        Chap2CheckpointManager.SetAutoLoadOnSceneStart(false);

        SaveSystem.DeleteFile();     // 파일 삭제
        SaveSystem.ResetToNewGame(); // 메모리 상태 리셋

        Time.timeScale = 1f;
        SceneManager.LoadScene(firstChapterSceneName);  // 항상 Chap1 첫 씬으로
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