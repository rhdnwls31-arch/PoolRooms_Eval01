using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 메인 메뉴 배경 영상(2026-11-27). 메뉴가 열릴 때 Resources/MenuVideos의 영상 중 하나를 무작위로 골라 계속 반복한다
/// (사용자 결정: 세 영상을 차례로 넘기지 않고 하나만). 영상 끝과 처음은 이어지지 않으므로, 끝나기 조금 전부터
/// 같은 영상의 처음을 천천히 겹쳐 되감는다(깜빡이거나 툭 끊기지 않게 — 두 플레이어를 번갈아 쓴다).
/// 영상은 녹화 도구(MenuVideoRecorder, DEPTH 5m > Menu > Record Menu Videos)가 만든다. 소리는 쓰지 않는다.
/// </summary>
public class MenuBackgroundVideo : MonoBehaviour
{
    /// <summary>영상을 찾는 Resources 폴더 이름.</summary>
    public const string ResourceFolder = "MenuVideos";
    /// <summary>반복할 때 끝과 처음이 겹쳐 바뀌는 시간(초).</summary>
    public const float CrossfadeSeconds = 1.6f;
    /// <summary>메뉴가 처음 뜰 때 검은 화면에서 영상이 밝아지는 시간(초).</summary>
    public const float FirstFadeSeconds = 1.2f;
    /// <summary>글자가 잘 읽히도록 영상 위에 덮는 검은 막의 진하기(0~1).</summary>
    public const float DimAlpha = 0.28f;
    const int TexW = 1920, TexH = 1080;

    /// <summary>지금 메뉴 배경으로 도는 영상 이름(없으면 null).</summary>
    public static string CurrentClipName { get; private set; }

    readonly VideoPlayer[] players = new VideoPlayer[2];
    readonly RawImage[] images = new RawImage[2];
    readonly RenderTexture[] targets = new RenderTexture[2];
    int front;          // 지금 보이는 플레이어
    bool crossfading;
    float fadeT;

    /// <summary>
    /// parent의 맨 아래(첫 자식)에 영상 층과 어둡게 덮는 막을 만든다. 메뉴 글자는 그 위에 그려진다.
    /// 에디터 미리보기(Play 아님)에서는 층만 만들고 재생하지 않는다.
    /// </summary>
    public static MenuBackgroundVideo Attach(RectTransform parent)
    {
        var layer = NewRect("BackgroundVideo", parent);
        Stretch(layer);
        layer.SetSiblingIndex(0);
        var v = layer.gameObject.AddComponent<MenuBackgroundVideo>();
        for (int i = 0; i < 2; i++)
        {
            var img = NewRect("Video" + i, layer).gameObject.AddComponent<RawImage>();
            Stretch(img.rectTransform);
            img.color = new Color(1, 1, 1, 0); img.raycastTarget = false;
            v.images[i] = img;
        }
        var dim = NewRect("Dim", layer).gameObject.AddComponent<Image>();
        Stretch(dim.rectTransform);
        dim.color = new Color(0, 0, 0, DimAlpha); dim.raycastTarget = false;
        // 메뉴(오른쪽 가운데)와 제목(위 가운데) 뒤만 조금 더 어둡게 — 밝은 벽·기둥 위에서도 글자가 읽히게
        AddShade(layer, "ShadeRight", horizontal: true, from: ShadeRightStart, maxAlpha: ShadeRightAlpha);
        AddShade(layer, "ShadeTop", horizontal: false, from: ShadeTopStart, maxAlpha: ShadeTopAlpha);
        return v;
    }

    /// <summary>오른쪽 그늘: 화면 가로 이 비율부터 오른쪽 끝까지 점점 진해진다.</summary>
    public const float ShadeRightStart = 0.5f, ShadeRightAlpha = 0.7f;
    /// <summary>위쪽 그늘: 화면 세로 이 비율(아래 기준)부터 위 끝까지 점점 진해진다.</summary>
    public const float ShadeTopStart = 0.68f, ShadeTopAlpha = 0.4f;

