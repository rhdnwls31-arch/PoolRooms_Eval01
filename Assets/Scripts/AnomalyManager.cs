using UnityEngine;

// 라운드마다 이상현상을 켜고 끄는 스크립트
public class AnomalyManager : MonoBehaviour
{
    [Header("마네킹")]
    public MannequinTracker mannequinA; // 왼쪽 벽 마네킹
    public MannequinTracker mannequinB; // 오른쪽 벽 마네킹
    public GameObject extraMannequin;   // 평소엔 꺼져 있다가 수심 1m 가짜 엔딩에서 복도 중앙에 서는 마네킹 (MannequinRunner + MannequinWatcher + [신규] MannequinChargeTrigger가 붙어 있음 — 돌진은 애니메이션 없이 MannequinChargeTrigger가 단독으로 처리)
    public Transform player;            // 플레이어 (바라볼 대상)

    [Header("조명 / 소품 / 물")]
    public Transform corridor; // Corridor 오브젝트 (아래의 천장 조명을 자동으로 찾음)
    public Transform flamingo; // 플라밍고
    public Transform radio;    // 라디오
    public Transform water;    // 물 바닥

    [Header("물 차오르기")]
    public float waterStartY = 0.02f;   // 물의 시작 높이
    public float waterMaxY = 0.7f;      // 물의 최대 높이
    public float waterSpeed = 0.06f;    // 초당 올라가는 높이(m)
    public float waterHiddenY = -1f;    // 평소에 숨겨 두는 높이 (바닥 아래)

    [Header("가짜 엔딩 (수심 1m)")]
    public float fakeEndingTurnSpeed = 1.5f;           // 마네킹이 플레이어를 돌아보는 속도 (일반 추적보다 느리고 음산하게)
    public float fakeEndingRestOffsetAngle = 70f;      // "쉬는 자세" 자체를 플레이어 정면에서 이만큼 어긋나게 둔다 —
                                                        // 복도 중앙이라 플레이어와 일직선이 되는 경우가 있어, 어긋나게
                                                        // 안 해두면 Slerp이 할 일이 없어 회전이 전혀 안 보일 수 있다
    public float fakeEndingWatchDuration = 2.5f;       // [임시값] 바라보기 시작부터 정적 구간 끝까지 전체 시간(초) — 수치 미정, GAME_DESIGN.md 확인 필요

    const int TypeCount = 8; // 이상현상 종류 수 (case 번호 유지를 위해 그대로 둠)
    // 뽑기 대상 종류만 배열로 명시 (case 1: 달려오는 마네킹은 Story 004에서 제외)
    static readonly int[] RandomTypes = { 0, 2, 3, 4, 5, 6, 7 };
    int lastType = -1;       // 직전 라운드의 종류 (연속 방지용)

    Light[] ceilingLights;   // 복도의 천장 조명들
    Color[] lightColors;     // 조명의 원래 색
    float[] lightIntensities;// 조명의 원래 밝기
    int flickerIndex = -1;   // 깜빡이는 조명 번호 (-1이면 없음)
    float flickerTimer;      // 다음 깜빡임까지 남은 시간

    bool waterRising;        // 물이 차오르는 중인가

    MannequinRunner runner;   // 달려오는 마네킹 스크립트
    MannequinWatcher watcher; // [신규] 가짜 엔딩에서 플레이어를 돌아보는 마네킹 스크립트 (extraMannequin에 런타임으로 붙임)
    MannequinChargeTrigger chargeTrigger; // [신규] 가짜 엔딩의 돌진 트리거 (Story 006, extraMannequin에 런타임으로 붙임)

    Vector3 flamingoPos, radioPos;    // 소품의 원래 위치
    Quaternion flamingoRot, radioRot; // 소품의 원래 회전

