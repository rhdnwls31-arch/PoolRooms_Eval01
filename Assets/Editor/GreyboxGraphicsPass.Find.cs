using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 2026-11-18 찾기 규칙(기획서 §4-1): 방마다 출구를 벽으로 막아 두고, 그 방의 "이상한 점"을 응시해 찾으면 연다.
// 이상한 점은 다음 출구를 가리킨다. 모두 그레이박스 더미다. 나중에 VARCO 3D 모델로 바꾼다.
// 막는 벽은 떠나는 방의 벽면과 같은 면·같은 재질로 채워 테두리가 드러나지 않게 한다(통로 막기 §7과 같은 원칙).
public static partial class GreyboxGraphicsPass
{
    static void ApplyFindRooms()
    {
        HallArrow();
        CrackDepthSigns();
        YellowClown();
        AngelOddEye();
        ApplyHiddenEnding();
    }

    // 출구를 막는 벽 + 출구 잠금. 벽 재질과 렌더링 레이어는 떠나는 방의 벽에서 가져온다.
    static RevealWhenExplored FindGate(Transform gen, string name, int order, string id, Bounds b, string refWallPath)
    {
        var refWall = Find(refWallPath).GetComponent<MeshRenderer>();
        var go = new GameObject(name);
        go.transform.SetParent(gen, true);
        // 벽은 빈 묶음 아래에 둔다. 걸레받이 같은 덧붙이를 크기가 늘어난 상자 아래에 붙이면 같이 늘어나 버린다(2026-11-18 노란 방).
        var wall = new GameObject("Wall");
        wall.transform.SetParent(go.transform, false);
        Box(wall.transform, "Box", b.center, b.size, refWall.sharedMaterial, refWall.renderingLayerMask, true);
        var gate = go.AddComponent<RevealWhenExplored>();
        gate.wall = wall;
        gate.wallBounds = b;
        gate.requiredZones = new[] { id };
        gate.order = order;
        EditorUtility.SetDirty(gate);
        return gate;
    }

    static GazeTarget AddGaze(GameObject go, string id, RevealWhenExplored gate, Bounds room)
    {
        var g = go.AddComponent<GazeTarget>();
        g.id = id; g.gate = gate; g.roomBounds = room;
        EditorUtility.SetDirty(g);
        return g;
    }

