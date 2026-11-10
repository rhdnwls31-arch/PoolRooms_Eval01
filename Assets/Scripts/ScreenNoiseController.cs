using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Story 010 — "평소 / 근접 / 전환" 세 단계 화면 노이즈 통합 관리.
//
// Film Grain과 Chromatic Aberration은 이 스크립트가 직접 쓴다 — 둘 다 라운드가 바뀔 때만
// (SetAnomalyProximity 호출 시) 한 번씩 갱신되는 이산적(discrete) 쓰기라, Story 007의
// DreamTransitionController가 전환 순간에만 한 번 실행하는 Film Grain 스파이크 코루틴과
// 겹치지 않는다(그 코루틴은 시작 시점의 "현재 값"을 베이스라인으로 읽어 되돌아가므로, 평소/근접
// 중 어느 값이 떠 있었든 그대로 복원된다).
//
// Vignette는 이 스크립트가 직접 쓰지 않는다 — GameManager.UpdateVignette()가 이미 정신력에
// 따라 Vignette.intensity를 매번 쓰고 있어서, 여기서도 직접 쓰면 둘이 매번 서로 덮어쓴다.
// 대신 CurrentVignetteOffset만 내보내고, GameManager가 자신의 정신력 항과 더해서 쓴다 —
// Vignette.intensity의 쓰기 지점은 끝까지 GameManager 하나뿐이다.
//
// "전환" 단계의 Chromatic Aberration/Vignette 값은 참고용 데이터로만 존재한다 — Story
// 002/006/007의 기존 전환 연출 코드를 건드리지 않는다는 범위 밖 제약 때문에 실제로 연결되어
// 있지는 않다(Film Grain의 전환 피크만 DreamTransitionController를 통해 실제로 재생된다).
public class ScreenNoiseController : MonoBehaviour
{
    public enum Stage { Normal, Near }

    [Header("참조 (Inspector에서 연결 — GameManager.postFxVolume과 동일한 Volume)")]
    public Volume postFxVolume;

    [Header("평소 (Normal)")]
    public float normalFilmGrain = 0.1f;
    public float normalChromaticAberration = 0.03f;
    public float normalVignetteOffset = 0f;

    [Header("근접 (이상현상이 활성화된 라운드 동안 — 임시 기준, 추후 조정 가능)")]
    public float nearFilmGrain = 0.2f;
    public float nearChromaticAberration = 0.1f;
    public float nearVignetteOffset = 0.05f;

    [Header("전환 (참고용 — Film Grain만 DreamTransitionController를 통해 실제로 재생됨)")]
    public float transitionFilmGrainReference = 1f;             // DreamTransitionController.grainPeakIntensity와 동일
    public float transitionChromaticAberrationReference = 0.35f; // 연결되지 않음 — 데이터로만 존재
    public float transitionVignetteOffsetReference = 0.15f;      // 연결되지 않음 — 데이터로만 존재

    public Stage CurrentStage { get; private set; } = Stage.Normal;

    // GameManager.UpdateVignette()가 자신의 정신력 항에 더해서 쓴다.
    public float CurrentVignetteOffset => CurrentStage == Stage.Near ? nearVignetteOffset : normalVignetteOffset;

    FilmGrain filmGrain;
    ChromaticAberration chromaticAberration;

    void Awake()
    {
        if (postFxVolume != null)
        {
            postFxVolume.profile.TryGet(out filmGrain);
            postFxVolume.profile.TryGet(out chromaticAberration);
        }
    }

    void Start()
    {
        Apply();
    }

    // GameManager.StartRound()/FakeEndingRoutine()이 라운드가 바뀔 때마다 한 번씩만 호출한다 —
    // 매 프레임 호출하면 Story 007의 전환 스파이크 코루틴과 같은 프레임에 겹쳐 쓸 수 있다.
    public void SetAnomalyProximity(bool isNear)
    {
        CurrentStage = isNear ? Stage.Near : Stage.Normal;
        Apply();
    }

    void Apply()
    {
        bool near = CurrentStage == Stage.Near;
        if (filmGrain != null) filmGrain.intensity.value = near ? nearFilmGrain : normalFilmGrain;
        if (chromaticAberration != null) chromaticAberration.intensity.value = near ? nearChromaticAberration : normalChromaticAberration;
    }
}
