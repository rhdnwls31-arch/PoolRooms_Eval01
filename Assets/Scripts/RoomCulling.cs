using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

/// <summary>
/// 멀리 있는 방은 그리지 않는다(2026-11-25 성능 단계). 한 씬에 모든 방(약 430만 삼각형, 렌더러 3천여 개)이 다 켜져 있어,
/// 보이지 않는 먼 방까지 그림자·깊이 단계에서 그리느라 그래픽 카드가 프레임마다 1.5~4ms를 더 썼다.
/// 씬 맨 위 오브젝트(방 하나씩)를 묶음으로 보고, 카메라에서 그 묶음 범위까지 거리가 <see cref="cullDistance"/>보다 멀면
/// 렌더러를 forceRenderingOff로 끄고 물(WaterSurface)도 끈다. 게임 스크립트가 쓰는 renderer.enabled·SetActive는 건드리지 않는다.
/// 실행 인수 --noculling이면 끈다(비교 점검용).
/// </summary>
public class RoomCulling : MonoBehaviour
{
    [Tooltip("카메라에서 방 범위까지 이보다 멀면 그 방을 그리지 않는다(m). 열린 문 너머로 보이는 옆 방이 사라지지 않게 넉넉히.")]
    public float cullDistance = 30f;
    [Tooltip("묶음에서 뺄 맨 위 오브젝트 이름(플레이어, 화면 UI, 볼륨 등 그릴 것이 없는 것).")]
    public string[] skipRoots = { "Player", "FadeCanvas", "PauseUI", "Lighting" };
    [Tooltip("멀리서도 보이는 방(넓은 마당 너머 출구 등). 이 방들은 farDistance까지 그린다.")]
    public string[] farRoots = new string[0];
    [Tooltip("farRoots 방을 그리는 거리(m).")]
    public float farDistance = 90f;

    /// <summary>이 방 범위 안에 카메라가 있으면 hideRoots 방들을 거리와 상관없이 그리지 않는다(2026-11-28 QA).</summary>
    [System.Serializable]
    public class ExclusiveRoom
    {
        [Tooltip("카메라가 이 맨 위 오브젝트(방)의 범위 안에 있을 때")]
        public string insideRoot;
        [Tooltip("그리지 않을 맨 위 오브젝트(방) 이름")]
        public string[] hideRoots = new string[0];
    }

    [Tooltip("이 방 안에서는 저 방들을 그리지 않는다. 마지막 수영장 벽 위로 가라앉은 도시 껍데기(높이 80m)가 네모 상자로 보였다(2026-11-28 QA). --noculling이어도 적용된다(성능이 아니라 보이는 모습 규칙).")]
    public ExclusiveRoom[] exclusiveRooms = new ExclusiveRoom[0];

    /// <summary>켜져 있는지(점검용 스위치).</summary>
    public static bool Active = true;

    class Group
    {
        public string name;
        public Bounds bounds;
        public Renderer[] renderers;
        public WaterSurface[] waters;
        public bool hidden;
        public float distance;
        public readonly List<WaterSurface> disabledWaters = new List<WaterSurface>();
    }

    readonly List<Group> groups = new List<Group>();
    readonly HashSet<string> excluded = new HashSet<string>();

    /// <summary>지금 그리지 않는 방 수(점검용).</summary>
    public int HiddenCount { get; private set; }

    void Awake()
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--noculling") >= 0) Active = false;
        Build();
    }

    void Build()
    {
        groups.Clear();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root == gameObject || System.Array.IndexOf(skipRoots, root.name) >= 0) continue;
            var rs = root.GetComponentsInChildren<Renderer>(true);
            var ws = root.GetComponentsInChildren<WaterSurface>(true);
            if (rs.Length == 0 && ws.Length == 0) continue;
            if (!TryBounds(rs, ws, out var b)) continue;
            float dist = System.Array.IndexOf(farRoots, root.name) >= 0 ? Mathf.Max(cullDistance, farDistance) : cullDistance;
            groups.Add(new Group { name = root.name, bounds = b, renderers = rs, waters = ws, distance = dist });
        }
    }

    /// <summary>렌더러 범위(꺼진 것도 포함)와 물 위치로 묶음 범위를 만든다.</summary>
    static bool TryBounds(Renderer[] rs, WaterSurface[] ws, out Bounds b)
    {
        b = default; bool any = false;
        foreach (var r in rs)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        foreach (var w in ws)
        {
            var wb = new Bounds(w.transform.position, Vector3.Scale(w.transform.lossyScale, new Vector3(1f, 0.1f, 1f)));
            if (!any) { b = wb; any = true; } else b.Encapsulate(wb);
        }
        return any;
    }

    /// <summary>카메라 위치에서 이 범위의 방을 그릴지. 범위 안이거나 cullDistance 안이면 그린다.</summary>
    public static bool ShouldDraw(Bounds roomBounds, Vector3 eye, float cullDistance)
    {
        return roomBounds.SqrDistance(eye) <= cullDistance * cullDistance;
    }

    /// <summary>
    /// 카메라가 안에 있는 방의 규칙에 따라 그리지 않을 방 이름을 모은다. roomBounds는 방 이름으로 범위를 찾는다(없으면 거짓).
    /// </summary>
    public static void CollectExcluded(ExclusiveRoom[] rules, System.Func<string, (bool found, Bounds bounds)> roomBounds, Vector3 eye, HashSet<string> into)
    {
        into.Clear();
        if (rules == null) return;
        foreach (var rule in rules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.insideRoot) || rule.hideRoots == null) continue;
            var (found, b) = roomBounds(rule.insideRoot);
            if (!found || !b.Contains(eye)) continue;
            foreach (var n in rule.hideRoots) if (!string.IsNullOrEmpty(n) && n != rule.insideRoot) into.Add(n);
        }
    }

    (bool, Bounds) FindGroupBounds(string name)
    {
        foreach (var g in groups) if (g.name == name) return (true, g.bounds);
        return (false, default);
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;
        CollectExcluded(exclusiveRooms, FindGroupBounds, eye, excluded);
        int hiddenNow = 0;
        foreach (var g in groups)
        {
            bool hide = (Active && !ShouldDraw(g.bounds, eye, g.distance)) || excluded.Contains(g.name);
            if (hide) hiddenNow++;
            if (hide == g.hidden) continue;
            g.hidden = hide;
            foreach (var r in g.renderers) if (r != null) r.forceRenderingOff = hide;
            if (hide)
            {
                foreach (var w in g.waters) if (w != null && w.enabled) { w.enabled = false; g.disabledWaters.Add(w); }
            }
            else
            {
                foreach (var w in g.disabledWaters) if (w != null) w.enabled = true;
                g.disabledWaters.Clear();
            }
        }
        HiddenCount = hiddenNow;
    }

    void OnDisable()
    {
        foreach (var g in groups)
        {
            if (!g.hidden) continue;
            foreach (var r in g.renderers) if (r != null) r.forceRenderingOff = false;
            foreach (var w in g.disabledWaters) if (w != null) w.enabled = true;
            g.disabledWaters.Clear();
            g.hidden = false;
        }
    }
}
