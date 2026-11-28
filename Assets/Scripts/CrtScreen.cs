using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 화면 전체를 오래된 브라운관 TV처럼 보이게 하는 HDRP 후처리 볼륨 컴포넌트(2026-11-20).
/// 휜 유리, 둥근 모서리 베젤, 주사선, 형광체 줄무늬, 내려가는 밝은 띠, 가끔 밀리는 줄.
/// 값은 전역 프로파일(HDRP_GB_Global_Profile)에서 GreyboxGraphicsPass.ApplyCrtScreen이 넣는다.
/// HDRP Global Settings의 Custom Post Process Orders(After Post Process)에 등록돼 있어야 그려진다.
/// </summary>
[Serializable, VolumeComponentMenu("Post-processing/Custom/CRT Screen")]
public sealed class CrtScreen : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    /// <summary>효과 전체 세기. 0이면 꺼진다.</summary>
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
    /// <summary>유리가 볼록하게 휜 정도.</summary>
    public ClampedFloatParameter curvature = new ClampedFloatParameter(0.08f, 0f, 0.4f);
    /// <summary>화면 모서리 둥글기(화면 높이 기준 비율).</summary>
    public ClampedFloatParameter cornerRadius = new ClampedFloatParameter(0.06f, 0f, 0.3f);
    /// <summary>주사선 진하기.</summary>
    public ClampedFloatParameter scanline = new ClampedFloatParameter(0.35f, 0f, 1f);
    /// <summary>화면 높이당 주사선 수.</summary>
    public ClampedFloatParameter scanlineCount = new ClampedFloatParameter(360f, 120f, 1080f);
    /// <summary>RGB 형광체 세로 줄무늬 진하기.</summary>
    public ClampedFloatParameter phosphorMask = new ClampedFloatParameter(0.15f, 0f, 1f);
    /// <summary>위에서 아래로 내려가는 밝은 띠 세기.</summary>
    public ClampedFloatParameter rollBand = new ClampedFloatParameter(0.05f, 0f, 0.5f);
    /// <summary>가끔 한 줄이 옆으로 밀리는 세기.</summary>
    public ClampedFloatParameter jitter = new ClampedFloatParameter(0.5f, 0f, 1f);
    /// <summary>유리 가장자리가 어두워지는 정도.</summary>
    public ClampedFloatParameter edgeDarken = new ClampedFloatParameter(0.5f, 0f, 1f);
    /// <summary>검은 베젤 두께(위아래는 화면 높이, 양옆은 화면 너비 기준 비율). 화면 비율은 그대로이고, 클수록 보이는 화면이 좁아진다(2026-11-28 QA: 시야를 좁게).</summary>
    public ClampedFloatParameter bezelWidth = new ClampedFloatParameter(0f, 0f, 0.2f);

    Material m_Material;
    const string kShaderName = "Hidden/Shader/CrtScreen";

    /// <summary>재질이 있고 세기가 0보다 클 때만 그린다.</summary>
    public bool IsActive() => m_Material != null && intensity.value > 0f;

    /// <summary>다른 후처리(색 번짐, 비네팅, 입자)가 다 끝난 뒤 TV 유리를 씌운다.</summary>
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    /// <summary>씬 뷰에서는 작업이 불편하니 그리지 않는다.</summary>
    public override bool visibleInSceneView => false;

    /// <summary>셰이더를 찾아 재질을 만든다.</summary>
    public override void Setup()
    {
        var shader = Shader.Find(kShaderName);
        if (shader != null) m_Material = new Material(shader);
        else Debug.LogError($"CrtScreen: 셰이더 '{kShaderName}'를 찾지 못했다(Assets/Shaders/Resources/CrtScreen.shader).");
    }

    /// <summary>원본 화면을 받아 TV 화면으로 그린다.</summary>
    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (m_Material == null) return;
        m_Material.SetFloat("_Intensity", intensity.value);
        m_Material.SetFloat("_Curvature", curvature.value);
        m_Material.SetFloat("_CornerRadius", cornerRadius.value);
        m_Material.SetFloat("_Scanline", scanline.value);
        m_Material.SetFloat("_ScanlineCount", scanlineCount.value);
        m_Material.SetFloat("_Mask", phosphorMask.value);
        m_Material.SetFloat("_RollBand", rollBand.value);
        m_Material.SetFloat("_Jitter", jitter.value);
        m_Material.SetFloat("_EdgeDarken", edgeDarken.value);
        m_Material.SetFloat("_Bezel", bezelWidth.value);
        m_Material.SetTexture("_MainTex", source);
        HDUtils.DrawFullScreen(cmd, m_Material, destination, shaderPassId: 0);
    }

    /// <summary>재질을 지운다.</summary>
    public override void Cleanup() => CoreUtils.Destroy(m_Material);
}
