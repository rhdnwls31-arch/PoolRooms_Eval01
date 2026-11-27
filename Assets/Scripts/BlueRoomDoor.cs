using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 파란 침실 복도 끝의 문(기획서 §6-3d). 단계는 다음과 같다.
/// 0) 문에 바짝 다가가 바라보면 도어뷰어(볼록렌즈) 시야로 바뀐다. 처음에는 문밖(천사 방 하늘)에서 키 큰 인외가 렌즈를 들여다보고 있다.
/// 1) 물러나거나 눈을 떼면 시야가 풀리고, 그 사이 인외는 사라진다. 다시 보면 문밖에는 아무도 없다.
/// 2) 다시 들여다보고 눈을 떼면(시야가 다 풀리면) 눈앞에서 문이 천천히 열린다.
///    2026-11-20 사용자 결정: 전에는 문이 화면 밖에 있어야 열려서 뒤를 돌아봐야 했다. 그럴 필요 없이 바로 열리게 바꿨다.
/// 3) 열린 문 너머의 빛으로 들어가면, 천사 방 동쪽 벽에 붙은 같은 문 앞으로 옮겨진다(복도 끝과 천사 방이 이어진 순간이동).
/// 4) 천사 방에 남은 문은 플레이어가 멀어지고 화면 밖에 있을 때 사라진다. 하늘 벽만 남는다(§7 돌아갈 수 없다).
/// 소리(노크 등)는 나중에 넣는다(§8).
/// </summary>
public class BlueRoomDoor : MonoBehaviour
{
    [Header("도어뷰어")]
    [Tooltip("렌즈 자리. 복도 쪽 문 면의 눈높이.")]
    public Transform viewPoint;
    [Tooltip("이 거리(수평, 미터) 안에서 렌즈를 바라보면 도어뷰어 시야로 바뀐다.")]
    public float enterDistance = 1f;
    [Tooltip("이 거리보다 멀어지면 시야가 풀린다.")]
    public float exitDistance = 1.4f;
    [Tooltip("화면 가운데와 렌즈 사이 각도가 이보다 작아야 바라보는 것이다.")]
    public float lookAngle = 30f;
    [Tooltip("한 번 들여다보면 적어도 이만큼(초)은 시야가 유지된다. 스치듯 지나가며 한 프레임만 보이는 일을 막는다.")]
    public float minPeekTime = 1.2f;
    [Tooltip("시야가 바뀌는 페이드 시간(초).")]
    public float fadeTime = 0.25f;
    [Tooltip("문밖을 찍는 카메라(천사 방 안). 들여다보는 동안만 켠다.")]
    public Camera peepCamera;
    [Tooltip("문밖에 서 있는 인외. 처음 들여다볼 때만 보인다.")]
    public GameObject figure;
    [Tooltip("들여다보는 동안 인외의 머리가 천천히 갸웃거리는 각도(도). 0이면 가만히 있다.")]
    public float headTiltAngle = 0f; // 2026-11-19 사용자 결정: 숨 쉬듯 움직이면 이상해서 끔. 가만히 바라보기만 한다.
    [Tooltip("갸웃거림 한 번 왕복에 걸리는 시간(초).")]
    public float headTiltPeriod = 3.2f;

    [Header("문")]
    [Tooltip("문짝의 경첩. 열리면 y축으로 openAngle만큼 돈다.")]
    public Transform leafHinge;
    public float openAngle = 95f;
    [Tooltip("문이 다 열리기까지 걸리는 시간(초). 눈앞에서 열리므로 천천히, 끝에서 느려진다.")]
    public float openDuration = 1.8f;
    [Tooltip("시야가 풀린 뒤 문이 열리기 시작할 때까지 기다리는 시간(초).")]
    public float openDelay = 0.4f;
    [Tooltip("문이 닫혀 있는 동안 지나가지 못하게 막는 콜라이더.")]
    public Collider leafBlocker;

    [Header("순간이동")]
    [Tooltip("이 범위에 들어오면 천사 방으로 옮긴다(열린 문 너머의 빛).")]
    public Bounds teleportZone;
    [Tooltip("복도 끝 문(forward = 문을 지나가는 방향).")]
    public Transform fromAnchor;
    [Tooltip("천사 방 쪽 같은 문(forward = 천사 방 안쪽).")]
    public Transform toAnchor;
    [Tooltip("도착할 때 문 면에서 적어도 이만큼 앞에 선다(벽에 끼지 않게).")]
    public float minArrivalDepth = 1f;
    [Tooltip("천사 방 벽에 붙은 열린 문. 도착할 때 켜고, 화면 밖일 때 끈다.")]
    public GameObject arrivalDoor;
    [Tooltip("도착한 문에서 이만큼 멀어져야 문이 사라질 수 있다.")]
    public float arrivalHideDistance = 2f;

