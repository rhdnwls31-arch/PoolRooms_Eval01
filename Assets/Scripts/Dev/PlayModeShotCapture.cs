#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// 개발용: 에디터를 -playShots &lt;폴더&gt; 인자로 실행하면 Play 모드에서 지정한 지점을 Main Camera로 렌더해 PNG로 저장하고 종료한다.
/// 에디터 배치 렌더와 Game 창 결과가 달라서, 실제 Play 상태 그대로 찍기 위한 도구.
/// </summary>
public class PlayModeShotCapture : MonoBehaviour
{
    static readonly (string name, Vector3 pos, Vector3 look)[] Shots =
    {
        ("01_spawn", new Vector3(0, 1.6f, 22f), new Vector3(0, 2.5f, -20f)),
        ("02_pool_stairs", new Vector3(0, 1.6f, 17f), new Vector3(0, -0.5f, 8f)),
        ("03_in_pool", new Vector3(0, 0.7f, 6f), new Vector3(0, 1.5f, -25f)),
        ("04_channel_hall", new Vector3(24f, 1.6f, 0f), new Vector3(46f, 2f, 0f)),
        ("05_tunnel", new Vector3(0, 1.6f, -32f), new Vector3(0, 1.6f, -48f)),
        ("06_goal_room", new Vector3(0, 1.6f, -50f), new Vector3(0, 1.5f, -60f)),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-playShots");
        if (i < 0 || i + 1 >= args.Length) return;
        var go = new GameObject("__PlayShots");
        go.AddComponent<PlayModeShotCapture>().StartCoroutine(go.GetComponent<PlayModeShotCapture>().Run(args[i + 1]));
    }

    IEnumerator Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        for (int f = 0; f < 120; f++) yield return null; // 씬 스크립트/텍스처 로딩 대기
        var cam = Camera.main;
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.enabled = false;

        const int W = 1600, H = 900;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        foreach (var s in Shots)
        {
            cam.transform.position = s.pos; cam.transform.LookAt(s.look);
            for (int f = 0; f < 20; f++) yield return null;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"{outDir}/{s.name}.png", tex.EncodeToPNG());
            Debug.Log("[PlayShots] " + s.name);
        }
        UnityEditor.EditorApplication.Exit(0);
    }
}
#endif
