using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-28 사용자 QA 2차. 메뉴 DEPTH 5m > QA 1128 > Apply 2.
// 1) 수영장 홀 입구 천장 "깨짐": 천장판이 남쪽 벽 두께(30cm)만큼 큰 홀 쪽으로 튀어나와, 입구 위에서 천장판 밑면과
//    위쪽 벽(Hall_Wall_South_Upper) 밑면이 같은 높이(4.0m)에 겹쳐 그려졌다(z-fighting) → 천장판을 벽 안쪽 면까지 줄인다.
//    예전에 반사·TAA·텍스처 필터를 바꿨지만 원인이 겹친 면이라 남아 있었다.
// 2) 샤워실 공포감: 거꾸로 솟는 물줄기·천장에 고인 물을 검붉은 핏물로, 김 서린 거울의 흘러내린 자국을 핏자국으로,
//    타일 벽 몇 군데에 흘러내린 피 얼룩(데칼).
// 3) 물에 들어갈 때 첨벙 소리 끔(WaterAudio.playEnterSplash, 사용자: 경박하다).
public static partial class GreyboxGraphicsPass
{
    const string HallEntryCeilingPath = "GB_01_PoolHall/GFX_Generated/EntryRoof/Ceiling";
    const string HallSouthUpperPath = "GB_01_PoolHall/Hall_Wall_South_Upper";

    static readonly Color BloodFlowBase = new Color(0.30f, 0.0f, 0.01f, 1f);
    static readonly Color BloodFlowEmissive = Color.black; // 예전 물줄기는 하얗게 빛났다(4~5). 빛나면 분홍으로 보여 핏물은 빛을 끈다
    static readonly Color BloodPoolTint = new Color(0.55f, 0.06f, 0.05f, 1f);
    static readonly Color BloodInk = new Color(0.36f, 0.015f, 0.02f, 1f);      // 거울 자국·벽 얼룩 색(sRGB)

    const string MirrorTexPath = "Assets/Textures/Greybox/GB_Shower_FoggedMirror.png";
    const string MirrorBloodTexPath = "Assets/Textures/Greybox/GB_Shower_FoggedMirror_Blood.png";
    const string BloodDecalDir = "Assets/Textures/Greybox";
    const string BloodDecalMatDir = "Assets/Materials/Greybox";
    const int BloodDecalSeed = 1128;

    /// <summary>피 얼룩 모양(2026-11-28 사용자: 다 같은 모양이라 바꿔 달라). 자리마다 다른 모양을 쓴다.</summary>
    enum BloodShape
    {
        /// <summary>위에서 문지른 얼룩과 거기서 흘러내린 줄기 여러 개.</summary>
        SmearDrips,
        /// <summary>손가락으로 긁어내린 듯한 비스듬한 줄 네 개(이어진 줄).</summary>
        FingerDrag,
        /// <summary>튄 피: 가운데 덩어리, 둘레에 흩어진 방울, 짧은 줄기 두셋.</summary>
        Splatter,
        /// <summary>작은 덩어리에서 길게 한 줄기만 진하게.</summary>
        LongDrip,
        /// <summary>가로로 쓸고 지나간 띠, 한쪽 끝에서만 흘러내림.</summary>
        Swipe,
    }

    /// <summary>얼룩을 붙일 곳: 방 안 한 점에서 수평 방향으로 쏴 닿은 벽. (안쪽 점, 방향, 높이, 크기 배율, 모양, 기울기(도))</summary>
    static readonly (Vector3 from, Vector3 dir, float y, float scale, BloodShape shape, float tilt)[] BloodSpots =
    {
        (new Vector3(9.0f, 0f, -100.5f), Vector3.back, 1.9f, 1.0f, BloodShape.SmearDrips, 0f),     // 남쪽 벽
        (new Vector3(15.5f, 0f, -100.5f), Vector3.back, 1.6f, 0.9f, BloodShape.Swipe, -4f),        // 남쪽 벽 오른쪽
        (new Vector3(8.5f, 0f, -101.5f), Vector3.left, 1.5f, 0.85f, BloodShape.FingerDrag, 6f),    // 서쪽 벽(거울 아래쪽 옆)
        (new Vector3(17.0f, 0f, -95.5f), Vector3.right, 1.5f, 1.15f, BloodShape.Splatter, 0f),     // 동쪽 샤워 칸 안 벽(샤워기 관 옆)
        (new Vector3(12.65f, 0f, -95.0f), Vector3.forward, 2.1f, 0.9f, BloodShape.LongDrip, 0f),   // 북쪽 샤워 칸 안 벽(샤워기 관 왼쪽)
        // 거울 위: 김 서린 거울을 피 묻은 손으로 문질러 닦은 자국(2026-11-28 QA: 거울 자국만으로는 핏자국 같지 않았다)
        (new Vector3(8.6f, 0f, -97.4f), Vector3.left, 1.85f, 0.95f, BloodShape.Swipe, 4f),
        (new Vector3(8.6f, 0f, -95.0f), Vector3.left, 1.95f, 0.75f, BloodShape.SmearDrips, -6f),
    };

