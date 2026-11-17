using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// 리미널 배경 소품(C)과 데칼(B-2, B-5), 물 위 발광 구체(D-2).
// 소품은 전부 프리미티브 더미이고 각 방의 Placeholder_Props/GFX_Props 아래에 둔다(나중에 VARCO 모델로 교체).
// "이상한 것"이 아니라 평범한 배경이다. 동선(문과 문 사이)은 비워 둔다.
public static partial class GreyboxGraphicsPass
{
    static Material pMetal, pWhite, pDark, pWood, pRed, pBlue, pGrey, pCanvas;

    static void PropMaterials()
    {
        pMetal = Lit("GB_Prop_LockerMetal", m => Plain(m, new Color(0.45f, 0.53f, 0.56f), 0.5f, 0.6f));
        pWhite = Lit("GB_Prop_White", m => Plain(m, new Color(0.86f, 0.86f, 0.82f), 0.35f));
        pDark = Lit("GB_Prop_Dark", m => Plain(m, new Color(0.03f, 0.03f, 0.035f), 0.85f));
        pWood = Lit("GB_Prop_Wood", m => Plain(m, new Color(0.36f, 0.25f, 0.14f), 0.3f));
        pRed = Lit("GB_Prop_Orange", m => Plain(m, new Color(0.85f, 0.3f, 0.12f), 0.4f));
        pBlue = Lit("GB_Prop_SignBlue", m => Plain(m, new Color(0.15f, 0.32f, 0.55f), 0.3f));
        pGrey = Lit("GB_Prop_Grey", m => Plain(m, new Color(0.42f, 0.42f, 0.4f), 0.3f));
        pCanvas = Lit("GB_Prop_Canvas", m => Plain(m, new Color(0.72f, 0.68f, 0.56f), 0.1f));
    }

    static uint propLayer;

    /// <summary>방의 Placeholder_Props(없으면 만든다) 아래 GFX_Props를 새로 만든다.</summary>
    static Transform PropsRoot(GameObject room, uint layer)
    {
        propLayer = layer;
        var ph = room.transform.Find("Placeholder_Props");
        if (ph == null) { ph = new GameObject("Placeholder_Props").transform; ph.SetParent(room.transform, false); }
        return Fresh(ph, PropsName);
    }

