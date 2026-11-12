using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// URP로 만든 나머지 씬(복도, Pool Hall, 메인 메뉴)을 HDRP로 옮긴다. 멱등: 여러 번 실행해도 값이 누적되지 않는다.
// 실행: 메뉴 [DEPTH 5m > HDRP Trial > Port Other Scenes]
//   또는 Unity.exe -batchmode -projectPath . -executeMethod HdrpScenePort.RunBatch -quit
public static class HdrpScenePort
{
    struct Look
    {
        public string scene, profilePath;
        public float exposure;            // 고정 노출(EV100)
        public Color fogAlbedo; public float fogDistance;
        public float bloom, vignette, grain, chroma;
        public float sunLux; public Color sunColor;
        public float lumenPerIntensity;   // URP 점광 intensity → 루멘 환산
    }

    static readonly Look[] Looks =
    {
        // 복도: 실내 형광등, 어둡고 서늘하게(이상현상 모드)
        new Look { scene = "Scene_Game", profilePath = "Assets/Settings/HDRP_Corridor_Profile.asset", exposure = 8.9f,
                   fogAlbedo = new Color(0.75f, 0.85f, 0.8f), fogDistance = 60f, bloom = 0.25f, vignette = 0.3f, grain = 0.12f, chroma = 0.1f,
                   sunLux = 0f, sunColor = Color.white, lumenPerIntensity = 3000f },
        // Pool Hall: 차가운 회청색, 끝이 안 보이는 큰 홀
        new Look { scene = "Scene_SecondDream", profilePath = "Assets/Settings/HDRP_PoolHall_Profile.asset", exposure = 9.7f,
                   fogAlbedo = new Color(0.72f, 0.82f, 0.95f), fogDistance = 120f, bloom = 0.3f, vignette = 0.28f, grain = 0.12f, chroma = 0.08f,
                   sunLux = 14000f, sunColor = new Color(0.85f, 0.9f, 1f), lumenPerIntensity = 5000f },
    };

    [MenuItem("DEPTH 5m/HDRP Trial/Port Other Scenes")]
    public static void RunAll()
    {
        foreach (var l in Looks) PortGameScene(l);
        PortMainMenu();
        HdrpTrial.ApplyToIntro(); // 인트로 프로파일에도 모션 블러 추가분 반영
        Debug.Log("[HdrpScenePort] 완료");
    }

    public static void RunBatch()
    {
        try { RunAll(); }
        catch (System.Exception e) { Debug.LogError("[HdrpScenePort] 실패: " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static T Persist<T>(VolumeProfile p, T c) where T : VolumeComponent
    {
        c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }

    static VolumeProfile BuildProfile(Look l)
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(l.profilePath) != null) AssetDatabase.DeleteAsset(l.profilePath);
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, l.profilePath);

        var env = Persist(p, p.Add<VisualEnvironment>(true));
        env.skyType.Override((int)SkyType.PhysicallyBased);
        env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
        Persist(p, p.Add<PhysicallyBasedSky>(true));

        var exp = Persist(p, p.Add<Exposure>(true));
        exp.mode.Override(ExposureMode.Fixed); exp.fixedExposure.Override(l.exposure);

        Persist(p, p.Add<Tonemapping>(true)).mode.Override(TonemappingMode.ACES);

        var bloom = Persist(p, p.Add<Bloom>(true));
        bloom.intensity.Override(l.bloom); bloom.threshold.Override(0.9f); bloom.scatter.Override(0.7f);

        Persist(p, p.Add<ScreenSpaceAmbientOcclusion>(true)).intensity.Override(0.9f);
        Persist(p, p.Add<ScreenSpaceReflection>(true)).enabled.Override(true);

        var fog = Persist(p, p.Add<Fog>(true));
        fog.enabled.Override(true); fog.meanFreePath.Override(l.fogDistance);
        fog.albedo.Override(l.fogAlbedo); fog.enableVolumetricFog.Override(true);

        Persist(p, p.Add<WaterRendering>(true)).enable.Override(true);

        var mb = Persist(p, p.Add<MotionBlur>(true)); mb.intensity.Override(0.25f);

        // GameManager/ScreenNoiseController/DreamTransitionController가 런타임에 값을 바꾸는 항목들
        var vig = Persist(p, p.Add<Vignette>(true));
        vig.intensity.Override(l.vignette); vig.smoothness.Override(0.9f); vig.color.Override(Color.black);
        var grain = Persist(p, p.Add<FilmGrain>(true));
        grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(l.grain);
        Persist(p, p.Add<ChromaticAberration>(true)).intensity.Override(l.chroma);

        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(l.profilePath, ImportAssetOptions.ForceUpdate);
        return p;
    }

