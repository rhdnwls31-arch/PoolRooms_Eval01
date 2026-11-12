using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 타일 텍스처가 어긋나 보이던 문제 수정.
//   1) 이전 월드 UV 계산은 회전/비균등 스케일을 못 맞춰서 인접한 상자끼리 줄눈이 안 이어졌다 → 월드 좌표 기준 평면 투영으로 다시 계산.
//   2) 이름이 같은 오브젝트(Step_1 등)가 메시 에셋 이름 충돌로 서로 덮어써서 메시 참조가 비었다(계단이 안 보임) → 번호로 고유 이름.
// 대상: Env_Court_PoolTile / Env_Court_Deck 재질을 쓰는 큐브 계열 오브젝트. 멱등.
// 실행: 메뉴 [DEPTH 5m > HDRP Trial > Fix Tile UV]  또는  Unity.exe -batchmode -projectPath . -executeMethod TileUvFix.RunBatch -quit
public static class TileUvFix
{
    const string MeshDir = "Assets/Meshes/Generated";
    const float PoolRepeatMeters = 0.6f;  // 타일 한 칸 0.3m
    const float DeckRepeatMeters = 1.6f;  // 타일 한 칸 0.8m
    static readonly string[] Scenes = { "Assets/Scenes/Scene_Intro.unity", "Assets/Scenes/Scene_SecondDream.unity" };

    [MenuItem("DEPTH 5m/HDRP Trial/Fix Tile UV")]
    public static void RunAll()
    {
        Directory.CreateDirectory(MeshDir);
        foreach (var guid in AssetDatabase.FindAssets("WorldUV_ t:Mesh", new[] { MeshDir }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid)); // 장면 참조는 아래에서 모두 새로 연결한다
        foreach (var path in Scenes) FixScene(path);
        AssetDatabase.SaveAssets();
        Debug.Log("[TileUvFix] 완료");
    }

    public static void RunBatch()
    {
        try { RunAll(); }
        catch (System.Exception e) { Debug.LogError("[TileUvFix] 실패: " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static void FixScene(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        int n = 0, restored = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mat = r.sharedMaterial;
                var mf = r.GetComponent<MeshFilter>();
                if (mat == null || mf == null) continue;
                float repeat;
                if (mat.name == "Env_Court_PoolTile") repeat = PoolRepeatMeters;
                else if (mat.name == "Env_Court_Deck") repeat = DeckRepeatMeters;
                else continue;

                var cur = mf.sharedMesh;
                bool isCubeFamily = cur == null || cur.name == "Cube" || cur.name.StartsWith("WorldUV_");
                if (!isCubeFamily) continue; // 아치/터널 등 직접 만든 메시는 자체 UV를 유지
                if (cur == null) restored++;

                var mesh = Object.Instantiate(cube);
                mesh.name = $"WorldUV_{scene.name}_{n:D4}";
                BakeWorldUV(mesh, r.transform, repeat);
                string path = $"{MeshDir}/{mesh.name}.asset";
                AssetDatabase.CreateAsset(mesh, path);
                mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                EditorUtility.SetDirty(mf);
                n++;
            }
        Debug.Log($"[TileUvFix] {scene.name}: 메시 {n}개 재계산 (끊어진 참조 복구 {restored}개)");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 정점을 월드 좌표로 옮겨 면의 방향(월드 법선)에 따라 평면 투영: 인접한 상자끼리 줄눈이 이어진다.
    static void BakeWorldUV(Mesh mesh, Transform t, float repeat)
    {
        var m = t.localToWorldMatrix;
        var mn = m.inverse.transpose;
        var verts = mesh.vertices; var normals = mesh.normals;
        var uv = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 p = m.MultiplyPoint3x4(verts[i]);
            Vector3 n = mn.MultiplyVector(normals[i]).normalized;
            Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            Vector2 q;
            if (a.y >= a.x && a.y >= a.z) q = new Vector2(p.x, p.z);        // 위/아래
            else if (a.x >= a.z) q = new Vector2(p.z, p.y);                  // 동/서
            else q = new Vector2(p.x, p.y);                                  // 남/북
            uv[i] = q / repeat;
        }
        mesh.uv = uv;
    }
}