    /// <summary>pos에 yaw로 돌린 소품 그룹. 자식은 로컬 좌표(앞 = +Z)로 붙인다.</summary>
    static Transform PropGroup(Transform parent, string name, Vector3 pos, float yaw, Vector3? collider = null)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, true);
        g.position = pos; g.rotation = Quaternion.Euler(0, yaw, 0);
        if (collider.HasValue)
        {
            var bc = g.gameObject.AddComponent<BoxCollider>();
            bc.size = collider.Value; bc.center = new Vector3(0, collider.Value.y / 2, 0);
        }
        return g;
    }

    static void Part(Transform g, PrimitiveType type, string name, Vector3 localCenter, Vector3 size, Material m, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(g, false);
        go.transform.localPosition = localCenter; go.transform.localScale = size;
        if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = m; r.renderingLayerMask = propLayer;
    }

    static void PBox(Transform g, string name, Vector3 c, Vector3 s, Material m) => Part(g, PrimitiveType.Cube, name, c, s, m);

    static void FoldingChair(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "FoldingChair", pos, yaw, new Vector3(0.45f, 0.85f, 0.45f));
        PBox(g, "Seat", new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.03f, 0.4f), pGrey);
        PBox(g, "Back", new Vector3(0, 0.74f, -0.2f), new Vector3(0.42f, 0.22f, 0.025f), pGrey);
        foreach (float x in new[] { -0.2f, 0.2f })
        {
            Part(g, PrimitiveType.Cube, "Leg", new Vector3(x, 0.42f, 0), new Vector3(0.02f, 0.95f, 0.02f), pMetal, new Vector3(22, 0, 0));
            Part(g, PrimitiveType.Cube, "Leg", new Vector3(x, 0.23f, 0), new Vector3(0.02f, 0.5f, 0.02f), pMetal, new Vector3(-28, 0, 0));
        }
    }

    static void Lockers(Transform parent, Vector3 pos, float yaw, int count)
    {
        const float w = 0.38f, h = 1.8f, d = 0.45f;
        var g = PropGroup(parent, "Lockers", pos, yaw, new Vector3(w * count, h, d));
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) / 2f) * w;
            PBox(g, "Body", new Vector3(x, h / 2, 0), new Vector3(w - 0.012f, h, d), pMetal);
            PBox(g, "Vent", new Vector3(x, h - 0.22f, d / 2 + 0.002f), new Vector3(w * 0.55f, 0.1f, 0.004f), pDark);
            PBox(g, "Handle", new Vector3(x + w * 0.3f, h * 0.52f, d / 2 + 0.01f), new Vector3(0.02f, 0.1f, 0.02f), pGrey);
        }
    }

    // 벽걸이 소품: pos는 벽면 위의 점, yaw는 벽에서 방 안쪽을 보는 방향.
    static void LifeRing(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "LifeRingHolder", pos, yaw);
        PBox(g, "Board", new Vector3(0, 0, 0.012f), new Vector3(0.8f, 0.8f, 0.024f), pWhite);
        Part(g, PrimitiveType.Cylinder, "Ring", new Vector3(0, 0, 0.07f), new Vector3(0.62f, 0.045f, 0.62f), pRed, new Vector3(90, 0, 0));
        Part(g, PrimitiveType.Cylinder, "RingHole", new Vector3(0, 0, 0.072f), new Vector3(0.34f, 0.046f, 0.34f), pWhite, new Vector3(90, 0, 0));
    }

    static void SignBoard(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "SafetySign_Blank", pos, yaw);
        PBox(g, "Board", new Vector3(0, 0, 0.01f), new Vector3(0.6f, 0.8f, 0.02f), pWhite);
        PBox(g, "Header", new Vector3(0, 0.31f, 0.022f), new Vector3(0.6f, 0.16f, 0.004f), pBlue);
        for (int i = 0; i < 5; i++) // 글자 없는 줄
            PBox(g, "Line", new Vector3(-0.04f * (i % 2), 0.14f - i * 0.1f, 0.022f), new Vector3(0.44f - 0.08f * (i % 2), 0.025f, 0.004f), pGrey);
    }

    static void Clock(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "WallClock_NoHands", pos, yaw);
        Part(g, PrimitiveType.Cylinder, "Rim", new Vector3(0, 0, 0.02f), new Vector3(0.4f, 0.02f, 0.4f), pDark, new Vector3(90, 0, 0));
        Part(g, PrimitiveType.Cylinder, "Face", new Vector3(0, 0, 0.03f), new Vector3(0.35f, 0.021f, 0.35f), pWhite, new Vector3(90, 0, 0));
    }

    static void Drain(Transform parent, Vector3 pos)
    {
        var g = PropGroup(parent, "DrainCover", pos, 0);
        PBox(g, "Frame", new Vector3(0, 0.004f, 0), new Vector3(0.32f, 0.008f, 0.32f), pGrey);
        for (int i = 0; i < 5; i++) PBox(g, "Slot", new Vector3(-0.1f + i * 0.05f, 0.009f, 0), new Vector3(0.022f, 0.004f, 0.26f), pDark);
    }

    static void Frame(Transform parent, Vector3 pos, float yaw, float w, float h)
    {
        var g = PropGroup(parent, "PictureFrame_Blank", pos, yaw);
        PBox(g, "Frame", new Vector3(0, 0, 0.015f), new Vector3(w, h, 0.03f), pWood);
        PBox(g, "Canvas", new Vector3(0, 0, 0.031f), new Vector3(w - 0.08f, h - 0.08f, 0.004f), pCanvas);
    }

    static void DeskAndChair(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "EmptyDesk", pos, yaw, new Vector3(1.4f, 0.76f, 0.7f));
        PBox(g, "Top", new Vector3(0, 0.74f, 0), new Vector3(1.4f, 0.04f, 0.7f), pWood);
        PBox(g, "SideL", new Vector3(-0.67f, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.66f), pWood);
        PBox(g, "SideR", new Vector3(0.67f, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.66f), pWood);
        PBox(g, "Back", new Vector3(0, 0.45f, -0.32f), new Vector3(1.3f, 0.5f, 0.02f), pWood);
        var c = PropGroup(parent, "DeskChair", pos + g.forward * 0.75f, yaw + 180f, new Vector3(0.45f, 0.9f, 0.45f));
        PBox(c, "Seat", new Vector3(0, 0.46f, 0), new Vector3(0.44f, 0.06f, 0.44f), pGrey);
        PBox(c, "Back", new Vector3(0, 0.8f, -0.2f), new Vector3(0.42f, 0.4f, 0.04f), pGrey);
        Part(c, PrimitiveType.Cylinder, "Post", new Vector3(0, 0.22f, 0), new Vector3(0.05f, 0.22f, 0.05f), pMetal);
        Part(c, PrimitiveType.Cylinder, "Base", new Vector3(0, 0.015f, 0), new Vector3(0.5f, 0.015f, 0.5f), pMetal);
    }

    static void TvOnStand(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "TV_Off", pos, yaw, new Vector3(1.1f, 1.15f, 0.45f));
        PBox(g, "Stand", new Vector3(0, 0.25f, 0), new Vector3(1.1f, 0.5f, 0.42f), pWood);
        PBox(g, "Body", new Vector3(0, 0.82f, 0), new Vector3(0.9f, 0.6f, 0.34f), pGrey);
        PBox(g, "Screen", new Vector3(0, 0.83f, 0.172f), new Vector3(0.76f, 0.48f, 0.004f), pDark);
    }

    // ---------------------------------------------------------------- 데칼

    static Material DecalMat(string name, Texture2D tex, Color color, float smoothness, bool albedo)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("HDRP/Decal")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseColorMap", tex); m.SetColor("_BaseColor", color);
        m.SetFloat("_AffectAlbedo", albedo ? 1 : 0); m.SetFloat("_AffectNormal", 0); m.SetFloat("_AffectMetal", 0); m.SetFloat("_AffectAO", 0);
        m.SetFloat("_AffectSmoothness", 1); m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_MaskBlendSrc", 0); // 알파(텍스처 모양)로 섞는다
        m.SetFloat("_DecalBlend", 1f);
        HDShaderUtils.ResetMaterialKeywords(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>forward 방향으로 투영하는 데칼. pos는 표면에서 0.15m 떨어진 점.</summary>
    static void Decal(Transform parent, string name, Material m, Vector3 pos, Vector3 euler, float w, float h, float fade = 1f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(euler);
        var d = go.AddComponent<DecalProjector>();
        d.material = m; d.size = new Vector3(w, h, 0.4f); d.pivot = new Vector3(0, 0, 0.2f); d.fadeFactor = fade;
        d.startAngleFade = 60f; d.endAngleFade = 85f; // 옆면으로 번지지 않게
    }

    // 금 간 자국: 위에서 아래로 갈라지며 가지를 친다. 알파 = 금.
    static Texture2D CrackTexture(string name, int seed)
    {
        const int size = 512;
        var segs = new List<(Vector2 a, Vector2 b, float w)>();
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();
        void Grow(Vector2 p, float ang, float width, int depth)
        {
            int steps = 10 + rng.Next(14);
            for (int i = 0; i < steps && width > 0.12f; i++)
            {
                ang += (R() - 0.5f) * 0.9f;
                var q = p + new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang)) * (0.02f + 0.03f * R());
                if (q.x < 0.06f || q.x > 0.94f || q.y < 0.05f || q.y > 0.97f) break;
                segs.Add((p, q, width));
                if (depth < 3 && R() < 0.2f) Grow(q, ang + (R() < 0.5f ? -1 : 1) * (0.5f + 0.6f * R()), width * 0.6f, depth + 1);
                p = q; width *= 0.965f;
            }
        }
        Grow(new Vector2(0.5f, 0.96f), 0f, 1f, 0);
        Grow(new Vector2(0.42f + 0.16f * R(), 0.8f), (R() - 0.5f) * 1.2f, 0.7f, 1);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float a = 0, halo = 0;
                foreach (var s in segs)
                {
                    var ab = s.b - s.a; float t = Mathf.Clamp01(Vector2.Dot(p - s.a, ab) / ab.sqrMagnitude);
                    float dist = (p - (s.a + ab * t)).magnitude * size; // 픽셀 단위
                    a = Mathf.Max(a, Mathf.Clamp01(1.4f * s.w + 0.3f - dist));
                    halo = Mathf.Max(halo, Mathf.Clamp01(1f - dist / (14f * s.w + 4f)) * 0.22f);
                }
                px[y * size + x] = new Color(0.02f, 0.02f, 0.02f, Mathf.Clamp01(a + halo));
            }
        return Save(name, size, px, TexKind.Alpha, 0, true);
    }

    // 젖은 얼룩: 가장자리가 불규칙한 웅덩이 모양.
    static Texture2D BlotchTexture(string name, int seed)
    {
        const int size = 256;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                float n = Fbm(u, v, 3, 5, seed);
                float a = SStep(0.0f, 0.22f, (1f - r) - 0.55f * (1f - n) + 0.1f);
                px[y * size + x] = new Color(0.25f, 0.27f, 0.26f, a);
            }
        return Save(name, size, px, TexKind.Alpha, 0, true);
    }

    // ---------------------------------------------------------------- 방별 배치

    static void ApplyPropsAndDecals()
    {
        PropMaterials();
        var crackA = DecalMat("GB_Decal_CrackA", CrackTexture("GB_Decal_CrackA", 7), new Color(1, 1, 1, 0.95f), 0.1f, true);
        var crackB = DecalMat("GB_Decal_CrackB", CrackTexture("GB_Decal_CrackB", 23), new Color(1, 1, 1, 0.95f), 0.1f, true);
        var blotch = BlotchTexture("GB_Decal_Blotch", 5);
        var wet = DecalMat("GB_Decal_Wet", blotch, new Color(0.72f, 0.77f, 0.77f, 0.3f), 0.9f, false);       // 젖은 반사 자국: 색은 그대로, 반짝임만(색을 어둡게 하면 그림자처럼 보였다)
        var damp = DecalMat("GB_Decal_Damp", blotch, new Color(0.35f, 0.3f, 0.2f, 0.6f), 0.5f, true);       // 벽·카펫의 눅눅한 얼룩

        // ① 수영장 홀 ------------------------------------------------------------
        {
            var room = Root("GB_01_PoolHall");
            var props = PropsRoot(room, LOutdoor);
            // 입구 방(넓다): 사물함, 접이식 의자, 안전 수칙판, 시계
            Lockers(props, new Vector3(5.6f, 0, -75.4f), 270, 5);
            FoldingChair(props, new Vector3(-5.2f, 0, -79.6f), 90);
            FoldingChair(props, new Vector3(-5.2f, 0, -80.3f), 78);
            SignBoard(props, new Vector3(3.6f, 1.6f, -83.54f), 0);
            Clock(props, new Vector3(-3.6f, 2.7f, -83.54f), 0);
            // 홀: 데크 폭이 좁아(벽과 기둥 사이 0.9m) 바닥에는 얇은 배수구 덮개만, 나머지는 벽걸이
            LifeRing(props, new Vector3(-5.84f, 1.5f, -94.7f), 90);
            LifeRing(props, new Vector3(5.84f, 1.5f, -112.7f), 270);
            SignBoard(props, new Vector3(-5.84f, 1.6f, -100.7f), 90);
            foreach (float z in new[] { -89.7f, -95.7f, -101.7f, -107.7f, -113.7f })
            { Drain(props, new Vector3(3.72f, 0, z)); Drain(props, new Vector3(-3.72f, 0, z)); }
            FoldingChair(props, new Vector3(-4.9f, 0, -122.4f), 20);

            // D-2 물 위 발광 구체(장식만, 줍기 기능 없음)
            var orbMat = Lit("GB_GlowOrb_Hall", m => Emissive(m, new Color(0.82f, 0.95f, 1f), 2200f));
            var orbs = Group(props, "WaterGlowOrbs");
            var spots = new[] { new Vector3(-1.4f, 0.56f, -91.2f), new Vector3(1.6f, 0.7f, -94.9f), new Vector3(-0.3f, 0.6f, -99.3f), new Vector3(1.9f, 0.52f, -103.4f),
                                new Vector3(-1.8f, 0.74f, -106.1f), new Vector3(0.6f, 0.58f, -109.8f), new Vector3(-1.1f, 0.66f, -113.3f) };
            foreach (var s in spots)
                Prim(PrimitiveType.Sphere, orbs, "GlowOrb", new Vector3(s.x, -0.12f, s.z), Vector3.one * s.y, orbMat, LOutdoor);
            foreach (float z in new[] { -95f, -109f })
                MakeLight(orbs, "OrbGlowLight", LightType.Point, new Vector3(0, 0.5f, z), Vector3.zero, new Color(0.75f, 0.92f, 1f), 30000f, 7f, LOutdoor);

            // B-5 젖은 바닥 자국
            var decals = Group(Fresh(room.transform.Find(GenName), "Decals"), "Wet");
            var wetSpots = new[] { new Vector4(4.1f, -88.6f, 1.5f, 30), new Vector4(-3.9f, -108.4f, 1.6f, 300), new Vector4(0.6f, -119.6f, 2.2f, 150) };
            foreach (var w in wetSpots) Decal(decals, "WetPatch", wet, new Vector3(w.x, 0.15f, w.y), new Vector3(90, w.w, 0), w.z, w.z * 0.8f);
        }

        // ② 갈라진 복도 ----------------------------------------------------------
        {
            var room = Root("GB_02_Crack");
            var props = PropsRoot(room, LCrack);
            Lockers(props, new Vector3(9f, 0, -109.45f), 0, 4);
            FoldingChair(props, new Vector3(11.3f, 0, -104.15f), 170);
            SignBoard(props, new Vector3(8.4f, 1.6f, -103.72f), 180);
            Clock(props, new Vector3(12.2f, 3.1f, -109.68f), 0);
            Drain(props, new Vector3(13.1f, 0, -106.7f));
            Drain(props, new Vector3(8.2f, 0, -106.7f));

            var decals = Fresh(room.transform.Find(GenName), "Decals");
            // B-2 벽의 금: 북쪽 벽 큰 금 하나, 남쪽 벽 하나, 바닥으로 이어지는 금
            Decal(decals, "Crack_NorthWall", crackA, new Vector3(17.6f, 2.1f, -103.85f), new Vector3(0, 0, 0), 2.6f, 3.8f);
            Decal(decals, "Crack_SouthWall", crackB, new Vector3(13.6f, 2.3f, -109.55f), new Vector3(0, 180, 0), 2.2f, 3.4f);
            Decal(decals, "Crack_Floor", crackB, new Vector3(17.2f, 0.15f, -105.2f), new Vector3(90, 200, 0), 2.4f, 3f);
            Decal(decals, "Crack_Ceiling", crackA, new Vector3(13.4f, 3.85f, -108.4f), new Vector3(-90, 60, 0), 2f, 2.6f, 0.8f);
            // 금 아래로 번진 물 얼룩과 젖은 바닥
            Decal(decals, "Damp_NorthWall", damp, new Vector3(17.5f, 1.0f, -103.85f), new Vector3(0, 0, 0), 1.8f, 2f, 0.7f);
            Decal(decals, "Wet_Floor_A", wet, new Vector3(17.3f, 0.15f, -104.9f), new Vector3(90, 40, 0), 2.6f, 2f);
            Decal(decals, "Wet_Floor_B", wet, new Vector3(13.4f, 0.15f, -108.8f), new Vector3(90, 130, 0), 2f, 1.5f);
            Decal(decals, "Wet_Floor_C", wet, new Vector3(7.6f, 0.15f, -106.4f), new Vector3(90, 250, 0), 2.2f, 1.8f);
        }

        // ③ 노란 방 --------------------------------------------------------------
        {
            var room = Root("GB_03_Yellow");
            var props = PropsRoot(room, LYellow);
            DeskAndChair(props, new Vector3(23.4f, 0, -99.2f), 180);
            TvOnStand(props, new Vector3(35.1f, 0, -112.4f), 300);
            Frame(props, new Vector3(27.4f, 1.7f, -98.72f), 180, 0.5f, 0.7f);
            Frame(props, new Vector3(28.4f, 1.75f, -98.72f), 180, 0.7f, 0.5f);
            Frame(props, new Vector3(29.6f, 1.65f, -98.72f), 180, 0.45f, 0.6f);
            Frame(props, new Vector3(20.17f, 1.7f, -111.6f), 90, 0.6f, 0.8f);

            var decals = Fresh(room.transform.Find(GenName), "Decals");
            // 얼룩이 너무 많다는 피드백(2026-11-17): 카펫 하나만 옅게, 천장 얼룩은 뺀다.
            Decal(decals, "Damp_Carpet_A", damp, new Vector3(31.2f, 0.15f, -103.4f), new Vector3(90, 20, 0), 3.2f, 2.4f, 0.45f);
        }

        // ④ 천사 방: 비어 있는 게 연출이다. 천사 발밑 바닥 무늬만. ---------------
        {
            var room = Root("GB_04_Angel");
            var props = PropsRoot(room, LAngel);
            var a = room.transform.Find("Placeholder_Props/Angel").position;
            var inlayLight = Lit("GB_Angel_InlayLight", m => Plain(m, new Color(0.62f, 0.64f, 0.68f), 0.85f));
            var inlayDark = Lit("GB_Angel_InlayDark", m => Plain(m, new Color(0.04f, 0.04f, 0.05f), 0.9f));
            var g = PropGroup(props, "FloorInlay", a, 0);
            Part(g, PrimitiveType.Cylinder, "RingOuter", new Vector3(0, 0.003f, 0), new Vector3(6.4f, 0.003f, 6.4f), inlayLight);
            Part(g, PrimitiveType.Cylinder, "RingInner", new Vector3(0, 0.005f, 0), new Vector3(6.0f, 0.005f, 6.0f), inlayDark);
            Part(g, PrimitiveType.Cylinder, "Disc", new Vector3(0, 0.007f, 0), new Vector3(3.0f, 0.007f, 3.0f), inlayLight);
            Part(g, PrimitiveType.Cylinder, "DiscCore", new Vector3(0, 0.009f, 0), new Vector3(2.7f, 0.009f, 2.7f), inlayDark);
        }
    }
}
