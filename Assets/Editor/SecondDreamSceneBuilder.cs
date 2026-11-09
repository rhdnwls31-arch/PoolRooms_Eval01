using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Story 008 — 두 번째 꿈 공간: 말라붙은 실내 수영장 홀.
//
// 구조: 첫 번째 복도와 같은 재질/치수의 작은 Entry(진입 전실) 하나를 지나면, 남쪽 문을 통해
// 폭은 같지만 천장이 두 배 높고 훨씬 긴 Pool Hall로 이어진다. 홀 중앙에는 물 없이 마른
// 수영장 바닥(Basin)이 깔려 있고, 기둥이 일정 간격으로 똑같이 반복되며, 안개가 끝을
// 가린다. 모든 "섬뜩함" 요소는 Acceptance Criteria의 7가지 예시 중 최소 2개 이상이 아니라
// 거의 전부를 가볍게 깔아 둔다(끝이 안 보이는 수영장 / 너무 넓고 빈 느낌 / 따뜻한 빛·차가운
// 물빛 대비 / 물 대신 바닥 / 기둥의 비정상적 반복 / 멀리 선 마네킹 한 명 / 보이지 않는
// 라디오 소리).
//
// Story 009(관찰 후 종료)·Story 010(노이즈 디테일)이 이 씬을 계속 다듬으므로 이 빌더는
// 남겨 둔다 — Environment/Dummies 하위를 매번 지우고 다시 짓는 멱등 재생성 스크립트로
// 유지한다.
//
// 이 프로젝트에는 Player 프리팹이 없다(Assets/Prefabs/는 마네킹과 소품뿐) — Player와
// FadeCanvas는 Scene_Game에서 복제해 온다.
//
// 실행: 메뉴 [DEPTH 5m > Build Second Dream Scene]
//   또는 Unity.exe -batchmode -quit -projectPath . -executeMethod SecondDreamSceneBuilder.BuildBatch
public static class SecondDreamSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Scene_SecondDream.unity";
    const string GameScenePath = "Assets/Scenes/Scene_Game.unity";
    const string PostFxProfilePath = "Assets/Settings/Horror_PostFX.asset";

    // ---- Entry (첫 번째 복도와 동일한 치수 — 대비의 기준점) -------------------
    const float EntryHalfX = 6f;
    const float EntryHalfZ = 6f;
    const float EntryWallHeight = 4f;
    const float WallThick = 0.3f;
    const float FloorThick = 0.3f;
    const float FadeInTime = 2f; // "눈을 뜨는" 연출 — Story 003의 firstFadeTime(2초)과 동일

    const float DoorHalfWidth = 1.2f; // Entry 남쪽 벽의 문 틈 (폭 2.4m)

    // ---- Pool Hall ----------------------------------------------------------
    // 폭은 Entry와 똑같다(문 위치에서 벽이 어긋나지 않는다) — 대신 천장이 두 배 높고
    // 훨씬 길다. 폭이 아니라 "높이 + 길이 + 텅 빔"으로 openness를 만든다.
    const float HallHalfX = EntryHalfX;
    const float HallDepth = 40f;       // Entry 남쪽 벽(z=EntryHalfZ)에서 Hall 끝까지
    const float HallWallHeight = 8f;

    // 마른 수영장(Basin) — Hall 중앙, 문에서 몇 걸음 지나 시작해 끝벽 전에 멈춘다.
    const float BasinHalfX = 3f;
    const float BasinZStartOffset = 3f;   // Hall 시작점 기준
    const float BasinZEndOffset = 34f;    // Hall 시작점 기준 (Hall 끝에서 6m 앞)
    const float BasinDepth = 1.4f;        // Story 001 수영장과 동일 — 같은 "수영장" 언어
    const float CurbHeight = 0.36f;       // CharacterController.StepOffset(0.3)보다 높다 =
                                           // 플레이어가 바닥만 있는 수영장에도 "빠지지" 않는다

    // 기둥 — 완전히 동일한 간격·형태로 반복해 "공간 일부의 비정상적 반복"을 만든다.
    const float PillarRadius = 0.4f;
    static readonly float[] PillarZOffsets = { 8f, 14f, 20f, 26f, 32f };

    // 안개 — 홀 끝벽과 마네킹을 "끝이 보이지 않는" 거리까지 밀어낸다.
    const float FogStartDistance = 20f;
    const float FogEndDistance = 38f;

    static Material MatFloor, MatWall, MatCourtWall, MatCourtDeck, MatPoolTile, MatPillar;

    [MenuItem("DEPTH 5m/Build Second Dream Scene")]
    public static void Build()
    {
        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LoadMaterials();

        var env = Root(scene, "Environment");
        ClearChildren(env, keep: null);

        BuildEntryFoyer(env);
        BuildPoolHall(env);
        BuildPillars(env);
        BuildMannequin(env);
        BuildHiddenRadio(env);
        BuildLight(scene);
        BuildColdLights(env);
        BuildFog();
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

    // ---- 머터리얼 -------------------------------------------------------------
    // Entry는 첫 번째 복도용 머터리얼(Env_Floor/Env_Wall)을 그대로 쓴다 — "같은 건물"이라는
    // 신호. Hall은 Story 001(야외 수영장)에서 쓰던 Env_Court_* 세트를 재사용한다 — 새 머터리얼을
    // 만들지 않고도 톤이 확실히 달라져서 "다른 공간"으로 읽힌다.
    static void LoadMaterials()
    {
        MatFloor = Load("Assets/Materials/Env/Env_Floor.mat");
        MatWall = Load("Assets/Materials/Env/Env_Wall.mat");
        MatCourtWall = Load("Assets/Materials/Env/Env_Court_Wall.mat");
        MatCourtDeck = Load("Assets/Materials/Env/Env_Court_Deck.mat");
        MatPoolTile = Load("Assets/Materials/Env/Env_Court_PoolTile.mat");
        MatPillar = Load("Assets/Materials/Env/Env_Pillar.mat");
    }

    static Material Load(string p)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) Debug.LogWarning("[SecondDreamSceneBuilder] 머터리얼 없음: " + p);
        return m;
    }

    // ---- Entry: 첫 번째 복도와 같은 작은 방, 남쪽 벽에 문 틈만 뚫려 있다 --------
    static void BuildEntryFoyer(Transform env)
    {
        Box(env, "Entry_Floor", new Vector3(0, -FloorThick / 2f, 0),
            new Vector3(EntryHalfX * 2f, FloorThick, EntryHalfZ * 2f), MatFloor, true);

        float h = EntryWallHeight / 2f;
        Box(env, "Entry_Wall_North", new Vector3(0, h, -EntryHalfZ), new Vector3(EntryHalfX * 2f, EntryWallHeight, WallThick), MatWall, true);
        Box(env, "Entry_Wall_West", new Vector3(-EntryHalfX, h, 0), new Vector3(WallThick, EntryWallHeight, EntryHalfZ * 2f), MatWall, true);
        Box(env, "Entry_Wall_East", new Vector3(EntryHalfX, h, 0), new Vector3(WallThick, EntryWallHeight, EntryHalfZ * 2f), MatWall, true);

        // 남쪽 벽 — 가운데 문 틈(폭 2.4m)만 빼고 두 조각으로 나눠 막는다.
        float segLen = EntryHalfX - DoorHalfWidth;
        float segCenterX = (EntryHalfX + DoorHalfWidth) / 2f;
        Box(env, "Entry_Wall_South_West", new Vector3(-segCenterX, h, EntryHalfZ), new Vector3(segLen, EntryWallHeight, WallThick), MatWall, true);
        Box(env, "Entry_Wall_South_East", new Vector3(segCenterX, h, EntryHalfZ), new Vector3(segLen, EntryWallHeight, WallThick), MatWall, true);
    }

    // ---- Pool Hall: 넓고 높은 홀 + 물 없는 수영장 바닥(Basin) ------------------
    static void BuildPoolHall(Transform env)
    {
        float hallStartZ = EntryHalfZ;
        float hallEndZ = hallStartZ + HallDepth;
        float basinStartZ = hallStartZ + BasinZStartOffset;
        float basinEndZ = hallStartZ + BasinZEndOffset;
        float hallCenterZ = (hallStartZ + hallEndZ) / 2f;
        float h = HallWallHeight / 2f;

        // 측벽 — Entry보다 두 배 높고, 다른 머터리얼(Env_Court_Wall)이라 한눈에 "다른 공간"이다.
        Box(env, "Hall_Wall_West", new Vector3(-HallHalfX, h, hallCenterZ), new Vector3(WallThick, HallWallHeight, HallDepth), MatCourtWall, true);
        Box(env, "Hall_Wall_East", new Vector3(HallHalfX, h, hallCenterZ), new Vector3(WallThick, HallWallHeight, HallDepth), MatCourtWall, true);
        // 끝벽 — 물리적으로는 있지만 안개 속이라 평소엔 보이지 않는다("끝이 보이지 않는 수영장").
        Box(env, "Hall_Wall_Far", new Vector3(0, h, hallEndZ), new Vector3(HallHalfX * 2f, HallWallHeight, WallThick), MatCourtWall, true);

        // 데크 — Basin을 감싸는 ㄷ자 바닥. 폭 방향 두 줄 + Basin 앞/뒤를 덮는 두 캡.
        float deckStripHalfX = (HallHalfX - BasinHalfX) / 2f;
        float deckStripCenterX = BasinHalfX + deckStripHalfX;
        Box(env, "Hall_Deck_West", new Vector3(-deckStripCenterX, -FloorThick / 2f, hallCenterZ),
            new Vector3(deckStripHalfX * 2f, FloorThick, HallDepth), MatCourtDeck, true);
        Box(env, "Hall_Deck_East", new Vector3(deckStripCenterX, -FloorThick / 2f, hallCenterZ),
            new Vector3(deckStripHalfX * 2f, FloorThick, HallDepth), MatCourtDeck, true);

        float nearCapLen = basinStartZ - hallStartZ;
        Box(env, "Hall_Deck_NearCap", new Vector3(0, -FloorThick / 2f, hallStartZ + nearCapLen / 2f),
            new Vector3(BasinHalfX * 2f, FloorThick, nearCapLen), MatCourtDeck, true);
        float farCapLen = hallEndZ - basinEndZ;
        Box(env, "Hall_Deck_FarCap", new Vector3(0, -FloorThick / 2f, basinEndZ + farCapLen / 2f),
            new Vector3(BasinHalfX * 2f, FloorThick, farCapLen), MatCourtDeck, true);

        BuildBasin(env, basinStartZ, basinEndZ);
    }

    // 물 대신 바닥만 있는 마른 수영장. Story 001(IntroMapBuilder.BuildPoolBasin)과 같은
    // 짜임 — 타일 안쪽 벽 + 바닥 — 이지만 Water 쿼드/WaterScroll/물소리를 전부 생략한다.
    // "물이 있어야 할 자리에 바닥"은 이 생략 자체가 연출이다.
    static void BuildBasin(Transform env, float basinStartZ, float basinEndZ)
    {
        float basinLen = basinEndZ - basinStartZ;
        float basinCenterZ = (basinStartZ + basinEndZ) / 2f;
        float y = -BasinDepth / 2f;

        Box(env, "Basin_Floor", new Vector3(0, -BasinDepth - FloorThick / 2f, basinCenterZ),
            new Vector3(BasinHalfX * 2f + 0.8f, FloorThick, basinLen + 0.8f), MatPoolTile, true);
        Box(env, "Basin_Wall_West", new Vector3(-BasinHalfX, y, basinCenterZ), new Vector3(0.4f, BasinDepth, basinLen), MatPoolTile, true);
        Box(env, "Basin_Wall_East", new Vector3(BasinHalfX, y, basinCenterZ), new Vector3(0.4f, BasinDepth, basinLen), MatPoolTile, true);
        Box(env, "Basin_Wall_Near", new Vector3(0, y, basinStartZ), new Vector3(BasinHalfX * 2f, BasinDepth, 0.4f), MatPoolTile, true);
        Box(env, "Basin_Wall_Far", new Vector3(0, y, basinEndZ), new Vector3(BasinHalfX * 2f, BasinDepth, 0.4f), MatPoolTile, true);

        // 테두리 턱 — StepOffset보다 높아 플레이어가 바닥만 있는 Basin에도 "빠지지" 않는다.
        Box(env, "BasinCurb_West", new Vector3(-BasinHalfX - 0.15f, CurbHeight / 2f, basinCenterZ), new Vector3(0.3f, CurbHeight, basinLen), MatCourtDeck, true);
        Box(env, "BasinCurb_East", new Vector3(BasinHalfX + 0.15f, CurbHeight / 2f, basinCenterZ), new Vector3(0.3f, CurbHeight, basinLen), MatCourtDeck, true);
        Box(env, "BasinCurb_Near", new Vector3(0, CurbHeight / 2f, basinStartZ - 0.15f), new Vector3(BasinHalfX * 2f + 0.6f, CurbHeight, 0.3f), MatCourtDeck, true);
        Box(env, "BasinCurb_Far", new Vector3(0, CurbHeight / 2f, basinEndZ + 0.15f), new Vector3(BasinHalfX * 2f + 0.6f, CurbHeight, 0.3f), MatCourtDeck, true);
    }

    // 완전히 동일한 기둥을 데크 안쪽에 일정 간격으로 세운다 — "공간 일부의 비정상적 반복".
    static void BuildPillars(Transform env)
    {
        float hallStartZ = EntryHalfZ;
        float deckStripHalfX = (HallHalfX - BasinHalfX) / 2f;
        float pillarX = BasinHalfX + deckStripHalfX / 2f;

        for (int i = 0; i < PillarZOffsets.Length; i++)
        {
            float z = hallStartZ + PillarZOffsets[i];
            Cyl(env, "Pillar_W" + (i + 1), new Vector3(-pillarX, HallWallHeight / 2f, z),
                new Vector3(PillarRadius * 2f, HallWallHeight / 2f, PillarRadius * 2f), MatPillar);
            Cyl(env, "Pillar_E" + (i + 1), new Vector3(pillarX, HallWallHeight / 2f, z),
                new Vector3(PillarRadius * 2f, HallWallHeight / 2f, PillarRadius * 2f), MatPillar);
        }
    }

    // 홀 끝, Basin 너머에 가만히 선 마네킹 한 명 — 플레이어를 정면으로 바라본다.
    static void BuildMannequin(Transform env)
    {
        float hallStartZ = EntryHalfZ;
        float z = hallStartZ + BasinZEndOffset + (HallDepth - BasinZEndOffset) / 2f;
        var go = PlacePrefab(env, "Assets/Prefabs/Mannequin_A.prefab", "Mannequin_Distant", new Vector3(0, 0, z), 180f);
        if (go == null) Debug.LogWarning("[SecondDreamSceneBuilder] Mannequin_A 프리팹을 찾지 못했다");
    }

    // 소리만 있고 보이지 않는 라디오 — 동쪽 벽 바로 뒤(플레이어 시야 밖)에 숨긴 AudioSource뿐.
    // Implementation Notes: "기존 오디오 소스를 숨겨진 위치에 배치하는 것만으로 구현 가능" —
    // 새 시스템도, 라디오 메쉬도 필요 없다.
    static void BuildHiddenRadio(Transform env)
    {
        float z = EntryHalfZ + PillarZOffsets[1]; // 기둥 두 번째 줄 높이 — 벽 바로 뒤
        var go = new GameObject("Hidden_Radio_Ambience");
        go.transform.SetParent(env, false);
        go.transform.localPosition = new Vector3(HallHalfX + 0.6f, 1.2f, z); // 동쪽 벽 바깥쪽 = 안 보임

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = true;
        src.loop = true;
        src.volume = 0.25f;
        src.spatialBlend = 1f; // 3D — 방향은 들리되 위치(라디오 자체)는 안 보인다
        src.maxDistance = 22f;
        src.rolloffMode = AudioRolloffMode.Linear;
        // 클립은 아직 없다(프로젝트에 오디오 에셋 자체가 없음) — VARCO Sound 자산이 들어오면 연결.
    }

    // 최소한의 따뜻한 조명 하나. Scene_Game의 Directional Light는 세기 0으로 꺼져 있다(복도는
    // 점 조명으로 밝힌다) — 그대로 복제하면 방이 새까맣다. 전체 공간을 비추는 기준광으로 둔다.
    static void BuildLight(Scene scene)
    {
        var go = RootObject(scene, "SunLight");
        var light = go.GetComponent<Light>();
        if (light == null) light = go.AddComponent<Light>();
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.color = new Color(1f, 0.95f, 0.84f); // 따뜻한 빛
    }

    // Basin 안쪽에 남은 차가운 점 조명들 — 물은 없는데 수중등만 아직 켜져 있는 느낌.
    // 따뜻한 SunLight와 대비시켜 "따뜻한 빛/차가운 물빛 대비"를 만든다.
    static void BuildColdLights(Transform env)
    {
        float hallStartZ = EntryHalfZ;
        // 기둥보다 성긴 간격으로 — 기둥만큼 또렷하게 반복되면 조명 자체가 또 다른 반복 요소로
        // 묻혀 버린다.
        float[] offsets = { PillarZOffsets[0], PillarZOffsets[2], PillarZOffsets[4] };
        for (int i = 0; i < offsets.Length; i++)
        {
            var go = new GameObject("Basin_ColdLight_" + (i + 1));
            go.transform.SetParent(env, false);
            go.transform.localPosition = new Vector3(0, -BasinDepth + 0.25f, hallStartZ + offsets[i]);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.75f, 0.95f); // 차가운 물빛
            light.intensity = 2f;
            light.range = 7f;
        }
    }

    // 안개 — Basin 끝벽과 마네킹을 또렷한 윤곽 없이 시야 끝으로 밀어낸다. 커스텀 셰이더가
    // 아니라 엔진 기본 Fog([[depth5m-visual-reference-pool-glow]] 참고 — 셰이더 패스는
    // 보류, 지금은 기본 조명/안개만).
    static void BuildFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = FogStartDistance;
        RenderSettings.fogEndDistance = FogEndDistance;
        RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.08f);
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
            player.transform.position = new Vector3(0, 0, -(EntryHalfZ - 2f)); // 시작 지점 — 문을 바라보고 섬
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

    static GameObject Cyl(Transform p, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject PlacePrefab(Transform p, string path, string name, Vector3 pos, float yaw)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null) { Debug.LogWarning("[SecondDreamSceneBuilder] 프리팹 없음: " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, p);
        go.name = name;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return go;
    }
}
