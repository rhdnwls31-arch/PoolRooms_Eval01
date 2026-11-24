using UnityEngine;

/// <summary>
/// 하늘 계단 기믹. 2026-11-24 1차(플레이어를 느리게 하다가 꼭대기로 옮기기)는 "뒤를 돌자마자 계단 맨 끝에 있어 버그 같다"는 피드백으로 바꿨다.
/// 지금 방식: 계단·선반 통로·뒤쪽 벽(<see cref="movers"/>)을 플레이어와 함께 통째로 계단 방향으로 옮긴다. 플레이어와 방이 같이 움직이므로
/// 플레이어 눈에 보이는 계단·선반·뒤쪽 문은 그대로이고, 움직이지 않는 것(위쪽 문, 위 발판, 무늬 없는 하늘 벽)과의 거리만 바뀐다.
/// 1) 문을 보며 오르는 동안: 오른 만큼 방과 플레이어를 함께 뒤로 되돌린다. 걸음·계단은 보통처럼 지나가는데 문만 가까워지지 않는다(계단이 늘어나는 느낌).
///    maxBack만큼 되돌렸으면 그 뒤로는 보통으로 오른다. (지금은 0 = 끔: 되돌리면 선반이 하늘 벽보다 내려가 틈이 보였다. 보통 속도로 오른다.)
/// 2) armAfter만큼 오르려 한 뒤 뒤를 돌아보면(문이 화면에 없을 때) 방과 플레이어를 함께 꼭대기 쪽으로 옮긴다. 뒤 장면은 그대로라 순간이동처럼 안 보이고,
///    다시 앞을 보면 문이 코앞에 내려와 있다. 한 번만.
/// 3) 아래 발판까지 내려오면 방과 플레이어를 함께 원래 자리로 되돌린다(보이는 장면은 그대로). 떨어져 다시 태어나면 방만 되돌린다.
/// 계단은 방과 함께 옮기되, 위 발판 너머(zTop보다 앞)로 나가는 칸은 숨기고 위 끝 빈자리는 미리 만든 덧칸(꺼 둔 렌더러)으로 채운다.
/// 걷는 면(경사 콜라이더)과 계단 밑판은 늘 "옮긴 아래 끝 ~ 고정된 위 끝"을 잇게 다시 맞춘다. PlayerController는 고치지 않는다.
/// </summary>
public class StairsTreadmill : MonoBehaviour
{
    [Tooltip("플레이어와 함께 옮기는 것들(계단 칸 묶음·아래 발판·선반 통로·뒤쪽 벽). 이름 경로는 그대로 두려고 묶지 않고 하나씩 옮긴다.")]
    public Transform[] movers = new Transform[0];
    [Tooltip("계단 칸 렌더러(덧칸 포함). 옮긴 뒤 위치가 zTop보다 앞(-Z)이면 숨긴다.")]
    public Renderer[] steps = new Renderer[0];
    [Tooltip("보이지 않는 경사 콜라이더(BoxCollider, 크기 = size). 아래 끝을 따라간다.")]
    public BoxCollider ramp;
    [Tooltip("계단 밑판(보기용 상자, localScale = 크기). 아래 끝을 따라간다.")]
    public Transform stringer;
    [Tooltip("경사 콜라이더 아래 끝·위 끝(월드, 옮기기 전).")]
    public Vector3 rampBottom, rampTop;
    [Tooltip("밑판 아래 끝·위 끝(월드, 옮기기 전)과 판 두께 방향으로 내릴 거리.")]
    public Vector3 stringerBottom, stringerTop;
    public float stringerDrop = 0.125f, stringerThickness = 0.25f, stringerWidth = 1.6f, rampDrop = 0.1f, rampThickness = 0.2f, rampWidth = 1.6f;

    [Tooltip("기믹이 작동하는 계단 구간(월드, 옮기기 전 기준). 플레이어 발이 이 안에 있을 때만.")]
    public Bounds stairs;
    [Tooltip("계단 위 끝의 문(월드). 화면에 보이는지 판정한다.")]
    public Bounds door;
    [Tooltip("계단 경사: 앞(-Z)으로 1m 갈 때 오르는 높이.")]
    public float slope = 0.6667f;
    [Tooltip("마지막 계단 끝(= 위 발판 시작) z.")]
    public float zTop = -168f;
    [Tooltip("문을 보며 오를 때 되돌리는 최대 거리(m, 앞쪽). 덧칸 수 × 칸 깊이보다 작아야 한다.")]
    public float maxBack = 4.5f;
    [Tooltip("이만큼(m, 앞으로) 올라가려고 해야 옮길 준비가 된다.")]
    public float armAfter = 2.5f;
    [Tooltip("뒤를 본 사이 플레이어를 옮겨 놓을 z(월드). 계단 꼭대기 몇 칸 아래.")]
    public float jumpToZ = -164.5f;
    [Tooltip("방 기준 z가 이보다 뒤(+Z)면 아래 발판 = 처음 상태로 되돌린다.")]
    public float resetZ = -149.6f;

