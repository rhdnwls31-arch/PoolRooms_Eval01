using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 화면 전체 TV(브라운관) 화면 효과(2026-11-20 사용자 선택). 화면 가운데로 시선을 모으고, 꿈을 낡은 TV로 다시 보는 느낌을 준다.
// 효과 본체는 Assets/Scripts/CrtScreen.cs + Assets/Shaders/Resources/CrtScreen.shader. 여기서는 전역 프로파일 값과 HDRP 등록만 한다.
public static partial class GreyboxGraphicsPass
{
    // 조절값. 오래 보면 눈이 피로하니 주사선·줄무늬는 약하게, 밀리는 줄은 가끔만.
    const float CrtIntensity = 1f;
    const float CrtCurvature = 0.08f;     // 유리 휨
    const float CrtCornerRadius = 0.06f;  // 모서리 둥글기(화면 높이 비율)
    const float CrtScanline = 0.3f;       // 주사선 진하기
    const float CrtScanlineCount = 360f;  // 화면 높이당 줄 수
    const float CrtPhosphor = 0.12f;      // RGB 세로 줄무늬
    const float CrtRollBand = 0.05f;      // 내려가는 밝은 띠
    const float CrtJitter = 0.5f;         // 가끔 밀리는 줄
    const float CrtEdgeDarken = 0.5f;     // 유리 가장자리 어두움

    static void ApplyCrtScreen()
    {
        RegisterAfterPostProcess(typeof(CrtScreen));

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HDRP_GB_Global_Profile.asset");
        if (profile == null) throw new Exception("HDRP_GB_Global_Profile 없음");
        var crt = Get<CrtScreen>(profile);
        crt.intensity.Override(CrtIntensity);
        crt.curvature.Override(CrtCurvature);
        crt.cornerRadius.Override(CrtCornerRadius);
        crt.scanline.Override(CrtScanline);
        crt.scanlineCount.Override(CrtScanlineCount);
        crt.phosphorMask.Override(CrtPhosphor);
        crt.rollBand.Override(CrtRollBand);
        crt.jitter.Override(CrtJitter);
        crt.edgeDarken.Override(CrtEdgeDarken);
        EditorUtility.SetDirty(crt); EditorUtility.SetDirty(profile);
    }

    /// <summary>
    /// HDRP Global Settings의 Custom Post Process Orders → After Post Process 목록에 넣는다.
    /// 설정 클래스(CustomPostProcessOrdersSettings)가 internal이라 리플렉션으로 꺼낸다. 이미 있으면 그대로 둔다.
    /// </summary>
    static void RegisterAfterPostProcess(Type componentType)
    {
        var ordersType = typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition.CustomPostProcessOrdersSettings");
        var getter = typeof(GraphicsSettings).GetMethod("GetRenderPipelineSettings", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(ordersType);
        var orders = getter.Invoke(null, null);
        var list = ordersType.GetProperty("afterPostProcessCustomPostProcesses").GetValue(orders);
        var lt = list.GetType();
        string name = componentType.AssemblyQualifiedName;
        if ((bool)lt.GetMethod("Contains", new[] { typeof(string) }).Invoke(list, new object[] { name })) return;
        lt.GetMethod("Add", new[] { typeof(string) }).Invoke(list, new object[] { name });
        var global = GraphicsSettings.GetSettingsForRenderPipeline<HDRenderPipeline>();
        if (global != null) { EditorUtility.SetDirty(global); AssetDatabase.SaveAssetIfDirty(global); }
        Debug.Log($"CrtScreen: After Post Process 목록에 등록 ({componentType.Name}), global={global}");
    }
}
