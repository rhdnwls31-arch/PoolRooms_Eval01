using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// HDRP 시험 변환(브랜치 hdrp-test 전용): 재질 변환 + HDRP 에셋 설정 + 인트로 씬 조명/Volume 교체 + 캡처.
// 실행: Unity.exe -batchmode -projectPath . -executeMethod HdrpTrial.RunBatch -quit
public static class HdrpTrial
{
    const string ScenePath = "Assets/Scenes/Scene_Intro.unity";
    const string ProfilePath = "Assets/Settings/HDRP_Intro_Profile.asset";
    const string OutDir = "production/qa/evidence/hdrp-trial";
    const float EmissiveScale = 6000f;   // URP 발광 색 → HDRP 니트 환산(시각 조정)
    const float ExposureEV = 8.7f;

    [MenuItem("DEPTH 5m/HDRP Trial/Run")]
    public static void Run()
    {
        ConfigureAsset();
        ConvertMaterials();
        ApplyToIntro();
        Capture();
    }

    public static void RunBatch()
    {
        try { Run(); }
        catch (System.Exception e) { Debug.LogError("[HdrpTrial] 실패: " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    // ---- HDRP 에셋: SSR/SSGI/물/볼류메트릭 지원 켜기 -----------------------------------
    static void ConfigureAsset()
    {
        var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/HDRPDefaultResources/DefaultHDRPAsset.asset");
        var so = new SerializedObject(asset);
        foreach (var name in new[] { "supportSSR", "supportSSGI", "supportWater", "supportVolumetrics", "supportSSAO" })
        {
            var p = so.FindProperty("m_RenderPipelineSettings." + name);
            if (p != null) p.boolValue = true; else Debug.LogWarning("[HdrpTrial] 속성 없음: " + name);
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log("[HdrpTrial] HDRP 에셋 설정 완료");
    }

    // ---- URP/Lit → HDRP/Lit -------------------------------------------------------------
    static void ConvertMaterials()
    {
        var lit = Shader.Find("HDRP/Lit");
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null) continue;
            if (m.shader.name != "Universal Render Pipeline/Lit") continue;

            Color baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            Vector2 tile = m.HasProperty("_BaseMap") ? m.GetTextureScale("_BaseMap") : Vector2.one;
            Vector2 off = m.HasProperty("_BaseMap") ? m.GetTextureOffset("_BaseMap") : Vector2.zero;
            float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            float smooth = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.5f;
            Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            float bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            Color emis = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            bool transparent = m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f;
            bool cutout = m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            bool doubleSided = m.HasProperty("_Cull") && m.GetFloat("_Cull") < 0.5f;

            m.shader = lit;
            m.SetColor("_BaseColor", baseColor);
            if (baseMap != null) { m.SetTexture("_BaseColorMap", baseMap); m.SetTextureScale("_BaseColorMap", tile); m.SetTextureOffset("_BaseColorMap", off); }
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (bump != null) { m.SetTexture("_NormalMap", bump); m.SetFloat("_NormalScale", bumpScale); }
            if (emis.maxColorComponent > 0.001f)
            {
                m.SetColor("_EmissiveColor", new Color(emis.r, emis.g, emis.b, 1f) * EmissiveScale);
                m.SetFloat("_UseEmissiveIntensity", 0f);
            }
            if (transparent) { m.SetFloat("_SurfaceType", 1f); m.SetFloat("_BlendMode", 0f); m.SetFloat("_ZWrite", 0f); }
            if (cutout) { m.SetFloat("_AlphaCutoffEnable", 1f); m.SetFloat("_AlphaCutoff", cutoff); }
            if (doubleSided) m.SetFloat("_DoubleSidedEnable", 1f);
            HDShaderUtils.ResetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[HdrpTrial] 재질 변환: " + n + "개");
    }

    // ---- 인트로 씬: Volume/조명/카메라 교체 --------------------------------------------------
    // profile.Add로 만든 컴포넌트는 에셋의 하위 오브젝트로 저장해야 디스크에 남는다.
    // (안 하면 같은 세션에서만 보이고, 에디터를 다시 열어 Play하면 설정이 전부 비어 화면이 깨진다.)
    static T Persist<T>(VolumeProfile profile, T component) where T : VolumeComponent
    {
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    static VolumeProfile BuildProfile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (existing != null) AssetDatabase.DeleteAsset(ProfilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        var env = Persist(profile, profile.Add<VisualEnvironment>(true));
        env.skyType.Override((int)SkyType.PhysicallyBased);
        env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

        var pbs = Persist(profile, profile.Add<PhysicallyBasedSky>(true));
        pbs.groundTint.Override(new Color(0.62f, 0.54f, 0.5f));

        var exp = Persist(profile, profile.Add<Exposure>(true));
        exp.mode.Override(ExposureMode.Fixed);
        exp.fixedExposure.Override(ExposureEV);

        var tm = Persist(profile, profile.Add<Tonemapping>(true));
        tm.mode.Override(TonemappingMode.ACES);

        var mb = Persist(profile, profile.Add<MotionBlur>(true));
        mb.intensity.Override(0.25f); // 설정의 모션 블러 끔은 런타임 SettingsVolume이 0으로 덮는다

        var bloom = Persist(profile, profile.Add<Bloom>(true));
        bloom.intensity.Override(0.35f);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.7f);

        var ssao = Persist(profile, profile.Add<ScreenSpaceAmbientOcclusion>(true));
        ssao.intensity.Override(0.9f);

        var ssr = Persist(profile, profile.Add<ScreenSpaceReflection>(true));
        ssr.enabled.Override(true);

        var fog = Persist(profile, profile.Add<Fog>(true));
        fog.enabled.Override(true);
        fog.meanFreePath.Override(90f);
        fog.albedo.Override(new Color(1f, 0.74f, 0.84f));
        fog.enableVolumetricFog.Override(true);

        var wr = Persist(profile, profile.Add<WaterRendering>(true)); // 이게 없으면 Water Surface가 그려지지 않는다
        wr.enable.Override(true);

        var vig = Persist(profile, profile.Add<Vignette>(true));
        vig.intensity.Override(0.25f);

        var grain = Persist(profile, profile.Add<FilmGrain>(true));
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.15f);

        var ca = Persist(profile, profile.Add<ChromaticAberration>(true));
        ca.intensity.Override(0.12f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceUpdate);
        return profile;
    }

    internal static void ApplyToIntro()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var profile = BuildProfile();

        // 기존 Volume들: 모두 끄고 새 전역 Volume 하나만 사용
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)) v.enabled = false;
        var volGo = GameObject.Find("HDRP_GlobalVolume");
        if (volGo == null) { volGo = new GameObject("HDRP_GlobalVolume"); SceneManager.MoveGameObjectToScene(volGo, scene); }
        var vol = volGo.GetComponent<Volume>() ?? volGo.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 100; vol.sharedProfile = profile; vol.enabled = true;

