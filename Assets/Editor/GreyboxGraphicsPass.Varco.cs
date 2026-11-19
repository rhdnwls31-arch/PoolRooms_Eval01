using UnityEditor;
using UnityEngine;

// 2026-11-19: VARCO 3D 모델 프리팹으로 그레이박스 더미를 바꾼다.
// 프리팹은 Assets/VARCO/<방>/Prefabs/VAR_<방>_<이름>.prefab. 정면 = +Z, 바닥 물건은 발밑 가운데, 벽 물건은 뒷면 가운데가 원점.
// 프리팹이 없으면 null/false를 돌려주고, 부르는 쪽은 예전 프리미티브 더미를 만든다.
public static partial class GreyboxGraphicsPass
{
    static GameObject VarcoPrefab(string room, string name) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/VARCO/{room}/Prefabs/VAR_{room}_{name}.prefab");

    // ---------------------------------------------------------------- 삐에로: 풍선만 떼어 낸 메쉬
    // VARCO 삐에로는 풍선까지 한 덩어리 메쉬다. 풍선만 따로 움직이거나 깜빡일 수 있게 프리팹 원점 기준으로 굽고
    // 얼굴보다 앞(z > 0.07) · 손보다 위(y > 1.53)의 삼각형을 풍선(+ 손 위 끈)으로 나눈다. 손 = (0.17, 1.45, 0.11), 프리팹 키 2.1m 기준.
    const string PierrotDir = "Assets/VARCO/Yellow/Models";
    public static readonly Vector3 PierrotHand = new Vector3(0.17f, 1.45f, 0.11f);

    static bool PierrotParts(out UnityEngine.Mesh body, out UnityEngine.Mesh balloon, out Material mat)
    {
        body = balloon = null; mat = null;
        var prefab = VarcoPrefab("Yellow", "Pierrot_Balloon");
        if (prefab == null) return false;
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
        string bodyPath = $"{PierrotDir}/VAR_Yellow_Pierrot_Body.asset", balPath = $"{PierrotDir}/VAR_Yellow_Pierrot_BalloonOnly.asset";
        body = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(bodyPath);
        balloon = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(balPath);
        if (body != null && balloon != null) return true;

        var src = mf.sharedMesh;
        var M = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix; // 모델 → 프리팹 원점
        var v = src.vertices; var n = src.normals; var t = src.tangents;
        for (int i = 0; i < v.Length; i++) v[i] = M.MultiplyPoint3x4(v[i]);
        for (int i = 0; i < n.Length; i++) n[i] = M.MultiplyVector(n[i]).normalized;
        for (int i = 0; i < t.Length; i++) { var d = M.MultiplyVector(t[i]).normalized; t[i] = new Vector4(d.x, d.y, d.z, t[i].w); }
        var tri = src.triangles;
        var a = new System.Collections.Generic.List<int>(); var b = new System.Collections.Generic.List<int>();
        bool Bal(Vector3 p) => p.y > 1.53f && p.z > 0.07f;
        for (int i = 0; i < tri.Length; i += 3)
            (Bal(v[tri[i]]) && Bal(v[tri[i + 1]]) && Bal(v[tri[i + 2]]) ? b : a).AddRange(new[] { tri[i], tri[i + 1], tri[i + 2] });
        UnityEngine.Mesh Make(string name, System.Collections.Generic.List<int> ids)
        {
            var m = new UnityEngine.Mesh { name = name, indexFormat = src.indexFormat };
            m.vertices = v; m.normals = n; m.tangents = t; m.uv = src.uv;
            m.SetTriangles(ids, 0); m.RecalculateBounds();
            return m;
        }
        body = Make("VAR_Yellow_Pierrot_Body", a); balloon = Make("VAR_Yellow_Pierrot_BalloonOnly", b);
        AssetDatabase.CreateAsset(body, bodyPath); AssetDatabase.CreateAsset(balloon, balPath);
        AssetDatabase.SaveAssets();
        return true;
    }

    static GameObject MeshPart(Transform parent, string name, UnityEngine.Mesh mesh, Material mat, uint layer, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.renderingLayerMask = layer;
        return go;
    }

    /// <summary>
    /// parent 아래 로컬 위치·회전에 VARCO 프리팹을 놓는다. 렌더링 레이어는 layer로 맞춘다.
    /// parent에 BoxCollider가 있으면 모델 크기에 맞춘다(fitCollider).
    /// </summary>
    static GameObject VarcoInto(Transform parent, string room, string name, uint layer, Vector3 localPos = default, Vector3 localEuler = default, bool fitCollider = true)
    {
        var prefab = VarcoPrefab(room, name);
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.renderingLayerMask = layer;

        if (fitCollider && parent.TryGetComponent<BoxCollider>(out var bc))
        {
            // 부모 로컬 공간에서 모델이 차지하는 상자
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = default; bool first = true;
            foreach (var r in rs)
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var lp = parent.InverseTransformPoint(c);
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; } else b.Encapsulate(lp);
                }
            }
            bc.center = b.center; bc.size = b.size;
        }
        return go;
    }
}
