using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-18 새 방 두 개(사용자 선택): 잠긴 샤워실(복도 → 샤워실 → 노란 방), 밤의 거실(노란 방 → 거실 → 천사 방).
// 기존 방 사이에는 끼울 틈이 없어서, 빈 땅에 방을 짓고 길을 다시 잇는다.
//   잠긴 샤워실: 금 간 복도 북쪽(x 6.45~19.55, z -103.1~-91.5). 복도 북쪽 벽으로 들어가 동쪽 벽으로 노란 방 서쪽 벽에 닿는다.
//   밤의 거실: 노란 방 동쪽·천사 방 동쪽(x 38.75~50, z -124~-110.5). 노란 방 동쪽 벽 → 짧은 통로 → 거실, 거실 서쪽 벽 → 천사 방 동쪽 벽.
// 예전 길(복도 → 노란 방, 노란 방 → 천사 방)은 양쪽 벽으로 막는다. 덧판(Cover)의 문 자리는 Rooms.cs에서 옮겼다.
// 기존 씬 벽에 구멍을 낼 때는 원래 벽을 끄고 GFX_Generated 아래에 조각을 새로 만든다(다시 실행해도 같은 결과).
public static partial class GreyboxGraphicsPass
{
    const uint LShower = 1u << 4, LLiving = 1u << 5;
    const int TvOnlyLayer = 31; // 거실 TV 카메라에만 보이는 것(소파의 사람). 플레이어 카메라는 이 레이어를 그리지 않는다.

    // 샤워실
    const float ShX0 = 6.45f, ShX1 = 19.55f, ShZ0 = -103.1f, ShZ1 = -91.5f, ShH = 3.2f;
    const float ShInX = 9.5f;            // 복도 → 샤워실 구멍 가운데 x (폭 1.2, 높이 2.2)
    const float ShOutZ = -101.5f;        // 샤워실 → 노란 방 구멍 가운데 z (폭 1.8, 높이 2.4)
    // 거실
    const float LvX0 = 38.75f, LvX1 = 50f, LvZ0 = -124f, LvZ1 = -110.5f, LvH = 2.8f;
    const float LvInZ = -112.4f;         // 노란 방 → 거실 구멍 가운데 z (폭 1.8, 높이 2.4)
    // 거실의 출구는 서쪽 벽(천사 방)에서 남쪽 벽(파란 침실, BdInX)으로 옮겼다(2026-11-18).

    static void ApplyNewRooms()
    {
        RewireOldPassages();
        BuildShower();
        BuildLiving();
        BuildCorridor(); // 2026-11-18 꽃무늬 아파트 복도(노란 방 → 복도 → 거실, Corridor.cs)
        BuildBedroom();
        BuildDollRoom(); // 2026-11-25 목각인형 화실(거실 → 화실 → 침실, 순간이동, DollRoom.cs)
        BuildComputer(); // 2026-11-18 컴퓨터 속 맵(침실 → 컴퓨터 속 → 도시, Computer.cs)
        BuildSunkenCity(); // 2026-11-18 가라앉은 집들의 도시(컴퓨터 속 → 도시 → 천사 방, SunkenCity.cs)
    }