    /// <summary>0 처음, 1 인외를 봄, 2 빈 문밖을 봄, 3 문이 열림, 4 천사 방 도착, 5 남은 문이 사라짐.</summary>
    public int Stage { get; private set; }
    /// <summary>지금 도어뷰어로 보고 있는지.</summary>
    public bool Peeking { get; private set; }

    Transform player;
    Camera cam;
    CanvasGroup overlay;
    FisheyeDisc disc;
    float peekStart, overlayAlpha;
    float openT = 1f, clearTime = -1f;
    Quaternion hingeClosed;

    void Awake()
    {
        if (leafHinge != null) hingeClosed = leafHinge.localRotation;
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        BuildOverlay();
        ResetDoor();
    }

    void Update()
    {
        if (player == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (!knocked && Stage == 0 && knockZone.size != Vector3.zero && knockZone.Contains(player.position))
        {
            knocked = true;
            Invoke(nameof(Knock), knockDelay);
        }
        if (Stage <= 2) UpdatePeek();
        if (Stage == 2 && !Peeking && overlayAlpha <= 0f)
        {
            if (clearTime < 0f) clearTime = Time.time;
            if (Time.time - clearTime >= openDelay) Open();
        }
        else clearTime = -1f;
        if (Stage >= 3 && openT < 1f && leafHinge != null)
        {
            openT = Mathf.Min(1f, openT + Time.deltaTime / Mathf.Max(0.01f, openDuration));
            float e = 1f - (1f - openT) * (1f - openT) * (1f - openT); // 끝에서 느려진다
            leafHinge.localRotation = hingeClosed * Quaternion.Euler(0f, openAngle * e, 0f);
        }
        if (Stage == 3 && teleportZone.Contains(player.position)) Teleport();
        if (Stage == 4 && arrivalDoor != null)
        {
            Vector3 d = player.position - toAnchor.position; d.y = 0f;
            if (d.magnitude > arrivalHideDistance && !InView(RendererBounds(arrivalDoor.transform)))
            {
                arrivalDoor.SetActive(false);
                Stage = 5;
            }
        }

        float target = Peeking ? 1f : 0f;
        overlayAlpha = Mathf.MoveTowards(overlayAlpha, target, Time.deltaTime / Mathf.Max(0.01f, fadeTime));
        if (overlay != null) overlay.alpha = overlayAlpha;
        if (peepCamera != null) peepCamera.enabled = overlayAlpha > 0f;
        if (!Peeking && overlayAlpha <= 0f && figure != null && figure.activeSelf) figure.SetActive(false);
        TiltHead();
    }

    // 2026-11-19: 멈춰 있으면 마네킹 같아서, 보고 있는 동안 머리가 렌즈 앞에서 천천히 갸웃거린다(살아 있다는 느낌).
    // 2026-11-19 VARCO 모델은 머리가 따로 없는 한 덩어리라, 머리 대신 몸 전체가 발을 축으로 렌즈 쪽으로 살짝 숨 쉬듯 기운다.
    [Tooltip("머리가 따로 없는 모델일 때, 몸 전체가 렌즈 쪽으로 기우는 각도(도).")]
    public float bodyLeanAngle = 0f;
    Transform head;
    Quaternion headRest;
    Vector3 bodyRestPos;
    Quaternion bodyRestRot;
    bool bodyMode, tiltInit;
    void TiltHead()
    {
        if (figure == null || !figure.activeInHierarchy || headTiltAngle <= 0f) return;
        float k = Mathf.Sin((Time.time - peekStart) * Mathf.PI * 2f / Mathf.Max(0.1f, headTiltPeriod));
        if (!tiltInit)
        {
            tiltInit = true;
            head = figure.transform.Find("Head");
            if (head != null) headRest = head.rotation;
            else { bodyMode = true; bodyRestPos = figure.transform.position; bodyRestRot = figure.transform.rotation; }
        }
        if (bodyMode)
        {
            var t = figure.transform;
            t.SetPositionAndRotation(bodyRestPos, bodyRestRot);
            t.RotateAround(bodyRestPos, bodyRestRot * Vector3.right, (k * 0.5f + 0.5f) * bodyLeanAngle); // 앞으로만 기울었다 돌아온다
            return;
        }
        if (head == null) return;
        // 렌즈 너머를 찍는 카메라와 머리를 잇는 축으로 돌려서, 렌즈 쪽에서 보면 고개를 좌우로 기울이는 것처럼 보이게 한다.
        Vector3 axis = peepCamera != null ? (head.position - peepCamera.transform.position).normalized : figure.transform.forward;
        head.rotation = Quaternion.AngleAxis(k * headTiltAngle, axis) * headRest;
    }

    void UpdatePeek()
    {
        Vector3 to = viewPoint.position - cam.transform.position;
        Vector3 flat = to; flat.y = 0f;
        float dist = flat.magnitude;
        float angle = Vector3.Angle(cam.transform.forward, to);
        if (!Peeking)
        {
            if (dist <= enterDistance && angle <= lookAngle) StartPeek();
        }
        else if (Time.time - peekStart >= minPeekTime && (dist > exitDistance || angle > lookAngle + 15f))
            EndPeek();
    }

    void StartPeek()
    {
        Peeking = true;
        peekStart = Time.time;
        if (figure != null) figure.SetActive(Stage == 0);
        // 2026-11-27 사운드: 인외가 렌즈 너머에 있는 동안만 기이한 소리
        if (Stage == 0) GameAudio.Play(Cue.PeepEerie, viewPoint.position);
        if (disc != null && overlay != null)
        {
            var pr = ((RectTransform)overlay.transform).rect;
            float d = Mathf.Min(pr.width, pr.height) * 0.94f;
            disc.rectTransform.sizeDelta = new Vector2(d, d);
        }
    }

    void EndPeek()
    {
        Peeking = false;
        // 2026-11-27 사운드: 렌즈에서 눈을 떼고 조금 뒤, 문 너머(방금 들여다보던 그것)가 노크한다
        GameAudio.Stop(Cue.PeepEerie, 0.4f);
        if (Stage == 0) Stage = 1;
        else if (Stage == 1) Stage = 2;
        // 인외는 시야가 완전히 풀린 뒤(Update에서, 화면 밖에서) 사라진다. 다음에 들여다볼 때 이미 없다.
    }

    [Tooltip("복도에 들어온 뒤 노크가 들리기까지(초).")]
    public float knockDelay = 0.3f;
    [Tooltip("이 범위(복도)에 처음 들어오면 문 너머에서 한 번 노크한다(2026-11-27 사용자: 복도 진입하자마자 똑똑, 그다음엔 안 난다).")]
    public Bounds knockZone;
    bool knocked;

    void Knock() => GameAudio.Play(Cue.Knock, viewPoint.position);

    void Open()
    {
        Stage = 3;
        openT = 0f; // 문짝은 Update에서 openDuration 동안 돌아간다(앞쪽 = 플레이어 반대쪽으로 열린다)
        GameAudio.Play(Cue.DoorCreak, leafHinge != null ? leafHinge.position + Vector3.up : viewPoint.position); // 2026-11-27 사운드
        if (leafBlocker != null) leafBlocker.enabled = false;
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
        if (arrivalDoor != null) arrivalDoor.SetActive(true);
        Stage = 4;
        GameAudio.Play(Cue.Transition, pos); // 2026-11-27 사운드: 막 전환
    }

    /// <summary>처음 상태(닫힌 문, 인외 숨김, 도착 문 숨김)로 되돌린다. 개발용 방 옮기기에서도 쓴다.</summary>
    public void ResetDoor()
    {
        CancelInvoke(nameof(Knock));
        knocked = false;
        Stage = 0;
        Peeking = false;
        overlayAlpha = 0f;
        if (overlay != null) overlay.alpha = 0f;
        if (peepCamera != null) peepCamera.enabled = false;
        if (figure != null) figure.SetActive(false);
        if (leafHinge != null) leafHinge.localRotation = hingeClosed;
        openT = 1f; clearTime = -1f;
        if (leafBlocker != null) leafBlocker.enabled = true;
        if (arrivalDoor != null) arrivalDoor.SetActive(false);
    }

    bool InView(Bounds b)
    {
        if (b.size == Vector3.zero) return false;
        return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b);
    }

