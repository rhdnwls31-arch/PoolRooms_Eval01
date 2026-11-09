using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면을 검게 만들거나 걷어내는(페이드) 스크립트
public class ScreenFader : MonoBehaviour
{
    public Image fadeImage;       // 화면을 덮는 검은 이미지 (Inspector에서 연결)
    public float fadeTime = 0.8f; // 페이드에 걸리는 시간(초)

    void Awake()
    {
        SetAlpha(1f); // 시작할 때는 완전히 검게
    }

    // 즉시 완전히 검게
    public void SetBlack() { SetAlpha(1f); }

    // 페이드 색을 바꾼다 (알파는 현재 값 유지). 물속 전환처럼 검정이 아닌 색으로
    // 페이드해야 할 때 CollapseSequence가 호출한다 — 별도 페이드 시스템을 만들지 않기 위함.
    public void SetColor(Color rgb)
    {
        Color c = fadeImage.color;
        fadeImage.color = new Color(rgb.r, rgb.g, rgb.b, c.a);
    }

    // 검은 화면 → 보이는 화면 (페이드 인)
    public IEnumerator FadeIn() { return Fade(1f, 0f, fadeTime); }

    // 기본 fadeTime 대신 다른 길이로 페이드인한다. 의식을 되찾으며 "눈을 뜨는" 첫
    // 페이드인처럼 라운드 전환용 기본값(0.4초)보다 길게 걸어야 하는 경우에 쓴다.
    public IEnumerator FadeIn(float duration) { return Fade(1f, 0f, duration); }

    // 보이는 화면 → 검은 화면 (페이드 아웃)
    public IEnumerator FadeOut() { return Fade(0f, 1f, fadeTime); }

    // 기본 fadeTime 대신 다른 길이로 페이드아웃한다. 0.7초짜리 쓰러지는 연출처럼
    // 기본값(0.6초)보다 짧게 덮어야 하는 경우에 쓴다.
    public IEnumerator FadeOut(float duration) { return Fade(0f, 1f, duration); }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { SetAlpha(to); yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / duration)); // 시간에 따라 투명도를 서서히 바꿈
            yield return null; // 한 프레임 쉬고 계속
        }
        SetAlpha(to);
    }

    void SetAlpha(float a)
    {
        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }
}