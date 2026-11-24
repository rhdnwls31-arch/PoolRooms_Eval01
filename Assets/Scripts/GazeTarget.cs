using System.Collections;
using UnityEngine;

/// <summary>
/// 방의 "이상한 점". 플레이어가 화면 한가운데에 두고 잠시 바라보면 찾은 것으로 친다(기획서 §4-1).
/// 찾으면 한 번 반응(깜빡임, 불 꺼짐, 떨어짐)하고, 연결된 출구(<see cref="RevealWhenExplored"/>)에 알린다.
/// 키 입력과 화면 안내는 없다. 오래 못 찾으면 아주 살짝 깜빡여 알려 준다.
/// swapIn/swapOut을 채우면 "화면 밖에 있는 동안 바뀌는" 이상한 점이 된다(노란 방 인형). 바뀐 뒤에만 찾을 수 있다.
/// </summary>
public class GazeTarget : MonoBehaviour
{
    [Header("판정")]
    [Tooltip("출구 쪽에 알릴 이름. RevealWhenExplored.requiredZones에 같은 이름이 있어야 한다.")]
    public string id = "gaze";
    [Tooltip("찾았을 때 알릴 출구.")]
    public RevealWhenExplored gate;
    [Tooltip("이 거리(미터) 안에서 봐야 찾은 것이다.")]
    public float maxDistance = 6f;
    [Tooltip("화면 한가운데에 이만큼(초) 머물러야 찾은 것이다. 스쳐 보는 것으로는 열리지 않게.")]
    public float dwellTime = 1.5f;
    [Tooltip("판정용 범위를 렌더러 범위보다 이만큼(미터) 넓힌다. 작은 표지도 겨누기 쉽게.")]
    public float boundsPadding = 0.1f;
    [Tooltip("비워 두면 자식 렌더러 전체로 판정한다. 채우면 이것들만 겨눠야 찾은 것이다(천사 눈: 날개 말고 눈알).")]
    public Renderer[] gazeRenderers = new Renderer[0];
    [Tooltip("채우면 렌더러 대신 이 빈 오브젝트의 위치·크기(lossyScale)를 판정 범위로 쓴다(한 덩어리 모델에서 일부만 겨누게 할 때).")]
    public Transform gazeBox;

    [Header("힌트")]
    [Tooltip("이 방의 범위. 플레이어가 안에 있는 동안만 힌트 시간을 센다.")]
    public Bounds roomBounds;
    [Tooltip("방에 들어온 뒤 이만큼(초) 못 찾으면 깜빡이기 시작한다.")]
    public float hintAfter = 120f;
    [Tooltip("힌트 깜빡임 간격(초).")]
    public float hintInterval = 4f;
    [Tooltip("힌트로 꺼져 있는 시간(초).")]
    public float hintFlash = 0.12f;

    [Header("화면 밖에서 바뀌기(선택)")]
    [Tooltip("바뀔 때 끄는 것.")]
    public GameObject[] swapOut = new GameObject[0];
    [Tooltip("바뀔 때 켜는 것. 처음에는 꺼 둔다.")]
    public GameObject[] swapIn = new GameObject[0];
    [Tooltip("한 번 본 뒤 화면 밖에 이만큼(초) 있으면 바뀐다.")]
    public float unseenToSwap = 3f;

    [Header("찾았을 때 반응")]
    public GameObject[] hideOnFound = new GameObject[0];
    [Tooltip("찾으면 켜는 것. 처음에는 꺼 둔다.")]
    public GameObject[] showOnFound = new GameObject[0];
    [Tooltip("찾으면 껐다 켜는 렌더러(글자 깜빡임, 눈 깜빡임).")]
    public Renderer[] blinkRenderers = new Renderer[0];
    [Tooltip("찾으면 껐다 켜는 오브젝트(렌더러 대신 오브젝트째 깜빡일 때).")]
    public GameObject[] blinkObjects = new GameObject[0];
    public int blinkCount = 0;
    public float blinkOff = 0.15f;
    [Tooltip("찾으면 바닥으로 떨어지는 것(열쇠 등).")]
    public Transform dropTarget;
    public float dropToY;
    [Tooltip("찾은 뒤 hideOnFound/showOnFound를 바꾸기까지 기다리는 시간(초). 섬뜩한 것이 바로 사라지지 않게.")]
    public float hideDelay = 0f;
    [Tooltip("참이면 hideDelay가 지난 뒤에도 이 대상이 화면에서 벗어날 때까지 기다렸다가 hideOnFound/showOnFound를 바꾼다(보는 앞에서는 안 바뀐다).")]
    public bool changeWhenUnseen = false;
    [Tooltip("찾은 뒤 출구에 알리기까지 기다리는 시간(초).")]
    public float notifyDelay = 0f;
    [Tooltip("0보다 크면 hideOnFound는 이 시간(초)에 걸쳐 스르르 사라지고 showOnFound는 스르르 나타난다(각 오브젝트의 FadeAway). 2026-11-24 깜빡임 대신.")]
    public float fadeTime = 0f;

