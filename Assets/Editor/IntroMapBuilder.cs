using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene_Intro의 야외 수영장 레이아웃을 코드로 생성한다.
//
// 왜 스크립트인가: 오브젝트가 100개를 넘고 "벽을 좀 더 높게", "선베드를 두 개 더" 같은
// 수정이 반복된다. 씬 YAML을 손으로 고치는 것보다 숫자 하나 바꿔 재실행하는 쪽이
// 안전하고 되돌리기 쉽다.
//
// 건드리는 것: Environment / Dummies 하위, 그리고 FadeCanvas의 눈꺼풀 오버레이.
// 건드리지 않는 것: Player, Main Camera, Lighting, CollapseSequence 오브젝트 자체.
//
// 실행: 메뉴 [DEPTH 5m > Rebuild Intro Map]
//   또는 Unity.exe -batchmode -quit -projectPath . -executeMethod IntroMapBuilder.BuildBatch
public static class IntroMapBuilder
{
    const string ScenePath = "Assets/Scenes/Scene_Intro.unity";

    // ---- 치수 (여기만 고치면 된다) ----------------------------------------
    const float DeckHalfX = 15f;    // 데크 반폭
    const float DeckHalfZ = 20f;    // 데크 반길이
    const float PoolHalfX = 4.3f;   // 수영장 개구부 반폭
    const float PoolHalfZ = 7.3f;   // 수영장 개구부 반길이
    const float PoolDepth = 1.5f;   // 수영장 깊이
    const float WallHeight = 9f;    // 중정 벽 높이 — 지평선을 가릴 만큼
    const float WallThick = 0.6f;
    const float CurbHeight = 0.36f; // CharacterController의 StepOffset(0.3)보다 높게 =
                                    // 플레이어가 수영장에 빠지지 않는다
    const float DeckThick = 0.4f;
    const float WaterY = -0.12f;    // 수면은 데크보다 살짝 아래 (실제 수영장처럼)

    static Material MatFloor, MatWall, MatWater, MatGlow, MatProp, MatCushion, MatPlant, MatMetal, MatPoolTile;

