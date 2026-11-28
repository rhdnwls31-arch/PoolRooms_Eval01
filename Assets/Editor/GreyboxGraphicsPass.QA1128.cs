using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-28 사용자 QA(전날 플레이) 반영. 메뉴 DEPTH 5m > QA 1128 > Apply.
// 1) 거실: TV가 스르르 바뀐 뒤에도 출구가 12초를 기다려야 열렸다 → 디졸브(2초)가 끝나면 바로 열 수 있게.
// 2) 마지막 수영장: 벽 위로 가라앉은 도시 껍데기(높이 80m)가 네모 상자로 보였다 → 그 방 안에서는 다른 방들을 그리지 않는다(RoomCulling).
// 3) 하늘 계단(천사방 다음): 빛을 받는 하늘색 상자라 모서리·면이 보여 "파란 박스"였다 → 빛을 받지 않는 한 가지 색으로(모서리가 사라져 끝없는 하늘).
// 4) 분위기: 조금 더 어둡고 기이하게 — 노출 -0.6, 비네트·채도. (TV 베젤 두께는 GreyboxGraphicsPass.Crt.cs의 CrtBezel)
// 5) 물에 뜬 튜브·공에 콜라이더 + 몸으로 밀기(FloatPush).
public static partial class GreyboxGraphicsPass
{
    const string FinalPoolRoot = "GB_05_SameIntro";
    /// <summary>마지막 수영장 안에서 그리지 않을 방(위로 보이던 가라앉은 도시, 같은 모양의 첫 수영장과 그 다음 방들).</summary>
    static readonly string[] HiddenFromFinalPool = { "GB_06_SunkenCity", "Environment", "Dummies", "GB_00_IntroExit", "GB_01_PoolHall", "GB_03d_Computer", "GB_02c_Corridor" };

    const string SkyShellPath = "GB_04b_SkyStairs/GFX_Generated/Shell";
    const string SkyUnlitMatPath = "Assets/Materials/Greybox/GB_Stairs_SkyUnlit.mat";
    /// <summary>하늘 계단 벽·천장 색(선형, 노출 영향 없음). 예전 빛 받은 벽의 화면 평균 밝기에 맞췄다.</summary>
    static readonly Color SkyUnlitColor = new Color(0.40f, 0.40f, 0.62f);

    const float MoodExposureCompensation = -0.6f; // EV. 음수일수록 어둡다
    const float MoodVignette = 0.32f;             // 예전 0.2
    const float MoodSaturation = -22f;            // 예전 -12

    const float LivingNotifyAfterFade = 0.5f;     // 디졸브가 끝나고 이만큼 뒤 출구를 열 수 있다(초)

    /// <summary>밀리는 튜브·공 콜라이더 높이(m). 발판처럼 밟고 올라서지 못하게 키보다 높게.</summary>
    const float FloatColliderHeight = 1.8f;