    /// <summary>찾았는지.</summary>
    public bool Found { get; private set; }
    /// <summary>화면 밖에서 바뀌었는지(바뀌는 이상한 점일 때).</summary>
    public bool Swapped { get; private set; }
    /// <summary>화면 밖에서 바뀌어야 찾을 수 있는 이상한 점인지.</summary>
    public bool NeedsSwap => swapIn.Length > 0 || swapOut.Length > 0;

    float dwell, unseen, roomTime, hintClock;
    bool seen, hintOff;
    Vector3 dropStart;
    Camera cam;

    void Awake()
    {
        if (dropTarget != null) dropStart = dropTarget.localPosition;
        ApplyState(false, false);
    }

    void Update()
    {
        if (Found) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        var b = CurrentBounds();
        if (b.size == Vector3.zero) return;

        if (NeedsSwap && !Swapped)
        {
            bool inView = GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b);
            if (inView && Vector3.Distance(cam.transform.position, b.center) <= maxDistance * 2f) seen = true;
            if (SwapStep(ref unseen, seen, inView, Time.deltaTime, unseenToSwap)) Swap();
        }
        else if (Accumulate(ref dwell, IsGazedAt(cam, b), Time.deltaTime, dwellTime))
        {
            SetHintVisible(true);
            StartCoroutine(OnFound());
            return;
        }

