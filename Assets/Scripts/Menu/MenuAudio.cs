using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 소리(2026-11-27 사운드 단계). <see cref="MenuController"/>가 메뉴를 만들 때 붙인다.
/// 1) 메인 메뉴 "시작하기"를 누르면 가라앉는 소리(씬이 바뀌어도 끝까지 들린다). 마우스 올림·누름 소리는 넣지 않는다(사용자 결정).
/// 2) 메인 메뉴만: 주제곡 + 배경 영상에 맞는 환경음(플라밍고 = 바깥 수영장, 수영장 홀 = 실내 물소리, 가라앉은 도시 = 도시) + 아주 작은 브라운관 웅 소리.
/// 소리 파일은 Resources/MenuAudio에 있다(에디터 패스가 다듬어 넣는다). 없는 파일은 그냥 조용하다.
/// </summary>
public class MenuAudio : MonoBehaviour
{
    public const string ResourceFolder = "MenuAudio";

    [Range(0f, 1f)] public float uiVolume = 0.5f;
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    [Range(0f, 1f)] public float ambienceVolume = 0.25f;
    [Range(0f, 1f)] public float crtVolume = 0.06f;

    static MenuAudio current;
    AudioClip start;

    /// <summary>메뉴에 소리를 붙인다. main이면 주제곡·환경음도 튼다. startButton은 누르면 시작 소리를 낸다.</summary>
    public static MenuAudio Attach(GameObject host, Transform root, bool main, Button startButton)
    {
        var m = host.AddComponent<MenuAudio>();
        current = m;
        m.start = Load("ui_start");
        // 2026-11-27 사용자 결정: 마우스 올림·누름 소리는 넣지 않는다. 메인 메뉴 "시작하기"의 가라앉는 소리만.
        if (main && startButton != null) startButton.onClick.AddListener(PlayStart);
        if (main) m.StartCoroutine(m.BackgroundNextFrame()); // 배경 영상이 먼저 영상을 고르게 한 프레임 기다린다
        return m;
    }

    static AudioClip Load(string n) => Resources.Load<AudioClip>(ResourceFolder + "/" + n);

    AudioSource Source(string n, bool loop)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0f; s.ignoreListenerPause = true;
        return s;
    }

    System.Collections.IEnumerator BackgroundNextFrame()
    {
        yield return null;
        StartBackground();
    }

    void StartBackground()
    {
        Loop(Load("menu_theme"), musicVolume);
        Loop(Load(AmbienceFor(MenuBackgroundVideo.CurrentClipName)), ambienceVolume);
        Loop(Load("amb_crt"), crtVolume);
    }

    void Loop(AudioClip c, float vol)
    {
        if (c == null) return;
        var s = Source("Loop_" + c.name, true);
        s.clip = c; s.volume = vol; s.Play();
    }

    /// <summary>배경 영상 이름에 맞는 환경음 파일 이름. 모르는 영상이면 실내 물소리.</summary>
    public static string AmbienceFor(string videoName)
    {
        var n = (videoName ?? "").ToLowerInvariant();
        if (n.Contains("flamingo")) return "amb_outdoor_pool";
        if (n.Contains("city")) return "amb_city";
        return "amb_pool";
    }

    /// <summary>시작하기: 씬이 바뀌어도 끝까지 들리게 따로 남는 소리로 낸다.</summary>
    public static void PlayStart()
    {
        if (current == null || current.start == null) return;
        var go = new GameObject("MenuStartSound");
        DontDestroyOnLoad(go);
        var s = go.AddComponent<AudioSource>();
        s.spatialBlend = 0f; s.volume = current.uiVolume; s.clip = current.start; s.ignoreListenerPause = true;
        s.Play();
        Destroy(go, current.start.length + 0.1f);
    }

    void OnDestroy() { if (current == this) current = null; }
}
