using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 방별 구성. 좌표는 모두 월드 기준이고, 방 크기·벽 위치는 씬에 있는 값을 그대로 따른다(바꾸지 않는다).
public static partial class GreyboxGraphicsPass
{
    static Material housingMat;
    static Material Housing() => housingMat ??= Lit("GB_Fixture_Housing", m => Plain(m, new Color(0.7f, 0.72f, 0.7f), 0.35f, 0.6f));

    // ---------------------------------------------------------------- ① 수영장 홀 (A-4, D-2)
    // 원본 Scene_SecondDream 톤: 차가운 회청색, 노출 9.7. 태양은 씬 공용(분홍)이라 Local Volume의 화이트밸런스로 식힌다.
    static void ApplyHall()
    {
        var root = Root("GB_01_PoolHall");
        var gen = Fresh(root.transform, GenName);
        var profile = Profile("PoolHall", p =>
        {
            var exp = Persist(p, p.Add<Exposure>(true));
            exp.mode.Override(ExposureMode.Fixed); exp.fixedExposure.Override(9.7f);
            var fog = Persist(p, p.Add<Fog>(true));
            fog.enabled.Override(true); fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(120f); fog.albedo.Override(new Color(0.62f, 0.76f, 1f));
            var wb = Persist(p, p.Add<WhiteBalance>(true));
            wb.temperature.Override(-40f); wb.tint.Override(-8f);
        });
        LocalVolume(gen, "LocalVolume", new Vector3(0f, 4f, -97.7f), new Vector3(11.7f, 14f, 51.7f), 1.5f, 110f, profile);

        // B-1: 입구 방 타일이 벽 크기에 따라 가로로 늘어나 있었다(원본 Scene_SecondDream도 같다).
        // 공용 재질 Env_Wall / Env_Floor는 건드리지 않고, 같은 색의 월드 Triplanar 재질을 새로 만들어 입구 방에만 입힌다.
        var clean = TileSet("GB_TileClean", 8, new Color(0.9f, 0.92f, 0.92f), new Color(0.6f, 0.64f, 0.66f), new Color(0.55f, 0.58f, 0.5f), 0.1f, 31);
        var entryWall = Lit("GB_Hall_EntryWallTile", m => Textured(m, clean, 1.6f, new Color(0.62f, 0.8f, 0.82f)));     // 20cm 타일
        var entryFloor = Lit("GB_Hall_EntryFloorTile", m => Textured(m, clean, 2.4f, new Color(0.35f, 0.6f, 0.68f)));   // 30cm 타일
        foreach (Transform t in root.transform)
        {
            if (!t.TryGetComponent<MeshRenderer>(out var r)) continue;
            if (t.name.StartsWith("Entry_Wall")) r.sharedMaterial = entryWall;
            else if (t.name.StartsWith("Entry_Floor")) r.sharedMaterial = entryFloor;
        }
    }

