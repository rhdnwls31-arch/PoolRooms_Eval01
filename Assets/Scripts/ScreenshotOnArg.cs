using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run-and-observe capture: Game.exe --scene Shop --screenshot C:/abs/shop.png --after 60
// --afterseconds N captures N seconds in instead (use this for time-based sequences).
// Optional --walkseconds N: before the --after wait, drive the first CharacterController
// found forward (its own transform.forward) for N seconds, to reach a trigger/state a
// static spawn point cannot. Optional --walkspeed (default 4).
public class ScreenshotOnArg : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Arg("--screenshot") == null) return;
        var go = new GameObject(nameof(ScreenshotOnArg));
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenshotOnArg>();
    }

    static string Arg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    IEnumerator Start()
    {
        var scene = Arg("--scene");
        if (!string.IsNullOrEmpty(scene)) yield return SceneManager.LoadSceneAsync(scene);

        // --yaw N: 캡처 전에 플레이어를 N도 돌린다. 맵을 사방에서 확인할 때 쓴다.
        if (float.TryParse(Arg("--yaw"), out var yaw) && yaw != 0f)
        {
            var ccYaw = Object.FindFirstObjectByType<CharacterController>();
            if (ccYaw != null) ccYaw.transform.Rotate(0f, yaw, 0f);
        }

        if (float.TryParse(Arg("--walkseconds"), out var walkSeconds) && walkSeconds > 0)
        {
            var cc = Object.FindFirstObjectByType<CharacterController>();
            if (cc != null)
            {
                float speed = float.TryParse(Arg("--walkspeed"), out var s) ? s : 4f;
                float elapsed = 0f;
                while (elapsed < walkSeconds)
                {
                    cc.Move(cc.transform.forward * speed * Time.deltaTime);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // --afterseconds: 실시간 기준 대기. 프레임 수(--after)는 FPS에 따라 달라져
        // 초 단위로 정의된 연출(예: 25초 뒤 쓰러짐)의 특정 순간을 잡지 못한다.
        if (float.TryParse(Arg("--afterseconds"), out var waitSeconds) && waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }
        else
        {
            int after = int.TryParse(Arg("--after"), out var n) ? n : 60;
            for (int i = 0; i < after; i++) yield return null;
        }
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Arg("--screenshot"));
        for (int i = 0; i < 3; i++) yield return null;   // the file is written after the frame
        Application.Quit();
    }
}
