using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-18 가라앉은 집들의 도시(사용자 요청): 컴퓨터 속 → 도시 → 천사 방.
// 컴퓨터 화면 속으로 들어가면, 마른 수영장 타일로 지은 거대한 도시(바닥 60×60m, 높이 80m)다.
// 옅은 하늘색 타일 집들이 삐뚤게 쌓여 탑이 되고, 위는 격자 유리 채광창이다. 처음엔 물이 한 방울도 없다.
// 이상한 점(§4-1): 바닥층 집 하나의 창 안만 천장까지 물이 차 있고, 그 물속에서 검은 인외가 이쪽을 내려다본다(2026-11-19, 예전엔 플라밍고 튜브).
// 찾으면 배수구에서 물이 차올라(FloodRise) 도시 전체가 물 밑이 된다. 다 차고 그 집을 안 볼 때 집 문이 열리고,
// 문 안의 빛으로 들어가면 천사 방(하늘)으로 옮겨진다. 다른 게임의 맵은 따라 하지 않고 분위기와 장치만 빌렸다.
// 모두 그레이박스 더미다. 나중에 VARCO 3D 모델로 바꾼다.
public static partial class GreyboxGraphicsPass
{
    const uint LCity = 1u << 8;
    const float CtX0 = 100f, CtX1 = 160f, CtZ0 = -200f, CtZ1 = -140f, CtH = 80f, CtCx = 130f, CtCz = -170f;
    static readonly Vector3 CityArrivalPos = new Vector3(CtCx, 0f, CtZ1 + 0.15f); // 북쪽 벽(두께 가운데). 컴퓨터 화면이 여기로 이어진다
    // 물 찬 집(이상한 점 + 출구): 앞면 z, 크기
    const float FhX = 130f, FhFront = -164.5f, FhW = 8f, FhD = 7f, FhH = 5f, FhDoorW = 1.6f, FhDoorH = 2.4f;
    const float FloodTop = 70f, FloodTime = 28f; // 2026-11-23 사용자 요청 "조금 더 빠르게": 40 → 28초(문 열림도 같이 당겨진다)

    static Material ctDark, ctLitWin, ctTrim;