    static void AddShade(RectTransform layer, string name, bool horizontal, float from, float maxAlpha)
    {
        var img = NewRect(name, layer).gameObject.AddComponent<RawImage>();
        var rt = img.rectTransform;
        rt.anchorMin = horizontal ? new Vector2(from, 0f) : new Vector2(0f, from);
        rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        const int N = 64;
        var tex = new Texture2D(horizontal ? N : 1, horizontal ? 1 : N, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[N];
        for (int i = 0; i < N; i++)
        {
            float t = i / (N - 1f);
            px[i] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, t) * maxAlpha * 255f));
        }
        tex.SetPixels32(px); tex.Apply(false);
        img.texture = tex; img.raycastTarget = false;
    }

    void Start()
    {
        var clips = Resources.LoadAll<VideoClip>(ResourceFolder);
        if (clips.Length == 0) { Debug.LogWarning($"[MenuBackgroundVideo] Resources/{ResourceFolder}에 영상이 없다."); enabled = false; return; }
        Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
        var clip = clips[UnityEngine.Random.Range(0, clips.Length)]; // 메뉴를 열 때마다 하나를 무작위로
        CurrentClipName = clip.name; // 메뉴 환경음이 영상에 맞춰 고른다(MenuAudio)

        for (int i = 0; i < 2; i++)
        {
            targets[i] = new RenderTexture(TexW, TexH, 0, RenderTextureFormat.ARGB32) { name = "MenuVideo" + i };
            var p = gameObject.AddComponent<VideoPlayer>();
            p.playOnAwake = false; p.isLooping = false; p.skipOnDrop = true; p.waitForFirstFrame = true;
            p.renderMode = VideoRenderMode.RenderTexture; p.targetTexture = targets[i];
            p.audioOutputMode = VideoAudioOutputMode.None;
            p.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            p.clip = clip; // 두 플레이어 모두 같은 영상(반복할 때 서로 넘겨받는다)
            players[i] = p;
            images[i].texture = targets[i];
        }
        front = 0;
        players[0].Play();
        players[1].Prepare();
    }

    void Update()
    {
        var cur = players[front];
        // 처음: 첫 프레임이 나오면 검은 화면에서 천천히 밝힌다
        if (!crossfading && images[front].color.a < 1f && cur.isPlaying && cur.frame > 0)
            SetAlpha(images[front], images[front].color.a + Time.unscaledDeltaTime / FirstFadeSeconds);

        var back = players[1 - front];
        if (!crossfading)
        {
            double remain = cur.length - cur.time;
            if (cur.isPlaying && cur.frame > 0 && remain <= CrossfadeSeconds && back.isPrepared)
            {
                images[front].transform.SetSiblingIndex(0);     // 뒤 영상을 지금 영상 바로 위로 올려 서서히 나타나게
                images[1 - front].transform.SetSiblingIndex(1); // (어둡게 덮는 막·그늘은 그 위에 그대로)
                back.Play();
                crossfading = true; fadeT = 0f;
            }
        }
        else
        {
            fadeT += Time.unscaledDeltaTime / CrossfadeSeconds;
            if (back.frame > 0) SetAlpha(images[1 - front], fadeT);
            if (fadeT >= 1f && back.frame > 0)
            {
                SetAlpha(images[1 - front], 1f);
                cur.Stop();
                SetAlpha(images[front], 0f);
                front = 1 - front;
                crossfading = false;
                players[1 - front].Prepare(); // 방금 멈춘 플레이어를 다음 반복용으로 처음부터 준비
            }
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < 2; i++) if (targets[i] != null) { targets[i].Release(); Destroy(targets[i]); }
    }

    static void SetAlpha(RawImage img, float a) { var c = img.color; c.a = Mathf.Clamp01(a); img.color = c; }

    // ---- TV 테두리 -----------------------------------------------------------------
    // 영상에는 노이즈만 들어 있고(녹화 때 테두리를 뺐다), 둥근 모서리·어두운 가장자리는 메뉴 맨 위에 한 번만 덮는다.
    // 메뉴 UI는 화면 위 오버레이라 카메라 후처리(CrtScreen)를 받지 않으므로 같은 모양을 그림으로 만든다.
    /// <summary>TV 화면 모서리 둥글기(화면 높이 기준, 인게임 CrtScreen과 같은 값).</summary>
    public const float FrameCornerRadius = 0.06f;
    /// <summary>테두리 안쪽 여백(화면 높이 기준).</summary>
    public const float FrameInset = 0.012f;
    /// <summary>가장자리가 어두워지는 폭(화면 높이 기준)과 가장 어두운 정도.</summary>
    public const float FrameEdgeWidth = 0.16f, FrameEdgeDarken = 0.45f;
    static Texture2D frameTex;

    /// <summary>parent의 맨 위(마지막 자식)에 TV 테두리를 덮는다. 클릭은 막지 않는다.</summary>
    public static RawImage AttachTvFrame(RectTransform parent)
    {
        var img = NewRect("TvFrame", parent).gameObject.AddComponent<RawImage>();
        Stretch(img.rectTransform);
        img.texture = FrameTexture();
        img.raycastTarget = false;
        img.transform.SetAsLastSibling();
        return img;
    }

    /// <summary>16:9 둥근 사각형 바깥은 검정, 안쪽은 가장자리로 갈수록 살짝 어두워지는 알파 텍스처.</summary>
    public static Texture2D FrameTexture()
    {
        if (frameTex != null) return frameTex;
        const int W = 960, H = 540;
        frameTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[W * H];
        float halfW = 0.5f * W / H, halfH = 0.5f; // 화면 높이 = 1
        float bx = halfW - FrameInset, by = halfH - FrameInset, r = FrameCornerRadius;
        float aa = 1.5f / H; // 가장자리 부드럽게(약 1.5px)
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / H - halfW, v = (y + 0.5f) / H - halfH;
                float d = RoundRectDistance(u, v, bx, by, r); // 안쪽 음수
                float outside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-aa, aa, d));
                float edge = FrameEdgeDarken * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-d / FrameEdgeWidth)));
                float a = Mathf.Max(outside, edge);
                px[y * W + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(a * 255f));
            }
        frameTex.SetPixels32(px); frameTex.Apply(false);
        return frameTex;
    }

    /// <summary>둥근 사각형까지의 부호 있는 거리(안쪽 음수). 반폭 bx,by, 모서리 반지름 r.</summary>
    public static float RoundRectDistance(float u, float v, float bx, float by, float r)
    {
        float qx = Mathf.Abs(u) - (bx - r), qy = Mathf.Abs(v) - (by - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
}
