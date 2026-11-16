using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // E 키는 새 Input System으로 읽는다 (PlayerController와 같은 방식)

/// <summary>
/// 수영장 홀의 옆길 문. 한 번 지나가면 뒤에서 닫히고 다시는 열리지 않는다.
/// 빈 GameObject 하나에 붙이면 Play 시작 때 문틀, 문짝, 문 너머 작은 빈 방(박스)을
/// 큐브로 직접 만든다. 씬 파일이나 다른 스크립트는 건드리지 않는다.
///
/// 배치 규칙: 이 오브젝트의 위치 = 문턱 가운데 바닥, 파란 화살표(+Z) = 홀 쪽.
/// 방은 오브젝트 뒤쪽(-Z)으로 roomDepth만큼 생긴다. 홀 안쪽, 옆벽에 등을 붙여 두면 된다.
/// 에디터에서는 Gizmo(노란 상자)로 방이 차지할 자리를 미리 볼 수 있다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class OneWayDoor : MonoBehaviour
{
    /// <summary>문의 상태. 닫힘 → 열림 → 봉인(다시 열리지 않음) 순서로만 바뀐다.</summary>
    public enum DoorState { Closed, Open, Sealed }

    [Header("크기 (미터)")]
    [Tooltip("문 구멍의 너비. 문짝 너비와 같다.")]
    public float doorWidth = 1.2f;
    [Tooltip("문 구멍의 높이.")]
    public float doorHeight = 2.2f;
    [Tooltip("방 안쪽 너비 (좌우).")]
    public float roomWidth = 3f;
    [Tooltip("방 안쪽 깊이 (문에서 뒷벽까지).")]
    public float roomDepth = 4f;
    [Tooltip("방 안쪽 높이.")]
    public float roomHeight = 3f;
    [Tooltip("벽 두께.")]
    public float wallThickness = 0.15f;

    [Header("상호작용")]
    [Tooltip("이 거리 안에서만 E가 먹는다 (카메라에서 문 가운데까지).")]
    public float interactDistance = 2.5f;
    [Tooltip("카메라 정면과 문 방향 사이 각도가 이 값 이하일 때만 '마주 봤다'고 본다.")]
    public float interactAngle = 35f;
    [Tooltip("열린 문의 회전 각도. 문은 방 안쪽으로 밀려 열린다.")]
    public float openAngle = 95f;
    [Tooltip("여닫는 데 걸리는 시간(초).")]
    public float swingDuration = 0.6f;

    [Header("재질 (비우면 기본 재질 사용)")]
    [Tooltip("방 벽·바닥·천장·문틀에 쓸 재질.")]
    public Material wallMaterial;
    [Tooltip("문짝에 쓸 재질. 비우면 기본 재질을 어둡게 칠해 홀에서 멀리서도 보이게 한다.")]
    public Material doorMaterial;
    [Tooltip("doorMaterial이 비었을 때 문짝에 칠할 색.")]
    public Color fallbackDoorColor = new Color(0.25f, 0.18f, 0.14f);

    /// <summary>현재 문 상태 (읽기 전용).</summary>
    public DoorState State => state;

    DoorState state = DoorState.Closed;
    Transform hinge;          // 문짝의 경첩. 이것을 돌려서 연다.
    Transform playerRoot;     // "Player" 태그가 붙은 오브젝트
    Transform playerCamera;   // 마주 봤는지 판정할 카메라
    bool swinging;            // 여닫는 중이면 입력을 받지 않는다

    void Awake()
    {
        // 자식 콜라이더(방 안쪽 트리거)의 OnTriggerEnter를 이 오브젝트에서 받으려면
        // 루트에 Rigidbody가 있어야 한다. 움직이지 않도록 키네마틱으로 둔다.
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        BuildRoom();
    }

    void Start()
    {
        // 플레이어와 카메라 찾기. 카메라는 PlayerController에 연결된 것을 먼저 쓴다.
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerRoot = player.transform;
            var pc = player.GetComponent<PlayerController>();
            if (pc != null && pc.cameraTransform != null) playerCamera = pc.cameraTransform;
        }
        if (playerCamera == null && Camera.main != null) playerCamera = Camera.main.transform;

        if (playerRoot == null) Debug.LogWarning("[OneWayDoor] 'Player' 태그 오브젝트를 찾지 못했다. 문이 열리지 않는다.", this);
    }

    void Update()
    {
        // 닫혀 있을 때만, 여닫는 중이 아닐 때만 E를 본다. 봉인되면 아무것도 하지 않는다.
        if (state != DoorState.Closed || swinging) return;

        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        if (IsPlayerFacingDoor()) StartCoroutine(Swing(openAngle, DoorState.Open));
    }

    // 방 안쪽 깊은 곳의 트리거에 플레이어가 들어오면 문을 닫고 봉인한다.
    // (CorridorTrigger.cs와 같은 태그 검사 모양)
    void OnTriggerEnter(Collider other)
    {
        if (state != DoorState.Open) return;
        if (!other.CompareTag("Player")) return;

        state = DoorState.Sealed; // 닫히기 시작한 순간부터 다시 열 수 없다
        StartCoroutine(Swing(0f, DoorState.Sealed));
    }

    /// <summary>
    /// 플레이어가 홀 쪽에서, 가까이, 문을 바라보고 있는지 검사한다.
    /// 방 안쪽(-Z)에서 누르는 E는 무시된다.
    /// </summary>
    bool IsPlayerFacingDoor()
    {
        if (playerRoot == null || playerCamera == null) return false;

        // 홀 쪽(+Z)에 서 있어야 한다
        if (transform.InverseTransformPoint(playerRoot.position).z <= 0f) return false;

        Vector3 doorCenter = transform.TransformPoint(new Vector3(0f, doorHeight * 0.5f, 0f));
        Vector3 toDoor = doorCenter - playerCamera.position;
        if (toDoor.magnitude > interactDistance) return false;

        return Vector3.Angle(playerCamera.forward, toDoor) <= interactAngle;
    }

    // 경첩을 targetAngle까지 부드럽게 돌린다. 끝나면 finalState로 바꾼다.
    IEnumerator Swing(float targetAngle, DoorState finalState)
    {
        swinging = true;
        Quaternion from = hinge.localRotation;
        Quaternion to = Quaternion.Euler(0f, targetAngle, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, swingDuration);
            hinge.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        hinge.localRotation = to;
        state = finalState;
        swinging = false;
    }

    // ---------------------------------------------------------------
    // 방 만들기: 전부 이 오브젝트의 로컬 좌표 기준.
    // z = 0 이 문턱(앞벽 바깥면), -Z 쪽이 방 안, y = 0 이 바닥.
    // ---------------------------------------------------------------
    void BuildRoom()
    {
        float w = Mathf.Max(roomWidth, doorWidth + 0.4f);
        float d = Mathf.Max(roomDepth, doorWidth + 1.2f); // 닫히는 문짝이 플레이어를 치지 않을 만큼 깊게
        float h = Mathf.Max(roomHeight, doorHeight + 0.2f);
        float t = wallThickness;
        float halfW = w * 0.5f;

        var root = new GameObject("OneWayDoor_Room").transform;
        root.SetParent(transform, false);

        // 바닥은 홀 바닥과 겹쳐 깜빡이지 않도록 2cm 띄운다
        Part(root, "Floor",   new Vector3(0f, 0.02f - 0.05f, -d * 0.5f), new Vector3(w + 2 * t, 0.1f, d + t));
        Part(root, "Ceiling", new Vector3(0f, h + t * 0.5f, -d * 0.5f),  new Vector3(w + 2 * t, t, d + t));
        Part(root, "Back",    new Vector3(0f, h * 0.5f, -d - t * 0.5f),  new Vector3(w + 2 * t, h, t));
        Part(root, "Left",    new Vector3(-halfW - t * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(t, h, d));
        Part(root, "Right",   new Vector3( halfW + t * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(t, h, d));

        // 앞벽: 문 구멍을 남기고 왼쪽, 오른쪽, 위 세 조각
        float sideW = halfW + t - doorWidth * 0.5f;
        float sideX = doorWidth * 0.5f + sideW * 0.5f;
        Part(root, "FrontLeft",  new Vector3(-sideX, h * 0.5f, -t * 0.5f), new Vector3(sideW, h, t));
        Part(root, "FrontRight", new Vector3( sideX, h * 0.5f, -t * 0.5f), new Vector3(sideW, h, t));
        Part(root, "FrontTop",   new Vector3(0f, doorHeight + (h - doorHeight) * 0.5f, -t * 0.5f),
                                 new Vector3(doorWidth, h - doorHeight, t));

        // 문짝: 경첩은 문 구멍 왼쪽 끝. 문짝은 경첩에서 +X로 뻗는다.
        // 경첩을 +Y로 돌리면 +X가 -Z(방 안)로 돌아가므로 방 안쪽으로 열린다.
        hinge = new GameObject("DoorHinge").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(-doorWidth * 0.5f, 0f, -t * 0.5f);
        var leaf = Part(hinge, "DoorLeaf", new Vector3(doorWidth * 0.5f, doorHeight * 0.5f, 0f),
                        new Vector3(doorWidth - 0.02f, doorHeight - 0.02f, 0.08f));
        var leafRenderer = leaf.GetComponent<Renderer>();
        if (doorMaterial != null) leafRenderer.sharedMaterial = doorMaterial;
        else TintFallback(leafRenderer, fallbackDoorColor);

        // 닫힘 트리거: 문짝이 쓸고 지나가는 범위(문 너비)보다 0.4m 더 안쪽부터 뒷벽까지
        float triggerStart = doorWidth + 0.4f;
        var trigger = new GameObject("SealTrigger");
        trigger.transform.SetParent(root, false);
        trigger.transform.localPosition = new Vector3(0f, h * 0.5f, -(triggerStart + d) * 0.5f);
        var box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(w, h, d - triggerStart);
    }

    // 큐브 하나를 만들어 붙인다. 콜라이더는 큐브 기본 BoxCollider를 그대로 쓴다.
    GameObject Part(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (wallMaterial != null) go.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        return go;
    }

    // 문짝 재질이 없을 때 기본 재질 사본에 색만 칠한다 (HDRP Lit은 _BaseColor)
    static void TintFallback(Renderer r, Color c)
    {
        var m = r.material; // 사본 생성
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // 에디터에서 방이 차지할 자리와 문 위치를 미리 보여준다 (Play 전 배치용)
    void OnDrawGizmos()
    {
        float w = Mathf.Max(roomWidth, doorWidth + 0.4f) + 2 * wallThickness;
        float d = Mathf.Max(roomDepth, doorWidth + 1.2f) + wallThickness;
        float h = Mathf.Max(roomHeight, doorHeight + 0.2f) + wallThickness;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new Vector3(0f, h * 0.5f, -d * 0.5f), new Vector3(w, h, d));
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(0f, doorHeight * 0.5f, 0f), new Vector3(doorWidth, doorHeight, 0.1f));
        Gizmos.DrawLine(new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.05f, 1f)); // 홀 쪽 방향
    }
}
