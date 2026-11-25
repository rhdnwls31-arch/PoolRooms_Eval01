using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 방별 조명·재질·후처리 적용. 멱등: 방마다 GFX_Generated / GFX_Props 를 지우고 다시 만든다.
// 벽·바닥의 트랜스폼과 콜라이더는 건드리지 않고 재질과 렌더링 레이어만 바꾼다.
// 실행: 메뉴 [DEPTH 5m > Greybox Graphics > Apply]  또는  Unity.exe -batchmode -projectPath . -executeMethod GreyboxGraphicsPass.ApplyBatch
public static partial class GreyboxGraphicsPass
{
    const string MatDir = "Assets/Materials/Greybox";
    const string GenName = "GFX_Generated";   // 구조물 쪽: 조명, Volume, 덧판
    const string PropsName = "GFX_Props";     // Placeholder_Props 아래: 배경 소품
    const int IgnoreRaycastLayer = 2;

    // 렌더링 레이어: 방마다 하나. 그림자 없는 조명이 벽 너머 옆방 바닥·천장을 밝히지 않게 하고, 태양이 실내에 닿지 않게 한다.
    const uint LOutdoor = 1u << 0, LCrack = 1u << 1, LYellow = 1u << 2, LAngel = 1u << 3;

    static Scene scene;

