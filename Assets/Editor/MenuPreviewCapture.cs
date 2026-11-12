using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴 UI를 Play 없이 1920x1080으로 렌더해 PNG로 저장한다(디자인 확인용).
// 실제 게임과 같은 MenuUIFactory를 쓰므로 보이는 모습이 그대로다. 호버/클릭 같은 상호작용은 캡처되지 않는다.
// 저장: production/qa/evidence/menu-ui/   실행: [DEPTH 5m > Menu > Capture Menu Previews]
//       또는 Unity.exe -batchmode -projectPath . -executeMethod MenuPreviewCapture.CaptureBatch
public static class MenuPreviewCapture
{
    const string OutDir = "production/qa/evidence/menu-ui";
    const int W = 1920, H = 1080;

    [MenuItem("DEPTH 5m/Menu/Capture Menu Previews")]
    public static void CaptureAll()
    {
        Directory.CreateDirectory(OutDir);
        // 메인 메뉴 씬(검정)
        EditorSceneManager.OpenScene("Assets/Scenes/Scene_MainMenu.unity", OpenSceneMode.Single);
        Shot("main", MenuMode.Main, false, null);
        Shot("main_settings", MenuMode.Main, true, null);
        // 일시정지: 인트로 화면 위에 덮어서
        EditorSceneManager.OpenScene("Assets/Scenes/Scene_Intro.unity", OpenSceneMode.Single);
        var view = new Vector3[] { new Vector3(0, 1.6f, 22f), new Vector3(0, 2.5f, -20f) };
        Shot("pause", MenuMode.Pause, false, view);
        Shot("pause_settings", MenuMode.Pause, true, view);
        Debug.Log("[MenuPreviewCapture] 완료 → " + OutDir);
    }

    public static void CaptureBatch()
    {
        try { CaptureAll(); }
        catch (System.Exception e) { Debug.LogError("[MenuPreviewCapture] 실패: " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    // view == null: 검은 배경(메인). view != null: {카메라 위치, 바라볼 점}으로 씬을 비춘다.
    static void Shot(string name, MenuMode mode, bool settings, Vector3[] view)
    {
        var camGo = new GameObject("__MenuCapCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f;
        if (view == null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.cullingMask = -1; /* 캔버스 오브젝트는 Default 레이어 */ }
        else { cam.clearFlags = CameraClearFlags.Skybox; camGo.transform.position = view[0]; camGo.transform.LookAt(view[1]); }

        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        var canvas = MenuController.CreateCanvas(null);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        var refs = MenuUIFactory.Build((RectTransform)canvas.transform, mode);
        if (mode == MenuMode.Main && refs.continueButton != null) refs.continueButton.interactable = GameSave.Exists;
        refs.settingsPanel.SetActive(settings);
        MenuUIFactory.RefreshAll(refs);

        Canvas.ForceUpdateCanvases();
        cam.Render(); // 첫 프레임은 폰트 텍스처가 안 잡힐 수 있어 두 번 렌더
        Canvas.ForceUpdateCanvases();
        cam.Render();

        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes($"{OutDir}/{name}.png", tex.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(canvas.gameObject);
        Object.DestroyImmediate(camGo);
        Debug.Log("[MenuPreviewCapture] " + name + ".png");
    }
}
