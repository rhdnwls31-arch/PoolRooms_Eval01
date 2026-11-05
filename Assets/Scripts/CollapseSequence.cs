using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 도입부에서 플레이어가 자유롭게 돌아다니다 "의식을 잃는" 연출.
//
// 씬 시작 후 freeRoamTime이 지나면 플레이어가 어디에 있든 발동한다 — 특정 위치에
// 닿아야 시작하는 트리거가 아니다. 그전까지는 아무것도 잠그지 않는다.
//
// 카메라는 **건드리지 않는다.** 이전 구현은 카메라를 수직으로 내렸는데, 몸이 접히는
// 단서가 하나도 없으니 "사람이 쓰러진다"가 아니라 "바닥이 꺼진다 / 엘리베이터가 내려간다"로
// 읽혔다. 숫자를 조정해서 고칠 수 있는 문제가 아니라 표현 방식의 문제였다.
// 대신 눈꺼풀이 닫히고 시야가 좁아진다 — 의식을 잃는 것은 원래 눈에서 일어나는 일이다.
public class CollapseSequence : MonoBehaviour
{
    [Header("연출 대상 (Inspector에서 연결)")]
    public PlayerController player;        // 조작을 끌 대상
    public ScreenFader fader;              // 눈꺼풀이 없을 때의 대비용 (기존 ScreenFader 재사용)
    public AudioSource collapseSound;      // 쓰러지는 소리. 클립이 아직 없으면 무음 재생
    public RectTransform eyelidTop;        // 위에서 내려오는 검은 막 — IntroMapBuilder가 만들고 연결한다
    public RectTransform eyelidBottom;     // 아래에서 올라오는 검은 막

    [Header("1. 자유 이동 — 씬 시작 후 이 시간이 지나면 발동")]
    public float freeRoamTime = 20f;

    [Header("2. 눈꺼풀 — 처짐 → 살짝 떠짐 → 완전히 감김")]
    public float droopTime = 0.45f;                       // 처음 무겁게 처지는 시간
    [Range(0f, 1f)] public float droopAmount = 0.30f;
    public float blinkOpenTime = 0.22f;                   // 다시 살짝 떠지는 시간 (버티는 순간)
    [Range(0f, 1f)] public float blinkOpenAmount = 0.08f;
    public float closeTime = 0.75f;                       // 완전히 감기는 시간
    public float holdBlack = 0.35f;                       // 암전 유지

    [Header("3. 시야 좁힘 (URP Vignette, 런타임 생성)")]
    public float vignetteMax = 0.62f;

    [Header("4. 다음 씬")]
    public string nextSceneName = "Scene_Game";

    // 위/아래 눈꺼풀이 화면 높이에서 차지하는 최대 비율. 윗눈꺼풀이 더 많이 내려오는 것이
    // 실제 눈과 같고, 합이 1을 넘어야 완전히 감겼을 때 틈이 없다.
    const float TopShare = 0.62f;
    const float BottomShare = 0.45f;

    Vignette vignette;

    void Start()
    {
        SetupVignette();
        SetLids(0f);
        SetVignette(0f);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // 1. 여기서는 아무것도 하지 않는다 — 플레이어는 평소대로 걷고 둘러본다.
        yield return new WaitForSeconds(freeRoamTime);

        if (collapseSound != null) collapseSound.Play(); // 클립이 없으면 호출만 됨

        // 2-a. 눈꺼풀이 무겁게 처진다. 이 동안은 **아직 조작이 살아 있다** —
        //      시야가 흐려지는데 아직 움직일 수 있어야 "어? 왜 이러지"가 읽힌다.
        yield return Lids(0f, droopAmount, droopTime, 0f, vignetteMax * 0.45f);

        // 2-b. 여기서 조작을 끊는다 (GameManager.SetPlayerControl과 같은 방식)
        if (player != null) player.enabled = false;

        // 2-c. 한 번 버티듯 살짝 떠졌다가
        yield return Lids(droopAmount, blinkOpenAmount, blinkOpenTime, vignetteMax * 0.45f, vignetteMax * 0.30f);

        // 2-d. 완전히 감긴다
        yield return Lids(blinkOpenAmount, 1f, closeTime, vignetteMax * 0.30f, vignetteMax);

        // 눈꺼풀이 연결되지 않은 경우의 대비 — 화면이 밝은 채로 씬이 바뀌지 않게
        if (eyelidTop == null && fader != null) yield return fader.FadeOut(0.3f);

        if (holdBlack > 0f) yield return new WaitForSeconds(holdBlack);

        // 3. 다음 씬으로 (복도에서 눈을 뜨는 연출은 Story 003 범위)
        if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
    }

    // 눈꺼풀과 비네트를 함께 보간한다. 둘은 항상 같이 움직여야 한 가지 현상으로 읽힌다.
    IEnumerator Lids(float from, float to, float duration, float vFrom, float vTo)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            SetLids(Mathf.Lerp(from, to, u));
            SetVignette(Mathf.Lerp(vFrom, vTo, u));
            yield return null;
        }
        SetLids(to);
        SetVignette(vTo);
    }

    // amount: 0 = 완전히 뜬 상태, 1 = 완전히 감긴 상태
    void SetLids(float amount)
    {
        if (eyelidTop == null || eyelidBottom == null) return;
        var parent = eyelidTop.parent as RectTransform;
        if (parent == null) return;

        float h = parent.rect.height;
        eyelidTop.sizeDelta = new Vector2(0f, h * TopShare * amount);
        eyelidBottom.sizeDelta = new Vector2(0f, h * BottomShare * amount);
    }

    void SetVignette(float intensity)
    {
        if (vignette != null) vignette.intensity.value = intensity;
    }

    // 씬에 Volume을 미리 두지 않고 런타임에 만든다 — 이 연출 전용이라
    // 다른 포스트 프로세싱 설정에 영향을 주지 않는다.
    void SetupVignette()
    {
        var go = new GameObject("CollapseVignette");
        go.transform.SetParent(transform, false);

        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignette = profile.Add<Vignette>(true);
        vignette.color.overrideState = true;
        vignette.color.value = Color.black;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.9f;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0f;
        volume.profile = profile;
    }
}