    static void BuildSunkenCity()
    {
        NewRoomRoot("GB_06_SunkenCity", out var gen);
        float K = Mathf.Pow(2f, AngelEV - 10f);

        // ---- 재질
        var tiles = TileSet("GB_City_Tile", 8, new Color(0.84f, 0.94f, 0.98f), new Color(0.58f, 0.7f, 0.76f), new Color(0.45f, 0.58f, 0.55f), 0.08f, 57);
        var wallMat = Lit("GB_City_Wall", m => Textured(m, tiles, 2.4f, new Color(0.78f, 0.9f, 0.98f), 0.6f));
        var floorMat = Lit("GB_City_Floor", m => Textured(m, tiles, 1.6f, new Color(0.92f, 0.97f, 1f), 0.6f));
        var houseMats = new[]
        {
            Lit("GB_City_HouseBlue", m => Textured(m, tiles, 2f, new Color(0.72f, 0.88f, 1f), 0.5f)),
            Lit("GB_City_HouseWhite", m => Textured(m, tiles, 2f, new Color(1f, 1f, 1f), 0.5f)),
            Lit("GB_City_HouseMint", m => Textured(m, tiles, 2f, new Color(0.76f, 0.98f, 0.9f), 0.5f)),
        };
        var roofMats = new[]
        {
            Lit("GB_City_RoofBlue", m => Plain(m, new Color(0.36f, 0.62f, 0.8f), 0.35f)),
            Lit("GB_City_RoofWhite", m => Plain(m, new Color(0.93f, 0.94f, 0.92f), 0.3f)),
            Lit("GB_City_RoofTeal", m => Plain(m, new Color(0.3f, 0.6f, 0.62f), 0.35f)),
        };
        ctDark = Lit("GB_City_WindowDark", m => Plain(m, new Color(0.08f, 0.16f, 0.24f), 0.92f));
        ctLitWin = Lit("GB_City_WindowLit", m => Emissive(m, new Color(1f, 0.93f, 0.78f), 180f * K));
        ctTrim = Lit("GB_City_Trim", m => Plain(m, new Color(0.96f, 0.97f, 0.97f), 0.45f));
        var rail = Lit("GB_City_Rail", m => Plain(m, new Color(0.93f, 0.95f, 0.96f), 0.7f, 0.6f));
        var red = Lit("GB_City_RingRed", m => Plain(m, new Color(0.9f, 0.22f, 0.18f), 0.35f));
        var drain = Lit("GB_City_Drain", m => Plain(m, new Color(0.28f, 0.32f, 0.34f), 0.5f, 0.5f));
        var skyTop = Lit("GB_City_Skylight", m => Emissive(m, new Color(0.62f, 0.84f, 1f), 1300f * K));
        var door = Lit("GB_City_Door", m => Plain(m, new Color(0.2f, 0.36f, 0.55f), 0.4f));
        var glow = Lit("GB_City_DoorLight", m => Emissive(m, new Color(0.8f, 0.95f, 1f), 2600f * K));
        var waterIn = Lit("GB_City_WindowWaterBack", m => Emissive(m, new Color(0.25f, 0.7f, 0.82f), 160f * K));
        var waterFront = Transparent("GB_City_WindowWater", new Color(0.35f, 0.78f, 0.9f, 0.45f), 0.97f, 40f * K);
        var water = Transparent("GB_City_Water", new Color(0.3f, 0.7f, 0.8f, 0.55f), 0.97f, 20f * K);

        // ---- 껍데기: 타일 벽 네 개(80m) + 바닥 + 격자 유리 채광창
        var shell = Group(gen, "Shell");
        float w = CtX1 - CtX0, d = CtZ1 - CtZ0;
        Box(shell, "Floor", new Vector3(CtCx, -0.15f, CtCz), new Vector3(w + 0.6f, 0.3f, d + 0.6f), floorMat, LCity, true);
        Box(shell, "North", new Vector3(CtCx, CtH / 2, CtZ1 + 0.15f), new Vector3(w + 0.6f, CtH, 0.3f), wallMat, LCity, true);
        Box(shell, "South", new Vector3(CtCx, CtH / 2, CtZ0 - 0.15f), new Vector3(w + 0.6f, CtH, 0.3f), wallMat, LCity, true);
        Box(shell, "West", new Vector3(CtX0 - 0.15f, CtH / 2, CtCz), new Vector3(0.3f, CtH, d), wallMat, LCity, true);
        Box(shell, "East", new Vector3(CtX1 + 0.15f, CtH / 2, CtCz), new Vector3(0.3f, CtH, d), wallMat, LCity, true);
        Box(shell, "Skylight", new Vector3(CtCx, CtH + 0.6f, CtCz), new Vector3(w + 0.6f, 0.3f, d + 0.6f), skyTop, LCity);
        var grid = Group(shell, "SkylightGrid");
        for (float x = CtX0; x <= CtX1 + 0.01f; x += 7.5f)
            Box(grid, "BeamX", new Vector3(x, CtH, CtCz), new Vector3(0.5f, 0.9f, d), ctTrim, LCity);
        for (float z = CtZ0; z <= CtZ1 + 0.01f; z += 7.5f)
            Box(grid, "BeamZ", new Vector3(CtCx, CtH - 0.05f, z), new Vector3(w, 0.8f, 0.5f), ctTrim, LCity);
        // 벽 위아래 띠: 수영장 가장자리 같은 흰 타일 줄
        foreach (var y in new[] { 0.15f, 12f, 30f, 55f })
        {
            Box(shell, "BandN", new Vector3(CtCx, y, CtZ1 - 0.05f), new Vector3(w, 0.3f, 0.1f), ctTrim, LCity);
            Box(shell, "BandS", new Vector3(CtCx, y, CtZ0 + 0.05f), new Vector3(w, 0.3f, 0.1f), ctTrim, LCity);
            Box(shell, "BandW", new Vector3(CtX0 + 0.05f, y, CtCz), new Vector3(0.1f, 0.3f, d), ctTrim, LCity);
            Box(shell, "BandE", new Vector3(CtX1 - 0.05f, y, CtCz), new Vector3(0.1f, 0.3f, d), ctTrim, LCity);
        }

        // ---- 쌓인 집 탑들. 가운데 길(도착 자리 → 물 찬 집, x 124~136)은 비워 둔다.
        var towers = Group(gen, "Towers");
        int ti = 0;
        void Tower(float x, float z, int count, int seed)
        {
            var rng = new System.Random(seed);
            var t = Group(towers, $"Tower_{ti++:00}");
            StackHouses(t, rng, new Vector3(x, 0f, z), 0f, count, houseMats, roofMats, (float)(rng.NextDouble() * 360f), true);
        }
        Tower(110f, -149f, 7, 11);
        Tower(150f, -150f, 9, 12);
        Tower(108f, -178f, 11, 13);
        Tower(146f, -181f, 6, 14);
        Tower(118f, -193f, 5, 15);
        Tower(140f, -194f, 8, 16);
        Tower(111f, -164f, 4, 17);
        Tower(149f, -165f, 3, 18);

        // ---- 물 찬 집: 출구 문 + 이상한 점(창 안만 물이 가득). 그 위로 집이 11채 더 쌓인 가장 높은 탑이다.
        var fh = Group(gen, "FloodedHouse");
        float hx0 = FhX - FhW / 2, hx1 = FhX + FhW / 2, back = FhFront - FhD, dx0 = FhX - FhDoorW / 2, dx1 = FhX + FhDoorW / 2;
        float tunnelBack = FhFront - 4f, midZ = (FhFront + back) / 2;
        var fhMat = houseMats[0];
        Box(fh, "Left", new Vector3((hx0 + dx0) / 2, FhH / 2, midZ), new Vector3(dx0 - hx0, FhH, FhD), fhMat, LCity, true);
        Box(fh, "Right", new Vector3((dx1 + hx1) / 2, FhH / 2, midZ), new Vector3(hx1 - dx1, FhH, FhD), fhMat, LCity, true);
        Box(fh, "Over", new Vector3(FhX, (FhDoorH + FhH) / 2, midZ), new Vector3(FhDoorW, FhH - FhDoorH, FhD), fhMat, LCity, true);
        Box(fh, "Back", new Vector3(FhX, FhDoorH / 2, (tunnelBack + back) / 2), new Vector3(FhDoorW, FhDoorH, tunnelBack - back), fhMat, LCity, true);
        // 문 안: 빛으로 가득한 좁은 방
        float tz0 = FhFront - 0.3f, tzc = (tz0 + tunnelBack) / 2, tl = tz0 - tunnelBack;
        Box(fh, "In_Floor", new Vector3(FhX, 0.01f, tzc), new Vector3(FhDoorW, 0.02f, tl), glow, LCity, true);
        Box(fh, "In_Back", new Vector3(FhX, FhDoorH / 2, tunnelBack + 0.01f), new Vector3(FhDoorW, FhDoorH, 0.02f), glow, LCity);
        Box(fh, "In_Top", new Vector3(FhX, FhDoorH - 0.01f, tzc), new Vector3(FhDoorW, 0.02f, tl), glow, LCity);
        Box(fh, "In_L", new Vector3(dx0 + 0.01f, FhDoorH / 2, tzc), new Vector3(0.02f, FhDoorH, tl), glow, LCity);
        Box(fh, "In_R", new Vector3(dx1 - 0.01f, FhDoorH / 2, tzc), new Vector3(0.02f, FhDoorH, tl), glow, LCity);
        // 문틀과 다른 창들
        Box(fh, "DoorFrame_L", new Vector3(dx0 - 0.08f, FhDoorH / 2 + 0.08f, FhFront + 0.04f), new Vector3(0.16f, FhDoorH + 0.16f, 0.08f), ctTrim, LCity);
        Box(fh, "DoorFrame_R", new Vector3(dx1 + 0.08f, FhDoorH / 2 + 0.08f, FhFront + 0.04f), new Vector3(0.16f, FhDoorH + 0.16f, 0.08f), ctTrim, LCity);
        Box(fh, "DoorFrame_T", new Vector3(FhX, FhDoorH + 0.08f, FhFront + 0.04f), new Vector3(FhDoorW + 0.32f, 0.16f, 0.08f), ctTrim, LCity);
        Box(fh, "Window_R", new Vector3(FhX + 2.4f, 2.4f, FhFront + 0.04f), new Vector3(1.4f, 1.3f, 0.08f), ctDark, LCity);
        Box(fh, "Window_Up_L", new Vector3(FhX - 2.4f, 4.1f, FhFront + 0.04f), new Vector3(1.1f, 0.7f, 0.08f), ctDark, LCity);
        Box(fh, "Window_Up_R", new Vector3(FhX + 2.4f, 4.1f, FhFront + 0.04f), new Vector3(1.1f, 0.7f, 0.08f), ctDark, LCity);
        // 그 위로 쌓인 집들(가장 높은 탑)
        StackHouses(Group(fh, "Stack"), new System.Random(10), new Vector3(FhX, FhH, midZ), 0f, 11, houseMats, roofMats, 0f, false, 1);

        // 출구 벽 = 집 문(찾기 규칙). 다 차고 시선을 떼면 열린다.
        var gate = FindGate(gen, "FindGate_City", 9, "CityFloodedWindow",
            new Bounds(new Vector3(FhX, FhDoorH / 2, FhFront - 0.15f), new Vector3(FhDoorW, FhDoorH, 0.3f)), "GB_06_SunkenCity/GFX_Generated/FloodedHouse/Over");
        foreach (var r in gate.wall.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = door;
        Box(gate.wall.transform, "Knob", new Vector3(dx1 - 0.25f, 1.05f, FhFront + 0.04f), new Vector3(0.08f, 0.08f, 0.08f), rail, LCity);

        // ---- 이상한 점: 튀어나온 창 하나 안만 물이 천장까지 차 있고, 플라밍고 튜브가 그 천장에 눌려 떠 있다.
        var odd = new GameObject("Oddity_FloodedWindow").transform;
        odd.SetParent(gen, true);
        odd.position = new Vector3(FhX - 2.4f, 2.3f, FhFront);
        const float bw = 1.8f, bh = 1.5f, bd = 0.6f;
        Vector3 O(float x, float y, float z) => odd.position + new Vector3(x, y, z);
        Box(odd, "Back", O(0f, 0f, 0.02f), new Vector3(bw, bh, 0.04f), waterIn, LCity);
        Box(odd, "Frame_T", O(0f, bh / 2 + 0.06f, bd / 2), new Vector3(bw + 0.24f, 0.12f, bd), ctTrim, LCity);
        Box(odd, "Frame_B", O(0f, -bh / 2 - 0.06f, bd / 2), new Vector3(bw + 0.24f, 0.12f, bd), ctTrim, LCity);
        Box(odd, "Frame_L", O(-bw / 2 - 0.06f, 0f, bd / 2), new Vector3(0.12f, bh, bd), ctTrim, LCity);
        Box(odd, "Frame_R", O(bw / 2 + 0.06f, 0f, bd / 2), new Vector3(0.12f, bh, bd), ctTrim, LCity);
        Box(odd, "Sill", O(0f, -bh / 2 - 0.16f, bd / 2 + 0.08f), new Vector3(bw + 0.4f, 0.08f, bd + 0.16f), ctTrim, LCity);
        // 2026-11-19 사용자 요청: 플라밍고 튜브 대신 볼록렌즈를 들여다보던 검은 인외(VARCO PeepholeFigure)가 물 찬 창 안에서 이쪽을 내려다본다.
        // 집(Left)은 속이 찬 상자라 인외는 벽 안에 서 있고, 앞으로 10도 숙인 윗몸만 창 안 물속으로 나온다(아랫몸은 벽에 가려진다).
        // 프리팹: 키 2.4m(숙인 자세), 얼굴은 발보다 0.42m 앞·2.26m 높이, 가장 앞은 0.58m. 숙이면 가장 앞이 발보다 약 0.95m 앞.
        // 첫 시도(통째로 벽 안에 세우기)는 고개를 숙여 정수리만 보였고 모델이 밝은 회색이었다 →
        // 윗몸(어깨 위)만 떼어 뒤로 젖혀 얼굴이 창 밖을 보게 하고, 거의 검은 재질로 밝은 물 앞의 실루엣이 되게 한다.
        var bust = PeepFigureBust(out var peepNormal);
        if (bust != null)
        {
            var figMat = Lit("GB_City_WindowFigure", m =>
            {
                Plain(m, new Color(0.03f, 0.03f, 0.035f), 0.35f);
                if (peepNormal != null) { m.SetTexture("_NormalMap", peepNormal); m.EnableKeyword("_NORMALMAP"); }
            });
            // 2026-11-19 사용자 요청 "창틀을 뚫고 나온 건": 창 안에 갇혀 있던 자세(-28도 젖힘) → 앞으로 숙여(+figPitch)
            // 두 손으로 창턱(유리 밖)을 짚고 머리·어깨가 물 밖으로 쑥 나와 아래의 플레이어를 내려다본다. 물 면이 몸을 가로지른다.
            // 이 모델은 머리가 손보다 1m쯤 앞에 있어서, 손을 창턱에 두면 머리는 유리 밖 약 1m에 온다.
            // 2026-11-19 "조금만 더 올리자": 손은 창턱에 둔 채 덜 숙여(20 → 5도) 등이 창 위쪽까지 올라오게 한다.
            const float figScale = 1.25f, figPitch = 5f;
            var rot = Quaternion.Euler(figPitch, 0f, 0f);
            var bv = bust.vertices; // 메쉬는 몸 전체 꼭짓점을 그대로 갖고 있어서 삼각형이 쓰는 것만 센다
            float minY = float.PositiveInfinity, minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            foreach (int i in bust.triangles) { var p = rot * (bv[i] * figScale); minY = Mathf.Min(minY, p.y); minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); }
            float handZ = float.NegativeInfinity; // 손(가장 낮은 12cm)의 가장 앞
            foreach (int i in bust.triangles) { var p = rot * (bv[i] * figScale); if (p.y < minY + 0.12f) handZ = Mathf.Max(handZ, p.z); }
            float sillTop = -bh / 2 - 0.12f;
            var fig = new GameObject("Window_Figure").transform;
            fig.SetParent(odd, false);
            fig.localScale = Vector3.one * figScale;
            fig.rotation = rot;
            fig.position = O(-0.1f - (minX + maxX) / 2, sillTop + 0.01f - minY, bd + 0.1f - handZ);
            // 머리만 목을 축으로 플레이어를 쳐다보게 한다(LookAtPlayer). 자르지 않은 스킨 메쉬라 목이 휘기만 하고 몸에 구멍이 나지 않는다.
            var skinned = PeepFigureSkinnedBust(out _);
            if (skinned != null)
            {
                var pivot = new GameObject("HeadPivot").transform; // 머리 뼈
                pivot.SetParent(fig, false);
                pivot.localPosition = PeepNeck;
                var body = new GameObject("Body");
                body.transform.SetParent(fig, false);
                var smr = body.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = skinned; smr.sharedMaterial = figMat; smr.renderingLayerMask = LCity;
                smr.bones = new[] { fig, pivot }; smr.rootBone = fig;
                smr.localBounds = new Bounds(skinned.bounds.center, skinned.bounds.size + Vector3.one * 0.6f); // 머리가 돌아도 잘리지 않게
                smr.shadowCastingMode = ShadowCastingMode.Off;
                var look = pivot.gameObject.AddComponent<LookAtPlayer>();
                look.faceAxis = Vector3.down; look.maxAngle = 60f; look.range = 18f; // 스킨이라 목이 휘기만 해서 60도까지 돌려도 갈라지지 않는다 look.turnSpeed = 1.6f;
            }
            else MeshPart(fig, "Body", bust, figMat, LCity, 1f).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        else
        {
            // 프리팹이 없으면 예전 플라밍고 튜브(창 안 천장에 눌림, 고개는 아래로)
            var pink = Lit("GB_City_Flamingo", m => Plain(m, new Color(1f, 0.55f, 0.68f), 0.5f));
            var beak = Lit("GB_City_FlamingoBeak", m => Plain(m, new Color(0.15f, 0.15f, 0.15f), 0.4f));
            var fl = new GameObject("Flamingo").transform;
            fl.SetParent(odd, true);
            fl.position = O(0.1f, bh / 2 - 0.2f, 0.3f);
            Prim(PrimitiveType.Sphere, fl, "Ring", O(0.1f, bh / 2 - 0.18f, 0.3f), new Vector3(0.95f, 0.26f, 0.42f), pink, LCity);
            Prim(PrimitiveType.Cylinder, fl, "Neck", O(-0.42f, bh / 2 - 0.42f, 0.3f), new Vector3(0.09f, 0.22f, 0.09f), pink, LCity, false, new Vector3(0f, 0f, -20f));
            Prim(PrimitiveType.Sphere, fl, "Head", O(-0.5f, bh / 2 - 0.66f, 0.3f), new Vector3(0.2f, 0.18f, 0.18f), pink, LCity);
            Box(fl, "Beak", O(-0.6f, bh / 2 - 0.74f, 0.3f), new Vector3(0.12f, 0.05f, 0.05f), beak, LCity, false, new Vector3(0f, 0f, 30f));
        }
        // 물 기포 몇 개
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Sphere, odd, "Bubble", O(-0.6f + i * 0.3f, -0.5f + (i % 3) * 0.28f, 0.25f), Vector3.one * (0.05f + (i % 2) * 0.03f), waterFront, LCity);
        var front = Box(odd, "WaterFront", O(0f, 0f, bd - 0.02f), new Vector3(bw, bh, 0.03f), waterFront, LCity);
        var room = new Bounds(new Vector3(CtCx, CtH / 2, CtCz), new Vector3(w, CtH, d));
        var gz = AddGaze(odd.gameObject, "CityFloodedWindow", gate, room);
        // 2026-11-19 사용자 결정: 창이 깜빡이는 모션은 이상해서 뺀다. 인외(창 밖으로 나온 몸 포함)나 창을 보고 있으면 물이 차오른다.
        var figR = odd.Find("Window_Figure/Body")?.GetComponent<Renderer>();
        gz.gazeRenderers = figR != null ? new[] { front.GetComponent<Renderer>(), figR } : new[] { front.GetComponent<Renderer>() };
        gz.maxDistance = 10f; gz.dwellTime = 1.5f; gz.boundsPadding = 0.15f;
        gz.blinkRenderers = new Renderer[0]; gz.blinkCount = 0;
        gz.notifyDelay = FloodTime + 2f; // 물이 다 찬 뒤에 문이 열린다

        // ---- 계단 + 동쪽 벽 산책로(6m) + 다이빙대 + 늘어진 사다리. 난간에는 구명 튜브.
        var walk = Group(gen, "Walkway");
        const float stairX = 158.5f, stairW = 2.5f, rise = 0.2f, run = 0.4f, walkY = 6f;
        const int steps = 30;
        for (int i = 0; i < steps; i++)
        {
            float top = rise * (i + 1);
            Box(walk, "Step", new Vector3(stairX, top / 2, -146f - run * (i + 0.5f)), new Vector3(stairW, top, run), ctTrim, LCity, true);
        }
        float wz0 = -146f - run * steps, wz1 = -190f;
        Box(walk, "Deck", new Vector3(157f, walkY - 0.15f, (wz0 + wz1) / 2), new Vector3(6f, 0.3f, wz0 - wz1), ctTrim, LCity, true);
        for (float z = wz0 - 4f; z > wz1; z -= 8f)
            Box(walk, "Pillar", new Vector3(154.4f, (walkY - 0.3f) / 2, z), new Vector3(0.5f, walkY - 0.3f, 0.5f), ctTrim, LCity, true);
        // 난간: 기둥 + 위 손잡이. 막는 콜라이더는 보이지 않는 판.
        void Railing(Vector3 a, Vector3 b, float baseY)
        {
            Vector3 dir = b - a; float len = dir.magnitude; dir /= len;
            for (float s = 0f; s <= len + 0.01f; s += 2f)
            {
                var p = a + dir * s;
                Box(walk, "Post", new Vector3(p.x, baseY + 0.55f, p.z), new Vector3(0.07f, 1.1f, 0.07f), rail, LCity);
            }
            var mid = (a + b) / 2;
            Box(walk, "Rail", new Vector3(mid.x, baseY + 1.1f, mid.z), new Vector3(Mathf.Abs(dir.x) * len + 0.07f, 0.07f, Mathf.Abs(dir.z) * len + 0.07f), rail, LCity);
            var guard = new GameObject("RailGuard");
            guard.transform.SetParent(walk, true);
            guard.transform.position = new Vector3(mid.x, baseY + 0.6f, mid.z);
            guard.AddComponent<BoxCollider>().size = new Vector3(Mathf.Abs(dir.x) * len + 0.1f, 1.2f, Mathf.Abs(dir.z) * len + 0.1f);
        }
        Railing(new Vector3(154.1f, 0f, wz0), new Vector3(154.1f, 0f, wz1), walkY);
        Railing(new Vector3(154.1f, 0f, wz1 + 0.05f), new Vector3(159.9f, 0f, wz1 + 0.05f), walkY);
        Railing(new Vector3(154.1f, 0f, wz0), new Vector3(157.25f, 0f, wz0), walkY);
        // 계단 바깥쪽 난간(비스듬)
        {
            float zA = -146f, zB = wz0, len = zA - zB, ang = Mathf.Atan2(walkY, len) * Mathf.Rad2Deg;
            Box(walk, "StairRail", new Vector3(stairX - stairW / 2 - 0.05f, walkY / 2 + 1f, (zA + zB) / 2), new Vector3(0.07f, 0.07f, Mathf.Sqrt(len * len + walkY * walkY)), rail, LCity, false, new Vector3(ang, 0f, 0f));
            var g = Box(walk, "StairGuard", new Vector3(stairX - stairW / 2 - 0.05f, walkY / 2 + 0.6f, (zA + zB) / 2), new Vector3(0.1f, 1.2f, Mathf.Sqrt(len * len + walkY * walkY)), rail, LCity, true, new Vector3(ang, 0f, 0f));
            Object.DestroyImmediate(g.GetComponent<MeshRenderer>());
        }
        // 다이빙대: 산책로에서 도시 쪽으로 튀어나왔다. 아래에 물은 없다(처음에는).
        Box(walk, "DivingBoard", new Vector3(152f, walkY + 0.12f, -176f), new Vector3(4.2f, 0.12f, 0.7f), Lit("GB_City_Board", m => Plain(m, new Color(0.35f, 0.62f, 0.85f), 0.4f)), LCity);
        Box(walk, "DivingBoardBase", new Vector3(154.6f, walkY + 0.05f, -176f), new Vector3(0.6f, 0.2f, 0.9f), ctTrim, LCity);
        // 산책로 끝에서 바닥까지 늘어진 수영장 사다리
        PoolLadder(walk, new Vector3(153.85f, 0f, -186f), walkY + 0.9f, rail, 90f);
        PoolLadder(walk, new Vector3(CtX0 + 0.25f, 0f, -158f), 9f, rail, 90f);   // 서쪽 벽: 아무 데도 안 가는 사다리
        PoolLadder(walk, new Vector3(145.2f, 0f, -150f), 4.2f, rail, -90f);

        // ---- 구명 튜브(물이 차면 떠오른다)
        var floaters = new System.Collections.Generic.List<Transform>();
        var rings = Group(gen, "Rings");
        void Ring(Vector3 c, Vector3 euler)
        {
            var g = new GameObject("LifeRing").transform;
            g.SetParent(rings, true);
            g.SetPositionAndRotation(c, Quaternion.Euler(euler));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                seg.name = "Seg"; seg.transform.SetParent(g, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.32f, Mathf.Sin(a) * 0.32f, 0f);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                seg.transform.localScale = new Vector3(0.16f, 0.27f, 0.16f);
                var r = seg.GetComponent<MeshRenderer>(); r.sharedMaterial = i % 2 == 0 ? red : ctTrim; r.renderingLayerMask = LCity;
            }
            floaters.Add(g);
        }
        for (float z = wz0 - 2f; z > wz1 + 1f; z -= 6f) Ring(new Vector3(153.95f, walkY + 0.7f, z), new Vector3(0f, 90f, 0f));
        Ring(new Vector3(124.6f, 0.45f, -152f), new Vector3(-75f, 20f, 0f));
        Ring(new Vector3(135.2f, 0.06f, -160f), new Vector3(-90f, 0f, 0f));
        Ring(new Vector3(116f, 0.06f, -156.5f), new Vector3(-90f, 0f, 0f));
        Ring(new Vector3(142.5f, 0.06f, -171f), new Vector3(-90f, 0f, 0f));
        Ring(new Vector3(127f, 0.06f, -178f), new Vector3(-90f, 0f, 0f));
        // 2026-11-20 사용자 요청: 인트로 수영장 플라밍고 튜브를 길가 바닥에 하나 둔다. 물이 차면 구명 튜브처럼 떠오른다.
        // 모델은 콜라이더가 없어 시선 판정을 막지 않는다. 피벗이 바닥이라 floatDepth(0.15m)만큼 물에 잠긴다.
        var flSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/prop_flamingo.prefab");
        if (flSrc != null)
        {
            var fl = (GameObject)PrefabUtility.InstantiatePrefab(flSrc, rings);
            fl.name = "Prop_Flamingo_City";
            fl.transform.SetPositionAndRotation(new Vector3(121f, 0f, -160f), Quaternion.Euler(-90f, 35f, 0f));
            foreach (var r in fl.GetComponentsInChildren<Renderer>()) r.renderingLayerMask = LCity;
            floaters.Add(fl.transform);
        }

