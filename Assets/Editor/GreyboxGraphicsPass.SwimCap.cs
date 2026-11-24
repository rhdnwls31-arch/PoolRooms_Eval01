using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 2026-11-24 VARCO 수영모 인외(VAR_PoolHall_SwimCapFigure, 2.3m, 정면 +Z) 정리. 사용자 피드백: 얼굴 앞에 조각이 떠 있고 물안경이 뭉개져 있다.
// 1) 몸에 붙지 않은 작은 조각(삼각형 32개, 얼굴 앞)을 뺀다.
// 2) 머리 윗부분(2.10m 위, 머리 가운데에서 0.14m 안 = 수영모와 뭉개진 물안경)과 2.07m 위 어두운 끈 조각을 잘라 내고, 그 자리를 둥근 수영모 돔(단색, 꼭대기 2.27m)으로 덮는다.
//    물안경 조각을 색으로 골라 칠하거나 눌러 넣는 방법은 얼굴 그늘까지 잡혀 얼룩이 생겼다(처음 두 번 시도). 얼굴(코·턱)은 원래 그대로 둔다.
//    2.10m 아래에 남은 어두운 물안경 조각(밝기 < 0.3)만 얼굴색으로 칠한다.
// 3) 자른 선 위에 깨끗한 물안경을 씌워 이음매를 가린다: 머리를 한 바퀴 감는 고무 끈(2.8cm) + 둥근 렌즈 두 개 + 코 다리.
// 프리팹 원점 기준으로 한 번 굽고 PoolHall/Models/VAR_PoolHall_SwimCapFigure_Clean3.asset에 둔다.
public static partial class GreyboxGraphicsPass
{
    const float SwimCutY = 2.10f, SwimTopY = 2.27f, GoggleY = 2.10f;

    static UnityEngine.Mesh SwimCapCleanMesh(out Material[] mats)
    {
        mats = null;
        var prefab = VarcoPrefab("PoolHall", "SwimCapFigure");
        if (prefab == null) return null;
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        var baseMat = mf.GetComponent<MeshRenderer>().sharedMaterial;
        Material PlainMat(string name, Color c, float smooth)
        {
            string p = $"Assets/VARCO/PoolHall/Materials/VAR_PoolHall_SwimCap_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                m = new Material(baseMat) { name = $"VAR_PoolHall_SwimCap_{name}" };
                m.SetTexture("_BaseColorMap", null); m.SetTexture("_NormalMap", null); m.DisableKeyword("_NORMALMAP");
                AssetDatabase.CreateAsset(m, p);
            }
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(m);
            return m;
        }
        var src = mf.sharedMesh;
        var M = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var v = new List<Vector3>(src.vertices); var n = new List<Vector3>(src.normals); var t4 = new List<Vector4>(src.tangents); var uv = new List<Vector2>(src.uv);
        for (int i = 0; i < v.Count; i++) v[i] = M.MultiplyPoint3x4(v[i]);
        for (int i = 0; i < n.Count; i++) n[i] = M.MultiplyVector(n[i]).normalized;
        for (int i = 0; i < t4.Count; i++) { var d = M.MultiplyVector(t4[i]).normalized; t4[i] = new Vector4(d.x, d.y, d.z, t4[i].w); }
        var tri = src.triangles;

