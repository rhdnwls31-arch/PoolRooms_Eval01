using UnityEngine;

/// <summary>
/// 물 차오르는 마을에서 출구 문이 열렸을 때 켜진다(RevealWhenExplored.showOnReveal). 2026-11-24 사용자 피드백:
/// 처음 하는 사람은 물이 차면 당황해서 돌아다니니, 소리 없이 눈으로 "문이 열렸다"를 알게 한다.
/// 1) 문 앞에서 수면까지 솟는 빛기둥(이 오브젝트 아래 메쉬)과 빛이 천천히 숨 쉬듯 밝아졌다 어두워진다.
/// 2) 물에 뜬 튜브들이 문 위 수면으로 흘러가 모인다(FloodRise.StartDrift).
/// 게임 규칙에는 관여하지 않는다.
/// </summary>
public class DoorBeacon : MonoBehaviour
{
    [Tooltip("튜브를 띄우는 물. 켜질 때 문 쪽으로 흘려보낸다.")]
    public FloodRise flood;
    [Tooltip("튜브가 모일 곳(월드, 높이는 무시) = 문 바로 앞.")]
    public Vector3 gatherAt;
    [Tooltip("숨 쉬듯 밝기가 바뀌는 빛들.")]
    public Light[] lights = new Light[0];
    [Tooltip("숨 쉬는 빛기둥(가로로만 살짝 부푼다).")]
    public Transform column;
    [Tooltip("한 번 숨 쉬는 시간(초)과 밝기 변화 폭(0~1).")]
    public float period = 3.5f, depth = 0.35f;

    float[] baseIntensity = new float[0];
    Vector3 columnScale;
    float startTime;

    void Awake()
    {
        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null) baseIntensity[i] = lights[i].intensity;
        if (column != null) columnScale = column.localScale;
    }

    void OnEnable()
    {
        startTime = Time.time;
        if (flood != null) flood.StartDrift(gatherAt);
    }

    void OnDisable()
    {
        if (flood != null) flood.StopDrift();
    }

    void Update()
    {
        float t = Time.time - startTime;
        float rise = Mathf.SmoothStep(0f, 1f, t / 1.5f);                       // 열리는 순간 1.5초에 걸쳐 켜진다
        float breath = 1f - depth * 0.5f * (1f - Mathf.Cos(t * 2f * Mathf.PI / period));
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = baseIntensity[i] * rise * breath;
        if (column != null) column.localScale = new Vector3(columnScale.x * (0.92f + 0.08f * breath) * rise, columnScale.y, columnScale.z * (0.92f + 0.08f * breath) * rise);
    }
}
