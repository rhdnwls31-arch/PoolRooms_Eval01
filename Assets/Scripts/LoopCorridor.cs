using UnityEngine;

/// <summary>
/// 꽃무늬 아파트 복도(기획서 §6-3a 예정): 이상한 점을 찾기 전까지 아무리 걸어도 끝에 닿지 않는 복도.
/// 복도는 segment마다 똑같이 생겼다. 플레이어가 loopX를 넘으면 segment만큼 뒤로 옮겨 같은 자리를 다시 걷게 한다.
/// 끝(풍선과 마지막 문이 있는 벽, endGroup)은 늘 플레이어보다 endAhead 앞에 있어 가까워지지 않는다.
/// 찾으면(gaze.Found) 반복이 멈추고 끝이 그 자리에 선다. 끝을 따라 움직이는 출구 벽과 순간이동 범위도 옮겨 준다.
/// 화면이 끊겨 보이지 않게 이 복도의 Volume에서 모션 블러를 끈다(에디터 배치 쪽).
/// </summary>
public class LoopCorridor : MonoBehaviour
{
    [Tooltip("찾으면 반복이 멈추는 이상한 점.")]
    public GazeTarget gaze;
    [Tooltip("복도 범위. 플레이어가 이 안에 있을 때만 반복한다.")]
    public Bounds corridor;
    [Tooltip("복도가 뻗는 방향(월드, 수평).")]
    public Vector3 axis = Vector3.right;
    [Tooltip("복도 시작 지점(축 위 좌표의 기준).")]
    public Vector3 origin;
    [Tooltip("이 거리(시작에서 축 방향)를 넘으면 segment만큼 뒤로 옮긴다.")]
    public float loopAt = 40f;
    [Tooltip("복도가 되풀이되는 길이. 문·등·벽지 간격이 모두 이 길이로 나누어떨어져야 한다.")]
    public float segment = 20f;
    [Tooltip("끝 벽 묶음(풍선, 마지막 문). 플레이어 앞 endAhead에 둔다.")]
    public Transform endGroup;
    [Tooltip("찾기 전 끝 벽이 플레이어보다 앞서 있는 거리.")]
    public float endAhead = 35f;
    [Tooltip("끝을 따라 옮길 출구(마지막 문). 벽 범위는 endGroup 기준 로컬 값.")]
    public RevealWhenExplored exitGate;
    public Bounds exitGateLocal;
    [Tooltip("끝을 따라 옮길 순간이동(문 안의 빛). 범위는 endGroup 기준 로컬 값.")]
    public TeleportZone exitZone;
    public Bounds exitZoneLocal;

    /// <summary>뒤로 옮긴 횟수(확인용).</summary>
    public int Loops { get; private set; }
    /// <summary>반복이 멈췄는지.</summary>
    public bool Broken => gaze != null && gaze.Found;

    Transform player;
    Vector3 endStart;

    /// <summary>축 위 거리 d에서, 반복해야 하면 뒤로 옮길 거리(0이면 그대로). 순수 함수라 테스트한다.</summary>
    public static float LoopShift(float d, float loopAt, float segment, bool broken)
    {
        if (broken || segment <= 0f || d <= loopAt) return 0f;
        return Mathf.Ceil((d - loopAt) / segment) * segment;
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        if (endGroup != null) endStart = endGroup.position;
        SyncExit();
    }

    void Update()
    {
        if (player == null || endGroup == null) return;
        if (!corridor.Contains(player.position)) return;
        Vector3 ax = axis.normalized;
        float d = Vector3.Dot(player.position - origin, ax);
        if (!Broken)
        {
            float shift = LoopShift(d, loopAt, segment, false);
            if (shift > 0f)
            {
                Move(player.position - ax * shift);
                d -= shift;
                Loops++;
            }
            Vector3 e = endGroup.position;
            float ed = Vector3.Dot(e - origin, ax);
            endGroup.position = e + ax * (d + endAhead - ed);
        }
        SyncExit();
    }

    void Move(Vector3 pos)
    {
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = pos;
        if (cc != null) cc.enabled = true;
    }

    void SyncExit()
    {
        if (endGroup == null) return;
        if (exitGate != null) exitGate.wallBounds = new Bounds(endGroup.TransformPoint(exitGateLocal.center), exitGateLocal.size);
        if (exitZone != null) exitZone.zone = new Bounds(endGroup.TransformPoint(exitZoneLocal.center), exitZoneLocal.size);
    }

    /// <summary>처음 상태로(개발용 방 옮기기). 반복 여부는 gaze 상태를 따른다.</summary>
    public void ResetLoop()
    {
        Loops = 0;
        if (endGroup != null) endGroup.position = endStart;
        SyncExit();
    }
}
