using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 2026-11-18 사용자 피드백 2차: 홀 입구 방 천장, 인트로 출구 잠금(구경 후 열림), 천사 방 다음 하늘 계단 구역.
public static partial class GreyboxGraphicsPass
{
    // ---------------------------------------------------------------- 하늘 계단 구역 배치값
    // 천사 방과 같은 수영장(GB_05) 사이에 계단 구역을 넣으려고 GB_05 전체를 뒤(-z)로, 위(+y)로 옮긴다.
    // 계단은 바닥(y 0)에서 SkyRise만큼 올라가 같은 수영장 입구(높이 SkyRise)로 이어진다.
    // 2026-11-18 2차: 계단을 길게(8m → 12m, 40 → 60칸), 아래 발판도 길게. 같은 수영장은 그만큼 더 민다.
    const float SkyRise = 12f;     // 계단이 올라가는 높이(m). 0.2m 계단 60개.
    const float SkyShift = -28f;   // 같은 수영장을 뒤로 미는 거리(m)
    static readonly Vector3 SameBasePos = new Vector3(28.15f, 0f, -215f); // 옮기기 전 GB_05 루트 위치
    /// <summary>같은 수영장(GB_05)을 원래 자리에서 옮긴 양. GB_05 안의 좌표를 코드에 적을 때 더한다.</summary>
    static Vector3 SameOffset => new Vector3(0f, SkyRise, SkyShift);
    const string StairsRoot = "GB_04b_SkyStairs";
    // 떨어진 플레이어가 다시 태어나는 자리: 천사 방 가운데, 출구 쪽을 본다.
    static readonly Vector3 FallRespawnPos = new Vector3(28.15f, 0.05f, -121f);

    /// <summary>같은 수영장 루트를 정해진 자리에 둔다. 다시 실행해도 같은 자리다.</summary>
    static void PlaceSameIntro()
    {
        var root = Root("GB_05_SameIntro").transform;
        root.position = SameBasePos + SameOffset;
        EditorUtility.SetDirty(root);
    }