    const float MaxStep = 0.5f; // 한 프레임에 이보다 많이 움직이면 걸음이 아니라 순간이동

    Transform player;
    CharacterController cc;
    Camera cam;
    Vector3 last;
    Vector3[] bases = new Vector3[0];
    float shift;      // 방을 계단 방향(-Z)으로 옮긴 거리(m). 음수 = 뒤로
    float tried;
    bool done;

    Vector3 Along(float s) => new Vector3(0f, slope * s, -s);

    void Start()
    {
        bases = new Vector3[movers.Length];
        for (int i = 0; i < movers.Length; i++) if (movers[i] != null) bases[i] = movers[i].position;
        SetShift(0f);
    }

    void Update()
    {
        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) return;
            player = go.transform; cc = go.GetComponent<CharacterController>(); last = player.position;
        }
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var p = player.position;
        if ((p - last).magnitude > MaxStep) { if (shift != 0f) SetShift(0f); done = false; tried = 0f; last = p; return; } // 순간이동(방 옮기기·다시 태어나기)
        var rel = p - Along(shift);                            // 방 기준 위치
        if (rel.z > resetZ)
        {
            if (shift != 0f) Move(-shift);                     // 방과 플레이어를 같이 되돌린다: 보이는 장면은 그대로
            done = false; tried = 0f; last = player.position; return;
        }
        if (done || !stairs.Contains(rel)) { last = p; return; }

        float forward = last.z - p.z;                          // 이번 프레임 앞으로 간 거리(+ = 올라감)
        var look = cam.transform.forward;
        if (forward > 0f && look.z < -0.25f)
        {
            tried += forward;
            float back = Mathf.Min(forward, shift + maxBack); // 되돌릴 수 있는 만큼만
            if (back > 0f) Move(-back);
        }
        else if (tried >= armAfter && p.z > jumpToZ + 1f && look.z > 0.2f && !DoorInView())
        {
            Move(p.z - jumpToZ);
            done = true;
        }
        last = player.position;
    }

    /// <summary>방과 플레이어를 계단 방향으로 s(m)만큼 같이 옮긴다.</summary>
    void Move(float s)
    {
        SetShift(shift + s);
        var to = player.position + Along(s);
        if (cc != null) cc.enabled = false;
        player.position = to;
        if (cc != null) cc.enabled = true;
    }

    void SetShift(float s)
    {
        shift = s;
        for (int i = 0; i < movers.Length && i < bases.Length; i++) if (movers[i] != null) movers[i].position = bases[i] + Along(s);
        foreach (var r in steps) if (r != null) r.enabled = r.transform.position.z > zTop;
        if (ramp != null)
        {
            Span(ramp.transform, rampBottom + Along(s), rampTop, rampDrop, out float len);
            ramp.size = new Vector3(rampWidth, rampThickness, len);
        }
        if (stringer != null)
        {
            Span(stringer, stringerBottom + Along(s), stringerTop, stringerDrop, out float l2);
            stringer.localScale = new Vector3(stringerWidth, stringerThickness, l2 + 0.3f);
        }
    }

    // a~b를 잇는 기울어진 판: 가운데에서 면 아래쪽으로 drop만큼 내린다.
    static void Span(Transform t, Vector3 a, Vector3 b, float drop, out float length)
    {
        var d = (b - a); length = d.magnitude; d /= Mathf.Max(length, 1e-4f);
        var n = Vector3.Cross(d, Vector3.right).normalized; if (n.y < 0) n = -n;
        t.SetPositionAndRotation((a + b) / 2 - n * drop, Quaternion.LookRotation(d, n));
    }

    bool DoorInView() => GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), door);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan; Gizmos.DrawWireCube(stairs.center, stairs.size);
        Gizmos.color = Color.yellow; Gizmos.DrawWireCube(door.center, door.size);
    }
}
