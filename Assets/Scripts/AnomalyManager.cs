using UnityEngine;

// 라운드마다 이상현상을 켜고 끄는 스크립트
public class AnomalyManager : MonoBehaviour
{
    [Header("마네킹")]
    public MannequinTracker mannequinA; // 왼쪽 벽 마네킹
    public MannequinTracker mannequinB; // 오른쪽 벽 마네킹
    public GameObject extraMannequin;   // 평소엔 꺼져 있는 마네킹 (MannequinRunner가 붙어 있음)
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

    MannequinRunner runner;  // 달려오는 마네킹 스크립트

    Vector3 flamingoPos, radioPos;    // 소품의 원래 위치
    Quaternion flamingoRot, radioRot; // 소품의 원래 회전

    void Awake()
    {
        // 마네킹: 플레이어를 알려 주고, 추가 마네킹은 끈 상태로 시작
        mannequinA.player = player;
        mannequinB.player = player;

        runner = extraMannequin.GetComponent<MannequinRunner>();
        runner.player = player;
        runner.Init();                 // 꺼지기 전에 원래 위치를 기억시킴
        extraMannequin.SetActive(false);

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
        extraMannequin.SetActive(false);

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
}
