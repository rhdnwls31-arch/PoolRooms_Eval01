using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Story 007 전환의 목적지로 쓰는 최소 자리표시 씬. Story 008이 실제 레벨 디자인/분위기를
// 만들 때까지의 더미 — 바닥 + 벽 4장, 최소 조명 하나, Player 1명, 페이드 인 전용
// FadeCanvas뿐이다. 더 꾸미지 않는다([[depth5m-placeholder-art-pipeline]] 참고: 플레이스홀더
// 더미는 전용 부모(Environment) 아래, 손으로 다듬지 않는다).
//
// 이 프로젝트에는 Player 프리팹이 없다(Assets/Prefabs/는 마네킹과 소품 두 개뿐) — Player와
// FadeCanvas는 Scene_Game에서 복제해 온다.
//
// 실행: 메뉴 [DEPTH 5m > Build Second Dream Scene]
//   또는 Unity.exe -batchmode -quit -projectPath . -executeMethod SecondDreamSceneBuilder.BuildBatch
public static class SecondDreamSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Scene_SecondDream.unity";
    const string GameScenePath = "Assets/Scenes/Scene_Game.unity";
    const string PostFxProfilePath = "Assets/Settings/Horror_PostFX.asset";

    // ---- 치수 ----------------------------------------------------------
    const float RoomHalfX = 6f;
    const float RoomHalfZ = 6f;
    const float WallHeight = 4f;
    const float WallThick = 0.3f;
    const float FloorThick = 0.3f;
    const float FadeInTime = 2f; // "눈을 뜨는" 연출 — Story 003의 firstFadeTime(2초)과 동일

    static Material MatFloor, MatWall;

    [MenuItem("DEPTH 5m/Build Second Dream Scene")]
    public static void Build()
    {
        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LoadMaterials();

        var env = Root(scene, "Environment");
        ClearChildren(env, keep: null);

        BuildRoom(env);
        BuildLight(scene);
        BuildVolume(scene);
        ImportPlayerAndFader(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        RegisterInBuildSettings();

        Debug.Log("[SecondDreamSceneBuilder] Scene_SecondDream 생성/갱신 완료.");
    }

    public static void BuildBatch()
    {
        try { Build(); }
        catch (System.Exception e) { Debug.LogError("[SecondDreamSceneBuilder] 실패: " + e); EditorApplication.Exit(1); }
    }

    // ---- 머터리얼 (복도용 기존 머터리얼 재사용 — 실내 느낌의 일관성) ------
    static void LoadMaterials()
    {
        MatFloor = Load("Assets/Materials/Env/Env_Floor.mat");
        MatWall = Load("Assets/Materials/Env/Env_Wall.mat");
    }

    static Material Load(string p)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) Debug.LogWarning("[SecondDreamSceneBuilder] 머터리얼 없음: " + p);
        return m;
    }

    // ---- 구조: 바닥 + 벽 4장뿐 (플레이스홀더, Story 008이 교체) ------------
    static void BuildRoom(Transform env)
    {
        Box(env, "Floor", new Vector3(0, -FloorThick / 2f, 0),
            new Vector3(RoomHalfX * 2f, FloorThick, RoomHalfZ * 2f), MatFloor, true);

        float h = WallHeight / 2f;
        Box(env, "Wall_North", new Vector3(0, h, -RoomHalfZ), new Vector3(RoomHalfX * 2f, WallHeight, WallThick), MatWall, true);
        Box(env, "Wall_South", new Vector3(0, h, RoomHalfZ), new Vector3(RoomHalfX * 2f, WallHeight, WallThick), MatWall, true);
        Box(env, "Wall_West", new Vector3(-RoomHalfX, h, 0), new Vector3(WallThick, WallHeight, RoomHalfZ * 2f), MatWall, true);
        Box(env, "Wall_East", new Vector3(RoomHalfX, h, 0), new Vector3(WallThick, WallHeight, RoomHalfZ * 2f), MatWall, true);
    }

    // 최소한의 조명 하나. Scene_Game의 Directional Light는 세기 0으로 꺼져 있다(복도는
    // 점 조명으로 밝힌다) — 그대로 복제하면 방이 새까맣다. 자리표시 방이 최소한 보이도록
    // 새로 추가한다. 분위기는 Story 008의 몫 — 보이는 데 필요한 만큼만 둔다.
    static void BuildLight(Scene scene)
    {
        var go = RootObject(scene, "PlaceholderLight");
        var light = go.GetComponent<Light>();
        if (light == null) light = go.AddComponent<Light>();
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.color = new Color(1f, 0.95f, 0.84f);
    }

    // Horror_PostFX 프로파일을 쓰는 Volume — 다른 씬과의 분위기 일관성을 위해서만 둔다.
    // 프로파일 자산 자체는 건드리지 않는다(sharedProfile 참조만 건다).
    static void BuildVolume(Scene scene)
    {
        var go = RootObject(scene, "PostFX Volume");
        var volume = go.GetComponent<Volume>();
        if (volume == null) volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxProfilePath);
        if (volume.sharedProfile == null) Debug.LogWarning("[SecondDreamSceneBuilder] Horror_PostFX 프로파일을 찾지 못했다");
    }

    // Player와 FadeCanvas를 Scene_Game에서 복제해 온다(이 프로젝트엔 Player 프리팹이 없다).
    static void ImportPlayerAndFader(Scene scene)
    {
        var gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        var srcPlayer = Find(gameScene, "Player");
        var srcFader = Find(gameScene, "FadeCanvas");

        if (srcPlayer != null)
        {
            var old = Find(scene, "Player");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var player = (GameObject)Object.Instantiate(srcPlayer.gameObject);
            player.name = "Player";
            SceneManager.MoveGameObjectToScene(player, scene);
            player.transform.position = new Vector3(0, 0, -(RoomHalfZ - 2f)); // 시작 지점 — 방 중앙을 바라보고 섬
            player.transform.rotation = Quaternion.identity;
        }
        else Debug.LogWarning("[SecondDreamSceneBuilder] Scene_Game에서 Player를 찾지 못했다");

        if (srcFader != null)
        {
            var old = Find(scene, "FadeCanvas");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var fadeCanvas = (GameObject)Object.Instantiate(srcFader.gameObject);
            fadeCanvas.name = "FadeCanvas";
            SceneManager.MoveGameObjectToScene(fadeCanvas, scene);

            var fader = fadeCanvas.GetComponent<ScreenFader>();
            fader.fadeTime = FadeInTime; // "눈을 뜨는" 연출 — 기본 페이드보다 길게 (Story 003과 동일한 값)

            var introFadeIn = fadeCanvas.GetComponent<IntroFadeIn>();
            if (introFadeIn == null) introFadeIn = fadeCanvas.AddComponent<IntroFadeIn>();
            introFadeIn.fader = fader;
        }
        else Debug.LogWarning("[SecondDreamSceneBuilder] Scene_Game에서 FadeCanvas를 찾지 못했다");

        EditorSceneManager.CloseScene(gameScene, true); // 저장하지 않고 닫는다
    }

    static void RegisterInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
            if (s.path == ScenePath) return; // 이미 등록됨

        var list = new List<EditorBuildSettingsScene>(scenes);
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---- 헬퍼 (IntroMapBuilder와 동일한 패턴) ------------------------------
    static Transform Root(Scene scene, string name)
    {
        var t = Find(scene, name);
        if (t != null) return t;
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.transform;
    }

    static GameObject RootObject(Scene scene, string name)
    {
        var t = Find(scene, name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    static Transform Find(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go.transform;
        return null;
    }

    static void ClearChildren(Transform t, string keep)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i);
            if (keep != null && c.name == keep) continue;
            Object.DestroyImmediate(c.gameObject);
        }
    }

    static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, Material mat, bool collide)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
}