    [MenuItem("DEPTH 5m/Greybox Graphics/Apply")]
    public static void Apply()
    {
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(MatDir);
        SetupSun();
        ApplyHall();
        ApplyCrack();
        ApplyYellow();
        ApplyAngel();
        ApplyPropsAndDecals();
        ApplyWaterAndSky();
        ApplyVhsLook();
        ApplyCrtScreen();
        ApplyPlaytestFixes();
        SwapPoolParasols();
        SwapPoolPalms(); // 2026-11-23 VARCO 물속 야자수
        ApplyCrackArrowImage(); // 2026-11-23 VARCO 2D: 금 간 복도 화살표(씬에 손으로 놓은 판)
        FixTunnelSeams(); // 2026-11-23 인트로·같은 수영장 아치 터널이 벽과 겹쳐 깨져 보이던 것
        FixLivingBookcase(); // 2026-11-24 거실 VARCO 책장: 틀만 남기고 책은 새로
        SwapPoolDeckProps(); // 2026-11-24 VARCO 데크 벤치·화분 야자수·큰 그릇 나무
        RaiseIntroWalls(); TidyYellowPlaceholders(); AngelCloudFog(); ApplyLivingImages(); // 2026-11-24 플레이 피드백(GreyboxGraphicsPass.Feedback1124.cs)
        ApplyIntroLight(); // 2026-11-24 후처리: 인트로 전용 볼륨(SSGI·SSR·옅은 안개)과 해 높이
        GradeRooms(); // 2026-11-24 후처리 4단계: 거실·침실만 그림자·채도 보정
        ConvertFindReactions(); AddWaterBobs(); // 2026-11-24 2차 피드백: 찾기 반응 스르르·보는 앞에서 열림, 공·튜브 동동(Feedback1124b.cs)
        ApplyBuildFeedback1125(); // 2026-11-25 빌드 플레이: 수영장 홀 남쪽 벽 닫기, 거실 TV 찾는 거리(Feedback1125.cs)
        SwapDistantMannequin(); // 2026-11-24 수영장 홀 끝 마네킹 → VARCO 수영모 인외(모델이 오면)
        SwapPoolLoungers(); // 2026-11-20 VARCO 선베드: 인트로·같은 수영장 더미를 바꾼다(같은 수영장 복사본이 만들어진 뒤라 맨 끝)
        ApplyPerformance(); // 2026-11-25 성능 단계: DLSS 등(Perf1125.cs)
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GreyboxGraphicsPass] apply 완료");
    }

    public static void ApplyBatch()
    {
        try { Apply(); }
        catch (Exception e) { Debug.LogError("[GreyboxGraphicsPass] apply 실패: " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    // ---------------------------------------------------------------- 공통 도구

    static GameObject Root(string name)
    {
        foreach (var r in scene.GetRootGameObjects()) if (r.name == name) return r;
        throw new Exception("루트 오브젝트 없음: " + name);
    }

    /// <summary>parent 아래 같은 이름의 자식을 지우고 빈 오브젝트를 새로 만든다.</summary>
    static Transform Fresh(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name == name) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; // 자식은 월드 좌표로 배치한다
        return go.transform;
    }

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 center, Vector3 size, Material m, uint layer, bool collider = false, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = center; go.transform.localScale = size;
        if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = m; r.renderingLayerMask = layer;
        return go;
    }

    static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, uint layer, bool collider = false, Vector3? euler = null)
        => Prim(PrimitiveType.Cube, parent, name, center, size, m, layer, collider, euler);

    static Material Lit(string name, Action<Material> setup)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
        else m.shader = Shader.Find("HDRP/Lit");
        setup(m);
        HDShaderUtils.ResetMaterialKeywords(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>텍스처 세트를 월드 좌표 Triplanar로 입힌다. repeatMeters = 텍스처 한 장이 덮는 길이.</summary>
    static void Textured(Material m, (Texture2D albedo, Texture2D normal, Texture2D mask) t, float repeatMeters, Color tint, float normalScale = 1f, bool useNormal = true)
    {
        m.SetColor("_BaseColor", tint);
        m.SetTexture("_BaseColorMap", t.albedo);
        m.SetTexture("_NormalMap", useNormal ? t.normal : null); m.SetFloat("_NormalScale", normalScale);
        m.SetTexture("_MaskMap", t.mask);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SmoothnessRemapMin", 0f); m.SetFloat("_SmoothnessRemapMax", 1f);
        m.SetFloat("_UVBase", 5f); // Triplanar(월드): 상자 크기와 상관없이 타일 크기가 일정하다
        m.SetFloat("_TexWorldScale", 1f / repeatMeters);
    }

    static void Plain(Material m, Color c, float smoothness, float metallic = 0f)
    {
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
    }

    static void Emissive(Material m, Color c, float nits)
    {
        m.SetColor("_BaseColor", c);
        m.SetColor("_EmissiveColor", new Color(c.r, c.g, c.b, 1f) * nits);
        m.SetFloat("_UseEmissiveIntensity", 0f);
        m.SetFloat("_EmissiveExposureWeight", 1f);
    }

    static Light MakeLight(Transform parent, string name, LightType type, Vector3 pos, Vector3 euler, Color color, float lumen, float range, uint layers, bool shadows = false, bool volumetric = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(euler);
        var l = go.AddComponent<Light>();
        l.type = type;
        var hd = go.AddComponent<HDAdditionalLightData>();
        l.color = color; l.range = range;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        hd.SetIntensity(lumen, LightUnit.Lumen);
        hd.affectsVolumetric = volumetric;
        hd.lightlayersMask = (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)layers;
        return l;
    }

    static void SetLayers(GameObject root, uint mask)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = mask;
    }

    static void AddLayer(GameObject root, string childPrefix, uint mask)
    {
        foreach (Transform t in root.transform)
            if (t.name.StartsWith(childPrefix) && t.TryGetComponent<Renderer>(out var r)) r.renderingLayerMask |= mask;
    }

    /// <summary>방 루트 바로 아래 구조물(Floor, Wall_*, Ceiling)의 재질을 이름으로 바꾼다.</summary>
    static void SetStructureMaterials(Transform parent, Material floor, Material wall, Material ceiling)
    {
        foreach (Transform t in parent)
        {
            if (!t.TryGetComponent<MeshRenderer>(out var r)) continue;
            if (t.name.StartsWith("Floor")) r.sharedMaterial = floor;
            else if (t.name.StartsWith("Wall")) r.sharedMaterial = wall;
            else if (t.name.StartsWith("Ceiling")) r.sharedMaterial = ceiling;
        }
    }

    static void DisableOldLights(GameObject root)
    {
        foreach (Transform t in root.transform)
            if (t.name == "GB_Light" && t.TryGetComponent<Light>(out var l)) l.enabled = false; // 지우지 않고 끈다
    }

    static T Persist<T>(VolumeProfile p, T c) where T : VolumeComponent
    {
        c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }

    static VolumeProfile Profile(string name, Action<VolumeProfile> build)
    {
        string path = $"Assets/Settings/HDRP_GB_{name}_Profile.asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
        else
        {
            foreach (var c in p.components) if (c != null) Object.DestroyImmediate(c, true); // GUID를 유지하려고 에셋은 그대로 두고 내용만 비운다
            p.components.Clear();
        }
        build(p);
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
    }

    /// <summary>실내용 프로파일: 고정 노출, 하늘 앰비언트 차단, 방 색 안개, 화이트밸런스. 그 밖의 항목은 전역 Volume 값을 그대로 쓴다.</summary>
    static VolumeProfile RoomProfile(string name, float ev, float skyDiffuse, float skyReflection, Color fogAlbedo, float fogDistance, Color fogColor, float temperature, float tint, float anisotropy = 0f, float probeIntensity = 1f)
    {
        return Profile(name, p =>
        {
            var exp = Persist(p, p.Add<Exposure>(true));
            exp.mode.Override(ExposureMode.Fixed); exp.fixedExposure.Override(ev);
            var ilc = Persist(p, p.Add<IndirectLightingController>(true));
            ilc.indirectDiffuseLightingMultiplier.Override(skyDiffuse);
            ilc.reflectionLightingMultiplier.Override(skyReflection);
            ilc.reflectionProbeIntensityMultiplier.Override(probeIntensity);
            var fog = Persist(p, p.Add<Fog>(true));
            fog.enabled.Override(true); fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(fogDistance); fog.albedo.Override(fogAlbedo);
            fog.colorMode.Override(FogColorMode.ConstantColor); fog.color.Override(fogColor);
            fog.globalLightProbeDimmer.Override(skyDiffuse); // 안개가 하늘빛(분홍)을 머금지 않게
            fog.anisotropy.Override(anisotropy);
            var wb = Persist(p, p.Add<WhiteBalance>(true));
            wb.temperature.Override(temperature); wb.tint.Override(tint);
        });
    }

    static void LocalVolume(Transform parent, string name, Vector3 center, Vector3 size, float blend, float priority, VolumeProfile profile)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = center;
        go.layer = IgnoreRaycastLayer; // 방 크기 트리거가 상호작용·시선 레이캐스트를 가로채지 않게
        var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        var v = go.AddComponent<Volume>();
        v.isGlobal = false; v.blendDistance = blend; v.priority = priority; v.sharedProfile = profile; // 전역 Volume(100)보다 높아야 적용된다
    }

    /// <summary>옆방 벽의 뒷면을 가리는 덧판(콜라이더 없음). 문 자리는 비운다. alongX: 벽이 X축으로 뻗는지. fixedPos: 덧판 면의 고정 좌표.</summary>
    static void Cover(Transform parent, string name, bool alongX, float fixedPos, float a, float b, float height, float doorA, float doorB, float doorH, Material m, uint layer)
    {
        const float th = 0.04f;
        void Piece(string n, float s0, float s1, float y0, float y1)
        {
            if (s1 - s0 < 0.01f || y1 - y0 < 0.01f) return;
            var c = alongX ? new Vector3((s0 + s1) / 2, (y0 + y1) / 2, fixedPos) : new Vector3(fixedPos, (y0 + y1) / 2, (s0 + s1) / 2);
            var sz = alongX ? new Vector3(s1 - s0, y1 - y0, th) : new Vector3(th, y1 - y0, s1 - s0);
            Box(parent, name + n, c, sz, m, layer);
        }
        Piece("_A", a, doorA, 0, height); Piece("_B", doorB, b, 0, height); Piece("_Top", doorA, doorB, doorH, height);
    }

    static void SetupSun()
    {
        foreach (var l in Root("Lighting").GetComponentsInChildren<Light>(true))
            if (l.type == LightType.Directional)
            {
                var hd = l.GetComponent<HDAdditionalLightData>();
                hd.linkShadowLayers = false;                       // 그림자는 모든 메시가 드리우게 두고
                l.renderingLayerMask = -1;
                hd.lightlayersMask = (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)LOutdoor; // 빛만 실내 메시에 닿지 않게 한다
            }
    }

    // 형광등 한 개: 등기구 상자 + 발광판 + Rectangle Area Light. along = 길이 방향(월드 축).
    static Light Fluorescent(Transform parent, string name, Vector3 pos, bool alongZ, float length, float width, Color color, float lumen, float nits, Material housing, string emissiveMatName, uint layer)
    {
        var g = Group(parent, name);
        var size = alongZ ? new Vector3(width, 0.06f, length) : new Vector3(length, 0.06f, width);
        Box(g, "Housing", pos + Vector3.down * 0.03f, size + new Vector3(0.08f, 0f, 0.08f), housing, layer);
        var em = Lit(emissiveMatName, m => Emissive(m, color, nits));
        Box(g, "Tube", pos + Vector3.down * 0.065f, new Vector3(size.x, 0.02f, size.z), em, layer);
        var l = MakeLight(g, "AreaLight", LightType.Rectangle, pos + Vector3.down * 0.09f, new Vector3(90, alongZ ? 90 : 0, 0), color, lumen, 14f, layer);
        l.areaSize = new Vector2(length, width);
        return l;
    }
}