        var tex = new Texture2D(2, 2);
        tex.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(baseMat.GetTexture("_BaseColorMap"))));
        Color Avg(float y0, float y1, float minGray)
        {
            Color s = Color.black; int c = 0;
            for (int i = 0; i < tri.Length; i += 3)
            {
                var ce = (v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f;
                if (ce.y < y0 || ce.y > y1) continue;
                var u = (uv[tri[i]] + uv[tri[i + 1]] + uv[tri[i + 2]]) / 3f; var col = tex.GetPixelBilinear(u.x, u.y);
                if (col.grayscale < minGray) continue;
                s += col; c++;
            }
            var r = c > 0 ? s / c : new Color(0.7f, 0.7f, 0.7f); r.a = 1f; return r;
        }
        var skin = Avg(1.93f, 2.0f, 0.45f);   // 턱 아래
        var capCol = Avg(2.22f, 2.31f, 0.5f); // 수영모 꼭대기
        mats = new[] { baseMat, PlainMat("Skin", skin, 0.35f), PlainMat("Strap", new Color(0.13f, 0.14f, 0.16f), 0.35f),
                       PlainMat("Lens", new Color(0.03f, 0.035f, 0.045f), 0.92f), PlainMat("Cap", capCol, 0.55f) };

        const string path = "Assets/VARCO/PoolHall/Models/VAR_PoolHall_SwimCapFigure_Clean6.asset";
        var cached = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (cached != null) return cached;

        // 머리 가운데(수영모 꼭대기 쪽 꼭짓점 평균)와 10° 칸 번호
        Vector3 hc = Vector3.zero; int hn = 0; foreach (var q in v) if (q.y > 2.2f) { hc += q; hn++; } hc /= Mathf.Max(hn, 1);
        float Radial(Vector3 q) => new Vector2(q.x - hc.x, q.z - hc.z).magnitude;
        int Bin(Vector3 q) => Mathf.FloorToInt((Mathf.Atan2(q.x - hc.x, q.z - hc.z) * Mathf.Rad2Deg + 360f) % 360f / 10f) % 36;

        // 1) 이어진 덩어리(같은 위치의 꼭짓점을 하나로 본다)
        var key = new Dictionary<Vector3Int, int>(); var rep = new int[v.Count];
        for (int i = 0; i < v.Count; i++) { var k = Vector3Int.RoundToInt(v[i] * 10000f); if (!key.TryGetValue(k, out int r)) { r = i; key[k] = i; } rep[i] = r; }
        var par = new int[v.Count]; for (int i = 0; i < v.Count; i++) par[i] = i;
        int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
        for (int i = 0; i < tri.Length; i += 3) { int a = Find(rep[tri[i]]); par[Find(rep[tri[i + 1]])] = a; par[Find(rep[tri[i + 2]])] = a; }
        var size = new Dictionary<int, int>();
        for (int i = 0; i < tri.Length; i += 3) { int r = Find(rep[tri[i]]); size[r] = size.TryGetValue(r, out int c) ? c + 1 : 1; }

        // 2) 삼각형 나누기: 떠 있는 조각 / 머리 윗부분(잘라 냄) / 아래 남은 어두운 물안경 조각(얼굴색) / 나머지
        var body = new List<int>(); var skinTris = new List<int>(); int island = 0, cut = 0;
        for (int i = 0; i < tri.Length; i += 3)
        {
            if (size[Find(rep[tri[i]])] < 500) { island++; continue; }
            var ce = (v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f;
            // 앞·옆(정면에서 ±110°)은 0.19m까지 잘라 귀 옆으로 삐져나온 물안경 조각까지 뺀다. 뒤는 두건이 가까워 0.14m까지만.
            float yawC = Mathf.Abs(Mathf.DeltaAngle(0f, Mathf.Atan2(ce.x - hc.x, ce.z - hc.z) * Mathf.Rad2Deg));
            if (ce.y > SwimCutY && Radial(ce) < (yawC < 110f ? 0.19f : 0.14f)) { cut++; continue; }
            var u = (uv[tri[i]] + uv[tri[i + 1]] + uv[tri[i + 2]]) / 3f;
            bool dark = ce.y > 2.02f && Radial(ce) < 0.2f && tex.GetPixelBilinear(u.x, u.y).grayscale < 0.3f;
            if (dark && ce.y > 2.07f && Radial(ce) > 0.13f) { cut++; continue; } // 머리 면보다 튀어나온 옛 끈·코 다리 조각은 뺀다(안쪽 것은 얼굴색으로 칠해 구멍이 안 나게)
            (dark ? skinTris : body).AddRange(new[] { tri[i], tri[i + 1], tri[i + 2] });
        }

        // 자른 선의 머리 둘레: 남은 삼각형 꼭짓점 중 2.07~2.10m, 칸마다 가장 바깥(빈 칸은 이웃 값), 이웃과 평균
        var ring = new float[36];
        foreach (var list in new[] { body, skinTris })
            foreach (int i in list)
            {
                var q = v[i]; if (q.y < SwimCutY - 0.03f || q.y > SwimCutY + 0.002f) continue;
                float r = Radial(q); if (r > 0.14f) continue;
                ring[Bin(q)] = Mathf.Max(ring[Bin(q)], r);
            }
        for (int pass = 0; pass < 36; pass++)
            for (int b = 0; b < 36; b++) if (ring[b] < 0.06f) ring[b] = Mathf.Max(ring[(b + 35) % 36], ring[(b + 1) % 36]);
        for (int b = 0; b < 36; b++) if (ring[b] < 0.06f) ring[b] = 0.105f;
        var sm = new float[36];
        for (int b = 0; b < 36; b++) sm[b] = (ring[(b + 35) % 36] + ring[b] * 2f + ring[(b + 1) % 36]) / 4f + 0.002f;

        var strap = new List<int>(); var lens = new List<int>(); var cap = new List<int>();
        void AddV(Vector3 p, Vector3 nrm) { v.Add(p); n.Add(nrm); t4.Add(new Vector4(1, 0, 0, 1)); uv.Add(Vector2.zero); }
        void Quad(List<int> dst, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm)
        {
            int s = v.Count; AddV(a, nrm); AddV(b, nrm); AddV(c, nrm); AddV(d, nrm);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), nrm) >= 0) dst.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            else dst.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
        }
        float RingR(float angDeg) // 각도 사이 보간
        {
            float f = ((angDeg % 360f) + 360f) % 360f / 10f; int b0 = Mathf.FloorToInt(f) % 36; int b1 = (b0 + 1) % 36;
            return Mathf.Lerp(sm[b0], sm[b1], f - Mathf.Floor(f));
        }

        // 3) 수영모 돔: 자른 선(2.10)의 둘레에서 꼭대기(2.30)까지 타원 단면. 2mm 아래(2.098)에서 시작해 틈이 없게.
        {
            const int A = 48, H = 16; float y0 = SwimCutY - 0.012f, hgt = SwimTopY - y0; int s0 = v.Count;
            for (int j = 0; j <= H; j++)
            {
                float k = j / (float)H; float phi = k * Mathf.PI * 0.5f; float y = y0 + Mathf.Sin(phi) * hgt; float sc = Mathf.Pow(Mathf.Cos(phi), 0.6f); // 0.6제곱: 달걀처럼 뾰족하지 않게 둥근 수영모
                for (int i = 0; i <= A; i++)
                {
                    float ang = i * 360f / A; float a = ang * Mathf.Deg2Rad; float R = RingR(ang) + 0.003f;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var p = new Vector3(hc.x, y, hc.z) + dir * (R * sc);
                    var nrm = (dir * (Mathf.Pow(Mathf.Cos(phi), 0.4f) / R) + Vector3.up * (Mathf.Sin(phi) / hgt)).normalized;
                    AddV(p, nrm);
                }
            }
            for (int j = 0; j < H; j++)
                for (int i = 0; i < A; i++)
                {
                    int a0 = s0 + j * (A + 1) + i, a1 = a0 + 1, b0 = a0 + A + 1, b1 = b0 + 1;
                    if (Vector3.Dot(Vector3.Cross(v[a1] - v[a0], v[b0] - v[a0]), n[a0] + n[b1]) >= 0) cap.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                    else cap.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
        }

        // 4) 끈: 둘레 + 5mm, 높이 3.2cm(자른 선 2.10 아래위 1.6cm를 덮음), 두께 4mm
        const float bh = 0.016f, th = 0.004f;
        Vector3 P(int b, float off, float dy)
        {
            float ang = b * 10f; float a = ang * Mathf.Deg2Rad; float r = RingR(ang) + 0.005f + off;
            return new Vector3(hc.x + Mathf.Sin(a) * r, GoggleY + dy, hc.z + Mathf.Cos(a) * r);
        }
        for (int b = 0; b < 36; b++)
        {
            float a = (b + 0.5f) * 10f * Mathf.Deg2Rad; var outN = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Quad(strap, P(b, th, -bh), P(b + 1, th, -bh), P(b + 1, th, bh), P(b, th, bh), outN);
            Quad(strap, P(b, 0f, bh), P(b, th, bh), P(b + 1, th, bh), P(b + 1, 0f, bh), Vector3.up);
            Quad(strap, P(b, 0f, -bh), P(b + 1, 0f, -bh), P(b + 1, th, -bh), P(b, th, -bh), Vector3.down);
        }
        // 5) 렌즈: 정면(+Z)에서 좌우 21°, 납작한 타원체(폭 6cm, 높이 4.2cm, 두께 2.2cm)
        void Lens(float yawDeg)
        {
            float a = yawDeg * Mathf.Deg2Rad; var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var c = new Vector3(hc.x, GoggleY, hc.z) + dir * (RingR(yawDeg) + 0.013f);
            var rot = Quaternion.LookRotation(dir);
            var e = new Vector3(0.033f, 0.024f, 0.012f); // 렌즈 가장자리에 남은 옛 조각을 덮는 크기
            const int U = 16, V = 8; int s0 = v.Count;
            for (int j = 0; j <= V; j++)
                for (int i = 0; i <= U; i++)
                {
                    float th2 = j / (float)V * Mathf.PI, ph = i / (float)U * Mathf.PI * 2f;
                    var unitZ = new Vector3(Mathf.Sin(th2) * Mathf.Cos(ph), Mathf.Sin(th2) * Mathf.Sin(ph), Mathf.Cos(th2)); // 극을 앞뒤(Z)로
                    AddV(c + rot * Vector3.Scale(unitZ, e), (rot * new Vector3(unitZ.x / e.x, unitZ.y / e.y, unitZ.z / e.z)).normalized);
                }
            for (int j = 0; j < V; j++)
                for (int i = 0; i < U; i++)
                {
                    int a0 = s0 + j * (U + 1) + i, a1 = a0 + 1, b0 = a0 + U + 1, b1 = b0 + 1;
                    if (Vector3.Dot(Vector3.Cross(v[b0] - v[a0], v[a1] - v[a0]), n[a0] + n[b1]) >= 0) lens.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                    else lens.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                }
        }
        Lens(-21f); Lens(21f);
        {
            var c = new Vector3(hc.x, GoggleY, hc.z) + Vector3.forward * (RingR(0f) + 0.016f);
            float hw = 0.016f, hh = 0.009f, hd = 0.004f; // 코 다리를 넓혀 두 렌즈 사이 옛 조각을 덮는다
            Quad(strap, c + new Vector3(-hw, -hh, hd), c + new Vector3(hw, -hh, hd), c + new Vector3(hw, hh, hd), c + new Vector3(-hw, hh, hd), Vector3.forward);
            Quad(strap, c + new Vector3(-hw, hh, -hd), c + new Vector3(hw, hh, -hd), c + new Vector3(hw, hh, hd), c + new Vector3(-hw, hh, hd), Vector3.up);
        }

        var mesh = new UnityEngine.Mesh { name = "VAR_PoolHall_SwimCapFigure_Clean", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, subMeshCount = 5 };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTangents(t4); mesh.SetUVs(0, uv);
        mesh.SetTriangles(body, 0); mesh.SetTriangles(skinTris, 1); mesh.SetTriangles(strap, 2); mesh.SetTriangles(lens, 3); mesh.SetTriangles(cap, 4);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); AssetDatabase.SaveAssets();
        Debug.Log($"[GreyboxGraphicsPass] 수영모 인외: 떠 있는 조각 {island}, 머리 윗부분 {cut} 잘라 냄, 남은 물안경 조각 {skinTris.Count / 3} 얼굴색, 둘레 앞 {sm[0]:F3} 옆 {sm[9]:F3} 뒤 {sm[18]:F3}");
        return mesh;
    }
}
