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

    [Tooltip("StartDrift 뒤 뜬 물건이 목표 쪽으로 흘러가는 속도(m/s)와 목표 둘레에 모이는 반지름(m).")]
    public float driftSpeed = 0.9f, driftGatherRadius = 3f;

    /// <summary>지금 수면 높이.</summary>
    public float WaterY { get; private set; }
    /// <summary>다 찼는지.</summary>
    public bool Full => elapsed >= duration;

    float elapsed;
    Vector3[] floatStart = new Vector3[0];
    Vector3[] floatOrigin = new Vector3[0]; // 처음 자리(흘러가도 그대로)
    bool drifting;
    Vector3 driftTarget;

    /// <summary>뜬 물건들을 target(월드, 높이는 무시) 쪽으로 천천히 흘려보낸다. 출구가 열렸다는 걸 눈으로 알려 줄 때 쓴다.</summary>
    public void StartDrift(Vector3 target) { drifting = true; driftTarget = target; }

    /// <summary>흘려보내기를 멈춘다(물건은 지금 자리에 머문다).</summary>
    public void StopDrift() => drifting = false;
    Camera cam;

    /// <summary>0~1 진행도를 0~1 수위로 바꾼다. 처음은 느리고 끝으로 갈수록 빠르다.</summary>
    public static float Fill(float t, float power) => Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.01f, power));

    void Awake()
    {
        floatStart = new Vector3[floaters.Length];
        for (int i = 0; i < floaters.Length; i++) if (floaters[i] != null) floatStart[i] = floaters[i].position;
        floatOrigin = (Vector3[])floatStart.Clone();
    }

    void OnEnable()
    {
        elapsed = 0f;
        Apply(startY);
    }

    void OnDisable()
    {
        // 처음 상태: 마른 도시.
        drifting = false;
        for (int i = 0; i < floaters.Length && i < floatOrigin.Length; i++) { floatStart[i] = floatOrigin[i]; if (floaters[i] != null) floaters[i].position = floatOrigin[i]; }
        if (underwater != null) underwater.weight = 0f;
        if (surface != null) SetSurface(startY);
    }

    void Update()
    {
        elapsed = Mathf.Min(elapsed + Time.deltaTime, duration);
        if (drifting) Drift(Time.deltaTime);
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

    /// <summary>뜬 물건의 가로 자리를 목표 쪽으로 옮긴다. 물건마다 목표 둘레의 다른 자리로 가서 한 점에 겹치지 않는다.</summary>
    void Drift(float dt)
    {
        for (int i = 0; i < floatStart.Length; i++)
        {
            float a = i * 2.399f; // 황금각으로 둘레에 흩는다
            var goal = new Vector3(driftTarget.x + Mathf.Cos(a) * driftGatherRadius * (0.4f + 0.6f * ((i * 7) % 5) / 4f), 0f,
                                   driftTarget.z + Mathf.Sin(a) * driftGatherRadius * (0.4f + 0.6f * ((i * 3) % 5) / 4f));
            var cur = new Vector3(floatStart[i].x, 0f, floatStart[i].z);
            var next = Vector3.MoveTowards(cur, goal, driftSpeed * dt);
            floatStart[i] = new Vector3(next.x, floatStart[i].y, next.z);
        }
    }

    void SetSurface(float y)
    {
        if (surface == null) return;
        var p = surface.position;
        surface.position = new Vector3(p.x, y, p.z);
    }
}