    void Awake()
    {
        // 마네킹: 플레이어를 알려 줌
        mannequinA.player = player;
        mannequinB.player = player;

        runner = extraMannequin.GetComponent<MannequinRunner>();
        runner.player = player;

        // [신규] 복도 중앙이라 플레이어 스폰 지점과 정확히 일직선에 있다 — "쉬는 자세"를 그대로 두면
        // 가짜 엔딩이 시작돼도 이미 플레이어를 정면으로 보고 있는 셈이라 돌아보는 움직임이 안 보인다.
        // Init()이 "원래 자세"로 기억하기 전에 미리 어긋나게 돌려 둬서, 항상 눈에 보이는 회전이 있게 한다.
        extraMannequin.transform.rotation *= Quaternion.Euler(0f, fakeEndingRestOffsetAngle, 0f);
        runner.Init();                 // 꺼지기 전에 원래 위치를 기억시킴 (이 어긋난 자세가 "원래 자세"가 됨)
        extraMannequin.SetActive(false);

        // extraMannequin은 이제 수심 1m 가짜 엔딩의 "복도 중앙에 서 있는 마네킹"이기도 하다.
        // 평소 라운드에서는 기존처럼 꺼 둔다 — 가짜 엔딩 라운드가 시작될 때(화면이 검을 때)
        // ShowFakeEndingMannequin()에서 미리 켜서, 화면이 밝아졌을 땐 이미 서 있는 것처럼 보이게 한다.
        watcher = extraMannequin.AddComponent<MannequinWatcher>();
        watcher.player = player;
        watcher.turnSpeed = fakeEndingTurnSpeed;
        watcher.watchDuration = fakeEndingWatchDuration;

        // [신규] 가짜 엔딩의 돌진 트리거 (Story 006) — 바라보기 다음 단계. 애니메이션/MannequinRunner
        // 없이 독자적으로 움직이므로 player만 필요하다.
        chargeTrigger = extraMannequin.AddComponent<MannequinChargeTrigger>();
        chargeTrigger.player = player;

        // [신규] 가짜 엔딩은 완전히 정적인 자세여야 한다 — Animator를 꺼서 기본 상태(Idle)의
        // 미세한 움직임조차 재생되지 않게 한다 (다른 마네킹처럼 애니메이션이 전혀 없어야 함)
        Animator extraAnimator = extraMannequin.GetComponent<Animator>();
        if (extraAnimator != null) extraAnimator.enabled = false;

        // 천장 조명을 모두 찾아서 원래 색/밝기를 기억
        ceilingLights = corridor.GetComponentsInChildren<Light>();
        lightColors = new Color[ceilingLights.Length];
        lightIntensities = new float[ceilingLights.Length];
        for (int i = 0; i < ceilingLights.Length; i++)
        {
            lightColors[i] = ceilingLights[i].color;
            lightIntensities[i] = ceilingLights[i].intensity;
        }

        // 소품의 원래 위치/회전을 기억
        flamingoPos = flamingo.position; flamingoRot = flamingo.rotation;
        radioPos = radio.position; radioRot = radio.rotation;

        // 물은 끄지 않고 바닥 아래에 숨겨 둔다
        // (꺼 두면 처음 켤 때 쉐이더를 준비하느라 화면이 끊김 → 미리 그려 두면 끊김이 없음)
        water.gameObject.SetActive(true);
        SetWaterY(waterHiddenY);
    }

    void SetWaterY(float y)
    {
        Vector3 p = water.position;
        p.y = y;
        water.position = p;
    }