    [MenuItem("DEPTH 5m/QA 1128/Apply 2")]
    public static void ApplyQa1128bMenu()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyQa1128b();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static void ApplyQa1128b()
    {
        TrimHallEntryCeiling();
        BloodShower();
        foreach (var wa in Object.FindObjectsByType<WaterAudio>(FindObjectsInactive.Include)) { wa.playEnterSplash = false; EditorUtility.SetDirty(wa); }
    }

    // ---- 1) 천장 -------------------------------------------------------------------------
    static void TrimHallEntryCeiling()
    {
        var ceil = GameObject.Find(HallEntryCeilingPath);
        var upper = GameObject.Find(HallSouthUpperPath);
        if (ceil == null || upper == null) { Debug.LogWarning("[QA1128b] 홀 입구 천장/위쪽 벽 없음"); return; }
        var r = ceil.GetComponent<Renderer>();
        float wallInner = upper.GetComponent<Renderer>().bounds.max.z; // 입구 쪽(북쪽) 면
        var b = r.bounds;
        if (b.min.z >= wallInner - 0.001f) { Debug.Log("[QA1128b] 천장은 이미 벽 안쪽까지"); return; }

        // 세계 z축이 천장의 어느 로컬 축인지 찾아 그 축 크기만 줄이고, 줄인 만큼 북쪽으로 옮긴다.
        var t = ceil.transform;
        Vector3 local = t.InverseTransformDirection(Vector3.forward);
        int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
        float oldSize = b.size.z, newSize = b.max.z - wallInner;
        var s = t.localScale; s[axis] *= newSize / oldSize; t.localScale = s;
        t.position += new Vector3(0f, 0f, (wallInner + b.max.z) * 0.5f - b.center.z);
        EditorUtility.SetDirty(t);
        Debug.Log($"[QA1128b] 홀 입구 천장 z {b.min.z:F2}~{b.max.z:F2} → {wallInner:F2}~{b.max.z:F2}");
    }

    // ---- 2) 샤워실 -------------------------------------------------------------------------
    static void BloodShower()
    {
        var flow = LoadMat("GB_Shower_FlowUp");
        if (flow != null)
        {
            flow.SetColor("_BaseColor", BloodFlowBase);
            flow.SetColor("_EmissiveColor", BloodFlowEmissive);
            if (flow.HasProperty("_EmissiveColorLDR")) flow.SetColor("_EmissiveColorLDR", BloodFlowEmissive);
            // 물처럼 매끈한 반사(0.95)와 반사 유지가 겹쳐 붉은 줄기가 분홍으로 바랬다 → 덜 매끈하게, 반사 유지 끔(진득한 핏물)
            flow.SetFloat("_Smoothness", 0.35f);
            if (flow.HasProperty("_EnableBlendModePreserveSpecularLighting")) flow.SetFloat("_EnableBlendModePreserveSpecularLighting", 0f);
            ValidateHdMaterial(flow); EditorUtility.SetDirty(flow);
        }
        var pool = LoadMat("GB_Shower_CeilingWet");
        if (pool != null) { pool.SetColor("_BaseColor", BloodPoolTint); ValidateHdMaterial(pool); EditorUtility.SetDirty(pool); }

        // 바라보는 동안·사라지는 동안에는 FadeAway가 따로 만든 투명 짝 재질로 바꿔 끼운다. 그 짝이 예전 하얀 물 색 그대로라
        // 특정 시야(응시 중)에서 핏물이 하얗게 돌아왔다(2026-11-28 QA) → 짝 재질도 원래 재질과 같은 색·반사로 맞춘다.
        var odd = GameObject.Find("GB_02b_Shower/GFX_Generated/Showers/Oddity_Shower_Up");
        if (odd != null)
            foreach (var fa in odd.GetComponentsInChildren<FadeAway>(true))
            {
                int k = 0;
                foreach (var r in fa.renderers)
                {
                    if (r == null) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        var fm = k < fa.fadeMaterials.Length ? fa.fadeMaterials[k] : null;
                        k++;
                        if (fm == null || m == null || fm == m) continue;
                        MatchFadePair(m, fm);
                    }
                }
            }