        // ---- 감시탑 의자, 배수구
        var props = Group(gen, "Props");
        propLayer = LCity;
        var chair = PropGroup(props, "LifeguardChair", new Vector3(138f, 0f, -153f), 215f, new Vector3(1.4f, 2.4f, 1.4f));
        foreach (var (lx, lz) in new[] { (-0.55f, -0.55f), (0.55f, -0.55f), (-0.55f, 0.55f), (0.55f, 0.55f) })
            PBox(chair, "Leg", new Vector3(lx, 1.2f, lz), new Vector3(0.08f, 2.4f, 0.08f), ctTrim);
        PBox(chair, "Seat", new Vector3(0f, 2.4f, 0f), new Vector3(1.2f, 0.08f, 1f), ctTrim);
        PBox(chair, "BackRest", new Vector3(0f, 2.95f, -0.5f), new Vector3(1.2f, 1f, 0.08f), ctTrim);
        for (int i = 0; i < 5; i++) PBox(chair, "Rung", new Vector3(0f, 0.4f + i * 0.4f, 0.55f), new Vector3(1.1f, 0.05f, 0.05f), ctTrim);
        foreach (var p in new[] { new Vector3(126f, 0f, -150f), new Vector3(134f, 0f, -157f), new Vector3(121f, 0f, -170f), new Vector3(139f, 0f, -174f), new Vector3(130f, 0f, -186f), new Vector3(116f, 0f, -186f), new Vector3(150f, 0f, -158f) })
        {
            Box(props, "Drain", p + new Vector3(0f, 0.01f, 0f), new Vector3(0.7f, 0.02f, 0.7f), drain, LCity);
            for (int i = 0; i < 3; i++) Box(props, "DrainSlot", p + new Vector3(-0.2f + i * 0.2f, 0.022f, 0f), new Vector3(0.05f, 0.005f, 0.6f), ctDark, LCity);
        }

