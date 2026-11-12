using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Scene_Intro 확장판(2026-11-12) — "네모나게 키우지 않고, 벽을 뚫어 옆방과 연결한다."
// Pool Glow 레퍼런스: 아치형 출입구, 타일 터널, 흰 원기둥, 얕은 수영장과 계단, 화분 속 큰 나무.
//
// 구조(평면, +x 동 / -z 북):
//   A  중정(기존, x±22 z±28)  ── 동쪽 아치 ──▶  B  수로 홀   (x 22~50,  z±13)
//        │                  ◀── 서쪽 아치 ──   C  석호 정원 (x -50~-22, z±13)
//        └── 북쪽 아치 ──▶ 타일 터널(길이 20) ──▶ E  목표 방 (x±12, z -70~-48, 구체 받침대)
//
// 아치 벽/터널/아치 패널은 프리미티브로 안 되는 곡면이라 코드로 메시를 만든다(작은 절차적 메시).
public static partial class IntroMapBuilder
{
    // ---- 확장 치수 ------------------------------------------------------------
    const float DoorWidth = 4.5f;          // 아치 출입구 폭 (반지름 2.25)
    const float DoorStraightH = 2.4f;      // 아치가 시작되는 높이 (꼭대기 = 2.4+2.25 = 4.65m)
    const float WingWallHeight = 11f;      // 옆방 벽 높이
    const float WingHalfZ = 13f;           // B/C 반폭(z)
    const float WingOuterX = 50f;          // B/C 바깥 끝(|x|)
    const float TunnelLength = 20f;
    const float GoalHalfX = 12f;
    const float GoalZFar = 70f;            // E 방 북쪽 끝(|z|)
    const int ArcSegments = 20;

    static Material MatArchGlow;

    // 확장 전체. 기존 중정(A) 벽과 출입구는 BuildCourtyardWalls가 만든다.
    static void BuildWings(Transform env)
    {
        MatArchGlow = MakeEmissiveMat("Assets/Materials/Env/Env_ArchGlow.mat", new Color(0.62f, 0.84f, 0.76f), 0.7f);
        BuildChannelHall(env);
        BuildLagoonCourt(env);
        BuildTunnelAndGoal(env);
    }

    // ---- 수영장(범용) -----------------------------------------------------------
    // 수영장 바닥/안쪽 벽/테두리 턱/물/계단. depth가 얕고(0.9m) 계단으로 걸어 들어간다.
    // stairsNorth/South: 그 끝(-z / +z)에 폭 stairW짜리 계단을 만든다(턱도 그 폭만큼 비운다).
    static void BuildPool(Transform parent, string prefix, Vector3 c, float hx, float hz, float depth,
                          bool stairsNorth, bool stairsSouth, bool tileCurb = true)
    {
        float y = -depth / 2f;
        Box(parent, prefix + "_Floor", new Vector3(c.x, -depth - 0.2f, c.z), new Vector3(hx * 2f + 0.8f, 0.4f, hz * 2f + 0.8f), MatPoolTile, true);
        Box(parent, prefix + "_Wall_North", new Vector3(c.x, y, c.z - hz - 0.2f), new Vector3(hx * 2f + 0.8f, depth, 0.4f), MatPoolTile, true);
        Box(parent, prefix + "_Wall_South", new Vector3(c.x, y, c.z + hz + 0.2f), new Vector3(hx * 2f + 0.8f, depth, 0.4f), MatPoolTile, true);
        Box(parent, prefix + "_Wall_West", new Vector3(c.x - hx - 0.2f, y, c.z), new Vector3(0.4f, depth, hz * 2f), MatPoolTile, true);
        Box(parent, prefix + "_Wall_East", new Vector3(c.x + hx + 0.2f, y, c.z), new Vector3(0.4f, depth, hz * 2f), MatPoolTile, true);

        // 테두리 턱(StepOffset 0.3보다 높아 걸어 다니다 빠지지 않는다). 계단 끝은 폭만큼 비운다.
        Box(parent, prefix + "Curb_West", new Vector3(c.x - hx - 0.15f, CurbHeight / 2f, c.z), new Vector3(0.3f, CurbHeight, hz * 2f), MatFloor, true);
        Box(parent, prefix + "Curb_East", new Vector3(c.x + hx + 0.15f, CurbHeight / 2f, c.z), new Vector3(0.3f, CurbHeight, hz * 2f), MatFloor, true);
        EndCurb(parent, prefix + "Curb_North", c, hx, c.z - hz - 0.15f, stairsNorth);
        EndCurb(parent, prefix + "Curb_South", c, hx, c.z + hz + 0.15f, stairsSouth);

        float riser = depth / (PoolSteps + 1);
        if (stairsNorth) BuildStairs(parent, prefix + "_StairN", new Vector3(c.x, 0, c.z - hz), +1, depth, riser);
        if (stairsSouth) BuildStairs(parent, prefix + "_StairS", new Vector3(c.x, 0, c.z + hz), -1, depth, riser);

        // 수면
        var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
        water.name = prefix + "_Water";
        Object.DestroyImmediate(water.GetComponent<Collider>());
        water.transform.SetParent(parent, false);
        water.transform.localPosition = new Vector3(c.x, WaterY, c.z);
        water.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        water.transform.localScale = new Vector3(hx * 2f, hz * 2f, 1f);
        water.GetComponent<MeshRenderer>().sharedMaterial = MatWater;
        water.AddComponent<WaterScroll>().scrollSpeed = new Vector2(0.01f, 0.015f);
    }

