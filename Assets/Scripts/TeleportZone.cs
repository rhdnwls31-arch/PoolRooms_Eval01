using UnityEngine;

/// <summary>
/// 범위에 들어서면 다른 자리로 옮긴다(비유클리드 연결). 위치와 방향은 두 기준(fromAnchor → toAnchor)으로 옮겨서,
/// 걷던 방향 그대로 이어서 걷는 것처럼 보인다. 도착한 곳의 표시(arrivalObject)는 멀어지고 화면 밖일 때 사라진다.
/// 컴퓨터 속 맵의 모니터 화면 → 천사 방(기획서 §6-3e)에 쓴다. 범위 앞은 출구 벽(RevealWhenExplored)이 막고 있다.
/// </summary>
public class TeleportZone : MonoBehaviour
{
    [Tooltip("이 범위에 플레이어가 들어오면 옮긴다.")]
    public Bounds zone;
    [Tooltip("떠나는 쪽 기준(forward = 지나가는 방향).")]
    public Transform fromAnchor;
    [Tooltip("도착하는 쪽 기준(forward = 도착한 방 안쪽).")]
    public Transform toAnchor;
    [Tooltip("도착할 때 기준 면에서 적어도 이만큼 앞에 선다(벽에 끼지 않게).")]
    public float minArrivalDepth = 1f;
    [Tooltip("도착한 곳에 잠깐 남는 표시(지나온 자리). 도착할 때 켜고, 화면 밖일 때 끈다.")]
    public GameObject arrivalObject;
    [Tooltip("도착한 자리에서 이만큼 멀어져야 표시가 사라질 수 있다.")]
    public float hideDistance = 2.5f;
    [Tooltip("옮겨질 때 막 전환 소리(물속으로 가라앉는 듯한 소리)를 낸다.")]
    public bool transitionSound = true;

    /// <summary>0 기다림, 1 도착함(표시 보임), 2 표시가 사라짐.</summary>
    public int Stage { get; private set; }

    Transform player;
    Camera cam;

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        ResetZone();
    }

    void Update()
    {
        if (player == null) return;
        if (Stage == 0 && zone.Contains(player.position)) Teleport();
        else if (Stage == 1 && arrivalObject != null)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            Vector3 d = player.position - toAnchor.position; d.y = 0f;
            if (d.magnitude > hideDistance && !InView(arrivalObject.transform))
            {
                arrivalObject.SetActive(false);
                Stage = 2;
            }
        }
    }

    void Teleport()
    {
        Vector3 local = fromAnchor.InverseTransformPoint(player.position);
        local.z = Mathf.Max(local.z, minArrivalDepth);
        Vector3 pos = toAnchor.TransformPoint(local);
        Quaternion rot = toAnchor.rotation * Quaternion.Inverse(fromAnchor.rotation) * player.rotation;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(pos, rot);
        if (cc != null) cc.enabled = true;
        if (arrivalObject != null) arrivalObject.SetActive(true);
        Stage = 1;
        if (transitionSound) GameAudio.Play(Cue.Transition, pos); // 2026-11-27 사운드: 막 전환
    }

    /// <summary>처음 상태로 되돌린다(개발용 방 옮기기에서도 쓴다).</summary>
    public void ResetZone()
    {
        Stage = 0;
        if (arrivalObject != null) arrivalObject.SetActive(false);
    }

    bool InView(Transform root)
    {
        Bounds b = default; bool any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b);
    }
}
