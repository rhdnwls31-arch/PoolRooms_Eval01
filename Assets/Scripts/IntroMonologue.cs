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
    }

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