    // ---------------------------------------------------------------- ② 갈라진 복도 (A-1, B-1)
    static void ApplyCrack()
    {
        var root = Root("GB_02_Crack");
        DisableOldLights(root);
        SetLayers(root, LCrack);
        AddLayer(Root("GB_01_PoolHall"), "Hall_Wall_West", LCrack);   // 문틀 안쪽 면이 복도 조명도 받게
        AddLayer(Root("GB_03_Yellow"), "Wall_West", LCrack);

        var tileTex = TileSet("GB_TileSmall", 16, new Color(0.88f, 0.92f, 0.88f), new Color(0.68f, 0.71f, 0.67f), new Color(0.5f, 0.52f, 0.36f), 0.3f, 11);
        var wallMat = Lit("GB_Crack_WallTile", m => Textured(m, tileTex, 1.6f, new Color(0.95f, 0.98f, 0.95f)));            // 10cm 타일
        var floorMat = Lit("GB_Crack_FloorTile", m => Textured(m, tileTex, 3.2f, new Color(0.6f, 0.68f, 0.69f), 0.8f));     // 20cm 타일, 조금 어둡게
        var ceilMat = Lit("GB_Crack_Ceiling", m => Plain(m, new Color(0.8f, 0.82f, 0.79f), 0.15f));
        SetStructureMaterials(root.transform, floorMat, wallMat, ceilMat);

        var gen = Fresh(root.transform, GenName);
        var covers = Group(gen, "WallCovers");
        Cover(covers, "West", false, 6.17f, -109.7f, -103.7f, 4f, -107.3f, -106.1f, 2.2f, wallMat, LCrack);
        // 2026-11-18: 복도 → 노란 방 길을 막고 샤워실로 돌렸다(NewRooms.cs). 동쪽 끝 덧판은 문 자리 없이 덮는다.
        Cover(covers, "East", false, 19.83f, -109.7f, -103.7f, 4f, -107.7f, -107.7f, 2.6f, wallMat, LCrack);

        // 창백한 형광등 3개(4300K 근처, 살짝 초록). 깜빡임 없음.
        var lights = Group(gen, "Lights");
        var cold = new Color(0.93f, 1f, 0.95f);
        foreach (float x in new[] { 8.6f, 13.1f, 17.6f })
            Fluorescent(lights, $"Fluorescent_{x:F0}", new Vector3(x, 4f, -106.7f), true, 2.4f, 0.22f, cold, 90000f, 2600f, Housing(), "GB_Crack_TubeGlow", LCrack);
        // 아래로만 쏘는 면광원이 못 밝히는 천장을 채우는 약한 보조광
        foreach (float x in new[] { 9.6f, 16.6f })
            MakeLight(lights, "CeilingFill", LightType.Point, new Vector3(x, 1.5f, -106.7f), Vector3.zero, new Color(0.88f, 0.98f, 0.92f), 32000f, 9f, LCrack, false, false);

        var profile = RoomProfile("Crack", 8.0f, 0.04f, 0.12f, new Color(0.78f, 0.9f, 0.82f), 45f, new Color(0.02f, 0.03f, 0.025f), -4f, -6f);
        LocalVolume(gen, "LocalVolume", new Vector3(13.07f, 2f, -106.7f), new Vector3(13.55f, 4f, 6f), 1.2f, 120f, profile);
    }

