using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 인게임 화면 노이즈: 옅은 필름 그레인 + 가는 주사선(브라운관/캠코더 느낌).
/// 메인 메뉴를 뺀 모든 씬 위에 얹는 화면 전체 오버레이이며, 씬이 바뀌어도 유지된다.
/// 색 번짐/비네트/그레인 강도는 씬의 Post-processing Volume이 따로 맡는다.
/// </summary>
public class InGameNoiseOverlay : MonoBehaviour
{
    const string MainMenuScene = "Scene_MainMenu";
    const float GrainAlpha = 0.03f;     // 그레인 세기(낮을수록 은은)
    const float ScanlineAlpha = 0.05f;  // 주사선 세기
    const int ScanlinePeriodPx = 3;     // 주사선 간격(화면 픽셀)

    static InGameNoiseOverlay instance;
    Canvas canvas;
    RawImage scan;
    Texture2D scanTex;
    int lastH;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() { SceneManager.sceneLoaded += (s, m) => Ensure(s.name); }

    static void Ensure(string sceneName)
    {
        if (instance == null)
        {
            var go = new GameObject("InGameNoiseOverlay");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<InGameNoiseOverlay>();
            instance.Build();
        }
        instance.canvas.enabled = sceneName != MainMenuScene;
    }

    void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400; // 메뉴(500) 아래

        var grain = NewLayer("Grain", new Color(1, 1, 1, GrainAlpha));
        var nf = grain.gameObject.AddComponent<NoiseFlicker>();
        nf.target = grain; nf.Prime();

        scanTex = new Texture2D(1, ScanlinePeriodPx, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        for (int i = 0; i < ScanlinePeriodPx; i++) scanTex.SetPixel(0, i, i == 0 ? Color.black : new Color(0, 0, 0, 0));
        scanTex.Apply();
        scan = NewLayer("Scanlines", new Color(1, 1, 1, ScanlineAlpha));
        scan.texture = scanTex;
        UpdateScan();
    }

    RawImage NewLayer(string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var raw = go.AddComponent<RawImage>();
        raw.color = c; raw.raycastTarget = false;
        return raw;
    }

    void UpdateScan()
    {
        lastH = Screen.height;
        scan.uvRect = new Rect(0, 0, 1, Screen.height / (float)ScanlinePeriodPx);
    }

    void Update() { if (Screen.height != lastH) UpdateScan(); }
}