    static void PortGameScene(Look l)
    {
        var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{l.scene}.unity", OpenSceneMode.Single);
        var profile = BuildProfile(l);

        // 기존 PostFX Volume(GameManager.postFxVolume이 참조)에 새 프로파일을 꽂는다 — 참조를 유지해야 런타임 연출이 계속 동작한다.
        Volume main = null;
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (main == null) { main = v; continue; }
            v.enabled = false;
        }
        if (main == null) { var g = new GameObject("PostFX Volume"); SceneManager.MoveGameObjectToScene(g, scene); main = g.AddComponent<Volume>(); }
        main.isGlobal = true; main.priority = 100; main.sharedProfile = profile; main.enabled = true;
        EditorUtility.SetDirty(main);

        foreach (var li in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (li.GetComponent<HDAdditionalLightData>() != null) continue; // 이미 변환됨(값 누적 방지)
            var hd = li.gameObject.AddComponent<HDAdditionalLightData>();
            if (li.type == LightType.Directional) { hd.SetIntensity(li.intensity > 0.01f ? l.sunLux : 0f, LightUnit.Lux); li.color = l.sunColor; }
            else hd.SetIntensity(li.intensity * l.lumenPerIntensity, LightUnit.Lumen);
        }

        EnsureHdCameras();
        ConvertWater(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HdrpScenePort] " + l.scene);
    }

    static void EnsureHdCameras()
    {
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var hd = cam.GetComponent<HDAdditionalCameraData>() ?? cam.gameObject.AddComponent<HDAdditionalCameraData>();
            hd.volumeLayerMask = ~0;
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            EditorUtility.SetDirty(cam);
        }
    }

    // 이름이 "Water"로 끝나는 커스텀 셰이더 물 메시 → HDRP Water Surface(Pool). 이미 바꿨으면(렌더러 꺼짐) 건너뛴다.
    static void ConvertWater(Scene scene)
    {
        var targets = new List<Renderer>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (r.name.EndsWith("Water") && r.enabled) targets.Add(r);
        foreach (var r in targets)
        {
            var b = r.bounds;
            r.enabled = false;
            foreach (var c in r.GetComponents<Collider>()) c.enabled = false;
            var go = new GameObject(r.name + "_HDRP");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            var ws = go.AddComponent<WaterSurface>();
            ws.surfaceType = WaterSurfaceType.Pool; ws.geometryType = WaterGeometryType.Quad;
            go.transform.localScale = new Vector3(b.size.x, 1f, b.size.z);
            ws.refractionColor = new Color(0.55f, 0.92f, 0.98f); ws.scatteringColor = new Color(0.25f, 0.72f, 0.78f);
            ws.absorptionDistance = 8f; ws.maxRefractionDistance = 1.5f;
            ws.ripplesWindSpeed = 2.5f; ws.ripplesChaos = 0.6f; ws.causticsIntensity = 1f;
            Debug.Log($"[HdrpScenePort] 물 {r.name} center={b.center} size={b.size}");
        }
    }

    static void PortMainMenu()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_MainMenu.unity", OpenSceneMode.Single);
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var hd = cam.GetComponent<HDAdditionalCameraData>() ?? cam.gameObject.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = Color.black;
            cam.backgroundColor = Color.black;
            EditorUtility.SetDirty(cam);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HdrpScenePort] Scene_MainMenu");
    }
}
