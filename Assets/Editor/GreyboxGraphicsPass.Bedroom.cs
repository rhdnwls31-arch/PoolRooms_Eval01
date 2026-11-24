using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 2026-11-18 파란 침실(사용자 선택): 밤의 거실 → 파란 침실 → 천사 방.
// 거실 남쪽(x 38.75~49, z -136~-124.3)의 빈 땅에 짓는다. 거실 남쪽 벽으로 들어온다.
// 나가는 길은 기믹(§6-3d, BlueGimmick.cs): 동쪽 벽의 그림자 → 벽이 복도로 이어짐 → 복도 끝 문의 도어뷰어 → 천사 방으로 순간이동.
// 분위기: 방 전체가 파란색 하나로 칠해져 있고, 바닥은 지그재그 카펫, 가구는 평범한 침실 그대로다. 달빛 한 줄기만 밝다.
// 이상한 점: 침대 이불이 사람 모양으로 불룩하다. 응시하면 두 번 꿈틀(깜빡)하고 꺼지고, 동쪽 벽에 사람 그림자가 생긴다.
// 모두 그레이박스 더미다. 나중에 VARCO 3D 모델로 바꾼다.
public static partial class GreyboxGraphicsPass
{
    const uint LBedroom = 1u << 6;
    const float BdX0 = 38.75f, BdX1 = 49f, BdZ0 = -136f, BdZ1 = -124.3f, BdH = 2.7f;
    const float BdInX = 40.6f;           // 거실 → 침실 구멍 가운데 x (폭 1.8, 높이 2.4). 거실 TV(서쪽 벽) 옆 모서리.
    const float BdOutZ = -133.4f;        // 침대 가운데 z. 예전 침실 → 천사 방 구멍 자리(2026-11-18 기믹 확장으로 없앰)

    // 2026-11-20 사용자 요청: VARCO 침실 가구 텍스처는 옅은 회청색(평균 sRGB 0.25, 0.29, 0.34)이라 방 조명에서 갈색으로 읽혔다.
    // "방 전체가 파란색 하나"에 맞게 재질 색(곱해지는 값, 선형)으로 파랑 쪽으로 밝힌다. 텍스처 무늬는 그대로 남는다.
    static readonly Color BedroomVarcoTint = new Color(0.6f, 1.1f, 4.8f);
    static readonly string[] BedroomVarcoFurniture = { "Bookshelf", "CRTMonitor", "Desk", "LowCabinet", "WallShelf", "WoodChair",
        "Wardrobe", "Nightstand", "StandingMirror", "ToyChest",
        "CurvedLowTable", "Dresser", "RotaryPhone" }; // 2026-11-20 VARCO 추가분(전화기도 흰 모델이라 같이 파랗게) // 2026-11-20 방 채우기 소품(아직 더미, VARCO가 오면 같이 칠한다)

