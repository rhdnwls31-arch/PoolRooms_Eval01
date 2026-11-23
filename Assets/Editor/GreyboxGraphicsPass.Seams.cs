using UnityEditor;
using UnityEngine;

// 2026-11-23 사용자 피드백 "통로 벽 깨짐": 인트로(와 같은 수영장)의 아치 터널이 목표 방 남쪽 벽 두께 안으로 0.3m 들어가,
// 터널 타일 면과 벽 아치 구멍 안쪽 면이 같은 자리에 겹쳐 번갈아 보였다(z-fighting, 아치 위쪽이 들쭉날쭉).
// 반대쪽(수영장 북쪽 벽)은 겹치지 않지만, 벽 두께(0.6m)만큼 분홍 안쪽 면이 띠처럼 보였다.
// 터널 단면을 1.5cm 줄이고 양쪽 벽 두께 끝까지 늘려, 타일 면이 늘 벽 안쪽 면보다 앞에 오게 한다.
// 씬 전체 상자 면 겹침 검사(같은 평면, 다른 재질, 0.1m² 이상 24곳)는 모두 벽 속·바닥 아래에 숨은 면이었다.
public static partial class GreyboxGraphicsPass
{
    const string TunnelFixedName = "Tunnel_Inset";

    static void FixTunnelSeams()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || t.name != "Tunnel") continue;
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name == TunnelFixedName) continue;
            var src = mf.sharedMesh;
            var b = src.bounds;
            float r = b.max.x;                // 반폭 = 아치 반지름
            float hs = b.max.y - r;           // 아치 시작 높이
            float len = b.max.z;              // 로컬 z 0 → len (0 = 수영장 북쪽 벽 바깥면, len = 목표 방 벽 가운데)
            const float inset = 0.015f, backA = 0.6f, overG = 0.3f; // 수영장 벽 두께, 목표 방 벽 나머지 반
            var v = src.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                var p = v[i];
                if (p.y <= hs + 1e-3f) p.x -= Mathf.Sign(p.x) * inset;                        // 옆 기둥 부분
                else { var c = new Vector2(p.x, p.y - hs); c *= (r - inset) / r; p.x = c.x; p.y = hs + c.y; } // 아치
                p.z = p.z < 1e-3f ? -backA : (p.z > len - 1e-3f ? len + overG : p.z);
                v[i] = p;
            }
            var m = Object.Instantiate(src);
            m.name = TunnelFixedName;
            m.vertices = v;
            m.RecalculateNormals(); m.RecalculateBounds();
            mf.sharedMesh = m;
            var mc = t.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = m;
            EditorUtility.SetDirty(mf);
            Debug.Log($"[GreyboxGraphicsPass] 터널 이음매 고침: {t.parent?.parent?.name}/{t.name}");
        }
    }
}
