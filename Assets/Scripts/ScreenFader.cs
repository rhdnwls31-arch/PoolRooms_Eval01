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

    // 검은 화면 → 보이는 화면 (페이드 인)
    public IEnumerator FadeIn() { return Fade(1f, 0f); }

    // 보이는 화면 → 검은 화면 (페이드 아웃)
    public IEnumerator FadeOut() { return Fade(0f, 1f); }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / fadeTime)); // 시간에 따라 투명도를 서서히 바꿈
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