using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-24 2차 플레이 피드백(사용자가 고른 방향):
// 1) 찾기 규칙: 깜빡임이 별로고, 시선을 돌려야 문이 열리는 것이 애매하다 → 찾은 이상한 점은 스르르 사라지고(나타날 것은 스르르 나타나고),
//    출구 벽은 보는 앞에서 스르르 걷히며 문틈으로 빛이 샌다(→ 같은 날 3차 피드백으로 끔: GateOpenInView). 인트로 출구(둘러보기)는 그대로.
// 2) 물에 뜬 공·튜브가 동동 떠다니게(WaterBob).
// 3) 수영모 인외: 얼굴 앞 빛 판(네모 테두리가 보이고 가까이서 얼굴이 보였다)을 빼고, 얼굴을 지우개로 지운 듯 매끈한 살로 만든다.
// (구름방 구름 덩어리 제거는 Angel.cs, 마을 문 빛은 SunkenCity.cs, 계단은 Layout.cs + StairsTreadmill.cs)
public static partial class GreyboxGraphicsPass
{
    const float FindFadeTime = 2f, GateOpenTime = 2.5f;
    const bool GateOpenInView = false; // 2026-11-24 3차: 출구는 예전처럼(뒤돌면 열림), 문틈 빛 없음. 이상한 점 스르르는 유지
    const bool SwimFaceGlowOn = false; // 2026-11-24: 빛 판(네모 테두리가 보였다)을 끄고 지운 얼굴로
    const float GateSpillLumenAtEV68 = 20000f; // 거실(EV 6.8) 기준. 방 노출이 1 오를 때마다 2배

    static void ConvertFindReactions()
    {
        var gates = new HashSet<RevealWhenExplored>();
        var gateRoomPos = new Dictionary<RevealWhenExplored, Vector3>(); // 출구 벽은 방 경계에 있어 방 볼륨을 못 찾는다: 이상한 점 자리로 방 노출을 읽는다
        int targets = 0, fades = 0;
        foreach (var g in Object.FindObjectsByType<GazeTarget>(FindObjectsInactive.Include))
        {
            if (g.gate == null) continue; // 숨은 엔딩 마네킹은 그대로
            gates.Add(g.gate);
            if (!gateRoomPos.ContainsKey(g.gate)) gateRoomPos[g.gate] = g.roomBounds.size != Vector3.zero ? g.roomBounds.center : g.transform.position;
            var hide = new List<GameObject>(g.hideOnFound);
            if (g.blinkCount > 0 && hide.Count == 0)
            {
                // 깜빡이기만 하던 이상한 점(갈라진 복도 표지, 천사 눈, 컴퓨터 커서)은 깜빡이던 것이 스르르 사라진다
                foreach (var r in g.blinkRenderers) if (r != null && !hide.Contains(r.gameObject)) hide.Add(r.gameObject);
                foreach (var o in g.blinkObjects) if (o != null && !hide.Contains(o)) hide.Add(o);
            }
            g.hideOnFound = hide.ToArray();
            g.blinkCount = 0;
            g.changeWhenUnseen = false; // 보는 앞에서 스르르
            g.fadeTime = FindFadeTime;
            foreach (var o in g.hideOnFound) if (AddFade(o)) fades++;
            foreach (var o in g.showOnFound) if (o != null && !HasBehaviour(o) && AddFade(o)) fades++;
            EditorUtility.SetDirty(g);
            targets++;
        }
        foreach (var gate in gates)
        {
            var old = gate.transform.Find("RevealSpill");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (!GateOpenInView)
            {
                // 2026-11-24 3차 피드백: "문이 스르르 생기는 건 별로, 빛도 별로. 문은 기존처럼 뒤를 돌면 생기게" → 예전 방식(벽이 화면 밖일 때 한 번에 열림), 빛 없음.
                gate.openTime = 0f; gate.spill = new Light[0];
                var wf = gate.wall != null ? gate.wall.GetComponent<FadeAway>() : null;
                if (wf != null) Object.DestroyImmediate(wf);
                EditorUtility.SetDirty(gate);
                continue;
            }
            gate.openTime = GateOpenTime;
            if (gate.wall != null) AddFade(gate.wall);
            uint layer = 1u;
            var wr = gate.wall != null ? gate.wall.GetComponentInChildren<Renderer>(true) : null;
            if (wr != null) layer = wr.renderingLayerMask;
            float ev = RoomEVAt(gateRoomPos[gate]);
            Debug.Log($"[GreyboxGraphicsPass] {gate.name}: 방 노출 EV {ev:F1}");
            var spill = MakeLight(gate.transform, "RevealSpill", LightType.Point, gate.wallBounds.center, Vector3.zero, new Color(1f, 0.88f, 0.7f),
                GateSpillLumenAtEV68 * Mathf.Pow(2f, ev - 6.8f), 5f, layer, false, true);
            gate.spill = new[] { spill };
            EditorUtility.SetDirty(gate);
        }
        Debug.Log($"[GreyboxGraphicsPass] 찾기 반응: 이상한 점 {targets}개 스르르, 페이드 묶음 {fades}개, 출구 {gates.Count}개 ({(GateOpenInView ? "보는 앞에서 열림" : "예전처럼 화면 밖에서 열림")})");
    }