    void Update()
    {
        // 물이 차오르는 중이면 매 프레임 조금씩 올림
        if (waterRising)
        {
            float y = Mathf.MoveTowards(water.position.y, waterMaxY, waterSpeed * Time.deltaTime);
            SetWaterY(y);
        }

        // 조명 하나가 깜빡이는 중이면 불규칙하게 켰다 껐다 함
        if (flickerIndex >= 0)
        {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                flickerTimer = Random.Range(0.05f, 0.35f);
                bool on = Random.value < 0.5f;
                ceilingLights[flickerIndex].intensity = on ? lightIntensities[flickerIndex] : 0f;
            }
        }
    }

    // 모든 이상현상을 끄고 원래대로 되돌림
    void ResetAll()
    {
        mannequinA.ResetState();
        mannequinB.ResetState();
        runner.ResetState();            // 달리던 마네킹을 멈추고 제자리로
        extraMannequin.SetActive(false); // 평소 라운드에서는 다시 숨김 (가짜 엔딩 때만 StartFakeEndingLook()이 켬)

        for (int i = 0; i < ceilingLights.Length; i++)
        {
            ceilingLights[i].color = lightColors[i];
            ceilingLights[i].intensity = lightIntensities[i];
        }
        flickerIndex = -1;

        flamingo.position = flamingoPos; flamingo.rotation = flamingoRot;
        radio.position = radioPos; radio.rotation = radioRot;

        waterRising = false;
        SetWaterY(waterHiddenY);        // 물을 다시 바닥 아래로 숨김
    }

    // hasAnomaly가 true면 이상현상 하나를 켜고, false면 모두 원래대로 둔다
    public void ApplyRound(bool hasAnomaly)
    {
        ResetAll(); // 먼저 전부 원래 상태로
        if (!hasAnomaly) return;

        // 직전 라운드와 다른 종류가 나올 때까지 다시 뽑음 (뽑기 대상은 RandomTypes로 제한)
        int pick;
        do { pick = RandomTypes[Random.Range(0, RandomTypes.Length)]; } while (pick == lastType);
        lastType = pick;

        // 왼쪽(A)/오른쪽(B) 중 누구에게 시킬지 무작위로 고름
        bool useLeft = Random.value < 0.5f;
        MannequinTracker target = useLeft ? mannequinA : mannequinB;
        string side = useLeft ? "왼쪽" : "오른쪽";

        switch (pick)
        {
            case 0:
                target.StartBodyTrack();
                Debug.Log("이상현상: " + side + " 마네킹이 나를 쳐다본다");
                break;
            case 1:
                extraMannequin.SetActive(true);
                runner.StartRun();      // 잠시 뒤 플레이어를 향해 달려옴
                Debug.Log("이상현상: 마네킹이 달려온다");
                break;
            case 2:
                target.StartHeadTrack();
                Debug.Log("이상현상: " + side + " 마네킹 머리가 돌아가 있다");
                break;
            case 3:
                // 모든 조명을 붉게
                foreach (Light l in ceilingLights) l.color = new Color(1f, 0.15f, 0.1f);
                Debug.Log("이상현상: 조명이 붉게 변했다");
                break;
            case 4:
                // 조명 하나를 골라 깜빡이게 (Update에서 처리)
                flickerIndex = Random.Range(0, ceilingLights.Length);
                flickerTimer = 0f;
                Debug.Log("이상현상: 조명 하나가 깜빡인다");
                break;
            case 5:
                // 플라밍고를 복도 한가운데로 (X=270은 모델 가져올 때 생긴 기본 회전이라 유지)
                flamingo.position = new Vector3(0f, flamingoPos.y, -7f);
                flamingo.rotation = Quaternion.Euler(270f, 160f, 0f);
                Debug.Log("이상현상: 플라밍고가 복도 가운데로 옮겨졌다");
                break;
            case 6:
                radio.position = new Vector3(0.3f, radioPos.y, -9f);
                radio.rotation = Quaternion.Euler(270f, 20f, 0f);
                Debug.Log("이상현상: 라디오가 복도 가운데로 옮겨졌다");
                break;
            case 7:
                SetWaterY(waterStartY); // 바닥 아래에서 시작 높이로 올림
                waterRising = true;     // Update에서 서서히 올라감
                Debug.Log("이상현상: 물이 차오른다");
                break;
        }
    }

    // [신규] 수심 1m 가짜 엔딩 1단계: 마네킹을 켠다 (화면이 검을 때 호출 — 화면이 밝아지면
    // "이미 서 있던 것"처럼 보이고, 아직 돌아보기는 시작하지 않은 평소 자세 그대로다)
    public void ShowFakeEndingMannequin()
    {
        extraMannequin.SetActive(true);
    }

    // [신규] 수심 1m 가짜 엔딩 2단계: 플레이어를 돌아보기 시작한다 (GameManager가 화면이 다
    // 밝아진 "뒤에" 호출 — 돌아보는 움직임 전체가 플레이어 눈에 보이도록 하기 위함)
    public void StartFakeEndingLook()
    {
        watcher.BeginWatching();
    }

    // [신규] 돌아보기 뒤의 정적 구간이 끝났는가 (GameManager가 매 프레임 폴링)
    public bool FakeEndingStaticPauseComplete => watcher.StaticPauseComplete;

    // [신규] 수심 1m 가짜 엔딩 3단계: 정적 구간이 끝난 뒤 돌진을 시작한다 (GameManager가 호출).
    // watcher는 여기서 끈다 — watcher의 느린 Slerp 회전이 돌진 중에도 계속 끼어들면 "천천히 돌아보다가
    // 다가오는" 것처럼 보여 돌진이 굼떠 보인다. chargeTrigger가 시작 즉시 플레이어 쪽으로 방향을
    // 스냅시키므로, 더는 watcher가 필요 없다.
    public void StartFakeEndingCharge()
    {
        watcher.enabled = false;
        chargeTrigger.StartCharge();
    }
}
