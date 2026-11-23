using UnityEngine;

/// <summary>
/// 나간 형광등이 가끔 깜빡이게 한다(2026-11-23 노란 방, 사용자 요청).
/// 평소엔 꺼져 있다가 offTime 사이 무작위 시간마다 짧게 몇 번 켜졌다 꺼진다. 끝은 늘 꺼진 상태다.
/// 켜질 때는 lamp를 켜고 tube 재질을 onMaterial로, 꺼질 때는 offMaterial로 바꾼다. 소리는 아직 없다.
/// </summary>
public class LightFlicker : MonoBehaviour
{
    /// <summary>깜빡일 빛. 꺼진 상태로 시작한다.</summary>
    public Light lamp;
    /// <summary>등 관(빛나는 판). 켜지면 onMaterial, 꺼지면 offMaterial.</summary>
    public Renderer tube;
    public Material onMaterial;
    public Material offMaterial;
    /// <summary>깜빡임 묶음 사이 꺼져 있는 시간(초) 범위.</summary>
    public Vector2 offTime = new Vector2(2.5f, 8f);
    /// <summary>한 묶음에서 켜지는 횟수 범위.</summary>
    public Vector2Int blinks = new Vector2Int(1, 4);
    /// <summary>등마다 다르게 깜빡이도록 쓰는 난수 시드.</summary>
    public int seed = 1;

    System.Random rng;
    float next;
    int toggles;
    bool on;

    void OnEnable()
    {
        rng = new System.Random(seed);
        Set(false);
        next = Time.time + Range(offTime.x, offTime.y);
    }

    void Update()
    {
        if (Time.time < next) return;
        if (toggles <= 0) toggles = rng.Next(blinks.x, blinks.y + 1) * 2; // 켜짐·꺼짐 한 쌍씩이라 끝은 꺼짐
        on = !on;
        Set(on);
        toggles--;
        next = Time.time + (toggles > 0 ? Range(0.03f, on ? 0.12f : 0.25f) : Range(offTime.x, offTime.y));
    }

    void Set(bool value)
    {
        on = value;
        if (lamp != null) lamp.enabled = value;
        if (tube != null) tube.sharedMaterial = value ? onMaterial : offMaterial;
    }

    float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
}
