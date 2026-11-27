using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 첫 장면(2026-11-19 사용자 아이디어). 검은 화면에서 천천히 밝아지는 동안 주인공이 "Where... am I?"라고 중얼거린다.
/// 그레이박스 씬에서는 IntroFadeIn 대신 이 스크립트가 첫 페이드인을 맡는다. 다 밝아질 때까지 움직이지 못한다.
/// 목소리(voice)는 아직 비어 있다. 녹음이 생기면 칸에 넣기만 하면 된다.
/// </summary>
public class IntroMonologue : MonoBehaviour
{
    public ScreenFader fader;
    public PlayerController player;
    [Tooltip("주인공의 첫 혼잣말(영어 자막).")]
    public string line = "Where... am I?";
    [Tooltip("나중에 넣을 목소리. 비어 있으면 자막만 나온다.")]
    public AudioClip voice;
    [Tooltip("밝아지기 전 검은 화면으로 있는 시간(초).")]
    public float blackHold = 1.0f;
    [Tooltip("검은 화면에서 다 밝아지는 시간(초).")]
    public float dissolveTime = 3.5f;
    [Tooltip("밝아지기 시작하고 몇 초 뒤에 말하는지.")]
    public float lineDelay = 0.8f;
    [Tooltip("자막이 나타나고 사라지는 시간(초).")]
    public float lineFade = 0.5f;
    [Tooltip("자막을 보여 주는 시간(초).")]
    public float lineHold = 2.2f;

    // ---- 목표 안내(2026-11-27 사용자 요청): 혼잣말 뒤 오른쪽 위에 얇은 글꼴로 띄웠다가 스르르 사라진다 ----
    [Tooltip("혼잣말 뒤 오른쪽 위에 보여 줄 목표 안내. 비우면 띄우지 않는다.")]
    public string objectiveLine = "이상현상을 찾아 탈출하세요.";
    [Tooltip("혼잣말이 끝나고 목표 안내가 나타나기까지(초).")]
    public float objectiveDelay = 0.6f;
    [Tooltip("목표 안내가 나타나는 시간(초).")]
    public float objectiveFadeIn = 1.2f;
    [Tooltip("목표 안내를 보여 주는 시간(초).")]
    public float objectiveHold = 30f; // 2026-11-27 사용자 요청: 10초는 짧다
    [Tooltip("목표 안내가 사라지는(디졸브) 시간(초).")]
    public float objectiveFadeOut = 2.5f;
    [Tooltip("목표 안내 글자 크기(1920x1080 기준).")]
    public int objectiveFontSize = 34;

    void Start() { StartCoroutine(Run()); }

    IEnumerator Run()
    {
        if (fader == null) yield break;
        fader.SetColor(Color.black);
        fader.SetBlack();
        if (player != null) player.enabled = false;
        var text = Subtitle.Create(fader.fadeImage.transform.parent, "IntroSubtitle");
        text.transform.SetAsLastSibling(); // 검은 화면 위에 보이게

        yield return new WaitForSeconds(blackHold);
        StartCoroutine(Dissolve());
        yield return new WaitForSeconds(lineDelay);
        Subtitle.PlayVoice(voice, player != null ? player.cameraTransform : null);
        yield return Subtitle.Show(text, line, lineFade, lineHold);
        float left = dissolveTime - lineDelay - lineFade * 2f - lineHold;
        if (left > 0f) yield return new WaitForSeconds(left);
        if (player != null) player.enabled = true;
        Destroy(text.gameObject);

        if (!string.IsNullOrEmpty(objectiveLine)) StartCoroutine(ShowObjective(fader.fadeImage.transform.parent));
    }

    /// <summary>
    /// 오른쪽 위 목표 안내. 혼잣말 자막(Subtitle)과 같은 글꼴(2026-11-27: Pretendard Light는 너무 얇아 안 보였다), 검은 글자.
    /// 플레이어가 움직일 수 있게 된 뒤에 나타나고, 보여 주는 동안 조작을 막지 않는다.
    /// </summary>
    IEnumerator ShowObjective(Transform canvas)
    {
        yield return new WaitForSeconds(objectiveDelay);
        var go = new GameObject("ObjectiveHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-90f, -70f); rt.sizeDelta = new Vector2(900f, 60f);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Subtitle.Create와 같은 글꼴
        t.fontSize = objectiveFontSize; t.alignment = TextAnchor.UpperRight;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false; t.supportRichText = false;
        t.text = objectiveLine;
        // 2026-11-27 사용자 피드백: 시작 자리는 흰 벽이 많아 밝은 글자가 묻혔다 → 검은 글자, 그림자 없음
        var baseColor = new Color(0.06f, 0.06f, 0.07f, 1f);

        // 보이는 밝기가 고르게 변하도록 SmoothStep으로 나타났다가, 디졸브로 사라진다
        for (float x = 0f; x < objectiveFadeIn; x += Time.deltaTime) { t.color = WithAlpha(baseColor, Mathf.SmoothStep(0f, 1f, x / objectiveFadeIn)); yield return null; }
        t.color = baseColor;
        yield return new WaitForSeconds(objectiveHold);
        for (float x = 0f; x < objectiveFadeOut; x += Time.deltaTime) { t.color = WithAlpha(baseColor, 1f - Mathf.SmoothStep(0f, 1f, x / objectiveFadeOut)); yield return null; }
        Destroy(go);
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    // UI는 선형 색공간에서 섞여서 검은 막의 알파를 고르게 줄이면 눈에는 처음에 확 밝아져 보인다.
    // 보이는 밝기(v)가 시간에 고르게 늘도록 알파 = 1 - v^2.2로 줄인다.
    IEnumerator Dissolve()
    {
        var img = fader.fadeImage;
        for (float t = 0f; t < dissolveTime; t += Time.deltaTime)
        {
            float v = Mathf.SmoothStep(0f, 1f, t / dissolveTime);
            var c = img.color; c.a = 1f - Mathf.Pow(v, 2.2f); img.color = c;
            yield return null;
        }
        var e = img.color; e.a = 0f; img.color = e;
    }
}
