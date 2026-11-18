using UnityEngine;

/// <summary>
/// 떠 있는 물체를 천천히 위아래로 흔들고 살짝 기울인다. orbitRadius가 0보다 크면 처음 자리 둘레를 돈다(종이비행기).
/// 컴퓨터 속 맵(기획서 §6-3e)의 떠 있는 창·아이콘·종이비행기에 쓴다.
/// </summary>
public class FloatDrift : MonoBehaviour
{
    [Tooltip("위아래로 흔들리는 높이(미터).")]
    public float bobHeight = 0.3f;
    [Tooltip("위아래 한 번 오가는 시간(초).")]
    public float bobPeriod = 6f;
    [Tooltip("좌우로 기우는 각도(도).")]
    public float swayDegrees = 3f;
    [Tooltip("0보다 크면 처음 자리를 중심으로 이 반지름으로 돈다. 앞(+Z)이 도는 방향을 향한다.")]
    public float orbitRadius = 0f;
    [Tooltip("한 바퀴 도는 시간(초).")]
    public float orbitPeriod = 30f;
    [Tooltip("여러 물체가 같이 움직이지 않게 시간을 어긋나게 한다(초).")]
    public float phase;

    Vector3 basePos;
    Quaternion baseRot;

    void Start()
    {
        basePos = transform.position;
        baseRot = transform.rotation;
    }

    void Update()
    {
        float t = Time.time + phase;
        Vector3 p = basePos + Vector3.up * Mathf.Sin(t * Mathf.PI * 2f / bobPeriod) * bobHeight;
        Quaternion r = baseRot;
        if (orbitRadius > 0f)
        {
            float a = t * Mathf.PI * 2f / orbitPeriod;
            p += new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * orbitRadius;
            r = Quaternion.Euler(0f, a * Mathf.Rad2Deg + 90f, 0f) * baseRot;
        }
        r *= Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f / (bobPeriod * 1.3f)) * swayDegrees);
        transform.SetPositionAndRotation(p, r);
    }
}
