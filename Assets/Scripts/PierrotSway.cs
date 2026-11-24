using UnityEngine;

/// <summary>
/// 노란 방 키다리 삐에로가 발을 축으로 천천히 좌우로 흔들흔들한다(2026-11-24 멘토 피드백). 몸 메쉬(이 오브젝트)에 붙인다.
/// 풍선 축(follower)도 손 자리에 맞춰 같이 옮겨 끈이 손을 떠나지 않게 한다. stopSignal이 켜지면(풍선이 날아감) 풍선 축은 놓아준다.
/// 게임 규칙에는 관여하지 않는다.
/// </summary>
public class PierrotSway : MonoBehaviour
{
    [Tooltip("좌우로 기우는 최대 각도(도).")]
    public float rollDegrees = 2.5f;
    [Tooltip("앞뒤로 기우는 최대 각도(도). 좌우의 절반 주기로 살짝.")]
    public float pitchDegrees = 0.8f;
    [Tooltip("좌우 한 번 왕복 주기(초).")]
    public float period = 4.2f;
    [Tooltip("손 위치에 있는 풍선 축. 몸과 같이 움직인다.")]
    public Transform follower;
    [Tooltip("켜지면 풍선 축을 더 이상 움직이지 않는다(BalloonFlight의 releaseSignal과 같은 것).")]
    public GameObject stopSignal;

    Quaternion baseRot;
    Vector3 basePos, followerBase;

    void Awake()
    {
        baseRot = transform.localRotation;
        basePos = transform.localPosition;
        if (follower != null) followerBase = follower.localPosition;
    }

    void Update()
    {
        float t = Time.time * 2f * Mathf.PI / period;
        // 좌우는 사인, 앞뒤는 두 배 빠르게 조금: 메트로놈처럼 끝에서 잠깐 멈칫하는 느낌
        var sway = Quaternion.Euler(Mathf.Sin(t * 2f) * pitchDegrees, 0f, Mathf.Sin(t) * rollDegrees);
        transform.localRotation = sway * baseRot;
        transform.localPosition = basePos;
        if (follower != null && (stopSignal == null || !stopSignal.activeInHierarchy))
            follower.localPosition = basePos + sway * (followerBase - basePos);
    }
}
