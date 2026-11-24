using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-24 플레이 피드백 묶음(후처리 전에 찾은 더미·틈).
// 1) 인트로 중정 벽이 16m라 동쪽 벽 위로 물 차오르는 마을 바깥벽(80m)이 살짝 보였다 → 벽·벽 기둥·파라펫을 24m로 올린다.
//    벽은 IntroMapBuilder가 한 번 만든 것(Environment)이라 여기서 위에 덧벽을 얹는다. 같은 수영장 복사본(GB_05_SameIntro)은 그대로.
// 2) 노란 방에 남은 처음 기획 자리표시(Placeholder_Props): 앉은 삐에로 인형(Pierrot_Doll, 지금은 키다리 삐에로가 대신)과
//    "바뀌는 포스터"(Poster_Changes, 검은 판). 둘 다 참조하는 스크립트가 없다. 인형은 숨기고, 포스터 자리는 숨겼다가 그림이 오면 포스터 D로 바꾼다.
public static partial class GreyboxGraphicsPass
{
    const float IntroWallTop = 24f; // 원래 16m(IntroMapBuilder.WallHeight). 21m에서는 남동쪽 모서리 너머로 아직 조금 보였고 24m면 중정 어디서 봐도 옆 방이 안 보인다(광선 검사, 여유 3°)

    static void RaiseIntroWalls()
    {
        var env = GameObject.Find("Environment");
        if (env == null) return;
        var et = env.transform;
        int walls = 0, cols = 0;
        foreach (var side in new[] { "North", "South", "East", "West" })
        {
            var cap = et.Find("Wall_" + side + "_Cap");
            if (cap == null) continue;
            var old = et.Find("Wall_" + side + "_Raise");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var capR = cap.GetComponent<MeshRenderer>();
            var wallR = et.Find("Wall_" + side)?.GetComponent<MeshRenderer>();
            var m = wallR != null ? wallR.sharedMaterial : capR.sharedMaterial; // 벽과 같은 재질(그레이박스 패스가 바꾼 것)
            // 덧벽: 원래 벽 두께(0.6) × 파라펫 길이, 16 → IntroWallTop
            var s = cap.localScale;
            bool alongX = s.x > s.z;
            var size = alongX ? new Vector3(s.x, IntroWallTop - 16f, 0.6f) : new Vector3(0.6f, IntroWallTop - 16f, s.z);
            var raise = GameObject.CreatePrimitive(PrimitiveType.Cube);
            raise.name = "Wall_" + side + "_Raise";
            Object.DestroyImmediate(raise.GetComponent<Collider>());
            raise.transform.SetParent(et, true);
            raise.transform.position = new Vector3(cap.position.x, 16f + size.y / 2f, cap.position.z);
            raise.transform.localScale = size;
            var rr = raise.GetComponent<MeshRenderer>(); rr.sharedMaterial = m; rr.renderingLayerMask = capR.renderingLayerMask;
            cap.position = new Vector3(cap.position.x, IntroWallTop + s.y / 2f, cap.position.z);
            EditorUtility.SetDirty(cap);
            walls++;
        }
        foreach (Transform c in et)
        {
            if (!c.name.StartsWith("Wall_Col_")) continue;
            c.localScale = new Vector3(c.localScale.x, IntroWallTop / 2f, c.localScale.z); // 원기둥 높이 = 2 × scale.y
            c.position = new Vector3(c.position.x, IntroWallTop / 2f, c.position.z);
            EditorUtility.SetDirty(c);
            cols++;
        }
        Debug.Log($"[GreyboxGraphicsPass] 인트로 벽 {walls}면·벽 기둥 {cols}개를 {IntroWallTop}m로");
    }

    static void TidyYellowPlaceholders()
    {
        var room = GameObject.Find("GB_03_Yellow");
        if (room == null) return;
        var ph = room.transform.Find("Placeholder_Props");
        if (ph == null) return;
        var doll = ph.Find("Pierrot_Doll");
        if (doll != null) { doll.gameObject.SetActive(false); EditorUtility.SetDirty(doll.gameObject); }
        var pc = ph.Find("Poster_Changes");
        if (pc != null)
        {
            var old = ph.Find("Poster_VAR_Yellow_Poster_D");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var img = Img2D("Yellow", "Poster_D");
            var r = pc.GetComponent<MeshRenderer>();
            if (img != null)
            {
                propLayer = r.renderingLayerMask;
                // 검은 판(동쪽 벽 x 36.1, 0.7×1.0) 자리에 포스터, 방 안(-X)을 본다
                Poster(ph, new Vector3(pc.position.x - 0.03f, pc.position.y, pc.position.z), 270f, 1.0f * Img2DAspect(img), 1.0f, img);
                r.enabled = false;
            }
            else r.enabled = false; // 그림이 오기 전에는 검은 판도 숨긴다(쓰임 없는 자리표시)
            EditorUtility.SetDirty(r);
        }
    }

