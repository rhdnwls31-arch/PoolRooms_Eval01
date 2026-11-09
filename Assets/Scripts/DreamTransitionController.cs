using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Story 007: 화면 전환 연출 (노이즈 증가 → 암전 → 씬 전환).
// Story 006의 가짜 엔딩 돌진 마네킹과의 접촉(GameManager.OnFakeEndingContact)이
// BeginTransition()을 호출하면서 시작된다.
//
// 흐름: Film Grain 강도를 짧고 강하게 스파이크시켰다가 평소 수준(시작 시점에 읽어 둔 값 —
// Horror_PostFX.asset의 평소 세기를 바꾸는 것은 Story 010의 몫이라 여기서는 건드리지 않는다)
// 으로 되돌린 뒤, 화면을 암전시키고, 화면이 완전히 검은 동안 다음 씬(두 번째 꿈 공간,
// Story 008)을 로드한다. 암전 이후의 페이드인은 이 스크립트의 책임이 아니다 — 목적지 씬
// 자신의 ScreenFader + IntroFadeIn(Story 003과 동일한 패턴)이 맡는다.
//
// postFxVolume.profile을 통해 얻는 FilmGrain은 Volume의 런타임 전용 복사본이다
// (Volume.profile 접근 시 sharedProfile을 자동으로 복제한다) — Horror_PostFX.asset
// 원본을 손으로도, 코드로도 고치지 않는다.
public class DreamTransitionController : MonoBehaviour
{
    [Header("참조 (Inspector에서 연결 — GameManager.postFxVolume/fader와 동일한 오브젝트)")]
    public Volume postFxVolume;   // GameManager.postFxVolume과 같은 Volume을 가리켜야 한다
    public ScreenFader fader;     // GameManager.fader와 같은 FadeCanvas의 ScreenFader
    public Transform player;      // 조작을 끌 대상 (GameManager.player와 동일)

    [Header("다음 씬")]
    public string nextSceneName = "Scene_SecondDream";

    [Header("노이즈 스파이크 (Film Grain intensity, 0~1)")]
    public float grainPeakIntensity = 1f; // 스파이크 최고치
    public float grainRiseTime = 0.12f;   // 평소 → 최고치 (짧고 강하게)
    public float grainFallTime = 0.35f;   // 최고치 → 평소로 즉시 감소

    [Header("암전")]
    public float blackoutFadeTime = 0.5f; // ScreenFader.fadeTime(라운드 전환용 0.4초)과 별도로 조정 가능

    // Story 006의 마네킹 접촉(GameManager.OnFakeEndingContact)이 호출한다.
    public void BeginTransition()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false; // 낡은 씬은 곧 사라지므로 조작만 끈다
        }

        FilmGrain filmGrain = null;
        if (postFxVolume != null) postFxVolume.profile.TryGet(out filmGrain);

        if (filmGrain != null)
        {
            float baseline = filmGrain.intensity.value; // 평소 수준 — 이 값으로 되돌아간다
            yield return RampGrain(filmGrain, baseline, grainPeakIntensity, grainRiseTime);
            yield return RampGrain(filmGrain, grainPeakIntensity, baseline, grainFallTime);
        }

        yield return fader.FadeOut(blackoutFadeTime); // 화면이 완전히 검게

        SceneManager.LoadScene(nextSceneName); // 암전 구간 중에 로드 — 페이드인은 목적지 씬의 IntroFadeIn이 맡는다
    }

    IEnumerator RampGrain(FilmGrain filmGrain, float from, float to, float duration)
    {
        if (duration <= 0f) { filmGrain.intensity.value = to; yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            filmGrain.intensity.value = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        filmGrain.intensity.value = to;
    }
}
