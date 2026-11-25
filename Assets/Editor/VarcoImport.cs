using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// VARCO FBX 한 개를 프로젝트 규칙대로 들여온다(2026-11-20, 그동안 손으로 하던 절차를 도구로 묶음).
/// 1) Assets/VARCO/&lt;방&gt;/Models로 옮기기 2) 박힌 텍스처를 Textures/&lt;FBX이름&gt;/로 꺼내 _BaseColor/_Normal로 이름 바꾸기
/// 3) 노멀맵 설정 4) HDRP/Lit 재질 만들고 material_0에 연결 5) 프리팹(루트 빈 객체 + Model 자식, 정면 +Z,
/// 바닥 물건은 발밑 가운데, 벽 물건은 뒷면 가운데가 원점).
/// 이름이 base_color/normal/material_0으로 모두 같아서, 2)~4)를 건너뛰면 엉뚱한 텍스처가 붙는다.
/// </summary>
public static class VarcoImport
{
    const string Root = "Assets/VARCO";
    const string TemplateMat = "Assets/VARCO/Bedroom/Materials/VAR_Bedroom_Bed_Empty.mat";

    /// <param name="srcFbx">지금 FBX 경로(이미 Models에 있으면 그 경로)</param>
    /// <param name="room">방 폴더 이름(VAR_&lt;room&gt;_&lt;name&gt;)</param>
    /// <param name="height">최종 높이(m). 0이면 scale을 그대로 쓴다</param>
    /// <param name="yaw">정면이 +Z가 되게 돌리는 각도</param>
    /// <param name="wall">벽에 거는 물건이면 원점을 뒷면 가운데로</param>
    /// <param name="scale">height가 0일 때 원본에 곱할 배율</param>
    /// <returns>만든 프리팹의 크기 요약</returns>
    public static string Import(string srcFbx, string room, string name, float height, float yaw = 0f, bool wall = false, float scale = 1f)
    {
        string fbxName = $"VAR_{room}_{name}";
        string roomDir = Ensure(Root, room); // 새 방이면 방 폴더부터 만든다(2026-11-25 DollRoom)
        foreach (var sub in new[] { "Models", "Textures", "Materials", "Prefabs" }) Ensure(roomDir, sub);
        string texDir = Ensure($"{roomDir}/Textures", fbxName);
        string dest = $"{roomDir}/Models/{fbxName}.fbx";

        if (srcFbx != dest)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(dest) != null) throw new System.Exception($"{dest} 이미 있음");
            string err = AssetDatabase.MoveAsset(srcFbx, dest);
            if (!string.IsNullOrEmpty(err)) throw new System.Exception(err);
        }
        var imp = (ModelImporter)AssetImporter.GetAtPath(dest);

        // 텍스처: 이미 정리돼 있으면 그대로(Blender에서 고친 책장처럼 텍스처를 따로 넣은 경우)
        string basePath = $"{texDir}/{fbxName}_BaseColor.png", normPath = $"{texDir}/{fbxName}_Normal.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(basePath) == null)
        {
            imp.ExtractTextures(texDir);
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var f = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                if (f.Contains("base") || f.Contains("color") || f.Contains("albedo") || f.Contains("diffuse")) AssetDatabase.RenameAsset(p, $"{fbxName}_BaseColor");
                else if (f.Contains("normal")) AssetDatabase.RenameAsset(p, $"{fbxName}_Normal");
            }
        }
        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
        if (baseTex == null) throw new System.Exception($"{fbxName}: 베이스 색 텍스처를 못 찾음");
        var nti = (TextureImporter)AssetImporter.GetAtPath(normPath);
        if (nti != null && nti.textureType != TextureImporterType.NormalMap) { nti.textureType = TextureImporterType.NormalMap; nti.SaveAndReimport(); }
        var normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);

        // 재질: 기존 VARCO 재질(HDRP/Lit, 노멀맵 키워드 켜짐)을 복사해 텍스처만 바꾼다
        string matPath = $"{roomDir}/Materials/{fbxName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(AssetDatabase.LoadAssetAtPath<Material>(TemplateMat)) { name = fbxName };
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetTexture("_BaseColorMap", baseTex);
        if (normTex != null) mat.SetTexture("_NormalMap", normTex);
        EditorUtility.SetDirty(mat);

        imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "material_0"), mat);
        imp.SaveAndReimport();

        // 프리팹
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dest));
        var root = new GameObject(fbxName);
        model.name = "Model";
        model.transform.SetParent(root.transform, false);
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
        model.transform.localScale = Vector3.one;
        if (height > 0f) scale = height / LocalBounds(root.transform).size.y; // 높이로 정하면 배율을 거기서 구한다
        model.transform.localScale = Vector3.one * scale;
        foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        var b = LocalBounds(root.transform);
        var pivot = wall ? new Vector3(b.center.x, b.center.y, b.min.z) : new Vector3(b.center.x, b.min.y, b.center.z);
        model.transform.localPosition -= pivot;
        b = LocalBounds(root.transform);
        string prefabPath = $"{roomDir}/Prefabs/{fbxName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return $"{fbxName}: 크기 {b.size.x:F2}×{b.size.y:F2}×{b.size.z:F2} (가운데 {b.center.ToString("F2")})";
    }

    /// <summary>
    /// 프리팹(또는 FBX)들을 정면 비스듬히 위(+Z 쪽)에서 한 장씩 찍어 가로로 이어 붙인 PNG를 만든다. 방향·크기 확인용.
    /// 장면 밖 높은 곳(y 2000)에 잠깐 놓고 지운다.
    /// </summary>
    public static void RenderSheet(string[] assetPaths, string outPng, int tile = 384)
    {
        var holder = new GameObject("__VarcoPreview"); holder.transform.position = new Vector3(0, 2000, 0);
        var lightGo = new GameObject("Sun"); lightGo.transform.SetParent(holder.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(40, 160, 0);
        var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 250f;
        var camGo = new GameObject("Cam"); camGo.transform.SetParent(holder.transform, false);
        var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.gray;
        var rt = new RenderTexture(tile, tile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        var sheet = new Texture2D(tile * assetPaths.Length, tile, TextureFormat.RGB24, false);
        for (int i = 0; i < assetPaths.Length; i++)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(assetPaths[i]);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = Vector3.zero;
            Bounds b = default; bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            float d = b.extents.magnitude / Mathf.Tan(15f * Mathf.Deg2Rad) * 1.05f;
            var dir = new Vector3(0.45f, 0.4f, 1f).normalized;
            camGo.transform.position = b.center + dir * d;
            camGo.transform.LookAt(b.center);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = d * 3;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(tile, tile, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, tile, tile), 0, 0); tex.Apply();
            sheet.SetPixels(i * tile, 0, tile, tile, tex.GetPixels());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
        RenderTexture.active = null;
        File.WriteAllBytes(outPng, sheet.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(sheet);
        Object.DestroyImmediate(holder);
    }

    static Bounds LocalBounds(Transform root)
    {
        Bounds b = default; bool first = true;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh; if (m == null) continue;
            var toRoot = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (var v in m.vertices)
            {
                var p = toRoot.MultiplyPoint3x4(v);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    static string Ensure(string parent, string child)
    {
        string p = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(p)) AssetDatabase.CreateFolder(parent, child);
        return p;
    }
}
