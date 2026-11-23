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
    // 2026-11-23 사용자 피드백 "화면 위에 여백": 앞에서 깊이를 재 보니 유리는 x -0.22~0.35, y 0.13~0.545라 예전 판(위 0.51)이 위쪽 3.5cm를 못 덮었다.
    // 잰 유리에 맞춰 다시 굽는다(새 파일 _Screen2).
    static UnityEngine.Mesh CrtScreenMesh() => GlassScreenMesh("LivingRoom", "CRTTV", 0.065f, 0.337f, 0.284f, 0.206f, 0.08f, "2");

    /// <summary>
    /// VARCO 화면 달린 물건(TV, 모니터)의 유리 면을 앞(+Z)에서 쏘아 따라가는 둥근 모서리 메쉬. 프리팹 원점 기준, 유리 위 4mm.
    /// cx, cy = 유리 가운데, hw/hh = 반폭/반높이, r = 모서리 반지름. 만든 메쉬는 Models/VAR_&lt;방&gt;_&lt;이름&gt;_Screen.asset에 둔다.
    /// </summary>
    static UnityEngine.Mesh GlassScreenMesh(string room, string name, float cx, float cy, float hw, float hh, float r, string version = "")
    {
        string path = $"Assets/VARCO/{room}/Models/VAR_{room}_{name}_Screen{version}.asset";
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

    // ---------------------------------------------------------------- 수영장 선베드
    // 2026-11-20 VARCO 선베드(Blender에서 깨진 조각·등받이 정리, 0.74×0.86×1.93). 더미(IntroMapBuilder.Lounger)는
    // 등받이가 -Z라 VARCO(정면 +Z = 발 쪽, 등받이 -Z)와 방향이 같다. 더미 조각은 끄고(렌더러만) 그룹 콜라이더는 모델에 맞춘다.
    // 인트로(Dummies)와 같은 수영장 복사본(GB_05_SameIntro/Dummies) 모두. 다시 실행해도 같은 결과.
    const string LoungerVarcoName = "VARCO_Lounger";
    static void SwapPoolLoungers()
    {
        if (VarcoPrefab("Pool", "Bench") == null) return;
        var clean = BenchCleanMesh(out var cleanMats);
        RemoveIntroTowels();
        FixIntroRadio();
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null) continue; // 앞에서 지운 예전 VARCO_Lounger의 자식(다시 실행할 때)
            if (!(t.name.StartsWith("Prop_Lounger_") || t.name.StartsWith("LagoonCourt_Lounger_"))) continue;
            var old = t.Find(LoungerVarcoName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            uint layer = 1u;
            foreach (Transform c in t)
                if (c.TryGetComponent<MeshRenderer>(out var r)) { layer = r.renderingLayerMask; r.enabled = false; EditorUtility.SetDirty(r); }
            var go = VarcoInto(t, "Pool", "Bench", layer);
            go.name = LoungerVarcoName;
            if (clean != null) // 받침대만 단색인 메쉬로 바꿔 끼운다(프리팹 원점 기준이라 루트에 바로 붙인다)
            {
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) mr.enabled = false;
                var cp = MeshPart(go.transform, "Clean", clean, cleanMats[0], layer, 1f);
                cp.GetComponent<MeshRenderer>().sharedMaterials = cleanMats;
            }
            n++;
        }
        Debug.Log($"[GreyboxGraphicsPass] VARCO 선베드 {n}개");
    }

    // ---------------------------------------------------------------- 수영장 파라솔
    // 2026-11-20 VARCO 파라솔: 인트로·같은 수영장의 Prop_Parasol_*는 펼친 것, 라군 마당(LagoonCourt_Parasol_*)은 접힌 것.
    // 더미 조각 렌더러만 끄고 기둥 콜라이더(0.22×2.2)는 그대로 둔다(펼친 천 아래로 걸어 다닐 수 있게).
    const string ParasolVarcoName = "VARCO_Parasol";
    static void SwapPoolParasols()
    {
        if (VarcoPrefab("Pool", "Parasol_Open") == null) return;
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null) continue;
            bool lagoon = t.name.StartsWith("LagoonCourt_Parasol_");
            if (!(lagoon || t.name.StartsWith("Prop_Parasol_"))) continue;
            var old = t.Find(ParasolVarcoName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            uint layer = 1u;
            foreach (Transform c in t)
                if (c.TryGetComponent<MeshRenderer>(out var r)) { layer = r.renderingLayerMask; r.enabled = false; EditorUtility.SetDirty(r); }
            var go = VarcoInto(t, "Pool", lagoon ? "Parasol_Folded" : "Parasol_Open", layer, default, default, false);
            go.name = ParasolVarcoName;
            n++;
        }
        Debug.Log($"[GreyboxGraphicsPass] VARCO 파라솔 {n}개");
    }

    // ---------------------------------------------------------------- 물속 야자수
    // 2026-11-23 VARCO 야자수(2.92×4.5×3.04, 줄기 밑동 = 원점에서 x -0.13). 인트로·같은 수영장의 Prop_WaterPalm_*, Lagoon_Palm_*.
    // 더미 줄기는 수영장 바닥(그룹 y -0.9)에서 시작하므로 밑동을 거기 맞추고, 높이 = 더미 줄기 + 1.2m(잎)로 배율을 정한다.
    // 줄기 콜라이더(0.5 × 줄기 높이)는 그대로 둔다(잎은 스치고 지나감). 그룹마다 이름으로 정한 방향으로 돌려 똑같아 보이지 않게 한다.
    const string PalmVarcoName = "VARCO_Palm";
    static void SwapPoolPalms()
    {
        if (VarcoPrefab("Pool", "WaterPalm_A") == null) return;
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null) continue;
            if (!(t.name.StartsWith("Prop_WaterPalm_") || t.name.StartsWith("Lagoon_Palm_"))) continue;
            var old = t.Find(PalmVarcoName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var trunk = t.Find("Trunk");
            float trunkH = trunk != null ? trunk.localScale.y * 2f : 3.4f;
            uint layer = 1u;
            foreach (Transform c in t)
                if (c.TryGetComponent<MeshRenderer>(out var r)) { layer = r.renderingLayerMask; r.enabled = false; EditorUtility.SetDirty(r); }
            // 2026-11-23 B(잎이 풍성, 3.95×4.5×4.15, 밑동 (0.374, 0.126))를 섞는다: 수영장마다 8그루 중 3그루.
            bool b = VarcoPrefab("Pool", "WaterPalm_B") != null && (t.name == "Prop_WaterPalm_2" || t.name == "Prop_WaterPalm_4" || t.name == "Lagoon_Palm_2");
            var baseOff = b ? new Vector3(-0.374f, 0f, -0.126f) : new Vector3(0.13f, 0f, 0f);
            float s = (trunkH + 1.2f) / 4.5f;
            int h = 7; foreach (char ch in t.name) h = h * 31 + ch; // 실행마다 같은 값(string.GetHashCode는 보장 안 됨)
            float yaw = (h & 0xffff) / 65535f * 360f;
            var holder = new GameObject(PalmVarcoName).transform;
            holder.SetParent(t, false);
            holder.localPosition = new Vector3(0f, trunk != null ? trunk.localPosition.y - trunkH / 2f : -0.9f, 0f);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            holder.localScale = Vector3.one * s;
            VarcoInto(holder, "Pool", b ? "WaterPalm_B" : "WaterPalm_A", layer, baseOff, default, false);
            n++;
        }
        Debug.Log($"[GreyboxGraphicsPass] VARCO 야자수 {n}개");
    }

    // ---------------------------------------------------------------- 컴퓨터 속 거대한 모니터
    // 2026-11-20 VARCO 거대 브라운관(9.4×7.12×9.85)은 화면 유리가 막힌 통짜라, 출구 연출(화면 벽이 사라지고 빛의 방이 드러남)에 맞게
    // 유리 면 삼각형만 지운 메쉬를 만든다(프리팹 원점 기준, 처음 한 번만). 유리 = x ±3.2, y 1.5~5.95, 앞면(z 4.93)에서 0.3~0.6 들어감.
    public const float GiantGlassBottom = 1.5f, GiantGlassZ = 4.45f;
    static UnityEngine.Mesh GiantMonitorOpenMesh(out Material mat)
    {
        mat = null;
        var prefab = VarcoPrefab("Computer", "GiantMonitor");
        if (prefab == null) return null;
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
        const string path = "Assets/VARCO/Computer/Models/VAR_Computer_GiantMonitor_Open.asset";
        var cached = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (cached != null) return cached;
        var src = mf.sharedMesh;
        var M = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var v = src.vertices; var n = src.normals; var t = src.tangents;
        for (int i = 0; i < v.Length; i++) v[i] = M.MultiplyPoint3x4(v[i]);
        for (int i = 0; i < n.Length; i++) n[i] = M.MultiplyVector(n[i]).normalized;
        for (int i = 0; i < t.Length; i++) { var d = M.MultiplyVector(t[i]).normalized; t[i] = new Vector4(d.x, d.y, d.z, t[i].w); }
        bool Glass(Vector3 p) => Mathf.Abs(p.x) < 3.3f && p.y > 1.45f && p.y < 6.0f && p.z > 3.8f && p.z < 4.68f;
        var tri = src.triangles; var keep = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tri.Length; i += 3)
            if (!(Glass(v[tri[i]]) && Glass(v[tri[i + 1]]) && Glass(v[tri[i + 2]]))) keep.AddRange(new[] { tri[i], tri[i + 1], tri[i + 2] });
        var m = new UnityEngine.Mesh { name = "VAR_Computer_GiantMonitor_Open", indexFormat = src.indexFormat };
        m.vertices = v; m.normals = n; m.tangents = t; m.uv = src.uv;
        m.SetTriangles(keep, 0); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path); AssetDatabase.SaveAssets();
        Debug.Log($"[GreyboxGraphicsPass] 거대 모니터 유리 삼각형 {(tri.Length - keep.Count) / 3}개 지움");
        return m;
    }

    // 2026-11-23 사용자 피드백 "라디오가 바닥을 본다": 모델이 Z-up인데 Y 회전만 들어가 뒤로 누워 있었다.
    // 인트로·같은 수영장의 Prop_Radio를 X -90으로 세우고 앞면이 수영장 쪽(로컬 yaw 70)을 보게, 밑면은 협탁 윗면 그대로.
    static void FixIntroRadio()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || t.name != "Prop_Radio") continue;
            var r = t.GetComponentInChildren<Renderer>();
            float bottom = r.bounds.min.y;
            t.localEulerAngles = new Vector3(-90f, 70f, 0f);
            t.position += Vector3.up * (bottom - r.bounds.min.y);
            EditorUtility.SetDirty(t);
        }
    }

    // 2026-11-23 사용자 요청: 선베드 위 수건 더미(Prop_Towels_*, 인트로·같은 수영장)를 지운다. IntroMapBuilder에서도 뺐다.
    static void RemoveIntroTowels()
    {
        var dead = new System.Collections.Generic.List<GameObject>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t != null && t.name.StartsWith("Prop_Towels_")) dead.Add(t.gameObject);
        foreach (var g in dead) Object.DestroyImmediate(g);
        if (dead.Count > 0) Debug.Log($"[GreyboxGraphicsPass] 수건 더미 {dead.Count}개 지움");
    }

    // ---------------------------------------------------------------- 선베드: 등받이 아래 받침대만 깨끗한 단색으로
    // 2026-11-23 사용자 피드백 "벤치 뒤가 더러운 느낌". 재질은 정상(HDRP/Lit, 노멀맵 설정 정상)이고 받침대 부분 그림 자체가 얼룩져 있다.
    // 이 모델은 UV를 여러 부분이 겹쳐 써서(받침대 삼각형이 텍스처 절반 넓이와 겹침) 그림만 칠하면 프레임까지 번진다.
    // 그래서 받침대 삼각형(등받이 쪽 z < -0.2, 등받이 판보다 6cm 아래, 옆 레일 안쪽)을 두 번째 서브메쉬로 떼어 단색 재질을 준다.
    // 프리팹 원점 기준으로 굽고 Pool/Models/VAR_Pool_Bench_Clean.asset에 둔다(처음 한 번만).
    static UnityEngine.Mesh BenchCleanMesh(out Material[] mats)
    {
        mats = null;
        var prefab = VarcoPrefab("Pool", "Bench");
        if (prefab == null) return null;
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        var baseMat = mf.GetComponent<MeshRenderer>().sharedMaterial;
        const string plainPath = "Assets/VARCO/Pool/Materials/VAR_Pool_Bench_Plain.mat";
        var plain = AssetDatabase.LoadAssetAtPath<Material>(plainPath);
        if (plain == null)
        {
            // 텍스처의 밝은 부분(프레임 흰색) 평균을 단색으로 쓴다
            var tex = new Texture2D(2, 2); tex.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(baseMat.GetTexture("_BaseColorMap"))));
            var px = tex.GetPixels(); Color s = Color.black; int c = 0;
            for (int i = 0; i < px.Length; i += 13) if (px[i].grayscale > 0.6f) { s += px[i]; c++; }
            var col = c > 0 ? s / c : new Color(0.85f, 0.83f, 0.8f); col.a = 1f;
            plain = new Material(baseMat) { name = "VAR_Pool_Bench_Plain" };
            plain.SetTexture("_BaseColorMap", null); plain.SetTexture("_NormalMap", null); plain.DisableKeyword("_NORMALMAP");
            plain.SetColor("_BaseColor", col);
            AssetDatabase.CreateAsset(plain, plainPath);
        }
        mats = new[] { baseMat, plain };
        const string path = "Assets/VARCO/Pool/Models/VAR_Pool_Bench_Clean.asset";
        var cached = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (cached != null) return cached;
        var src = mf.sharedMesh;
        var M = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var v = src.vertices; var n = src.normals; var t = src.tangents;
        for (int i = 0; i < v.Length; i++) v[i] = M.MultiplyPoint3x4(v[i]);
        for (int i = 0; i < n.Length; i++) n[i] = M.MultiplyVector(n[i]).normalized;
        for (int i = 0; i < t.Length; i++) { var d = M.MultiplyVector(t[i]).normalized; t[i] = new Vector4(d.x, d.y, d.z, t[i].w); }
        var tri = src.triangles; var a = new System.Collections.Generic.List<int>(); var b = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tri.Length; i += 3)
        {
            var ce = (v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f;
            float surf = 0.38f + (-0.2f - ce.z) * 0.63f; // 등받이 윗면 높이(z -0.2에서 0.38m → -0.96에서 0.86m)
            bool strut = ce.z < -0.2f && ce.y > 0.12f && ce.y < surf - 0.06f && Mathf.Abs(ce.x) < 0.33f;
            (strut ? b : a).AddRange(new[] { tri[i], tri[i + 1], tri[i + 2] });
        }
        var m = new UnityEngine.Mesh { name = "VAR_Pool_Bench_Clean", indexFormat = src.indexFormat, subMeshCount = 2 };
        m.vertices = v; m.normals = n; m.tangents = t; m.uv = src.uv;
        m.SetTriangles(a, 0); m.SetTriangles(b, 1); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path); AssetDatabase.SaveAssets();
        Debug.Log($"[GreyboxGraphicsPass] 선베드 받침대 삼각형 {b.Count / 3}개를 단색으로");
        return m;
    }

    // ---------------------------------------------------------------- 노란 방 TV 받침대: 받침만 떼기 + 윗판
    // 2026-11-23 사용자 피드백 "모니터 아래 선반 텍스처 깨짐": 재질은 정상(HDRP/Lit, 베이스·노멀)이고, AI 모델이 TV 밑동과 받침 윗면을
    // 한 덩어리로 섞어 만들어 윗면 그림이 번져 있다(원래 그런 것). 받침 윗면에 얇은 나무 윗판(무늬 없는 받침 색)을 덮어 가린다.
    // 거실 TV도 이 받침을 쓴다(사용자 요청): 모든 꼭짓점이 받침 윗면보다 낮은 삼각형만 떼어 낸 메쉬(Yellow/Models/..._StandOnly.asset).
    /// <summary>노란 방 TV+받침 모델의 받침 윗면 높이·받침 발자국(프리팹 원점 기준), 받침 색 재질.</summary>
    static bool TvStandInfo(out float top, out Rect foot, out Material plain)
    {
        top = 0f; foot = default; plain = null;
        var prefab = VarcoPrefab("Yellow", "CRTTV_Stand");
        if (prefab == null) return false;
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        var M = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var v = mf.sharedMesh.vertices;
        float x0 = 9, x1 = -9, z0 = 9, z1 = -9;
        foreach (var a in v)
        {
            var w = M.MultiplyPoint3x4(a);
            if (w.z > 0.365f) top = Mathf.Max(top, w.y);          // 받침 앞면은 TV(앞 0.35)보다 앞으로 나와 있다
            if (w.y < 0.3f) { x0 = Mathf.Min(x0, w.x); x1 = Mathf.Max(x1, w.x); z0 = Mathf.Min(z0, w.z); z1 = Mathf.Max(z1, w.z); }
        }
        foot = Rect.MinMaxRect(x0, z0, x1, z1);
        const string matPath = "Assets/VARCO/Yellow/Materials/VAR_Yellow_CRTTV_StandTop.mat";
        plain = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (plain == null)
        {
            var baseMat = mf.GetComponent<MeshRenderer>().sharedMaterial;
            var tex = new Texture2D(2, 2); tex.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(baseMat.GetTexture("_BaseColorMap"))));
            var uv = mf.sharedMesh.uv; Color s = Color.black; int c = 0;
            for (int i = 0; i < v.Length; i += 3)
            {
                var w = M.MultiplyPoint3x4(v[i]);
                if (w.y > 0.12f && w.y < 0.32f) { s += tex.GetPixelBilinear(uv[i].x, uv[i].y); c++; } // 받침 옆면 색
            }
            var col = c > 0 ? s / c : new Color(0.45f, 0.3f, 0.2f); col.a = 1f;
            plain = new Material(baseMat) { name = "VAR_Yellow_CRTTV_StandTop" };
            plain.SetTexture("_BaseColorMap", null); plain.SetTexture("_NormalMap", null); plain.DisableKeyword("_NORMALMAP");
            plain.SetColor("_BaseColor", col); plain.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(plain, matPath);
        }
        return true;
    }

    /// <summary>받침 윗면을 덮는 2cm 윗판. parent는 받침 프리팹 원점과 같은 공간이어야 한다.</summary>
    static GameObject TvStandTopBoard(Transform parent, uint layer)
    {
        if (!TvStandInfo(out float top, out Rect foot, out Material plain)) return null;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = "StandTop";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(foot.center.x, top - 0.005f, foot.center.y);
        go.transform.localScale = new Vector3(foot.width + 0.01f, 0.02f, foot.height + 0.01f);
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = plain; r.renderingLayerMask = layer;
        return go;
    }

    /// <summary>노란 방 TV+받침 모델에서 받침만 떼어 낸 메쉬(프리팹 원점 기준). 처음 한 번 굽는다.</summary>
    static UnityEngine.Mesh TvStandOnlyMesh(out Material mat, out float top)
    {
        mat = null; top = 0f;
        if (!TvStandInfo(out top, out _, out _)) return null;
        var prefab = VarcoPrefab("Yellow", "CRTTV_Stand");
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
        const string path = "Assets/VARCO/Yellow/Models/VAR_Yellow_CRTTV_StandOnly.asset";
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
            if (v[tri[i]].y <= top + 0.01f && v[tri[i + 1]].y <= top + 0.01f && v[tri[i + 2]].y <= top + 0.01f) keep.AddRange(new[] { tri[i], tri[i + 1], tri[i + 2] });
        var m = new UnityEngine.Mesh { name = "VAR_Yellow_CRTTV_StandOnly", indexFormat = src.indexFormat };
        m.vertices = v; m.normals = n; m.tangents = t; m.uv = src.uv;
        m.SetTriangles(keep, 0); m.RecalculateBounds();
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
            // 부모 로컬 공간에서 모델이 차지하는 상자. 메쉬 자체 상자의 모서리를 옮겨 잰다
            // (월드 상자로 재면 비스듬히 돌린 그룹에서 콜라이더가 부풀었다: 2026-11-20 휴지통 3.66m).
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = default; bool first = true;
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                var lb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(r.transform.InverseTransformPoint(r.bounds.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var lp = parent.InverseTransformPoint(r.transform.TransformPoint(c));
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; } else b.Encapsulate(lp);
                }
            }
            bc.center = b.center; bc.size = b.size;
        }
        return go;
    }
}