    static bool HasBehaviour(GameObject o)
    {
        foreach (var mb in o.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && !(mb is GazeTarget) && !(mb is FadeAway) && !(mb is FacePlayer)) return true; // 물 차오름·풍선 같은 장치는 페이드하지 않는다
        return false;
    }

    /// <summary>오브젝트에 FadeAway를 붙이고 렌더러마다 투명 짝 재질을 만든다.</summary>
    static bool AddFade(GameObject o)
    {
        if (o == null) return false;
        var fa = o.GetComponent<FadeAway>() ?? o.AddComponent<FadeAway>();
        var rs = new List<Renderer>(); var twins = new List<Material>();
        foreach (var r in o.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            rs.Add(r);
            foreach (var m in r.sharedMaterials) twins.Add(FadeTwin(m));
        }
        fa.renderers = rs.ToArray(); fa.fadeMaterials = twins.ToArray();
        fa.lights = o.GetComponentsInChildren<Light>(true);
        EditorUtility.SetDirty(fa);
        return true;
    }

    static Material FadeTwin(Material m)
    {
        if (m == null || !m.HasProperty("_SurfaceType")) return null;
        string dir = $"{MatDir}/Fade";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(MatDir, "Fade");
        string path = $"{dir}/{m.name}_Fade.mat";
        var t = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (t == null) { t = new Material(m); AssetDatabase.CreateAsset(t, path); }
        else { t.shader = m.shader; t.CopyPropertiesFromMaterial(m); }
        bool wasTransparent = m.GetFloat("_SurfaceType") > 0.5f;
        t.SetFloat("_SurfaceType", 1f);
        if (!wasTransparent) t.SetFloat("_BlendMode", 0f); // 알파 섞기
        if (t.HasProperty("_EnableBlendModePreserveSpecularLighting")) t.SetFloat("_EnableBlendModePreserveSpecularLighting", 0f);
        if (t.HasProperty("_EnableFogOnTransparent")) t.SetFloat("_EnableFogOnTransparent", 1f);
        UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(t);
        EditorUtility.SetDirty(t);
        return t;
    }

