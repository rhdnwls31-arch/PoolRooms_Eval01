using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 개발용 메뉴 "DEPTH 5m/방에서 시작": 그레이박스 맵을 고른 방에서 바로 Play한다. Play 중이면 그 방으로 옮긴다.
/// 실제 옮기기는 RoomStartDebug(Assets/Scripts/Dev)가 한다. Play 중에는 F1~F7로도 옮길 수 있다.
/// </summary>
static class RoomStartMenu
{
    const string Root = "DEPTH 5m/방에서 시작/";
    const string ScenePath = "Assets/Scenes/Greybox/Scene_DescentGreybox.unity";

    [MenuItem(Root + "1 인트로 메인 (F1)", false, 0)] static void R1() => Start(0);
    [MenuItem(Root + "2 수영장 홀 (F2)", false, 1)] static void R2() => Start(1);
    [MenuItem(Root + "3 금 간 복도 (F3)", false, 2)] static void R3() => Start(2);
    [MenuItem(Root + "4 노란 방 (F4)", false, 3)] static void R4() => Start(3);
    [MenuItem(Root + "5 천사 방 (F5)", false, 4)] static void R5() => Start(4);
    [MenuItem(Root + "6 하늘 계단 (F6)", false, 5)] static void R6() => Start(5);
    [MenuItem(Root + "7 마지막 수영장 (F7)", false, 6)] static void R7() => Start(6);

    /// <summary>Play 중이면 바로 옮기고, 아니면 그레이박스 씬을 열어 고른 방에서 Play를 시작한다.</summary>
    static void Start(int room)
    {
        if (EditorApplication.isPlaying)
        {
            RoomStartDebug.GoToNow(room);
            return;
        }
        if (SceneManager.GetActiveScene().name != RoomStartDebug.SceneName)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        SessionState.SetInt(RoomStartDebug.PendingKey, room);
        EditorApplication.EnterPlaymode();
    }
}