        // ---- 빛: 채광창의 해(이 방만, 그림자 없음) + 위에서 내려오는 빛기둥(스포트, 그림자·볼륨) + 바닥 반사광
        var lights = Group(gen, "Lights");
        var sunGo = new GameObject("CitySun");
        sunGo.transform.SetParent(lights, true);
        sunGo.transform.SetPositionAndRotation(new Vector3(CtCx, 78f, CtCz), Quaternion.Euler(62f, 160f, 0f));
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional; sun.color = new Color(1f, 0.98f, 0.92f); sun.shadows = LightShadows.None;
        var sunHd = sunGo.AddComponent<HDAdditionalLightData>();
        sunHd.SetIntensity(2600f * K, LightUnit.Lux);
        sunHd.affectsVolumetric = false;
        sunHd.lightlayersMask = (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)LCity;
        foreach (var (p, e) in new[] { (new Vector3(122f, 79f, -160f), new Vector3(78f, 160f, 0f)), (new Vector3(140f, 79f, -175f), new Vector3(74f, 200f, 0f)), (new Vector3(128f, 79f, -188f), new Vector3(80f, 20f, 0f)) })
        {
            var sp = MakeLight(lights, "Shaft", LightType.Spot, p, e, new Color(1f, 0.97f, 0.88f), 9000000f * K, 120f, LCity, true, true);
            sp.spotAngle = 22f; sp.innerSpotAngle = 10f;
        }
        foreach (var p in new[] { new Vector3(CtCx, 1.5f, -150f), new Vector3(CtCx, 1.5f, -172f), new Vector3(115f, 2f, -185f), new Vector3(150f, 2f, -168f) })
            MakeLight(lights, "FloorBounce", LightType.Point, p, Vector3.zero, new Color(0.8f, 0.95f, 1f), 500000f * K, 30f, LCity, false, false);

