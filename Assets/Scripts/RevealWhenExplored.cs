using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 처음에는 벽으로 막혀 있던 출구를, 플레이어가 정해진 곳들을 모두 둘러본 뒤 화면 밖에 있을 때 연다.
/// 인트로에서 가운데 출구만 보고 바로 나가지 않고 옆 공간들을 구경하게 만든다(2026-11-18 사용자 아이디어).
/// 둘러볼 곳은 <see cref="VisitZone"/> 트리거가 알려 준다. 열린 뒤에는 다시 닫히지 않는다.
/// </summary>
public class RevealWhenExplored : MonoBehaviour
{
    [Tooltip("출구를 막고 있는 벽(콜라이더 포함). 열 때 끈다.")]
    public GameObject wall;
    [Tooltip("벽이 차지하는 월드 범위. 화면에 보이는지 판정할 때 쓴다.")]
    public Bounds wallBounds;
    [Tooltip("둘러봐야 하는 곳의 이름. VisitZone.zoneId와 같아야 한다.")]
    public string[] requiredZones = new string[0];
    [Tooltip("열 때 함께 켤 오브젝트(통로 안 조명 등).")]
    public GameObject[] showOnReveal = new GameObject[0];
    [Tooltip("몇 번째 방의 출구인지(인트로 0, 수영장 홀 1, ...). 개발용 방 옮기기에서 앞 방 출구를 열어 둘 때 쓴다.")]
    public int order;

    [Header("보는 앞에서 열기(2026-11-24)")]
    [Tooltip("0보다 크면 화면에 보여도 연다. 벽이 이 시간(초)에 걸쳐 스르르 걷히고(벽의 FadeAway) spill 빛이 켜진다. 0이면 예전처럼 벽이 화면 밖일 때 한 번에 열린다.")]
    public float openTime = 0f;
    [Tooltip("열릴 때 켜지며 밝아지는 빛(문틈에서 새는 빛). 처음에는 꺼 둔다.")]
    public Light[] spill = new Light[0];

    float[] spillBase = new float[0];

    /// <summary>출구가 열렸는지.</summary>
    public bool Revealed { get; private set; }
    /// <summary>지금까지 둘러본 곳 수.</summary>
    public int VisitedCount => visited.Count;

    readonly HashSet<string> visited = new HashSet<string>();
    Camera cam;

    void Awake()
    {
        if (wall != null) wall.SetActive(true);
        foreach (var go in showOnReveal) if (go != null) go.SetActive(false);
        spillBase = new float[spill.Length];
        for (int i = 0; i < spill.Length; i++) if (spill[i] != null) { spillBase[i] = spill[i].intensity; spill[i].gameObject.SetActive(false); }
    }

    /// <summary>VisitZone이 플레이어가 들어왔을 때 부른다.</summary>
    public void MarkVisited(string zoneId)
    {
        if (!string.IsNullOrEmpty(zoneId)) visited.Add(zoneId);
    }

    /// <summary>필요한 곳을 모두 둘러봤는지.</summary>
    public bool AllVisited()
    {
        foreach (var z in requiredZones) if (!visited.Contains(z)) return false;
        return true;
    }

    /// <summary>열어도 되는지. 모두 둘러봤고, 벽 자리가 화면에 없을 때만 참이다.</summary>
    public bool CanReveal(bool wallInView) => !Revealed && AllVisited() && (openTime > 0f || !wallInView);

    void Update()
    {
        if (Revealed || !AllVisited()) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (CanReveal(IsWallVisible(cam))) Reveal();
    }

    /// <summary>
    /// 카메라에 벽이 실제로 보이는지. 화면 범위 안에 있고, 벽의 가운데·네 귀퉁이 중 하나라도 가리는 것 없이 닿으면 보인다.
    /// 화면 범위만 보면 다른 벽 너머 먼 곳에서도 "보인다"가 되어 열리지 않는다.
    /// </summary>
    public bool IsWallVisible(Camera c)
    {
        if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(c), wallBounds)) return false;
        Vector3 from = c.transform.position, ctr = wallBounds.center, e = wallBounds.extents * 0.8f;
        var points = new[] { ctr, ctr + new Vector3(e.x, e.y, 0), ctr + new Vector3(-e.x, e.y, 0), ctr + new Vector3(e.x, -e.y, 0), ctr + new Vector3(-e.x, -e.y, 0) };
        foreach (var p in points)
        {
            if (!Physics.Linecast(from, p, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            if (wall != null && hit.collider.transform.IsChildOf(wall.transform)) return true;
        }
        return false;
    }

    /// <summary>벽을 끄고 통로를 연다. instant면 천천히 걷히지 않고 바로 연다(개발용 방 옮기기).</summary>
    public void Reveal(bool instant = false)
    {
        Revealed = true;
        foreach (var go in showOnReveal) if (go != null) go.SetActive(true);
        if (openTime > 0f && !instant && isActiveAndEnabled) { StartCoroutine(Open()); return; }
        if (wall != null) wall.SetActive(false);
        SetSpill(1f);
    }

    System.Collections.IEnumerator Open()
    {
        var fade = wall != null ? wall.GetComponent<FadeAway>() : null;
        if (fade != null && wall.activeInHierarchy) fade.Begin(1f, 0f, openTime, true);
        // 빛은 벽보다 조금 먼저 밝아져 틈으로 새는 것처럼 보이게 한다.
        for (float t = 0f; t < openTime; t += Time.deltaTime)
        {
            SetSpill(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (openTime * 0.7f))));
            yield return null;
        }
        SetSpill(1f);
        if (wall != null && (fade == null || !fade.Running)) wall.SetActive(false);
    }

    void SetSpill(float k)
    {
        for (int i = 0; i < spill.Length; i++)
        {
            if (spill[i] == null || i >= spillBase.Length) continue;
            spill[i].gameObject.SetActive(k > 0f);
            spill[i].intensity = spillBase[i] * k;
        }
    }

    /// <summary>처음 상태(닫힘, 아무 곳도 안 둘러봄)로 되돌린다. 개발용 방 옮기기에서 쓴다.</summary>
    public void ResetReveal()
    {
        StopAllCoroutines();
        Revealed = false;
        visited.Clear();
        if (wall != null) wall.SetActive(true);
        foreach (var go in showOnReveal) if (go != null) go.SetActive(false);
        SetSpill(0f);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Revealed ? Color.green : Color.cyan;
        Gizmos.DrawWireCube(wallBounds.center, wallBounds.size);
    }
}
