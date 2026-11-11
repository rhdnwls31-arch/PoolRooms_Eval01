using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Visual Polish 확인용 자동 캡처 — Play 없이 에디터에서 씬을 1920x1080으로 렌더해 PNG로 저장한다.
// 저장 위치: production/qa/evidence/visual-polish/ (재촬영하면 덮어씀)
// 실행: Unity.exe -batchmode -projectPath . -executeMethod VisualPolishCapture.CaptureAll -logFile cap.log
//       (-nographics 금지 — 렌더가 필요)
// 이 캡처는 "씬 설정 그대로의 한 프레임"이라 Play 중 연출(눈꺼풀, 노이즈 등)은 포함되지 않는다.
public static class VisualPolishCapture
{
    const string OutDir = "production/qa/evidence/visual-polish";
    const int Width = 1920, Height = 1080;

    struct Shot { public string name; public Vector3 pos; public Vector3 lookAt; }

    [MenuItem("DEPTH 5m/Visual Polish/Capture Evidence Shots")]
    public static void CaptureAll()
    {
        try
        {
            Directory.CreateDirectory(OutDir);
            Capture("Assets/Scenes/Scene_Intro.unity", "intro", new[]
            {
                new Shot { name = "01_deck_to_pool",  pos = new Vector3(0, 1.7f, -22f),  lookAt = new Vector3(0, 0.2f, 2f) },
                new Shot { name = "02_poolside_down", pos = new Vector3(5.5f, 1.8f, -9f), lookAt = new Vector3(0, -1.2f, 4f) },
                new Shot { name = "03_orbs_sky",      pos = new Vector3(0, 1.7f, -18f),  lookAt = new Vector3(0, 4.5f, 8f) },
                new Shot { name = "04_shadows_deck",  pos = new Vector3(-12f, 3.0f, -16f), lookAt = new Vector3(-4f, 0f, -2f) },
            });
            Capture("Assets/Scenes/Scene_SecondDream.unity", "hall", new[]
            {
                new Shot { name = "01_entry_to_hall", pos = new Vector3(0, 1.6f, 4f),    lookAt = new Vector3(0, 1.4f, 30f) },
                new Shot { name = "02_hall_mid",      pos = new Vector3(0, 1.6f, 14f),   lookAt = new Vector3(0, 1.5f, 44f) },
                new Shot { name = "03_hall_up",       pos = new Vector3(0, 1.6f, 14f),   lookAt = new Vector3(0, 12f, 24f) },
                new Shot { name = "04_hall_back",     pos = new Vector3(0, 1.6f, 18f),   lookAt = new Vector3(0, 3f, 0f) },
            });
            Debug.Log("[VisualPolishCapture] 완료 → " + OutDir);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[VisualPolishCapture] 실패: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Capture(string scenePath, string prefix, Shot[] shots)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var go = new GameObject("__CaptureCam");
        SceneManager.MoveGameObjectToScene(go, scene);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);

        foreach (var s in shots)
        {
            go.transform.position = s.pos;
            go.transform.LookAt(s.lookAt);
            // 첫 프레임은 볼륨/굴절 텍스처가 안 잡힐 수 있어 두 번 렌더하고 두 번째를 저장.
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"{OutDir}/{prefix}_{s.name}.png", tex.EncodeToPNG());
            Debug.Log($"[VisualPolishCapture] {prefix}_{s.name}.png");
        }

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        EditorSceneManager.MarkSceneDirty(scene); // 임시 카메라를 지웠으므로 저장하지 않고 닫는다
    }
}