    /// <summary>
    /// 통로 문턱 표시(DoorSlot_Marker)를 보이지 않게 한다(렌더러만 끈다. 통로 막기가 켜고 끄는 오브젝트는 그대로 둔다).
    /// 2cm 두께면 타일이 들뜬 것처럼 보였고, 납작하게 해도 벽 앞·구름 바닥 띠 밖으로 삐져나온 검은 띠가 보였다(2026-11-18 플레이 피드백).
    /// 판 아래 바닥은 끊김 없이 이어져 있다. 납작하게 만드는 것도 남겨 둔다(다시 켜도 턱이 없게). 다시 실행해도 같은 결과다.
    /// </summary>
    static void FlattenThresholds()
    {
        const float thick = 0.003f;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "DoorSlot_Marker") continue;
                float parentY = t.parent != null ? t.parent.lossyScale.y : 1f;
                var s = t.localScale; s.y = thick / parentY; t.localScale = s;
                var p = t.position; p.y = (t.parent != null ? t.parent.position.y : 0f) + thick / 2f; t.position = p;
                if (t.TryGetComponent<Renderer>(out var r)) { r.enabled = false; EditorUtility.SetDirty(r); }
                EditorUtility.SetDirty(t);
            }
    }

    static GameObject RootOrCreate(string name)
    {
        foreach (var r in scene.GetRootGameObjects()) if (r.name == name) return r;
        return new GameObject(name);
    }

    // ---------------------------------------------------------------- ④-b 하늘 계단 구역(참고 이미지 07, 계단 아래는 칸칸이 벽)
    // 천사 방 출구 → 좁은 계단을 올라가면 하늘 벽에 뚫린 문 → 같은 수영장.
    // 위쪽 벽·천장은 천사 방과 같은 하늘이고, 발판 아래로는 칸칸이 선반이 층층이 쌓인 깊은 수직 통로다.
    // 난간은 없다. 떨어지면 화면이 검게 바뀌고 천사 방에서 다시 시작한다(FallRespawn).
    // 걷는 면은 계단 대신 보이지 않는 경사로다.
    static void ApplySkyStairs()
    {
        const float x0 = 22.15f, x1 = 34.15f, yB = -60f, yShelf = -1.5f;
        float yT = SkyRise + 8f;                       // 천장. 수영장 홀·같은 수영장 벽 너머로 보이지 않게 낮게 둔다.
        const float zN = -144.8f;                      // 천사 방 출구 통로 끝
        float zS = -144.7f + SkyShift;                 // 옮긴 같은 수영장 북쪽 벽 앞면
        const float dW = 26.65f, dE = 29.65f, doorH = 4.3f; // 문 자리(천사 방 통로, 같은 수영장 아치와 같다)
        const float zStair = -150f, run = 0.3f, rise = 0.2f, sw = 1.6f;
        int steps = Mathf.RoundToInt(SkyRise / rise);
        float zTop = zStair - run * steps;             // 마지막 계단 끝 = 위 발판 시작

        var root = RootOrCreate(StairsRoot);
        root.transform.position = new Vector3(28.15f, 0f, zN);
        var gen = Fresh(root.transform, GenName);

        var sky = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/GB_Angel_Sky.mat");
        var stone = Lit("GB_Stair_Stone", m => Plain(m, new Color(0.86f, 0.86f, 0.84f), 0.25f));
        var shelfMats = new[]
        {
            Lit("GB_Shaft_Shelf_0", m => Plain(m, new Color(0.84f, 0.79f, 0.64f), 0.2f)),
            Lit("GB_Shaft_Shelf_1", m => Plain(m, new Color(0.55f, 0.51f, 0.4f), 0.2f)),
            Lit("GB_Shaft_Shelf_2", m => Plain(m, new Color(0.22f, 0.2f, 0.16f), 0.2f)),
        };
        var shelfBack = Lit("GB_Shaft_Back", m => Plain(m, new Color(0.62f, 0.58f, 0.46f), 0.1f));
        var bottom = Lit("GB_Shaft_Bottom", m => Plain(m, new Color(0.01f, 0.01f, 0.01f), 0f));
        var clutterMats = new[]
        {
            Lit("GB_Shaft_Clutter_Paper", m => Plain(m, new Color(0.9f, 0.88f, 0.8f), 0.1f)),
            Lit("GB_Shaft_Clutter_Box", m => Plain(m, new Color(0.5f, 0.38f, 0.26f), 0.2f)),
            Lit("GB_Shaft_Clutter_Blue", m => Plain(m, new Color(0.35f, 0.42f, 0.6f), 0.3f)),
        };

        // 하늘 껍데기(콜라이더 없음). 0.04m 판. 선반 높이(yShelf)부터 천장까지.
        var shell = Group(gen, "Shell");
        void Wall(string n, bool alongX, float fixedPos, float a, float b, float y0, float y1)
        {
            if (b - a < 0.01f || y1 - y0 < 0.01f) return;
            var c = alongX ? new Vector3((a + b) / 2, (y0 + y1) / 2, fixedPos) : new Vector3(fixedPos, (y0 + y1) / 2, (a + b) / 2);
            var s = alongX ? new Vector3(b - a, y1 - y0, 0.04f) : new Vector3(0.04f, y1 - y0, b - a);
            Box(shell, n, c, s, sky, LAngel);
        }
        // 북쪽(천사 방 쪽): 문 자리 y 0~4.3 비움
        Wall("North_W", true, zN - 0.02f, x0, dW, yShelf, yT);
        Wall("North_E", true, zN - 0.02f, dE, x1, yShelf, yT);
        Wall("North_Below", true, zN - 0.02f, dW, dE, yShelf, -0.3f);
        Wall("North_Above", true, zN - 0.02f, dW, dE, doorH, yT);
        // 남쪽(같은 수영장 쪽): 문 자리 y SkyRise ~ SkyRise+4.3 비움
        Wall("South_W", true, zS + 0.04f, x0, dW, yShelf, yT);
        Wall("South_E", true, zS + 0.04f, dE, x1, yShelf, yT);
        Wall("South_Below", true, zS + 0.04f, dW, dE, yShelf, SkyRise - 0.3f);
        Wall("South_Above", true, zS + 0.04f, dW, dE, SkyRise + doorH, yT);
        Wall("West", false, x0, zS, zN, yShelf, yT);
        Wall("East", false, x1, zS, zN, yShelf, yT);
        Box(shell, "Ceiling", new Vector3((x0 + x1) / 2, yT, (zN + zS) / 2), new Vector3(x1 - x0, 0.04f, zN - zS), sky, LAngel);

        // 계단 아래: 칸칸이 선반 벽이 층층이 내려가는 수직 통로(2026-11-18 참고 이미지). 콜라이더 없음.
        // 깊어질수록 선반 색이 어두워지고 맨 아래는 검다.
        var shaft = Group(gen, "Shaft");
        const float cellW = 1.2f, cellH = 1.0f, depth = 0.7f, th = 0.06f;
        int levels = Mathf.RoundToInt((yShelf - yB) / cellH);
        Material ShelfMat(float y) => shelfMats[Mathf.Clamp((int)((yShelf - y) / (yShelf - yB) * 3f), 0, 2)];
        void ShelfWall(string n, int seed, Vector3 origin, Vector3 along, Vector3 inward, float length)
        {
            var g = Group(shaft, n);
            int cols = Mathf.Max(1, Mathf.RoundToInt(length / cellW));
            float cw = length / cols;
            Vector3 Sz(float a, float h, float d) => new Vector3(Mathf.Abs(along.x) * a + Mathf.Abs(inward.x) * d, h, Mathf.Abs(along.z) * a + Mathf.Abs(inward.z) * d);
            // 뒤판(깊이별 3토막)
            for (int k = 0; k < 3; k++)
            {
                float y0 = yShelf - (yShelf - yB) * (k + 1) / 3f, y1 = yShelf - (yShelf - yB) * k / 3f;
                var c = origin + along * (length / 2) + inward * 0.02f + Vector3.up * ((y0 + y1) / 2);
                Box(g, $"Back_{k}", c, Sz(length, y1 - y0, 0.04f), k == 0 ? shelfBack : shelfMats[k], LAngel);
            }
            // 가로 선반
            for (int i = 0; i <= levels; i++)
            {
                float y = yShelf - i * cellH;
                Box(g, $"Shelf_{i:00}", origin + along * (length / 2) + inward * (depth / 2) + Vector3.up * y, Sz(length, th, depth), ShelfMat(y), LAngel);
            }
            // 세로 칸막이(깊이별 3토막)
            for (int j = 0; j <= cols; j++)
                for (int k = 0; k < 3; k++)
                {
                    float y0 = yShelf - (yShelf - yB) * (k + 1) / 3f, y1 = yShelf - (yShelf - yB) * k / 3f;
                    Box(g, $"Div_{j:00}_{k}", origin + along * (j * cw) + inward * (depth / 2) + Vector3.up * ((y0 + y1) / 2), Sz(th, y1 - y0, depth), shelfMats[k], LAngel);
                }
            // 칸 안의 물건 몇 개(종이 더미, 상자)
            for (int j = 0; j < cols; j++)
                for (int i = 0; i < levels; i++)
                {
                    if (Hash(j, i, seed) > 0.12f) continue;
                    float w = cw * (0.3f + 0.4f * Hash(j, i, seed + 11)), hh = cellH * (0.15f + 0.5f * Hash(j, i, seed + 23));
                    float y = yShelf - (i + 1) * cellH + th / 2 + hh / 2;
                    var c = origin + along * (j * cw + cw * (0.3f + 0.4f * Hash(j, i, seed + 27))) + inward * (depth * 0.45f) + Vector3.up * y;
                    Box(g, $"Item_{j:00}_{i:00}", c, Sz(w, hh, depth * 0.6f), clutterMats[(int)(Hash(j, i, seed + 29) * 2.99f)], LAngel);
                }
        }
        ShelfWall("Shelves_West", 701, new Vector3(x0, 0, zN), Vector3.back, Vector3.right, zN - zS);
        ShelfWall("Shelves_East", 733, new Vector3(x1, 0, zS), Vector3.forward, Vector3.left, zN - zS);
        ShelfWall("Shelves_North", 757, new Vector3(x1, 0, zN), Vector3.left, Vector3.back, x1 - x0);
        ShelfWall("Shelves_South", 787, new Vector3(x0, 0, zS), Vector3.right, Vector3.forward, x1 - x0);
        Box(shaft, "Bottom", new Vector3((x0 + x1) / 2, yB, (zN + zS) / 2), new Vector3(x1 - x0, 0.04f, zN - zS), bottom, LAngel);

        // 걷는 곳: 아래 발판, 계단, 위 발판. 난간 없음.
        var walk = Group(gen, "Walkway");
        Box(walk, "Landing_Bottom", new Vector3(28.15f, -0.15f, (zN + zStair) / 2), new Vector3(dE - dW, 0.3f, zN - zStair), stone, LAngel, true);
        for (int i = 0; i < steps; i++)
            Box(walk, $"Step_{i:00}", new Vector3(28.15f, rise * (i + 1) - rise / 2, zStair - run * i - run / 2), new Vector3(sw, rise, run), stone, LAngel);
        // 계단 밑판(뒤 모서리를 잇는 기울어진 판). 보기용.
        {
            var a = new Vector3(28.15f, 0f, zStair - run);
            var b = new Vector3(28.15f, SkyRise - rise, zTop);
            var d = (b - a).normalized; var n = Vector3.Cross(d, Vector3.right).normalized; if (n.y < 0) n = -n;
            var c = (a + b) / 2 - n * 0.125f;
            Box(walk, "Stringer", c, new Vector3(sw, 0.25f, (b - a).magnitude + 0.3f), stone, LAngel, false, Quaternion.LookRotation(d, n).eulerAngles);
        }
        // 보이지 않는 경사로: 계단 앞 모서리를 잇는 면 위를 걷는다
        {
            var a = new Vector3(28.15f, rise, zStair);
            var b = new Vector3(28.15f, SkyRise, zStair - run * (steps - 1));
            var d = (b - a).normalized; var n = Vector3.Cross(d, Vector3.right).normalized; if (n.y < 0) n = -n;
            var ramp = new GameObject("Ramp_Collider");
            ramp.transform.SetParent(walk, true);
            ramp.transform.SetPositionAndRotation((a + b) / 2 - n * 0.1f, Quaternion.LookRotation(d, n));
            ramp.AddComponent<BoxCollider>().size = new Vector3(sw, 0.2f, (b - a).magnitude);
        }
        // 위 발판은 같은 수영장 북쪽 벽 두께(0.3m)까지 들어가 그쪽 바닥 끝(z -173.0)과 맞닿는다. 벽 앞면에서 끊으면 문 아래가 비어 보인다(2026-11-18).
        float topLen = zTop - (zS - 0.3f);
        Box(walk, "Landing_Top", new Vector3(28.15f, SkyRise - 0.15f, zTop - topLen / 2), new Vector3(dE - dW, 0.3f, topLen), stone, LAngel, true);

        // 떨어지면 다시 시작: 발판 한참 아래의 넓은 트리거
        var fall = new GameObject("FallRespawn");
        fall.transform.SetParent(gen, true);
        fall.transform.position = new Vector3((x0 + x1) / 2, -20f, (zN + zS) / 2);
        fall.layer = IgnoreRaycastLayer;
        var fbc = fall.AddComponent<BoxCollider>(); fbc.isTrigger = true; fbc.size = new Vector3(x1 - x0 + 2f, 8f, zN - zS + 2f);
        var spawn = new GameObject("RespawnPoint").transform;
        spawn.SetParent(fall.transform, true);
        spawn.SetPositionAndRotation(FallRespawnPos, Quaternion.Euler(0f, 180f, 0f));
        var fr = fall.AddComponent<FallRespawn>();
        fr.respawnPoint = spawn;
        EditorUtility.SetDirty(fr);

        // 천사 방과 같은 노출·안개
        var angelProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HDRP_GB_Angel_Profile.asset");
        LocalVolume(gen, "LocalVolume", new Vector3((x0 + x1) / 2, (yB + yT) / 2, (zN + zS) / 2), new Vector3(x1 - x0, yT - yB, zN - zS), 1f, 125f, angelProfile);
    }

    // ---------------------------------------------------------------- 홀 입구 방 천장
    // 입구 방(벽 4m)이 위가 뚫려 있어서, 그 뒤의 큰 벽(인트로 벽 11m, 홀 벽 8m)이 맨 판으로 드러나 더미처럼 보였다.
    // 천장을 덮어 실내(타일 현관)로 만들고 형광등을 단다. 그 너머 홀은 지금처럼 하늘이 열려 있다.
    static void ApplyHallEntryRoof()
    {
        var root = Root("GB_01_PoolHall");
        var gen = root.transform.Find(GenName);
        var roof = Group(gen, "EntryRoof");
        var ceil = Lit("GB_Hall_EntryCeiling", m => Textured(m, CeilingTileSet(), 2.4f, new Color(0.72f, 0.86f, 0.98f))); // 원래 누런 텍스처라 푸르게 눌러 회백색으로
        Box(roof, "Ceiling", new Vector3(0f, 4.15f, -77.7f), new Vector3(12.3f, 0.3f, 12.3f), ceil, LOutdoor);
        var cool = new Color(0.9f, 0.97f, 1f);
        foreach (float x in new[] { -2.6f, 2.6f })
            foreach (float z in new[] { -75.1f, -80.3f })
                Fluorescent(roof, $"Panel_{(x < 0 ? "W" : "E")}_{-z:F0}", new Vector3(x, 4f, z), true, 1.2f, 0.3f, cool, EntryPanelLumen, EntryPanelNits, Housing(), "GB_Hall_EntryPanelGlow", LOutdoor);
    }
    const float EntryPanelLumen = 260000f, EntryPanelNits = 4000f;

    // ---------------------------------------------------------------- 인트로 출구 잠금
    // 처음에는 가운데 출구가 벽으로 막혀 있다. 옆의 두 공간(동쪽 수로 홀, 서쪽 라군 마당)을 모두 둘러본 뒤
    // 출구 쪽을 보고 있지 않을 때 벽이 사라진다(RevealWhenExplored). 통로 안 조명도 그때 켠다.
    static void ApplyIntroExitGate(Light passageGlow)
    {
        var gen = Root("GB_00_IntroExit").transform.Find(GenName);
        var wallMat = Find("GB_00_IntroExit/Goal_Wall_North_0").GetComponent<MeshRenderer>();

        var gateGo = new GameObject("ExitGate");
        gateGo.transform.SetParent(gen, true);
        var bounds = new Bounds(new Vector3(0f, 1.5f, -70f), new Vector3(3f, 3f, 0.6f));
        var wall = Box(gateGo.transform, "Wall", bounds.center, bounds.size, wallMat.sharedMaterial, wallMat.renderingLayerMask, true);

        var gate = gateGo.AddComponent<RevealWhenExplored>();
        gate.wall = wall;
        gate.wallBounds = bounds;
        gate.requiredZones = new[] { "ChannelHall", "LagoonCourt" };
        gate.showOnReveal = new[] { passageGlow.gameObject };
        passageGlow.gameObject.SetActive(false);

        void Zone(string id, Vector3 c, Vector3 s)
        {
            var z = new GameObject("Visit_" + id);
            z.transform.SetParent(gateGo.transform, true);
            z.transform.position = c;
            z.layer = IgnoreRaycastLayer;
            var bc = z.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = s;
            var v = z.AddComponent<VisitZone>(); v.zoneId = id; v.target = gate;
        }
        Zone("ChannelHall", new Vector3(37f, 2f, 0f), new Vector3(24f, 4f, 24f)); // 동쪽 수로 홀(x 22~50) 안쪽
        Zone("LagoonCourt", new Vector3(-37f, 2f, 0f), new Vector3(24f, 4f, 24f)); // 서쪽 라군 마당(x -50~-22) 안쪽
        EditorUtility.SetDirty(gate);
    }

    // ---------------------------------------------------------------- 벽·기둥 높이(2026-11-18 2차)
    // 수영장 홀(벽 8m) 너머로 천사 방·하늘 계단의 하늘 상자가 보였다. 홀 벽과 기둥을 12m로 올린다.
    // 인트로(와 똑같은 마지막 수영장)의 큰 돌기둥은 그 구역 벽 높이까지 올린다(메인 마당 16m, 라군 마당 11m).
    // 바닥 높이는 그대로 두고 위만 늘린다. 이미 그 높이면 건드리지 않는다(다시 실행해도 같다).
    const float HallWallTop = 12f;
    static void ApplyHeights()
    {
        foreach (Transform t in Root("GB_01_PoolHall").transform)
            if ((t.name.StartsWith("Hall_Wall") || t.name.StartsWith("Pillar_")) && t.TryGetComponent<Renderer>(out var r) && r.bounds.max.y > 7.5f)
                RaiseTop(t, HallWallTop);
        foreach (var (rootName, dummies, baseY) in new[] { ("Dummies", "", 0f), ("GB_05_SameIntro", "Dummies", SkyRise) })
        {
            var d = dummies == "" ? Root(rootName).transform : Root(rootName).transform.Find(dummies);
            foreach (Transform t in d)
            {
                if (t.name.StartsWith("Prop_BigPillar")) RaiseTop(t, baseY + 16f);
                else if (t.name.StartsWith("Lagoon_Pillar")) RaiseTop(t, baseY + 11f);
            }
        }
    }

    /// <summary>바닥(렌더러 아래 끝)은 그대로 두고 위 끝이 topY가 되게 세로로 늘린다. 콜라이더도 같이 늘어난다.</summary>
    static void RaiseTop(Transform t, float topY)
    {
        var r = t.GetComponent<Renderer>();
        var b = r.bounds;
        if (Mathf.Abs(b.max.y - topY) < 0.01f || b.size.y < 0.01f) return;
        float oldMin = b.min.y;
        var s = t.localScale; s.y *= (topY - oldMin) / b.size.y; t.localScale = s;
        t.position += Vector3.up * (oldMin - r.bounds.min.y);
        EditorUtility.SetDirty(t);
    }
}
