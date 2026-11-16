using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // E 키는 새 Input System으로 읽는다 (OneWayDoor와 같은 방식)

/// <summary>
/// 문 너머 방의 출구 문. 마주 보고 E를 누르면 암전 → 플레이어를 돌아올 자리로 옮기고
/// → "틀린 것" 하나를 켜고 끈 뒤 → 다시 밝아진다. 한 번만 동작한다.
/// 같은 수영장으로 돌아왔는데 하나만 틀린 상태를 씬 전환 없이 만든다.
///
/// 배치 규칙: 이 오브젝트의 위치 = 문짝 아래 가운데(벽 안쪽 면), 파란 화살표(+Z) = 플레이어가 서는 쪽.
/// 문짝은 Play 시작 때 큐브로 만든다. 문 구멍은 내지 않는다(벽에 붙은 문짝만 보인다).
/// 주의: hideOnReturn에 넣는 오브젝트의 자식으로 두면 안 된다. 같이 꺼지면서 연출이 멈춘다.
/// </summary>
public class PoolReturnDoor : MonoBehaviour
{
    [Header("크기 (미터)")]
    [Tooltip("문짝 너비.")]
    public float doorWidth = 1.1f;
    [Tooltip("문짝 높이.")]
    public float doorHeight = 2.1f;

    [Header("상호작용")]
    [Tooltip("이 거리 안에서만 E가 먹는다 (카메라에서 문 가운데까지).")]
    public float interactDistance = 2.5f;
    [Tooltip("카메라 정면과 문 방향 사이 각도가 이 값 이하일 때만 '마주 봤다'고 본다.")]
    public float interactAngle = 35f;

    [Header("돌아오기")]
    [Tooltip("돌아온 플레이어가 설 자리와 바라볼 방향.")]
    public Transform returnPoint;
    [Tooltip("암전·밝아짐에 쓸 FadeCanvas의 ScreenFader.")]
    public ScreenFader fader;
    [Tooltip("돌아온 순간 끌 오브젝트 (예: 들어갔던 문).")]
    public GameObject[] hideOnReturn;
    [Tooltip("돌아온 순간 켤 오브젝트 (예: 문 구멍을 막는 벽 조각).")]
    public GameObject[] showOnReturn;
    [Tooltip("완전히 어두운 상태로 머무는 시간(초).")]
    public float holdBlack = 0.8f;
    [Tooltip("다시 밝아지는 데 걸리는 시간(초). 처음 눈 뜨는 느낌으로 조금 길게.")]
    public float fadeInTime = 1.6f;

    [Header("재질 (비우면 기본 재질을 어둡게 칠함)")]
    public Material doorMaterial;
    public Color fallbackDoorColor = new Color(0.25f, 0.18f, 0.14f);

    /// <summary>이미 한 번 사용됐는지 (읽기 전용).</summary>
    public bool Used => used;

    bool used;
    Transform playerRoot;
    Transform playerCamera;
    PlayerController playerController;
    CharacterController characterController;

    void Awake()
    {
        BuildLeaf();
    }

    void Start()
    {
        // 플레이어와 카메라 찾기 (OneWayDoor와 같은 방식)
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerRoot = player.transform;
            playerController = player.GetComponent<PlayerController>();
            characterController = player.GetComponent<CharacterController>();
            if (playerController != null && playerController.cameraTransform != null) playerCamera = playerController.cameraTransform;
        }
        if (playerCamera == null && Camera.main != null) playerCamera = Camera.main.transform;

        if (playerRoot == null) Debug.LogWarning("[PoolReturnDoor] 'Player' 태그 오브젝트를 찾지 못했다. 문이 동작하지 않는다.", this);
        if (returnPoint == null) Debug.LogWarning("[PoolReturnDoor] returnPoint가 비어 있다. 문이 동작하지 않는다.", this);
    }

    void Update()
    {
        if (used) return;
        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;
        if (IsPlayerFacingDoor()) Use();
    }

    /// <summary>
    /// 출구 문을 사용한다. E 입력과 같은 길이며, 한 번만 동작한다.
    /// 거리·방향 검사는 하지 않는다 (검사는 Update에서 E를 볼 때만).
    /// </summary>
    public void Use()
    {
        if (used || playerRoot == null || returnPoint == null) return;
        used = true;
        StartCoroutine(ReturnSequence());
    }

    /// <summary>플레이어가 문 앞쪽(+Z)에서, 가까이, 문을 바라보고 있는지 검사한다.</summary>
    bool IsPlayerFacingDoor()
    {
        if (playerRoot == null || playerCamera == null) return false;
        if (transform.InverseTransformPoint(playerRoot.position).z <= 0f) return false;

        Vector3 doorCenter = transform.TransformPoint(new Vector3(0f, doorHeight * 0.5f, 0f));
        Vector3 toDoor = doorCenter - playerCamera.position;
        if (toDoor.magnitude > interactDistance) return false;

        return Vector3.Angle(playerCamera.forward, toDoor) <= interactAngle;
    }

    // 조작 정지 → 암전 → 이동 + 틀린 것 바꾸기 → 잠깐 어둠 → 밝아짐 → 조작 복귀
    IEnumerator ReturnSequence()
    {
        if (playerController != null) playerController.enabled = false;

        if (fader != null) yield return StartCoroutine(fader.FadeOut());

        // CharacterController는 켜진 채로 위치를 바꾸면 되돌려지므로 잠깐 끈다
        if (characterController != null) characterController.enabled = false;
        playerRoot.SetPositionAndRotation(returnPoint.position, returnPoint.rotation);
        if (characterController != null) characterController.enabled = true;

        foreach (var go in hideOnReturn) if (go != null) go.SetActive(false);
        foreach (var go in showOnReturn) if (go != null) go.SetActive(true);

        yield return new WaitForSeconds(holdBlack);

        if (fader != null) yield return StartCoroutine(fader.FadeIn(fadeInTime));

        if (playerController != null) playerController.enabled = true;
    }

    // 벽 안쪽 면에 붙는 얇은 문짝 하나. 로컬 z = 0 이 벽면, +Z가 방 안쪽.
    void BuildLeaf()
    {
        var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = "ReturnDoorLeaf";
        leaf.transform.SetParent(transform, false);
        leaf.transform.localPosition = new Vector3(0f, doorHeight * 0.5f, 0.04f);
        leaf.transform.localScale = new Vector3(doorWidth, doorHeight, 0.08f);
        Destroy(leaf.GetComponent<Collider>()); // 벽이 이미 막고 있으니 문짝 콜라이더는 필요 없다

        var r = leaf.GetComponent<Renderer>();
        if (doorMaterial != null) r.sharedMaterial = doorMaterial;
        else
        {
            var m = r.material; // 사본 생성 (HDRP Lit은 _BaseColor)
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", fallbackDoorColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", fallbackDoorColor);
        }
    }

    // 에디터에서 문짝 자리와 플레이어 쪽 방향을 미리 보여준다
    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(new Vector3(0f, doorHeight * 0.5f, 0.04f), new Vector3(doorWidth, doorHeight, 0.08f));
        Gizmos.DrawLine(new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.05f, 1f));
    }
}
