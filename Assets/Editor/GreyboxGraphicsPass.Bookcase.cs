using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 2026-11-24 거실 VARCO 책장(VAR_LivingRoom_Bookcase, 0.9×1.9×0.43): 책이 녹아 붙고 표지 그림이 번져 깨짐이 많았다(사용자 피드백).
// 나무 틀은 깨끗해서 틀만 남기고, 칸 안의 삼각형(책)은 지운 뒤 코드로 만든 단순한 책 상자를 줄지어 세운다.
// 칸 높이는 모델에서 잰 값(선반 윗면 0.125·0.43·0.77·1.11·1.49, 아랫면 0.39·0.73·1.065·1.44·1.835, 안쪽 폭 ±0.385, 뒤판 앞면 z -0.17).
// 프리팹 원점 기준으로 한 번 굽고 LivingRoom/Models/VAR_LivingRoom_Bookcase_Clean.asset에 둔다.
public static partial class GreyboxGraphicsPass
{
    static readonly (float lo, float hi)[] BookcaseBays = { (0.125f, 0.39f), (0.43f, 0.73f), (0.77f, 1.065f), (1.11f, 1.44f), (1.49f, 1.835f) };
    const float BookcaseInnerX = 0.382f, BookcaseBackZ = -0.16f;
    // 원래 책 색(사용자 스크린샷): 빨강, 크림, 베이지, 주황빛 빨강
    static readonly (string name, Color col)[] BookColors =
    {
        ("Red", new Color(0.55f, 0.12f, 0.11f)), ("Cream", new Color(0.80f, 0.73f, 0.58f)),
        ("Beige", new Color(0.64f, 0.52f, 0.37f)), ("Rust", new Color(0.68f, 0.30f, 0.18f)),
    };

    static UnityEngine.Mesh BookcaseCleanMesh(out Material[] mats)
    {
        mats = null;
        var prefab = VarcoPrefab("LivingRoom", "Bookcase");
        if (prefab == null) return null;
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        var baseMat = mf.GetComponent<MeshRenderer>().sharedMaterial;

        mats = new Material[1 + BookColors.Length];
        mats[0] = baseMat;
        for (int i = 0; i < BookColors.Length; i++)
        {
            string p = $"Assets/VARCO/LivingRoom/Materials/VAR_LivingRoom_Book_{BookColors[i].name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                m = new Material(baseMat) { name = $"VAR_LivingRoom_Book_{BookColors[i].name}" };
                m.SetTexture("_BaseColorMap", null); m.SetTexture("_NormalMap", null); m.DisableKeyword("_NORMALMAP");
                AssetDatabase.CreateAsset(m, p);
            }
            m.SetColor("_BaseColor", BookColors[i].col); m.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(m);
            mats[i + 1] = m;
        }

        const string path = "Assets/VARCO/LivingRoom/Models/VAR_LivingRoom_Bookcase_Clean.asset";
        var cached = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (cached != null) return cached;

        var src = mf.sharedMesh;
        var M = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var v = new List<Vector3>(src.vertices); var n = new List<Vector3>(src.normals); var t = new List<Vector4>(src.tangents); var uv = new List<Vector2>(src.uv);
        for (int i = 0; i < v.Count; i++) v[i] = M.MultiplyPoint3x4(v[i]);
        for (int i = 0; i < n.Count; i++) n[i] = M.MultiplyVector(n[i]).normalized;
        for (int i = 0; i < t.Count; i++) { var d = M.MultiplyVector(t[i]).normalized; t[i] = new Vector4(d.x, d.y, d.z, t[i].w); }

