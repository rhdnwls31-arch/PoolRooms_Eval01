using UnityEngine;

// 마네킹에 붙이는 스크립트: 플레이어를 몸 전체로, 또는 머리만으로 따라 보게 한다
public class MannequinTracker : MonoBehaviour
{
    public Transform player;      // 바라볼 대상 (AnomalyManager가 자동으로 넣어 줌)
    public float turnSpeed = 3f;  // 돌아가는 속도 (클수록 빠름)

    Quaternion normalRotation;     // 몸의 원래 방향
    Transform head;                // 머리 뼈
    Quaternion headNormalRotation; // 머리의 원래 방향

    bool trackBody; // true면 몸 전체가 플레이어를 따라 봄
    bool trackHead; // true면 머리만 플레이어를 따라 봄

    void Awake()
    {
        // 원래 방향을 기억해 둠
        normalRotation = transform.rotation;

        // 마네킹 안에서 이름이 "Head"인 뼈를 찾아 원래 방향을 기억
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t.name == "Head") { head = t; break; }
        }
        if (head != null) headNormalRotation = head.rotation;
    }

    void Update()
    {
        if (player == null) return;

        // 플레이어까지의 방향 (높이는 무시하고 좌우로만)
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f) return;

        if (trackBody)
        {
            // 몸 전체를 플레이어 쪽으로 부드럽게 돌림
            Quaternion target = Quaternion.LookRotation(toPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * turnSpeed);
        }

        if (trackHead && head != null)
        {
            // 몸의 앞방향에서 플레이어 방향까지 몇 도인지 계산 (너무 꺾이지 않게 제한)
            float angle = Vector3.SignedAngle(transform.forward, toPlayer, Vector3.up);
            angle = Mathf.Clamp(angle, -120f, 120f);

            // 머리만 위쪽(Y축) 기준으로 부드럽게 돌림
            Quaternion target = Quaternion.AngleAxis(angle, Vector3.up) * headNormalRotation;
            head.rotation = Quaternion.Slerp(head.rotation, target, Time.deltaTime * turnSpeed);
        }
    }

    // 모든 움직임을 멈추고 원래 자세로 되돌림
    public void ResetState()
    {
        trackBody = false;
        trackHead = false;
        transform.rotation = normalRotation;
        if (head != null) head.rotation = headNormalRotation;
    }

    // 몸 전체로 플레이어를 따라 보기 시작
    public void StartBodyTrack() { trackBody = true; }

    // 머리만으로 플레이어를 따라 보기 시작
    public void StartHeadTrack() { trackHead = true; }
}