using UnityEngine;

/// <summary>
/// 물에 뜬 공·튜브가 동동 떠 있게 한다(2026-11-24 사용자 요청). 위아래로 살짝 오르내리고, 천천히 기울며, 제자리 둘레를 아주 조금 맴돈다.
/// 움직임을 매 프레임 "지난번에 더한 만큼 빼고 새로 더하는" 식으로 얹기 때문에, 다른 스크립트(숨은 엔딩 등)가 옮겨도 그 자리에서 계속 떠 있다.
/// 물체마다 시작 시간을 위치로 어긋나게 해 같이 움직이지 않는다. 그림자·콜라이더는 건드리지 않는다.
/// </summary>
public class WaterBob : MonoBehaviour
{
    [Tooltip("위아래로 오르내리는 높이(m, 한쪽).")]
    public float bobHeight = 0.04f;
    [Tooltip("한 번 오르내리는 시간(초).")]
    public float bobPeriod = 3.2f;
    [Tooltip("기우는 각도(도).")]
    public float tiltDegrees = 3f;
    [Tooltip("제자리 둘레를 맴도는 반지름(m).")]
    public float driftRadius = 0.12f;
    [Tooltip("한 바퀴 맴도는 시간(초).")]
    public float driftPeriod = 23f;
    [Tooltip("천천히 도는 속도(도/초). 0이면 돌지 않는다.")]
    public float spinDegreesPerSecond = 2f;

    Vector3 lastOffset;
    Quaternion lastTilt = Quaternion.identity;
    float phase;

    void Start()
    {
        var p = transform.position;
        phase = Mathf.Repeat(p.x * 1.7f + p.z * 2.3f, 100f); // 위치로 어긋나게(결정적)
    }

    void Update()
    {
        float t = Time.time + phase;
        float w = Mathf.PI * 2f;
        var offset = new Vector3(Mathf.Sin(t * w / driftPeriod) * driftRadius,
                                 Mathf.Sin(t * w / bobPeriod) * bobHeight,
                                 Mathf.Cos(t * w / driftPeriod * 0.83f) * driftRadius);
        var tilt = Quaternion.Euler(Mathf.Sin(t * w / (bobPeriod * 1.37f)) * tiltDegrees, 0f, Mathf.Cos(t * w / (bobPeriod * 1.11f)) * tiltDegrees);

        // 지난번 것을 빼고 새로 얹는다(다른 스크립트가 옮긴 것은 남는다)
        transform.position += offset - lastOffset;
        var baseRot = transform.rotation * Quaternion.Inverse(lastTilt);
        baseRot = Quaternion.Euler(0f, spinDegreesPerSecond * Time.deltaTime, 0f) * baseRot;
        transform.rotation = baseRot * tilt;
        lastOffset = offset; lastTilt = tilt;
    }
}