    [MenuItem("DEPTH 5m/Rebuild Intro Map")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        LoadMaterials();

        var env = Root(scene, "Environment");
        var dummies = Root(scene, "Dummies");
        ClearChildren(env, keep: "PoolReflectionProbe");
        ClearChildren(dummies, keep: null);

        BuildDeck(env);
        BuildPoolBasin(env);
        BuildWater(env);
        BuildCourtyardWalls(env);
        BuildBackdrop(env);
        BuildProps(dummies);
        BuildEyelids(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[IntroMapBuilder] Scene_Intro 재생성 완료.");
    }

    public static void BuildBatch()
    {
        try { Build(); }
        catch (System.Exception e) { Debug.LogError("[IntroMapBuilder] 실패: " + e); EditorApplication.Exit(1); }
    }

    // ---- 머터리얼 ---------------------------------------------------------
    static void LoadMaterials()
    {
        // Env_Floor / Env_Wall은 Scene_Game의 **실내 복도용 타일**이다. 야외에 그대로 쓰면
        // 중정 전체가 차갑고 실내처럼 읽힌다 — 레퍼런스의 밝은 화이트 톤으로 따로 만든다.
        MatFloor = MakeMat("Assets/Materials/Env/Env_Court_Deck.mat", new Color(0.90f, 0.88f, 0.84f), 0.12f);
        MatWall = MakeMat("Assets/Materials/Env/Env_Court_Wall.mat", new Color(0.96f, 0.95f, 0.93f), 0.06f);
        MatPoolTile = MakeMat("Assets/Materials/Env/Env_Court_PoolTile.mat", new Color(0.86f, 0.93f, 0.94f), 0.55f);
        MatWater = Load("Assets/Materials/Env/Env_Water.mat");
        MatGlow = Load("Assets/Materials/Env/Env_WindowGlow.mat");
        // 야외 소품용 (레퍼런스의 밝은 화이트 톤)
        MatProp = MakeMat("Assets/Materials/Env/Env_Court_Prop.mat", new Color(0.93f, 0.92f, 0.90f), 0.25f);
        MatCushion = MakeMat("Assets/Materials/Env/Env_Court_Cushion.mat", new Color(0.98f, 0.97f, 0.95f), 0.05f);
        MatPlant = MakeMat("Assets/Materials/Env/Env_Court_Plant.mat", new Color(0.30f, 0.52f, 0.28f), 0.15f);
        MatMetal = MakeMat("Assets/Materials/Env/Env_Court_Metal.mat", new Color(0.82f, 0.84f, 0.86f), 0.80f);
    }

    static Material Load(string p)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) Debug.LogWarning("[IntroMapBuilder] 머터리얼 없음: " + p);
        return m;
    }

    static Material MakeMat(string path, Color c, float smoothness)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(sh);
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---- 구조 -------------------------------------------------------------
    // 데크를 통판 하나로 깔면 수영장이 "평면 위의 사각형"이 되어 물 위를 걸어 다니게 된다.
    // 개구부를 비운 4장으로 나눠 깐다.
    static void BuildDeck(Transform env)
    {
        float endLen = DeckHalfZ - PoolHalfZ;
        float sideLen = DeckHalfX - PoolHalfX;
        Box(env, "Deck_North", new Vector3(0, -DeckThick / 2f, -(PoolHalfZ + endLen / 2f)),
            new Vector3(DeckHalfX * 2f, DeckThick, endLen), MatFloor, true);
        Box(env, "Deck_South", new Vector3(0, -DeckThick / 2f, PoolHalfZ + endLen / 2f),
            new Vector3(DeckHalfX * 2f, DeckThick, endLen), MatFloor, true);
        Box(env, "Deck_West", new Vector3(-(PoolHalfX + sideLen / 2f), -DeckThick / 2f, 0),
            new Vector3(sideLen, DeckThick, PoolHalfZ * 2f), MatFloor, true);
        Box(env, "Deck_East", new Vector3(PoolHalfX + sideLen / 2f, -DeckThick / 2f, 0),
            new Vector3(sideLen, DeckThick, PoolHalfZ * 2f), MatFloor, true);

        // 코핑(테두리 턱). StepOffset보다 높아 플레이어가 수영장에 빠지지 않는다.
        Box(env, "PoolCurb_North", new Vector3(0, CurbHeight / 2f, -PoolHalfZ - 0.15f),
            new Vector3(PoolHalfX * 2f + 0.6f, CurbHeight, 0.3f), MatFloor, true);
        Box(env, "PoolCurb_South", new Vector3(0, CurbHeight / 2f, PoolHalfZ + 0.15f),
            new Vector3(PoolHalfX * 2f + 0.6f, CurbHeight, 0.3f), MatFloor, true);
        Box(env, "PoolCurb_West", new Vector3(-PoolHalfX - 0.15f, CurbHeight / 2f, 0),
            new Vector3(0.3f, CurbHeight, PoolHalfZ * 2f), MatFloor, true);
        Box(env, "PoolCurb_East", new Vector3(PoolHalfX + 0.15f, CurbHeight / 2f, 0),
            new Vector3(0.3f, CurbHeight, PoolHalfZ * 2f), MatFloor, true);
    }

    static void BuildPoolBasin(Transform env)
    {
        // 수영장 안쪽은 밝은 타일. 데크와 같은 색을 쓰면 물이 탁하게 보인다.
        float y = -PoolDepth / 2f;
        Box(env, "Pool_Floor", new Vector3(0, -PoolDepth - 0.2f, 0),
            new Vector3(PoolHalfX * 2f + 0.8f, 0.4f, PoolHalfZ * 2f + 0.8f), MatPoolTile, true);
        Box(env, "Pool_Wall_North", new Vector3(0, y, -PoolHalfZ - 0.2f),
            new Vector3(PoolHalfX * 2f + 0.8f, PoolDepth, 0.4f), MatPoolTile, true);
        Box(env, "Pool_Wall_South", new Vector3(0, y, PoolHalfZ + 0.2f),
            new Vector3(PoolHalfX * 2f + 0.8f, PoolDepth, 0.4f), MatPoolTile, true);
        Box(env, "Pool_Wall_West", new Vector3(-PoolHalfX - 0.2f, y, 0),
            new Vector3(0.4f, PoolDepth, PoolHalfZ * 2f), MatPoolTile, true);
        Box(env, "Pool_Wall_East", new Vector3(PoolHalfX + 0.2f, y, 0),
            new Vector3(0.4f, PoolDepth, PoolHalfZ * 2f), MatPoolTile, true);
    }

    static void BuildWater(Transform env)
    {
        var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
        water.name = "Water";
        Object.DestroyImmediate(water.GetComponent<Collider>()); // 수면에는 충돌 없음
        water.transform.SetParent(env, false);
        water.transform.localPosition = new Vector3(0, WaterY, 0);
        water.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        water.transform.localScale = new Vector3(PoolHalfX * 2f, PoolHalfZ * 2f, 1f);
        water.GetComponent<MeshRenderer>().sharedMaterial = MatWater;
        water.AddComponent<WaterScroll>().scrollSpeed = new Vector2(0.01f, 0.015f);

        // 물소리는 반드시 별도 오브젝트 — 나중에 소리만/모델만 따로 교체할 수 있어야 한다
        var amb = new GameObject("Water_Ambience");
        amb.transform.SetParent(water.transform, false);
        var src = amb.AddComponent<AudioSource>();
        src.playOnAwake = true;
        src.loop = true;
        src.volume = 0.35f;
        src.spatialBlend = 0f;
    }

    // 사방을 막는다. 한 면만 있으면 나머지 3면으로 지평선이 보여 어색하다.
    static void BuildCourtyardWalls(Transform env)
    {
        float wx = DeckHalfX - WallThick / 2f;
        float wz = DeckHalfZ - WallThick / 2f;
        float h = WallHeight / 2f;
        // 남북 벽이 모서리까지 덮어 틈이 생기지 않게 한다
        Box(env, "Wall_North", new Vector3(0, h, -wz), new Vector3(DeckHalfX * 2f, WallHeight, WallThick), MatWall, true);
        Box(env, "Wall_South", new Vector3(0, h, wz), new Vector3(DeckHalfX * 2f, WallHeight, WallThick), MatWall, true);
        Box(env, "Wall_West", new Vector3(-wx, h, 0), new Vector3(WallThick, WallHeight, DeckHalfZ * 2f - WallThick * 2f), MatWall, true);
        Box(env, "Wall_East", new Vector3(wx, h, 0), new Vector3(WallThick, WallHeight, DeckHalfZ * 2f - WallThick * 2f), MatWall, true);
    }

    // 벽 너머로 솟는 건물 매스. 지평선을 열지 않으면서 깊이를 준다.
    static void BuildBackdrop(Transform env)
    {
        Box(env, "BuildingBackdrop", new Vector3(0, 7f, -24f), new Vector3(34f, 14f, 6f), MatWall, false);
        for (int i = 0; i < 3; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "BuildingWindow_" + (i + 1);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(env, false);
            q.transform.localPosition = new Vector3(-10f + i * 10f, 11f, -20.95f);
            q.transform.localScale = new Vector3(1.4f, 1.9f, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = MatGlow;
        }
    }

    // ---- 소품 (전부 Dummies 하위. 그룹 하나가 나중에 VARCO 에셋이 들어올 슬롯) ----
    static void BuildProps(Transform d)
    {
        // 실제 프리팹이 이미 있는 것은 프리미티브로 대체하지 않는다.
        //
        // 아래 값은 **사용자가 유니티 에디터에서 직접 맞춘 값**이다. 추측하지 말 것:
        //   - 회전 X = -90 — prop_flamingo.fbx는 이 각도라야 튜브가 눕는다.
        //     (Scene_Game YAML에서 회전값을 읽어 맞춰 보려 했으나 더 세워져서 폐기했다.)
        //   - Y 는 수면(-0.12)에 닿아 보이도록 인스턴스마다 눈으로 조정한 값이다.
        // 나중에 VARCO 에셋으로 교체하면 둘 다 다시 맞춰야 한다.
        PlaceFlamingo(d, "Prop_Flamingo_A", new Vector3(1.9f, -0.191f, 1.6f), 18f);
        PlaceFlamingo(d, "Prop_Flamingo_B", new Vector3(-2.2f, -0.21f, -3.4f), -47f);
        PlacePrefab(d, "Assets/Prefabs/prop_radio.prefab", "Prop_Radio", new Vector3(-8f, 0.52f, -2f), 20f);

        float[] rowZ = { -4.5f, 0f, 4.5f };
        for (int i = 0; i < rowZ.Length; i++)
        {
            Lounger(d, "Prop_Lounger_W" + (i + 1), new Vector3(-6.6f, 0, rowZ[i]), 90f);
            Lounger(d, "Prop_Lounger_E" + (i + 1), new Vector3(6.6f, 0, rowZ[i]), -90f);
        }
        SideTable(d, "Prop_SideTable_1", new Vector3(-8f, 0, -2f));
        SideTable(d, "Prop_SideTable_2", new Vector3(-8f, 0, 2.5f));
        SideTable(d, "Prop_SideTable_3", new Vector3(8f, 0, -2f));
        SideTable(d, "Prop_SideTable_4", new Vector3(8f, 0, 2.5f));
        Parasol(d, "Prop_Parasol_1", new Vector3(-9.6f, 0, 0f));
        Parasol(d, "Prop_Parasol_2", new Vector3(9.6f, 0, 0f));
        Bench(d, "Prop_Bench_1", new Vector3(-9f, 0, 13f), 0f);
        Bench(d, "Prop_Bench_2", new Vector3(9f, 0, 13f), 0f);
        Bench(d, "Prop_Bench_3", new Vector3(0f, 0, 17f), 180f);
        Ladder(d, "Prop_PoolLadder_W", new Vector3(-2.2f, 0, -PoolHalfZ - 0.1f));
        Ladder(d, "Prop_PoolLadder_E", new Vector3(2.2f, 0, -PoolHalfZ - 0.1f));

        // 남쪽(스폰 뒤편)이 벽 하나에 벤치 하나뿐이라 휑했다 — 여기도 채운다
        Lounger(d, "Prop_Lounger_S1", new Vector3(-11.5f, 0, 8.5f), 115f);
        Lounger(d, "Prop_Lounger_S2", new Vector3(11.5f, 0, 8.5f), -115f);
        SideTable(d, "Prop_SideTable_5", new Vector3(-10.4f, 0, 9.6f));
        SideTable(d, "Prop_SideTable_6", new Vector3(10.4f, 0, 9.6f));
        Parasol(d, "Prop_Parasol_3", new Vector3(-12.2f, 0, 11.8f));
        Parasol(d, "Prop_Parasol_4", new Vector3(12.2f, 0, 11.8f));
        Bench(d, "Prop_Bench_4", new Vector3(-5.5f, 0, 18.6f), 180f);
        Bench(d, "Prop_Bench_5", new Vector3(5.5f, 0, 18.6f), 180f);
        // 수건 더미 — 선베드 위의 작은 디테일
        TowelStack(d, "Prop_Towels_1", new Vector3(-6.6f, 0.42f, -4.5f));
        TowelStack(d, "Prop_Towels_2", new Vector3(6.6f, 0.42f, 4.5f));
        TowelStack(d, "Prop_Towels_3", new Vector3(-11.5f, 0.42f, 8.5f));

        // 화분 — 벽을 따라 빈 면을 채운다
        Vector3[] planters = {
            new Vector3(-13.2f, 0, -16f), new Vector3(-13.2f, 0, -10f), new Vector3(-13.2f, 0, -3f),
            new Vector3(-13.2f, 0,  4f),  new Vector3(-13.2f, 0,  11f), new Vector3(-13.2f, 0, 17f),
            new Vector3( 13.2f, 0, -16f), new Vector3( 13.2f, 0, -10f), new Vector3( 13.2f, 0, -3f),
            new Vector3( 13.2f, 0,  4f),  new Vector3( 13.2f, 0,  11f), new Vector3( 13.2f, 0, 17f),
            new Vector3(-9f, 0, -17.8f),  new Vector3(-3f, 0, -17.8f),
            new Vector3( 3f, 0, -17.8f),  new Vector3( 9f, 0, -17.8f),
            // 남쪽 벽
            new Vector3(-11f, 0, 18.4f),  new Vector3(-1.8f, 0, 18.4f),
            new Vector3( 1.8f, 0, 18.4f), new Vector3( 11f, 0, 18.4f),
            // 네 모서리
            new Vector3(-13.4f, 0, -18.2f), new Vector3(13.4f, 0, -18.2f),
            new Vector3(-13.4f, 0,  18.2f), new Vector3(13.4f, 0,  18.2f),
        };
        for (int i = 0; i < planters.Length; i++)
            Planter(d, "Prop_Planter_" + (i + 1).ToString("00"), planters[i], 1.6f + (i % 3) * 0.55f);
    }

    static void Lounger(Transform p, string name, Vector3 pos, float yaw)
    {
        var g = Group(p, name, pos, yaw);
        Box(g, "Frame", new Vector3(0, 0.34f, 0), new Vector3(0.72f, 0.1f, 1.9f), MatCushion, false);
        Box(g, "Back", new Vector3(0, 0.6f, -0.72f), new Vector3(0.72f, 0.1f, 0.85f), MatCushion, false, new Vector3(-38f, 0, 0));
        Box(g, "Leg_A", new Vector3(-0.28f, 0.15f, 0.8f), new Vector3(0.06f, 0.3f, 0.06f), MatMetal, false);
        Box(g, "Leg_B", new Vector3(0.28f, 0.15f, 0.8f), new Vector3(0.06f, 0.3f, 0.06f), MatMetal, false);
        Box(g, "Leg_C", new Vector3(-0.28f, 0.15f, -0.8f), new Vector3(0.06f, 0.3f, 0.06f), MatMetal, false);
        Box(g, "Leg_D", new Vector3(0.28f, 0.15f, -0.8f), new Vector3(0.06f, 0.3f, 0.06f), MatMetal, false);
    }

    static void TowelStack(Transform p, string name, Vector3 pos)
    {
        var g = Group(p, name, pos, 0f);
        Box(g, "Towel_1", new Vector3(0, 0.03f, 0), new Vector3(0.44f, 0.06f, 0.32f), MatCushion, false);
        Box(g, "Towel_2", new Vector3(0.02f, 0.09f, 0.01f), new Vector3(0.42f, 0.06f, 0.30f), MatCushion, false, new Vector3(0, 7f, 0));
    }

    static void Bench(Transform p, string name, Vector3 pos, float yaw)
    {
        var g = Group(p, name, pos, yaw);
        Box(g, "Seat", new Vector3(0, 0.45f, 0), new Vector3(2.1f, 0.12f, 0.55f), MatProp, false);
        Box(g, "Back", new Vector3(0, 0.78f, -0.24f), new Vector3(2.1f, 0.55f, 0.1f), MatProp, false);
        Box(g, "Leg_L", new Vector3(-0.9f, 0.22f, 0), new Vector3(0.12f, 0.45f, 0.5f), MatProp, false);
        Box(g, "Leg_R", new Vector3(0.9f, 0.22f, 0), new Vector3(0.12f, 0.45f, 0.5f), MatProp, false);
    }

    static void SideTable(Transform p, string name, Vector3 pos)
    {
        var g = Group(p, name, pos, 0f);
        Cyl(g, "Top", new Vector3(0, 0.48f, 0), new Vector3(0.52f, 0.03f, 0.52f), MatProp);
        Cyl(g, "Stem", new Vector3(0, 0.24f, 0), new Vector3(0.07f, 0.24f, 0.07f), MatMetal);
        Cyl(g, "Base", new Vector3(0, 0.02f, 0), new Vector3(0.38f, 0.02f, 0.38f), MatMetal);
    }

    static void Parasol(Transform p, string name, Vector3 pos)
    {
        var g = Group(p, name, pos, 0f);
        Cyl(g, "Pole", new Vector3(0, 1.1f, 0), new Vector3(0.07f, 1.1f, 0.07f), MatMetal);
        Cyl(g, "Canopy", new Vector3(0, 2.2f, 0), new Vector3(2.6f, 0.06f, 2.6f), MatCushion);
        Cyl(g, "Base", new Vector3(0, 0.05f, 0), new Vector3(0.6f, 0.05f, 0.6f), MatProp);
    }

    static void Ladder(Transform p, string name, Vector3 pos)
    {
        var g = Group(p, name, pos, 0f);
        Cyl(g, "Rail_L", new Vector3(-0.22f, 0.45f, 0), new Vector3(0.05f, 0.45f, 0.05f), MatMetal);
        Cyl(g, "Rail_R", new Vector3(0.22f, 0.45f, 0), new Vector3(0.05f, 0.45f, 0.05f), MatMetal);
        Cyl(g, "Rung", new Vector3(0, 0.88f, 0), new Vector3(0.05f, 0.22f, 0.05f), MatMetal, new Vector3(0, 0, 90f));
    }

    static void Planter(Transform p, string name, Vector3 pos, float plantHeight)
    {
        var g = Group(p, name, pos, 0f);
        Cyl(g, "Pot", new Vector3(0, 0.3f, 0), new Vector3(0.72f, 0.3f, 0.72f), MatProp);
        Cyl(g, "Stem", new Vector3(0, 0.6f + plantHeight * 0.5f, 0), new Vector3(0.1f, plantHeight * 0.5f, 0.1f), MatPlant);
        // 잎 — 평평한 상자 몇 장. 더미라 이 이상 다듬지 않는다.
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f;
            var leaf = Box(g, "Leaf_" + (i + 1), Vector3.zero, new Vector3(0.22f, 0.04f, 1.15f), MatPlant, false);
            leaf.transform.localPosition = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.6f + plantHeight, 0.5f);
            leaf.transform.localRotation = Quaternion.Euler(-28f, a, 0);
        }
    }

    // ---- 눈꺼풀 오버레이 + 연출 컨트롤러 ----------------------------------
    // 컨트롤러는 반드시 **씬 루트**에 둔다. Environment 하위에 두면 맵을 다시 생성할 때
    // 같이 지워진다 (실제로 한 번 그렇게 날아갔다).
    static void BuildEyelids(Scene scene)
    {
        var canvas = Find(scene, "FadeCanvas");
        if (canvas == null) { Debug.LogWarning("[IntroMapBuilder] FadeCanvas 없음 — 눈꺼풀 생략"); return; }

        var top = MakeEyelid(canvas, "Eyelid_Top", 1f);
        var bot = MakeEyelid(canvas, "Eyelid_Bottom", 0f);

        var t = Find(scene, "CollapseSequence");
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject("CollapseSequence");
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        var seq = go.GetComponent<CollapseSequence>();
        if (seq == null) seq = go.AddComponent<CollapseSequence>();

        // 소리는 별도 AudioSource — 나중에 소리만 따로 교체할 수 있어야 한다
        var src = go.GetComponent<AudioSource>();
        if (src == null) src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.volume = 0.7f;
        src.spatialBlend = 0f;

        var playerT = Find(scene, "Player");
        seq.player = playerT != null ? playerT.GetComponent<PlayerController>() : null;
        seq.fader = canvas.GetComponent<ScreenFader>();
        seq.collapseSound = src;
        seq.eyelidTop = top;
        seq.eyelidBottom = bot;

        if (seq.player == null) Debug.LogWarning("[IntroMapBuilder] Player/PlayerController를 찾지 못했다");
        if (seq.fader == null) Debug.LogWarning("[IntroMapBuilder] ScreenFader를 찾지 못했다");
        EditorUtility.SetDirty(seq);
    }

    // edge: 1 = 화면 위에서 아래로 내려오는 막, 0 = 아래에서 위로 올라오는 막
    static RectTransform MakeEyelid(Transform canvas, string name, float edge)
    {
        var old = canvas.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.anchorMin = new Vector2(0f, edge);
        rt.anchorMax = new Vector2(1f, edge);
        rt.pivot = new Vector2(0.5f, edge);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f); // 높이는 CollapseSequence가 런타임에 키운다
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        rt.SetSiblingIndex(0);              // FadeImage보다 아래 = 페이드가 눈꺼풀을 덮는다
        return rt;
    }

    // ---- 헬퍼 -------------------------------------------------------------
    static Transform Root(Scene scene, string name)
    {
        var t = Find(scene, name);
        if (t != null) return t;
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.transform;
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

    static Transform Group(Transform p, string name, Vector3 pos, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return go.transform;
    }

    static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, Material mat,
                          bool collide, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        // 소품에 콜라이더를 두면 CharacterController가 의자 다리에 걸려 멈춘다.
        // 구조물(벽·바닥·코핑)만 충돌을 남긴다.
        if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Cyl(Transform p, string name, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static void PlaceFlamingo(Transform p, string name, Vector3 pos, float yaw)
    {
        var go = PlacePrefab(p, "Assets/Prefabs/prop_flamingo.prefab", name, pos, yaw);
        if (go == null) return;
        go.transform.localEulerAngles = new Vector3(-90f, yaw, 0f);

        // 프리팹 인스턴스에서는 localEulerAngles를 넣어도 Inspector 표시값
        // (m_LocalEulerAnglesHint)이 프리팹 원본값(0,30,0)으로 남는다. 실제 회전은
        // 맞지만 Inspector에는 엉뚱한 숫자가 보이므로 오버라이드를 직접 기록한다.
        var so = new SerializedObject(go.transform);
        so.FindProperty("m_LocalEulerAnglesHint").vector3Value = new Vector3(-90f, yaw, 0f);
        so.ApplyModifiedProperties();
    }

    static GameObject PlacePrefab(Transform p, string path, string name, Vector3 pos, float yaw)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null) { Debug.LogWarning("[IntroMapBuilder] 프리팹 없음: " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, p);
        go.name = name;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return go;
    }
}
