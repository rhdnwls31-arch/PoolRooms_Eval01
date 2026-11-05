using UnityEngine;

// 추가 마네킹에 붙이는 스크립트: 플레이어를 향해 달려와서, 닿으면 정신력을 하나 깎는다
// 마네킹에 Animator가 있고, 상태 이름이 "Idle" / "Run" 이어야 한다
public class MannequinRunner : MonoBehaviour
{
    public Transform player;           // 달려갈 대상 (AnomalyManager가 자동으로 넣어 줌)
    public float runSpeed = 4.5f;      // 달리는 속도 (m/초)
    public float startDelay = 1.2f;    // 이상현상이 켜진 뒤 달리기 시작까지 기다리는 시간(초)
    public float hitDistance = 0.9f;   // 플레이어와 이 거리 안으로 들어오면 부딪친 것으로 판정
    public float turnSpeed = 10f;      // 플레이어 쪽으로 몸을 돌리는 빠르기
    public float runYawOffset = -85f;  // 달리기 모션이 옆을 보고 있어서 보정하는 각도 (옆으로 달리면 이 값을 조절)

    Animator animator;     // 애니메이션 재생용
    Vector3 startPos;      // 원래 위치
    Quaternion startRot;   // 원래 방향
    bool inited;           // 원래 위치를 기억했는가
    bool running;          // 이상현상 진행 중인가
    bool isMoving;         // 지금 달리는 중인가
    float waitTimer;       // 시작 대기 시간

    // 원래 위치/방향을 기억 (AnomalyManager가 처음에 한 번 호출)
    public void Init()
    {
        if (inited) return;
        startPos = transform.position;
        startRot = transform.rotation;
        animator = GetComponent<Animator>();
        inited = true;
    }

    // 이상현상 시작: 처음엔 가만히 서 있다가 대기 시간이 지나면 달리기 시작
    public void StartRun()
    {
        Init();
        running = true;
        isMoving = false;
        waitTimer = startDelay;
        PlayAnim("Idle");
    }

    // 멈추고 원래 자리로 되돌림
    public void ResetState()
    {
        Init();
        running = false;
        isMoving = false;
        transform.position = startPos;
        transform.rotation = startRot;
        PlayAnim("Idle");
    }

    // 애니메이션 상태를 바꿈 (이미 같은 상태면 다시 시작하지 않음)
    void PlayAnim(string stateName)
    {
        if (animator == null || !animator.gameObject.activeInHierarchy) return;
        if (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)) return;
        animator.CrossFade(stateName, 0.15f);
    }

    void Update()
    {
        if (!running || player == null) return;

        // 플레이어 쪽 방향 (높이는 무시하고 평면에서만)
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (dist < 0.01f) return;
        Vector3 dir = toPlayer / dist;

        // 출발 전에는 가만히 서서 플레이어를 쳐다봄
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
        }
        else
        {
            // 플레이어에게 닿았다 → 정신력 하나 깎고 멈춤
            if (dist <= hitDistance)
            {
                running = false;
                PlayAnim("Idle");
                if (GameManager.Instance != null) GameManager.Instance.OnPlayerHit(gameObject);
                return;
            }

            if (!isMoving) { isMoving = true; PlayAnim("Run"); }
            transform.position += dir * runSpeed * Time.deltaTime;   // 달려옴
        }

        // 몸 방향: 달릴 때는 모션이 옆을 보고 있으므로 보정 각도를 더해 줌
        float yaw = isMoving ? runYawOffset : 0f;
        Quaternion target = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * turnSpeed);
    }
}