    static Bounds RendererBounds(Transform root)
    {
        Bounds b = default; bool any = false;
        if (root == null) return b;
        foreach (var r in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // 화면을 덮는 도어뷰어 시야: 검은 바탕 + 가운데 둥근 렌즈 화면. 페이드 이미지(ScreenFader) 바로 아래 층에 둔다.
    void BuildOverlay()
    {
        if (peepCamera == null || peepCamera.targetTexture == null) return;
        Transform parent; int index = -1;
        var fader = FindAnyObjectByType<ScreenFader>();
        if (fader != null && fader.fadeImage != null)
        {
            parent = fader.fadeImage.transform.parent;
            index = fader.fadeImage.transform.GetSiblingIndex();
        }
        else
        {
            var cgo = new GameObject("PeepholeCanvas", typeof(Canvas));
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            parent = cgo.transform;
        }

        var go = new GameObject("PeepholeView", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        if (index >= 0) rt.SetSiblingIndex(index);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var bg = go.GetComponent<Image>();
        bg.color = Color.black; bg.raycastTarget = false;
        overlay = go.GetComponent<CanvasGroup>();
        overlay.alpha = 0f; overlay.blocksRaycasts = false; overlay.interactable = false;

        var dgo = new GameObject("Lens", typeof(RectTransform), typeof(CanvasRenderer));
        var drt = (RectTransform)dgo.transform;
        drt.SetParent(rt, false);
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.sizeDelta = new Vector2(600f, 600f);
        disc = dgo.AddComponent<FisheyeDisc>();
        disc.texture = peepCamera.targetTexture;
        disc.raycastTarget = false;
    }
}
