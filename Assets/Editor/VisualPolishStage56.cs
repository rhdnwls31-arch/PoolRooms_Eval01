using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Visual Polish 5~6단계(재질/타일 + 물)를 한 번에 적용한다. 현재 에셋만 사용(외부 다운로드 없음).
//
// 5단계 — 타일 줄눈
//   Env_Court_Deck / Env_Court_PoolTile 에 tile_pool.png 를 입힌다. 큐브 프리미티브는 면마다 UV가
//   0~1이라 크기가 다른 상자에 같은 타일링을 쓰면 줄눈이 찌그러진다 → 씬의 해당 재질을 쓰는 큐브마다
//   "월드 미터 기준 UV" 메시를 만들어 교체한다(Assets/Meshes/Generated). 콜라이더·위치·크기는 그대로.
//   Pool Hall(Scene_SecondDream)의 Basin/Deck도 같은 재질이라 같이 처리한다.
//   녹 얼룩(컨테이너·문)은 Scene_Game 소속이라 이번 작업 범위에서 제외.
// 6단계 — 물
//   PoolReflectionProbe 를 수영장 전체(13.6x26)로 키우고 한 번 굽는다(매 프레임 갱신 아님).
//   PoolWater 셰이더에 반사 샘플 1개 추가(별도 파일 수정). 바닥에 가산 물그림자 쿼드 1장(Pool_Caustic).
//
// 모든 작업은 여러 번 실행해도 같은 결과(멱등). 실행: [DEPTH 5m > Visual Polish > Apply Stage 5 + 6]
public static class VisualPolishStage56
{
    const string IntroScenePath = "Assets/Scenes/Scene_Intro.unity";
    const string SecondDreamScenePath = "Assets/Scenes/Scene_SecondDream.unity";
    const string TilePath = "Assets/Textures/Env/tile_pool.png";
    const string CausticTexPath = "Assets/Textures/Env/water_caustic.png";
    const string CausticMatPath = "Assets/Materials/Env/Env_PoolCaustic.mat";
    const string ProbeTexPath = "Assets/Textures/Env/PoolReflectionProbe.exr";
    const string MeshDir = "Assets/Meshes/Generated";

    // tile_pool.png 한 장 = 2x2 타일. 한 번 반복되는 길이(미터).
    const float PoolRepeatMeters = 0.6f;  // 타일 한 칸 0.3m (수영장 안쪽)
    const float DeckRepeatMeters = 1.6f;  // 타일 한 칸 0.8m (데크)
    const float CausticRepeatMeters = 4f; // 물그림자 무늬 한 번 반복 길이
    const float CausticLift = 0.02f;      // 수영장 바닥 위로 띄우는 높이
    const float ProbeMargin = 1.2f;       // 수영장 개구부 바깥 여유 (벽 포함)

