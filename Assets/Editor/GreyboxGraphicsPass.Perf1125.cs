using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-25 성능 단계(목표 120fps, 1080p, RTX 3060 Ti). 빌드 측정에서 그래픽 카드가 프레임당 12~20ms로 병목(CPU 6~7ms).
// 1) DLSS: 낮은 해상도로 그리고 NVIDIA DLSS로 1080p까지 올린다. 지원 안 되는 그래픽 카드에서는 HDRP가 그냥 원래 해상도로 그린다.
public static partial class GreyboxGraphicsPass
{
    /// <summary>DLSS 품질(0 = 최대 성능, 1 = 균형, 2 = 최대 품질, 3 = 울트라 성능, 4 = DLAA).</summary>
    const uint DlssQuality = 2;

    static void ApplyPerformance()
    {
        EnableDlss();
        LimitTvCamera();
        AddRoomCulling();
    }

    /// <summary>
    /// 2) 거실 TV 화면용 카메라는 거실 근처에서만 그린다(빌드 비교: 어느 방에서나 그래픽 카드 1.9~2.3ms 절약).
    /// </summary>
    static void LimitTvCamera()
    {
        var tvCam = GameObject.Find("TV_Camera");
        var tv = GameObject.Find("Oddity_TV");
        if (tvCam == null || tv == null || !tv.TryGetComponent<GazeTarget>(out var g)) return;
        if (!tvCam.TryGetComponent<CameraOnlyNear>(out var near)) near = tvCam.AddComponent<CameraOnlyNear>();
        near.area = g.roomBounds;
        near.margin = 4f;
        EditorUtility.SetDirty(near);
    }

    /// <summary>
    /// 3) 30m보다 먼 방은 그리지 않는다(빌드 비교: 다른 방을 다 끄면 1.5~4.2ms 절약).
    /// </summary>
    static void AddRoomCulling()
    {
        var go = GameObject.Find("RoomCulling") ?? new GameObject("RoomCulling");
        if (!go.TryGetComponent<RoomCulling>(out var rc)) rc = go.AddComponent<RoomCulling>();
        rc.cullDistance = RoomCullDistance;
        EditorUtility.SetDirty(rc);
    }

    const float RoomCullDistance = 30f;

    static void EnableDlss()
    {
        var asset = GraphicsSettings.defaultRenderPipeline as HDRenderPipelineAsset;
        if (asset == null) return;
        var so = new SerializedObject(asset);
        const string root = "m_RenderPipelineSettings.dynamicResolutionSettings.";
        so.FindProperty(root + "enabled").boolValue = true;
        var names = so.FindProperty(root + "advancedUpscalerNames");
        names.ClearArray();
        names.InsertArrayElementAtIndex(0);
        names.GetArrayElementAtIndex(0).stringValue = "DLSS";
        so.FindProperty(root + "DLSSPerfQualitySetting").uintValue = DlssQuality;
        so.FindProperty(root + "DLSSUseOptimalSettings").boolValue = true;
        // DLSS가 최적 배율(최대 품질 ≈ 67%)을 고른다. 최소를 100으로 두면 배율이 1에 묶여 DLSS가 낮춰 그리지 못했다(2026-11-25 측정 scale 1.000).
        // DLSS를 못 쓰는 그래픽 카드에서는 기본 배율이 최대값(100%)이라 원래 해상도 그대로다.
        so.FindProperty(root + "minPercentage").floatValue = 50f;
        so.FindProperty(root + "maxPercentage").floatValue = 100f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);

        // 카메라는 기본 끔(2026-11-25 사용자 결정: 1080p에서 이득이 없어 끄고, 설정 메뉴 "DLSS"에서 켠다 → GameSettings.ApplyDlss).
        // 플레이어 눈 카메라만(TV·도어뷰어처럼 텍스처에 그리는 작은 카메라는 그대로)
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (!cam.CompareTag("MainCamera")) continue;
            var hd = cam.GetComponent<HDAdditionalCameraData>();
            if (hd == null) continue;
            hd.allowDynamicResolution = false;
            hd.allowDeepLearningSuperSampling = false;
            hd.deepLearningSuperSamplingUseCustomQualitySettings = false;
            hd.deepLearningSuperSamplingUseCustomAttributes = false;
            EditorUtility.SetDirty(hd);
        }
    }
}
