using UnityEngine;

// 수심 1m 가짜 엔딩(Story 005)에서 복도 중앙 마네킹에 붙는 스크립트.
// MannequinTracker와 똑같은 회전 패턴(Slerp으로 플레이어 쪽을 계속 바라봄)을 그대로 재사용한다.
// "정적 구간"은 "그만 쳐다본다"는 뜻이 아니라 "아직 돌진하지 않는다"는 뜻이므로, 추적 자체는
// watchDuration이 지나도 멈추지 않고 계속된다 — StaticPauseComplete는 "이제 돌진해도 되는
// 시점"을 알리는 신호일 뿐이다. 돌진이 시작된 뒤 실제로 쫓아가며 도는 동작은 Story 006이
// MannequinRunner의 기존 패턴으로 이어받는다(그쪽도 매 프레임 플레이어 방향을 다시 계산한다).
public class MannequinWatcher : MonoBehaviour
{
    public Transform player;           // 바라볼 대상 (AnomalyManager가 넣어 줌)
    public float turnSpeed = 1.5f;     // 돌아보는 속도 (MannequinTracker보다 느리고 음산하게)
    public float watchDuration = 2.5f; // 바라보기 시작부터 "이제 돌진해도 되는 시점"까지 걸리는 시간(초)

    // 정적 구간 타이머가 끝났는가 — 추적 자체는 멈추지 않는다. AnomalyManager가 그대로
    // 외부에 통과시키고, GameManager가 매 프레임 확인한다.
    public bool StaticPauseComplete { get; private set; }

    bool watching;   // 지금 플레이어를 바라보는 중인가 (계속 true로 유지됨)
    float timer;     // 정적 구간 신호까지 남은 시간

    void Update()
    {
        if (!watching || player == null) return;

        // 플레이어까지의 방향 (높이는 무시하고 좌우로만 — MannequinTracker와 동일한 방식)
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(toPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * turnSpeed);
        }

        if (!StaticPauseComplete)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) StaticPauseComplete = true;
        }
    }

    // 가짜 엔딩 시작: 지금 자세(평소 "쉬는 자세"는 AnomalyManager가 미리 어긋나게 돌려 둔 상태 —
    // 그래야 여기서부터 플레이어 쪽으로 "돌아오는" 움직임이 항상 눈에 보인다)에서 바라보기 시작.
    // 이후 계속 플레이어를 따라 돈다 — Story 006이 돌진을 시작해도 이 컴포넌트를 끄지 않는 한 유지된다.
    public void BeginWatching()
    {
        StaticPauseComplete = false;
        timer = watchDuration;
        watching = true;
    }
}
