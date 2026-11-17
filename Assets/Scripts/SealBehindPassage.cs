using UnityEngine;

/// <summary>
/// 지나온 통로를 막아 다시 돌아갈 수 없게 한다(기획 필러 4 "돌아갈 수 없다"). 두 단계로 막는다.
/// 1) 플레이어가 통로를 지나 도착한 방에 blockDistance만큼 들어오면, 보이지 않는 벽(blocker)이 바로 켜진다.
///    곧바로 뒤돌아도 나갈 수 없다.
/// 2) minDistance 이상 들어와 있고 판 자리가 화면 밖일 때, 도착한 방의 벽과 똑같은 판(plug)이 나타난다.
///    뒤를 돌아보면 이미 벽이다.
/// blocker와 plug는 처음에 꺼 둔다. 판의 크기는 plugBounds에 미리 적어 둔다(꺼진 렌더러는 크기를 모른다).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class SealBehindPassage : MonoBehaviour
{
    [Tooltip("통로를 막는 보이는 판(자식 포함). 처음에는 꺼 둔다.")]
    public GameObject plug;
    [Tooltip("통로를 막는 보이지 않는 벽(콜라이더만). 처음에는 꺼 둔다.")]
    public GameObject blocker;
    [Tooltip("막는 판이 차지하는 월드 범위. 화면에 보이는지와 플레이어 거리를 판정할 때 쓴다.")]
    public Bounds plugBounds;
    [Tooltip("도착한 방 쪽을 향하는 월드 방향(통로 → 방). 플레이어가 이쪽에 있어야 막는다.")]
    public Vector3 arrivalDirection = Vector3.back;
    [Tooltip("판 면에서 플레이어(중심)까지 이만큼 들어오면 보이지 않는 벽을 켠다. 플레이어 반지름(0.3m)보다 커야 몸에 겹치지 않는다.")]
    public float blockDistance = 0.45f;
    [Tooltip("판 면에서 플레이어(중심)까지 이만큼 들어와야 보이는 판을 켠다. 보이지 않는 벽에 붙어 선 플레이어(약 0.38m)도 돌아서면 판이 켜지게 그보다 작게 둔다.")]
    public float minDistance = 0.35f;
    [Tooltip("보이는 판을 켤 때 함께 끌 오브젝트(문턱 표시 등).")]
    public GameObject[] hideOnSeal;

    /// <summary>보이지 않는 벽이 켜졌는지(돌아갈 수 없는 상태).</summary>
    public bool Blocked { get; private set; }
    /// <summary>보이는 판까지 켜졌는지(읽기 전용).</summary>
    public bool Sealed { get; private set; }

    bool armed;
    Transform player;
    Camera cam;

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (plug != null) plug.SetActive(false);
        if (blocker != null) blocker.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (Sealed || !other.CompareTag("Player")) return;
        armed = true;
        player = other.transform;
    }

    void Update()
    {
        if (!armed || Sealed || player == null) return;
        float depth = DepthPastFace(player.position);
        if (!Blocked && depth >= blockDistance) Block();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        bool inView = GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), plugBounds);
        if (CanSeal(depth, inView)) Seal();
    }

    /// <summary>플레이어가 판의 도착 방 쪽 면에서 도착 방향으로 얼마나 들어와 있는지(미터). 음수면 아직 통로 안이다.</summary>
    public float DepthPastFace(Vector3 playerPosition)
    {
        Vector3 dir = arrivalDirection.sqrMagnitude > 0f ? arrivalDirection.normalized : Vector3.back;
        Vector3 face = plugBounds.center + Vector3.Scale(dir, plugBounds.extents);
        return Vector3.Dot(playerPosition - face, dir);
    }

    /// <summary>보이는 판을 켜도 되는지. 충분히 들어와 있고, 판 자리가 화면에 없을 때만 참이다.</summary>
    public bool CanSeal(float depthPastFace, bool plugInView) => !plugInView && depthPastFace >= minDistance;

    /// <summary>보이지 않는 벽을 켠다. 이때부터 통로로 돌아갈 수 없다.</summary>
    public void Block()
    {
        Blocked = true;
        if (blocker != null) blocker.SetActive(true);
    }

    /// <summary>보이는 판을 켜고 문턱 표시를 끈다. 다시 열리지 않는다.</summary>
    public void Seal()
    {
        if (!Blocked) Block();
        Sealed = true;
        armed = false;
        if (plug != null) plug.SetActive(true);
        if (hideOnSeal != null)
            foreach (var go in hideOnSeal) if (go != null) go.SetActive(false);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Sealed ? Color.red : Blocked ? new Color(1f, 0.5f, 0f) : Color.magenta;
        Gizmos.DrawWireCube(plugBounds.center, plugBounds.size);
    }
}