    static void TintBedroomVarco()
    {
        foreach (var n in BedroomVarcoFurniture)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/VARCO/Bedroom/Materials/VAR_Bedroom_{n}.mat");
            if (mat == null) continue;
            // 2026-11-20: 흰 텍스처(낮은 탁자·전화기)에 같은 색을 곱하면 하얗게 빛나 보였다. 텍스처 평균 밝기가
            // 처음 6종 기준(sRGB 약 0.29)보다 밝으면 그 비율만큼 줄여 곱한다. 기준보다 어두운 것은 그대로(처음 6종은 바뀌지 않음).
            float lum = BaseMapLum(mat);
            float k = lum > 0.4f ? BedroomVarcoRefLum / lum : 1f;
            var tint = BedroomVarcoTint * k; tint.a = 1f;
            mat.SetColor("_BaseColor", tint.gamma); // 재질 색은 sRGB로 저장되므로 선형 값을 변환해 넣는다
            EditorUtility.SetDirty(mat);
        }
    }

    const float BedroomVarcoRefLum = 0.29f;

    /// <summary>재질 베이스 텍스처의 평균 밝기(sRGB, 띄엄띄엄 표본). PNG 파일을 직접 읽는다(텍스처가 읽기 가능하지 않아도 된다).</summary>
    static float BaseMapLum(Material mat)
    {
        var tex = mat.GetTexture("_BaseColorMap");
        if (tex == null) return BedroomVarcoRefLum;
        var path = AssetDatabase.GetAssetPath(tex);
        if (!File.Exists(path)) return BedroomVarcoRefLum;
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(path));
        var px = t.GetPixels32();
        double sum = 0; int n = 0;
        for (int i = 0; i < px.Length; i += 37) { var c = px[i]; if (c.a < 128) continue; sum += (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255.0; n++; }
        Object.DestroyImmediate(t);
        return n > 0 ? (float)(sum / n) : BedroomVarcoRefLum;
    }

    static void BuildBedroom()
    {
        NewRoomRoot("GB_03c_Bedroom", out var gen);
        // 노란 벽지 텍스처에 파랑을 곱하면 회갈색이 돼서(2026-11-18 확인) 무늬 없는 파란 벽으로 한다.
        var wall = Lit("GB_Bedroom_Wall", m => Plain(m, new Color(0.36f, 0.46f, 0.92f), 0.18f));
        var carpet = Lit("GB_Bedroom_Carpet", m =>
        {
            Plain(m, Color.white, 0.08f);
            m.SetTexture("_BaseColorMap", ZigzagTexture());
            m.SetFloat("_UVBase", 5f); m.SetFloat("_TexWorldScale", 1f / 1.6f); // 월드 기준 1.6m마다 무늬 한 장
        });
        var ceil = Lit("GB_Bedroom_Ceiling", m => Plain(m, new Color(0.3f, 0.36f, 0.58f), 0.1f));
        var furn = Lit("GB_Bedroom_Furniture", m => Plain(m, new Color(0.55f, 0.63f, 0.9f), 0.25f));
        var deep = Lit("GB_Bedroom_Deep", m => Plain(m, new Color(0.16f, 0.2f, 0.42f), 0.3f));
        var bedding = Lit("GB_Bedroom_Blanket", m => Plain(m, new Color(0.46f, 0.56f, 0.95f), 0.12f));
        var black = Lit("GB_Bedroom_Black", m => Plain(m, new Color(0.02f, 0.02f, 0.04f), 0.55f));
        var paper = Lit("GB_Bedroom_Paper", m => Plain(m, new Color(0.85f, 0.88f, 0.95f), 0.1f));
        var night = Lit("GB_Bedroom_NightGlass", m => Emissive(m, new Color(0.35f, 0.5f, 1f), 600f)); // 달빛이 들어오는 창
        var screenMat = Lit("GB_Bedroom_Screen", m => Emissive(m, new Color(0.4f, 0.6f, 1f), 40f));

        // ---- 껍데기. 북쪽 벽은 거실 남쪽 벽을 같이 쓰고, 침실 쪽에는 파란 덧판을 댄다.
        var shell = Group(gen, "Shell");
        float fx0 = BdX0 - 0.3f, fx1 = BdX1 + 0.3f, fz0 = BdZ0 - 0.3f;
        var fc = new Vector3((fx0 + fx1) / 2, 0f, (fz0 + BdZ1) / 2); var fs = new Vector3(fx1 - fx0, 0.3f, BdZ1 - fz0);
        Box(shell, "Floor", fc + Vector3.down * 0.15f, fs, carpet, LBedroom, true);
        Box(shell, "Ceiling", fc + Vector3.up * (BdH + 0.15f), fs, ceil, LBedroom);
        WallWithHoles(shell, "West", false, BdX0 - 0.15f, 0.3f, fz0, BdZ1, 0f, BdH, new (float, float, float)[0], wall, LBedroom);
        WallWithHoles(shell, "East", false, BdX1 + 0.15f, 0.3f, fz0, BdZ1, 0f, BdH, new[] { (CrZ - CrW / 2, CrZ + CrW / 2, CrH) }, wall, LBedroom);
        WallWithHoles(shell, "South", true, BdZ0 - 0.15f, 0.3f, fx0, fx1, 0f, BdH, new (float, float, float)[0], wall, LBedroom);
        Cover(Group(gen, "NorthCover"), "N", true, BdZ1 - 0.02f, BdX0, BdX1, BdH, BdInX - 0.9f, BdInX + 0.9f, 2.4f, wall, LBedroom);
        var trim = Group(gen, "Baseboard");
        Cover(trim, "N", true, BdZ1 - 0.05f, BdX0, BdX1, 0.1f, BdInX - 0.9f, BdInX + 0.9f, 0.1f, deep, LBedroom);
        Cover(trim, "W", false, BdX0 + 0.02f, BdZ0, BdZ1, 0.1f, BdZ0, BdZ0, 0.1f, deep, LBedroom);
        Cover(trim, "E", false, BdX1 - 0.02f, BdZ0, BdZ1, 0.1f, CrZ - CrW / 2, CrZ + CrW / 2, 0.1f, deep, LBedroom);
        Cover(trim, "S", true, BdZ0 + 0.02f, BdX0, BdX1, 0.1f, BdX0, BdX0, 0.1f, deep, LBedroom);

        propLayer = LBedroom;
        TintBedroomVarco();
        var props = Group(gen, "Props");

        // ---- 침대(이상한 점). 방 남쪽 가운데에 x 방향으로 놓이고, 머리 쪽(서쪽)이 출구를 향한다.
        float bz = BdOutZ, bx0 = 42.4f, bx1 = 44.6f;
        var bed = new GameObject("Bed").transform; bed.SetParent(props, true); bed.position = new Vector3((bx0 + bx1) / 2, 0f, bz);
        var bc = bed.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(bx1 - bx0, 0.62f, 1.6f); bc.center = new Vector3(0, 0.31f, 0);
        Transform lump;
        if (VarcoPrefab("Bedroom", "Bed_BlanketFigure") != null && VarcoPrefab("Bedroom", "Bed_Empty") != null)
        {
            // 2026-11-19 VARCO 침대: 모델은 머리(헤드보드)가 -Z라 90도 돌려 머리를 서쪽(-X)으로.
            // 사람이 든 침대는 Oddity_BlanketFigure 아래(찾으면 꺼진다), 빈 침대는 처음엔 꺼 두고 찾은 순간 켠다(BuildBlueGimmick의 showOnFound).
            lump = new GameObject("Oddity_BlanketFigure").transform; lump.SetParent(bed, false);
            VarcoInto(lump, "Bedroom", "Bed_BlanketFigure", LBedroom, default, new Vector3(0, 90, 0), false);
            var empty = VarcoInto(bed, "Bedroom", "Bed_Empty", LBedroom, default, new Vector3(0, 90, 0), false);
            empty.SetActive(false);
            // 콜라이더는 매트리스 높이까지만: 빈 침대(더 크고 높음)에 맞추면 이불 속 사람을 바라보는 시선을 가려 찾을 수 없었다(2026-11-19).
            bc.size = new Vector3(2.0f, 0.55f, 1.15f); bc.center = new Vector3(0, 0.275f, 0);
        }
        else
        {
        PBox(bed, "Frame", new Vector3(0, 0.22f, 0), new Vector3(bx1 - bx0, 0.24f, 1.6f), deep);
        PBox(bed, "Mattress", new Vector3(0.05f, 0.43f, 0), new Vector3(bx1 - bx0 - 0.1f, 0.2f, 1.5f), furn);
        PBox(bed, "Headboard", new Vector3(-(bx1 - bx0) / 2 + 0.04f, 0.55f, 0), new Vector3(0.08f, 1.1f, 1.6f), deep);
        PBox(bed, "Pillow", new Vector3(-0.78f, 0.6f, 0), new Vector3(0.42f, 0.13f, 0.95f), furn);
        PBox(bed, "Blanket", new Vector3(0.28f, 0.56f, 0), new Vector3(1.6f, 0.07f, 1.58f), bedding);
        foreach (var lx in new[] { -1f, 1f }) foreach (var lz in new[] { -1f, 1f })
                PBox(bed, "Leg", new Vector3(lx * ((bx1 - bx0) / 2 - 0.06f), 0.05f, lz * 0.74f), new Vector3(0.08f, 0.1f, 0.08f), deep);
        // 이불 속 사람: 머리끝까지 이불을 덮어쓴 사람 모양. 베개 위의 둥근 머리, 넓은 어깨, 몸통, 갈라진 두 다리와 솟은 발끝.
        // 처음 플레이 확인에서 베개 여러 개처럼 보여서(2026-11-18) 머리를 베개 위에 올리고 어깨·다리 갈래를 또렷하게 했다.
        lump = new GameObject("Oddity_BlanketFigure").transform; lump.SetParent(bed, false);
        Part(lump, PrimitiveType.Sphere, "Head", new Vector3(-0.76f, 0.78f, 0f), new Vector3(0.3f, 0.3f, 0.28f), bedding);
        Part(lump, PrimitiveType.Sphere, "Shoulders", new Vector3(-0.42f, 0.66f, 0f), new Vector3(0.34f, 0.3f, 0.62f), bedding);
        Part(lump, PrimitiveType.Sphere, "Body", new Vector3(-0.05f, 0.64f, 0f), new Vector3(0.8f, 0.26f, 0.52f), bedding);
        Part(lump, PrimitiveType.Capsule, "Leg_L", new Vector3(0.55f, 0.6f, 0.12f), new Vector3(0.18f, 0.38f, 0.18f), bedding, new Vector3(0, 0, 90));
        Part(lump, PrimitiveType.Capsule, "Leg_R", new Vector3(0.55f, 0.6f, -0.12f), new Vector3(0.18f, 0.38f, 0.18f), bedding, new Vector3(0, 0, 90));
        Part(lump, PrimitiveType.Sphere, "Toes_L", new Vector3(0.92f, 0.7f, 0.12f), new Vector3(0.12f, 0.22f, 0.14f), bedding);
        Part(lump, PrimitiveType.Sphere, "Toes_R", new Vector3(0.92f, 0.7f, -0.12f), new Vector3(0.12f, 0.22f, 0.14f), bedding);
        }

        // ---- 북쪽 벽: 책상(모니터, 책 더미, 의자), 키 큰 책장
        var desk = new GameObject("Desk").transform; desk.SetParent(props, true); desk.position = new Vector3(45.6f, 0f, BdZ1 - 0.4f);
        var dc = desk.gameObject.AddComponent<BoxCollider>(); dc.size = new Vector3(1.8f, 0.78f, 0.7f); dc.center = new Vector3(0, 0.39f, 0);
        // 2026-11-20 VARCO 책상(1.37×0.76×0.79)·브라운관 모니터: 정면이 방 쪽(-Z)이게 180도, 뒷면을 벽에 붙인다.
        // 모니터 화면은 유리 면을 따라 만든 발광 메쉬를 덮어 켜진 화면으로 둔다(거실 TV와 같은 방식).
        bool vDesk = VarcoInto(desk, "Bedroom", "Desk", LBedroom, new Vector3(0, 0, -0.05f), new Vector3(0, 180, 0)) != null;
        if (!vDesk)
        {
            PBox(desk, "Top", new Vector3(0, 0.76f, 0), new Vector3(1.8f, 0.05f, 0.7f), furn);
            PBox(desk, "Drawers", new Vector3(0.62f, 0.37f, 0), new Vector3(0.5f, 0.72f, 0.64f), furn);
            PBox(desk, "Leg_L", new Vector3(-0.85f, 0.37f, 0), new Vector3(0.05f, 0.74f, 0.64f), furn);
        }
        float deskTop = vDesk ? 0.76f : 0.785f;
        var mon = VarcoInto(desk, "Bedroom", "CRTMonitor", LBedroom, new Vector3(0.15f, deskTop, 0.05f), new Vector3(0, 180, 0), false);
        if (mon != null)
        {
            var monScreen = GlassScreenMesh("Bedroom", "CRTMonitor", 0f, 0.235f, 0.125f, 0.095f, 0.03f);
            if (monScreen != null)
            {
                var s = MeshPart(mon.transform, "Screen", monScreen, screenMat, LBedroom, 1f);
                s.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        else
        {
            PBox(desk, "Monitor", new Vector3(0.1f, 1.02f, 0.12f), new Vector3(0.5f, 0.38f, 0.32f), deep);
            PBox(desk, "Screen", new Vector3(0.1f, 1.03f, -0.045f), new Vector3(0.4f, 0.28f, 0.01f), screenMat);
        }
        float bookX = vDesk ? -0.58f : -0.62f;
        for (int i = 0; i < 4; i++) PBox(desk, "Book", new Vector3(bookX + i * 0.07f, deskTop + 0.14f + (i % 2) * 0.025f, 0.1f), new Vector3(0.05f, 0.28f + (i % 2) * 0.05f, 0.22f), i % 2 == 0 ? deep : furn);
        PBox(desk, "BookStack", new Vector3(vDesk ? -0.15f : -0.3f, deskTop + 0.05f, 0.1f), new Vector3(0.22f, 0.1f, 0.2f), deep);
        var chair = new GameObject("Chair").transform; chair.SetParent(props, true); chair.position = new Vector3(45.5f, 0f, BdZ1 - (VarcoPrefab("Bedroom", "WoodChair") != null ? 1.12f : 1.05f));
        chair.rotation = Quaternion.Euler(0, 12f, 0);
        // 의자 정면(+Z)이 책상(북쪽)을 보게 둔다: 앉는 사람이 책상을 향한다.
        if (VarcoInto(chair, "Bedroom", "WoodChair", LBedroom, default, default, false) == null)
        {
            PBox(chair, "Seat", new Vector3(0, 0.46f, 0), new Vector3(0.45f, 0.04f, 0.42f), furn);
            PBox(chair, "Back", new Vector3(0, 0.75f, -0.2f), new Vector3(0.45f, 0.55f, 0.03f), furn);
            foreach (var o in new[] { new Vector2(-0.2f, -0.18f), new Vector2(0.2f, -0.18f), new Vector2(-0.2f, 0.18f), new Vector2(0.2f, 0.18f) })
                PBox(chair, "Leg", new Vector3(o.x, 0.22f, o.y), new Vector3(0.03f, 0.44f, 0.03f), furn);
        }

        var shelf = new GameObject("Bookshelf").transform; shelf.SetParent(props, true); shelf.position = new Vector3(47.9f, 0f, BdZ1 - 0.22f);
        var shc = shelf.gameObject.AddComponent<BoxCollider>(); shc.size = new Vector3(1.4f, 2.1f, 0.42f); shc.center = new Vector3(0, 1.05f, 0);
        // VARCO 책장(1.24×2.1×0.46, 2026-11-20 Blender에서 녹은 책 덩어리 제거·선반 바닥 다시 칠함). 정면이 방 쪽(-Z), 뒷면을 벽에.
        if (VarcoInto(shelf, "Bedroom", "Bookshelf", LBedroom, new Vector3(0, 0, -0.025f), new Vector3(0, 180, 0)) == null)
        {
        PBox(shelf, "Side_L", new Vector3(-0.68f, 1.05f, 0), new Vector3(0.04f, 2.1f, 0.42f), furn);
        PBox(shelf, "Side_R", new Vector3(0.68f, 1.05f, 0), new Vector3(0.04f, 2.1f, 0.42f), furn);
        PBox(shelf, "Mid", new Vector3(0, 1.05f, 0), new Vector3(0.03f, 2.1f, 0.42f), furn);
        PBox(shelf, "Back", new Vector3(0, 1.05f, 0.19f), new Vector3(1.4f, 2.1f, 0.03f), deep);
        for (int k = 0; k < 6; k++)
        {
            float y = 0.04f + k * 0.4f;
            PBox(shelf, "Board", new Vector3(0, y, 0), new Vector3(1.36f, 0.03f, 0.4f), furn);
            if (k == 0 || k == 5) continue;
            for (int i = 0; i < 9; i++)
            {
                if ((i * 7 + k * 3) % 5 == 0) continue; // 군데군데 빈칸
                float x = -0.6f + i * 0.075f + (i > 4 ? 0.08f : 0f);
                float h = 0.24f + ((i + k) % 3) * 0.04f;
                PBox(shelf, "Book", new Vector3(x, y + 0.015f + h / 2, 0.02f), new Vector3(0.06f, h, 0.28f), (i + k) % 3 == 0 ? furn : deep);
            }
        }
        }

        // ---- 동쪽 벽: 낮은 수납장, 벽 선반, 달빛 창문, 옛날 전화기가 놓인 작은 탁자
        // VARCO 낮은 수납장(1.14×0.75×0.65)·벽 선반(1.1×0.7×0.43, 책 몇 권 포함): 정면이 방 쪽(서쪽 -X), 뒷면을 동쪽 벽에.
        if (VarcoPrefab("Bedroom", "LowCabinet") != null)
        {
            var cab = PropGroup(props, "Cabinet", new Vector3(BdX1 - 0.015f - 0.325f, 0f, -126.4f), -90f, new Vector3(1.14f, 0.75f, 0.65f));
            VarcoInto(cab, "Bedroom", "LowCabinet", LBedroom);
        }
        else
        {
        Box(props, "Cabinet", new Vector3(BdX1 - 0.25f, 0.45f, -126.4f), new Vector3(0.48f, 0.9f, 1.0f), furn, LBedroom, true);
        Box(props, "Cabinet_Gap", new Vector3(BdX1 - 0.49f, 0.45f, -126.4f), new Vector3(0.01f, 0.02f, 0.96f), deep, LBedroom);
        }
        if (VarcoPrefab("Bedroom", "WallShelf") != null)
        {
            var ws = PropGroup(props, "WallShelf", new Vector3(BdX1 - 0.015f, 2.0f, -128.4f), -90f);
            VarcoInto(ws, "Bedroom", "WallShelf", LBedroom, default, default, false);
        }
        else
        {
        Box(props, "WallShelf", new Vector3(BdX1 - 0.15f, 1.85f, -128.4f), new Vector3(0.3f, 0.04f, 1.5f), furn, LBedroom);
        for (int i = 0; i < 7; i++)
            Box(props, "ShelfBook", new Vector3(BdX1 - 0.17f, 1.99f + (i % 2) * 0.02f, -129.0f + i * 0.08f), new Vector3(0.22f, 0.24f + (i % 2) * 0.04f, 0.06f), i % 3 == 0 ? furn : deep, LBedroom);
        }
        Box(props, "Window_Glass", new Vector3(BdX1 - 0.05f, 1.55f, -130.6f), new Vector3(0.02f, 1.2f, 1.4f), night, LBedroom); // 틀보다 방 쪽에 둔다
        Box(props, "Window_Frame", new Vector3(BdX1 - 0.03f, 1.55f, -130.6f), new Vector3(0.03f, 1.3f, 1.5f), deep, LBedroom);
        Box(props, "Window_Bar", new Vector3(BdX1 - 0.07f, 1.55f, -130.6f), new Vector3(0.03f, 1.3f, 0.04f), deep, LBedroom);

        var side = new GameObject("PhoneTable").transform; side.SetParent(props, true); side.position = new Vector3(BdX1 - 0.4f, 0f, -132.9f);
        var sdc = side.gameObject.AddComponent<BoxCollider>(); sdc.size = new Vector3(0.6f, 0.7f, 0.9f); sdc.center = new Vector3(0, 0.35f, 0);
        PBox(side, "Top", new Vector3(0, 0.68f, 0), new Vector3(0.62f, 0.04f, 0.92f), furn);
        foreach (var o in new[] { new Vector2(-0.25f, -0.38f), new Vector2(0.25f, -0.38f), new Vector2(-0.25f, 0.38f), new Vector2(0.25f, 0.38f) })
            PBox(side, "Leg", new Vector3(o.x, 0.33f, o.y), new Vector3(0.04f, 0.66f, 0.04f), furn);
        // 탁자 윗면(0.70)에 올려 둔다. 전에는 본체가 5mm, 수화기가 본체 위로 1cm 떠 있었다.
        // 2026-11-20 VARCO 다이얼 전화기(0.24×0.13×0.18): 정면(다이얼)이 방 쪽(서쪽 -X)을 보게 -90도.
        if (VarcoInto(side, "Bedroom", "RotaryPhone", LBedroom, new Vector3(0, 0.70f, 0.05f), new Vector3(0, -90, 0), false) == null)
        {
        PBox(side, "Phone_Base", new Vector3(0, 0.745f, 0.05f), new Vector3(0.22f, 0.09f, 0.26f), black);
        PBox(side, "Phone_Receiver", new Vector3(0, 0.815f, 0.05f), new Vector3(0.07f, 0.05f, 0.3f), black);
        Part(side, PrimitiveType.Cylinder, "Phone_Dial", new Vector3(-0.112f, 0.745f, 0.05f), new Vector3(0.14f, 0.005f, 0.14f), deep, new Vector3(0, 0, 90));
        }

        // ---- 가운데: 다리가 휜 낮은 탁자. 서쪽 벽: 서랍장과 그 위의 종이 한 장. 남쪽 벽: 검은 액자.
        var table = new GameObject("LowTable").transform; table.SetParent(props, true); table.position = new Vector3(46.4f, 0f, -131.2f);
        var tc = table.gameObject.AddComponent<BoxCollider>(); tc.size = new Vector3(1.3f, 0.45f, 1.3f); tc.center = new Vector3(0, 0.22f, 0);
        // 2026-11-20 VARCO 다리 휜 낮은 탁자(1.12×0.6×1.12).
        if (VarcoInto(table, "Bedroom", "CurvedLowTable", LBedroom) == null)
        {
        PBox(table, "Top", new Vector3(0, 0.42f, 0), new Vector3(1.3f, 0.07f, 1.3f), furn);
        PBox(table, "Apron", new Vector3(0, 0.35f, 0), new Vector3(1.18f, 0.08f, 1.18f), furn);
        foreach (var o in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
        {
            PBox(table, "Leg", new Vector3(o.x * 0.52f, 0.18f, o.y * 0.52f), new Vector3(0.06f, 0.34f, 0.06f), furn);
            PBox(table, "Foot", new Vector3(o.x * 0.58f, 0.02f, o.y * 0.58f), new Vector3(0.12f, 0.04f, 0.06f), furn);
        }
        }
        // 2026-11-20 VARCO 서랍장은 키 큰 서랍장(0.75×1.1×0.51)이라 낮고 긴 더미와 모양이 다르다. 같은 서쪽 벽 자리, 정면이 방 쪽(동쪽).
        // 종이 한 장은 서랍장 윗면에 올린다.
        if (VarcoPrefab("Bedroom", "Dresser") != null)
        {
            var dr = PropGroup(props, "Dresser", new Vector3(BdX0 + 0.02f + 0.255f, 0f, -127.6f), 90f, new Vector3(0.75f, 1.1f, 0.51f));
            VarcoInto(dr, "Bedroom", "Dresser", LBedroom);
            Box(props, "Note", new Vector3(BdX0 + 0.3f, 1.103f, -127.65f), new Vector3(0.22f, 0.004f, 0.3f), paper, LBedroom, false, new Vector3(0, 14f, 0));
        }
        else
        {
        Box(props, "Dresser", new Vector3(BdX0 + 0.28f, 0.42f, -127.6f), new Vector3(0.52f, 0.84f, 2.4f), furn, LBedroom, true);
        foreach (var z in new[] { -126.8f, -128.4f }) Box(props, "Dresser_Line", new Vector3(BdX0 + 0.545f, 0.5f, z), new Vector3(0.01f, 0.02f, 0.7f), deep, LBedroom);
        Box(props, "Note", new Vector3(BdX0 + 0.3f, 0.845f, -128.1f), new Vector3(0.22f, 0.004f, 0.3f), paper, LBedroom, false, new Vector3(0, 14f, 0));
        }
        // 2026-11-24 남쪽 벽 검은 액자(장난감 상자 위): 그림이 있으면 틀 없는 포스터로 바꾼다(크기 액자의 1.2배, 가로는 그림 비율).
        var bdPoster = Img2D("Bedroom", "Poster");
        if (bdPoster != null) { propLayer = LBedroom; Poster(props, new Vector3(45.8f, 1.6f, BdZ0 + 0.02f), 0f, 0.84f * Img2DAspect(bdPoster), 0.84f, bdPoster); }
        else Box(props, "Frame", new Vector3(45.8f, 1.6f, BdZ0 + 0.03f), new Vector3(0.9f, 0.7f, 0.04f), black, LBedroom);
        Box(props, "Rug_Under_Table", new Vector3(46.4f, 0.004f, -131.2f), new Vector3(2.2f, 0.008f, 2.0f), deep, LBedroom);

        // 2026-11-20 방 채우기(넓은데 휑하다는 피드백). 비어 있던 서쪽 벽 남쪽·남쪽 벽에 둔다.
        // 입구(북쪽 x 40.6) → 침대의 시선, 동쪽 벽(그림자·복도 입구 CrZ)은 비운다.
        // 2026-11-23 VARCO 모델이 더미보다 깊어(옷장 0.78, 상자 0.6) 벽에서 조금 띄웠다.
        // 서쪽 벽: 옷장 / 침대 남쪽 머리맡: 협탁과 작은 스탠드 / 남서쪽 모서리: 전신 거울 / 남쪽 벽 액자 아래: 장난감 상자.
        var mirrorGlass = Lit("GB_Bedroom_Mirror", m => Plain(m, new Color(0.08f, 0.1f, 0.2f), 0.95f, 0.9f));
        var lampShade = Lit("GB_Bedroom_LampShade", m => Emissive(m, new Color(0.55f, 0.65f, 1f), 120f));
        FillProp(props, "Bedroom", "Wardrobe", new Vector3(BdX0 + 0.42f, 0f, -133.4f), 90f, new Vector3(1.2f, 2.0f, 0.6f), g =>
        {
            PBox(g, "Body", new Vector3(0, 1.0f, 0), new Vector3(1.2f, 2.0f, 0.58f), furn);
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                PBox(g, "Door", new Vector3(x, 1.02f, 0.292f), new Vector3(0.58f, 1.9f, 0.006f), furn);
                PBox(g, "Knob", new Vector3(x * 0.15f, 1.05f, 0.31f), new Vector3(0.025f, 0.12f, 0.025f), deep);
            }
            PBox(g, "Crown", new Vector3(0, 2.0f, 0.01f), new Vector3(1.26f, 0.04f, 0.62f), deep);
        });
        var nsPos = new Vector3(42.75f, 0f, BdOutZ - 1.2f);
        FillProp(props, "Bedroom", "Nightstand", nsPos, 0f, new Vector3(0.45f, 0.55f, 0.4f), g =>
        {
            PBox(g, "Body", new Vector3(0, 0.275f, 0), new Vector3(0.45f, 0.55f, 0.4f), furn);
            PBox(g, "Drawer", new Vector3(0, 0.4f, 0.201f), new Vector3(0.38f, 0.16f, 0.006f), deep);
        });
        // 협탁 위 스탠드는 따로(나중에 VARCO 모델이 협탁 한 덩어리로 와도 불빛 자리는 같게). 콜라이더 없음.
        var lamp = PropGroup(props, "BedsideLamp", nsPos + new Vector3(-0.08f, 0.55f, 0f), 0f);
        Part(lamp, PrimitiveType.Cylinder, "Base", new Vector3(0, 0.01f, 0), new Vector3(0.12f, 0.01f, 0.12f), deep);
        Part(lamp, PrimitiveType.Cylinder, "Stem", new Vector3(0, 0.12f, 0), new Vector3(0.02f, 0.11f, 0.02f), deep);
        Part(lamp, PrimitiveType.Cylinder, "Shade", new Vector3(0, 0.27f, 0), new Vector3(0.2f, 0.08f, 0.2f), lampShade);
        FillProp(props, "Bedroom", "StandingMirror", new Vector3(BdX0 + 0.45f, 0f, BdZ0 + 0.45f), 45f, new Vector3(0.55f, 1.6f, 0.3f), g =>
        {
            PBox(g, "Frame", new Vector3(0, 0.82f, -0.05f), new Vector3(0.55f, 1.5f, 0.04f), deep);
            PBox(g, "Glass", new Vector3(0, 0.82f, -0.028f), new Vector3(0.47f, 1.42f, 0.005f), mirrorGlass);
            Part(g, PrimitiveType.Cube, "Stand", new Vector3(0, 0.5f, -0.17f), new Vector3(0.04f, 1.0f, 0.04f), deep, new Vector3(-18f, 0, 0));
            PBox(g, "Foot", new Vector3(0, 0.02f, -0.05f), new Vector3(0.5f, 0.04f, 0.12f), deep);
        });
        FillProp(props, "Bedroom", "ToyChest", new Vector3(45.8f, 0f, BdZ0 + 0.32f), 0f, new Vector3(0.9f, 0.5f, 0.45f), g =>
        {
            PBox(g, "Body", new Vector3(0, 0.22f, 0), new Vector3(0.9f, 0.44f, 0.45f), furn);
            PBox(g, "Lid", new Vector3(0, 0.47f, 0), new Vector3(0.94f, 0.06f, 0.49f), deep);
            Part(g, PrimitiveType.Sphere, "Ball", new Vector3(0.62f, 0.12f, 0.12f), Vector3.one * 0.24f, bedding);
        });

        // ---- 빛: 창문으로 들어오는 파란 달빛 한 줄기 + 보조광. 방 전체를 파랗게, 벽과 가구가 읽힐 만큼은 밝게(처음엔 벽이 거의 검었다).
        var lights = Group(gen, "Lights");
        var moonPos = new Vector3(BdX1 - 0.3f, 2.3f, -130.6f);
        var moonAim = new Vector3(44.2f, 0f, -132.4f);
        var moon = MakeLight(lights, "Moonlight", LightType.Spot, moonPos, Quaternion.LookRotation(moonAim - moonPos).eulerAngles, new Color(0.45f, 0.6f, 1f), 26000f, 14f, LBedroom, true, false); // 안개 산란 끔: 켜면 창문 위 광원 자리에 빛 얼룩이 맺혀 보였다(2026-11-20)
        moon.spotAngle = 62f; moon.innerSpotAngle = 30f;
        // 2026-11-20 빛을 가구 쪽으로 좁힌다: 방 전체를 고르게 밝히던 채움 빛 2개를 줄이고(160000/14m → 90000/9m, 40000/10m → 22000/6m)
        // 협탁 스탠드의 작은 빛을 더한다. 방 모서리는 어둠에 묻힌다.
        MakeLight(lights, "Fill", LightType.Point, new Vector3(44f, -2.5f, -130f), Vector3.zero, new Color(0.3f, 0.42f, 1f), 90000f, 9f, LBedroom, false, false);
        MakeLight(lights, "CeilingFill", LightType.Point, new Vector3(44f, 1.3f, -129f), Vector3.zero, new Color(0.35f, 0.45f, 1f), 22000f, 6f, LBedroom, false, false);
        MakeLight(lights, "BedsideLamp", LightType.Point, nsPos + new Vector3(-0.08f, 0.85f, 0f), Vector3.zero, new Color(0.55f, 0.65f, 1f), 2500f, 3f, LBedroom, false, false);
        MakeLight(lights, "ScreenGlow", LightType.Point, new Vector3(45.7f, 1.1f, BdZ1 - 0.8f), Vector3.zero, new Color(0.45f, 0.65f, 1f), 2500f, 3f, LBedroom, false, false);
        var profile = RoomProfile("Bedroom", 6.8f, 0.01f, 0.04f, new Color(0.35f, 0.45f, 0.95f), 30f, new Color(0.01f, 0.015f, 0.05f), -30f, 0f);
        var roomCenter = new Vector3((BdX0 + BdX1) / 2, BdH / 2, (BdZ0 + BdZ1) / 2);
        var roomSize = new Vector3(BdX1 - BdX0, BdH, BdZ1 - BdZ0);
        LocalVolume(gen, "LocalVolume", roomCenter, roomSize, 1f, 125f, profile);

        // ---- 찾기 규칙과 기믹(§6-3d): 이불 속 사람 → 벽의 그림자 → 복도 → 도어뷰어 → 천사 방(BlueGimmick.cs)
        BuildBlueGimmick(gen, props, lump, new Bounds(roomCenter, roomSize), profile, wall, carpet, ceil, furn, deep, black);
    }

    // 지그재그(갈매기) 무늬 카펫: 진한 남색과 파랑이 번갈아 꺾인다. 반복해서 깔리도록 Repeat.
    static Texture2D ZigzagTexture()
    {
        const int n = 256;
        var dark = new Color(0.05f, 0.07f, 0.28f); var light = new Color(0.16f, 0.24f, 0.85f);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float tri = Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f);          // 0~0.5 꺾임
                bool band = Mathf.Repeat(v * 6f + tri * 3f, 1f) < 0.5f;
                bool block = Mathf.Repeat(u * 2f, 1f) < 0.5f;                     // 칸마다 방향을 뒤집어 엇갈리게
                if (block) band = Mathf.Repeat(v * 6f - tri * 3f, 1f) < 0.5f;
                px[y * n + x] = band ? light : dark;
            }
        Directory.CreateDirectory(TexDir);
        string path = $"{TexDir}/GB_Bedroom_Zigzag.png";
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat; imp.sRGBTexture = true; imp.mipmapEnabled = true; imp.anisoLevel = 8;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