        // 기믹을 풀면(물줄기가 사라지면) 바로 물방울 소리가 멎게
        if (odd != null)
            foreach (var z in Object.FindObjectsByType<AudioZone>(FindObjectsInactive.Include))
                if (z.name.Contains("Shower")) { z.silenceWhenFound = odd.GetComponent<GazeTarget>(); EditorUtility.SetDirty(z); }

        var mirror = LoadMat("GB_Shower_Mirror");
        var bloodMirror = MakeBloodMirrorTexture();
        if (mirror != null && bloodMirror != null)
        {
            mirror.SetTexture("_BaseColorMap", bloodMirror);
            if (mirror.HasProperty("_MainTex")) mirror.SetTexture("_MainTex", bloodMirror);
            ValidateHdMaterial(mirror); EditorUtility.SetDirty(mirror);
        }

        PlaceBloodDecals();
    }

    /// <summary>투명 짝 재질의 색·발광·반사를 원래 재질과 같게 맞춘다(알파는 짝 재질 것을 둔다).</summary>
    static void MatchFadePair(Material src, Material fade)
    {
        foreach (var p in new[] { "_BaseColor", "_EmissiveColor", "_EmissiveColorLDR" })
        {
            if (!src.HasProperty(p) || !fade.HasProperty(p)) continue;
            var c = src.GetColor(p);
            if (p == "_BaseColor") c.a = fade.GetColor(p).a;
            fade.SetColor(p, c);
        }
        foreach (var p in new[] { "_Smoothness", "_EnableBlendModePreserveSpecularLighting" })
            if (src.HasProperty(p) && fade.HasProperty(p)) fade.SetFloat(p, src.GetFloat(p));
        ValidateHdMaterial(fade);
        EditorUtility.SetDirty(fade);
        Debug.Log($"[QA1128b] 짝 재질 맞춤 {src.name} → {fade.name}");
    }

    static Material LoadMat(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        Debug.LogWarning("[QA1128b] 재질 없음: " + name);
        return null;
    }

    /// <summary>
    /// 김 서린 거울 그림에서 배경(김)보다 어두운 곳(흘러내린 자국·얼룩)을 핏빛으로 바꾼 새 텍스처를 만든다. 원본은 그대로 둔다.
    /// </summary>
    static Texture2D MakeBloodMirrorTexture()
    {
        if (!System.IO.File.Exists(MirrorTexPath)) { Debug.LogWarning("[QA1128b] 거울 텍스처 없음"); return null; }
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(System.IO.File.ReadAllBytes(MirrorTexPath));
        var px = src.GetPixels32();

        // 배경 밝기 = 밝기 분포의 중앙값(대부분이 김 서린 바탕)
        var lums = new float[px.Length];
        for (int i = 0; i < px.Length; i++) lums[i] = (0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b) / 255f;
        var sorted = (float[])lums.Clone(); System.Array.Sort(sorted);
        float bg = Mathf.Max(0.05f, sorted[sorted.Length / 2]);

        int w = src.width, h = src.height;
        var darkness = new float[px.Length];
        for (int i = 0; i < px.Length; i++) darkness[i] = Mathf.Clamp01((bg - lums[i]) / bg * 3f); // 바탕보다 어두울수록 1
        const int Side = 6; // 가는 줄인지 볼 옆 거리(px)
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float dark = darkness[i];
                if (dark <= 0.2f) continue; // 김 서린 바탕의 얼룩덜룩함은 그대로(전체가 분홍으로 물들었다)
                // 가는 흘러내린 자국만 피로: 양옆이 밝으면 가는 줄. 넓은 구름 그림은 양옆도 어두워 그대로 둔다.
                // 양옆이 모두 밝아야 줄(한쪽만 밝으면 구름 가장자리라 빨간 테두리가 생겼다)
                float sideDark = Mathf.Max(darkness[y * w + Mathf.Max(0, x - Side)], darkness[y * w + Mathf.Min(w - 1, x + Side)]);
                float line = Mathf.Clamp01((dark - sideDark) * 2.5f);
                if (line <= 0f) continue;
                Color c = px[i];
                var ink = Color.Lerp(BloodFresh, BloodDeep, dark * 0.45f); // 가는 줄이라 어두우면 갈색 선으로만 보였다 → 선홍 쪽으로
                ink.a = c.a;
                px[i] = Color.Lerp(c, ink, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.6f, dark)) * line);
            }
        var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        dst.SetPixels32(px); dst.Apply();
        System.IO.File.WriteAllBytes(MirrorBloodTexPath, dst.EncodeToPNG());
        Object.DestroyImmediate(src); Object.DestroyImmediate(dst);
        AssetDatabase.ImportAsset(MirrorBloodTexPath, ImportAssetOptions.ForceUpdate);
        CopyImporterSettings(MirrorTexPath, MirrorBloodTexPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(MirrorBloodTexPath);
    }

    static void CopyImporterSettings(string fromPath, string toPath)
    {
        var from = AssetImporter.GetAtPath(fromPath) as TextureImporter;
        var to = AssetImporter.GetAtPath(toPath) as TextureImporter;
        if (from == null || to == null) return;
        var s = new TextureImporterSettings(); from.ReadTextureSettings(s); to.SetTextureSettings(s);
        to.maxTextureSize = from.maxTextureSize; to.textureCompression = from.textureCompression;
        to.SaveAndReimport();
    }

    // ---- 피 얼룩 데칼 ----------------------------------------------------------------------
    static void PlaceBloodDecals()
    {
        var root = GameObject.Find("GB_02b_Shower/GFX_Generated");
        if (root == null) return;
        var old = root.transform.Find("BloodSmears");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject("BloodSmears").transform;
        group.SetParent(root.transform, false);

        var mats = new Dictionary<BloodShape, Material>();
        int placed = 0;
        foreach (var spot in BloodSpots)
        {
            if (!mats.TryGetValue(spot.shape, out var mat)) mats[spot.shape] = mat = BloodDecalMaterial(spot.shape);
            if (mat == null) continue;
            var origin = new Vector3(spot.from.x, spot.y, spot.from.z);
            if (!Physics.Raycast(origin, spot.dir, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) { Debug.LogWarning($"[QA1128b] 벽 못 찾음 {origin} {spot.dir}"); continue; }
            var go = new GameObject("BloodSmear_" + placed + "_" + spot.shape);
            go.transform.SetParent(group, true);
            go.transform.position = hit.point + hit.normal * 0.15f;
            // 벽을 향하되 조금 기울여 줄 맞춘 느낌을 없앤다
            go.transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up) * Quaternion.Euler(0f, 0f, spot.tilt);
            var dp = go.AddComponent<DecalProjector>();
            dp.material = mat;
            var size = BloodDecalSize(spot.shape);
            dp.size = new Vector3(size.x * spot.scale, size.y * spot.scale, 0.4f);
            dp.pivot = new Vector3(0f, -0.3f * size.y * spot.scale, 0.2f); // 얼룩 위쪽이 맞힌 높이쯤, 흐름은 아래로
            dp.fadeFactor = 0.92f;
            placed++;
        }
        Debug.Log($"[QA1128b] 피 얼룩 {placed}곳, 모양 {mats.Count}가지");
        // 첫 시도의 한 가지 모양 파일은 지운다
        AssetDatabase.DeleteAsset(BloodDecalMatDir + "/GB_Shower_BloodSmear.mat");
        AssetDatabase.DeleteAsset(BloodDecalDir + "/GB_Shower_BloodSmear.png");
    }

    /// <summary>모양별 데칼 크기(가로, 세로 m).</summary>
    static Vector2 BloodDecalSize(BloodShape s)
    {
        switch (s)
        {
            case BloodShape.FingerDrag: return new Vector2(0.7f, 1.3f);
            case BloodShape.Splatter: return new Vector2(1.1f, 1.1f);
            case BloodShape.LongDrip: return new Vector2(0.45f, 1.9f);
            case BloodShape.Swipe: return new Vector2(1.4f, 1.0f);
            default: return new Vector2(0.9f, 1.7f);
        }
    }

    static Material BloodDecalMaterial(BloodShape shape)
    {
        var tex = MakeBloodTexture(shape);
        string path = BloodDecalMatDir + "/GB_Shower_Blood_" + shape + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var sh = Shader.Find("HDRP/Decal");
            if (sh == null) { Debug.LogWarning("[QA1128b] HDRP/Decal 셰이더 없음"); return null; }
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_BaseColorMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_AffectAlbedo")) mat.SetFloat("_AffectAlbedo", 1f);
        // 색만 바꾼다. 매끈함·AO·금속을 같이 바꾸면 데칼 상자 전체가 옅은 네모로 보였다.
        foreach (var f in new[] { "_AffectNormal", "_AffectMetal", "_AffectAO", "_AffectSmoothness", "_AffectEmission" })
            if (mat.HasProperty(f)) mat.SetFloat(f, 0f);
        if (mat.HasProperty("_DecalBlend")) mat.SetFloat("_DecalBlend", 1f);
        mat.enableInstancing = true;
        ValidateHdMaterial(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---- 모양 그리기(알파 칸에 진하기를 쌓는다) ----------------------------------------------------

    /// <summary>타원 덩어리(가장자리로 갈수록 옅어짐).</summary>
    static void Blob(float[] a, int W, int H, float cx, float cy, float rx, float ry, float strength)
    {
        for (int y = Mathf.Max(0, (int)(cy - ry)); y < Mathf.Min(H, (int)(cy + ry) + 1); y++)
            for (int x = Mathf.Max(0, (int)(cx - rx)); x < Mathf.Min(W, (int)(cx + rx) + 1); x++)
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry, d = dx * dx + dy * dy;
                if (d < 1f) { int i = y * W + x; a[i] = Mathf.Max(a[i], strength * (1f - d * d)); }
            }
    }

    /// <summary>위(top)에서 아래(bottom)로 흐르는 줄기. 아래로 갈수록 가늘고 끝에 방울이 맺힌다. slope는 1px 내려갈 때 옆으로 가는 양.</summary>
    static void Drip(float[] a, int W, int H, float x0, float top, float bottom, float width, float strength, float slope, int seed, bool bead = true)
    {
        for (int y = (int)bottom; y < (int)top; y++)
        {
            if (y < 0 || y >= H) continue;
            float t = (top - y) / Mathf.Max(1f, top - bottom);
            float cx = x0 + (top - y) * slope + Mathf.Sin(y * 0.05f + seed) * 1.2f;
            float half = width * (1f - 0.4f * t) * 0.5f;
            for (int x = (int)(cx - half - 2); x <= (int)(cx + half + 2); x++)
            {
                if (x < 0 || x >= W) continue;
                float e = 1f - Mathf.Clamp01(Mathf.Abs(x - cx) - half + 1f);
                int i = y * W + x; a[i] = Mathf.Max(a[i], strength * e);
            }
        }
        if (bead) Blob(a, W, H, x0 + (top - bottom) * slope, bottom, width * 0.9f, width * 1.1f, strength);
    }

    // 피 색(sRGB). 2026-11-28 QA: 부드럽게 번진 한 가지 색이라 잉크·물감 같았다 → 가장자리는 또렷하게 들쭉날쭉,
    // 얇게 묻은 곳은 밝은 선홍, 고인 곳은 검붉게, 마른 가장자리는 테두리처럼 더 진하게(커피 링), 둘레에 잔 방울.
    static readonly Color BloodFresh = new Color(0.55f, 0.03f, 0.035f);
    static readonly Color BloodDeep = new Color(0.22f, 0.008f, 0.012f);
    static readonly Color BloodDried = new Color(0.16f, 0.02f, 0.015f);

    /// <summary>여러 겹 펄린 잡음(0~1).</summary>
    static float Fbm(float x, float y)
    {
        float v = 0f, amp = 0.5f, f = 1f;
        for (int o = 0; o < 4; o++) { v += amp * Mathf.PerlinNoise(x * f, y * f); f *= 2.1f; amp *= 0.5f; }
        return v / 0.9375f;
    }

    /// <summary>
    /// 진하기 칸(a)을 실제 핏자국처럼 칠한다. 가장자리를 잡음으로 비틀어 또렷하게 자르고(물감처럼 번지지 않게),
    /// 얇은 곳은 선홍·두꺼운 곳은 검붉게·가장자리는 말라붙은 테두리, 둘레에 잔 방울을 흩뿌린다.
    /// </summary>
    static Color[] FinishBlood(float[] a, int W, int H, int seed)
    {
        var px = new Color[W * H];
        var rng = new System.Random(BloodDecalSeed * 7 + seed);
        float ox = (float)rng.NextDouble() * 50f, oy = (float)rng.NextDouble() * 50f;

        // 잔 방울: 진한 곳 근처에 작은 점들(방향 없이 흩어짐)
        var dots = new float[W * H];
        for (int k = 0; k < 160; k++)
        {
            int x = rng.Next(W), y = rng.Next(H);
            // 진한 곳에서 너무 먼 점은 버린다(둘레 18px 안에 진한 칸이 있어야)
            bool near = false;
            for (int t = 0; t < 8 && !near; t++)
            {
                int sx = Mathf.Clamp(x + rng.Next(-18, 19), 0, W - 1), sy = Mathf.Clamp(y + rng.Next(-18, 19), 0, H - 1);
                near = a[sy * W + sx] > 0.5f;
            }
            if (!near) continue;
            float r = 0.8f + 2.6f * (float)(rng.NextDouble() * rng.NextDouble());
            for (int yy = (int)(y - r - 1); yy <= y + r + 1; yy++) for (int xx = (int)(x - r - 1); xx <= x + r + 1; xx++)
            {
                if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                float d = Mathf.Sqrt((xx - x) * (xx - x) + (yy - y) * (yy - y));
                dots[yy * W + xx] = Mathf.Max(dots[yy * W + xx], Mathf.Clamp01(r + 0.5f - d));
            }
        }

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                // 비튼 자리에서 진하기를 읽어 가장자리를 들쭉날쭉하게
                float n = Fbm(x * 0.045f + ox, y * 0.045f + oy);
                int wx = Mathf.Clamp(x + (int)((n - 0.5f) * 10f), 0, W - 1);
                int wy = Mathf.Clamp(y + (int)((Fbm(x * 0.045f + oy, y * 0.045f + ox) - 0.5f) * 10f), 0, H - 1);
                float v = Mathf.Max(a[wy * W + wx], dots[i]);
                // 또렷한 가장자리(번진 띠가 아니라 묻은 자국)
                float body = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.36f, v + (n - 0.5f) * 0.12f));
                if (body <= 0f) { px[i] = new Color(BloodDeep.r, BloodDeep.g, BloodDeep.b, 0f); continue; }
                float thick = Mathf.Clamp01((v - 0.35f) / 0.6f);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(v - 0.36f) / 0.1f); // 가장자리 바로 안쪽 띠
                var c = Color.Lerp(BloodFresh, BloodDeep, Mathf.Pow(thick, 0.7f));
                c = Color.Lerp(c, BloodDried, ring * 0.6f);
                c *= 0.85f + 0.3f * Fbm(x * 0.12f + 3f, y * 0.12f + 7f);   // 얼룩덜룩한 농담
                c.a = body * Mathf.Lerp(0.72f, 0.97f, Mathf.Max(thick, ring));
                px[i] = c;
            }
        return px;
    }

    /// <summary>모양별 피 얼룩 텍스처를 만든다(같은 모양·씨앗이면 늘 같은 그림).</summary>
    static Texture2D MakeBloodTexture(BloodShape shape)
    {
        const int W = 256, H = 512;
        var rng = new System.Random(BloodDecalSeed + (int)shape * 101);
        System.Func<float> R = () => (float)rng.NextDouble();
        var a = new float[W * H];

        switch (shape)
        {
            case BloodShape.SmearDrips:
                for (int k = 0; k < 18; k++) Blob(a, W, H, W * (0.25f + 0.5f * R()), H * (0.70f + 0.12f * R()), W * (0.08f + 0.12f * R()), H * (0.025f + 0.04f * R()), 0.75f + 0.25f * R());
                for (int k = 0; k < 11; k++) Drip(a, W, H, W * (0.22f + 0.56f * R()), H * (0.74f + 0.06f * R()), H * (0.05f + 0.5f * R()), 6f + 8f * R(), 0.95f, 0f, k);
                break;

            case BloodShape.FingerDrag:
                // 위쪽 손바닥 자국 같은 덩어리, 거기서 비스듬히 끌린 네 줄
                Blob(a, W, H, W * 0.42f, H * 0.86f, W * 0.22f, H * 0.07f, 0.9f);
                for (int f = 0; f < 4; f++)
                {
                    float x0 = W * (0.24f + 0.12f * f) + 6f * R();
                    float top = H * (0.86f - 0.02f * f), bottom = H * (0.18f + 0.15f * R());
                    // 끊긴 조각으로 그리면 네모 블록처럼 보였다 → 이어진 한 줄, 아래로 갈수록 옅고 가늘게
                    Drip(a, W, H, x0, top, bottom, 14f + 6f * R(), 0.8f + 0.15f * R(), 0.12f, f * 7, f % 2 == 0);
                }
                break;

            case BloodShape.Splatter:
                Blob(a, W, H, W * 0.5f, H * 0.62f, W * 0.16f, H * 0.07f, 1f);
                Blob(a, W, H, W * 0.42f, H * 0.66f, W * 0.1f, H * 0.05f, 0.9f);
                for (int k = 0; k < 70; k++)
                {
                    float ang = R() * Mathf.PI * 2f, dist = 25f + 95f * R() * R();
                    float r = 2f + 7f * R() * (1f - dist / 130f);
                    Blob(a, W, H, W * 0.5f + Mathf.Cos(ang) * dist, H * 0.62f + Mathf.Sin(ang) * dist * 0.9f, r, r, 0.85f);
                }
                for (int k = 0; k < 3; k++) Drip(a, W, H, W * (0.38f + 0.24f * R()), H * 0.6f, H * (0.3f + 0.15f * R()), 7f + 5f * R(), 0.9f, 0f, k);
                break;

            case BloodShape.LongDrip:
                Blob(a, W, H, W * 0.5f, H * 0.9f, W * 0.18f, H * 0.04f, 0.95f);
                Drip(a, W, H, W * 0.52f, H * 0.9f, H * 0.04f, 20f, 1f, 0.01f, 3);
                Drip(a, W, H, W * 0.36f, H * 0.89f, H * 0.7f, 8f, 0.85f, 0f, 5);
                break;

            case BloodShape.Swipe:
                // 왼쪽에서 오른쪽으로 쓸며 옅어지는 띠(위아래 가장자리가 들쭉날쭉), 왼쪽 진한 곳에서만 흘러내림
                for (int k = 0; k < 40; k++)
                {
                    float t = k / 39f;
                    Blob(a, W, H, W * (0.08f + 0.84f * t), H * (0.72f + 0.04f * Mathf.Sin(t * 5f) + 0.02f * R()), W * 0.07f, H * (0.05f - 0.025f * t), 1f - 0.6f * t);
                }
                for (int k = 0; k < 4; k++) Drip(a, W, H, W * (0.1f + 0.12f * k + 0.05f * R()), H * 0.7f, H * (0.2f + 0.3f * R()), 8f + 6f * R(), 0.95f - 0.12f * k, 0f, k);
                break;
        }

        var px = FinishBlood(a, W, H, (int)shape);
        string texPath = BloodDecalDir + "/GB_Shower_Blood_" + shape + ".png";
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
        imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
    }
}