        // 1) 틀: 칸 안(옆판 안쪽, 뒤판 앞, 선반 윗면 1cm 위 ~ 아랫면 바로 아래)에 가운데가 있는 삼각형을 뺀다
        var tri = src.triangles; var frame = new List<int>(); int removed = 0;
        for (int i = 0; i < tri.Length; i += 3)
        {
            var ce = (v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f;
            bool inBay = false;
            if (Mathf.Abs(ce.x) < BookcaseInnerX && ce.z > BookcaseBackZ)
                foreach (var (lo, hi) in BookcaseBays) if (ce.y > lo + 0.01f && ce.y < hi - 0.004f) { inBay = true; break; }
            if (inBay) removed++; else frame.AddRange(new[] { tri[i], tri[i + 1], tri[i + 2] });
        }

        // 2) 책: 칸마다 왼쪽부터 세운 책을 빈틈없이 채운다. 칸 절반쯤은 한쪽 끝에 눕힌 책 2~4권 더미.
        var books = new List<int>[BookColors.Length];
        for (int i = 0; i < books.Length; i++) books[i] = new List<int>();
        var rng = new System.Random(20261124);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        void Box(Vector3 min, Vector3 max, int color)
        {
            var c = (min + max) * 0.5f; var e = (max - min) * 0.5f;
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                // 면의 두 축
                Vector3 a = d.x != 0 ? Vector3.forward : Vector3.right;
                Vector3 b = Vector3.Cross(d, a);
                int s = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float sa = (k == 0 || k == 3) ? -1 : 1, sb = (k < 2) ? -1 : 1;
                    v.Add(c + Vector3.Scale(d + a * sa + b * sb, e));
                    n.Add(d); t.Add(new Vector4(a.x, a.y, a.z, 1f)); uv.Add(Vector2.zero);
                }
                // 바깥을 보게 감는다
                var p0 = v[s]; var p1 = v[s + 1]; var p2 = v[s + 2];
                bool flip = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), d) < 0;
                if (!flip) books[color].AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                else books[color].AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
        }
        int lastColor = -1;
        int PickColor() { int c; do c = rng.Next(BookColors.Length); while (c == lastColor && rng.NextDouble() < 0.7); lastColor = c; return c; }

        int bookCount = 0;
        foreach (var (lo, hi) in BookcaseBays)
        {
            float bayH = hi - lo - 0.012f;
            float x = -BookcaseInnerX + 0.004f, xEnd = BookcaseInnerX - 0.004f;
            bool stack = rng.NextDouble() < 0.5;
            bool stackLeft = rng.NextDouble() < 0.5;
            float stackW = R(0.17f, 0.21f);
            if (stack && stackLeft) x += stackW + 0.01f;
            if (stack && !stackLeft) xEnd -= stackW + 0.01f;
            while (x < xEnd - 0.015f)
            {
                float w = Mathf.Min(R(0.022f, 0.046f), xEnd - x);
                float h = bayH * R(0.68f, 0.93f);
                float front = R(0.085f, 0.11f), depth = R(0.19f, 0.24f);
                Box(new Vector3(x, lo, front - depth), new Vector3(x + w - 0.002f, lo + h, front), PickColor());
                x += w; bookCount++;
            }
            if (stack)
            {
                float sx = stackLeft ? -BookcaseInnerX + 0.006f : BookcaseInnerX - 0.006f - stackW;
                float y = lo; int count = rng.Next(2, 5);
                for (int i = 0; i < count; i++)
                {
                    float th = R(0.028f, 0.042f), ww = stackW * R(0.86f, 1f), dd = R(0.15f, 0.2f), off = R(0f, stackW - ww);
                    float front = R(0.08f, 0.1f);
                    Box(new Vector3(sx + off, y, front - dd), new Vector3(sx + off + ww, y + th, front), PickColor());
                    y += th; bookCount++;
                }
            }
        }

        var mesh = new UnityEngine.Mesh { name = "VAR_LivingRoom_Bookcase_Clean", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, subMeshCount = 1 + BookColors.Length };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTangents(t); mesh.SetUVs(0, uv);
        mesh.SetTriangles(frame, 0);
        for (int i = 0; i < books.Length; i++) mesh.SetTriangles(books[i], i + 1);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); AssetDatabase.SaveAssets();
        Debug.Log($"[GreyboxGraphicsPass] 책장: 칸 안 삼각형 {removed}개 뺌, 책 {bookCount}권");
        return mesh;
    }

    /// <summary>거실 책장 VARCO 인스턴스의 원래 렌더러를 끄고 틀+새 책 메쉬로 바꿔 끼운다. Apply 끝에서 부른다.</summary>
    static void FixLivingBookcase()
    {
        var prefab = VarcoPrefab("LivingRoom", "Bookcase");
        if (prefab == null) return;
        var clean = BookcaseCleanMesh(out var mats);
        if (clean == null) return;
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) != prefab) continue;
            var old = t.Find("Clean");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            uint layer = 1u;
            foreach (var mr in t.GetComponentsInChildren<MeshRenderer>()) { layer = mr.renderingLayerMask; mr.enabled = false; EditorUtility.SetDirty(mr); }
            var cp = MeshPart(t, "Clean", clean, mats[0], layer, 1f);
            cp.GetComponent<MeshRenderer>().sharedMaterials = mats;
            n++;
        }
        Debug.Log($"[GreyboxGraphicsPass] 거실 책장 {n}개 정리");
    }
}
