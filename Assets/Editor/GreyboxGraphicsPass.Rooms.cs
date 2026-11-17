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
        Cover(covers, "East", false, 19.83f, -109.7f, -103.7f, 4f, -107.7f, -105.7f, 2.6f, wallMat, LCrack);

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
        Cover(covers, "South", true, -114.38f, 20.15f, 36.15f, 3f, 27.15f, 29.15f, 2.6f, wallMat, LYellow); // 천사 방 북쪽 벽의 뒷면을 벽지로 가린다
        // 복도 북·남 벽 끝(x=20.15)이 노란 방 서쪽 벽 안쪽 면과 같은 자리라 타일이 세로 줄로 비쳤다. 벽지판으로 덮는다.
        Cover(covers, "West", false, 20.18f, -114.4f, -98.7f, 3f, -107.7f, -105.7f, 2.6f, wallMat, LYellow);

        // 걸레받이: 벽지와 카펫이 만나는 선
        var trimMat = Lit("GB_Yellow_Trim", m => Plain(m, new Color(0.3f, 0.22f, 0.12f), 0.3f));
        var trim = Group(gen, "Baseboard");
        Cover(trim, "N", true, -98.72f, 20.15f, 36.15f, 0.1f, 20.15f, 20.15f, 0.1f, trimMat, LYellow);
        Cover(trim, "E", false, 36.13f, -114.4f, -98.7f, 0.1f, -114.4f, -114.4f, 0.1f, trimMat, LYellow);
        Cover(trim, "S", true, -114.35f, 20.15f, 36.15f, 0.1f, 27.15f, 29.15f, 0.1f, trimMat, LYellow);
        Cover(trim, "W", false, 20.22f, -114.4f, -98.7f, 0.1f, -107.7f, -105.7f, 0.1f, trimMat, LYellow);

        // 격자 천장등 9개: 누런 형광 패널(3000K 근처)
        var lights = Group(gen, "Lights");
        var warm = new Color(1f, 0.9f, 0.6f);
        foreach (float x in new[] { 23.05f, 28.15f, 33.25f })
            foreach (float z in new[] { -101.45f, -106.55f, -111.65f })
                Fluorescent(lights, $"Panel_{x:F0}_{-z:F0}", new Vector3(x, 3f, z), false, 1.2f, 0.6f, warm, 120000f, 1500f, Housing(), "GB_Yellow_PanelGlow", LYellow);
        // 바닥 반사광 흉내: 그림자 없는 점광을 바닥 아래에 두면 바닥 윗면은 등지고 천장과 벽 아래쪽만 고르게 밝힌다.
        foreach (float x in new[] { 24.15f, 32.15f })
            foreach (float z in new[] { -102.6f, -110.6f })
                MakeLight(lights, "BounceFill", LightType.Point, new Vector3(x, -3f, z), Vector3.zero, new Color(1f, 0.86f, 0.52f), 120000f, 14f, LYellow, false, false);
        // 안개를 옅게 깔아 공기가 탁해 보이게
        var profile = RoomProfile("Yellow", 7.9f, 0.03f, 0.1f, new Color(1f, 0.86f, 0.5f), 26f, new Color(0.05f, 0.04f, 0.015f), 5f, 2f);
        LocalVolume(gen, "LocalVolume", new Vector3(28.15f, 1.5f, -106.55f), new Vector3(16f, 3f, 15.7f), 1f, 130f, profile); // 천사 방(125)보다 높다: 노란 방 안에서는 노란 방 값이 이긴다
    }

    // ---------------------------------------------------------------- ④ 천사 방 (A-3, B-4)
    static void ApplyAngel()
    {
        var root = Root("GB_04_Angel");
        DisableOldLights(root);
        SetLayers(root, LAngel);
        AddLayer(root, "Wall_North", LYellow);

        var marble = MarbleSet("GB_Marble", new Color(0.2f, 0.21f, 0.25f), new Color(0.6f, 0.63f, 0.7f), 0.45f);
        var stone = MarbleSet("GB_Stone", new Color(0.44f, 0.46f, 0.5f), new Color(0.6f, 0.62f, 0.66f), 0.18f);
        var floorMat = Lit("GB_Angel_MarbleFloor", m => Textured(m, marble, 4f, Color.white, 0.5f));
        var wallMat = Lit("GB_Angel_Stone", m => { Textured(m, stone, 6f, Color.white, 0.8f); m.SetFloat("_SmoothnessRemapMax", 0.45f); });
        var ceilMat = Lit("GB_Angel_Ceiling", m => Plain(m, new Color(0.32f, 0.33f, 0.36f), 0.2f));
        SetStructureMaterials(root.transform, floorMat, wallMat, ceilMat);
        SetStructureMaterials(root.transform.Find("Passage_To_SameIntro"), floorMat, wallMat, ceilMat);

        var gen = Fresh(root.transform, GenName);
        var lights = Group(gen, "Lights");
        var angel = root.transform.Find("Placeholder_Props/Angel").position; // (24.15, 0, -138.7)

        // 주인공: 천장에서 천사에게 떨어지는 스포트라이트 하나. 볼류메트릭으로 빛줄기가 보인다.
        var spotPos = new Vector3(angel.x + 0.6f, 11.85f, angel.z + 3.2f);
        var spot = MakeLight(lights, "AngelSpot", LightType.Spot, spotPos, Vector3.zero, new Color(0.86f, 0.93f, 1f), 0f, 22f, LAngel, true);
        spot.transform.LookAt(angel + Vector3.up * 2.4f);
        spot.spotAngle = 34f; spot.innerSpotAngle = 20f;
        var shd = spot.GetComponent<HDAdditionalLightData>();
        shd.SetIntensity(6e5f, LightUnit.Candela);
        shd.volumetricDimmer = 1.6f;
        // 조명 기구(천장의 발광 원반)
        var lampMat = Lit("GB_Angel_LampGlow", m => Emissive(m, new Color(0.86f, 0.93f, 1f), 6000f));
        Prim(PrimitiveType.Cylinder, lights, "AngelSpot_Lamp", new Vector3(spotPos.x, 11.97f, spotPos.z), new Vector3(0.7f, 0.03f, 0.7f), lampMat, LAngel);

        // 벽 윤곽만 겨우 보이는 차가운 잔광
        foreach (float z in new[] { -120.5f, -129f, -137.5f })
            MakeLight(lights, "DimFill", LightType.Point, new Vector3(28.15f, 5.5f, z), Vector3.zero, new Color(0.45f, 0.56f, 0.9f), 200000f, 30f, LAngel, false, false);
        // 노란 방의 바닥·천장·벽 끝이 북쪽 벽을 0.3m 뚫고 나와 노란 조명을 받는다. 어두운 방에서 선으로 보여서 돌 띠로 덮는다(콜라이더 없음).
        var caps = Group(gen, "NorthWallTrim");
        Box(caps, "Top", new Vector3(28.15f, 3.15f, -114.86f), new Vector3(17f, 0.38f, 0.36f), wallMat, LAngel);
        Box(caps, "West", new Vector3(20f, 1.67f, -114.86f), new Vector3(0.38f, 3.34f, 0.36f), wallMat, LAngel);
        Box(caps, "East", new Vector3(36.3f, 1.67f, -114.86f), new Vector3(0.38f, 3.34f, 0.36f), wallMat, LAngel);
        // 입구: 노란 방에서 흘러 들어온 빛
        var spill = MakeLight(lights, "EntrySpill", LightType.Spot, new Vector3(28.15f, 2.3f, -113.9f), new Vector3(28f, 180f, 0f), new Color(1f, 0.8f, 0.46f), 0f, 12f, LAngel, false, false);
        spill.spotAngle = 80f; spill.innerSpotAngle = 30f;
        spill.GetComponent<HDAdditionalLightData>().SetIntensity(14000f, LightUnit.Candela);
        // 출구: 어둠 속에서도 문틀이 보이게(기획 원칙 "출구는 보인다")
        var exitGlow = Lit("GB_Angel_ExitGlow", m => Emissive(m, new Color(0.8f, 0.9f, 1f), 500f));
        Box(lights, "ExitMarker", new Vector3(28.15f, 4.45f, -142.68f), new Vector3(3f, 0.08f, 0.04f), exitGlow, LAngel);
        MakeLight(lights, "ExitWash", LightType.Point, new Vector3(28.15f, 3.6f, -141.6f), Vector3.zero, new Color(0.7f, 0.82f, 1f), 12000f, 7f, LAngel, false, false);
        MakeLight(lights, "PassageLight", LightType.Point, new Vector3(28.15f, 3.6f, -143.85f), Vector3.zero, new Color(0.7f, 0.82f, 1f), 5000f, 4f, LAngel, false, false);

        // B-4 메모: 반사 배율을 1로 올리면 화면 반사가 닿지 않는 바닥에 분홍 하늘이 비친다(방별 하늘 반사만 끄는 설정이 없다).
        // 반사 프로브(실시간, 검은 큐브맵)로 덮어 보았지만 적용되지 않아, 반사 배율을 낮춰 어두운 바닥을 유지한다.
        // 어둡게 하되 노출 차이는 작게(출구 밖 햇빛이 화면을 덮지 않게): 조도를 낮춰서 어둡게 만든다.
        var profile = RoomProfile("Angel", 7.8f, 0f, 0.04f, new Color(0.75f, 0.83f, 1f), 28f, Color.black, -10f, 0f, 0.45f);
        LocalVolume(gen, "LocalVolume", new Vector3(28.15f, 6f, -128.6f), new Vector3(20f, 12f, 27.8f), 3f, 125f, profile); // 출구 통로를 지나며 3m에 걸쳐 바깥 빛으로 바뀐다
    }

    // ---------------------------------------------------------------- 물과 하늘 (D-1, D-3)
    static void ApplyWaterAndSky()
    {
        // D-1: 더 맑은 하늘색 물. 흡수를 줄여 바닥 타일이 비치게 하고, 물결 무늬(caustics)는 조금 약하게.
        foreach (var ws in Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
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
        UnityEditor.EditorUtility.SetDirty(profile);
        Root("HDRP_GlobalVolume").GetComponent<Volume>().sharedProfile = profile;
    }
}