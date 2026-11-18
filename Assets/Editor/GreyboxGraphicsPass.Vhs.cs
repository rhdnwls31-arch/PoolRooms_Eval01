using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 화면 전체 VHS·캠코더 느낌(2026-11-18 무드 보드 design/references/weirdcore-mood-2026-11-18/).
// 색 번짐, 거친 입자, 살짝 휜 렌즈, 번지는 하이라이트, 바랜 색. HDRP 기본 후처리만 쓴다.
// 그레이박스 전용 전역 프로파일(HDRP_GB_Global_Profile)에만 넣는다. 방별 프로파일은 이 항목들을 덮지 않으므로 모든 방에 같이 적용된다.
public static partial class GreyboxGraphicsPass
{
    // 조절값. 너무 세면 멀미가 나니 색 번짐과 렌즈 휨은 낮게 둔다.
    const float VhsChromatic = 0.45f;     // 화면 가장자리 색 번짐(0~1)
    const float VhsGrain = 0.32f;         // 입자 세기(0~1)
    const float VhsGrainResponse = 0.6f;  // 밝은 곳에서도 입자가 보이는 정도(0이면 어두운 곳에만)
    const float VhsVignette = 0.3f;       // 가장자리 어둡게
    const float VhsLensDistortion = 0.12f;// 양수 = 볼록 렌즈(배럴)
    const float VhsBloom = 0.35f;         // 하이라이트 번짐
    const float VhsSaturation = -12f;     // 바랜 색(-100~100)
    const float VhsContrast = -8f;

    static void ApplyVhsLook()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HDRP_GB_Global_Profile.asset");
        if (profile == null) throw new System.Exception("HDRP_GB_Global_Profile 없음 (ApplyWaterAndSky가 먼저 만들어야 한다)");

        var ca = Get<ChromaticAberration>(profile);
        ca.intensity.Override(VhsChromatic);

        var grain = Get<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium2);
        grain.intensity.Override(VhsGrain);
        grain.response.Override(VhsGrainResponse);

        var vig = Get<Vignette>(profile);
        vig.intensity.Override(VhsVignette);
        vig.smoothness.Override(0.45f);

        var lens = Get<LensDistortion>(profile);
        lens.intensity.Override(VhsLensDistortion);
        lens.scale.Override(1.03f); // 휘면서 가장자리가 잘리지 않게 살짝 확대

        var bloom = Get<Bloom>(profile);
        bloom.intensity.Override(VhsBloom);
        bloom.scatter.Override(0.75f);
        bloom.threshold.Override(0.9f);

        var color = Get<ColorAdjustments>(profile);
        color.saturation.Override(VhsSaturation);
        color.contrast.Override(VhsContrast);

        foreach (var c in profile.components) EditorUtility.SetDirty(c);
        EditorUtility.SetDirty(profile);
    }

    /// <summary>프로파일에 있으면 그것을, 없으면 새로 만들어 에셋에 저장한 것을 돌려준다. 켜 둔다.</summary>
    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet<T>(out var c)) c = Persist(p, p.Add<T>(true));
        c.active = true;
        return c;
    }
}
