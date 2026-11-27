using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 에디터에서 Play를 누르면 지금 열린 씬이 아니라 메인 메뉴부터 시작한다(2026-11-27 사용자 요청: 빌드와 같은 흐름).
/// 메뉴 "DEPTH 5m/Play From Main Menu"로 켜고 끈다(기본 켬). 끄면 열린 씬에서 바로 시작한다(방 하나만 볼 때).
/// </summary>
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    const string MenuPath = "DEPTH 5m/Play From Main Menu";
    const string PrefKey = "DEPTH5m.PlayFromMainMenu";
    const string MainMenuScene = "Assets/Scenes/Scene_MainMenu.unity";

    /// <summary>메인 메뉴부터 시작하는지.</summary>
    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set { EditorPrefs.SetBool(PrefKey, value); Apply(); }
    }

    static PlayFromMainMenu()
    {
        EditorApplication.update += ApplyWhenReady; // 컴파일 직후엔 씬 파일을 못 불러올 때가 있어, 불러와질 때까지 매 틱 시도
        // 에디터가 막 켜질 때는 씬 파일을 아직 못 불러올 수 있어, Play 누르기 직전에도 다시 맞춘다
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Apply(); };
    }

    static void ApplyWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Apply();
        if (!Enabled || EditorSceneManager.playModeStartScene != null) EditorApplication.update -= ApplyWhenReady;
    }

    static void Apply()
    {
        EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScene) : null;
        Menu.SetChecked(MenuPath, Enabled);
    }

    [MenuItem(MenuPath)]
    static void Toggle() => Enabled = !Enabled;

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }
}
