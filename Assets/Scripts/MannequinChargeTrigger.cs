using UnityEngine;

// 수심 1m 가짜 엔딩(Story 006)에서 복도 중앙 마네킹이 플레이어에게 거의 순간적으로 닿는 돌진.
// 달리기/Idle 애니메이션을 넣어 봤지만 기이한 느낌에 안 맞아서(2026-11-09 설계 변경) 뺐다 —
// 지금 서 있는 자세 그대로, 아주 빠르게 플레이어의 현재 위치를 향해 다가갈 뿐이다.
// 회전(플레이어 쪽으로 돌아보기)은 Story 005의 MannequinWatcher가 계속 맡는다 — 돌진 중에도
// 끄지 않아서, 다가가는 동안에도 계속 플레이어 쪽으로 고개가 돌아가 있는 것처럼 보인다.
public class MannequinChargeTrigger : MonoBehaviour
{
    public Transform player;          // 다가갈 대상 (AnomalyManager가 넣어 줌)
    public float chargeDuration = 1f; // 시작 거리를 이 시간(초) 안에 주파하도록 속도를 역산한다
    public float hitDistance = 0.9f;  // 이 거리 안으로 들어오면 접촉으로 판정
    public float minSpeed = 15f;      // 시작 거리가 아주 가까워도 "느릿느릿 다가옴"처럼 보이지 않게 두는 최소 속도

    bool charging;
    float speed;

    // 돌진 시작: Story 005의 정적 구간이 끝난 뒤 AnomalyManager.StartFakeEndingCharge()가 호출.
    // 시작 시점의 거리로 속도를 역산해 두므로, 플레이어가 가만히 있어도 chargeDuration 안에 닿는다.
    // 방향은 즉시 스냅(Slerp 없음) — Story 005의 느린 돌아보기가 돌진까지 이어지면 굼떠 보인다.
    public void StartCharge()
    {
        Vector3 toPlayer = Flat(player.position - transform.position);
        if (toPlayer.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(toPlayer);

        speed = Mathf.Max(toPlayer.magnitude / chargeDuration, minSpeed);
        charging = true;
    }

    void Update()
    {
        if (!charging || player == null) return;

        Vector3 toPlayer = Flat(player.position - transform.position);
        float dist = toPlayer.magnitude;

        if (dist <= hitDistance)
        {
            charging = false;
            if (GameManager.Instance != null) GameManager.Instance.OnFakeEndingContact();
            return;
        }

        transform.position += (toPlayer / dist) * speed * Time.deltaTime; // 플레이어의 지금 위치를 향해 계속 다가감
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
