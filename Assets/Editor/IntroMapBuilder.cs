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
    const float DeckHalfX = 22f;    // 데크 반폭 — 맵 확장(2026-11-10): 기존 소품 배치는 그대로,
                                    // 가장자리에 걸어 다닐 여백을 더 둔다.
    const float DeckHalfZ = 28f;    // 데크 반길이
    // 2026-11-10: Pool Glow 레퍼런스(사진 같은 라군 분위기)처럼 수영장을 더 크게.
    // 2026-11-11: "너비를 조금 더" 요청으로 한 번 더 확장 — Lounger_W/E를 ±6.6→±7.8로
    // 밀어내 여유를 만들었다(아래 BuildProps). Bench_3(0,17)이 x=0 기준 가장 가까운
    // 제약이라 PoolHalfZ는 그보다 한참 여유 있게.
    const float PoolHalfX = 6.8f;   // 수영장 개구부 반폭 (Lounger 안쪽 끝 7.4까지 여유 0.3m)
    const float PoolHalfZ = 13.0f;  // 수영장 개구부 반길이 (Bench_3 안쪽 끝 16.65까지 여유 3.3m)
    const float PoolDepth = 1.5f;   // 수영장 깊이
    const float WallHeight = 16f;   // 중정 벽 높이 — 지평선을 가릴 만큼 (2026-11-10: 9→16,
                                    // 벽 위쪽 끝과 하늘이 너무 또렷하게 잘려 보이는 문제 완화)
    const float WallThick = 0.6f;
    const float CurbHeight = 0.36f; // CharacterController의 StepOffset(0.3)보다 높게 =
                                    // 플레이어가 수영장에 빠지지 않는다
    const float DeckThick = 0.4f;
    const float WaterY = -0.12f;    // 수면은 데크보다 살짝 아래 (실제 수영장처럼)

    static Material MatFloor, MatWall, MatWater, MatGlow, MatProp, MatCushion, MatPlant, MatMetal, MatPoolTile, MatTrunk, MatGlowOrb;

    [MenuItem("DEPTH 5m/Rebuild Intro Map")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        LoadMaterials();

        var env = Root(scene, "Environment");
        var dummies = Root(scene, "Dummies");
        ClearChildren(env, keep: "PoolReflectionProbe");
        ClearChildren(dummies, keep: null);

        BuildDawnSky();
        BuildDeck(env);
        BuildPoolBasin(env);
        BuildWater(env);
        BuildCourtyardWalls(env);
        BuildBackdrop(env);
        var interactPrompt = BuildInteractPrompt(scene);
        BuildProps(dummies, interactPrompt);
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
        MatTrunk = MakeMat("Assets/Materials/Env/Env_Court_Trunk.mat", new Color(0.45f, 0.32f, 0.22f), 0.10f);
        // 2026-11-11: Pool Glow 레퍼런스의 반복 모티프 — 물 위에 뜬 발광 구체. 톤은 하늘색.
        MatGlowOrb = MakeEmissiveMat("Assets/Materials/Env/Env_GlowOrb.mat", new Color(0.65f, 0.85f, 1f), 2.2f);
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

    // 발광 소품용 — Emission을 켜서 Bloom이 잡아먹도록 한다.
    static Material MakeEmissiveMat(string path, Color color, float intensity)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(sh);
        m.SetColor("_BaseColor", color);
        m.SetColor("_EmissionColor", color * intensity);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // 하늘이 지나치게 밝고 바래 보이는 문제 — 기존엔 Unity 기본 프로시저럴 스카이박스
    // (Default-Skybox) 그대로였다. 이미 설정된 따뜻한 안개색/환경광과 맞춘 톤 + 낮은 노출로
    // 전용 머터리얼을 만들어 쓴다. 셰이더 자체는 그대로(Skybox/Procedural) — 셰이더 교체는
    // 별도 보류 작업([[depth5m-visual-reference-pool-glow]] 참고).
    static void BuildDawnSky()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Env/Env_DawnSky.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(mat, "Assets/Materials/Env/Env_DawnSky.mat");
        }
        mat.SetColor("_SkyTint", new Color(0.93f, 0.78f, 0.70f));
        mat.SetColor("_GroundColor", new Color(0.62f, 0.54f, 0.50f));
        mat.SetFloat("_AtmosphereThickness", 1.15f);
        mat.SetFloat("_Exposure", 0.9f);
        EditorUtility.SetDirty(mat);

        RenderSettings.skybox = mat;
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
        // TD-002: 사다리로 올라온 뒤 데크로 나갈 수 있도록, 북쪽 코핑에 사다리 x(±2.2) 위치에
        // 폭 1.0m 개구부 2곳을 남기고 3구간으로 분할한다. PoolHalfX가 바뀌어도(2026-11-10
        // 맵 확장) 자동으로 맞도록 고정 숫자 대신 수식으로 계산한다.
        const float ladderX = 2.2f;
        const float gapHalf = 0.5f; // 개구부 폭(1.0m)의 절반
        float curbOuterX = PoolHalfX + 0.3f;             // 코핑 전체 바깥쪽 끝
        float centerSegWidth = (ladderX - gapHalf) * 2f;  // 가운데(문 사이) 구간 폭
        float sideSegWidth = curbOuterX - (ladderX + gapHalf); // 양쪽 끝 구간 폭
        float sideSegCenterX = (curbOuterX + (ladderX + gapHalf)) / 2f;

        Box(env, "PoolCurb_North_W", new Vector3(-sideSegCenterX, CurbHeight / 2f, -PoolHalfZ - 0.15f),
            new Vector3(sideSegWidth, CurbHeight, 0.3f), MatFloor, true);
        Box(env, "PoolCurb_North_C", new Vector3(0, CurbHeight / 2f, -PoolHalfZ - 0.15f),
            new Vector3(centerSegWidth, CurbHeight, 0.3f), MatFloor, true);
        Box(env, "PoolCurb_North_E", new Vector3(sideSegCenterX, CurbHeight / 2f, -PoolHalfZ - 0.15f),
            new Vector3(sideSegWidth, CurbHeight, 0.3f), MatFloor, true);
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

        // 파라펫(옥상 난간 턱) — 벽 맨 위에 살짝 튀어나온 얇은 테두리를 둘러, 완전히 평평한
        // 상자가 하늘과 또렷한 수평선으로 잘려 보이는 문제를 완화한다. 콜라이더는 걸어 다니는
        // 높이보다 훨씬 위라 게임플레이에 영향 없음.
        float capH = 0.6f;
        float capY = WallHeight + capH / 2f;
        float capOverhang = 0.4f; // 벽 두께보다 이만큼 더 두껍게 — 옥상 턱처럼 보이게
        Box(env, "Wall_North_Cap", new Vector3(0, capY, -wz), new Vector3(DeckHalfX * 2f, capH, WallThick + capOverhang), MatWall, true);
        Box(env, "Wall_South_Cap", new Vector3(0, capY, wz), new Vector3(DeckHalfX * 2f, capH, WallThick + capOverhang), MatWall, true);
        Box(env, "Wall_West_Cap", new Vector3(-wx, capY, 0), new Vector3(WallThick + capOverhang, capH, DeckHalfZ * 2f - WallThick * 2f), MatWall, true);
        Box(env, "Wall_East_Cap", new Vector3(wx, capY, 0), new Vector3(WallThick + capOverhang, capH, DeckHalfZ * 2f - WallThick * 2f), MatWall, true);
    }

    // 벽 너머로 솟는 건물 매스. 지평선을 열지 않으면서 깊이를 준다.
    // 북쪽 벽(Wall_North, z=-wz) 뒤에 자리잡아야 하므로 DeckHalfZ/WallHeight를 따라간다 —
    // 둘 다 맵 확장(2026-11-10)으로 바뀌었으니 하드코딩하면 벽보다 안쪽(앞)으로 들어와 버린다.
    static void BuildBackdrop(Transform env)
    {
        const float peekAboveWall = 5f;  // 기존(벽 9 -> 매스 14)과 같은 비율로 벽 위로 솟는 높이
        const float gapBehindWall = 4f;  // 벽에서 얼마나 더 뒤에 세울지

        float backdropHeight = WallHeight + peekAboveWall;
        float backdropZ = -(DeckHalfZ + gapBehindWall);
        float backdropY = backdropHeight / 2f;

        Box(env, "BuildingBackdrop", new Vector3(0, backdropY, backdropZ), new Vector3(34f, backdropHeight, 6f), MatWall, false);

        float windowY = WallHeight + 2f;        // 벽 위로 2m — 기존과 같은 "살짝 넘겨 보이는" 비율
        float windowZ = backdropZ + 3.05f;      // 매스 앞면 쪽 (기존 상대 위치 그대로)
        for (int i = 0; i < 3; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "BuildingWindow_" + (i + 1);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(env, false);
            q.transform.localPosition = new Vector3(-10f + i * 10f, windowY, windowZ);
            q.transform.localScale = new Vector3(1.4f, 1.9f, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = MatGlow;
        }
    }

    // ---- 소품 (전부 Dummies 하위. 그룹 하나가 나중에 VARCO 에셋이 들어올 슬롯) ----
    static void BuildProps(Transform d, GameObject interactPrompt)
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

        // 2026-11-10: Pool Glow 레퍼런스 분위기 — 물 속에서 자라는 야자수 + 큰 흰 기둥.
        // 좌표는 사다리(x ±2.2, z≈-7.05)/플라밍고/코핑(새 PoolHalfX 5.6, PoolHalfZ 10.5)과
        // 겹치지 않도록 간격을 두고 골랐다.
        WaterPalm(d, "Prop_WaterPalm_1", new Vector3(-3.5f, 0, -5.0f), 3.2f);
        WaterPalm(d, "Prop_WaterPalm_2", new Vector3(3.5f, 0, -2.5f), 3.6f);
        WaterPalm(d, "Prop_WaterPalm_3", new Vector3(-3.0f, 0, 5.0f), 3.0f);
        WaterPalm(d, "Prop_WaterPalm_4", new Vector3(3.5f, 0, 7.0f), 3.4f);

        // 플레이어 스폰(0,0,10)이 -Z를 정면으로 보고 있어, x=0 근처에 두면 기둥이 시야를
        // 전부 가린다 — 야자수처럼 옆으로 비켜 세운다.
        BigPillar(d, "Prop_BigPillar_1", new Vector3(-4.0f, 0, 1.0f), 5.5f);
        BigPillar(d, "Prop_BigPillar_2", new Vector3(4.0f, 0, 3.0f), 5.0f);

        // 야자수 밑동마다 한두 개씩 군집 + 수영장 가운데 몇 개 — 사진 속 반복 모티프.
        GlowOrb(d, "Prop_GlowOrb_1", new Vector3(-3.0f, 0, -4.3f), 0.35f);
        GlowOrb(d, "Prop_GlowOrb_2", new Vector3(-4.0f, 0, -4.6f), 0.28f);
        GlowOrb(d, "Prop_GlowOrb_3", new Vector3(3.0f, 0, -1.8f), 0.32f);
        GlowOrb(d, "Prop_GlowOrb_4", new Vector3(-2.5f, 0, 4.3f), 0.30f);
        GlowOrb(d, "Prop_GlowOrb_5", new Vector3(3.0f, 0, 6.3f), 0.34f);
        GlowOrb(d, "Prop_GlowOrb_6", new Vector3(0f, 0, -4.0f), 0.40f);
        GlowOrb(d, "Prop_GlowOrb_7", new Vector3(1.0f, 0, 4.0f), 0.38f);

        float[] rowZ = { -4.5f, 0f, 4.5f };
        for (int i = 0; i < rowZ.Length; i++)
        {
            // 2026-11-11: 수영장이 커지면서 ±6.6은 새 PoolHalfX(6.8)와 겹친다 — ±7.8로 밀어냄.
            Lounger(d, "Prop_Lounger_W" + (i + 1), new Vector3(-7.8f, 0, rowZ[i]), 90f);
            Lounger(d, "Prop_Lounger_E" + (i + 1), new Vector3(7.8f, 0, rowZ[i]), -90f);
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
        // TD-002: 코핑 위(−0.1f)가 아니라 수영장 개구부 안쪽(+0.25f)으로 — 사다리가
        // 실제로 바닥까지 내려가야 들어간 뒤 돌아 나올 수 있다.
        Ladder(d, "Prop_PoolLadder_W", new Vector3(-2.2f, 0, -PoolHalfZ + 0.25f), interactPrompt);
        Ladder(d, "Prop_PoolLadder_E", new Vector3(2.2f, 0, -PoolHalfZ + 0.25f), interactPrompt);

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
        GroupCollider(g, new Vector3(0, 0.35f, 0), new Vector3(0.80f, 0.70f, 2.00f));
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
        GroupCollider(g, new Vector3(0, 0.50f, -0.05f), new Vector3(2.20f, 1.00f, 0.70f));
    }

    static void SideTable(Transform p, string name, Vector3 pos)
    {
        var g = Group(p, name, pos, 0f);
        Cyl(g, "Top", new Vector3(0, 0.48f, 0), new Vector3(0.52f, 0.03f, 0.52f), MatProp);
        Cyl(g, "Stem", new Vector3(0, 0.24f, 0), new Vector3(0.07f, 0.24f, 0.07f), MatMetal);
        Cyl(g, "Base", new Vector3(0, 0.02f, 0), new Vector3(0.38f, 0.02f, 0.38f), MatMetal);
        GroupCollider(g, new Vector3(0, 0.26f, 0), new Vector3(0.55f, 0.52f, 0.55f));
    }

    static void Parasol(Transform p, string name, Vector3 pos)
    {
        var g = Group(p, name, pos, 0f);
        Cyl(g, "Pole", new Vector3(0, 1.1f, 0), new Vector3(0.07f, 1.1f, 0.07f), MatMetal);
        Cyl(g, "Canopy", new Vector3(0, 2.2f, 0), new Vector3(2.6f, 0.06f, 2.6f), MatCushion);
        Cyl(g, "Base", new Vector3(0, 0.05f, 0), new Vector3(0.6f, 0.05f, 0.6f), MatProp);
        // 기둥만 — 캐노피(y 2.2)는 플레이어 키(1.8)보다 위라 콜라이더가 필요 없다.
        GroupCollider(g, new Vector3(0, 1.10f, 0), new Vector3(0.22f, 2.20f, 0.22f));
    }

    static void Ladder(Transform p, string name, Vector3 pos, GameObject interactPrompt)
    {
        var g = Group(p, name, pos, 0f);

        // TD-002: 레일을 수영장 바닥 근처까지 연장 (y -1.45 ~ +0.95)
        Cyl(g, "Rail_L", new Vector3(-0.22f, -0.25f, 0), new Vector3(0.05f, 1.2f, 0.05f), MatMetal);
        Cyl(g, "Rail_R", new Vector3(0.22f, -0.25f, 0), new Vector3(0.05f, 1.2f, 0.05f), MatMetal);

        // 가로대 — 기존 손잡이(0.88) + 수중 가로대 5개
        float[] rungY = { 0.88f, -0.25f, -0.50f, -0.75f, -1.00f, -1.25f };
        foreach (var y in rungY)
            Cyl(g, "Rung_" + y.ToString("0.00"), new Vector3(0, y, 0), new Vector3(0.05f, 0.22f, 0.05f), MatMetal, new Vector3(0, 0, 90f));

        // TD-002: 자동 계단 등반(스텝 콜라이더)은 CharacterController 캡슐(높이 1.8m)이
        // 계단 간격(0.25m)보다 훨씬 커서 한 칸 올라서면 바로 위 칸과 몸이 겹쳐 막히는
        // 문제가 있어 폐기 — 대신 E 입력으로 명시적으로 데크까지 올려준다.
        var climbZone = new GameObject("ClimbZone");
        climbZone.transform.SetParent(g, false);
        climbZone.transform.localPosition = new Vector3(0, -0.75f, 0);
        var zoneCol = climbZone.AddComponent<BoxCollider>();
        zoneCol.isTrigger = true;
        zoneCol.size = new Vector3(1.2f, 1.8f, 1.4f);

        var exitPoint = new GameObject("ExitPoint");
        exitPoint.transform.SetParent(g, false);
        // 로컬 z -0.9 = 코핑(폭 0.3, 바깥쪽 끝)을 지나 데크 위 (y 0, 데크 표면과 동일)
        exitPoint.transform.localPosition = new Vector3(0, 0, -0.9f);

        var climb = climbZone.AddComponent<LadderClimb>();
        climb.exitPoint = exitPoint.transform;
        climb.promptUI = interactPrompt;
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
        // 화분만 — 잎은 통과(스치는 느낌 유지).
        GroupCollider(g, new Vector3(0, 0.30f, 0), new Vector3(0.76f, 0.60f, 0.76f));
    }

    // Pool Glow 레퍼런스 — 물 속에서 자라는 야자수. Planter()와 비슷한 모양이지만 화분이
    // 없고(수영장 바닥에서 바로 자람), 몸통이 수면 위로 한참 솟아 있다.
    static void WaterPalm(Transform p, string name, Vector3 pos, float trunkHeight)
    {
        var g = Group(p, name, pos, 0f);

        // 몸통 — 수영장 바닥 근처(y -1.0)에서 trunkHeight만큼 위로
        float trunkCenterY = trunkHeight / 2f - 1.0f;
        Cyl(g, "Trunk", new Vector3(0, trunkCenterY, 0), new Vector3(0.22f, trunkHeight / 2f, 0.22f), MatTrunk);

        // 잎 — Planter()와 같은 방식(방사형 평판 6장), 몸통 꼭대기에서
        float topY = trunkHeight - 1.0f;
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f;
            var leaf = Box(g, "Leaf_" + (i + 1), Vector3.zero, new Vector3(0.26f, 0.05f, 1.6f), MatPlant, false);
            leaf.transform.localPosition = Quaternion.Euler(0, a, 0) * new Vector3(0, topY, 0.8f);
            leaf.transform.localRotation = Quaternion.Euler(-30f, a, 0);
        }

        // 몸통만 콜라이더 — 수영장 안에서도 부딪히는 느낌(잎은 스치고 지나감).
        GroupCollider(g, new Vector3(0, trunkCenterY, 0), new Vector3(0.5f, trunkHeight, 0.5f));
    }

    // Pool Glow 레퍼런스의 반복 모티프 — 수면에 반쯤 잠긴 채 뜬 발광 구체. 콜라이더 없음
    // (장식용, 부딪히지 않고 스치고 지나간다).
    static void GlowOrb(Transform p, string name, Vector3 pos, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = new Vector3(pos.x, WaterY, pos.z);
        go.transform.localScale = Vector3.one * radius * 2f;
        go.GetComponent<MeshRenderer>().sharedMaterial = MatGlowOrb;
    }

    // Pool Glow 레퍼런스 — 수영장 한가운데 서 있는 큰 흰 기둥.
    static void BigPillar(Transform p, string name, Vector3 pos, float height)
    {
        float centerY = height / 2f - 1.2f; // 바닥(수영장 바닥 근처)부터 위로
        var go = Cyl(p, name, new Vector3(pos.x, centerY, pos.z), new Vector3(2.4f, height / 2f, 2.4f), MatWall);
        var col = go.AddComponent<CapsuleCollider>();
        col.direction = 1; // Y축
        col.radius = 1.2f;
        col.height = height;
        col.center = Vector3.zero;
    }

    // TD-002: "E로 오르기" 같은 상호작용 안내 문구. 여러 사다리가 이 UI 하나를 공유한다.
    // 기존 UI(PAUSED, SURFACE 등)와 같은 방식으로 영어 + 기본 폰트를 쓴다 — 이 프로젝트에는
    // 아직 한글 글리프가 있는 폰트가 설정되어 있지 않아, 지금 한글을 넣으면 빈 네모로 깨진다.
    // 한글 전환은 별도 작업(TMP 한글 폰트 점검)에서 폰트 문제까지 같이 처리한다.
    static GameObject BuildInteractPrompt(Scene scene)
    {
        var canvas = Find(scene, "FadeCanvas");
        if (canvas == null) { Debug.LogWarning("[IntroMapBuilder] FadeCanvas 없음 — 상호작용 안내 생략"); return null; }

        var old = canvas.Find("InteractPrompt");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("InteractPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(400f, 40f);

        var txt = go.GetComponent<UnityEngine.UI.Text>();
        txt.text = "Press E";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 기존 UI와 동일한 기본 폰트
        txt.fontSize = 24;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;

        go.SetActive(false); // 평소엔 숨김 — LadderClimb이 근처에서만 켠다
        return go;
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

    // 소품 묶음 전체를 감싸는 단순 콜라이더 1개 — 개별 부품(다리 등)에는 콜라이더를 두지 않는다.
    // TD-001: 그룹당 콜라이더 1개면 틈이 없어 "의자 다리 끼임"이 재발하지 않는다.
    static void GroupCollider(Transform group, Vector3 center, Vector3 size)
    {
        var col = group.gameObject.AddComponent<BoxCollider>();
        col.center = center;
        col.size = size;
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