    // ---------------------------------------------------------------- 길 다시 잇기
    static void RewireOldPassages()
    {
        var crackGen = Root("GB_02_Crack").transform.Find(GenName);
        var yellowGen = Root("GB_03_Yellow").transform.Find(GenName);
        var angelGen = Root("GB_04_Angel").transform.Find(GenName);

        // 복도 → 노란 방 구멍: 양쪽 덧판은 Rooms.cs에서 문 자리 없이 만든다. 콜라이더만 채운다.
        var close = Group(crackGen, "Closed_CrackToYellow");
        Box(close, "Fill", new Vector3(20f, 1.3f, -106.7f), new Vector3(0.3f, 2.6f, 2f), Find("GB_02_Crack/Wall_North_0").GetComponent<MeshRenderer>().sharedMaterial, LCrack, true);
        // 노란 방 → 천사 방 구멍: 노란 방 쪽은 덧판이 덮고, 천사 방 쪽은 하늘 벽으로 채운다.
        var aw = Find("GB_04_Angel/Wall_North_1").GetComponent<MeshRenderer>();
        Box(Group(angelGen, "Closed_YellowToAngel"), "Fill", new Vector3(28.15f, 1.3f, -114.55f), new Vector3(2f, 2.6f, 0.3f), aw.sharedMaterial, aw.renderingLayerMask, true);

        // 새 구멍
        SplitSceneWall(Find("GB_02_Crack/Wall_North_0"), crackGen, (ShInX - 0.6f, ShInX + 0.6f, 2.2f));
        SplitSceneWall(Find("GB_03_Yellow/Wall_West_1"), yellowGen, (ShOutZ - 0.9f, ShOutZ + 0.9f, 2.4f));
        SplitSceneWall(Find("GB_03_Yellow/Wall_East_0"), yellowGen, (LvInZ - 0.9f, LvInZ + 0.9f, 2.4f));
        // 천사 방 동쪽 벽에는 이제 구멍이 없다. 파란 침실 복도 끝 문을 지나면 이 벽 앞으로 순간이동한다(§6-3d, BlueGimmick.cs).
        // 예전에 구멍을 내며 꺼 둔 원래 벽을 다시 켠다.
        var angelEast = Find("GB_04_Angel/Wall_East_0");
        angelEast.gameObject.SetActive(true);
        EditorUtility.SetDirty(angelEast.gameObject);
        foreach (var stale in new[] { "Wall_East_0_Cut", "Seal_BedroomToAngel" })
        {
            var old = angelGen.Find(stale);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // 노란 방 서쪽 벽의 액자(z -111.6)는 그대로 두고, 동쪽 벽 TV(35.1, -112.4)는 새 통로를 막지 않게 북쪽으로 옮긴다.
        var yProps = Root("GB_03_Yellow").transform.Find("Placeholder_Props/" + PropsName);
        foreach (Transform t in yProps)
            if (t.name == "TV_Off" && Mathf.Abs(t.position.z + 112.4f) < 0.1f) t.position = new Vector3(35.1f, 0f, -104.6f);

        // TV에만 보이는 레이어를 플레이어 카메라에서 뺀다.
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (cam.GetComponentInParent<CharacterController>(true) != null)
            {
                cam.cullingMask &= ~(1 << TvOnlyLayer);
                EditorUtility.SetDirty(cam);
            }
    }

    /// <summary>
    /// 씬에 있는 상자 벽(회전 없음)에 구멍을 낸다. 원래 벽은 끄고, 같은 재질·레이어의 조각을 parent 아래에 만든다.
    /// hole = (벽 방향 시작, 끝, 구멍 높이). 다시 실행하면 원래 벽 크기(transform)에서 다시 만든다.
    /// </summary>
    static void SplitSceneWall(Transform wall, Transform parent, params (float s0, float s1, float h)[] holes)
    {
        var r = wall.GetComponent<MeshRenderer>();
        Vector3 c = wall.position, s = wall.lossyScale;
        bool alongX = s.x > s.z;
        float a = alongX ? c.x - s.x / 2 : c.z - s.z / 2, b = alongX ? c.x + s.x / 2 : c.z + s.z / 2;
        float fixedC = alongX ? c.z : c.x, th = alongX ? s.z : s.x;
        WallWithHoles(parent, wall.name + "_Cut", alongX, fixedC, th, a, b, c.y - s.y / 2, c.y + s.y / 2, holes, r.sharedMaterial, r.renderingLayerMask);
        wall.gameObject.SetActive(false);
        EditorUtility.SetDirty(wall.gameObject);
    }

    /// <summary>구멍 뚫린 벽(콜라이더 포함). alongX: 벽이 X축으로 뻗는지. fixedC: 두께 방향 가운데 좌표.</summary>
    static void WallWithHoles(Transform parent, string name, bool alongX, float fixedC, float th, float a, float b, float y0, float y1,
        (float s0, float s1, float h)[] holes, Material m, uint layer)
    {
        var g = Group(parent, name);
        void Piece(string n, float s0, float s1, float py0, float py1)
        {
            if (s1 - s0 < 0.01f || py1 - py0 < 0.01f) return;
            var center = alongX ? new Vector3((s0 + s1) / 2, (py0 + py1) / 2, fixedC) : new Vector3(fixedC, (py0 + py1) / 2, (s0 + s1) / 2);
            var size = alongX ? new Vector3(s1 - s0, py1 - py0, th) : new Vector3(th, py1 - py0, s1 - s0);
            Box(g, n, center, size, m, layer, true);
        }
        var list = new List<(float s0, float s1, float h)>(holes);
        list.Sort((p, q) => p.s0.CompareTo(q.s0));
        float cur = a; int i = 0;
        foreach (var hole in list)
        {
            Piece($"Seg_{i}", cur, hole.s0, y0, y1);
            Piece($"Top_{i}", hole.s0, hole.s1, y0 + hole.h, y1);
            cur = hole.s1; i++;
        }
        Piece($"Seg_{i}", cur, b, y0, y1);
    }

    static Material TintedCopy(string name, string srcName, Color tint, float? smoothness = null)
    {
        var src = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{srcName}.mat");
        return Lit(name, m =>
        {
            m.CopyPropertiesFromMaterial(src);
            m.SetColor("_BaseColor", tint);
            if (smoothness.HasValue) m.SetFloat("_Smoothness", smoothness.Value);
        });
    }

    static Material Transparent(string name, Color c, float smoothness, float nits = 0f)
    {
        return Lit(name, m =>
        {
            Plain(m, c, smoothness);
            m.SetColor("_EmissiveColor", new Color(c.r, c.g, c.b, 1f) * nits); // 어두운 방에서도 물줄기가 보이게 살짝 빛낸다
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
            m.SetFloat("_SurfaceType", 1f); m.SetFloat("_BlendMode", 0f);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_TransparentZWrite", 0f);
            m.SetFloat("_AlphaCutoffEnable", 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
        });
    }

    static GameObject NewRoomRoot(string name, out Transform gen)
    {
        var root = RootOrCreate(name);
        root.transform.position = Vector3.zero;
        gen = Fresh(root.transform, GenName);
        return root;
    }

    // 흐르는 물 무늬: 세로 줄무늬(가는 물줄기 여러 가닥) + 위아래로 끊기는 덩어리. 가로·세로 모두 이어진다(정수 주파수).
    // 알파 = 물의 두께. WaterFlow가 이 무늬를 위로 흘린다(2026-11-19: 원기둥이 "물기둥"처럼 보인다는 피드백).
    static Texture2D FlowTexture()
    {
        const int size = 256;
        string path = $"{TexDir}/GB_Shower_Flow.png";
        var px = new Color[size * size];
        float S(float x) => 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * x);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float streak = Mathf.Pow(S(u * 7f + 0.3f * Mathf.Sin(2f * Mathf.PI * u * 3f) + 0.08f * Mathf.Sin(2f * Mathf.PI * v)), 6f)
                             + 0.6f * Mathf.Pow(S(u * 13f + 0.17f), 10f)
                             + 0.35f * Mathf.Pow(S(u * 23f + 0.41f + 0.05f * Mathf.Sin(2f * Mathf.PI * v * 2f)), 14f);
                float pulse = 0.5f + 0.5f * S(v * 2f + 0.5f * Mathf.Sin(2f * Mathf.PI * u * 5f));
                float a = Mathf.Clamp01(0.12f + 0.8f * streak * pulse);
                px[y * size + x] = new Color(0.85f + 0.15f * a, 0.95f, 1f, a);
            }
        System.IO.Directory.CreateDirectory(TexDir);
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat; imp.sRGBTexture = true; imp.alphaIsTransparency = true; imp.mipmapEnabled = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 김 서린 거울(가로 4.5m : 세로 0.8m): 뿌연 회청색 김 + 아래로 흘러내린 물줄기(맑은 유리가 드러나 어둡다) + 손으로 한 번 닦은 자국 + 물방울.
    // VARCO 그림이 들어오기 전 자리표시. 같은 결과가 나오도록 PerlinNoise만 쓴다.
    static Texture2D FoggedMirrorTexture()
    {
        const int w = 2048, h = 364;
        string path = $"{TexDir}/GB_Shower_FoggedMirror.png";
        var fog = new Color(0.80f, 0.84f, 0.86f);
        var glass = new Color(0.22f, 0.27f, 0.30f);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 4.5f, v = (y + 0.5f) / h * 0.8f; // 미터
                float n = Mathf.PerlinNoise(u * 3f, v * 3f) * 0.6f + Mathf.PerlinNoise(u * 11f + 7f, v * 11f) * 0.4f;
                float clear = 0.08f + 0.18f * n; // 김이 옅은 정도(0=완전 뿌옇다)
                // 물줄기: 위에서 시작해 아래로 흐른 가는 줄. 칸마다 다른 곳에서 시작한다.
                float lane = u * 9f;
                float cell = Mathf.Floor(lane), fx = lane - cell;
                float start = 0.15f + 0.55f * Mathf.PerlinNoise(cell * 1.7f, 3.3f); // 위에서부터 줄이 시작되는 높이(m, 위=0)
                float cx = 0.5f + 0.3f * (Mathf.PerlinNoise(cell * 2.3f, 9.1f) - 0.5f) + 0.02f * Mathf.Sin(v * 25f + cell);
                float top = 0.8f - v;
                float end = start + 0.12f + 0.45f * Mathf.PerlinNoise(cell * 3.1f, 5.7f); // 줄이 멈춘 곳(끝에 물방울)
                float width = (0.06f + 0.05f * Mathf.PerlinNoise(cell, v * 4f)) * (0.6f + 0.4f * Mathf.Clamp01((top - start) / 0.15f));
                if (Mathf.PerlinNoise(cell * 0.9f, 1.1f) > 0.4f && top > start && top < end)
                    clear = Mathf.Max(clear, Mathf.Clamp01(1f - Mathf.Abs(fx - cx) / width) * 0.8f);
                float bx = (fx - cx) / 0.11f, by = (top - end) / 0.018f; // 줄 끝 물방울
                if (Mathf.PerlinNoise(cell * 0.9f, 1.1f) > 0.4f) clear = Mathf.Max(clear, Mathf.Clamp01(1f - Mathf.Sqrt(bx * bx + by * by)) * 1.5f);
                // 손으로 닦은 자국: 가운데 칸에 가로 손바닥 획 두 번. 가장자리는 거칠고, 손가락 결(가로 줄무늬) 사이에 김이 조금 남는다.
                float wipe = 0f;
                foreach (var (wx, wy, rx, ry) in new[] { (2.2f, 0.52f, 0.5f, 0.09f), (2.45f, 0.36f, 0.42f, 0.08f) })
                {
                    float edgeN = 0.25f * (Mathf.PerlinNoise(u * 14f, wy * 9f) - 0.5f);
                    float du = (u - wx) / rx, dv = (v - wy) / (ry * (1f + edgeN * 2f));
                    float k = Mathf.Clamp01((1f - Mathf.Sqrt(du * du * du * du + dv * dv)) * 4f); // 납작한 타원, 가장자리 거칠게
                    float grain = 0.55f + 0.45f * Mathf.PerlinNoise(u * 3f, v * 140f); // 손가락 결
                    wipe = Mathf.Max(wipe, k * grain);
                }
                clear = Mathf.Max(clear, wipe * (0.7f + 0.2f * n));
                // 물방울: 작은 맑은 점
                float d = Mathf.PerlinNoise(u * 60f, v * 60f);
                if (d > 0.78f) clear = Mathf.Max(clear, (d - 0.78f) * 3.5f);
                var g = Color.Lerp(glass, new Color(0.42f, 0.5f, 0.54f), v / 0.8f * 0.7f); // 맑은 곳에 비친 방: 위가 조금 밝다(형광등)
                px[y * w + x] = Color.Lerp(fog, g, Mathf.Clamp01(clear));
            }
        System.IO.Directory.CreateDirectory(TexDir);
        var t = new Texture2D(w, h, TextureFormat.RGB24, false);
        t.SetPixels(px); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true; imp.mipmapEnabled = true; imp.npotScale = TextureImporterNPOTScale.None;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 천장의 젖은 얼룩: 가운데가 진하고 가장자리로 갈수록 흐려지는 불규칙한 원 + 옅은 물결 테. 알파 = 젖은 정도.
    static Texture2D WetStainTexture()
    {
        const int size = 256;
        string path = $"{TexDir}/GB_Shower_CeilingWet.png";
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float ang = Mathf.Atan2(dy, dx), d = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = 0.7f + 0.06f * Mathf.Sin(ang * 3f + 0.7f) + 0.035f * Mathf.Sin(ang * 7f + 2.1f) + 0.015f * Mathf.Sin(ang * 13f);
                // 흐릿한 가장자리. Mathf.SmoothStep은 GLSL smoothstep이 아니라(두 값 사이 보간) 직접 계산한다.
                float s = Mathf.Clamp01((d - edge * 0.2f) / (edge * 0.8f));
                float body = 1f - s * s * (3f - 2f * s);
                float ring = 0.12f * Mathf.Exp(-Mathf.Pow((d - edge * 0.85f) / 0.06f, 2f)); // 옅은 마른 자국 테
                float a = Mathf.Clamp01(0.85f * body + ring);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        System.IO.Directory.CreateDirectory(TexDir);
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true; imp.alphaIsTransparency = true; imp.mipmapEnabled = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- 잠긴 샤워실
    static void BuildShower()
    {
        NewRoomRoot("GB_02b_Shower", out var gen);
        var wall = TintedCopy("GB_Shower_WallTile", "GB_Crack_WallTile", new Color(0.78f, 0.95f, 1f));
        var floor = TintedCopy("GB_Shower_FloorTile", "GB_Crack_FloorTile", new Color(0.42f, 0.6f, 0.66f));
        var ceil = Lit("GB_Shower_Ceiling", m => Plain(m, new Color(0.72f, 0.8f, 0.82f), 0.2f));
        var metal = Lit("GB_Shower_Chrome", m => Plain(m, new Color(0.75f, 0.77f, 0.78f), 0.85f, 1f));
        var water = Transparent("GB_Shower_Water", new Color(0.5f, 0.78f, 0.84f, 0.35f), 0.97f);
        var stream = Transparent("GB_Shower_Stream", new Color(0.85f, 0.95f, 1f, 0.75f), 0.95f, 6f);
        var flowTex = FlowTexture();
        var flow = Lit("GB_Shower_FlowUp", m =>
        {
            Plain(m, new Color(0.9f, 0.97f, 1f, 1f), 0.95f);
            m.SetTexture("_BaseColorMap", flowTex); m.SetTextureScale("_BaseColorMap", new Vector2(1f, 3f));
            m.SetTexture("_EmissiveColorMap", flowTex); m.SetTextureScale("_EmissiveColorMap", new Vector2(1f, 3f));
            m.SetColor("_EmissiveColor", new Color(0.85f, 0.95f, 1f) * 5f); // 어두운 샤워실에서도 물줄기가 읽히게 살짝
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
            m.SetFloat("_SurfaceType", 1f); m.SetFloat("_BlendMode", 0f);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_TransparentZWrite", 0f);
            m.SetFloat("_AlphaCutoffEnable", 0f); m.SetFloat("_DoubleSidedEnable", 1f);
            m.renderQueue = (int)RenderQueue.Transparent;
        });
        var wetTex = WetStainTexture();
        var wet = Lit("GB_Shower_CeilingWet", m =>
        {
            Plain(m, new Color(0.3f, 0.37f, 0.4f, 1f), 0.55f);
            m.SetTexture("_BaseColorMap", wetTex);
            m.SetFloat("_SurfaceType", 1f); m.SetFloat("_BlendMode", 0f);
            // 투명한 곳에서도 반사가 남아 네모 판이 보였다 → 반사도 알파를 따르게
            m.SetFloat("_EnableBlendModePreserveSpecularLighting", 0f);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_TransparentZWrite", 0f);
            m.SetFloat("_AlphaCutoffEnable", 0f); m.SetFloat("_DoubleSidedEnable", 1f);
            m.renderQueue = (int)RenderQueue.Transparent;
        });
        var drain = Lit("GB_Shower_Drain", m => Plain(m, new Color(0.08f, 0.09f, 0.1f), 0.5f, 0.8f));
        var bench = Lit("GB_Shower_Bench", m => Plain(m, new Color(0.55f, 0.42f, 0.3f), 0.35f));
        var mirror = Lit("GB_Shower_Mirror", m => Plain(m, new Color(0.82f, 0.86f, 0.88f), 0.6f, 0.6f)); // 김이 서린 거울

        var shell = Group(gen, "Shell");
        Box(shell, "Floor", new Vector3((6.15f + 19.85f) / 2, -0.15f, (-103.4f - 91.2f) / 2), new Vector3(13.7f, 0.3f, 12.2f), floor, LShower, true);
        Box(shell, "Ceiling", new Vector3((6.15f + 19.85f) / 2, ShH + 0.15f, (-103.4f - 91.2f) / 2), new Vector3(13.7f, 0.3f, 12.2f), ceil, LShower);
        WallWithHoles(shell, "West", false, 6.3f, 0.3f, -103.4f, -91.2f, 0f, ShH, new (float, float, float)[0], wall, LShower);
        WallWithHoles(shell, "North", true, -91.35f, 0.3f, 6.15f, 19.85f, 0f, ShH, new (float, float, float)[0], wall, LShower);
        WallWithHoles(shell, "South", true, -103.25f, 0.3f, 6.15f, 19.85f, 0f, ShH, new[] { (ShInX - 0.6f, ShInX + 0.6f, 2.2f) }, wall, LShower);
        WallWithHoles(shell, "East", false, 19.7f, 0.3f, -103.4f, -91.2f, 0f, ShH, new[] { (ShOutZ - 0.9f, ShOutZ + 0.9f, 2.4f) }, wall, LShower);

        // 발목까지 찬 물(콜라이더 없음). 문 자리의 턱은 문이 있던 티가 나서 없앴다(2026-11-18 플레이 피드백).
        // 2026-11-19: 반투명 상자 → HDRP 물(잔잔한 물결, 바닥 타일에 빛무늬). 수면 = 0.12m.
        PoolWater(gen, "Water", new Vector3((ShX0 + ShX1) / 2, 0.12f, (ShZ0 + ShZ1) / 2), new Vector2(ShX1 - ShX0, ShZ1 - ShZ0), LShower,
            new Color(0.55f, 0.86f, 0.9f), new Color(0.2f, 0.55f, 0.62f), 4f, 0.8f, 1.2f);

        // 샤워기: 북쪽 벽 4개, 동쪽 벽 3개. 동쪽 맨 아래(출구 옆) 하나만 물이 바닥 배수구에서 천장으로 솟는다(나머지는 물이 나오지 않는다).
        var showers = Group(gen, "Showers");
        GameObject odd = null;
        void Shower(Vector3 wallPoint, Vector3 n, bool up, string nm)
        {
            var g = new GameObject(nm).transform; g.SetParent(showers, true);
            g.SetPositionAndRotation(wallPoint, Quaternion.LookRotation(n));
            void P(PrimitiveType t, string pn, Vector3 lc, Vector3 sz, Material m, Vector3? e = null)
            {
                var go = GameObject.CreatePrimitive(t); go.name = pn; Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(g, false); go.transform.localPosition = lc; go.transform.localScale = sz;
                if (e.HasValue) go.transform.localRotation = Quaternion.Euler(e.Value);
                var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.renderingLayerMask = LShower;
                if (m == stream || m == water || m == flow || m == wet) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            // VARCO 샤워기(벽에서 나와 아래로 굽은 팔 + 머리)가 있으면 팔·머리 더미 대신 쓰고, 배관은 그 아래까지 잇는다.
            if (VarcoInto(g, "Shower", "ShowerHead", LShower, new Vector3(0, 2.32f, 0), default, false))
                P(PrimitiveType.Cylinder, "Pipe", new Vector3(0, 1.75f, 0.05f), new Vector3(0.035f, 0.65f, 0.035f), metal);
            else
            {
                P(PrimitiveType.Cylinder, "Pipe", new Vector3(0, 1.6f, 0.05f), new Vector3(0.035f, 0.55f, 0.035f), metal);
                P(PrimitiveType.Cylinder, "Arm", new Vector3(0, 2.15f, 0.2f), new Vector3(0.03f, 0.16f, 0.03f), metal, new Vector3(90, 0, 0));
                P(PrimitiveType.Cylinder, "Head", new Vector3(0, 2.12f, 0.36f), new Vector3(0.18f, 0.025f, 0.18f), metal);
            }
            P(PrimitiveType.Sphere, "Valve", new Vector3(0, 1.1f, 0.05f), Vector3.one * 0.09f, metal);
            // 배수구 덮개는 물(수면 0.12) 밑 바닥에 둔다. 수면과 같은 높이면 물결에 깜빡였다(2026-11-19).
            P(PrimitiveType.Cylinder, "Drain", new Vector3(0, 0.005f, 0.36f), new Vector3(0.16f, 0.002f, 0.16f), drain);
            // 2026-11-19: 보통 샤워기의 떨어지는 물줄기(흰 원기둥 + 바닥 물보라)는 기둥처럼 보여 비현실적이라 뺐다(사용자 결정).
            // 물이 나오는 건 이상한 점(솟는 물기둥) 하나뿐이라 더 눈에 띈다.
            if (up)
            {
                // 배수구에서 물기둥이 천장까지 솟고, 천장에 물이 고여 번진다. 샤워기 머리에서는 물이 나오지 않는다.
                // 2026-11-19: 굵은 원기둥 하나는 "물기둥"처럼 보였다 → 위로 흐르는 줄무늬 물줄기 세 가닥(가운데 하나 + 가는 둘)으로.
                var up3 = new GameObject("Stream_Up").transform; up3.SetParent(g, false);
                float mid = (0.12f + ShH) / 2, half = (ShH - 0.12f) / 2;
                var strands = new (string n, Vector3 off, float r, float spd, float tilt)[]
                {
                    ("Strand_Core", new Vector3(0f, 0f, 0.36f), 0.075f, 1.6f, 0f),
                    ("Strand_A", new Vector3(0.035f, 0f, 0.33f), 0.028f, 2.3f, 1.2f),
                    ("Strand_B", new Vector3(-0.03f, 0f, 0.39f), 0.022f, 2.0f, -1.5f),
                };
                for (int k = 0; k < strands.Length; k++)
                {
                    var st = strands[k];
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = st.n; Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.transform.SetParent(up3, false);
                    go.transform.localPosition = new Vector3(st.off.x, mid, st.off.z);
                    go.transform.localRotation = Quaternion.Euler(st.tilt, 0f, st.tilt * 0.7f);
                    go.transform.localScale = new Vector3(st.r, half, st.r);
                    var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = flow; r.renderingLayerMask = LShower; r.shadowCastingMode = ShadowCastingMode.Off;
                    var wf = go.AddComponent<WaterFlow>(); wf.speed = st.spd; wf.phase = k * 0.13f; wf.wobble = k == 0 ? 0.1f : 0.2f;
                }
                // 천장에 번진 물: 2026-11-19 납작한 구(줄무늬가 방사형으로 퍼진 원판)가 어색하다는 피드백 →
                // 천장에 딱 붙은 젖은 얼룩(가장자리가 흐릿하고 불규칙한, 조금 어둡고 반들거리는 자국)으로.
                P(PrimitiveType.Quad, "CeilingPool", new Vector3(0, ShH - 0.003f, 0.36f), new Vector3(1.1f, 1.1f, 1f), wet, new Vector3(-90f, 0f, 0f));
                odd = g.gameObject;
            }
        }
        foreach (float x in new[] { 8.4f, 10.8f, 13.2f, 15.6f })
            Shower(new Vector3(x, 0f, ShZ1), Vector3.back, false, $"Shower_N_{x:F0}");
        Shower(new Vector3(ShX1, 0f, -93.6f), Vector3.left, false, "Shower_E_94");
        Shower(new Vector3(ShX1, 0f, -96.0f), Vector3.left, false, "Shower_E_96");
        Shower(new Vector3(ShX1, 0f, -98.6f), Vector3.left, true, "Oddity_Shower_Up");

        // 칸막이(허리 위까지 오는 낮은 벽)
        var parts = Group(gen, "Partitions");
        foreach (float x in new[] { 9.6f, 12f, 14.4f, 16.8f })
            Box(parts, $"Part_N_{x:F0}", new Vector3(x, 0.95f, ShZ1 - 0.55f), new Vector3(0.06f, 1.9f, 1.1f), wall, LShower, true);
        foreach (float z in new[] { -94.8f, -97.3f, -99.9f })
            Box(parts, $"Part_E_{-z:F0}", new Vector3(ShX1 - 0.55f, 0.95f, z), new Vector3(1.1f, 1.9f, 0.06f), wall, LShower, true);

        // 가운데 긴 의자와 서쪽 벽의 김 서린 거울
        if (VarcoPrefab("Shower", "Bench") != null)
        {
            // VARCO 긴 의자(1.5m) 두 개를 나란히
            foreach (float x in new[] { 11.65f, 13.35f })
            {
                var bg = new GameObject("Bench").transform; bg.SetParent(gen, true); bg.position = new Vector3(x, 0f, -98.5f);
                bg.gameObject.AddComponent<BoxCollider>();
                VarcoInto(bg, "Shower", "Bench", LShower);
            }
        }
        else
        {
            Box(gen, "Bench", new Vector3(12.5f, 0.45f, -98.5f), new Vector3(4.2f, 0.08f, 0.45f), bench, LShower, true);
            foreach (float x in new[] { 10.7f, 14.3f }) Box(gen, "BenchLeg", new Vector3(x, 0.22f, -98.5f), new Vector3(0.08f, 0.44f, 0.4f), metal, LShower);
        }
        // 김 서린 거울(서쪽 벽, 4.5×0.8m): 회색 판 대신 그림을 씌우고 금속 틀로 세 칸으로 나눈다.
        // VARCO 그림(Assets/VARCO/Shower/2D/VAR_Shower_FoggedMirror.png)이 있으면 그것, 없으면 코드로 만든 그림.
        var fogTex = Img2D("Shower", "FoggedMirror") ?? FoggedMirrorTexture();
        mirror = Lit("GB_Shower_Mirror", m => { Plain(m, Color.white, 0.55f); m.SetTexture("_BaseColorMap", fogTex); });
        Box(gen, "MirrorBack", new Vector3(ShX0 + 0.01f, 1.6f, -96.5f), new Vector3(0.02f, 0.84f, 4.54f), metal, LShower);
        var mq = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(mq.GetComponent<Collider>());
        mq.name = "Mirror"; mq.layer = gen.gameObject.layer;
        mq.transform.SetParent(gen, true);
        mq.transform.SetPositionAndRotation(new Vector3(ShX0 + 0.021f, 1.6f, -96.5f), Quaternion.LookRotation(Vector3.left)); // 보이는 면이 방(+X)을 본다
        mq.transform.localScale = new Vector3(4.5f, 0.8f, 1f);
        var mqr = mq.GetComponent<MeshRenderer>();
        mqr.sharedMaterial = mirror; mqr.renderingLayerMask = LShower; mqr.shadowCastingMode = ShadowCastingMode.Off;
        foreach (float z in new[] { -97.25f, -95.75f }) // 칸막이 두 줄
            Box(gen, "MirrorMullion", new Vector3(ShX0 + 0.03f, 1.6f, z), new Vector3(0.025f, 0.8f, 0.03f), metal, LShower);

        // 차가운 청록 형광등 2개 + 약한 보조광
        var lights = Group(gen, "Lights");
        var cold = new Color(0.8f, 0.95f, 1f);
        foreach (float x in new[] { 9.8f, 15.8f })
            Fluorescent(lights, $"Fluorescent_{x:F0}", new Vector3(x, ShH, -97.3f), true, 2.4f, 0.22f, cold, 90000f, 2600f, Housing(), "GB_Shower_TubeGlow", LShower);
        MakeLight(lights, "CeilingFill", LightType.Point, new Vector3(13f, 1.4f, -97.3f), Vector3.zero, new Color(0.82f, 0.95f, 1f), 30000f, 10f, LShower, false, false);
        var profile = RoomProfile("Shower", 7.2f, /* 8.0에서는 앞뒤 방(0.65)보다 한참 어두웠다(0.46), 7.6은 0.50 */ 0.04f, 0.15f, new Color(0.82f, 0.94f, 1f), 14f, new Color(0.03f, 0.04f, 0.05f), -18f, -2f);
        LocalVolume(gen, "LocalVolume", new Vector3((ShX0 + ShX1) / 2, ShH / 2, (ShZ0 + ShZ1) / 2), new Vector3(ShX1 - ShX0, ShH, ShZ1 - ShZ0), 1f, 125f, profile);

        // 찾기 규칙: 솟는 물기둥을 바라보면 물이 멈추고, 옆 동쪽 벽(출구)이 열린다.
        var gate = FindGate(gen, "FindGate_Shower", 3, "ShowerUp",
            new Bounds(new Vector3(ShX1 + 0.15f, 1.2f, ShOutZ), new Vector3(0.3f, 2.4f, 1.8f)), "GB_02b_Shower/GFX_Generated/Shell/East/Seg_0");
        var gz = AddGaze(odd, "ShowerUp", gate, new Bounds(new Vector3((ShX0 + ShX1) / 2, ShH / 2, (ShZ0 + ShZ1) / 2), new Vector3(ShX1 - ShX0, ShH, ShZ1 - ShZ0)));
        gz.hideOnFound = new[] { odd.transform.Find("Stream_Up").gameObject, odd.transform.Find("CeilingPool").gameObject };
    }

    // ---------------------------------------------------------------- 밤의 거실
    static void BuildLiving()
    {
        NewRoomRoot("GB_03b_Living", out var gen);
        var wall = TintedCopy("GB_Living_Wallpaper", "GB_Yellow_Wallpaper", new Color(0.36f, 0.42f, 0.33f));
        var floor = TintedCopy("GB_Living_Carpet", "GB_Yellow_Carpet", new Color(0.46f, 0.22f, 0.18f));
        var ceil = Lit("GB_Living_Ceiling", m => Plain(m, new Color(0.42f, 0.4f, 0.36f), 0.1f));
        var trim = Lit("GB_Living_Trim", m => Plain(m, new Color(0.18f, 0.12f, 0.08f), 0.35f));
        var wood = Lit("GB_Living_Wood", m => Plain(m, new Color(0.32f, 0.2f, 0.12f), 0.45f));
        var fabric = Lit("GB_Living_Sofa", m => Plain(m, new Color(0.38f, 0.3f, 0.26f), 0.15f));
        var shade = Lit("GB_Living_LampShade", m => Emissive(m, new Color(1f, 0.72f, 0.42f), 300f));
        var dark = Lit("GB_Living_Dark", m => Plain(m, new Color(0.04f, 0.04f, 0.05f), 0.4f));
        var night = Lit("GB_Living_NightGlass", m => Emissive(m, new Color(0.12f, 0.16f, 0.3f), 2f));
        var pale = Lit("GB_Living_Ghost", m => Plain(m, new Color(0.82f, 0.82f, 0.84f), 0.2f));

        var shell = Group(gen, "Shell");
        float cx = (38.45f + 50.3f) / 2, cz = (-124.3f - 110.2f) / 2, sx = 50.3f - 38.45f, sz = 124.3f - 110.2f;
        Box(shell, "Floor", new Vector3(cx, -0.15f, cz), new Vector3(sx, 0.3f, sz), floor, LLiving, true);
        Box(shell, "Ceiling", new Vector3(cx, LvH + 0.15f, cz), new Vector3(sx, 0.3f, sz), ceil, LLiving);
        WallWithHoles(shell, "West", false, 38.6f, 0.3f, -124.3f, -110.2f, 0f, LvH,
            new[] { (LvInZ - 0.9f, LvInZ + 0.9f, 2.4f) }, wall, LLiving);
        WallWithHoles(shell, "East", false, 50.15f, 0.3f, -124.3f, -110.2f, 0f, LvH, new (float, float, float)[0], wall, LLiving);
        WallWithHoles(shell, "North", true, -110.35f, 0.3f, 38.45f, 50.3f, 0f, LvH, new (float, float, float)[0], wall, LLiving);
        WallWithHoles(shell, "South", true, -124.15f, 0.3f, 38.45f, 50.3f, 0f, LvH, new[] { (BdInX - 0.9f, BdInX + 0.9f, 2.4f) }, wall, LLiving);
        // 걸레받이
        // 서쪽 벽은 노란 방 문(LvInZ)을 기준으로 두 번에 나눈다.
        Cover(Group(gen, "Baseboard"), "W", false, LvX0 + 0.02f, LvZ0, LvInZ - 0.9f, 0.1f, LvZ0, LvZ0, 0.1f, trim, LLiving);
        Cover(gen.Find("Baseboard"), "W2", false, LvX0 + 0.02f, LvInZ + 0.9f, LvZ1, 0.1f, LvInZ + 0.9f, LvInZ + 0.9f, 0.1f, trim, LLiving);
        Cover(gen.Find("Baseboard"), "N", true, LvZ1 - 0.02f, LvX0, LvX1, 0.1f, LvX0, LvX0, 0.1f, trim, LLiving);
        Cover(gen.Find("Baseboard"), "E", false, LvX1 - 0.02f, LvZ0, LvZ1, 0.1f, LvZ0, LvZ0, 0.1f, trim, LLiving);
        Cover(gen.Find("Baseboard"), "S", true, LvZ0 + 0.02f, LvX0, LvX1, 0.1f, BdInX - 0.9f, BdInX + 0.9f, 0.1f, trim, LLiving);

        // 노란 방 동쪽 벽(36.45) → 거실 서쪽 벽(38.45) 사이 짧은 통로. 벽은 거실 벽지로, 들어서면 분위기가 바뀐다.
        var pass = Group(gen, "Passage_FromYellow");
        Box(pass, "Floor", new Vector3(37.45f, -0.15f, LvInZ), new Vector3(2f, 0.3f, 1.8f), floor, LLiving | LYellow, true);
        Box(pass, "Ceiling", new Vector3(37.45f, 2.55f, LvInZ), new Vector3(2f, 0.3f, 2.4f), ceil, LLiving | LYellow);
        Box(pass, "Wall_N", new Vector3(37.45f, 1.35f, LvInZ + 1.05f), new Vector3(2f, 2.7f, 0.3f), wall, LLiving | LYellow, true);
        Box(pass, "Wall_S", new Vector3(37.45f, 1.35f, LvInZ - 1.05f), new Vector3(2f, 2.7f, 0.3f), wall, LLiving | LYellow, true);

        // 소품: 서쪽 벽(출구 옆)에 TV, 맞은편 소파, 사이에 탁자와 깔개, 소파 옆 스탠드, 북쪽 벽 가족사진, 동쪽 벽 밤 창문
        propLayer = LLiving;
        var props = Group(gen, "Props");
        var tvPos = new Vector3(LvX0 + 0.35f, 0f, -118.6f);
        var tv = new GameObject("Oddity_TV").transform; tv.SetParent(props, true);
        tv.SetPositionAndRotation(tvPos, Quaternion.LookRotation(Vector3.right));
        // VARCO 브라운관 TV가 있으면 받침대 위에 올리고, TV 카메라 화면(Quad)을 브라운관 유리 위에 맞춘다.
        // 유리 = 모델 로컬 x +0.05, 높이 0.33 가운데, 폭 0.58 · 높이 0.44(둥근 모서리라 조금 작게 덮는다), 앞면 z 0.35.
        bool crt = VarcoPrefab("LivingRoom", "CRTTV") != null;
        float tvY = 0.5f; // TV를 올리는 높이(받침 윗면)
        if (crt)
        {
            // 2026-11-23 사용자 요청: 받침은 노란 방 TV 받침(VARCO)을 떼어 같은 것을 쓴다. 없으면 예전 나무 상자.
            var standMesh = TvStandOnlyMesh(out var standMat, out float standTop);
            if (standMesh != null)
            {
                MeshPart(tv, "Stand", standMesh, standMat, LLiving, 1f);
                TvStandTopBoard(tv, LLiving);
                tvY = standTop + 0.005f;
            }
            else PBox(tv, "Stand", new Vector3(0, 0.25f, 0), new Vector3(1.1f, 0.5f, 0.7f), wood);
            VarcoInto(tv, "LivingRoom", "CRTTV", LLiving, new Vector3(0, tvY, 0), default, false);
        }
        else
        {
            PBox(tv, "Stand", new Vector3(0, 0.25f, 0), new Vector3(1.1f, 0.5f, 0.45f), wood);
            PBox(tv, "Body", new Vector3(0, 0.84f, -0.02f), new Vector3(0.82f, 0.62f, 0.5f), dark);
        }
        var rt = TvRenderTexture();
        var screenMat = Lit("GB_Living_TVScreen", m =>
        {
            Plain(m, Color.black, 0f); // 반짝임 없음: 둥근 화면은 앞의 TV 불빛을 하얗게 받아 그림이 바랬다(2026-11-19)
            m.SetTexture("_EmissiveColorMap", rt); m.SetColor("_EmissiveColor", Color.white * 90f);
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
        });
        // 브라운관이면 유리 모양(둥근 모서리, 볼록한 면)을 따라 만든 메쉬에 화면을 띄운다(CrtScreenMesh).
        var crtScreen = crt ? CrtScreenMesh() : null;
        GameObject screen;
        if (crtScreen != null)
        {
            screen = MeshPart(tv, "Screen", crtScreen, screenMat, LLiving, 1f);
            screen.transform.localPosition = new Vector3(0, tvY, 0); // 받침대 위 TV 원점
            screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        else
        {
            screen = Prim(PrimitiveType.Quad, tv, "Screen", tvPos + new Vector3(0.235f, 0.86f, 0), new Vector3(0.62f, 0.46f, 1f), screenMat, LLiving);
            screen.transform.rotation = Quaternion.LookRotation(Vector3.left); // 보이는 면(-Z)이 방 쪽(+x)
        }
        // 2026-11-23 VARCO 2D TV 그림(소파에 얼굴 없는 사람, 4:3). 실시간 화면 2mm 앞에 같은 모양으로 덮는다.
        // 찾으면 TV 속 사람이 사라지는 장치는 그대로: 이 그림도 사람과 함께 숨겨(hideOnFound) 아래의 실시간 화면(빈 소파)이 드러난다.
        GameObject tvImage = null;
        var tvImg = Img2D("LivingRoom", "TV_FacelessFigure");
        if (tvImg != null)
        {
            var imgMat = Lit("GB_Living_TVImage", m =>
            {
                Plain(m, Color.black, 0f);
                m.SetTexture("_EmissiveColorMap", tvImg); m.SetColor("_EmissiveColor", Color.white * 200f); // 90이면 실시간 화면보다 어두웠다(2026-11-23 Play)
                m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
            });
            tvImage = Object.Instantiate(screen, screen.transform.parent);
            tvImage.name = "Screen_Image";
            tvImage.transform.SetPositionAndRotation(screen.transform.position, screen.transform.rotation);
            tvImage.transform.position += crtScreen != null ? screen.transform.TransformDirection(Vector3.forward) * 0.002f : screen.transform.TransformDirection(Vector3.back) * 0.002f;
            tvImage.GetComponent<MeshRenderer>().sharedMaterial = imgMat;
        }

        var sofaPos = new Vector3(47.4f, 0f, -118.6f);
        var sofa = new GameObject("Sofa").transform; sofa.SetParent(props, true);
        sofa.SetPositionAndRotation(sofaPos, Quaternion.LookRotation(Vector3.left));
        var sc = sofa.gameObject.AddComponent<BoxCollider>(); sc.size = new Vector3(2.2f, 0.9f, 0.95f); sc.center = new Vector3(0, 0.45f, 0);
        // 바닥까지 내린다(2026-11-20): 전에는 좌석이 2.5cm, 등받이가 24cm 떠 있었다.
        // 2026-11-20 VARCO 소파(2.1×0.83×0.92). 정면 +Z = TV 쪽, 뒷면이 더미 등받이 자리와 같다.
        if (VarcoInto(sofa, "LivingRoom", "Sofa", LLiving) == null)
        {
        PBox(sofa, "Seat", new Vector3(0, 0.235f, 0.05f), new Vector3(2.2f, 0.47f, 0.85f), fabric);
        PBox(sofa, "Back", new Vector3(0, 0.4975f, -0.36f), new Vector3(2.2f, 0.995f, 0.22f), fabric);
        PBox(sofa, "Arm_L", new Vector3(-1.02f, 0.45f, 0.05f), new Vector3(0.18f, 0.5f, 0.85f), fabric);
        PBox(sofa, "Arm_R", new Vector3(1.02f, 0.45f, 0.05f), new Vector3(0.18f, 0.5f, 0.85f), fabric);
        }

        // 소파에 앉은 사람: TV 화면에만 보인다(그림자 없음). 진짜 소파는 비어 있다.
        var ghost = new GameObject("TvOnly_SittingFigure").transform; ghost.SetParent(sofa, false);
        void G(PrimitiveType t, string n, Vector3 lc, Vector3 s, Vector3? e = null)
        {
            var go = GameObject.CreatePrimitive(t); go.name = n; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(ghost, false); go.transform.localPosition = lc; go.transform.localScale = s;
            if (e.HasValue) go.transform.localRotation = Quaternion.Euler(e.Value);
            go.layer = TvOnlyLayer;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = pale; r.renderingLayerMask = LLiving; r.shadowCastingMode = ShadowCastingMode.Off;
        }
        G(PrimitiveType.Capsule, "Torso", new Vector3(0.2f, 0.8f, -0.12f), new Vector3(0.4f, 0.32f, 0.26f));
        G(PrimitiveType.Sphere, "Head", new Vector3(0.2f, 1.28f, -0.1f), new Vector3(0.22f, 0.26f, 0.24f));
        G(PrimitiveType.Capsule, "Thigh_L", new Vector3(0.1f, 0.52f, 0.15f), new Vector3(0.14f, 0.22f, 0.14f), new Vector3(90, 0, 0));
        G(PrimitiveType.Capsule, "Thigh_R", new Vector3(0.32f, 0.52f, 0.15f), new Vector3(0.14f, 0.22f, 0.14f), new Vector3(90, 0, 0));
        G(PrimitiveType.Capsule, "Shin_L", new Vector3(0.1f, 0.225f, 0.4f), new Vector3(0.12f, 0.22f, 0.12f)); // 발이 바닥에 닿게(전에는 3cm 떴다)
        G(PrimitiveType.Capsule, "Shin_R", new Vector3(0.32f, 0.225f, 0.4f), new Vector3(0.12f, 0.22f, 0.12f));

        // TV 카메라: TV 자리에서 소파 쪽을 찍어 화면에 보낸다.
        var camGo = new GameObject("TV_Camera"); camGo.transform.SetParent(tv, false);
        camGo.transform.position = tvPos + new Vector3(0.3f, 1.25f, 0f);
        camGo.transform.rotation = Quaternion.LookRotation(sofaPos + Vector3.up * 0.7f - camGo.transform.position);
        var tvCam = camGo.AddComponent<Camera>();
        tvCam.targetTexture = rt; tvCam.fieldOfView = 26f; // 소파와 앉은 사람이 화면을 채우게 좁게
        tvCam.nearClipPlane = 0.1f; tvCam.farClipPlane = 30f; tvCam.depth = -10;
        tvCam.cullingMask = ~0;
        if (camGo.GetComponent<HDAdditionalCameraData>() == null) camGo.AddComponent<HDAdditionalCameraData>();

        Box(props, "Rug", new Vector3(44.2f, 0.005f, -118.6f), new Vector3(3.4f, 0.01f, 2.4f), Lit("GB_Living_Rug", m => Plain(m, new Color(0.3f, 0.12f, 0.1f), 0.1f)), LLiving);
        // VARCO 낮은 탁자(1.2×0.42×0.78): 긴 쪽이 소파와 나란하게(남북) 90도 돌린다.
        if (VarcoPrefab("LivingRoom", "CoffeeTable") != null)
        {
            var ct = PropGroup(props, "CoffeeTable", new Vector3(44.6f, 0f, -118.6f), 90f, new Vector3(1.2f, 0.42f, 0.78f));
            VarcoInto(ct, "LivingRoom", "CoffeeTable", LLiving);
        }
        else
        {
        Box(props, "CoffeeTable", new Vector3(44.6f, 0.4f, -118.6f), new Vector3(0.6f, 0.05f, 1.1f), wood, LLiving, true);
        foreach (var o in new[] { new Vector2(-0.25f, -0.5f), new Vector2(0.25f, -0.5f), new Vector2(-0.25f, 0.5f), new Vector2(0.25f, 0.5f) })
            Box(props, "TableLeg", new Vector3(44.6f + o.x, 0.19f, -118.6f + o.y), new Vector3(0.05f, 0.38f, 0.05f), wood, LLiving);
        }
        // 스탠드: 소파 옆, 따뜻한 주황빛 하나가 방을 겨우 밝힌다.
        var lampPos = new Vector3(48.6f, 0f, -116.9f);
        if (VarcoPrefab("LivingRoom", "FloorLamp") != null)
        {
            // VARCO 스탠드(높이 1.6). 전등(1.45m)이 갓 안에 들어가므로, 갓이 빛을 막지 않게 스탠드는 그림자를 만들지 않는다.
            var lg = PropGroup(props, "FloorLamp", lampPos, 0f, new Vector3(0.4f, 1.6f, 0.4f));
            var lm = VarcoInto(lg, "LivingRoom", "FloorLamp", LLiving, default, default, false);
            foreach (var r in lm.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }
        else
        {
        Box(props, "Lamp_Pole", lampPos + new Vector3(0, 0.75f, 0), new Vector3(0.04f, 1.5f, 0.04f), dark, LLiving);
        Box(props, "Lamp_Base", lampPos + new Vector3(0, 0.02f, 0), new Vector3(0.3f, 0.04f, 0.3f), dark, LLiving);
        Prim(PrimitiveType.Cylinder, props, "Lamp_Shade", lampPos + new Vector3(0, 1.55f, 0), new Vector3(0.42f, 0.16f, 0.42f), shade, LLiving);
        }
        // 북쪽 벽 가족사진(빈 액자), 동쪽 벽 밤 창문과 커튼
        // 2026-11-24 사용자 요청: 노란 방처럼 틀 없이 포스터(그림)만. VARCO 2D(LivingRoom/2D/VAR_LivingRoom_Poster_A~C.png)가 있으면 그것,
        // 없으면 예전 빈 액자. 크기는 액자 높이의 1.2배, 가로는 그림 비율.
        var photoSlots = new[] { new Vector3(42.6f, 1.7f, 0.5f), new Vector3(43.5f, 1.6f, 0.4f), new Vector3(44.6f, 1.75f, 0.55f) };
        for (int pi = 0; pi < photoSlots.Length; pi++)
        {
            var f = photoSlots[pi];
            var img = Img2D("LivingRoom", "Poster_" + (char)('A' + pi));
            if (img != null)
            {
                float ph = f.z * 1.25f * 1.2f;
                Poster(props, new Vector3(f.x, f.y, LvZ1 - 0.012f), 180f, ph * Img2DAspect(img), ph, img);
                continue;
            }
            Box(props, "PhotoFrame", new Vector3(f.x, f.y, LvZ1 - 0.02f), new Vector3(f.z, f.z * 1.25f, 0.03f), wood, LLiving);
            Box(props, "Photo", new Vector3(f.x, f.y, LvZ1 - 0.04f), new Vector3(f.z * 0.8f, f.z, 0.01f), pale, LLiving);
        }
        Box(props, "Window_Glass", new Vector3(LvX1 - 0.02f, 1.5f, -116.4f), new Vector3(0.02f, 1.1f, 1.6f), night, LLiving);
        Box(props, "Window_Frame", new Vector3(LvX1 - 0.03f, 1.5f, -116.4f), new Vector3(0.03f, 1.2f, 1.7f), wood, LLiving);
        // 2026-11-23 VARCO 커튼(봉 포함, 2.21×2.4×0.28, 원점 = 뒷면 가운데). 창 가운데에 걸고 정면(+Z)이 방 안(서쪽)을 본다. 콜라이더 없음.
        var curtain = new GameObject("Curtains").transform;
        curtain.SetParent(props, true);
        curtain.SetPositionAndRotation(new Vector3(LvX1 - 0.01f, 1.45f, -116.4f), Quaternion.Euler(0f, -90f, 0f));
        if (VarcoInto(curtain, "LivingRoom", "Curtains", LLiving, default, default, false) == null)
        {
        Object.DestroyImmediate(curtain.gameObject);
        Box(props, "Curtain_L", new Vector3(LvX1 - 0.08f, 1.45f, -115.4f), new Vector3(0.06f, 1.9f, 0.5f), Lit("GB_Living_Curtain", m => Plain(m, new Color(0.35f, 0.08f, 0.08f), 0.1f)), LLiving);
        Box(props, "Curtain_R", new Vector3(LvX1 - 0.08f, 1.45f, -117.4f), new Vector3(0.06f, 1.9f, 0.5f), AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/GB_Living_Curtain.mat"), LLiving);
        }

        // 2026-11-20 방 채우기(넓은데 휑하다는 피드백). 모두 벽 쪽이고, 입구(서쪽 z -112.4) → TV → 출구(남쪽 x 40.6)의 서쪽 동선은 비운다.
        // 북쪽 벽: 가족사진 아래 장식장(꽃병·탁상시계) / 동쪽 벽 북쪽: 책장 / 소파 남쪽 끝: 협탁(재떨이) / TV를 보는 안락의자 / 남동쪽 모서리: 화분.
        var leaf = Lit("GB_Living_Plant", m => Plain(m, new Color(0.12f, 0.2f, 0.1f), 0.2f));
        FillProp(props, "LivingRoom", "Sideboard", new Vector3(43.6f, 0f, LvZ1 - 0.31f), 180f, new Vector3(1.6f, 0.8f, 0.45f), g =>
        {
            PBox(g, "Body", new Vector3(0, 0.42f, 0), new Vector3(1.6f, 0.72f, 0.45f), wood);
            PBox(g, "Top", new Vector3(0, 0.79f, 0.01f), new Vector3(1.64f, 0.03f, 0.48f), wood);
            foreach (float x in new[] { -0.4f, 0.4f }) PBox(g, "Door", new Vector3(x, 0.42f, 0.226f), new Vector3(0.76f, 0.6f, 0.006f), trim);
            foreach (float x in new[] { -0.7f, 0.7f }) PBox(g, "Foot", new Vector3(x, 0.03f, 0), new Vector3(0.06f, 0.06f, 0.4f), trim);
            Part(g, PrimitiveType.Cylinder, "Vase", new Vector3(-0.5f, 0.93f, 0), new Vector3(0.12f, 0.12f, 0.12f), pale);
            PBox(g, "Clock", new Vector3(0.45f, 0.9f, 0), new Vector3(0.2f, 0.2f, 0.1f), trim);
        });
        // 장식장 위 작은 스탠드(따로 둔다: VARCO 장식장이 와도 불빛 자리는 같게). 스탠드 빛을 줄이자 장식장·사진이 어둠에 묻혀서
        // 이 작은 빛으로 북쪽 벽 가구 주변만 밝힌다(아래 Lights의 SideboardLamp).
        // 2026-11-24 사용자 요청: 스탠드 모양은 없앤다(VARCO 장식장은 꽃병·시계까지 한 덩어리라 윗면이 낮아 스탠드가 떠 보였다).
        // 빛(SideboardLamp)은 남기되 벽에 빛 얼룩이 맺히지 않게 장식장 앞 위쪽으로 옮겨 은은한 채움 빛으로 쓴다.
        var sbLampPos = new Vector3(43.75f, 0.81f, LvZ1 - 0.25f);
        if (VarcoPrefab("LivingRoom", "Sideboard") == null)
        {
            var sbLamp = PropGroup(props, "SideboardLamp", sbLampPos, 0f);
            Part(sbLamp, PrimitiveType.Cylinder, "Base", new Vector3(0, 0.12f, 0), new Vector3(0.1f, 0.12f, 0.1f), dark);
            Part(sbLamp, PrimitiveType.Cylinder, "Shade", new Vector3(0, 0.3f, 0), new Vector3(0.24f, 0.08f, 0.24f), shade);
        }
        FillProp(props, "LivingRoom", "Bookcase", new Vector3(LvX1 - 0.18f, 0f, -112.3f), -90f, new Vector3(0.9f, 1.9f, 0.34f), g =>
        {
            foreach (float x in new[] { -0.43f, 0.43f }) PBox(g, "Side", new Vector3(x, 0.95f, 0), new Vector3(0.04f, 1.9f, 0.34f), wood);
            PBox(g, "Back", new Vector3(0, 0.95f, -0.16f), new Vector3(0.9f, 1.9f, 0.02f), trim);
            for (int k = 0; k < 5; k++)
            {
                float y = 0.05f + k * 0.45f;
                PBox(g, "Board", new Vector3(0, y, 0), new Vector3(0.82f, 0.03f, 0.32f), wood);
                if (k == 4) continue;
                for (int i = 0; i < 8; i++)
                {
                    if ((i * 5 + k * 3) % 7 == 0) continue; // 군데군데 빈칸
                    float h = 0.26f + ((i + k) % 3) * 0.04f;
                    PBox(g, "Book", new Vector3(-0.33f + i * 0.09f, y + 0.015f + h / 2, 0.02f), new Vector3(0.07f, h, 0.24f), (i + k) % 2 == 0 ? fabric : trim);
                }
            }
        });
        FillProp(props, "LivingRoom", "SideTable", new Vector3(47.5f, 0f, -120.25f), -90f, new Vector3(0.45f, 0.55f, 0.45f), g =>
        {
            PBox(g, "Top", new Vector3(0, 0.53f, 0), new Vector3(0.45f, 0.04f, 0.45f), wood);
            foreach (var o in new[] { new Vector2(-0.19f, -0.19f), new Vector2(0.19f, -0.19f), new Vector2(-0.19f, 0.19f), new Vector2(0.19f, 0.19f) })
                PBox(g, "Leg", new Vector3(o.x, 0.26f, o.y), new Vector3(0.04f, 0.51f, 0.04f), wood);
            Part(g, PrimitiveType.Cylinder, "Ashtray", new Vector3(0.05f, 0.565f, 0.03f), new Vector3(0.14f, 0.015f, 0.14f), dark);
        });
        // 안락의자: 소파 남쪽에서 비스듬히 TV를 본다(정면 +Z → TV 방향).
        var armPos = new Vector3(45.6f, 0f, -122.6f);
        var toTv = tvPos - armPos;
        FillProp(props, "LivingRoom", "Armchair", armPos, Mathf.Atan2(toTv.x, toTv.z) * Mathf.Rad2Deg, new Vector3(0.85f, 0.95f, 0.85f), g =>
        {
            PBox(g, "Seat", new Vector3(0, 0.22f, 0.05f), new Vector3(0.85f, 0.44f, 0.75f), fabric);
            PBox(g, "Back", new Vector3(0, 0.6f, -0.33f), new Vector3(0.85f, 0.7f, 0.19f), fabric);
            foreach (float x in new[] { -0.36f, 0.36f }) PBox(g, "Arm", new Vector3(x, 0.55f, 0.05f), new Vector3(0.13f, 0.22f, 0.75f), fabric);
        });
        // 2026-11-23 VARCO 화분은 잎이 1.05m로 퍼져 모서리 벽에 박혔다 → 모서리에서 더 띄움
        FillProp(props, "LivingRoom", "PottedPlant", new Vector3(LvX1 - 0.56f, 0f, LvZ0 + 0.52f), 0f, new Vector3(0.4f, 0.5f, 0.4f), g =>
        {
            Part(g, PrimitiveType.Cylinder, "Pot", new Vector3(0, 0.2f, 0), new Vector3(0.34f, 0.2f, 0.34f), trim);
            Part(g, PrimitiveType.Sphere, "Leaves", new Vector3(0, 0.75f, 0), new Vector3(0.6f, 0.75f, 0.6f), leaf);
            Part(g, PrimitiveType.Sphere, "Leaves_Top", new Vector3(0.08f, 1.12f, -0.04f), new Vector3(0.38f, 0.45f, 0.38f), leaf);
        });

        var lights = Group(gen, "Lights");
        // 밤의 방이지만 가구와 벽이 읽혀야 한다. 2026-11-19: 방 모음 사진에서 거의 검게 보여서 노출 7.4 → 6.8(파란 침실과 같게),
        // 스탠드를 키워 소파 쪽에 주황빛이 고이게 하고, 바닥 아래 있던 채움 빛을 방 한가운데 높이로 올려 벽과 사진이 어렴풋이 보이게 했다.
        // (천장 바로 밑에 두면 등이 없는 천장에 빛 얼룩이 생긴다.)
        // 2026-11-20 빛을 가구 쪽으로 좁힌다: 스탠드 14 → 8m, 방 전체를 채우던 Fill 20000/12m → 탁자 주변 9000/6m. 방 모서리는 어둠에 묻힌다.
        MakeLight(lights, "Lamp", LightType.Point, lampPos + new Vector3(0, 1.45f, 0), Vector3.zero, new Color(1f, 0.62f, 0.32f), 110000f, 8f, LLiving, true);
        MakeLight(lights, "TV_Glow", LightType.Point, tvPos + new Vector3(0.7f, 0.9f, 0), Vector3.zero, new Color(0.6f, 0.72f, 1f), 10000f, 6f, LLiving, false, false);
        MakeLight(lights, "Fill", LightType.Point, new Vector3(44.4f, 1.4f, -118f), Vector3.zero, new Color(0.9f, 0.7f, 0.5f), 9000f, 6f, LLiving, false, false);
        MakeLight(lights, "SideboardLamp", LightType.Point, sbLampPos + (VarcoPrefab("LivingRoom", "Sideboard") != null ? new Vector3(0, 1.0f, -1.0f) : new Vector3(0, 0.28f, 0.15f)), Vector3.zero, new Color(1f, 0.62f, 0.32f), 6000f, VarcoPrefab("LivingRoom", "Sideboard") != null ? 4f : 3.5f, LLiving, false, false);
        var profile = RoomProfile("Living", 6.8f, 0.01f, 0.04f, new Color(0.7f, 0.55f, 0.42f), 40f, new Color(0.02f, 0.015f, 0.01f), 8f, 0f);
        LocalVolume(gen, "LocalVolume", new Vector3((LvX0 + LvX1) / 2, LvH / 2, (LvZ0 + LvZ1) / 2), new Vector3(LvX1 - LvX0, LvH, LvZ1 - LvZ0), 1f, 125f, profile);

        // 찾기 규칙: TV 속 소파에 사람이 앉아 있는데 진짜 소파는 비어 있다. TV를 바라보면 화면이 깜빡이고 사람이 사라지며,
        // TV 옆 모서리의 남쪽 벽(출구, 파란 침실로)이 열린다.
        var gate = FindGate(gen, "FindGate_Living", 6, "LivingTV",
            new Bounds(new Vector3(BdInX, 1.2f, -124.15f), new Vector3(1.8f, 2.4f, 0.3f)), "GB_03b_Living/GFX_Generated/Shell/South/Seg_0");
        Box(gate.wall.transform, "Baseboard", new Vector3(BdInX, 0.05f, LvZ0 + 0.02f), new Vector3(1.8f, 0.1f, 0.04f), trim, LLiving);
        var gz = AddGaze(tv.gameObject, "LivingTV", gate, new Bounds(new Vector3((LvX0 + LvX1) / 2, LvH / 2, (LvZ0 + LvZ1) / 2), new Vector3(LvX1 - LvX0, LvH, LvZ1 - LvZ0)));
        gz.hideOnFound = tvImage != null ? new[] { ghost.gameObject, tvImage } : new[] { ghost.gameObject };
        gz.blinkRenderers = tvImage != null ? new[] { screen.GetComponent<Renderer>(), tvImage.GetComponent<Renderer>() } : new[] { screen.GetComponent<Renderer>() };
        gz.blinkCount = 3; gz.blinkOff = 0.12f;
        // 섬뜩함이 바로 끝나지 않게(2026-11-18 플레이 피드백): 찾은 뒤에도 TV 속 사람이 6초 더 앉아 있다가 사라지고,
        // 출구는 그 뒤 6초가 더 지나야 열린다. 당황한 플레이어가 방을 한 번 더 둘러보게 한다.
        gz.hideDelay = 6f; gz.notifyDelay = 12f;
    }

    static RenderTexture TvRenderTexture()
    {
        const string path = "Assets/Textures/Greybox/GB_Living_TV.renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (rt != null) return rt;
        rt = new RenderTexture(320, 240, 24, RenderTextureFormat.ARGBHalf) { name = "GB_Living_TV" };
        AssetDatabase.CreateAsset(rt, path);
        return rt;
    }

    // ---------------------------------------------------------------- 새 통로 막기(SealBehindPassage)
    static void NewRoomSeals()
    {
        Seal("Seal_CrackToShower", "GB_02b_Shower", "GB_02b_Shower/GFX_Generated/Shell/South/Seg_0", Vector3.forward,
            new Vector3(ShInX, 1.1f, -103.4f), new Vector3(1.2f, 2.2f, 0.6f),
            new Vector3(ShInX, 1.5f, ShZ0 + 0.6f + 1.5f), new Vector3(6f, 3f, 3f), null);
        var yellow = Seal("Seal_ShowerToYellow", "GB_03_Yellow", "GB_03_Yellow/GFX_Generated/WallCovers/West_A", Vector3.right,
            new Vector3(20.025f, 1.2f, ShOutZ), new Vector3(0.35f, 2.4f, 1.8f),
            new Vector3(22.3f, 1.5f, ShOutZ), new Vector3(3f, 3f, 5f), null);
        var trimR = Find("GB_03_Yellow/GFX_Generated/Baseboard/W_A").GetComponent<MeshRenderer>();
        Box(yellow.plug.transform, "Baseboard", new Vector3(20.22f, 0.05f, ShOutZ), new Vector3(0.04f, 0.1f, 1.8f), trimR.sharedMaterial, trimR.renderingLayerMask);
        Seal("Seal_YellowToLiving", "GB_03b_Living", "GB_03b_Living/GFX_Generated/Shell/West/Seg_0", Vector3.right,
            new Vector3(38.6f, 1.2f, LvInZ), new Vector3(0.3f, 2.4f, 1.8f),
            new Vector3(LvX0 + 0.6f + 1.5f, 1.5f, LvInZ), new Vector3(3f, 3f, 3.6f), null);
        // 거실 → 파란 침실: 막힘 판은 침실 쪽 덧판 면(z -124.34)까지 두껍게 해 테두리가 보이지 않게 한다.
        Seal("Seal_LivingToBedroom", "GB_03c_Bedroom", "GB_03c_Bedroom/GFX_Generated/Shell/West/Seg_0", Vector3.back,
            new Vector3(BdInX, 1.2f, -124.17f), new Vector3(1.8f, 2.4f, 0.34f),
            new Vector3(BdInX, 1.5f, BdZ1 - 0.6f - 1.5f), new Vector3(3.6f, 3f, 3f), null);
        // 침실 → 천사 방은 통로가 아니라 복도 끝 문의 순간이동이다. 뒤에 남은 문은 BlueRoomDoor가 지운다(§6-3d).
    }
}
