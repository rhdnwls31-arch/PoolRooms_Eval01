using UnityEditor;

/// <summary>
/// VARCO 모델 텍스처(Assets/VARCO/방/Textures/…)는 최대 1024로 들인다.
/// 2048 그대로면 빌드 875MB 중 611MB가 이 텍스처였다(2026-11-25). 전체 화면에서는 1024와 거의 같았지만, 코앞에서 보는 모델(KeepFull)은 결이 조금 부드러워져 2048로 둔다.
/// 2D 이미지(Assets/VARCO/방/2D/, 러그·포스터·크로키)는 벽·바닥에 크게 깔려 그대로 둔다.
/// </summary>
public class VarcoTextureSize : AssetPostprocessor
{
    /// <summary>VARCO 모델 텍스처 최대 크기.</summary>
    public const int MaxSize = 1024;
    /// <summary>비교 캡처할 때만 잠시 끄는 스위치(EditorPrefs). 평소에는 꺼져 있다.</summary>
    public const string BypassKey = "VarcoTextureSize.Bypass";

    /// <summary>
    /// 코앞에서 보는 모델은 2048 그대로(2026-11-25 사용자 결정): 이상한 점으로 바라보는 침대·TV,
    /// 다가오는 목각인형 5자세, 피에로, 도어뷰어 너머 인외.
    /// </summary>
    public static readonly string[] KeepFull =
    {
        "VAR_Bedroom_Bed_BlanketFigure", "VAR_DollRoom_Doll_", "VAR_Yellow_Pierrot_Balloon",
        "VAR_LivingRoom_CRTTV", "VAR_BlueCorridor_PeepholeFigure",
    };

    /// <summary>이 경로가 크기를 줄일 VARCO 모델 텍스처인지(가까이 보는 모델은 제외).</summary>
    public static bool Applies(string path)
    {
        path = path.Replace('\\', '/');
        if (!path.StartsWith("Assets/VARCO/") || !path.Contains("/Textures/")) return false;
        foreach (var k in KeepFull) if (path.Contains("/" + k)) return false;
        return true;
    }

    void OnPreprocessTexture()
    {
        if (!Applies(assetPath) || EditorPrefs.GetBool(BypassKey, false)) return;
        var ti = (TextureImporter)assetImporter;
        if (ti.maxTextureSize > MaxSize) ti.maxTextureSize = MaxSize;
    }
}