        UpdateHint(cam.transform.position);
    }

    /// <summary>바라본 시간을 쌓고, 다 찼으면 참. 눈을 떼면 처음부터 다시 센다.</summary>
    public static bool Accumulate(ref float dwell, bool gazing, float dt, float need)
    {
        dwell = gazing ? dwell + dt : 0f;
        return dwell >= need;
    }

    /// <summary>한 번 본 뒤 화면 밖에 있던 시간을 쌓고, 다 찼으면 참. 다시 화면에 들어오면 처음부터 센다.</summary>
    public static bool SwapStep(ref float unseen, bool seenOnce, bool inView, float dt, float need)
    {
        unseen = seenOnce && !inView ? unseen + dt : 0f;
        return unseen >= need;
    }

    /// <summary>화면 한가운데 광선이 범위에 닿는지. maxDistance 안이고, 그 사이를 다른 물체가 가리지 않아야 한다.</summary>
    public static bool RayReaches(Ray ray, Bounds b, float maxDistance, float occluderDistance)
    {
        if (!b.IntersectRay(ray, out float d)) return false;
        return d <= maxDistance && d <= occluderDistance + 0.05f;
    }

    bool IsGazedAt(Camera c, Bounds b)
    {
        var ray = new Ray(c.transform.position, c.transform.forward);
        float occ = float.PositiveInfinity;
        if (Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
            occ = hit.distance;
        return RayReaches(ray, b, maxDistance, occ);
    }

    Bounds CurrentBounds()
    {
        if (gazeBox != null)
        {
            // 렌더러 없이 판정 범위만 정한다: 위치 = 가운데, lossyScale = 크기(축 정렬 상자로 본다)
            var gb = new Bounds(gazeBox.position, gazeBox.lossyScale);
            gb.Expand(boundsPadding * 2f);
            return gb;
        }
        Bounds b = default; bool any = false;
        var rs = gazeRenderers.Length > 0 ? gazeRenderers : GetComponentsInChildren<Renderer>(false);
        foreach (var r in rs)
        {
            if (r == null || !r.gameObject.activeInHierarchy) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (any) b.Expand(boundsPadding * 2f);
        return b;
    }

    void UpdateHint(Vector3 playerPos)
    {
        if (!roomBounds.Contains(playerPos)) { if (hintOff) SetHintVisible(true); return; }
        roomTime += Time.deltaTime;
        if (roomTime < hintAfter) return;
        hintClock += Time.deltaTime;
        float t = Mathf.Repeat(hintClock, hintInterval);
        bool off = t < hintFlash;
        if (off != hintOff) SetHintVisible(!off);
    }

    void SetHintVisible(bool visible)
    {
        hintOff = !visible;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r.gameObject.activeInHierarchy) r.enabled = visible;
    }

    void Swap()
    {
        Swapped = true;
        foreach (var go in swapOut) if (go != null) go.SetActive(false);
        foreach (var go in swapIn) if (go != null) go.SetActive(true);
    }

    IEnumerator OnFound()
    {
        Found = true;
        float start = Time.time;
        if (hideDelay <= 0f && !changeWhenUnseen) StartCoroutine(ApplyFound());
        if (notifyDelay <= 0f && gate != null) gate.MarkVisited(id);

        if (dropTarget != null)
        {
            Vector3 from = dropTarget.position, to = new Vector3(from.x, dropToY, from.z);
            float v = 0f;
            while (dropTarget.position.y > to.y)
            {
                v += 9.81f * Time.deltaTime;
                dropTarget.position = Vector3.MoveTowards(dropTarget.position, to, v * Time.deltaTime);
                yield return null;
            }
        }
        for (int i = 0; i < blinkCount; i++)
        {
            foreach (var r in blinkRenderers) if (r != null) r.enabled = false;
            foreach (var o in blinkObjects) if (o != null) o.SetActive(false);
            yield return new WaitForSeconds(blinkOff);
            foreach (var r in blinkRenderers) if (r != null) r.enabled = true;
            foreach (var o in blinkObjects) if (o != null) o.SetActive(true);
            yield return new WaitForSeconds(blinkOff * 1.5f);
        }
        if (hideDelay > 0f || changeWhenUnseen)
        {
            while (Time.time - start < hideDelay) yield return null;
            if (changeWhenUnseen)
            {
                var b = CurrentBounds();
                while (cam != null && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b)) yield return null;
            }
            StartCoroutine(ApplyFound());
        }
        if (notifyDelay > 0f)
        {
            while (Time.time - start < notifyDelay) yield return null;
            if (gate != null) gate.MarkVisited(id);
        }
    }

    /// <summary>찾은 뒤 상태로 바꾼다. fadeTime이 있으면 사라질 것은 스르르 지우고 나타날 것은 스르르 띄운 뒤 확정한다.</summary>
    IEnumerator ApplyFound()
    {
        if (fadeTime <= 0f) { ApplyState(Swapped, true); yield break; }
        bool any = false;
        foreach (var go in showOnFound)
        {
            if (go == null) continue;
            go.SetActive(true);
            var f = go.GetComponent<FadeAway>();
            if (f != null) { f.Begin(0f, 1f, fadeTime, false); any = true; }
        }
        foreach (var go in hideOnFound)
        {
            if (go == null || !go.activeInHierarchy) continue;
            var f = go.GetComponent<FadeAway>();
            if (f != null) { f.Begin(1f, 0f, fadeTime, true); any = true; }
            else go.SetActive(false);
        }
        if (any) yield return new WaitForSeconds(fadeTime + 0.05f);
        ApplyState(Swapped, true);
    }

    void RestoreFades()
    {
        foreach (var go in hideOnFound) if (go != null) { var f = go.GetComponent<FadeAway>(); if (f != null) f.Restore(); }
        foreach (var go in showOnFound) if (go != null) { var f = go.GetComponent<FadeAway>(); if (f != null) f.Restore(); }
    }

    /// <summary>처음 상태로 되돌린다(개발용 방 옮기기에서 쓴다).</summary>
    public void ResetGaze()
    {
        StopAllCoroutines();
        RestoreFades();
        Found = false; Swapped = false; seen = false;
        dwell = unseen = roomTime = hintClock = 0f;
        SetHintVisible(true);
        if (dropTarget != null) dropTarget.localPosition = dropStart;
        foreach (var r in blinkRenderers) if (r != null) r.enabled = true;
        foreach (var o in blinkObjects) if (o != null) o.SetActive(true);
        ApplyState(false, false);
    }

    /// <summary>반응 없이 찾은 뒤 상태로 바로 만든다(개발용 방 옮기기에서 쓴다).</summary>
    public void CompleteSilently()
    {
        StopAllCoroutines();
        RestoreFades();
        Found = true; Swapped = NeedsSwap;
        SetHintVisible(true);
        ApplyState(Swapped, true);
        if (dropTarget != null) dropTarget.position = new Vector3(dropTarget.position.x, dropToY, dropTarget.position.z);
    }

    void ApplyState(bool swapped, bool found)
    {
        foreach (var go in swapOut) if (go != null) go.SetActive(!swapped);
        foreach (var go in swapIn) if (go != null) go.SetActive(swapped);
        foreach (var go in hideOnFound) if (go != null) go.SetActive(!found);
        foreach (var go in showOnFound) if (go != null) go.SetActive(found);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Found ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(roomBounds.center, roomBounds.size);
    }
}
