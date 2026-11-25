using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 이상한 점을 처음 바라볼 때 한 번씩 끊기던 것(2026-11-25 빌드 플레이, 천사 방이 가장 심했다)을 막는다.
/// 바라보는 동안 옅어지게 하려고 끼우는 투명 짝 재질(<see cref="FadeAway"/>)과 찾으면 켜지는 것(<see cref="GazeTarget"/>.showOnFound)은
/// 그 순간 처음 그려져, 그래픽 카드가 그 재질용 셰이더 상태를 그 자리에서 만드느라 멈췄다.
/// 씬이 열린 직후(시작 화면이 아직 어두울 때) 몇 프레임 동안 이 재질들을 카메라 앞에 아주 작게, 투명하게 한 번씩 그려 미리 만들어 둔다.
/// 실행 인수 --nowarmup이면 하지 않는다(비교 측정용).
/// </summary>
public class FadeWarmup : MonoBehaviour
{
    const int Frames = 3;              // 미리 그리는 프레임 수
    const float Distance = 0.5f;       // 카메라 앞 거리(m)
    const float Scale = 0.0005f;       // 눈에 안 띌 만큼 작게

    struct Draw { public Mesh mesh; public int sub; public Material mat; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--nowarmup") >= 0) return;
        SceneManager.sceneLoaded += (s, m) =>
        {
            if (FindObjectsByType<FadeAway>(FindObjectsInactive.Include).Length == 0) return;
            new GameObject(nameof(FadeWarmup)).AddComponent<FadeWarmup>();
        };
    }

    readonly List<Draw> draws = new List<Draw>();
    readonly List<Material> temp = new List<Material>();

    void Start()
    {
        Collect();
        StartCoroutine(Run());
    }

    void Collect()
    {
        foreach (var f in FindObjectsByType<FadeAway>(FindObjectsInactive.Include))
        {
            int k = 0;
            foreach (var r in f.renderers)
            {
                if (r == null) continue;
                var mesh = MeshOf(r);
                int slots = r.sharedMaterials.Length;
                for (int s = 0; s < slots; s++, k++)
                {
                    var twin = k < f.fadeMaterials.Length ? f.fadeMaterials[k] : null;
                    if (twin == null || mesh == null) continue;
                    var m = new Material(twin) { name = twin.name + " (warmup)" };
                    foreach (var p in new[] { "_BaseColor", "_UnlitColor", "_Color" })
                        if (m.HasProperty(p)) { var c = m.GetColor(p); c.a = 0f; m.SetColor(p, c); break; }
                    temp.Add(m);
                    draws.Add(new Draw { mesh = mesh, sub = Mathf.Min(s, mesh.subMeshCount - 1), mat = m });
                }
            }
        }
        // 찾으면 켜지는 것(처음엔 꺼져 있어 한 번도 그려지지 않았다)
        foreach (var g in FindObjectsByType<GazeTarget>(FindObjectsInactive.Include))
            foreach (var arr in new[] { g.showOnFound, g.showUnderneath, g.swapIn })
                foreach (var go in arr)
                {
                    if (go == null) continue;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var mesh = MeshOf(r);
                        if (mesh == null) continue;
                        var mats = r.sharedMaterials;
                        for (int s = 0; s < mats.Length; s++)
                            if (mats[s] != null) draws.Add(new Draw { mesh = mesh, sub = Mathf.Min(s, mesh.subMeshCount - 1), mat = mats[s] });
                    }
                }
    }

    static Mesh MeshOf(Renderer r)
    {
        if (r is SkinnedMeshRenderer sk) return sk.sharedMesh;
        return r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
    }

    IEnumerator Run()
    {
        for (int i = 0; i < Frames; i++)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var t = cam.transform;
                var mtx = Matrix4x4.TRS(t.position + t.forward * Distance, t.rotation, Vector3.one * Scale);
                foreach (var d in draws)
                {
                    var rp = new RenderParams(d.mat) { camera = cam, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
                    Graphics.RenderMesh(rp, d.mesh, d.sub, mtx);
                }
            }
            yield return null;
        }
        yield return null;
        foreach (var m in temp) if (m != null) Destroy(m);
        Destroy(gameObject);
    }
}
