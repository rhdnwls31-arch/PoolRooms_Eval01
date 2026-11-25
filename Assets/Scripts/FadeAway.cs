using System.Collections;
using UnityEngine;

/// <summary>
/// 오브젝트를 천천히 투명하게 지우거나(사라짐) 나타나게 한다. 2026-11-24 사용자 피드백: 찾기 규칙의 깜빡임이 별로라
/// 찾은 이상한 점은 스르르 사라지고, 출구 벽도 보는 앞에서 걷히게 바꿨다(GazeTarget.fadeTime, RevealWhenExplored.openTime).
/// 불투명 재질은 알파로 지울 수 없어서, 편집기에서 만든 투명 짝 재질(fadeMaterials)로 잠깐 바꿔 끼운 뒤 알파를 내린다.
/// 끝나면(또는 꺼지면) 원래 재질과 빛 세기로 되돌린다. 짝 재질이 없는 칸은 끝날 때 한 번에 꺼진다.
/// </summary>
public class FadeAway : MonoBehaviour
{
    [Tooltip("지울 렌더러들.")]
    public Renderer[] renderers = new Renderer[0];
    [Tooltip("renderers의 재질 칸을 차례로 펼친 투명 짝 재질. 비어 있는 칸은 지우지 않고 끝에 꺼진다.")]
    public Material[] fadeMaterials = new Material[0];
    [Tooltip("함께 어두워지는 빛.")]
    public Light[] lights = new Light[0];
    [Tooltip("지우는 동안 이만큼(비율) 키운다. 같은 자리 바꿈(사람 든 침대 → 빈 침대)에서 밑에 켜진 새 모델과 면이 겹쳐 " +
             "번갈아 보이며 깨져 보였다(2026-11-25). 살짝 키우고 띄우면 옛 모델이 늘 바깥에 있다.")]
    public float swell = 0f;
    [Tooltip("지우는 동안 이만큼(m) 위로 띄운다.")]
    public float lift = 0f;

    static readonly string[] ColorProps = { "_BaseColor", "_UnlitColor", "_Color" };

    Material[][] originals;
    // 바꿔 끼운 재질 칸(렌더러별). 한 번 만들면 계속 쓴다(바라볼 때마다 새로 만들고 지우면 끊겼다, 2026-11-25).
    Material[][] swappedCache;
    Material[] live;
    float[] liveAlpha;
    string[] liveProp;
    float[] lightBase;
    Coroutine running;
    float alpha = 1f;
    Vector3 baseScale, basePos;
    bool moved;

    /// <summary>지금 진행 중인지.</summary>
    public bool Running => running != null;
    /// <summary>지금 보이는 정도(1 = 원래, 0 = 안 보임).</summary>
    public float Alpha => originals == null ? 1f : alpha;

    /// <summary>
    /// 바라보는 동안 미리 조금 옅어지기(2026-11-25: 응시하면 반응한다는 것을 알게). a가 1이면 원래대로 되돌린다.
    /// 진행 중인 Begin이 있으면 건드리지 않는다.
    /// </summary>
    public void Preview(float a)
    {
        if (running != null || !isActiveAndEnabled) return;
        if (a >= 0.999f) { Restore(); return; }
        SwapIn();
        Apply(a);
    }

    /// <summary>from에서 to까지 알파(0 = 안 보임, 1 = 원래)를 time초에 걸쳐 바꾼다. deactivateAtEnd면 끝에 오브젝트를 끈다.</summary>
    public void Begin(float from, float to, float time, bool deactivateAtEnd)
    {
        if (!isActiveAndEnabled) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Run(from, to, Mathf.Max(0.01f, time), deactivateAtEnd));
    }

    IEnumerator Run(float from, float to, float time, bool deactivateAtEnd)
    {
        SwapIn();
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            Apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time)));
            yield return null;
        }
        Apply(to);
        running = null;
        if (deactivateAtEnd) gameObject.SetActive(false); // OnDisable이 원래대로 되돌린다
        else Restore();
    }

    void SwapIn()
    {
        if (originals != null) return;
        originals = new Material[renderers.Length][];
        bool build = swappedCache == null;
        if (build) swappedCache = new Material[renderers.Length][];
        var list = build ? new System.Collections.Generic.List<Material>() : null;
        int k = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) { originals[i] = null; continue; }
            originals[i] = r.sharedMaterials;
            if (build)
            {
                var swapped = new Material[originals[i].Length];
                for (int s = 0; s < swapped.Length; s++, k++)
                {
                    var twin = k < fadeMaterials.Length ? fadeMaterials[k] : null;
                    if (twin == null) { swapped[s] = originals[i][s]; continue; }
                    swapped[s] = new Material(twin) { name = twin.name + " (fading)" };
                    list.Add(swapped[s]);
                }
                swappedCache[i] = swapped;
            }
            if (swappedCache[i] != null) r.sharedMaterials = swappedCache[i];
        }
        if (build)
        {
            live = list.ToArray();
            liveAlpha = new float[live.Length];
            liveProp = new string[live.Length];
            for (int i = 0; i < live.Length; i++)
                foreach (var p in ColorProps)
                    if (live[i].HasProperty(p)) { liveProp[i] = p; liveAlpha[i] = live[i].GetColor(p).a; break; }
        }
        lightBase = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lightBase[i] = lights[i].intensity;
        if (swell > 0f || lift > 0f)
        {
            baseScale = transform.localScale; basePos = transform.position; moved = true;
            transform.localScale = baseScale * (1f + swell);
            transform.position = basePos + Vector3.up * lift;
        }
    }

    void Apply(float a)
    {
        alpha = a;
        for (int i = 0; i < live.Length; i++)
        {
            if (liveProp[i] == null) continue;
            var c = live[i].GetColor(liveProp[i]); c.a = liveAlpha[i] * a; live[i].SetColor(liveProp[i], c);
        }
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = lightBase[i] * a;
    }

    /// <summary>원래 재질과 빛 세기로 되돌린다.</summary>
    public void Restore()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        if (originals == null) return;
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null && originals[i] != null) renderers[i].sharedMaterials = originals[i];
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = lightBase[i];
        Apply(1f); // 다음에 다시 끼울 때 원래 알파에서 시작
        originals = null; alpha = 1f;
        if (moved) { transform.localScale = baseScale; transform.position = basePos; moved = false; }
    }

    void OnDisable() => Restore();

    void OnDestroy()
    {
        if (live != null) foreach (var m in live) if (m != null) Destroy(m);
    }
}
