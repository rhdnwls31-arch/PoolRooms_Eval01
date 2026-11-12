using UnityEditor;
using UnityEditor.SceneManagement;

// 배치 모드에서 인트로 씬을 Play로 실행한다(PlayModeShotCapture가 캡처 후 종료).
// 실행: Unity.exe -batchmode -projectPath . -executeMethod PlayCapture.Enter -playShots production/qa/evidence/hdrp-play
public static class PlayCapture
{
    public static void Enter()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Scene_Intro.unity", OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
}
