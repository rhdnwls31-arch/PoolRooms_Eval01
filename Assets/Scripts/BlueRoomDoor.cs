using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 파란 침실 복도 끝의 문(기획서 §6-3d). 단계는 다음과 같다.
/// 0) 문에 바짝 다가가 바라보면 도어뷰어(볼록렌즈) 시야로 바뀐다. 처음에는 문밖(천사 방 하늘)에서 키 큰 인외가 렌즈를 들여다보고 있다.
/// 1) 물러나거나 눈을 떼면 시야가 풀리고, 그 사이 인외는 사라진다. 다시 보면 문밖에는 아무도 없다.
/// 2) 두 번째로 보고 나서 문이 화면 밖에 있으면 문이 열린다.
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

    [Header("문")]
    [Tooltip("문짝의 경첩. 열리면 y축으로 openAngle만큼 돈다.")]
    public Transform leafHinge;
    public float openAngle = 95f;
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

        if (Stage <= 2) UpdatePeek();
        if (Stage == 2 && !Peeking && overlayAlpha <= 0f && !InView(RendererBounds(leafHinge))) Open();
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
        if (Stage == 0) Stage = 1;
        else if (Stage == 1) Stage = 2;
        // 인외는 시야가 완전히 풀린 뒤(Update에서, 화면 밖에서) 사라진다. 다음에 들여다볼 때 이미 없다.
    }

    void Open()
    {
        Stage = 3;
        if (leafHinge != null) leafHinge.localRotation = hingeClosed * Quaternion.Euler(0f, openAngle, 0f);
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
    }

    /// <summary>처음 상태(닫힌 문, 인외 숨김, 도착 문 숨김)로 되돌린다. 개발용 방 옮기기에서도 쓴다.</summary>
    public void ResetDoor()
    {
        Stage = 0;
        Peeking = false;
        overlayAlpha = 0f;
        if (overlay != null) overlay.alpha = 0f;
        if (peepCamera != null) peepCamera.enabled = false;
        if (figure != null) figure.SetActive(false);
        if (leafHinge != null) leafHinge.localRotation = hingeClosed;
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