    /// <summary>이 위치를 덮는 방 볼륨(우선순위 가장 높은 것)의 고정 노출. 없으면 전역 값.</summary>
    static float RoomEVAt(Vector3 p)
    {
        float best = -1f, ev = 9.7f;
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            if (v.isGlobal || v.sharedProfile == null || v.priority <= best) continue;
            var bc = v.GetComponent<BoxCollider>();
            if (bc == null || !bc.bounds.Contains(p)) continue;
            if (!v.sharedProfile.TryGet<Exposure>(out var e) || !e.fixedExposure.overrideState) continue;
            best = v.priority; ev = e.fixedExposure.value;
        }
        return ev;
    }

    // ---------------------------------------------------------------- 물에 뜬 공·튜브
    static void AddWaterBobs()
    {
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.parent == null || t.parent.name != "Dummies") continue;
            bool tube = t.name.StartsWith("Prop_Flamingo");
            bool ball = t.name.StartsWith("Prop_GlowOrb") || t.name.StartsWith("ChannelHall_Orb") || t.name.StartsWith("Lagoon_Orb");
            if (!tube && !ball) continue;
            var wb = t.GetComponent<WaterBob>() ?? t.gameObject.AddComponent<WaterBob>();
            if (tube) { wb.bobHeight = 0.035f; wb.bobPeriod = 3.8f; wb.tiltDegrees = 3.5f; wb.driftRadius = 0.18f; wb.driftPeriod = 26f; wb.spinDegreesPerSecond = 1.5f; }
            else { wb.bobHeight = 0.03f; wb.bobPeriod = 2.8f; wb.tiltDegrees = 6f; wb.driftRadius = 0.12f; wb.driftPeriod = 19f; wb.spinDegreesPerSecond = 4f; }
            EditorUtility.SetDirty(wb);
            n++;
        }
        Debug.Log($"[GreyboxGraphicsPass] 물에 뜬 공·튜브 {n}개 동동");
    }

    // ---------------------------------------------------------------- 수영모 인외: 지운 얼굴
    // 2026-11-24 3차: 다듬기만으로는 원래 얼굴과 거의 같아 보였다(원래 얼굴이 단순해서). 범위를 넓히고 얼굴 노멀을 거의 한 방향으로 눕혀 명암(코·턱 그늘)을 없앤다.
    const float EraseY0 = 1.965f, EraseY1 = 2.115f, EraseRadius = 0.135f, EraseYaw = 80f, EraseFlatten = 0.85f; // 반지름은 목에 두른 수건이 안 잡히게
    const int EraseIterations = 60;

    /// <summary>정리된 수영모 인외 메쉬에서 물안경 아래 얼굴(코·입·턱)을 매끈하게 펴고 무늬 없는 살색(1번 칸)으로 칠한 메쉬. 한 번 굽고 저장한다.</summary>
    // 4차: 얼굴 칸 재질. 빛을 받아도 형체가 안 읽히게 거의 검은 무광.
    static readonly Color SwimVoidColor = new Color(0.012f, 0.012f, 0.014f);

    static UnityEngine.Mesh SwimCapErasedMesh(UnityEngine.Mesh clean)
    {
        const string path = "Assets/VARCO/PoolHall/Models/VAR_PoolHall_SwimCapFigure_Void2.asset";
        var cached = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (cached != null) return cached;
        if (clean == null) return null;

        var mesh = Object.Instantiate(clean); mesh.name = "VAR_PoolHall_SwimCapFigure_Erased";
        var v = mesh.vertices; var n = mesh.normals;
        var subs = new List<int>[mesh.subMeshCount];
        // Clean6에 좌표가 NaN인 꼭짓점(49개, 물안경 만들 때 생긴 것)이 있어 범위 계산이 깨졌다: 그 꼭짓점을 쓰는 삼각형을 빼고 자리를 0으로.
        bool Bad(Vector3 q) => float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z);
        int dropped = 0;
        for (int s = 0; s < subs.Length; s++)
        {
            var src = mesh.GetTriangles(s); subs[s] = new List<int>(src.Length);
            for (int i = 0; i < src.Length; i += 3)
            {
                if (Bad(v[src[i]]) || Bad(v[src[i + 1]]) || Bad(v[src[i + 2]])) { dropped++; continue; }
                subs[s].Add(src[i]); subs[s].Add(src[i + 1]); subs[s].Add(src[i + 2]);
            }
        }
        for (int i = 0; i < v.Length; i++) if (Bad(v[i])) { v[i] = new Vector3(0f, 2.1f, 0f); n[i] = Vector3.forward; }
        for (int i = 0; i < n.Length; i++) if (Bad(n[i])) n[i] = Vector3.up;

        Vector3 hc = Vector3.zero; int hn = 0; foreach (var q in v) if (q.y > 2.2f) { hc += q; hn++; } hc /= Mathf.Max(hn, 1);
        bool InFace(Vector3 c)
        {
            if (c.y < EraseY0 || c.y > EraseY1) return false;
            var d = new Vector2(c.x - hc.x, c.z - hc.z);
            if (d.magnitude > EraseRadius) return false;
            return Mathf.Abs(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg) < EraseYaw; // 정면 = +Z
        }

        // 같은 위치 꼭짓점을 하나로(UV 이음매)
        var key = new Dictionary<Vector3Int, int>(); var rep = new int[v.Length];
        for (int i = 0; i < v.Length; i++) { var k = Vector3Int.RoundToInt(v[i] * 10000f); if (!key.TryGetValue(k, out int r)) { r = i; key[k] = i; } rep[i] = r; }

        // 얼굴 삼각형을 0번(무늬) → 1번(살색)으로
        var faceTris = new List<int>(); var keep = new List<int>();
        for (int i = 0; i < subs[0].Count; i += 3)
        {
            var c = (v[subs[0][i]] + v[subs[0][i + 1]] + v[subs[0][i + 2]]) / 3f;
            (InFace(c) ? faceTris : keep).AddRange(new[] { subs[0][i], subs[0][i + 1], subs[0][i + 2] });
        }
        var keepSkin = new List<int>();
        for (int i = 0; i < subs[1].Count; i += 3)
        {
            var c = (v[subs[1][i]] + v[subs[1][i + 1]] + v[subs[1][i + 2]]) / 3f;
            (InFace(c) ? faceTris : keepSkin).AddRange(new[] { subs[1][i], subs[1][i + 1], subs[1][i + 2] });
        }
        subs[0] = keep; subs[1] = keepSkin;
        // 2026-11-24 4차 "아예 얼굴을 안 보이게, 어둡게": 얼굴은 맨 끝 새 칸(검은 빈 얼굴 재질)으로 뺀다.
        System.Array.Resize(ref subs, subs.Length + 1);
        subs[subs.Length - 1] = new List<int>(faceTris);
        mesh.subMeshCount = subs.Length;

        // 이웃(합친 꼭짓점 기준)과 "모든 이웃 삼각형이 얼굴인" 안쪽 꼭짓점
        var faceSet = new HashSet<int>(); foreach (int i in faceTris) faceSet.Add(rep[i]);
        var nbr = new Dictionary<int, HashSet<int>>(); var outside = new HashSet<int>();
        var faceTriKeys = new HashSet<(int, int, int)>();
        for (int i = 0; i < faceTris.Count; i += 3) faceTriKeys.Add((rep[faceTris[i]], rep[faceTris[i + 1]], rep[faceTris[i + 2]]));
        foreach (var list in subs)
            for (int i = 0; i < list.Count; i += 3)
            {
                int a = rep[list[i]], b = rep[list[i + 1]], c = rep[list[i + 2]];
                bool isFace = faceTriKeys.Contains((a, b, c));
                foreach (var (x, y, z) in new[] { (a, b, c), (b, c, a), (c, a, b) })
                {
                    if (!faceSet.Contains(x)) continue;
                    if (!isFace) outside.Add(x);
                    if (!nbr.TryGetValue(x, out var hs)) nbr[x] = hs = new HashSet<int>();
                    hs.Add(y); hs.Add(z);
                }
            }
        var inner = new List<int>(); foreach (int x in faceSet) if (!outside.Contains(x)) inner.Add(x);
        var innerSet = new HashSet<int>(inner);

        // Taubin 다듬기(오그라들지 않게 +λ, -μ 번갈아): 코·입술 같은 작은 굴곡만 지워진다. 테두리는 고정.
        var pos = new Dictionary<int, Vector3>(); foreach (int x in faceSet) pos[x] = v[x];
        Vector3 P(int x) => pos.TryGetValue(x, out var p) ? p : v[x];
        for (int it = 0; it < EraseIterations * 2; it++)
        {
            float w = it % 2 == 0 ? 0.5f : -0.53f;
            var next = new Dictionary<int, Vector3>();
            foreach (int x in inner)
            {
                var hs = nbr[x]; Vector3 avg = Vector3.zero; foreach (int y in hs) avg += P(y); avg /= hs.Count;
                next[x] = P(x) + w * (avg - P(x));
            }
            foreach (var kv in next) pos[kv.Key] = kv.Value;
        }
        for (int i = 0; i < v.Length; i++) if (pos.TryGetValue(rep[i], out var p)) v[i] = p;

        // 얼굴 꼭짓점 노멀을 합친 꼭짓점 기준으로 다시(이음매 줄이 안 생기게)
        var acc = new Dictionary<int, Vector3>();
        foreach (var list in subs)
            for (int i = 0; i < list.Count; i += 3)
            {
                int a = list[i], b = list[i + 1], c = list[i + 2];
                if (!faceSet.Contains(rep[a]) && !faceSet.Contains(rep[b]) && !faceSet.Contains(rep[c])) continue;
                var fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                foreach (int x in new[] { a, b, c }) { int r = rep[x]; acc[r] = (acc.TryGetValue(r, out var s) ? s : Vector3.zero) + fn; }
            }
        for (int i = 0; i < v.Length; i++) if (innerSet.Contains(rep[i]) && acc.TryGetValue(rep[i], out var s2)) n[i] = s2.normalized;
        // 명암 지우기: 얼굴 꼭짓점 노멀을 정면(조금 위) 쪽으로 눕힌다. 빛을 어디서 받아도 얼굴이 한 톤의 평평한 살색으로 보인다.
        var flat = new Vector3(0f, 0.15f, 1f).normalized;
        for (int i = 0; i < v.Length; i++) if (faceSet.Contains(rep[i])) n[i] = Vector3.Lerp(n[i], flat, innerSet.Contains(rep[i]) ? EraseFlatten : EraseFlatten * 0.5f).normalized;

        mesh.vertices = v; mesh.normals = n;
        for (int s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); AssetDatabase.SaveAssets();
        Debug.Log($"[GreyboxGraphicsPass] 수영모 인외 지운 얼굴: 얼굴 삼각형 {faceTris.Count / 3}, 다듬은 꼭짓점 {inner.Count}, 깨진 삼각형 {dropped} 뺌");
        return mesh;
    }

    // 4차: 검은 얼굴 칸의 경계가 삼각형 모양으로 들쭉날쭉해 오류처럼 보였다. 얼굴 앞에 부드러운 검은 그림자(늘 카메라를 보는 판)를 얹어 경계를 덮는다.
    // 지난 빛 판은 네모 테두리가 보였다: 이번엔 조명·반사를 받지 않는 Unlit + 알파 섞기, 그림은 판 가장자리 한참 안쪽(반지름 0.9)에서 알파가 정확히 0이 된다.
    const string SwimShadeName = "FaceShade";
    static readonly Vector3 SwimShadeCenter = new Vector3(-0.01f, 2.12f, -0.005f); // 물안경 높이 = 수영모~턱을 한 원으로 덮는 가운데. 판은 여기서 0.24m 앞(0.15m는 코 안쪽이라 얼굴에 가려 안 보였다)
    // 2026-11-24 5차 사용자: 수영모 꼭대기까지 덮는 큰 원으로(가운데를 물안경 높이로 올리고 정원으로 키움).
    const float SwimShadeForward = 0.24f, SwimShadeWidth = 0.44f, SwimShadeHeight = 0.44f, SwimShadeAlpha = 0.97f; // 진한 부분 반지름 = 판 반쪽의 55%

    static Texture2D FaceShadeTexture()
    {
        string path = $"{TexDir}/GB_FaceShade3.png";
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (t != null) return t;
        const int size = 128;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float k = Mathf.Clamp01((r - 0.55f) / 0.4f); float a = 1f - k * k * (3f - 2f * k); // 가운데는 거의 꽉, 0.95부터는 완전히 투명(판 가장자리 안쪽에서 끝나 네모가 안 보인다)
                px[y * size + x] = new Color(0f, 0f, 0f, a);
            }
        System.IO.Directory.CreateDirectory(TexDir);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true; imp.alphaIsTransparency = true; imp.mipmapEnabled = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void AddSwimFaceShade(Transform figure, uint layer)
    {
        var old = figure.Find(SwimShadeName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var tex = FaceShadeTexture();
        string matPath = $"{MatDir}/GB_SwimCap_FaceShade.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("HDRP/Unlit")); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.shader = Shader.Find("HDRP/Unlit");
        mat.SetFloat("_SurfaceType", 1f); mat.SetFloat("_BlendMode", 0f); // 투명, 알파 섞기
        mat.SetColor("_UnlitColor", new Color(0.01f, 0.01f, 0.012f, SwimShadeAlpha));
        mat.SetTexture("_UnlitColorMap", tex);
        mat.SetFloat("_EnableFogOnTransparent", 1f);
        mat.SetFloat("_DoubleSidedEnable", 0f);
        UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(mat);
        EditorUtility.SetDirty(mat);

        var pivot = new GameObject(SwimShadeName);
        pivot.transform.SetParent(figure, false);
        pivot.transform.localPosition = SwimShadeCenter;
        var fp = pivot.AddComponent<FacePlayer>();
        fp.turnSpeed = 3600f; fp.bobAmplitude = 0f; fp.bobPeriod = 0f;
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Shade";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(pivot.transform, false);
        quad.transform.localPosition = new Vector3(0f, 0f, SwimShadeForward);
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        quad.transform.localScale = new Vector3(SwimShadeWidth, SwimShadeHeight, 1f);
        var mr = quad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.renderingLayerMask = layer;
        mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
    }
}