    // ---------------------------------------------------------------- 천사 방 바닥 구름 안개
    // 2026-11-24 사용자 피드백 "구름이 여전히 비어 보인다": 구름 덩어리 사이를 바닥에 깔리는 국소 볼륨 안개로 채운다.
    // 바닥 위 약 1.3m까지 하얗고, 위로 갈수록 옅어진다. 3D 노이즈로 뭉게뭉게하게, 아주 천천히 흐른다.
    const float AngelFogFreePath = 6f, AngelFogHeight = 1.3f; // 플레이 캡처로 맞춤: 1.6이면 바닥이 하얀 판처럼 막혔다
    // 국소 볼륨 안개는 방 안개가 볼류메트릭일 때만 그려진다. 천사 방은 "빛이 없으면 회색으로 탁해진다"고 일반 안개만 썼는데(ApplyAngel),
    // 하늘빛(라이트 프로브)을 다 받게 하면(1) 하얗게 밝다. 방 전체 안개는 40m면 눈까지 바래서 아주 옅게(250m) 한다.
    const float AngelRoomFogFreePath = 250f, AngelFogProbe = 1f; // 40·90이면 하늘 벽·눈까지 바랬다

    static void AngelCloudFog()
    {
        var root = GameObject.Find("GB_04_Angel");
        if (root == null) return;
        var old = root.transform.Find("CloudFloorFog");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("CloudFloorFog");
        go.transform.SetParent(root.transform, true);
        go.transform.position = new Vector3(AngelCx, AngelFogHeight / 2f - 0.3f, -128.85f); // 바닥(0) 아래 0.3m부터
        var fog = go.AddComponent<LocalVolumetricFog>();
        var p = fog.parameters;
        p.albedo = new Color(0.9f, 0.95f, 1f);
        p.meanFreePath = AngelFogFreePath;
        p.size = new Vector3(20.4f, AngelFogHeight, 28.2f);
        p.positiveFade = new Vector3(0.08f, 1f, 0.08f); // 위로 갈수록 끝까지 옅어지고, 벽 쪽 가장자리만 살짝
        p.negativeFade = new Vector3(0.08f, 0f, 0.08f);
        p.volumeMask = CloudNoise3D();
        p.textureTiling = new Vector3(1.5f, 1f, 2f);
        p.textureScrollingSpeed = new Vector3(0.006f, 0f, 0.004f);
        p.distanceFadeStart = 60f; p.distanceFadeEnd = 80f;
        fog.parameters = p;
        EditorUtility.SetDirty(fog);

        var prof = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/HDRP_GB_Angel_Profile.asset");
        if (prof != null && prof.TryGet<Fog>(out var rf))
        {
            rf.enableVolumetricFog.Override(true);
            rf.globalLightProbeDimmer.Override(AngelFogProbe);
            rf.meanFreePath.Override(AngelRoomFogFreePath);
            EditorUtility.SetDirty(rf); EditorUtility.SetDirty(prof);
        }
    }