    static void EndCurb(Transform parent, string name, Vector3 c, float hx, float z, bool gap)
    {
        float outerHalf = hx + 0.3f;
        if (!gap)
        {
            Box(parent, name, new Vector3(c.x, CurbHeight / 2f, z), new Vector3(hx * 2f + 0.6f, CurbHeight, 0.3f), MatFloor, true);
            return;
        }
        float g = StairWidth / 2f;
        float segW = outerHalf - g;
        float segC = (outerHalf + g) / 2f;
        Box(parent, name + "_W", new Vector3(c.x - segC, CurbHeight / 2f, z), new Vector3(segW, CurbHeight, 0.3f), MatFloor, true);
        Box(parent, name + "_E", new Vector3(c.x + segC, CurbHeight / 2f, z), new Vector3(segW, CurbHeight, 0.3f), MatFloor, true);
    }

    // 계단: 데크 끝(edge, y=0)에서 안쪽으로 dir(+1 = +z, -1 = -z) 방향으로 한 칸씩 내려간다.
    // 한 칸 높이 riser(0.15m)는 CharacterController StepOffset(0.3)보다 낮아 그냥 걸어 오르내린다.
    static void BuildStairs(Transform parent, string name, Vector3 edge, int dir, float depth, float riser)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        for (int k = 1; k <= PoolSteps; k++)
        {
            float top = -riser * k;
            float h = depth + top;                   // 바닥(-depth)에서 윗면까지
            float zc = edge.z + dir * StairTread * (k - 0.5f);
            Box(g, "Step_" + k, new Vector3(edge.x, top - h / 2f, zc), new Vector3(StairWidth, h, StairTread), MatPoolTile, true);
        }
    }

    // 사각 바닥 한 장에서 수영장 개구부를 빼고 4장(북/남 띠, 서/동 조각)으로 깐다.
    static void FloorWithHole(Transform p, string name, float x0, float x1, float z0, float z1,
                              float ox0, float ox1, float oz0, float oz1)
    {
        float t = DeckThick;
        Box(p, name + "_N", new Vector3((x0 + x1) / 2f, -t / 2f, (z0 + oz0) / 2f), new Vector3(x1 - x0, t, oz0 - z0), MatFloor, true);
        Box(p, name + "_S", new Vector3((x0 + x1) / 2f, -t / 2f, (oz1 + z1) / 2f), new Vector3(x1 - x0, t, z1 - oz1), MatFloor, true);
        Box(p, name + "_W", new Vector3((x0 + ox0) / 2f, -t / 2f, (oz0 + oz1) / 2f), new Vector3(ox0 - x0, t, oz1 - oz0), MatFloor, true);
        Box(p, name + "_E", new Vector3((ox1 + x1) / 2f, -t / 2f, (oz0 + oz1) / 2f), new Vector3(x1 - ox1, t, oz1 - oz0), MatFloor, true);
    }

    // ---- B: 수로 홀 (동쪽) --------------------------------------------------------
    static void BuildChannelHall(Transform env)
    {
        float x0 = DeckHalfX, x1 = WingOuterX;              // 22 ~ 50
        float cx = (x0 + x1) / 2f, len = x1 - x0;
        // 수로 개구부(월드 x cx±9.5, z ±2.6)를 비운 4장 — 통판으로 깔면 물이 바닥 밑에 묻힌다.
        FloorWithHole(env, "ChannelHall_Floor", x0, x1, -WingHalfZ, WingHalfZ, cx - 9.5f, cx + 9.5f, -2.6f, 2.6f);

        WingWalls(env, "ChannelHall", cx, 0, len, WingHalfZ * 2f, openWest: true, openEast: false, openNorth: false, openSouth: false);

        // 가운데 얕은 수로(긴 변이 x 방향): 회전한 그룹 안에서 만든다.
        var grp = new GameObject("ChannelHall_Pool").transform;
        grp.SetParent(env, false);
        grp.localPosition = new Vector3(cx, 0, 0);
        grp.localRotation = Quaternion.Euler(0, 90f, 0);
        BuildPool(grp, "Channel", Vector3.zero, 2.6f, 9.5f, 0.6f, true, true);

        // 수로 옆 큰 나무들 + 벽의 빛나는 아치 + 발광 구체
        for (int i = 0; i < 4; i++)
        {
            float x = x0 + 5f + i * 5.5f;
            Tree(DummiesRoot, "ChannelHall_Tree_N" + (i + 1), new Vector3(x, 0, -9.2f), 5.5f + (i % 2) * 1.2f);
            Tree(DummiesRoot, "ChannelHall_Tree_S" + (i + 1), new Vector3(x + 2.0f, 0, 9.2f), 5.0f + ((i + 1) % 2) * 1.4f);
        }
        for (int i = 0; i < 5; i++)
        {
            float x = x0 + 3.5f + i * 5.5f;
            ArchPanel(env, "ChannelHall_ArchN" + (i + 1), new Vector3(x, 0, -WingHalfZ + 0.32f), 0f, 3.0f, 2.6f);
            ArchPanel(env, "ChannelHall_ArchS" + (i + 1), new Vector3(x, 0, WingHalfZ - 0.32f), 180f, 3.0f, 2.6f);
        }
        float[] ox = { -6f, -2f, 1.5f, 5.5f, 8f };
        float[] oz = { 0.9f, -1.2f, 1.4f, -0.6f, 0.4f };
        for (int i = 0; i < ox.Length; i++)
            GlowOrb(DummiesRoot, "ChannelHall_Orb_" + (i + 1), new Vector3(cx + ox[i], 0, oz[i]), 0.28f + (i % 3) * 0.04f);

        // 실내처럼 따뜻한 보조광 (그림자 없음)
        for (int i = 0; i < 3; i++)
            PointLight(env, "ChannelHall_Light_" + (i + 1), new Vector3(x0 + 7f + i * 8f, 3.2f, 0), new Color(1f, 0.93f, 0.82f), 2.2f, 12f);
    }

    // ---- C: 석호 정원 (서쪽) ------------------------------------------------------
    static void BuildLagoonCourt(Transform env)
    {
        float x1 = -DeckHalfX, x0 = -WingOuterX;             // -50 ~ -22
        float cx = (x0 + x1) / 2f, len = x1 - x0;
        FloorWithHole(env, "LagoonCourt_Floor", x0, x1, -WingHalfZ, WingHalfZ, cx - 7.5f, cx + 7.5f, -7.0f, 7.0f);

        WingWalls(env, "LagoonCourt", cx, 0, len, WingHalfZ * 2f, openWest: false, openEast: true, openNorth: false, openSouth: false);

        // 큰 석호: 사방이 데크, 남북 끝에 계단.
        BuildPool(env, "Lagoon", new Vector3(cx, 0, 0), 7.5f, 7.0f, 0.9f, true, true);

        // 석호 속 야자수와 구체, 가장자리의 흰 기둥
        WaterPalm(DummiesRoot, "Lagoon_Palm_1", new Vector3(cx - 3.5f, 0, -3.0f), 3.6f);
        WaterPalm(DummiesRoot, "Lagoon_Palm_2", new Vector3(cx + 3.0f, 0, 1.5f), 3.2f);
        WaterPalm(DummiesRoot, "Lagoon_Palm_3", new Vector3(cx - 1.0f, 0, 4.5f), 3.8f);
        BigPillar(DummiesRoot, "Lagoon_Pillar_1", new Vector3(cx + 4.0f, 0, -4.5f), 5.0f);
        float[] ox = { -4.5f, -2.6f, 0.5f, 2.2f, 4.8f, -0.8f };
        float[] oz = { -2.2f, -4.4f, -1.0f, 3.8f, 0.2f, 5.8f };
        for (int i = 0; i < ox.Length; i++)
            GlowOrb(DummiesRoot, "Lagoon_Orb_" + (i + 1), new Vector3(cx + ox[i], 0, oz[i]), 0.28f + (i % 3) * 0.05f);

        // 데크 위: 선베드 두 쌍과 큰 화분
        Lounger(DummiesRoot, "LagoonCourt_Lounger_1", new Vector3(cx - 9.5f, 0, -8.5f), 90f);
        Lounger(DummiesRoot, "LagoonCourt_Lounger_2", new Vector3(cx - 9.5f, 0, 8.5f), 90f);
        SideTable(DummiesRoot, "LagoonCourt_Table_1", new Vector3(cx - 11.4f, 0, 8.5f));
        Parasol(DummiesRoot, "LagoonCourt_Parasol_1", new Vector3(cx - 11.0f, 0, -10.2f));
        Planter(DummiesRoot, "LagoonCourt_Planter_1", new Vector3(cx + 12.0f, 0, -10.5f), 2.8f);
        Planter(DummiesRoot, "LagoonCourt_Planter_2", new Vector3(cx + 12.0f, 0, 10.5f), 2.3f);

        for (int i = 0; i < 4; i++)
            ArchPanel(env, "LagoonCourt_ArchN" + (i + 1), new Vector3(x0 + 4f + i * 6.5f, 0, -WingHalfZ + 0.32f), 0f, 3.0f, 2.6f);
        PointLight(env, "LagoonCourt_Light_1", new Vector3(cx, 4.5f, 0), new Color(0.85f, 0.95f, 1f), 2.0f, 16f);
    }

    // ---- 터널 + E: 목표 방 (북쪽) ------------------------------------------------
    static void BuildTunnelAndGoal(Transform env)
    {
        float tz0 = -DeckHalfZ;                        // 터널 시작 = A 북쪽 벽 바깥면
        float tz1 = tz0 - TunnelLength;                // 터널 끝 = E 남쪽 벽
        Tunnel(env, "Tunnel", new Vector3(0, 0, tz0), -1, TunnelLength, DoorWidth, DoorStraightH);
        for (int i = 0; i < 4; i++)
            PointLight(env, "Tunnel_Light_" + (i + 1), new Vector3(0, 3.6f, tz0 - 2.5f - i * 5f), new Color(1f, 0.96f, 0.86f), 3.0f, 9f);

        // E 방
        float zc = (tz1 + -GoalZFar) / 2f, len = GoalZFar + tz1;    // tz1 = -48 → len = 22
        float ez = Mathf.Abs(len);
        Box(env, "Goal_Floor", new Vector3(0, -DeckThick / 2f, zc), new Vector3(GoalHalfX * 2f, DeckThick, ez), MatFloor, true);

        // 남쪽 벽(터널 쪽)만 아치 개구부, 나머지는 막힘
        float wh = WingWallHeight, h = wh / 2f;
        ArchWall(env, "Goal_Wall_South", new Vector3(0, 0, tz1), 0f, GoalHalfX * 2f + WallThick, wh, WallThick, 0f, DoorWidth, DoorStraightH, MatWall);
        Box(env, "Goal_Wall_North", new Vector3(0, h, -GoalZFar), new Vector3(GoalHalfX * 2f + WallThick, wh, WallThick), MatWall, true);
        Box(env, "Goal_Wall_West", new Vector3(-GoalHalfX, h, zc), new Vector3(WallThick, wh, ez), MatWall, true);
        Box(env, "Goal_Wall_East", new Vector3(GoalHalfX, h, zc), new Vector3(WallThick, wh, ez), MatWall, true);

        // 중앙 받침대와 목표 구체(자리표시 — 줍기 로직은 아직 없다)
        float pz = zc - 2f;
        Cyl(env, "Goal_Plinth", new Vector3(0, 0.3f, pz), new Vector3(3.6f, 0.3f, 3.6f), MatProp);
        var colHolder = env.Find("Goal_Plinth");
        var cc = colHolder.gameObject.AddComponent<CapsuleCollider>(); cc.radius = 0.5f; cc.height = 2f; cc.direction = 1;
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Goal_Orb";
        Object.DestroyImmediate(orb.GetComponent<Collider>());
        orb.transform.SetParent(env, false);
        orb.transform.localPosition = new Vector3(0, 1.5f, pz);
        orb.transform.localScale = Vector3.one * 0.9f;
        orb.GetComponent<MeshRenderer>().sharedMaterial = MatGlowOrb;
        PointLight(env, "Goal_OrbLight", new Vector3(0, 1.8f, pz), new Color(0.55f, 0.85f, 1f), 3.5f, 14f);
        PointLight(env, "Goal_Fill", new Vector3(0, 5f, zc + 4f), new Color(1f, 0.95f, 0.88f), 1.6f, 20f);

        // 벽을 따라 서 있는 흰 기둥과 아치 패널
        for (int i = 0; i < 3; i++)
        {
            float z = tz1 - 4f - i * 6f;
            WhiteColumn(env, "Goal_ColumnW" + (i + 1), new Vector3(-GoalHalfX + 1.0f, 0, z), 1.0f, wh);
            WhiteColumn(env, "Goal_ColumnE" + (i + 1), new Vector3(GoalHalfX - 1.0f, 0, z), 1.0f, wh);
        }
        for (int i = 0; i < 3; i++)
            ArchPanel(env, "Goal_ArchN" + (i + 1), new Vector3(-6f + i * 6f, 0, -GoalZFar + 0.32f), 0f, 3.0f, 2.8f);
    }

    // ---- 벽 세트 ----------------------------------------------------------------
    // 옆방 벽: 사방. 열린 쪽(A와 맞닿는 쪽)은 A의 아치 벽이 이미 있으므로 만들지 않는다.
    static void WingWalls(Transform env, string prefix, float cx, float cz, float lenX, float lenZ,
                          bool openWest, bool openEast, bool openNorth, bool openSouth)
    {
        float hx = lenX / 2f, hz = lenZ / 2f, h = WingWallHeight / 2f;
        if (!openNorth) Box(env, prefix + "_Wall_North", new Vector3(cx, h, cz - hz), new Vector3(lenX + WallThick, WingWallHeight, WallThick), MatWall, true);
        if (!openSouth) Box(env, prefix + "_Wall_South", new Vector3(cx, h, cz + hz), new Vector3(lenX + WallThick, WingWallHeight, WallThick), MatWall, true);
        if (!openWest) Box(env, prefix + "_Wall_West", new Vector3(cx - hx, h, cz), new Vector3(WallThick, WingWallHeight, lenZ), MatWall, true);
        if (!openEast) Box(env, prefix + "_Wall_East", new Vector3(cx + hx, h, cz), new Vector3(WallThick, WingWallHeight, lenZ), MatWall, true);
        // 바깥쪽 벽 기둥(스캘럽 느낌) — 벽에 반쯤 묻은 흰 원기둥
        for (float z = cz - hz + 4f; z <= cz + hz - 3f; z += 6.5f)
        {
            if (!openWest) WhiteColumn(env, prefix + "_ColW_" + z.ToString("0"), new Vector3(cx - hx + 0.2f, 0, z), 1.0f, WingWallHeight);
            if (!openEast) WhiteColumn(env, prefix + "_ColE_" + z.ToString("0"), new Vector3(cx + hx - 0.2f, 0, z), 1.0f, WingWallHeight);
        }
    }

    static void WhiteColumn(Transform p, string name, Vector3 pos, float radius, float height)
    {
        var go = Cyl(p, name, new Vector3(pos.x, height / 2f, pos.z), new Vector3(radius * 2f, height / 2f, radius * 2f), MatWall);
        go.AddComponent<CapsuleCollider>();
        var col = go.GetComponent<CapsuleCollider>();
        col.direction = 1; col.radius = 0.5f; col.height = 2f;
    }

    // ---- 나무 -------------------------------------------------------------------
    // 돌그릇 화분에 심은 큰 나무(Pool Glow 실내 사진). 줄기 + 납작한 수관 구체 몇 개.
    static void Tree(Transform p, string name, Vector3 pos, float height)
    {
        var g = Group(p, name, pos, 0f);
        Cyl(g, "Bowl", new Vector3(0, 0.3f, 0), new Vector3(2.2f, 0.3f, 2.2f), MatProp);
        Cyl(g, "Trunk", new Vector3(0, height / 2f, 0), new Vector3(0.45f, height / 2f, 0.45f), MatTrunk);
        Vector3[] offs = { new Vector3(0, 0.3f, 0), new Vector3(1.3f, -0.2f, 0.6f), new Vector3(-1.2f, -0.1f, -0.7f), new Vector3(0.3f, 0.1f, -1.3f) };
        for (int i = 0; i < offs.Length; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Crown_" + (i + 1);
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.transform.SetParent(g, false);
            s.transform.localPosition = new Vector3(0, height, 0) + offs[i];
            s.transform.localScale = new Vector3(3.4f - i * 0.3f, 2.1f, 3.4f - i * 0.3f);
            s.GetComponent<MeshRenderer>().sharedMaterial = MatPlant;
        }
        GroupCollider(g, new Vector3(0, 0.6f, 0), new Vector3(2.3f, 1.2f, 2.3f));
    }

    static void PointLight(Transform p, string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None; // 점 조명 그림자는 켜지 않는다(성능 기준)
    }

    // ================= 절차적 메시 =================================================
    // 아치 윤곽(폭 w, 직선 높이 hs): 왼쪽 아래 → 왼쪽 위 → 반원 → 오른쪽 위 → 오른쪽 아래.
    static List<Vector2> ArchProfile(float w, float hs)
    {
        var pts = new List<Vector2>();
        float r = w / 2f;
        pts.Add(new Vector2(-r, 0f));
        pts.Add(new Vector2(-r, hs));
        for (int i = 1; i < ArcSegments; i++)
        {
            float a = Mathf.PI - Mathf.PI * i / ArcSegments;   // 180° → 0°
            pts.Add(new Vector2(r * Mathf.Cos(a), hs + r * Mathf.Sin(a)));
        }
        pts.Add(new Vector2(r, hs));
        pts.Add(new Vector2(r, 0f));
        return pts;
    }

    // 아치 개구부가 뚫린 벽. 로컬: x = 벽 길이 방향, y = 위, z = 두께. 개구부 중심 u(벽 중앙 기준 x).
    // 앞면(-z)/뒷면(+z)을 아치선 위쪽 "스팬드럴" 쿼드와 좌우 사각형으로 채우고, 개구부 안쪽 면을 이어 붙인다.
    static GameObject ArchWall(Transform p, string name, Vector3 pos, float yaw, float length, float height, float thick,
                               float u, float doorW, float doorHs, Material mat)
    {
        var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
        float hl = length / 2f, ht = thick / 2f;
        var prof = ArchProfile(doorW, doorHs);        // 개구부 윤곽 (x는 u를 더해 이동)
        for (int i = 0; i < prof.Count; i++) prof[i] = new Vector2(prof[i].x + u, prof[i].y);

        // 좌/우 사각형, 아치 위 스팬드럴을 앞뒤로
        for (int side = 0; side < 2; side++)
        {
            float z = side == 0 ? -ht : ht;
            bool front = side == 1;   // AddQuad의 flip: z=-ht 면(법선 -z)은 false, z=+ht 면(법선 +z)은 true
            float left = prof[0].x, right = prof[prof.Count - 1].x;
            AddQuad(verts, tris, uvs, new Vector3(-hl, 0, z), new Vector3(left, 0, z), new Vector3(left, height, z), new Vector3(-hl, height, z), front);
            AddQuad(verts, tris, uvs, new Vector3(right, 0, z), new Vector3(hl, 0, z), new Vector3(hl, height, z), new Vector3(right, height, z), front);
            for (int i = 1; i < prof.Count - 2; i++)    // 수직 구간을 제외한 호 구간 (prof[1] 이후 호)
            {
                var a = prof[i]; var b = prof[i + 1];
                AddQuad(verts, tris, uvs, new Vector3(a.x, a.y, z), new Vector3(b.x, b.y, z), new Vector3(b.x, height, z), new Vector3(a.x, height, z), front);
            }
        }
        // 개구부 안쪽(좌측 문설주, 아치, 우측 문설주)
        for (int i = 0; i < prof.Count - 1; i++)
        {
            var a = prof[i]; var b = prof[i + 1];
            AddQuad(verts, tris, uvs, new Vector3(a.x, a.y, -ht), new Vector3(b.x, b.y, -ht), new Vector3(b.x, b.y, ht), new Vector3(a.x, a.y, ht), true);
        }
        // 위/양끝 마감(보이진 않지만 구멍 방지)
        AddQuad(verts, tris, uvs, new Vector3(-hl, height, -ht), new Vector3(hl, height, -ht), new Vector3(hl, height, ht), new Vector3(-hl, height, ht), false);
        AddQuad(verts, tris, uvs, new Vector3(-hl, 0, -ht), new Vector3(-hl, height, -ht), new Vector3(-hl, height, ht), new Vector3(-hl, 0, ht), false);
        AddQuad(verts, tris, uvs, new Vector3(hl, 0, ht), new Vector3(hl, height, ht), new Vector3(hl, height, -ht), new Vector3(hl, 0, -ht), false);

        return MakeMeshObject(p, name, pos, yaw, verts, tris, uvs, mat, true);
    }

    // 아치 단면을 따라 길게 뻗은 터널(안쪽 면만). dir: 시작점에서 z 방향(+1/-1)으로 length만큼.
    static GameObject Tunnel(Transform p, string name, Vector3 start, int dir, float length, float w, float hs)
    {
        var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
        var prof = ArchProfile(w, hs);
        float arc = 0f;
        float[] cum = new float[prof.Count];
        for (int i = 1; i < prof.Count; i++) { arc += Vector2.Distance(prof[i - 1], prof[i]); cum[i] = arc; }
        const float Repeat = 0.6f;
        for (int i = 0; i < prof.Count - 1; i++)
        {
            var a = prof[i]; var b = prof[i + 1];
            // 로컬 z = 0 → length. 안쪽을 보게 감는 순서(복도 안에서 바라볼 때 앞면).
            int v0 = verts.Count;
            verts.Add(new Vector3(a.x, a.y, 0)); verts.Add(new Vector3(b.x, b.y, 0));
            verts.Add(new Vector3(b.x, b.y, length)); verts.Add(new Vector3(a.x, a.y, length));
            uvs.Add(new Vector2(cum[i] / Repeat, 0)); uvs.Add(new Vector2(cum[i + 1] / Repeat, 0));
            uvs.Add(new Vector2(cum[i + 1] / Repeat, length / Repeat)); uvs.Add(new Vector2(cum[i] / Repeat, length / Repeat));
            tris.AddRange(new[] { v0, v0 + 1, v0 + 2, v0, v0 + 2, v0 + 3 }); // 법선이 터널 안쪽을 향하게
        }
        // 바닥은 따로 박스 — 터널 메시는 벽과 천장만
        var go = MakeMeshObject(p, name, start, dir > 0 ? 0f : 180f, verts, tris, uvs, MatPoolTile, true);
        Box(go.transform, name + "_Floor", new Vector3(0, -DeckThick / 2f, length / 2f), new Vector3(w, DeckThick, length), MatFloor, true);
        return go;
    }

    // 평평한 아치 모양 패널(빛나는 벽감). 로컬 +z를 향하는 앞면 한 장.
    static GameObject ArchPanel(Transform p, string name, Vector3 pos, float yaw, float w, float hs)
    {
        var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
        var prof = ArchProfile(w, hs);
        for (int i = 0; i < prof.Count; i++) { verts.Add(new Vector3(prof[i].x, prof[i].y, 0)); uvs.Add(prof[i]); }
        for (int i = 1; i < prof.Count - 1; i++) { tris.Add(0); tris.Add(i + 1); tris.Add(i); }
        // 안쪽 벽에서 살짝 띄운다(z-fight 방지)
        var go = MakeMeshObject(p, name, pos, yaw, verts, tris, uvs, MatArchGlow, false);
        return go;
    }

    static void AddQuad(List<Vector3> v, List<int> t, List<Vector2> uv, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool flip)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        uv.Add(new Vector2(a.x, a.y)); uv.Add(new Vector2(b.x, b.y)); uv.Add(new Vector2(c.x, c.y)); uv.Add(new Vector2(d.x, d.y));
        if (flip) t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        else t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
    }

    static GameObject MakeMeshObject(Transform p, string name, Vector3 pos, float yaw, List<Vector3> verts, List<int> tris,
                                     List<Vector2> uvs, Material mat, bool collide)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }
}
