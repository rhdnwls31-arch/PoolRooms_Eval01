using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 2026-11-18 사용자 피드백: 지나온 통로를 막은 자리(SealBehindPassage 판)에 걸레받이가 없어 문이 있던 자리가 티 났다.
// 문 자리를 채우는 판(통로 막기 판, 찾기 규칙 출구 벽)마다, 그 구멍 양옆에서 끊긴 걸레받이를 찾아 판 위에 이어 붙인다.
// 판이 켜질 때 같이 켜지도록 판의 자식으로 만든다. 앞으로 새 통로나 출구 벽을 만들어도 Apply 마지막에 자동으로 메운다.
public static partial class GreyboxGraphicsPass
{
    const string AutoTrimName = "Baseboard_Auto";

    static void PatchDoorwayTrims()
    {
        var holders = new List<(Transform parent, Bounds b, string label)>();
        foreach (var s in Object.FindObjectsByType<SealBehindPassage>(FindObjectsInactive.Include))
            if (s.plug != null) holders.Add((s.plug.transform, s.plugBounds, s.name));
        foreach (var g in Object.FindObjectsByType<RevealWhenExplored>(FindObjectsInactive.Include))
            if (g.wall != null) holders.Add((g.wall.transform, g.wallBounds, g.name));

        // 걸레받이 후보: 이름이나 부모 이름에 Baseboard가 들어간, 낮고 얇은 상자. 막는 판 자체에 붙은 것은 빼고 센다.
        var trims = new List<(MeshRenderer r, Bounds b)>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            if (r.name == AutoTrimName) continue;
            bool named = r.name.Contains("Baseboard") || (r.transform.parent != null && r.transform.parent.name.Contains("Baseboard"));
            if (!named) continue;
            bool onHolder = false;
            foreach (var h in holders) if (r.transform.IsChildOf(h.parent)) { onHolder = true; break; }
            if (onHolder) continue;
            trims.Add((r, WorldBounds(r)));
        }

        var log = new StringBuilder("[걸레받이 메우기]\n");
        foreach (var h in holders)
        {
            // 예전에 자동으로 붙인 것은 지우고 다시 만든다.
            for (int i = h.parent.childCount - 1; i >= 0; i--)
                if (h.parent.GetChild(i).name == AutoTrimName) Object.DestroyImmediate(h.parent.GetChild(i).gameObject);

            var b = h.b;
            bool normalX = b.size.x < b.size.z;            // 벽 두께 방향
            float nMin = normalX ? b.min.x : b.min.z, nMax = normalX ? b.max.x : b.max.z;
            float wMin = normalX ? b.min.z : b.min.x, wMax = normalX ? b.max.z : b.max.x;

            var made = new List<float>();
            foreach (var t in trims)
            {
                var tb = t.b;
                float tn = normalX ? tb.size.x : tb.size.z, tw = normalX ? tb.size.z : tb.size.x;
                if (tn > 0.1f || tb.size.y > 0.25f || tb.min.y > 0.1f || tw < 0.05f) continue;   // 벽을 따라 뻗은 낮고 얇은 띠만
                float nc = normalX ? tb.center.x : tb.center.z;
                if (nc < nMin - 0.35f || nc > nMax + 0.35f) continue;                            // 같은 벽 줄
                float t0 = normalX ? tb.min.z : tb.min.x, t1 = normalX ? tb.max.z : tb.max.x;
                bool endsAtHole = Mathf.Abs(t1 - wMin) < 0.15f || Mathf.Abs(t0 - wMax) < 0.15f;
                bool outside = t1 <= wMin + 0.15f || t0 >= wMax - 0.15f;
                if (!endsAtHole || !outside) continue;
                if (made.Exists(m => Mathf.Abs(m - nc) < 0.03f)) continue;
                // 판에 이미 손으로 붙인 걸레받이가 같은 자리에 있으면 그대로 둔다.
                bool covered = false;
                foreach (var r in h.parent.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.name.Contains("Baseboard")) continue;
                    var rb = WorldBounds(r);
                    float rc = normalX ? rb.center.x : rb.center.z;
                    if (Mathf.Abs(rc - nc) < 0.03f) { covered = true; break; }
                }
                made.Add(nc);
                if (covered) continue;

                float wc = (wMin + wMax) / 2, ww = wMax - wMin;
                var center = normalX ? new Vector3(nc, tb.center.y, wc) : new Vector3(wc, tb.center.y, nc);
                var size = normalX ? new Vector3(tn, tb.size.y, ww) : new Vector3(ww, tb.size.y, tn);
                Box(h.parent, AutoTrimName, center, size, t.r.sharedMaterial, t.r.renderingLayerMask);
                log.AppendLine($"  {h.label}: {t.r.name}에 맞춰 이음 (벽 줄 {nc:F2}, 폭 {ww:F2})");
            }
        }
        Debug.Log(log.ToString());
    }

    // 꺼진 오브젝트의 렌더러는 bounds가 비어 있어서, 메시 크기와 변환으로 직접 계산한다.
    static Bounds WorldBounds(MeshRenderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return r.bounds;
        var lb = mf.sharedMesh.bounds;
        var m = r.transform.localToWorldMatrix;
        var b = new Bounds(m.MultiplyPoint3x4(lb.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            b.Encapsulate(m.MultiplyPoint3x4(c));
        }
        return b;
    }
}
