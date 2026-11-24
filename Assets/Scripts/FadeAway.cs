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

    static readonly string[] ColorProps = { "_BaseColor", "_UnlitColor", "_Color" };

    Material[][] originals;
    Material[] live;
    float[] liveAlpha;
    string[] liveProp;
    float[] lightBase;
    Coroutine running;

    /// <summary>지금 진행 중인지.</summary>
    public bool Running => running != null;

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
        var list = new System.Collections.Generic.List<Material>();
        int k = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) { originals[i] = null; continue; }
            originals[i] = r.sharedMaterials;
            var swapped = new Material[originals[i].Length];
            for (int s = 0; s < swapped.Length; s++, k++)
            {
                var twin = k < fadeMaterials.Length ? fadeMaterials[k] : null;
                if (twin == null) { swapped[s] = originals[i][s]; continue; }
                swapped[s] = new Material(twin) { name = twin.name + " (fading)" };
                list.Add(swapped[s]);
            }
            r.sharedMaterials = swapped;
        }
        live = list.ToArray();
        liveAlpha = new float[live.Length];
        liveProp = new string[live.Length];
        for (int i = 0; i < live.Length; i++)
            foreach (var p in ColorProps)
                if (live[i].HasProperty(p)) { liveProp[i] = p; liveAlpha[i] = live[i].GetColor(p).a; break; }
        lightBase = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lightBase[i] = lights[i].intensity;
    }

    void Apply(float a)
    {
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
        foreach (var m in live) if (m != null) Destroy(m);
        originals = null; live = null;
    }

    void OnDisable() => Restore();
}
