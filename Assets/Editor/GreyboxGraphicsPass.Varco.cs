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

    // ---------------------------------------------------------------- 볼록렌즈 인외의 윗몸(가라앉은 도시 창 안)
    // 프리팹 원점(발밑) 기준으로 굽고, 꼭짓점이 모두 y > 1.35m인 삼각형만 남긴다(머리·어깨·윗팔). 노멀맵도 돌려준다.
    static UnityEngine.Mesh PeepFigureBust(out Texture normal)
    {
        normal = null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PeepFigurePrefab);
        if (prefab == null) return null;
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        var srcMat = mf.GetComponent<MeshRenderer>().sharedMaterial;
        if (srcMat != null && srcMat.HasProperty("_NormalMap")) normal = srcMat.GetTexture("_NormalMap");
        const string path = "Assets/VARCO/BlueCorridor/Models/VAR_BlueCorridor_PeepholeFigure_Bust.asset";
        var cached = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (cached != null) return cached;

        var src = mf.sharedMesh;
        var M = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var v = src.vertices; var n = src.normals; var t = src.tangents;
        for (int i = 0; i < v.Length; i++) v[i] = M.MultiplyPoint3x4(v[i]);
        for (int i = 0; i < n.Length; i++) n[i] = M.MultiplyVector(n[i]).normalized;
        for (int i = 0; i < t.Length; i++) { var d = M.MultiplyVector(t[i]).normalized; t[i] = new Vector4(d.x, d.y, d.z, t[i].w); }
        var tri = src.triangles; var keep = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tri.Length; i += 3)
            if (v[tri[i]].y > 1.35f && v[tri[i + 1]].y > 1.35f && v[tri[i + 2]].y > 1.35f) keep.AddRange(new[] { tri[i], tri[i + 1], tri[i + 2] });
        var m = new UnityEngine.Mesh { name = "VAR_BlueCorridor_PeepholeFigure_Bust", indexFormat = src.indexFormat };
        m.vertices = v; m.normals = n; m.tangents = t; m.uv = src.uv;
        m.SetTriangles(keep, 0); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path); AssetDatabase.SaveAssets();
        return m;
    }

    // 윗몸 하나를 뼈 두 개(몸통 = 원점, 머리 = 목 PeepNeck)로 감싼 스킨 메쉬. 2026-11-19: 삼각형을 잘라 머리를 떼니
    // 가슴·목 쪽에 구멍이 보였다(사용자 피드백) → 자르지 않고, 목을 따라 머리 뼈 가중치를 0 → 1로 부드럽게 올려 목만 휘게 한다.
    // 이 모델은 허리를 숙여 머리가 앞으로 매달려 있다(정수리 = +Z, 얼굴 = -Y). 가중치: z 0.22 → 0.36에서 0 → 1(목·머리만).
    // 목 = 머리와 몸통이 맞닿는 꼭짓점들의 가운데(측정값).
    public static readonly Vector3 PeepNeck = new Vector3(-0.02f, 2.171f, 0.227f);
    static UnityEngine.Mesh PeepFigureSkinnedBust(out Texture normal)
    {
        var bust = PeepFigureBust(out normal);
        if (bust == null) return null;
        const string path = "Assets/VARCO/BlueCorridor/Models/VAR_BlueCorridor_PeepholeFigure_BustSkinned.asset";
        var cached = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (cached != null) return cached;

        var v = bust.vertices;
        var m = new UnityEngine.Mesh { name = "VAR_BlueCorridor_PeepholeFigure_BustSkinned", indexFormat = bust.indexFormat };
        m.vertices = v; m.normals = bust.normals; m.tangents = bust.tangents; m.uv = bust.uv;
        m.SetTriangles(bust.triangles, 0);
        var bw = new BoneWeight[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            float t = Mathf.Clamp01((v[i].z - 0.22f) / 0.14f);
            float w = v[i].y > 1.95f ? t * t * (3f - 2f * t) : 0f;
            bw[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f - w, boneIndex1 = 1, weight1 = w };
        }
        m.boneWeights = bw;
        // 뼈 0 = 메쉬와 같은 자리(단위 행렬), 뼈 1 = 목. 메쉬 좌표 = 인외 원점(발밑) 기준.
        m.bindposes = new[] { Matrix4x4.identity, Matrix4x4.Translate(-PeepNeck) };
        m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path); AssetDatabase.SaveAssets();
        return m;
    }

    // ---------------------------------------------------------------- 브라운관 TV 화면: 유리 모양을 따라 만든 메쉬
    // 평평한 네모 판을 유리 앞에 띄우면 모서리가 테두리 위로 튀어나와 티가 났다(2026-11-19 사용자 피드백).
    // 그래서 모델 유리를 앞(+Z)에서 쏘아 표면을 따라가는 둥근 모서리 메쉬를 만든다(유리 위 4mm). CRTTV 프리팹 원점 기준.
    // 유리 = 가운데 (0.055, 0.32), 가로 반폭 0.27, 세로 반높이 0.19, 모서리 반지름 0.08. 보는 사람 왼쪽이 모델 +X.
    static UnityEngine.Mesh CrtScreenMesh() => GlassScreenMesh("LivingRoom", "CRTTV", 0.055f, 0.32f, 0.27f, 0.19f, 0.08f);

    /// <summary>
    /// VARCO 화면 달린 물건(TV, 모니터)의 유리 면을 앞(+Z)에서 쏘아 따라가는 둥근 모서리 메쉬. 프리팹 원점 기준, 유리 위 4mm.
    /// cx, cy = 유리 가운데, hw/hh = 반폭/반높이, r = 모서리 반지름. 만든 메쉬는 Models/VAR_&lt;방&gt;_&lt;이름&gt;_Screen.asset에 둔다.
    /// </summary>
    static UnityEngine.Mesh GlassScreenMesh(string room, string name, float cx, float cy, float hw, float hh, float r)
    {
        string path = $"Assets/VARCO/{room}/Models/VAR_{room}_{name}_Screen.asset";
        var cached = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (cached != null) return cached;
        var prefab = VarcoPrefab(room, name);
        if (prefab == null) return null;

        var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        probe.transform.position = new Vector3(0f, 400f, 0f);
        var mf = probe.GetComponentInChildren<MeshFilter>();
        var col = mf.gameObject.AddComponent<MeshCollider>(); col.sharedMesh = mf.sharedMesh;
        Physics.SyncTransforms();

        const float lift = 0.004f;
        const int nx = 28, ny = 20;
        var verts = new Vector3[(nx + 1) * (ny + 1)]; var uvs = new Vector2[verts.Length];
        for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
            {
                float u = i / (float)nx, v = j / (float)ny;
                float x = cx + hw - u * 2f * hw, y = cy - hh + v * 2f * hh; // u 0 = 보는 사람 왼쪽 = +X
                // 둥근 모서리: 모서리 원 밖의 점은 원 위로 당긴다
                float qx = Mathf.Abs(x - cx) - (hw - r), qy = Mathf.Abs(y - cy) - (hh - r);
                if (qx > 0f && qy > 0f && qx * qx + qy * qy > r * r)
                {
                    float k = r / Mathf.Sqrt(qx * qx + qy * qy);
                    x = cx + Mathf.Sign(x - cx) * ((hw - r) + qx * k);
                    y = cy + Mathf.Sign(y - cy) * ((hh - r) + qy * k);
                }
                var o = probe.transform.position + new Vector3(x, y, 1f);
                float z = Physics.Raycast(o, Vector3.back, out var hit, 2f) ? hit.point.z - probe.transform.position.z : 0.3f;
                verts[j * (nx + 1) + i] = new Vector3(x, y, z + lift);
                uvs[j * (nx + 1) + i] = new Vector2(u, v);
            }
        Object.DestroyImmediate(probe);

        var tris = new System.Collections.Generic.List<int>();
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
        var m = new UnityEngine.Mesh { name = $"VAR_{room}_{name}_Screen", vertices = verts, uv = uvs };
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        // 앞(+Z)을 보게: 법선이 뒤를 보면 감는 방향을 뒤집는다
        float nz = 0f; foreach (var n in m.normals) nz += n.z;
        if (nz < 0f) { for (int t = 0; t < tris.Count; t += 3) (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]); m.SetTriangles(tris, 0); m.RecalculateNormals(); }
        m.RecalculateTangents(); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path); AssetDatabase.SaveAssets();
        return m;
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
