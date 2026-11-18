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
        var props = Group(gen, "Props");

        // ---- 침대(이상한 점). 방 남쪽 가운데에 x 방향으로 놓이고, 머리 쪽(서쪽)이 출구를 향한다.
        float bz = BdOutZ, bx0 = 42.4f, bx1 = 44.6f;
        var bed = new GameObject("Bed").transform; bed.SetParent(props, true); bed.position = new Vector3((bx0 + bx1) / 2, 0f, bz);
        var bc = bed.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(bx1 - bx0, 0.62f, 1.6f); bc.center = new Vector3(0, 0.31f, 0);
        PBox(bed, "Frame", new Vector3(0, 0.22f, 0), new Vector3(bx1 - bx0, 0.24f, 1.6f), deep);
        PBox(bed, "Mattress", new Vector3(0.05f, 0.43f, 0), new Vector3(bx1 - bx0 - 0.1f, 0.2f, 1.5f), furn);
        PBox(bed, "Headboard", new Vector3(-(bx1 - bx0) / 2 + 0.04f, 0.55f, 0), new Vector3(0.08f, 1.1f, 1.6f), deep);
        PBox(bed, "Pillow", new Vector3(-0.78f, 0.6f, 0), new Vector3(0.42f, 0.13f, 0.95f), furn);
        PBox(bed, "Blanket", new Vector3(0.28f, 0.56f, 0), new Vector3(1.6f, 0.07f, 1.58f), bedding);
        foreach (var lx in new[] { -1f, 1f }) foreach (var lz in new[] { -1f, 1f })
                PBox(bed, "Leg", new Vector3(lx * ((bx1 - bx0) / 2 - 0.06f), 0.05f, lz * 0.74f), new Vector3(0.08f, 0.1f, 0.08f), deep);
        // 이불 속 사람: 머리끝까지 이불을 덮어쓴 사람 모양. 베개 위의 둥근 머리, 넓은 어깨, 몸통, 갈라진 두 다리와 솟은 발끝.
        // 처음 플레이 확인에서 베개 여러 개처럼 보여서(2026-11-18) 머리를 베개 위에 올리고 어깨·다리 갈래를 또렷하게 했다.
        var lump = new GameObject("Oddity_BlanketFigure").transform; lump.SetParent(bed, false);
        Part(lump, PrimitiveType.Sphere, "Head", new Vector3(-0.76f, 0.78f, 0f), new Vector3(0.3f, 0.3f, 0.28f), bedding);
        Part(lump, PrimitiveType.Sphere, "Shoulders", new Vector3(-0.42f, 0.66f, 0f), new Vector3(0.34f, 0.3f, 0.62f), bedding);
        Part(lump, PrimitiveType.Sphere, "Body", new Vector3(-0.05f, 0.64f, 0f), new Vector3(0.8f, 0.26f, 0.52f), bedding);
        Part(lump, PrimitiveType.Capsule, "Leg_L", new Vector3(0.55f, 0.6f, 0.12f), new Vector3(0.18f, 0.38f, 0.18f), bedding, new Vector3(0, 0, 90));
        Part(lump, PrimitiveType.Capsule, "Leg_R", new Vector3(0.55f, 0.6f, -0.12f), new Vector3(0.18f, 0.38f, 0.18f), bedding, new Vector3(0, 0, 90));
        Part(lump, PrimitiveType.Sphere, "Toes_L", new Vector3(0.92f, 0.7f, 0.12f), new Vector3(0.12f, 0.22f, 0.14f), bedding);
        Part(lump, PrimitiveType.Sphere, "Toes_R", new Vector3(0.92f, 0.7f, -0.12f), new Vector3(0.12f, 0.22f, 0.14f), bedding);

        // ---- 북쪽 벽: 책상(모니터, 책 더미, 의자), 키 큰 책장
        var desk = new GameObject("Desk").transform; desk.SetParent(props, true); desk.position = new Vector3(45.6f, 0f, BdZ1 - 0.4f);
        var dc = desk.gameObject.AddComponent<BoxCollider>(); dc.size = new Vector3(1.8f, 0.78f, 0.7f); dc.center = new Vector3(0, 0.39f, 0);
        PBox(desk, "Top", new Vector3(0, 0.76f, 0), new Vector3(1.8f, 0.05f, 0.7f), furn);
        PBox(desk, "Drawers", new Vector3(0.62f, 0.37f, 0), new Vector3(0.5f, 0.72f, 0.64f), furn);
        PBox(desk, "Leg_L", new Vector3(-0.85f, 0.37f, 0), new Vector3(0.05f, 0.74f, 0.64f), furn);
        PBox(desk, "Monitor", new Vector3(0.1f, 1.02f, 0.12f), new Vector3(0.5f, 0.38f, 0.32f), deep);
        PBox(desk, "Screen", new Vector3(0.1f, 1.03f, -0.045f), new Vector3(0.4f, 0.28f, 0.01f), screenMat);
        PBox(desk, "Keyboard", new Vector3(0.1f, 0.79f, -0.18f), new Vector3(0.42f, 0.02f, 0.14f), deep);
        for (int i = 0; i < 4; i++) PBox(desk, "Book", new Vector3(-0.62f + i * 0.09f, 0.92f, 0.1f), new Vector3(0.07f, 0.28f + (i % 2) * 0.05f, 0.22f), i % 2 == 0 ? deep : furn);
        PBox(desk, "BookStack", new Vector3(-0.3f, 0.83f, 0.1f), new Vector3(0.26f, 0.1f, 0.2f), deep);
        var chair = new GameObject("Chair").transform; chair.SetParent(props, true); chair.position = new Vector3(45.5f, 0f, BdZ1 - 1.05f);
        chair.rotation = Quaternion.Euler(0, 12f, 0);
        PBox(chair, "Seat", new Vector3(0, 0.46f, 0), new Vector3(0.45f, 0.04f, 0.42f), furn);
        PBox(chair, "Back", new Vector3(0, 0.75f, -0.2f), new Vector3(0.45f, 0.55f, 0.03f), furn);
        foreach (var o in new[] { new Vector2(-0.2f, -0.18f), new Vector2(0.2f, -0.18f), new Vector2(-0.2f, 0.18f), new Vector2(0.2f, 0.18f) })
            PBox(chair, "Leg", new Vector3(o.x, 0.22f, o.y), new Vector3(0.03f, 0.44f, 0.03f), furn);

        var shelf = new GameObject("Bookshelf").transform; shelf.SetParent(props, true); shelf.position = new Vector3(47.9f, 0f, BdZ1 - 0.22f);
        var shc = shelf.gameObject.AddComponent<BoxCollider>(); shc.size = new Vector3(1.4f, 2.1f, 0.42f); shc.center = new Vector3(0, 1.05f, 0);
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

        // ---- 동쪽 벽: 낮은 수납장, 벽 선반, 달빛 창문, 옛날 전화기가 놓인 작은 탁자
        Box(props, "Cabinet", new Vector3(BdX1 - 0.25f, 0.45f, -126.4f), new Vector3(0.48f, 0.9f, 1.0f), furn, LBedroom, true);
        Box(props, "Cabinet_Gap", new Vector3(BdX1 - 0.49f, 0.45f, -126.4f), new Vector3(0.01f, 0.02f, 0.96f), deep, LBedroom);
        Box(props, "WallShelf", new Vector3(BdX1 - 0.15f, 1.85f, -128.4f), new Vector3(0.3f, 0.04f, 1.5f), furn, LBedroom);
        for (int i = 0; i < 7; i++)
            Box(props, "ShelfBook", new Vector3(BdX1 - 0.17f, 1.99f + (i % 2) * 0.02f, -129.0f + i * 0.08f), new Vector3(0.22f, 0.24f + (i % 2) * 0.04f, 0.06f), i % 3 == 0 ? furn : deep, LBedroom);
        Box(props, "Window_Glass", new Vector3(BdX1 - 0.05f, 1.55f, -130.6f), new Vector3(0.02f, 1.2f, 1.4f), night, LBedroom); // 틀보다 방 쪽에 둔다
        Box(props, "Window_Frame", new Vector3(BdX1 - 0.03f, 1.55f, -130.6f), new Vector3(0.03f, 1.3f, 1.5f), deep, LBedroom);
        Box(props, "Window_Bar", new Vector3(BdX1 - 0.07f, 1.55f, -130.6f), new Vector3(0.03f, 1.3f, 0.04f), deep, LBedroom);

        var side = new GameObject("PhoneTable").transform; side.SetParent(props, true); side.position = new Vector3(BdX1 - 0.4f, 0f, -132.9f);
        var sdc = side.gameObject.AddComponent<BoxCollider>(); sdc.size = new Vector3(0.6f, 0.7f, 0.9f); sdc.center = new Vector3(0, 0.35f, 0);
        PBox(side, "Top", new Vector3(0, 0.68f, 0), new Vector3(0.62f, 0.04f, 0.92f), furn);
        foreach (var o in new[] { new Vector2(-0.25f, -0.38f), new Vector2(0.25f, -0.38f), new Vector2(-0.25f, 0.38f), new Vector2(0.25f, 0.38f) })
            PBox(side, "Leg", new Vector3(o.x, 0.33f, o.y), new Vector3(0.04f, 0.66f, 0.04f), furn);
        PBox(side, "Phone_Base", new Vector3(0, 0.75f, 0.05f), new Vector3(0.22f, 0.09f, 0.26f), black);
        PBox(side, "Phone_Receiver", new Vector3(0, 0.83f, 0.05f), new Vector3(0.07f, 0.05f, 0.3f), black);
        Part(side, PrimitiveType.Cylinder, "Phone_Dial", new Vector3(-0.112f, 0.75f, 0.05f), new Vector3(0.14f, 0.005f, 0.14f), deep, new Vector3(0, 0, 90));

        // ---- 가운데: 다리가 휜 낮은 탁자. 서쪽 벽: 서랍장과 그 위의 종이 한 장. 남쪽 벽: 검은 액자.
        var table = new GameObject("LowTable").transform; table.SetParent(props, true); table.position = new Vector3(46.4f, 0f, -131.2f);
        var tc = table.gameObject.AddComponent<BoxCollider>(); tc.size = new Vector3(1.3f, 0.45f, 1.3f); tc.center = new Vector3(0, 0.22f, 0);
        PBox(table, "Top", new Vector3(0, 0.42f, 0), new Vector3(1.3f, 0.07f, 1.3f), furn);
        PBox(table, "Apron", new Vector3(0, 0.35f, 0), new Vector3(1.18f, 0.08f, 1.18f), furn);
        foreach (var o in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
        {
            PBox(table, "Leg", new Vector3(o.x * 0.52f, 0.18f, o.y * 0.52f), new Vector3(0.06f, 0.34f, 0.06f), furn);
            PBox(table, "Foot", new Vector3(o.x * 0.58f, 0.02f, o.y * 0.58f), new Vector3(0.12f, 0.04f, 0.06f), furn);
        }
        Box(props, "Dresser", new Vector3(BdX0 + 0.28f, 0.42f, -127.6f), new Vector3(0.52f, 0.84f, 2.4f), furn, LBedroom, true);
        foreach (var z in new[] { -126.8f, -128.4f }) Box(props, "Dresser_Line", new Vector3(BdX0 + 0.545f, 0.5f, z), new Vector3(0.01f, 0.02f, 0.7f), deep, LBedroom);
        Box(props, "Note", new Vector3(BdX0 + 0.3f, 0.845f, -128.1f), new Vector3(0.22f, 0.004f, 0.3f), paper, LBedroom, false, new Vector3(0, 14f, 0));
        Box(props, "Frame", new Vector3(45.8f, 1.6f, BdZ0 + 0.03f), new Vector3(0.9f, 0.7f, 0.04f), black, LBedroom);
        Box(props, "Rug_Under_Table", new Vector3(46.4f, 0.004f, -131.2f), new Vector3(2.2f, 0.008f, 2.0f), deep, LBedroom);

        // ---- 빛: 창문으로 들어오는 파란 달빛 한 줄기 + 보조광. 방 전체를 파랗게, 벽과 가구가 읽힐 만큼은 밝게(처음엔 벽이 거의 검었다).
        var lights = Group(gen, "Lights");
        var moonPos = new Vector3(BdX1 - 0.3f, 2.3f, -130.6f);
        var moonAim = new Vector3(44.2f, 0f, -132.4f);
        var moon = MakeLight(lights, "Moonlight", LightType.Spot, moonPos, Quaternion.LookRotation(moonAim - moonPos).eulerAngles, new Color(0.45f, 0.6f, 1f), 26000f, 14f, LBedroom, true);
        moon.spotAngle = 62f; moon.innerSpotAngle = 30f;
        MakeLight(lights, "Fill", LightType.Point, new Vector3(44f, -2.5f, -130f), Vector3.zero, new Color(0.3f, 0.42f, 1f), 160000f, 14f, LBedroom, false, false);
        MakeLight(lights, "CeilingFill", LightType.Point, new Vector3(44f, 2.2f, -129f), Vector3.zero, new Color(0.35f, 0.45f, 1f), 40000f, 10f, LBedroom, false, false);
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