    [MenuItem("DEPTH 5m/QA 1128/Apply")]
    public static void ApplyQa1128Menu()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyQa1128();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static void ApplyQa1128()
    {
        // 1) 거실 TV
        foreach (var g in Object.FindObjectsByType<GazeTarget>(FindObjectsInactive.Include))
            if (g.name == "Oddity_TV") { g.notifyDelay = g.fadeTime + LivingNotifyAfterFade; EditorUtility.SetDirty(g); }

        // 2) 마지막 수영장 안에서 다른 방 안 그리기
        var rc = Object.FindAnyObjectByType<RoomCulling>(FindObjectsInactive.Include);
        if (rc != null)
        {
            rc.exclusiveRooms = new[] { new RoomCulling.ExclusiveRoom { insideRoot = FinalPoolRoot, hideRoots = HiddenFromFinalPool } };
            EditorUtility.SetDirty(rc);
        }
        else Debug.LogWarning("[QA1128] RoomCulling 없음");

        // 3) 하늘 계단 껍데기
        var shell = GameObject.Find(SkyShellPath);
        if (shell != null)
        {
            var mat = SkyUnlitMaterial();
            foreach (var r in shell.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                EditorUtility.SetDirty(r);
            }
        }
        else Debug.LogWarning("[QA1128] 하늘 계단 껍데기 없음");

        // 4) 분위기
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HDRP_GB_Global_Profile.asset");
        if (profile != null)
        {
            Get<Exposure>(profile).compensation.Override(MoodExposureCompensation);
            Get<Vignette>(profile).intensity.Override(MoodVignette);
            Get<ColorAdjustments>(profile).saturation.Override(MoodSaturation);
            foreach (var c in profile.components) EditorUtility.SetDirty(c);
            EditorUtility.SetDirty(profile);
        }
        ApplyCrtScreen(); // 베젤 두께·모서리·가장자리 어두움(Crt.cs 상수)

        // 5) 튜브·공
        int n = 0;
        foreach (var bob in Object.FindObjectsByType<WaterBob>(FindObjectsInactive.Include))
            if (IsPushableFloat(bob.name) && AddFloatPush(bob.gameObject)) n++;
        Debug.Log($"[QA1128] 밀리는 튜브·공 {n}개");
    }

    /// <summary>물에 뜬 튜브(플라밍고)와 공(빛나는 공)인지. 공 둘레 빛(OrbGlowLight)은 뺀다.</summary>
    static bool IsPushableFloat(string name)
    {
        if (name.Contains("Light")) return false;
        return name.Contains("Flamingo") || name.Contains("GlowOrb") || name.Contains("_Orb_");
    }

    /// <summary>콜라이더(자식 PushCollider, 세운 캡슐)와 FloatPush를 붙인다. 렌더러가 없으면 거짓.</summary>
    static bool AddFloatPush(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);

        var t = go.transform.Find("PushCollider");
        if (t == null) { t = new GameObject("PushCollider").transform; t.SetParent(go.transform, false); }
        t.gameObject.layer = go.layer;
        // 부모의 크기·회전과 상관없이 월드 크기로 맞추기 위해 크기를 되돌린다(부모는 거의 세워져 있다).
        var ls = go.transform.lossyScale;
        t.localScale = new Vector3(1f / Mathf.Max(ls.x, 1e-4f), 1f / Mathf.Max(ls.y, 1e-4f), 1f / Mathf.Max(ls.z, 1e-4f));
        t.rotation = Quaternion.identity;
        t.position = new Vector3(b.center.x, b.min.y + FloatColliderHeight * 0.5f, b.center.z);

        var cap = t.GetComponent<CapsuleCollider>();
        if (cap == null) cap = t.gameObject.AddComponent<CapsuleCollider>();
        cap.direction = 1;
        cap.radius = Mathf.Max(0.15f, Mathf.Max(b.extents.x, b.extents.z));
        cap.height = Mathf.Max(FloatColliderHeight, cap.radius * 2f);
        cap.center = Vector3.zero;
        var rb = t.GetComponent<Rigidbody>();
        if (rb == null) rb = t.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;

        if (go.GetComponent<FloatPush>() == null) go.AddComponent<FloatPush>();
        EditorUtility.SetDirty(go);
        return true;
    }

    /// <summary>하늘 계단용 빛을 받지 않는 재질(없으면 만든다). 노출에 영향받지 않는 발광 색으로 칠한다.</summary>
    static Material SkyUnlitMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyUnlitMatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("HDRP/Unlit"));
            AssetDatabase.CreateAsset(mat, SkyUnlitMatPath);
        }
        mat.SetColor("_UnlitColor", Color.black);
        mat.SetColor("_EmissiveColor", SkyUnlitColor);
        mat.SetColor("_EmissiveColorLDR", SkyUnlitColor);
        mat.SetFloat("_EmissiveExposureWeight", 0f);
        mat.SetFloat("_EmissiveIntensity", 1f);
        mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
        ValidateHdMaterial(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