        // 조명: 태양(Directional) lux, 점광(Lumen)
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var hd = l.GetComponent<HDAdditionalLightData>() ?? l.gameObject.AddComponent<HDAdditionalLightData>();
            if (l.type == LightType.Directional) { hd.SetIntensity(18000f, LightUnit.Lux); l.color = new Color(1f, 0.82f, 0.7f); }
            else hd.SetIntensity(PointLumens(l.name), LightUnit.Lumen); // 재실행해도 값이 누적되지 않게 이름 기준 고정값
        }

        // 시험 단계에서 아직 변환 안 된 것(커스텀 셰이더, URP 반사 프로브)은 잠시 끈다
        foreach (var name in new[] { "Pool_Caustic", "PoolReflectionProbe" })
        {
            var go = GameObject.Find(name);
            if (go != null) go.SetActive(false);
        }

        TuneLook();
        EnsureHdCameras();
        ApplyWater(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HdrpTrial] 인트로 씬 적용 완료");
    }

    // 씬의 실제 카메라(Main Camera 등)에 HDAdditionalCameraData가 없으면 Volume이 적용되지 않아
    // 캡처용 임시 카메라와 Game 화면이 다르게 나온다. 모든 카메라에 붙이고 Volume 마스크를 전체로 둔다.
    static void EnsureHdCameras()
    {
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.name.StartsWith("__")) continue;
            var hd = cam.GetComponent<HDAdditionalCameraData>() ?? cam.gameObject.AddComponent<HDAdditionalCameraData>();
            hd.volumeLayerMask = ~0;
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            EditorUtility.SetDirty(cam);
            Debug.Log("[HdrpTrial] HD 카메라 데이터: " + cam.name);
        }
    }

    // 레퍼런스(풀코어)의 분홍 파스텔 톤으로: 구체는 분홍빛 흰색, 안개와 햇빛은 분홍 쪽으로.
    static void TuneLook()
    {
        var orb = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Env/Env_GlowOrb.mat");
        if (orb != null)
        {
            orb.SetColor("_BaseColor", new Color(1f, 0.80f, 0.92f));
            orb.SetColor("_EmissiveColor", new Color(1f, 0.74f, 0.92f) * 2600f);
            HDShaderUtils.ResetMaterialKeywords(orb);
            EditorUtility.SetDirty(orb);
        }
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.color = new Color(1f, 0.80f, 0.78f);
    }

    static float PointLumens(string n)
    {
        if (n.StartsWith("Tunnel_Light")) return 12000f;
        if (n.StartsWith("ChannelHall_Light")) return 8800f;
        if (n == "Goal_OrbLight") return 14000f;
        if (n == "Goal_Fill") return 6400f;
        return 8000f;
    }

    static void RemoveWater()
    {
        foreach (var name in new[] { "Pool_Water_HDRP", "Channel_Water_HDRP", "Lagoon_Water_HDRP" })
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    // ---- 물: 커스텀 셰이더 메시 → HDRP Water Surface(Pool) -----------------------------------
    static void ApplyWater(Scene scene)
    {
        foreach (var name in new[] { "Pool_Water", "Channel_Water", "Lagoon_Water" })
        {
            var src = GameObject.Find(name);
            if (src == null) continue;
            var r = src.GetComponent<Renderer>();
            if (r == null) continue;
            var b = r.bounds;
            r.enabled = false;
            foreach (var c in src.GetComponents<Collider>()) c.enabled = false;

            var old = GameObject.Find(name + "_HDRP");
            if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject(name + "_HDRP");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            var ws = go.AddComponent<WaterSurface>();
            ws.surfaceType = WaterSurfaceType.Pool;
            ws.geometryType = WaterGeometryType.Quad;
            go.transform.localScale = new Vector3(b.size.x, 1f, b.size.z);
            // 수영장 느낌: 맑고 얕은 청록, 잔잔한 물결
            ws.refractionColor = new Color(0.55f, 0.92f, 0.98f);
            ws.scatteringColor = new Color(0.25f, 0.72f, 0.78f);
            ws.absorptionDistance = 8f;
            ws.maxRefractionDistance = 1.5f;
            ws.ripplesWindSpeed = 2.5f;
            ws.ripplesChaos = 0.6f;
            ws.causticsIntensity = 1.0f;
            Debug.Log($"[HdrpTrial] 물 {name}: center={b.center} size={b.size}");
        }
    }

    // ---- 캡처 ----------------------------------------------------------------------------------
    static void Capture()
    {
        Directory.CreateDirectory(OutDir);
        // 캡처는 게임이 실제로 쓰는 Main Camera로 찍는다(임시 카메라와 설정이 달라 결과가 달랐던 문제 방지).
        var cam = Camera.main;
        if (cam == null) throw new System.Exception("Main Camera 없음");
        var go = cam.gameObject;
        var savedPos = go.transform.position; var savedRot = go.transform.rotation;
        var savedTarget = cam.targetTexture;
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null) player.enabled = false;

        const int W = 1600, H = 900;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var shots = new (string, Vector3, Vector3)[]
        {
            ("01_spawn", new Vector3(0, 1.6f, 22f), new Vector3(0, 2.5f, -20f)),
            ("02_pool_stairs", new Vector3(0, 1.6f, 17f), new Vector3(0, -0.5f, 8f)),
            ("03_in_pool", new Vector3(0, 0.7f, 6f), new Vector3(0, 1.5f, -25f)),
            ("04_channel_hall", new Vector3(24f, 1.6f, 0f), new Vector3(46f, 2f, 0f)),
            ("05_tunnel", new Vector3(0, 1.6f, -32f), new Vector3(0, 1.6f, -48f)),
            ("06_goal_room", new Vector3(0, 1.6f, -50f), new Vector3(0, 1.5f, -60f)),
        };
        foreach (var (name, pos, look) in shots)
        {
            go.transform.position = pos; go.transform.LookAt(look);
            for (int i = 0; i < 4; i++) cam.Render(); // 노출/볼류메트릭 안정화
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"{OutDir}/{name}.png", tex.EncodeToPNG());
            Debug.Log("[HdrpTrial] " + name + ".png");
        }
        cam.targetTexture = savedTarget;
        go.transform.SetPositionAndRotation(savedPos, savedRot);
        if (player != null) player.enabled = true;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