    [MenuItem("DEPTH 5m/Visual Polish/Apply Stage 5 + 6 (Tiles + Water)")]
    public static void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyMaterials();
        ApplyToScene(IntroScenePath, intro: true);
        ApplyToScene(SecondDreamScenePath, intro: false);
        AssetDatabase.SaveAssets();
        Debug.Log("[VisualPolish] 5~6단계 적용 완료.");
    }

    /// <summary>배치 실행(Unity.exe -executeMethod)용.</summary>
    public static void ApplyAllBatch()
    {
        try { ApplyAll(); }
        catch (System.Exception e) { Debug.LogError("[VisualPolish] 5~6단계 실패: " + e); EditorApplication.Exit(1); }
    }

    // ---- 재질 -------------------------------------------------------------------
    static void ApplyMaterials()
    {
        var tile = AssetDatabase.LoadAssetAtPath<Texture2D>(TilePath);
        if (tile == null) { Debug.LogError("[VisualPolish] tile_pool.png 없음"); return; }

        SetTile("Assets/Materials/Env/Env_Court_Deck.mat", tile);
        SetTile("Assets/Materials/Env/Env_Court_PoolTile.mat", tile);

        // 거칠기 차이: 무광 콘크리트(데크/벽) < 도색 금속 < 유약 타일.
        SetSmoothness("Assets/Materials/Env/Env_Court_PoolTile.mat", 0.65f);
        SetSmoothness("Assets/Materials/Env/Env_Court_Metal.mat", 0.60f);
        SetSmoothness("Assets/Materials/Env/Env_Court_Deck.mat", 0.10f);

        // 물그림자 재질
        var shader = Shader.Find("Custom/PoolCaustic");
        var caustic = AssetDatabase.LoadAssetAtPath<Texture2D>(CausticTexPath);
        if (shader == null || caustic == null) { Debug.LogError("[VisualPolish] PoolCaustic 셰이더/텍스처 없음"); return; }
        var m = AssetDatabase.LoadAssetAtPath<Material>(CausticMatPath);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, CausticMatPath); }
        m.shader = shader;
        m.SetTexture("_BaseMap", caustic);
        EditorUtility.SetDirty(m);
    }

    static void SetTile(string path, Texture2D tex)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { Debug.LogWarning("[VisualPolish] 재질 없음: " + path); return; }
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", Vector2.one); // UV가 이미 미터 기준이라 타일링은 1
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(m);
    }

    static void SetSmoothness(string path, float v)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) return;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", v);
        EditorUtility.SetDirty(m);
    }

    // ---- 씬 ---------------------------------------------------------------------
    static void ApplyToScene(string scenePath, bool intro)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mat = r.sharedMaterial;
                var mf = r.GetComponent<MeshFilter>();
                if (mat == null || mf == null || mf.sharedMesh == null) continue;
                float repeat;
                if (mat.name == "Env_Court_PoolTile") repeat = PoolRepeatMeters;
                else if (mat.name == "Env_Court_Deck") repeat = DeckRepeatMeters;
                else continue;
                // 큐브 프리미티브(원본 메시 이름 "Cube")나 이미 만든 생성 메시만 대상.
                if (mf.sharedMesh.name != "Cube" && !mf.sharedMesh.name.StartsWith("WorldUV_")) continue;
                ApplyWorldUV(r, mf, repeat, scene.name);
                n++;
            }
        Debug.Log($"[VisualPolish] {scene.name}: 월드 UV 메시 {n}개 적용.");

        if (intro)
        {
            BuildCausticQuad(scene);
            ResizeAndBakeProbe(scene);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void ApplyWorldUV(MeshRenderer r, MeshFilter mf, float repeatMeters, string sceneName)
    {
        // 원본 큐브 메시는 이름이 "Cube", 이미 교체된 건 "WorldUV_..." — 원본에서 다시 복제해 UV만 계산.
        Mesh src = mf.sharedMesh.name == "Cube"
            ? mf.sharedMesh
            : Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        var mesh = Object.Instantiate(src);
        mesh.name = $"WorldUV_{sceneName}_{r.name}";

        var verts = mesh.vertices;
        var normals = mesh.normals;
        var uv = new Vector2[verts.Length];
        Vector3 s = r.transform.lossyScale;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i], n = normals[i];
            Vector2 p;
            if (Mathf.Abs(n.y) > 0.5f) p = new Vector2(v.x * s.x, v.z * s.z);       // 윗면/아랫면
            else if (Mathf.Abs(n.x) > 0.5f) p = new Vector2(v.z * s.z, v.y * s.y);  // 동/서 면
            else p = new Vector2(v.x * s.x, v.y * s.y);                              // 남/북 면
            uv[i] = p / repeatMeters;
        }
        mesh.uv = uv;

        Directory.CreateDirectory(MeshDir);
        string path = $"{MeshDir}/{mesh.name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    // 수영장 바닥 위의 가산 물그림자 쿼드 1장.
    static void BuildCausticQuad(Scene scene)
    {
        Transform env = null;
        foreach (var root in scene.GetRootGameObjects()) if (root.name == "Environment") env = root.transform;
        var floor = env != null ? env.Find("Pool_Floor") : null;
        if (floor == null) { Debug.LogWarning("[VisualPolish] Pool_Floor 없음 — 물그림자 생략"); return; }

        var old = env.Find("Pool_Caustic");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Pool_Floor는 개구부보다 0.8m 크다 → 개구부 크기 = scale - 0.8
        float w = floor.localScale.x - 0.8f, l = floor.localScale.z - 0.8f;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Pool_Caustic";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(env, false);
        go.transform.localPosition = new Vector3(floor.localPosition.x, floor.localPosition.y + floor.localScale.y / 2f + CausticLift, floor.localPosition.z);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(w, l, 1f);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(CausticMatPath);
        mat.SetTextureScale("_BaseMap", new Vector2(w / CausticRepeatMeters, l / CausticRepeatMeters));
        EditorUtility.SetDirty(mat);
        var rend = go.GetComponent<MeshRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        go.AddComponent<WaterScroll>().scrollSpeed = new Vector2(0.01f, -0.015f);
    }

    static void ResizeAndBakeProbe(Scene scene)
    {
        ReflectionProbe probe = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var p in root.GetComponentsInChildren<ReflectionProbe>(true))
                if (p.name == "PoolReflectionProbe") probe = p;
        if (probe == null) { Debug.LogWarning("[VisualPolish] PoolReflectionProbe 없음"); return; }

        Transform env = probe.transform.parent;
        var floor = env != null ? env.Find("Pool_Floor") : null;
        float w = floor != null ? floor.localScale.x - 0.8f : 13.6f;
        float l = floor != null ? floor.localScale.z - 0.8f : 26f;

        probe.size = new Vector3(w + ProbeMargin, 5f, l + ProbeMargin);
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake; // 매 프레임 갱신 아님
        probe.boxProjection = false;
        EditorUtility.SetDirty(probe);

        // 한 번만 굽는다.
        if (Lightmapping.BakeReflectionProbe(probe, ProbeTexPath))
        {
            AssetDatabase.ImportAsset(ProbeTexPath);
            probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(ProbeTexPath);
            Debug.Log($"[VisualPolish] PoolReflectionProbe 크기 {probe.size} 로 베이크 완료.");
        }
        else
        {
            Debug.LogWarning("[VisualPolish] 프로브 베이크 실패 — 크기만 조정됨. Inspector에서 Bake 버튼으로 수동 베이크.");
        }
    }
}
