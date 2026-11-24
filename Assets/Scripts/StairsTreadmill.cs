using UnityEngine;

/// <summary>
/// 하늘 계단 기믹(2026-11-24 사용자 선택): 문을 바라보며 올라가는 동안에는 거의 나아가지 않는다(꿈에서 아무리 걸어도 안 가는 느낌).
/// 그러다 뒤를 돌아본 사이(문이 화면에 없을 때) 계단 꼭대기 가까이로 옮겨 놓아, 다시 앞을 보면 문이 갑자기 코앞에 있다.
/// 한 번 옮기면 끝난다. 계단 아래 발판으로 내려가면 다시 처음 상태가 된다. PlayerController는 고치지 않고 위치만 되돌린다.
/// </summary>
public class StairsTreadmill : MonoBehaviour
{
    [Tooltip("기믹이 작동하는 계단 구간(월드). 플레이어 발이 이 안에 있을 때만.")]
    public Bounds stairs;
    [Tooltip("계단 위 끝의 문(월드). 화면에 보이는지 판정한다.")]
    public Bounds door;
    [Tooltip("계단 경사: 앞(-Z)으로 1m 갈 때 오르는 높이.")]
    public float slope = 0.6667f;
    [Tooltip("문을 보며 올라갈 때 되돌리는 비율(0 = 그대로, 1 = 전혀 못 감).")]
    [Range(0f, 1f)] public float cancel = 0.9f;
    [Tooltip("이만큼(m, 앞으로) 올라가려고 해야 옮길 준비가 된다.")]
    public float armAfter = 2.5f;
    [Tooltip("옮겨 놓을 자리의 z(월드). 계단 꼭대기 몇 칸 아래.")]
    public float jumpToZ = -165.3f;
    [Tooltip("이 z보다 뒤(+Z)로 내려가면 처음 상태로 돌아간다.")]
    public float resetZ = -149.6f;

    const float MaxStep = 0.5f; // 한 프레임에 이보다 많이 움직이면 걸음이 아니라 순간이동

    Transform player;
    CharacterController cc;
    Camera cam;
    Vector3 last;
    float tried;
    bool done;

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
        if (p.z > resetZ) { done = false; tried = 0f; last = p; return; }
        if (done || !stairs.Contains(p)) { last = p; return; }

        float forward = last.z - p.z;                         // 이번 프레임 앞으로 간 거리(+ = 올라감)
        if (Mathf.Abs(forward) > MaxStep || Mathf.Abs(last.y - p.y) > MaxStep) { last = p; return; } // 순간이동(방 옮기기·다시 태어나기)은 걸음으로 치지 않는다
        var look = cam.transform.forward;
        bool facingDoor = look.z < -0.25f;
        if (forward > 0f && facingDoor)
        {
            tried += forward;
            float back = forward * cancel;
            Teleport(p + new Vector3(0f, -back * slope, back));
            p = player.position;
        }
        else if (tried >= armAfter && p.z > jumpToZ + 1f && look.z > 0.2f && !DoorInView())
        {
            // 뒤를 본 사이 꼭대기 가까이로. 경사 위의 같은 높이 차를 유지한다.
            float dz = p.z - jumpToZ;
            Teleport(p + new Vector3(0f, dz * slope, -dz));
            p = player.position;
            done = true;
        }
        last = p;
    }

    bool DoorInView() => GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), door);

    void Teleport(Vector3 to)
    {
        if (cc != null) cc.enabled = false;
        player.position = to;
        if (cc != null) cc.enabled = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan; Gizmos.DrawWireCube(stairs.center, stairs.size);
        Gizmos.color = Color.yellow; Gizmos.DrawWireCube(door.center, door.size);
    }
}
