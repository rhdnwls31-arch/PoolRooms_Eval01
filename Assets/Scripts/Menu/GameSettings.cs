using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 사용자 설정(화면/프레임/음량/모션 블러/밝기/마우스 감도)의 저장과 적용.
/// 값은 PlayerPrefs에 저장하고, 바꾸는 즉시 <see cref="Apply"/>로 게임에 반영한다.
/// 밝기와 모션 블러는 씬마다 다른 Volume 프로파일을 건드리지 않고, 게임 시작 때 만든
/// 전역 "SettingsVolume"(우선순위 150, 씬이 바뀌어도 유지)의 오버라이드로 처리한다.
/// </summary>
public static class GameSettings
{
    // ---- 선택지 ----------------------------------------------------------
    public static readonly string[] ScreenModeLabels = { "전체 화면", "창 모드", "테두리 없음" };
    public static readonly string[] FrameRateLabels = { "30", "60", "120", "제한 없음" };
    static readonly int[] FrameRateValues = { 30, 60, 120, -1 };
    public static readonly string[] OnOffLabels = { "끔", "켬" };

    static readonly Vector2Int[] ResolutionCandidates =
    {
        new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
    };

    // ---- 기본값 ------------------------------------------------------------
    const float DefaultVolume = 0.8f;
    const float DefaultBrightness = 0.5f;   // 0.5 = 씬 기본 밝기 그대로
    const float DefaultSensitivity = 0.26f; // 0..1 슬라이더 → 0.3x~3x 중 정확히 1x
    const float BrightnessRangeEv = 1.0f;   // 슬라이더 양 끝에서 ±1 노출 단계
    const float SensMin = 0.3f, SensMax = 3f;

    static Volume settingsVolume;
    static ColorAdjustments colorAdjustments;
    static MotionBlur motionBlur;

    // ---- 값 -----------------------------------------------------------------
    public static int ScreenMode { get => PlayerPrefs.GetInt("set.screenMode", 0); set { PlayerPrefs.SetInt("set.screenMode", value); } }
    public static int ResolutionIndex { get => Mathf.Clamp(PlayerPrefs.GetInt("set.resolution", DefaultResolutionIndex()), 0, Resolutions().Count - 1); set { PlayerPrefs.SetInt("set.resolution", value); } }
    public static int FrameRateIndex { get => Mathf.Clamp(PlayerPrefs.GetInt("set.frameRate", 3), 0, FrameRateValues.Length - 1); set { PlayerPrefs.SetInt("set.frameRate", value); } }
    public static int MotionBlurOn { get => PlayerPrefs.GetInt("set.motionBlur", 1); set { PlayerPrefs.SetInt("set.motionBlur", value); } }
    public static float Volume01 { get => PlayerPrefs.GetFloat("set.volume", DefaultVolume); set { PlayerPrefs.SetFloat("set.volume", value); } }
    public static float Brightness01 { get => PlayerPrefs.GetFloat("set.brightness", DefaultBrightness); set { PlayerPrefs.SetFloat("set.brightness", value); } }
    public static float Sensitivity01 { get => PlayerPrefs.GetFloat("set.sensitivity", DefaultSensitivity); set { PlayerPrefs.SetFloat("set.sensitivity", value); } }

    /// <summary>PlayerController가 기본 감도에 곱하는 배율(0.3~3).</summary>
    public static float MouseSensitivityMultiplier => Mathf.Lerp(SensMin, SensMax, Sensitivity01);

    /// <summary>이 모니터가 지원하는 해상도 후보만 추린 목록.</summary>
    public static List<Vector2Int> Resolutions()
    {
        var list = new List<Vector2Int>();
        int maxW = Mathf.Max(Screen.currentResolution.width, 1280), maxH = Mathf.Max(Screen.currentResolution.height, 720);
        foreach (var r in ResolutionCandidates) if (r.x <= maxW && r.y <= maxH) list.Add(r);
        if (list.Count == 0) list.Add(new Vector2Int(1920, 1080));
        return list;
    }

    public static string[] ResolutionLabels()
    {
        var rs = Resolutions();
        var labels = new string[rs.Count];
        for (int i = 0; i < rs.Count; i++) labels[i] = rs[i].x + " x " + rs[i].y;
        return labels;
    }

    static int DefaultResolutionIndex()
    {
        var rs = Resolutions();
        int best = 0;
        for (int i = 0; i < rs.Count; i++) if (rs[i].x <= 1920) best = i; // 기본은 최대 1920x1080
        return best;
    }

    // ---- 적용 ---------------------------------------------------------------
    /// <summary>저장된 값을 지금 실행 중인 게임에 모두 반영한다.</summary>
    public static void Apply()
    {
        if (!Application.isPlaying) return; // 에디터 미리보기(캡처)에서는 아무것도 바꾸지 않는다
        AudioListener.volume = Volume01;

        int fps = FrameRateValues[FrameRateIndex];
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;

        EnsureSettingsVolume();
        if (colorAdjustments != null)
        {
            bool changed = Mathf.Abs(Brightness01 - DefaultBrightness) > 0.01f;
            colorAdjustments.postExposure.overrideState = changed;
            // 기본 밝기에서는 오버라이드를 끄고(씬 프로파일 그대로), 바꿨을 때만 -0.2(씬 기본 노출) 기준으로 가감.
            colorAdjustments.postExposure.value = -0.2f + (Brightness01 - DefaultBrightness) * 2f * BrightnessRangeEv;
        }
        if (motionBlur != null)
        {
            bool off = MotionBlurOn == 0;
            motionBlur.intensity.overrideState = off; // 끔이면 0으로 덮어쓰고, 켬이면 씬 프로파일 값 그대로
            motionBlur.intensity.value = 0f;
        }

        // 화면 모드/해상도는 빌드에서만(에디터 Game 창은 건드리지 않는다)
        if (!Application.isEditor)
        {
            var rs = Resolutions();
            var r = rs[Mathf.Clamp(ResolutionIndex, 0, rs.Count - 1)];
            FullScreenMode mode = ScreenMode == 0 ? FullScreenMode.ExclusiveFullScreen
                                : ScreenMode == 2 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(r.x, r.y, mode);
        }
    }

    public static void Save() { PlayerPrefs.Save(); }

    static void EnsureSettingsVolume()
    {
        if (settingsVolume != null) return;
        var go = new GameObject("SettingsVolume");
        Object.DontDestroyOnLoad(go);
        settingsVolume = go.AddComponent<Volume>();
        settingsVolume.isGlobal = true;
        settingsVolume.priority = 150f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        colorAdjustments = profile.Add<ColorAdjustments>(false);
        motionBlur = profile.Add<MotionBlur>(false);
        settingsVolume.profile = profile;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() { Apply(); }
}
