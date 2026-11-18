using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 가라앉은 집들의 도시(기획서 §6-3f 예정): 켜지는 순간부터 물이 차오른다.
/// 처음엔 발목까지 천천히, 그다음 빨라져서 duration초 만에 endY까지 찬다(Fill 곡선).
/// 카메라가 도시 안에서 수면 아래에 있으면 물속 Volume을 켠다. 구명 튜브 같은 뜨는 물건은 물이 닿으면 수면을 따라 올라간다.
/// 찾기 규칙(GazeTarget.showOnFound)으로 켜고, 꺼지면(개발용 방 옮기기의 ResetGaze) 처음 상태로 돌아간다.
/// </summary>
public class FloodRise : MonoBehaviour
{
    [Tooltip("올라가는 수면(가로로 넓은 얇은 판).")]
    public Transform surface;
    [Tooltip("물이 시작하는 높이와 다 찬 높이(월드 y).")]
    public float startY = -0.3f, endY = 70f;
    [Tooltip("다 차는 데 걸리는 시간(초).")]
    public float duration = 40f;
    [Tooltip("처음 느린 정도. 클수록 오래 발목에 머물다 나중에 빨라진다.")]
    public float easePower = 2.5f;
    [Tooltip("물속일 때 켜는 Volume(무게 0 → 1).")]
    public Volume underwater;
    [Tooltip("도시 범위. 카메라가 이 안에 있고 수면 아래일 때만 물속 화면이 된다.")]
    public Bounds city;
    [Tooltip("물에 뜨는 물건. 수면이 닿으면 수면을 따라 올라간다.")]
    public Transform[] floaters = new Transform[0];
    [Tooltip("뜬 물건이 수면 아래로 잠기는 깊이와 출렁임.")]
    public float floatDepth = 0.15f, bobHeight = 0.12f, bobPeriod = 3.2f;

    /// <summary>지금 수면 높이.</summary>
    public float WaterY { get; private set; }
    /// <summary>다 찼는지.</summary>
    public bool Full => elapsed >= duration;

    float elapsed;
    Vector3[] floatStart = new Vector3[0];
    Camera cam;

    /// <summary>0~1 진행도를 0~1 수위로 바꾼다. 처음은 느리고 끝으로 갈수록 빠르다.</summary>
    public static float Fill(float t, float power) => Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.01f, power));

    void Awake()
    {
        floatStart = new Vector3[floaters.Length];
        for (int i = 0; i < floaters.Length; i++) if (floaters[i] != null) floatStart[i] = floaters[i].position;
    }

    void OnEnable()
    {
        elapsed = 0f;
        Apply(startY);
    }

    void OnDisable()
    {
        // 처음 상태: 마른 도시.
        for (int i = 0; i < floaters.Length && i < floatStart.Length; i++) if (floaters[i] != null) floaters[i].position = floatStart[i];
        if (underwater != null) underwater.weight = 0f;
        if (surface != null) SetSurface(startY);
    }

    void Update()
    {
        elapsed = Mathf.Min(elapsed + Time.deltaTime, duration);
        Apply(Mathf.Lerp(startY, endY, Fill(elapsed / duration, easePower)));
    }

    void Apply(float y)
    {
        WaterY = y;
        SetSurface(y);
        float bob = Mathf.Sin(Time.time * Mathf.PI * 2f / bobPeriod);
        for (int i = 0; i < floaters.Length && i < floatStart.Length; i++)
        {
            var f = floaters[i];
            if (f == null) continue;
            float onWater = y - floatDepth + bob * bobHeight * (i % 2 == 0 ? 1f : -0.8f);
            var p = floatStart[i];
            f.position = new Vector3(p.x, Mathf.Max(p.y, onWater), p.z);
        }
        if (cam == null) cam = Camera.main;
        if (underwater != null && cam != null)
        {
            var c = cam.transform.position;
            underwater.weight = city.Contains(c) && c.y < y ? 1f : 0f;
        }
    }

    void SetSurface(float y)
    {
        if (surface == null) return;
        var p = surface.position;
        surface.position = new Vector3(p.x, y, p.z);
    }
}
