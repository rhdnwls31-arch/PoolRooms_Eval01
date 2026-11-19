using UnityEngine;

/// <summary>
/// 노란 방 키다리 삐에로의 풍선. 이 오브젝트(손 위치에 둔 축) 아래에 풍선 메쉬가 붙는다.
/// 평소: 바람도 없는데 출구 쪽으로 기운 채 천천히 흔들린다.
/// releaseSignal이 켜지면(GazeTarget.showOnFound) 손을 떠나 천장까지 오른 뒤 출구 벽 위로 둥둥 흘러가 그 자리에 머문다.
/// 출구는 GazeTarget.notifyDelay로 풍선이 도착할 즈음 열린다.
/// </summary>
public class BalloonFlight : MonoBehaviour
{
    [Tooltip("켜지면 날아가기 시작한다. 처음에는 꺼 둔 빈 오브젝트(GazeTarget.showOnFound에 넣는다).")]
    public GameObject releaseSignal;
    [Tooltip("평소 출구 쪽으로 기운 각도(도). 로컬 X축 회전 = 삐에로 앞(출구) 쪽으로 기운다.")]
    public float idleLean = 25f;
    [Tooltip("평소 흔들림 크기(도)와 주기(초).")]
    public float idleSway = 4f, idleSwayPeriod = 3.5f;

    [Header("날아가기")]
    [Tooltip("손을 떠나 오를 높이(이 축의 월드 y). 풍선 끝이 천장에 닿기 직전.")]
    public float riseToY = 2.1f;
    [Tooltip("멈출 곳(월드). 출구 벽 바로 앞 위쪽.")]
    public Vector3 stopAt;
    public float riseTime = 1.2f, driftTime = 4.5f;
    [Tooltip("도착 뒤 위아래로 떠 있는 크기(m)와 주기(초).")]
    public float hoverBob = 0.04f, hoverPeriod = 2.6f;

    Quaternion baseLocal;
    Vector3 startPos, risePos;
    float flyStart = -1f;

    void Awake() => baseLocal = transform.localRotation;

    // 기울지 않은 풍선 = 삐에로(부모)와 같은 방향
    Quaternion Upright => transform.parent != null ? transform.parent.rotation : Quaternion.identity;

    void Update()
    {
        if (flyStart < 0f)
        {
            if (releaseSignal != null && releaseSignal.activeInHierarchy) { Release(); return; }
            float sway = Mathf.Sin(Time.time * 2f * Mathf.PI / idleSwayPeriod) * idleSway;
            transform.localRotation = baseLocal * Quaternion.Euler(idleLean + sway * 0.4f, 0f, sway);
            return;
        }

        float t = Time.time - flyStart;
        if (t < riseTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / riseTime);
            transform.position = Vector3.Lerp(startPos, risePos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Upright, Time.deltaTime * 2f);
        }
        else if (t < riseTime + driftTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, (t - riseTime) / driftTime);
            var p = Vector3.Lerp(risePos, stopAt, k);
            p.y += Mathf.Sin(k * Mathf.PI * 3f) * 0.05f; // 흘러가며 살짝 오르내린다
            var dir = stopAt - risePos; dir.y = 0f;
            transform.position = p;
            // 가는 쪽으로 조금 끌려가듯 기운다(끈이 뒤로 처진다)
            var lean = dir.sqrMagnitude > 0.001f ? Quaternion.AngleAxis(12f * Mathf.Sin(k * Mathf.PI), Vector3.Cross(Vector3.up, dir.normalized)) : Quaternion.identity;
            transform.rotation = Quaternion.Slerp(transform.rotation, lean * Upright, Time.deltaTime * 3f);
        }
        else
        {
            float h = Mathf.Sin((t - riseTime - driftTime) * 2f * Mathf.PI / hoverPeriod) * hoverBob;
            transform.position = stopAt + Vector3.up * h;
            transform.rotation = Quaternion.Slerp(transform.rotation, Upright, Time.deltaTime * 1.5f);
        }
    }

    /// <summary>손을 놓고 날아가기 시작한다.</summary>
    public void Release()
    {
        if (flyStart >= 0f) return;
        flyStart = Time.time;
        startPos = transform.position;
        risePos = new Vector3(startPos.x, riseToY, startPos.z);
    }
}
