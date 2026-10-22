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

        StartCoroutine(FirstRound());
    }

    // 게임 시작: 검은 화면에서 라운드를 준비하고 서서히 밝아짐
    IEnumerator FirstRound()
    {
        busy = true;
        SetPlayerControl(false);
        fader.SetBlack();
        StartRound();
        yield return new WaitForSeconds(firstDelay);
        yield return fader.FadeIn();
        SetPlayerControl(true);
        busy = false;
    }

    // 새 라운드 시작: 플레이어를 시작점으로 되돌리고, 이상현상을 적용
    void StartRound()
    {
        roundCount++;

        if (firstRoundNormal && roundCount == 1)
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

        // CharacterController는 끄고 옮겨야 위치가 정확히 바뀜
        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.position = startPos;
        player.rotation = startRot;
        cc.enabled = true;

        // 플레이어를 옮긴 "다음에" 이상현상을 적용 (순서가 중요)
        anomalyManager.ApplyRound(hasAnomaly);
        UpdateSigns();
    }

    // 플레이어가 복도 끝에 도착하면 호출됨
    public void OnPlayerReachedEnd(bool isFrontEnd)
    {
        if (gameOver || busy) return;

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

    // 남은 정신력에 맞춰 비네팅 세기를 바꿈
    void UpdateVignette()
    {
        if (vignette == null) return;
        vignette.intensity.value = vignetteBase + (maxSanity - sanity) * vignettePerHit;
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

    // 전환 중에는 플레이어 이동/시점을 막음
    void SetPlayerControl(bool on)
    {
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = on;
    }
}