    // ---------------------------------------------------------------- ③ 노란 방 (A-2, B-3)
    static void ApplyYellow()
    {
        var root = Root("GB_03_Yellow");
        DisableOldLights(root);
        SetLayers(root, LYellow);
        AddLayer(root, "Wall_West", LCrack);
        AddLayer(Root("GB_04_Angel"), "Wall_North", LYellow);

        var wallMat = Lit("GB_Yellow_Wallpaper", m => Textured(m, WallpaperSet(), 3f, Color.white, 0.6f));    // 3m = 벽 높이: 위쪽 물 얼룩이 벽 위에 온다
        var floorMat = Lit("GB_Yellow_Carpet", m => Textured(m, CarpetSet(), 4f, Color.white, 1.4f));
        var ceilMat = Lit("GB_Yellow_CeilingTile", m => Textured(m, CeilingTileSet(), 2.4f, Color.white));     // 0.6m 격자
        SetStructureMaterials(root.transform, floorMat, wallMat, ceilMat);

        var gen = Fresh(root.transform, GenName);
        var covers = Group(gen, "WallCovers");
        // 2026-11-18: 노란 방 → 천사 방 길을 막고 거실로 돌렸다(NewRooms.cs). 남쪽 덧판은 문 자리 없이, 서쪽 문은 샤워실 쪽(z -101.5)으로 옮겼다.
        Cover(covers, "South", true, -114.38f, 20.15f, 36.15f, 3f, 27.15f, 27.15f, 2.6f, wallMat, LYellow); // 천사 방 북쪽 벽의 뒷면을 벽지로 가린다
        // 복도 북·남 벽 끝(x=20.15)이 노란 방 서쪽 벽 안쪽 면과 같은 자리라 타일이 세로 줄로 비쳤다. 벽지판으로 덮는다.
        Cover(covers, "West", false, 20.18f, -114.4f, -98.7f, 3f, ShOutZ - 0.9f, ShOutZ + 0.9f, 2.4f, wallMat, LYellow);

        // 걸레받이: 벽지와 카펫이 만나는 선
        var trimMat = Lit("GB_Yellow_Trim", m => Plain(m, new Color(0.3f, 0.22f, 0.12f), 0.3f));
        var trim = Group(gen, "Baseboard");
        Cover(trim, "N", true, -98.72f, 20.15f, 36.15f, 0.1f, 20.15f, 20.15f, 0.1f, trimMat, LYellow);
        Cover(trim, "E", false, 36.13f, -114.4f, -98.7f, 0.1f, LvInZ - 0.9f, LvInZ + 0.9f, 0.1f, trimMat, LYellow);
        Cover(trim, "S", true, -114.35f, 20.15f, 36.15f, 0.1f, 27.15f, 27.15f, 0.1f, trimMat, LYellow);
        Cover(trim, "W", false, 20.22f, -114.4f, -98.7f, 0.1f, ShOutZ - 0.9f, ShOutZ + 0.9f, 0.1f, trimMat, LYellow);

        // 격자 천장등 9개: 누런 형광 패널(3000K 근처)
        var lights = Group(gen, "Lights");
        var warm = new Color(1f, 0.9f, 0.6f);
        // 2026-11-20 빛을 가구 쪽으로 좁힌다: 가구가 없는 서쪽 가운데·방 한가운데 패널 2개는 나간 등(빛 없음, 어두운 관)으로 둔다.
        // 북쪽 줄은 책상·서류함·액자, 남쪽 줄은 소파·의자·삐에로, 동쪽 가운데는 TV를 비춘다.
        var deadTube = Lit("GB_Yellow_PanelDead", m => Plain(m, new Color(0.32f, 0.3f, 0.24f), 0.5f));
        foreach (float x in new[] { 23.05f, 28.15f, 33.25f })
            foreach (float z in new[] { -101.45f, -106.55f, -111.65f })
            {
                var l = Fluorescent(lights, $"Panel_{x:F0}_{-z:F0}", new Vector3(x, 3f, z), false, 1.2f, 0.6f, warm, 120000f, 1500f, Housing(), "GB_Yellow_PanelGlow", LYellow);
                l.range = 9f;
                // 같은 날 사용자 요청(패널 더 끄기): 북동(정수기만)·남서(상자·의자) 모서리도 끈다 → 방 모서리가 어둠에 묻힌다.
                // 켜 둔 5개: 북서(입구·책상), 북쪽 가운데(액자), 동쪽 가운데(TV), 남쪽 가운데(소파), 남동(삐에로).
                bool dead = (z == -106.55f && x < 30f) || (x > 30f && z == -101.45f) || (x < 25f && z == -111.65f);
                if (dead)
                {
                    // 2026-11-23 사용자 요청: 나간 등은 가끔 깜빡인다(LightFlicker). 평소엔 꺼져 있어 방 모서리는 그대로 어둡다.
                    var tube = l.transform.parent.Find("Tube").GetComponent<MeshRenderer>();
                    var fl = l.transform.parent.gameObject.AddComponent<LightFlicker>();
                    fl.lamp = l; fl.tube = tube; fl.onMaterial = tube.sharedMaterial; fl.offMaterial = deadTube;
                    fl.seed = Mathf.RoundToInt(x * 10f - z * 7f);
                    l.enabled = false;
                    tube.sharedMaterial = deadTube;
                }
            }
        // 바닥 반사광 흉내: 그림자 없는 점광을 바닥 아래에 두면 바닥 윗면은 등지고 천장과 벽 아래쪽만 고르게 밝힌다.
        // 2026-11-20 방 끝까지 고르게 밝혀 휑해 보이는 원인이라 120000/14m → 30000/8m.
        foreach (float x in new[] { 24.15f, 32.15f })
            foreach (float z in new[] { -102.6f, -110.6f })
                MakeLight(lights, "BounceFill", LightType.Point, new Vector3(x, -3f, z), Vector3.zero, new Color(1f, 0.86f, 0.52f), 30000f, 8f, LYellow, false, false);
        // 안개를 옅게 깔아 공기가 탁해 보이게
        var profile = RoomProfile("Yellow", 7.9f, 0.03f, 0.1f, new Color(1f, 0.86f, 0.5f), 26f, new Color(0.05f, 0.04f, 0.015f), 5f, 2f);
        LocalVolume(gen, "LocalVolume", new Vector3(28.15f, 1.5f, -106.55f), new Vector3(16f, 3f, 15.7f), 1f, 130f, profile); // 천사 방(125)보다 높다: 노란 방 안에서는 노란 방 값이 이긴다
    }

