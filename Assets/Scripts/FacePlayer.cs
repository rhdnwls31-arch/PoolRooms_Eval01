using UnityEngine;

/// <summary>
/// 오브젝트의 앞(+Z)이 플레이어 카메라를 천천히 향하게 돌리고, 제자리에서 살짝 떠오르내리게 한다.
/// 천사 방의 날개 달린 눈(더미)이 "모두 나를 보고 있다"는 느낌을 내는 데 쓴다. 게임 규칙에는 관여하지 않는다.
/// </summary>
public class FacePlayer : MonoBehaviour
{
    [Tooltip("초당 최대 회전 각도. 작을수록 느릿하게 따라온다.")]
    public float turnSpeed = 25f;
    [Tooltip("위아래로 떠다니는 폭(미터). 0이면 움직이지 않는다.")]
    public float bobAmplitude = 0.12f;
    [Tooltip("떠다니는 한 주기(초).")]
    public float bobPeriod = 5f;

    Transform target;
    Vector3 basePosition;
    float phase;

    void Awake()
    {
        basePosition = transform.position;
        phase = Mathf.Repeat(basePosition.x * 0.37f + basePosition.z * 0.11f, 1f) * Mathf.PI * 2f; // 다 같이 출렁이지 않게
    }

    void Update()
    {
        if (target == null && Camera.main != null) target = Camera.main.transform;
        if (bobPeriod > 0f)
            transform.position = basePosition + Vector3.up * (bobAmplitude * Mathf.Sin(Time.time * Mathf.PI * 2f / bobPeriod + phase));
        if (target == null) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, LookRotationTo(target.position), turnSpeed * Time.deltaTime);
    }

    /// <summary>현재 위치에서 point를 바라보는 회전. 너무 가까우면 지금 회전을 그대로 돌려준다.</summary>
    public Quaternion LookRotationTo(Vector3 point)
    {
        Vector3 d = point - transform.position;
        return d.sqrMagnitude < 1e-4f ? transform.rotation : Quaternion.LookRotation(d, Vector3.up);
    }
}
