using UnityEditor;
using UnityEditor.SceneManagement;

// 배치 모드에서 인트로 씬을 Play로 실행한다(PlayModeShotCapture가 캡처 후 종료).
// 실행: Unity.exe -batchmode -projectPath . -executeMethod PlayCapture.Enter -playShots production/qa/evidence/hdrp-play
public static class PlayCapture
{
    public static void Enter()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-playScene");
        string sceneName = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Scene_Intro";
        EditorSceneManager.OpenScene($"Assets/Scenes/{sceneName}.unity", OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
}