    /// <summary>타일되는 64³ 구름 노이즈(알파 = 밀도). 한 번 만들어 Textures/Greybox에 둔다.</summary>
    static Texture3D CloudNoise3D()
    {
        const string path = "Assets/Textures/Greybox/GB_CloudNoise3D.asset";
        var t = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
        if (t != null) return t;
        const int N = 64;
        var rng = new System.Random(1124);
        float[] lattice(int g) { var a = new float[g * g * g]; for (int i = 0; i < a.Length; i++) a[i] = (float)rng.NextDouble(); return a; }
        float Noise(float[] L, int g, float x, float y, float z)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
            float fx = x - x0, fy = y - y0, fz = z - z0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float V(int i, int j, int k) => L[((i % g + g) % g) + ((j % g + g) % g) * g + ((k % g + g) % g) * g * g];
            float a = Mathf.Lerp(Mathf.Lerp(V(x0, y0, z0), V(x0 + 1, y0, z0), fx), Mathf.Lerp(V(x0, y0 + 1, z0), V(x0 + 1, y0 + 1, z0), fx), fy);
            float b = Mathf.Lerp(Mathf.Lerp(V(x0, y0, z0 + 1), V(x0 + 1, y0, z0 + 1), fx), Mathf.Lerp(V(x0, y0 + 1, z0 + 1), V(x0 + 1, y0 + 1, z0 + 1), fx), fy);
            return Mathf.Lerp(a, b, fz);
        }
        int[] grids = { 4, 8, 16 }; float[] amps = { 0.55f, 0.3f, 0.15f };
        var Ls = new float[grids.Length][];
        for (int o = 0; o < grids.Length; o++) Ls[o] = lattice(grids[o]);
        var px = new Color[N * N * N];
        for (int z = 0; z < N; z++) for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float v = 0f;
            for (int o = 0; o < grids.Length; o++) { float s = grids[o] / (float)N; v += amps[o] * Noise(Ls[o], grids[o], x * s, y * s, z * s); }
            float d = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, v)); // 뭉치고 사이는 옅게
            px[x + y * N + z * N * N] = new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 1f, d));
        }
        t = new Texture3D(N, N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "GB_CloudNoise3D" };
        t.SetPixels(px); t.Apply();
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    // ---------------------------------------------------------------- 거실·파란 방 카펫, 거실 창밖 (2D 그림이 오면)
    // VAR_LivingRoom_Carpet.png(이음매 없는 카펫)를 거실 바닥과 파란 방 바닥 전체에 깐다. 파란 방은 파란 조명이라 같은 그림이어도 파랗게 보인다.
    // VAR_LivingRoom_WindowView.png는 커튼 뒤 창 유리 자리에 붙이고 살짝 빛나게 한다(밤 창밖).
    const float CarpetTileMeters = 1.5f, WindowViewNits = 6f;

    static void ApplyLivingImages()
    {
        var carpet = Img2D("LivingRoom", "Carpet");
        if (carpet != null)
        {
            foreach (var name in new[] { "GB_Living_Carpet", "GB_Bedroom_Carpet" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");
                if (m == null) continue;
                m.SetTexture("_BaseColorMap", carpet);
                m.SetColor("_BaseColor", name == "GB_Living_Carpet" ? Color.white : new Color(0.85f, 0.85f, 0.85f));
                m.SetFloat("_UVBase", 5f); m.SetFloat("_TexWorldScale", 1f / CarpetTileMeters);
                UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(m);
                EditorUtility.SetDirty(m);
            }
        }
        // 2026-11-24 사용자 "거실 러그 그림 새로": VAR_LivingRoom_Rug.png(3:2, 러그 한 장 전체)가 오면 탁자 밑 러그(3.4×2.4m 판) 윗면에 한 장 그대로 붙인다.
        var rugImg = Img2D("LivingRoom", "Rug");
        var rugMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/GB_Living_Rug.mat");
        if (rugImg != null && rugMat != null)
        {
            rugMat.SetTexture("_BaseColorMap", rugImg);
            rugMat.SetColor("_BaseColor", Color.white);
            rugMat.SetFloat("_UVBase", 0f); rugMat.SetFloat("_Smoothness", 0.1f);
            UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(rugMat);
            EditorUtility.SetDirty(rugMat);
        }
        var view = Img2D("LivingRoom", "WindowView");
        var living = GameObject.Find("GB_03b_Living");
        if (view == null || living == null) return;
        Transform glass = null;
        foreach (var t in living.GetComponentsInChildren<Transform>(true)) if (t.name == "Window_Glass") glass = t;
        if (glass == null) return;
        var old = glass.parent.Find("Poster_" + view.name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var gr = glass.GetComponent<MeshRenderer>();
        propLayer = gr.renderingLayerMask;
        gr.enabled = false; EditorUtility.SetDirty(gr);
        // 유리 앞면(-X, 방 쪽)에 1.6×1.1 그림. 동쪽 벽이라 방 안(-X)을 본다.
        // 창틀(Window_Frame)이 유리 앞 3cm까지 나와 있어 그 앞에 붙인다(-0.015로 붙였더니 틀에 가려 안 보였다, 2026-11-24 플레이 확인). 커튼(49.71~)보다는 뒤.
        Poster(glass.parent, glass.position + new Vector3(-0.04f, 0f, 0f), 270f, 1.6f, 1.1f, view);
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/GB_Prop_Poster_{view.name}.mat");
        if (mat != null)
        {
            Emissive(mat, Color.white, WindowViewNits);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_EmissiveColorMap", view);
            UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(mat);
            EditorUtility.SetDirty(mat);
        }
    }
}