    /// <summary>
    /// HDRP 물(Water Surface, Pool·Quad)을 놓는다. center = 수면 가운데, size = 가로·세로(m). 방 조명만 받게 렌더링 레이어를 맞춘다.
    /// 2026-11-19: 샤워실 발목 물과 도시에 차오르는 물을 반투명 판에서 이것으로 바꿨다(물결·굴절·바닥 빛무늬).
    /// </summary>
    static WaterSurface PoolWater(Transform parent, string name, Vector3 center, Vector2 size, uint layer,
        Color refraction, Color scattering, float absorption, float wind, float caustics)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, 1f, size.y);
        var ws = go.AddComponent<WaterSurface>();
        ws.surfaceType = WaterSurfaceType.Pool; ws.geometryType = WaterGeometryType.Quad;
        ws.refractionColor = refraction; ws.scatteringColor = scattering;
        ws.absorptionDistance = absorption; ws.maxRefractionDistance = 1.5f;
        ws.ripplesWindSpeed = wind; ws.ripplesChaos = 0.6f;
        ws.caustics = caustics > 0f; ws.causticsIntensity = caustics;
        ws.renderingLayerMask = (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)(int)layer;
        return ws;
    }

    // ---------------------------------------------------------------- 물과 하늘 (D-1, D-3)
    /// <summary>전역 고정 노출(EV). 클수록 어둡다.</summary>
    const float GlobalEV = 9.0f;

    static void ApplyWaterAndSky()
    {
        // D-1: 더 맑은 하늘색 물. 흡수를 줄여 바닥 타일이 비치게 하고, 물결 무늬(caustics)는 조금 약하게.
        foreach (var ws in Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!ws.name.EndsWith("_HDRP")) continue; // 수영장 물(HdrpScenePort)만. 샤워실·도시 물은 PoolWater()에서 따로 정한다.
            ws.refractionColor = new Color(0.62f, 0.94f, 1f);
            ws.scatteringColor = new Color(0.36f, 0.8f, 0.88f);
            ws.absorptionDistance = 14f; ws.maxRefractionDistance = 1.5f;
            ws.causticsIntensity = 0.8f;
            UnityEditor.EditorUtility.SetDirty(ws);
        }

        // D-3: 하늘은 새벽 분홍 그대로 두고 지평선과 천정 색만 다듬는다.
        // HDRP_Intro_Profile은 Scene_Intro와 같이 쓰므로 그레이박스 전용 복사본을 만들어 그쪽만 고친다.
        const string src = "Assets/Settings/HDRP_Intro_Profile.asset", dst = "Assets/Settings/HDRP_GB_Global_Profile.asset";
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(dst) == null) UnityEditor.AssetDatabase.CopyAsset(src, dst);
        var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(dst);
        if (profile.TryGet<PhysicallyBasedSky>(out var sky))
        {
            sky.horizonTint.Override(new Color(1f, 0.8f, 0.84f));
            sky.zenithTint.Override(new Color(0.78f, 0.74f, 1f));
            sky.horizonZenithShift.Override(0.15f);
            UnityEditor.EditorUtility.SetDirty(sky);
        }
        // 방별 밝기 맞추기(2026-11-20): 전역 노출은 방 볼륨이 없는 인트로·마지막 수영장에만 실제로 쓰인다.
        // 8.7에서는 화면 평균 밝기가 0.80~0.82, 마지막 수영장은 11%가 하얗게 날아갔다. 0.3 EV 어둡게 한다.
        if (profile.TryGet<Exposure>(out var gexp)) { gexp.fixedExposure.Override(GlobalEV); UnityEditor.EditorUtility.SetDirty(gexp); }
        UnityEditor.EditorUtility.SetDirty(profile);
        Root("HDRP_GlobalVolume").GetComponent<Volume>().sharedProfile = profile;
    }
}