using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;        // R 키 입력용
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;    // 씬 다시 불러오기(재시작)용
using UnityEngine.UI;                 // 결과 글자(Text)용

// 라운드 진행, 정답 판정, 승리/실패, 페이드, 수심 표지판, 정신력 램프, 결과 화면을 관리하는 스크립트
public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // 어디서든 GameManager.Instance로 접근

    public Transform player;              // 플레이어 (Inspector에서 연결)
    public AnomalyManager anomalyManager; // 이상현상 담당 (Inspector에서 연결)
    public ScreenFader fader;             // 페이드 담당 (FadeCanvas)
    public DreamTransitionController dreamTransition; // [신규] Story 007: 가짜 엔딩 접촉 뒤 노이즈 스파이크 → 암전 → 씬 전환 연출
    public ScreenNoiseController screenNoiseController; // [신규] Story 010: 평소/근접 단계 Film Grain·Chromatic Aberration 및 Vignette 오프셋
    public TextMesh[] depthSigns;         // 문 위 수심 표지판 글자 2개
    public Volume postFxVolume;           // 후처리 (PostFX Volume)
    public int targetRounds = 5;          // 연속 정답 목표
    public int maxSanity = 3;             // 시작 정신력
    [Range(0f, 1f)]
    public float anomalyChance = 0.6f;    // 이상현상이 나올 확률
    public bool firstRoundNormal = true;  // 첫 라운드는 항상 정상으로 시작

    [Header("결과 화면")]
    public GameObject resultPanel;        // ResultPanel
    public Text resultTitle;              // ResultTitle (SURFACE / DROWNED)
    public Text resultInfo;               // ResultInfo (라운드 수 등)

    [Header("정신력 램프 (신규)")]
    public Transform sanityLampsRoot;     // [신규] SanityLamps (램프 6개가 들어 있는 부모)
    public Material lampOnMat;            // [신규] 켜진 램프 재질 (Env_SanityOn)
    public Material lampOffMat;           // [신규] 꺼진 램프 재질 (Env_SanityOff)

    [Header("연출 시간(초)")]
    public float firstDelay = 0.4f;         // 게임 시작 후 밝아지기 전 대기
    public float firstFadeTime = 2f;        // 첫 페이드인 길이 — 의식을 되찾고 "눈을 뜨는" 연출용.
                                            // 라운드 전환 페이드는 ScreenFader.fadeTime(0.4초)을 그대로 쓴다.
    public float signLookTime = 0.3f;       // 표지판 숫자를 보여 주는 시간
    public float afterTeleportDelay = 0.1f; // 순간이동 후 밝아지기 전 대기

    [Header("정신력 연출")]
    public float vignetteBase = 0.2f;     // 정신력이 가득일 때 비네팅 세기
    public float vignettePerHit = 0.12f;  // 틀릴 때마다 더해지는 세기

    int correctStreak;     // 연속 정답 수
    int sanity;            // 남은 정신력
    int noAnomalyInARow;   // 이상현상 없는 라운드가 연속으로 몇 번인가
    int roundCount;        // 지금까지 시작한 라운드 수
    bool hasAnomaly;       // 이번 라운드에 이상현상이 있는가
    bool gameOver;         // 게임이 끝났는가
    bool busy;             // 페이드 전환 중인가 (중복 판정 방지)
    bool fakeEndingTriggered; // [신규] 수심 1m 가짜 엔딩에 들어갔는가 (한 번 들어가면 기존 SURFACE/DROWNED 판정으로 돌아가지 않음)
    Vector3 startPos;      // 플레이어 시작 위치
    Quaternion startRot;   // 플레이어 시작 방향
    Vignette vignette;     // 후처리의 비네팅 설정
    Renderer[] sanityLamps; // [신규] 램프 6개 (앞쪽 3개 + 뒤쪽 3개)

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        startPos = player.position;
        startRot = player.rotation;
        sanity = maxSanity;

        if (resultPanel != null) resultPanel.SetActive(false); // 결과 화면은 숨김

        // [신규] 램프 부모 아래의 Renderer들을 모아 둠
        if (sanityLampsRoot != null) sanityLamps = sanityLampsRoot.GetComponentsInChildren<Renderer>();

        // 후처리에서 비네팅 설정을 찾아 둠
        if (postFxVolume != null) postFxVolume.profile.TryGet(out vignette);
        UpdateVignette();
        UpdateLamps(); // [신규]

        // StartCoroutine(FirstRound())가 StartRound()를 동기적으로(같은 프레임에) 실행하므로,
        // --forceanomaly 플래그는 그보다 먼저 읽어 둬야 첫 라운드에 반영된다.
        bool skipToFakeEnding = false;
        foreach (var a in System.Environment.GetCommandLineArgs())
        {
            if (a == "--skiptofakeending") skipToFakeEnding = true;
            if (a == "--forceanomaly") forceAnomalyDebug = true; // [신규] Story 010: 첫 라운드를 "근접" 단계로 캡처하기 위한 디버그 훅
        }

        StartCoroutine(FirstRound());
        if (skipToFakeEnding) StartCoroutine(DebugSkipToFakeEnding());
    }

    // [디버그 전용] --skiptofakeending 인자로 실행하면 라운드를 거치지 않고 바로 가짜 엔딩
    // 접촉 상태로 건너뛴다. Story 007의 화면 전환(노이즈/암전/씬 전환)을 자동 스크린샷
    // 파이프라인으로 확인하기 위한 테스트 훅 — 인자를 명시적으로 넘길 때만 동작하므로
    // 일반 플레이/빌드에는 영향 없다.
    IEnumerator DebugSkipToFakeEnding()
    {
        yield return new WaitForSeconds(firstDelay + firstFadeTime + 0.5f); // 첫 페이드인이 끝날 때까지 대기
        OnFakeEndingContact();
    }

    // [디버그 전용] Story 010: --forceanomaly 인자로 실행하면 첫 라운드부터 이상현상이 활성화된
    // "근접" 단계로 시작한다 — 평소/근접 단계를 자동 스크린샷으로 비교 캡처하기 위한 테스트 훅.
    // 인자를 명시적으로 넘길 때만 동작하므로 일반 플레이/빌드에는 영향 없다.
    bool forceAnomalyDebug;

    // 게임 시작: 검은 화면에서 라운드를 준비하고 서서히 밝아짐
    IEnumerator FirstRound()
    {
        busy = true;
        SetPlayerControl(false);
        fader.SetBlack();
        StartRound();
        yield return new WaitForSeconds(firstDelay);
        yield return fader.FadeIn(firstFadeTime);
        SetPlayerControl(true);
        busy = false;
    }

    // 새 라운드 시작: 플레이어를 시작점으로 되돌리고, 이상현상을 적용
    void StartRound()
    {
        roundCount++;

        if (forceAnomalyDebug && roundCount == 1)
        {
            hasAnomaly = true; // [디버그 전용] Story 010: --forceanomaly로 "근접" 단계를 즉시 캡처
        }
        else if (firstRoundNormal && roundCount == 1)
        {
            hasAnomaly = false; // 첫 라운드는 무조건 정상
        }
        else if (noAnomalyInARow >= 2)
        {
            hasAnomaly = true;  // 이상현상 없는 라운드가 2번 이어졌으면 이번엔 반드시 이상현상
        }
        else
        {
            hasAnomaly = Random.value < anomalyChance;
        }

        if (hasAnomaly) noAnomalyInARow = 0;
        else noAnomalyInARow++;

        Debug.Log("라운드 " + roundCount + " 시작 / 이상현상: " + hasAnomaly);

        RepositionPlayer();

        // 플레이어를 옮긴 "다음에" 이상현상을 적용 (순서가 중요)
        anomalyManager.ApplyRound(hasAnomaly);
        // [신규] Story 010: 정답이라 정신력은 그대로여도 단계는 바뀔 수 있으므로 매 라운드 갱신한다
        if (screenNoiseController != null) screenNoiseController.SetAnomalyProximity(hasAnomaly);
        UpdateVignette();
        UpdateSigns();
    }

    // [신규] 플레이어를 시작 위치/방향으로 되돌림 (CharacterController는 끄고 옮겨야 위치가 정확히 바뀜)
    // StartRound()와 FakeEndingRoutine() 양쪽에서 재사용한다
    void RepositionPlayer()
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.position = startPos;
        player.rotation = startRot;
        cc.enabled = true;
    }

    // 플레이어가 복도 끝에 도착하면 호출됨
    public void OnPlayerReachedEnd(bool isFrontEnd)
    {
        if (gameOver || busy || fakeEndingTriggered) return;

        // 이상 없음 + 앞쪽, 또는 이상 있음 + 뒤쪽이면 정답
        bool correct = (isFrontEnd && !hasAnomaly) || (!isFrontEnd && hasAnomaly);

        if (correct)
        {
            correctStreak++;
            Debug.Log("정답! 연속 " + correctStreak + "/" + targetRounds);
        }
        else
        {
            correctStreak = 0; // 틀리면 다시 가라앉음
            sanity--;
            UpdateVignette();  // 틀릴 때마다 화면 가장자리가 어두워짐
            UpdateLamps();     // [신규] 램프가 하나 꺼짐
            Debug.Log("오답... 정신력 " + sanity + "/" + maxSanity);
        }

        StartCoroutine(Transition());
    }

    // 판정 뒤 흐름: 표지판 갱신 → 페이드 아웃 → (끝났으면 결과 화면) → 새 라운드 → 페이드 인
    IEnumerator Transition()
    {
        busy = true;
        SetPlayerControl(false);

        UpdateSigns();                                 // 표지판 숫자가 바로 바뀜
        yield return new WaitForSeconds(signLookTime); // 바뀐 숫자를 잠깐 볼 시간
        yield return fader.FadeOut();                  // 화면이 검게

        // [신규] 수심 1m: 기존 SURFACE 판정보다 먼저 확인 — 승패를 가르지 않는 별도의 가짜 엔딩 분기로 들어간다
        if (!fakeEndingTriggered && correctStreak == targetRounds - 1)
        {
            fakeEndingTriggered = true;
            yield return FakeEndingRoutine();
            yield break;
        }

        if (correctStreak >= targetRounds)
        {
            gameOver = true;
            Debug.Log("승리! 수면 위로 올라왔다");
            yield return ShowResult(true);
            yield break;
        }
        if (sanity <= 0)
        {
            gameOver = true;
            Debug.Log("실패... 익사했다");
            yield return ShowResult(false);
            yield break;
        }

        StartRound();
        yield return new WaitForSeconds(afterTeleportDelay);
        yield return fader.FadeIn();                   // 서서히 밝아짐
        SetPlayerControl(true);
        busy = false;
    }

    // [신규] 수심 1m 가짜 엔딩: 복도 중앙에 항상 서 있던 마네킹이 플레이어를 천천히 돌아보고,
    // 다 돌아본 뒤 잠깐 정적 구간에 들어간다. 여기서는 SURFACE/DROWNED 판정을 전혀 건드리지 않는다.
    // 돌아본 뒤의 돌진/접촉 처리는 Story 006이 이어받는다.
    IEnumerator FakeEndingRoutine()
    {
        Debug.Log("수심 1m... 마네킹이 나를 본다");

        RepositionPlayer();
        anomalyManager.ApplyRound(false); // 직전 라운드에 남아 있던 이상현상(조명/물/소품)을 정리 — 가짜 엔딩은 조용한 복도에서 시작한다
        // [신규] Story 010: 가짜 엔딩 복도는 직전 라운드의 "근접" 단계를 물려받지 않고 평소로 시작한다
        if (screenNoiseController != null) screenNoiseController.SetAnomalyProximity(false);
        UpdateVignette();
        anomalyManager.ShowFakeEndingMannequin(); // 화면이 검은 지금 켜 둔다 — 밝아지면 이미 서 있던 것처럼 보임

        yield return new WaitForSeconds(afterTeleportDelay);
        yield return fader.FadeIn();                   // 서서히 밝아짐
        SetPlayerControl(true);

        // 화면이 다 밝아진 "뒤에" 돌아보기를 시작한다 — 돌아보는 움직임 전체가 보이도록
        anomalyManager.StartFakeEndingLook();

        // 정적 구간이 끝날 때까지 기다림 (그 다음은 Story 006 담당)
        while (!anomalyManager.FakeEndingStaticPauseComplete)
            yield return null;

        anomalyManager.StartFakeEndingCharge(); // [신규] 정적 구간이 끝나면 돌진 시작 (Story 006)

        busy = false;
    }

    // [신규] 가짜 엔딩 돌진 마네킹에 닿았을 때 호출됨 (Story 006). 정신력/DROWNED는 건드리지 않고
    // 노이즈 스파이크 → 암전 → 씬 전환(DreamTransitionController, Story 007)으로 이어진다.
    public bool FakeEndingTransitionTriggered { get; private set; }

    public void OnFakeEndingContact()
    {
        if (FakeEndingTransitionTriggered) return;
        FakeEndingTransitionTriggered = true;
        Debug.Log("가짜 엔딩: 마네킹에게 닿았다 — 화면 전환 연출 시작 (Story 007)");
        if (dreamTransition != null) dreamTransition.BeginTransition();
    }

    // 결과 화면을 보여 주고, R 키를 누르면 처음부터 다시 시작
    IEnumerator ShowResult(bool win)
    {
        if (resultPanel != null) resultPanel.SetActive(true);

        if (win)
        {
            resultTitle.text = "SURFACE";
            resultTitle.color = new Color(0.75f, 1f, 0.95f);   // 맑은 청록색
            resultInfo.text = "Rounds: " + roundCount + "   Mistakes: " + (maxSanity - sanity);
        }
        else
        {
            resultTitle.text = "DROWNED";
            resultTitle.color = new Color(0.7f, 0.1f, 0.1f);   // 어두운 붉은색
            resultInfo.text = "Rounds survived: " + (roundCount - 1);
        }

        yield return new WaitForSeconds(0.8f); // 실수로 바로 넘어가지 않게 잠깐 대기

        // R 키가 눌릴 때까지 한 프레임씩 기다림
        while (!Keyboard.current.rKey.wasPressedThisFrame)
            yield return null;

        // 지금 씬을 처음부터 다시 불러옴 (모든 상태가 초기화됨)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 남은 정신력에 맞춰 비네팅 세기를 바꿈. [신규] Story 010: 여기에 screenNoiseController의
    // 평소/근접 오프셋을 더한다 — Vignette.intensity를 쓰는 곳은 이 메서드 하나로 유지해서
    // ScreenNoiseController와 서로 덮어쓰지 않게 한다.
    void UpdateVignette()
    {
        if (vignette == null) return;
        float stageOffset = screenNoiseController != null ? screenNoiseController.CurrentVignetteOffset : 0f;
        vignette.intensity.value = vignetteBase + (maxSanity - sanity) * vignettePerHit + stageOffset;
    }

    // [신규] 남은 정신력만큼 램프를 켜고 나머지는 끔
    void UpdateLamps()
    {
        if (sanityLamps == null || sanityLamps.Length == 0) return;

        int perSign = sanityLamps.Length / 2; // 표지판 하나당 램프 수 (3개)
        for (int i = 0; i < sanityLamps.Length; i++)
        {
            int slot = i % perSign; // 0, 1, 2 (앞/뒤 표지판 모두 같은 순서)
            sanityLamps[i].sharedMaterial = (slot < sanity) ? lampOnMat : lampOffMat;
        }
    }

    // 표지판 글자를 현재 수심으로 바꿈
    void UpdateSigns()
    {
        int depth = Mathf.Max(targetRounds - correctStreak, 0);
        string text = depth > 0 ? "DEPTH " + depth + "m" : "SURFACE";
        foreach (TextMesh sign in depthSigns) sign.text = text;
    }

    // [신규] 마네킹에 부딪쳤을 때 호출됨: 정신력을 하나 깎고 잠깐 암전
    public void OnPlayerHit(GameObject attacker)
    {
        if (gameOver || busy) return;
        StartCoroutine(HitRoutine(attacker));
    }

    IEnumerator HitRoutine(GameObject attacker)
    {
        busy = true;                 // 부딪친 동안은 복도 끝 판정이 겹치지 않게 막음
        SetPlayerControl(false);

        sanity--;                    // 정신력 하나 감소
        UpdateVignette();            // 화면 가장자리가 어두워짐
        UpdateLamps();               // 램프가 하나 꺼짐
        Debug.Log("마네킹에 부딪쳤다! 정신력 " + sanity + "/" + maxSanity);

        yield return fader.FadeOut();                        // 화면이 검게
        if (attacker != null) attacker.SetActive(false);    // 어두운 사이에 마네킹이 사라짐

        if (sanity <= 0)
        {
            gameOver = true;
            Debug.Log("실패... 익사했다");
            yield return ShowResult(false);
            yield break;
        }

        yield return new WaitForSeconds(0.3f);
        yield return fader.FadeIn();                         // 다시 밝아짐
        SetPlayerControl(true);
        busy = false;
    }

    // 전환 중에는 플레이어 이동/시점을 막음
    void SetPlayerControl(bool on)
    {
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = on;
    }
}