        var profile = RoomProfile("City", AngelEV, 0.45f, 0.2f, new Color(0.92f, 0.98f, 1f), 160f, new Color(0.78f, 0.9f, 0.96f), 0f, 0f, 0.6f);
        if (profile.TryGet<Fog>(out var cityFog))
        {
            // 80m 위까지 옅은 안개가 고르게 있게(기본값은 50m 위로 옅어진다). 하늘빛(분홍)을 머금으면 보랏빛으로 뿌예져서 끈다.
            cityFog.meanFreePath.Override(120f);
            cityFog.baseHeight.Override(CtH);
            cityFog.maximumHeight.Override(CtH + 20f);
            cityFog.globalLightProbeDimmer.Override(0f);
            EditorUtility.SetDirty(cityFog); EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
        // 빛줄기 판(2026-11-18)은 뺐다: 해 각도로 기울이면 윗부분이 벽을 뚫고 나가 벽에서 빛이 나오는 것처럼 보였다(사용자 피드백).
        LocalVolume(gen, "LocalVolume", room.center, room.size, 2f, 125f, profile);

        // ---- 물 차오름(찾으면 켜진다): 수면 판 + 물속 화면
        var flood = new GameObject("Flood");
        flood.transform.SetParent(gen, true);
        // 2026-11-19: 반투명 판 → HDRP 물. FloodRise는 위치만 올리므로 그대로 쓴다.
        var surf = PoolWater(flood.transform, "WaterSurface", new Vector3(CtCx, -0.3f, CtCz), new Vector2(w, d), LCity,
            new Color(0.62f, 0.94f, 1f), new Color(0.36f, 0.8f, 0.88f), 20f, 3f, 0.8f);
        var underProfile = RoomProfile("CityUnder", AngelEV, 0.45f, 0.2f, new Color(0.3f, 0.75f, 0.82f), 20f, new Color(0.1f, 0.45f, 0.56f), -18f, 0f, 0.75f);
        {
            // 물속: 청록으로 물들이고 살짝 어둡게. 2026-11-18 Play 확인에서 안개만으로는 물속처럼 안 보였다.
            var ca = Persist(underProfile, underProfile.Add<ColorAdjustments>(true));
            ca.colorFilter.Override(new Color(0.55f, 0.88f, 0.92f));
            ca.postExposure.Override(-0.2f);
            ca.saturation.Override(-5f);
            EditorUtility.SetDirty(underProfile);
            AssetDatabase.SaveAssets();
        }
        LocalVolume(flood.transform, "UnderwaterVolume", room.center, room.size, 0f, 140f, underProfile);
        var uv = flood.transform.Find("UnderwaterVolume").GetComponent<Volume>();
        uv.weight = 0f;
        var fr = flood.AddComponent<FloodRise>();
        fr.surface = surf.transform; fr.startY = -0.3f; fr.endY = FloodTop; fr.duration = FloodTime; fr.easePower = 2.5f; // 발목~무릎에 잠깐 머문다(2026-11-23: 3 → 2.5, 처음이 덜 느리게)
        fr.underwater = uv; fr.city = room; fr.floaters = floaters.ToArray();
        EditorUtility.SetDirty(fr); EditorUtility.SetDirty(uv);
        flood.SetActive(false);
        gz.showOnFound = new[] { flood };
        EditorUtility.SetDirty(gz);

        // ---- 문 안의 빛 → 천사 방 동쪽 벽. 천사 방에는 물이 떨어지는 집 문틀이 잠깐 남는다.
        var from = new GameObject("DoorAnchor").transform;
        from.SetParent(gen, true);
        from.SetPositionAndRotation(new Vector3(FhX, 0f, FhFront - 0.15f), Quaternion.LookRotation(Vector3.back));
        var to = new GameObject("AngelArrivalAnchor").transform;
        to.SetParent(gen, true);
        to.SetPositionAndRotation(AngelDoorPos, Quaternion.LookRotation(Vector3.left));
        var arrival = new GameObject("DoorArrival").transform;
        arrival.SetParent(to, false);
        Vector3 Wp(Vector3 l) => to.TransformPoint(l);
        var rotE = to.rotation.eulerAngles;
        const float fz = 0.15f, jb = 0.25f;
        Box(arrival, "Jamb_L", Wp(new Vector3(-(FhDoorW / 2 + jb / 2), (FhDoorH + jb) / 2, fz + 0.1f)), new Vector3(jb, FhDoorH + jb, 0.2f), houseMats[1], LAngel, false, rotE);
        Box(arrival, "Jamb_R", Wp(new Vector3(FhDoorW / 2 + jb / 2, (FhDoorH + jb) / 2, fz + 0.1f)), new Vector3(jb, FhDoorH + jb, 0.2f), houseMats[1], LAngel, false, rotE);
        Box(arrival, "Header", Wp(new Vector3(0f, FhDoorH + jb / 2, fz + 0.1f)), new Vector3(FhDoorW, jb, 0.2f), houseMats[1], LAngel, false, rotE);
        Box(arrival, "Glow", Wp(new Vector3(0f, FhDoorH / 2, fz + 0.01f)), new Vector3(FhDoorW, FhDoorH, 0.02f), glow, LAngel, false, rotE);
        Box(arrival, "Puddle", Wp(new Vector3(0f, 0.012f, fz + 1.3f)), new Vector3(2.4f, 0.01f, 2f), water, LAngel, false, rotE);
        arrival.gameObject.SetActive(false);
        var tzn = gen.gameObject.AddComponent<TeleportZone>();
        tzn.zone = new Bounds(new Vector3(FhX, FhDoorH / 2, (FhFront - 1.6f + tunnelBack) / 2), new Vector3(FhDoorW, FhDoorH, FhFront - 1.6f - tunnelBack));
        tzn.fromAnchor = from; tzn.toAnchor = to; tzn.arrivalObject = arrival.gameObject;
        EditorUtility.SetDirty(tzn);

        var spawn = new GameObject("Spawn_AngelArrival").transform;
        spawn.SetParent(gen, true);
        spawn.SetPositionAndRotation(to.TransformPoint(new Vector3(0f, 0.05f, 2f)), to.rotation);
    }

