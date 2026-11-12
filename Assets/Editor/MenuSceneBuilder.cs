using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 메인 메뉴 씬을 만들고, 게임 씬들(Intro/Game/SecondDream)에 새 일시정지 메뉴를 붙인다. 멱등.
//   - Scene_MainMenu: 검은 배경 + MenuController(Main). Build Settings 맨 앞에 등록.
//   - 각 게임 씬: 루트에 "PauseUI"(MenuController Pause). 예전 PauseMenu(ESC/Q) 컴포넌트는 꺼서 ESC가 겹치지 않게 하고
//     예전 PausePanel은 숨긴다(스크립트/오브젝트는 지우지 않는다).
// 실행: 메뉴 [DEPTH 5m > Menu > Build Main Menu + Pause UI]
//   또는 Unity.exe -batchmode -projectPath . -executeMethod MenuSceneBuilder.BuildBatch -quit
public static class MenuSceneBuilder
{
    const string MainMenuScenePath = "Assets/Scenes/Scene_MainMenu.unity";
    static readonly string[] GameScenes =
    {
        "Assets/Scenes/Scene_Intro.unity",
        "Assets/Scenes/Scene_Game.unity",
        "Assets/Scenes/Scene_SecondDream.unity",
    };

    [MenuItem("DEPTH 5m/Menu/Build Main Menu + Pause UI")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildMainMenuScene();
        foreach (var p in GameScenes) AddPauseUI(p);
        RegisterMainMenuFirst();
        AssetDatabase.SaveAssets();
        Debug.Log("[MenuSceneBuilder] 메인 메뉴 + 일시정지 UI 적용 완료.");
    }

    public static void BuildBatch()
    {
        try { BuildAll(); }
        catch (System.Exception e) { Debug.LogError("[MenuSceneBuilder] 실패: " + e); EditorApplication.Exit(1); }
    }

    static void BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;      // 배경 사진을 찍기 전까지 검정(2026-11-12)
        cam.cullingMask = 0;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>();

        var menu = new GameObject("MainMenu");
        var mc = menu.AddComponent<MenuController>();
        mc.mode = MenuMode.Main;
        mc.firstSceneName = "Scene_Intro";

        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
    }

    static void AddPauseUI(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 예전 일시정지(PauseMenu.cs): 같은 ESC를 먹으므로 컴포넌트를 끄고 패널을 숨긴다.
        foreach (var root in scene.GetRootGameObjects())
            foreach (var old in root.GetComponentsInChildren<PauseMenu>(true))
            {
                old.enabled = false;
                if (old.pausePanel != null) old.pausePanel.SetActive(false);
                EditorUtility.SetDirty(old);
            }

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "PauseUI") { Object.DestroyImmediate(root); break; }

        var go = new GameObject("PauseUI");
        var mc = go.AddComponent<MenuController>();
        mc.mode = MenuMode.Pause;
        SceneManager.MoveGameObjectToScene(go, scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MenuSceneBuilder] PauseUI 추가: " + scenePath);
    }

    static void RegisterMainMenuFirst()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == MainMenuScenePath);
        list.Insert(0, new EditorBuildSettingsScene(MainMenuScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
