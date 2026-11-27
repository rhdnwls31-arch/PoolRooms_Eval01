using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 물 소리(2026-11-27 사운드 2차 피드백: 물에 들어갔을 때 소리가 나야 한다). 플레이어에 붙인다.
/// 씬의 HDRP 물(WaterSurface, 수영장·수로·가라앉은 도시의 차오르는 물)마다 수면 높이와 넓이를 보고,
/// 발이 수면보다 enterDepth 넘게 잠기면 첨벙 소리를 한 번 내고 <see cref="InWater"/>를 켠다(발소리는 물 헤치는 소리로 바뀐다).
/// 카메라가 수면 아래로 내려가면 귀에 들리는 소리 전체를 먹먹하게(낮은 소리만) 만든다. 샤워실처럼 얕게 고인 물은 들어간 것으로 치지 않는다.
/// </summary>
public class WaterAudio : MonoBehaviour
{
    [Tooltip("발이 수면보다 이만큼(m) 넘게 잠기면 물에 들어간 것이다.")]
    public float enterDepth = 0.25f;
    [Tooltip("첨벙 소리 사이 최소 간격(초).")]
    public float splashCooldown = 1.2f;
    [Tooltip("물속에서 들리는 소리의 위쪽 한계(Hz). 낮을수록 먹먹하다.")]
    public float underwaterCutoff = 900f;

    /// <summary>지금 물에 들어가 있는지(발이 잠김).</summary>
    public bool InWater { get; private set; }
    /// <summary>지금 카메라가 물속인지.</summary>
    public bool CameraUnderwater { get; private set; }

    WaterSurface[] waters = new WaterSurface[0];
    float lastSplash = -10f;
    AudioLowPassFilter lowPass;

    void Start()
    {
        waters = FindObjectsByType<WaterSurface>(FindObjectsInactive.Include);
    }

    void Update()
    {
        var cam = Camera.main;
        Vector3 foot = transform.position;
        bool inWater = false, camUnder = false;
        foreach (var w in waters)
        {
            if (w == null || !w.gameObject.activeInHierarchy) continue; // 먼 방이라 꺼 둔 물(RoomCulling)은 enabled만 꺼지고 그대로 센다
            var t = w.transform;
            float y = t.position.y;
            if (Inside(t.position, t.lossyScale, t.rotation, foot) && Depth(y, foot.y) >= enterDepth) inWater = true;
            if (cam != null && cam.transform.position.y < y && Inside(t.position, t.lossyScale, t.rotation, cam.transform.position)) camUnder = true;
        }
        if (inWater && !InWater && Time.time - lastSplash >= splashCooldown)
        {
            lastSplash = Time.time;
            GameAudio.Play(Cue.WaterEnter, foot);
        }
        InWater = inWater;
        CameraUnderwater = camUnder;
        UpdateMuffle(cam);
    }

    void UpdateMuffle(Camera cam)
    {
        if (lowPass == null)
        {
            if (!camUnderOnce && !CameraUnderwater) return; // 물에 한 번도 안 들어갔으면 필터를 만들지 않는다
            var listener = cam != null ? cam.GetComponent<AudioListener>() : FindAnyObjectByType<AudioListener>();
            if (listener == null) return;
            lowPass = listener.GetComponent<AudioLowPassFilter>() ?? listener.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
        }
        camUnderOnce = true;
        float target = CameraUnderwater ? underwaterCutoff : 22000f;
        // 로그 눈금으로 천천히(약 0.3초) 바꿔 뚝 끊기지 않게
        float lf = Mathf.Log(lowPass.cutoffFrequency), lt = Mathf.Log(target);
        lowPass.cutoffFrequency = Mathf.Exp(Mathf.MoveTowards(lf, lt, Time.unscaledDeltaTime * 10f));
    }
    bool camUnderOnce;

    /// <summary>점 p가 물 판(가운데 center, 크기 scale의 x·z, 방향 rot) 위·아래 범위 안인지(높이는 보지 않는다).</summary>
    public static bool Inside(Vector3 center, Vector3 scale, Quaternion rot, Vector3 p)
    {
        Vector3 l = Quaternion.Inverse(rot) * (p - center);
        return Mathf.Abs(l.x) <= Mathf.Abs(scale.x) * 0.5f && Mathf.Abs(l.z) <= Mathf.Abs(scale.z) * 0.5f;
    }

    /// <summary>수면 높이 waterY에서 발 높이 footY까지 잠긴 깊이(음수면 물 위).</summary>
    public static float Depth(float waterY, float footY) => waterY - footY;
}