    /// <summary>집을 count채 삐뚤게 쌓는다. 바닥층(level 0)만 문이 있고 콜라이더가 있다. startLevel로 바닥층을 건너뛸 수 있다.</summary>
    static void StackHouses(Transform parent, System.Random rng, Vector3 basePos, float y0, int count, Material[] houses, Material[] roofs, float yaw, bool groundDoor, int startLevel = 0)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float y = basePos.y + y0, ox = 0f, oz = 0f;
        float leanX = R(-0.5f, 0.5f), leanZ = R(-0.5f, 0.5f);
        for (int i = 0; i < count; i++)
        {
            int level = startLevel + i;
            float hw = R(6f, 9f), hd = R(6f, 9f), hh = R(4.5f, 5.8f);
            if (level > 0) { ox += leanX + R(-0.9f, 0.9f); oz += leanZ + R(-0.9f, 0.9f); yaw += R(-14f, 14f); }
            int mi = rng.Next(houses.Length);
            House(parent, $"House_{level:00}", new Vector3(basePos.x + ox, y, basePos.z + oz), hw, hh, hd, yaw, houses[mi], roofs[rng.Next(roofs.Length)], groundDoor && level == 0, level <= 1, rng);
            y += hh;
        }
    }

    /// <summary>박공지붕 타일 집 하나. pos = 바닥 가운데. 앞(+Z 로컬)에 문, 네 면에 창.</summary>
    static void House(Transform parent, string name, Vector3 pos, float w, float h, float d, float yaw, Material body, Material roof, bool doorOn, bool collider, System.Random rng)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, true);
        g.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        GameObject L(Transform p, string n, Vector3 c, Vector3 s, Material m, bool col = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n; go.transform.SetParent(p, false);
            go.transform.localPosition = c; go.transform.localScale = s;
            if (!col) Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.renderingLayerMask = LCity;
            return go;
        }
        L(g, "Body", new Vector3(0f, h / 2, 0f), new Vector3(w, h, d), body, collider);
        // 박공지붕: 45° 돌린 상자를 납작한 부모 안에 넣어 세모 기둥처럼 보이게(아래 절반은 몸통 안에 묻힌다)
        var rp = new GameObject("RoofPivot").transform;
        rp.SetParent(g, false);
        rp.localPosition = new Vector3(0f, h, 0f); rp.localScale = new Vector3(1f, 0.55f, 1f);
        // 깊이는 몸통보다 살짝 짧게: 길면 묻힌 아래 절반이 앞뒤로 삐져나와 마름모로 보인다(2026-11-18 Play 확인).
        float s = w / Mathf.Sqrt(2f), rh = 0.55f * w / 2f;
        var rf = L(rp, "Roof", Vector3.zero, new Vector3(s, s, d - 0.04f), body);
        rf.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        // 처마: 양쪽 지붕판(앞뒤로 조금 나옴)
        float half = w / 2f + 0.3f, ang = Mathf.Atan2(rh, w / 2f) * Mathf.Rad2Deg, len = half / Mathf.Cos(ang * Mathf.Deg2Rad) + 0.1f;
        L(g, "RoofSlab_L", new Vector3(-half / 2f, h + rh / 2f + 0.02f, 0f), new Vector3(len, 0.16f, d + 0.6f), roof).transform.localRotation = Quaternion.Euler(0f, 0f, ang);
        L(g, "RoofSlab_R", new Vector3(half / 2f, h + rh / 2f + 0.02f, 0f), new Vector3(len, 0.16f, d + 0.6f), roof).transform.localRotation = Quaternion.Euler(0f, 0f, -ang);
        // 창: 네 면, 대부분 어둡고 가끔 하나만 불이 켜져 있다
        void Windows(float faceW, System.Func<float, Vector3> at, Vector3 size)
        {
            int n = Mathf.Max(1, Mathf.FloorToInt(faceW / 2.6f));
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n - 0.5f;
                if (doorOn && Mathf.Abs(u * faceW) < 1f && size.z < size.x) continue; // 문 자리
                var m = rng.NextDouble() < 0.07 ? ctLitWin : ctDark;
                L(g, "Window", at(u * faceW), size, m);
            }
        }
        float wy = h * 0.55f;
        Windows(w, u => new Vector3(u, wy, d / 2 + 0.03f), new Vector3(1f, 1.2f, 0.08f));
        Windows(w, u => new Vector3(u, wy, -d / 2 - 0.03f), new Vector3(1f, 1.2f, 0.08f));
        Windows(d, u => new Vector3(w / 2 + 0.03f, wy, u), new Vector3(0.08f, 1.2f, 1f));
        Windows(d, u => new Vector3(-w / 2 - 0.03f, wy, u), new Vector3(0.08f, 1.2f, 1f));
        if (doorOn)
        {
            L(g, "Door", new Vector3(0f, 1.05f, d / 2 + 0.03f), new Vector3(1.1f, 2.1f, 0.08f), ctDark);
            L(g, "DoorFrame", new Vector3(0f, 1.1f, d / 2 + 0.02f), new Vector3(1.4f, 2.3f, 0.06f), ctTrim);
        }
    }

    /// <summary>수영장 사다리: 손잡이 두 줄 + 발판. pos = 바닥 가운데, yaw = 사다리가 보는 쪽.</summary>
    static void PoolLadder(Transform parent, Vector3 pos, float height, Material m, float yaw)
    {
        var g = new GameObject("PoolLadder").transform;
        g.SetParent(parent, true);
        g.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        GameObject P(string n, Vector3 c, Vector3 s)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = n; go.transform.SetParent(g, false);
            go.transform.localPosition = c; go.transform.localScale = s;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.renderingLayerMask = LCity;
            return go;
        }
        P("Rail_L", new Vector3(-0.3f, height / 2, 0.12f), new Vector3(0.06f, height, 0.06f));
        P("Rail_R", new Vector3(0.3f, height / 2, 0.12f), new Vector3(0.06f, height, 0.06f));
        for (float y = 0.3f; y < height - 0.2f; y += 0.3f) P("Rung", new Vector3(0f, y, 0.12f), new Vector3(0.6f, 0.04f, 0.1f));
    }
}