    // 벽에 붙는 표지: 얇은 받침 + 글자 면(Quad). normal = 벽에서 방 쪽으로 향하는 방향.
    static GameObject WallSign(Transform parent, string name, Vector3 wallPoint, Vector3 normal, float w, float h, Material face, Material back, uint layer)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, true);
        root.transform.SetPositionAndRotation(wallPoint, Quaternion.LookRotation(-normal));
        // 자식은 부모와 같은 회전이라 localScale의 x = 표지 가로, z = 두께(벽 쪽)다.
        var bk = Box(root.transform, "Back", wallPoint + normal * 0.01f, new Vector3(w + 0.04f, h + 0.04f, 0.02f), back, layer);
        bk.transform.rotation = root.transform.rotation;
        var q = Prim(PrimitiveType.Quad, root.transform, "Face", wallPoint + normal * 0.021f, new Vector3(w, h, 1f), face, layer, false);
        q.transform.rotation = root.transform.rotation; // Quad의 보이는 면은 -Z 쪽 = 방 쪽
        return root;
    }

    // ---------------------------------------------------------------- 수영장 홀: 빈 벽을 가리키는 출구 화살표
    static void HallArrow()
    {
        var gen = Root("GB_01_PoolHall").transform.Find(GenName);
        var room = new Bounds(new Vector3(0f, 6f, -103.7f), new Vector3(12f, 12f, 40f));
        var gate = FindGate(gen, "FindGate_Hall", 1, "HallArrow",
            new Bounds(new Vector3(6f, 1.1f, -106.7f), new Vector3(0.3f, 2.2f, 1.2f)), "GB_01_PoolHall/Hall_Wall_West_South");

        var tex = SignTexture("GB_Find_ArrowSign", 512, 256, new Color(0.08f, 0.55f, 0.32f), Color.white, ArrowGlyph);
        var lit = Lit("GB_Find_ArrowSign_Lit", m =>
        {
            Plain(m, Color.white, 0.4f); m.SetTexture("_BaseColorMap", tex);
            m.SetTexture("_EmissiveColorMap", tex); m.SetColor("_EmissiveColor", Color.white * 60f);
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
        });
        var off = Lit("GB_Find_ArrowSign_Off", m =>
        {
            Plain(m, new Color(0.32f, 0.33f, 0.32f), 0.3f); m.SetTexture("_BaseColorMap", tex);
            m.SetTexture("_EmissiveColorMap", null); m.SetColor("_EmissiveColor", Color.black);
        });
        var housing = Housing();
        uint layer = Find("GB_01_PoolHall/Hall_Wall_West_South").GetComponent<MeshRenderer>().renderingLayerMask;

        // 홀 벽(x 5.85) 출구 3.8m 앞, 출구(-z) 쪽을 가리킨다. 홀 안에서 걸어오면 정면 오른쪽에 보인다.
        var sign = new GameObject("Oddity_ArrowSign");
        sign.transform.SetParent(gen, true);
        var litGo = WallSign(sign.transform, "Lit", new Vector3(5.85f, 2.3f, -102.9f), Vector3.left, 0.7f, 0.35f, lit, housing, layer);
        var offGo = WallSign(sign.transform, "Off", new Vector3(5.85f, 2.3f, -102.9f), Vector3.left, 0.7f, 0.35f, off, housing, layer);
        offGo.SetActive(false);
        var g = AddGaze(sign, "HallArrow", gate, room);
        g.hideOnFound = new[] { litGo }; g.showOnFound = new[] { offGo };
    }

    // ---------------------------------------------------------------- 금 간 복도: 1.2m · 1.2m · 1.2m · 5m (마네킹은 미끼)
    static void CrackDepthSigns()
    {
        var gen = Root("GB_02_Crack").transform.Find(GenName);
        var room = new Bounds(new Vector3(13f, 2f, -106.7f), new Vector3(13.6f, 4f, 6f));
        // 2026-11-18: 출구는 북쪽 벽(샤워실로 가는 구멍, x 8.9~10.1). 복도 벽 + 샤워실 남쪽 벽 두께를 채운다.
        var gate = FindGate(gen, "FindGate_Crack", 2, "CrackDepth",
            new Bounds(new Vector3(ShInX, 1.1f, -103.4f), new Vector3(1.2f, 2.2f, 0.6f)), "GB_02_Crack/Wall_North_0");

        var t12 = SignTexture("GB_Find_Depth_1_2m", 512, 256, new Color(0.93f, 0.94f, 0.92f), new Color(0.1f, 0.18f, 0.42f), TextGlyph("1.2m"));
        var t5 = SignTexture("GB_Find_Depth_5m", 512, 256, new Color(0.93f, 0.94f, 0.92f), new Color(0.1f, 0.18f, 0.42f), TextGlyph("5m"));
        // 2026-11-23 VARCO 2D 수심 표지(16:9). 있으면 그림을 쓰고 표지 높이를 비율에 맞춘다(폭 0.5 그대로).
        var i12 = Img2D("Crack", "DepthSign_1_2m"); var i5 = Img2D("Crack", "DepthSign_5m");
        if (i12 != null) t12 = i12;
        if (i5 != null) t5 = i5;
        float sh = i12 != null ? 0.5f / Img2DAspect(i12) : 0.25f;
        var m12 = Lit("GB_Find_Depth_1_2m", m => { Plain(m, Color.white, 0.6f); m.SetTexture("_BaseColorMap", t12); });
        var m5 = Lit("GB_Find_Depth_5m", m => { Plain(m, Color.white, 0.6f); m.SetTexture("_BaseColorMap", t5); });
        var back = Lit("GB_Find_SignBack", m => Plain(m, new Color(0.85f, 0.86f, 0.84f), 0.5f));

        // 북쪽 벽(z -103.7) 1.5m 높이. 5m 표지는 출구(x 9.5) 바로 옆, 나머지 1.2m는 마네킹(x 15)을 사이에 두고 동쪽으로.
        var signs = new GameObject("DepthSigns"); signs.transform.SetParent(gen, true);
        foreach (float x in new[] { 12.8f, 16.1f, 18.9f })
            WallSign(signs.transform, $"Depth_1_2m_{x:F0}", new Vector3(x, 1.5f, -103.7f), Vector3.back, 0.5f, sh, m12, back, LCrack);
        var odd = WallSign(gen, "Oddity_Depth_5m", new Vector3(10.95f, 1.5f, -103.7f), Vector3.back, 0.5f, sh, m5, back, LCrack);
        var g = AddGaze(odd, "CrackDepth", gate, room);
        g.blinkRenderers = new[] { odd.transform.Find("Face").GetComponent<Renderer>() };
        g.blinkCount = 2;
    }

    // ---------------------------------------------------------------- 노란 방: 풍선을 든 키다리 삐에로
    // 2026-11-19: 풍선 → 열쇠 장치는 잘 안 보여서 뺐다(사용자 결정).
    // 삐에로는 입구(서쪽 벽, 샤워실에서 오는 구멍)를 보고 서 있다. 가까이 다가가 바라보면 풍선이 손을 떠나
    // 천장을 타고 출구 벽까지 흘러가고, 도착할 즈음 출구가 열린다(2026-11-19 사용자 결정: 다가가면 날아간다, 처음엔 기울지 않는다).
    static void YellowClown()
    {
        var gen = Root("GB_03_Yellow").transform.Find(GenName);
        var room = new Bounds(new Vector3(28.15f, 1.5f, -106.4f), new Vector3(16f, 3f, 16f));
        // 2026-11-18: 출구는 동쪽 벽(거실로 가는 구멍, z -113.3~-111.5).
        var gate = FindGate(gen, "FindGate_Yellow", 4, "YellowClown",
            new Bounds(new Vector3(36.3f, 1.2f, LvInZ), new Vector3(0.3f, 2.4f, 1.8f)), "GB_03_Yellow/Wall_East_0");
        var trim = gen.Find("Baseboard/E_A");
        if (trim != null && trim.TryGetComponent<MeshRenderer>(out var tr))
            Box(gate.wall.transform, "Baseboard", new Vector3(36.13f, 0.05f, LvInZ), new Vector3(0.04f, 0.1f, 1.8f), tr.sharedMaterial, tr.renderingLayerMask);

        var white = Lit("GB_Find_Clown_Face", m => Plain(m, new Color(0.95f, 0.93f, 0.9f), 0.35f));
        var red = Lit("GB_Find_Clown_Red", m => Plain(m, new Color(0.78f, 0.08f, 0.08f), 0.45f));
        var suit = Lit("GB_Find_Clown_Suit", m => Plain(m, new Color(0.25f, 0.55f, 0.85f), 0.3f));
        var dark = Lit("GB_Find_Clown_Dark", m => Plain(m, new Color(0.05f, 0.05f, 0.06f), 0.3f));

        // 방 남동쪽에 서서 입구(서쪽 벽 x 20.15, z ShOutZ)를 바라본다. 들어오자마자 대각선 건너편에서 마주 본다.
        var pos = new Vector3(32f, 0f, -109.5f);
        var entry = new Vector3(20.15f, 0f, ShOutZ);
        var doll = new GameObject("Oddity_Clown").transform;
        doll.SetParent(gen, true);
        doll.SetPositionAndRotation(pos, Quaternion.LookRotation(entry - pos));
        var bc = doll.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(0.5f, 1.05f, 0.4f); bc.center = new Vector3(0, 0.52f, 0);
        Transform balloon; Vector3 handLocal;
        void P(PrimitiveType t, Transform p, string n, Vector3 lc, Vector3 s, Material m, Vector3? e = null)
        {
            var go = GameObject.CreatePrimitive(t); go.name = n; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(p, false); go.transform.localPosition = lc; go.transform.localScale = s;
            if (e.HasValue) go.transform.localRotation = Quaternion.Euler(e.Value);
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.renderingLayerMask = LYellow;
        }
        if (PierrotParts(out var pBody, out var pBalloon, out var pMat))
        {
            // 2026-11-19 VARCO 키다리 삐에로: 플레이어(1.8m)보다 커서 올려다보게, 모자·풍선 끝까지 2.7m(천장 3m).
            const float s = 2.7f / 2.1f;
            MeshPart(doll, "Body", pBody, pMat, LYellow, s);
            balloon = MeshPart(doll, "Balloon", pBalloon, pMat, LYellow, s).transform;
            handLocal = PierrotHand * s;
            bc.size = new Vector3(0.75f, 2.1f, 0.73f) * s; bc.center = new Vector3(0, 1.05f * s, 0);
        }
        else
        {
        P(PrimitiveType.Capsule, doll, "Body", new Vector3(0, 0.42f, 0), new Vector3(0.36f, 0.32f, 0.3f), suit);
        P(PrimitiveType.Sphere, doll, "Ruff", new Vector3(0, 0.7f, 0), new Vector3(0.36f, 0.08f, 0.36f), white);
        P(PrimitiveType.Sphere, doll, "Head", new Vector3(0, 0.85f, 0), new Vector3(0.26f, 0.28f, 0.26f), white);
        P(PrimitiveType.Sphere, doll, "Nose", new Vector3(0, 0.85f, 0.13f), Vector3.one * 0.06f, red);
        P(PrimitiveType.Sphere, doll, "Eye_L", new Vector3(-0.055f, 0.9f, 0.115f), Vector3.one * 0.03f, dark);
        P(PrimitiveType.Sphere, doll, "Eye_R", new Vector3(0.055f, 0.9f, 0.115f), Vector3.one * 0.03f, dark);
        P(PrimitiveType.Cylinder, doll, "Hat", new Vector3(0, 1.02f, 0), new Vector3(0.12f, 0.08f, 0.12f), red);
        P(PrimitiveType.Capsule, doll, "Arm_L", new Vector3(-0.2f, 0.5f, 0.05f), new Vector3(0.09f, 0.16f, 0.09f), suit, new Vector3(20, 0, 15));
        P(PrimitiveType.Capsule, doll, "Arm_R", new Vector3(0.21f, 0.58f, 0.1f), new Vector3(0.09f, 0.16f, 0.09f), suit, new Vector3(-50, 0, -10));
        P(PrimitiveType.Capsule, doll, "Leg_L", new Vector3(-0.08f, 0.12f, 0), new Vector3(0.11f, 0.12f, 0.11f), suit);
        P(PrimitiveType.Capsule, doll, "Leg_R", new Vector3(0.08f, 0.12f, 0), new Vector3(0.11f, 0.12f, 0.11f), suit);

        var hand = new Vector3(0.24f, 0.62f, 0.22f);
        handLocal = hand;
        balloon = new GameObject("Balloon").transform; balloon.SetParent(doll, false);
        P(PrimitiveType.Cylinder, balloon, "String", hand + new Vector3(0, 0.35f, 0), new Vector3(0.006f, 0.35f, 0.006f), dark);
        P(PrimitiveType.Sphere, balloon, "Ball", hand + new Vector3(0, 0.85f, 0), new Vector3(0.28f, 0.34f, 0.28f), red);
        }
        // 풍선 축: 손 위치. 풍선 메쉬를 그 아래로 옮겨(월드 위치 유지) 손을 중심으로 기울고 날아간다.
        var pivot = new GameObject("BalloonPivot").transform;
        pivot.SetParent(doll, false); pivot.localPosition = handLocal;
        balloon.SetParent(pivot, true);
        var release = new GameObject("BalloonRelease"); release.transform.SetParent(doll, false); release.SetActive(false);
        var flight = pivot.gameObject.AddComponent<BalloonFlight>();
        flight.releaseSignal = release;
        flight.idleLean = 0f;                                // 처음에는 기울지 않는다(문 쪽을 미리 알려 주지 않게)
        flight.riseToY = 2.1f;                               // 풍선 끝 ≈ 2.94m(천장 3m 바로 아래)
        flight.stopAt = new Vector3(35.75f, 2.1f, LvInZ);    // 출구(동쪽 벽 36.15) 문 위, 풍선이 벽에 닿기 직전
        EditorUtility.SetDirty(flight);

        var g = AddGaze(doll.gameObject, "YellowClown", gate, room);
        g.showOnFound = new[] { release };
        g.maxDistance = 3f; g.dwellTime = 0.4f;               // 가까이(3m 안) 다가가 바라보면 바로 날아간다
        g.notifyDelay = flight.riseTime + flight.driftTime - 0.3f; // 풍선이 문 위에 닿을 즈음 열린다
    }

    // ---------------------------------------------------------------- 천사 방: 혼자 출구 쪽 벽을 보는 눈
    static void AngelOddEye()
    {
        var gen = Root("GB_04_Angel").transform.Find(GenName);
        var room = new Bounds(new Vector3(28.15f, 6f, -128.85f), new Vector3(20.6f, 12f, 28.3f));
        var gate = FindGate(gen, "FindGate_Angel", 10, "AngelOddEye",
            new Bounds(new Vector3(28.15f, 2.15f, -142.85f), new Vector3(3f, 4.3f, 0.3f)), "GB_04_Angel/Wall_South_0");

        // 날개 달린 눈 8개는 모두 플레이어를 따라 돈다. 이 하나만 멈춰서 출구 자리를 본다.
        var eye = Find("GB_04_Angel/Placeholder_Props/GFX_Props/WingedEyes/WingedEye_02");
        if (eye.TryGetComponent<FacePlayer>(out var fp)) Object.DestroyImmediate(fp);
        eye.rotation = Quaternion.LookRotation(gate.wallBounds.center - eye.position, Vector3.up);
        var g = AddGaze(eye.gameObject, "AngelOddEye", gate, room);
        g.blinkCount = 2; g.blinkOff = 0.3f;
        g.maxDistance = 9f; // 눈이 6.4m 높이에 떠 있어 6m로는 바로 밑에서만 닿는다
        if (eye.Find("Iris") == null)
        {
            // VARCO 모델(한 덩어리): 찾으면 눈 모델 오브젝트째 껐다 켠다(2026-11-19 사용자 결정).
            // 판정은 날개를 뺀 눈알 크기의 빈 오브젝트(렌더러 없음)로 한다. 예전에 렌더러 달린 상자를 쓰다
            // 깜빡일 때 재질 없는 보라색 상자가 보였다.
            g.blinkObjects = new[] { eye.Find("WingedEye").gameObject };
            var core = new GameObject("GazeCore"); core.transform.SetParent(eye, false);
            core.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f); // 눈알 지름 약 0.9m(scale 1 기준)
            g.gazeBox = core.transform;
            g.boundsPadding = 0.1f;
            return;
        }
        g.blinkRenderers = new[] { eye.Find("Iris").GetComponent<Renderer>(), eye.Find("Pupil").GetComponent<Renderer>() };
        // 날개 끝을 봐도 열리던 것을 막는다(2026-11-18 플레이 피드백): 눈알을 정면으로 겨눠야 찾은 것이다.
        g.gazeRenderers = new[] { eye.Find("Eye").GetComponent<Renderer>(), eye.Find("Iris").GetComponent<Renderer>(), eye.Find("Pupil").GetComponent<Renderer>() };
        g.boundsPadding = 0f;
        g.maxDistance = 9f; // 눈이 6.4m 높이에 떠 있어 6m로는 바로 밑에서만 닿는다
    }

    // ---------------------------------------------------------------- 표지 텍스처(점 글꼴)

    delegate bool GlyphFn(float u, float v); // u, v = 0~1 (글자 칸 안), 참이면 글자 색

    static Texture2D SignTexture(string name, int w, int h, Color bg, Color fg, GlyphFn glyph)
    {
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                bool border = u < 0.03f || u > 0.97f || v < 0.06f || v > 0.94f;
                bool inner = !border && (u < 0.045f || u > 0.955f || v < 0.09f || v > 0.91f);
                px[y * w + x] = inner ? bg : border ? fg : glyph(u, v) ? fg : bg;
            }
        Directory.CreateDirectory(TexDir);
        string path = $"{TexDir}/{name}.png";
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true; imp.mipmapEnabled = true; imp.anisoLevel = 4;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 오른쪽을 가리키는 화살표
    static bool ArrowGlyph(float u, float v)
    {
        float y = Mathf.Abs(v - 0.5f);
        bool shaft = u > 0.18f && u < 0.62f && y < 0.09f;
        bool head = u >= 0.58f && u < 0.82f && y < (0.82f - u) * 1.25f;
        return shaft || head;
    }

    static readonly Dictionary<char, string[]> Font5x7 = new Dictionary<char, string[]>
    {
        ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
        ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
        ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
        ['.'] = new[] { "...", "...", "...", "...", "...", "...", ".#." },
        ['m'] = new[] { ".....", ".....", "##.#.", "#.#.#", "#.#.#", "#.#.#", "#.#.#" },
    };

    // 글자 줄을 표지 가운데에 놓는다. 글자 높이는 표지 높이의 55%.
    static GlyphFn TextGlyph(string text)
    {
        int cols = 0;
        foreach (char c in text) cols += Font5x7[c][0].Length + 1;
        cols -= 1;
        const float aspect = 2f; // 표지 가로:세로
        float cellV = 0.55f / 7f, cellU = cellV / aspect;
        float u0 = 0.5f - cols * cellU / 2f, v0 = 0.5f - 0.55f / 2f;
        return (u, v) =>
        {
            int cx = Mathf.FloorToInt((u - u0) / cellU), cy = 6 - Mathf.FloorToInt((v - v0) / cellV);
            if (cx < 0 || cy < 0 || cy > 6) return false;
            foreach (char c in text)
            {
                var rows = Font5x7[c]; int wdt = rows[0].Length;
                if (cx < wdt) return rows[cy][cx] == '#';
                cx -= wdt + 1;
                if (cx < 0) return false;
            }
            return false;
        };
    }
}
