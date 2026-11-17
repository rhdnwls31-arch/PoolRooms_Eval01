using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 그레이박스 씬을 배치 Play 모드로 실행해 방별 스크린샷을 찍고, 정해진 동선을 CharacterController로 밀어 막힘을 검사한다.
// 실행: Unity.exe -batchmode -projectPath . -executeMethod GreyboxGraphicsPass.CaptureBatch -gbShots <폴더> [-gbOnly 03,05] [-gbWalk] [-gbVariant <이름>]
// 씬은 저장하지 않는다.
public static partial class GreyboxGraphicsPass
{
    struct Shot { public string name; public Vector3 pos, look; public Shot(string n, Vector3 p, Vector3 l) { name = n; pos = p; look = l; } }

    // 01~07은 production/qa/evidence/greybox/ 의 이전 스크린샷과 같은 자리·방향(이미지에서 역산).
    static readonly Shot[] Shots =
    {
        new Shot("00-intro-spawn",        new Vector3(0, 1.6f, 22f),          new Vector3(0, 2.5f, -20f)),
        new Shot("01-intro-exit",         new Vector3(0, 1.6f, -52f),         new Vector3(0, 1.6f, -70f)),
        new Shot("02-pool-hall",          new Vector3(-2.5f, 1.6f, -73.5f),   new Vector3(0.5f, 1.0f, -100f)),
        new Shot("02b-pool-hall-inside",  new Vector3(4.2f, 1.6f, -86f),      new Vector3(-1f, 0.2f, -112f)),
        new Shot("03-crack-corridor",     new Vector3(7.2f, 1.6f, -106.7f),   new Vector3(20f, 1.6f, -106.7f)),
        new Shot("03b-crack-back",        new Vector3(18.8f, 1.6f, -106.7f),  new Vector3(6f, 1.6f, -106.7f)),
        new Shot("04-yellow-room",        new Vector3(21.5f, 1.6f, -100.5f),  new Vector3(30f, 1.3f, -114.5f)),
        new Shot("04b-yellow-props",      new Vector3(22f, 1.6f, -112.5f),    new Vector3(34.5f, 1.2f, -100.5f)),
        new Shot("05-angel-room",         new Vector3(28.15f, 1.6f, -116.5f), new Vector3(28.15f, 2.2f, -142f)),
        new Shot("05b-angel-close",       new Vector3(30f, 1.6f, -130f),      new Vector3(24.15f, 3.2f, -138.7f)),
        new Shot("05c-angel-back",        new Vector3(28.15f, 1.6f, -139f),   new Vector3(28.15f, 3f, -114f)),
        new Shot("06-same-intro-arrive",  new Vector3(28.15f, 1.6f, -190f),   new Vector3(28.15f, 1.5f, -230f)),
        new Shot("07-same-intro-mannequin", new Vector3(27.35f, 1.6f, -214f), new Vector3(27.35f, -0.3f, -221f)),
        // 방 사이 경계(A-6): 양쪽 방향
        new Shot("10-door-hall-to-crack", new Vector3(3.6f, 1.6f, -104.5f),   new Vector3(12f, 1.4f, -106.7f)),
        new Shot("11-door-crack-to-hall", new Vector3(11f, 1.6f, -106.7f),    new Vector3(0f, 1.2f, -106.7f)),
        new Shot("12-door-yellow-to-angel", new Vector3(28.15f, 1.6f, -109f), new Vector3(27f, 2.5f, -135f)),
        new Shot("13-door-angel-to-same", new Vector3(28.15f, 1.6f, -137f),   new Vector3(28.15f, 1.6f, -150f)),
        new Shot("14-door-same-back-to-angel", new Vector3(28.15f, 1.6f, -150f), new Vector3(28.15f, 1.8f, -135f)),
    };

    // 처음부터 끝까지 걸어가는 길(발 위치 x,z). 인트로 시작점 → 종료 트리거.
    static readonly Vector2[] WalkPath =
    {
        new Vector2(0, 22), new Vector2(8.4f, 19.5f), new Vector2(8.4f, -19.5f), new Vector2(0, -25), new Vector2(0, -48),
        new Vector2(4.5f, -56), new Vector2(4.5f, -66), new Vector2(0, -68.5f), new Vector2(0, -78), new Vector2(0, -84.8f),
        new Vector2(5.4f, -85.6f), new Vector2(5.4f, -105.2f), new Vector2(5.4f, -106.7f), new Vector2(12, -106.7f), new Vector2(21, -106.7f),
        new Vector2(28.15f, -108), new Vector2(28.15f, -116), new Vector2(31.5f, -128), new Vector2(28.15f, -141), new Vector2(28.15f, -146.5f),
        new Vector2(32.5f, -150), new Vector2(32.5f, -160), new Vector2(28.15f, -166), new Vector2(28.15f, -189), new Vector2(19.75f, -195.5f),
        new Vector2(19.75f, -234.5f), new Vector2(28.15f, -237),
    };

    static string capDir; static HashSet<string> capOnly; static bool capWalk;
    static int capFrame, capShot, capPhase, walkIdx; static float walkStuck, capStart; static Vector3 walkLast;
    static RenderTexture capRt; static Texture2D capTex;
    static readonly List<string> walkLog = new List<string>();

    public static void CaptureBatch()
    {
        var args = System.Environment.GetCommandLineArgs();
        capDir = ArgValue(args, "-gbShots") ?? "Temp/gb-shots";
        var only = ArgValue(args, "-gbOnly");
        capOnly = only != null ? new HashSet<string>(only.Split(',')) : null;
        capWalk = System.Array.IndexOf(args, "-gbWalk") >= 0;
        Directory.CreateDirectory(capDir);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var variant = ArgValue(args, "-gbVariant");
        if (variant != null) ApplyEndingVariant(variant); // 저장하지 않는 임시 변형(A-5 후보 비교용)
        capFrame = 0; capShot = 0; capPhase = 0;
        EditorApplication.update += CaptureTick;
        EditorApplication.isPlaying = true;
    }

    static string ArgValue(string[] args, string key)
    {
        int i = System.Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static void CaptureTick()
    {
        if (!EditorApplication.isPlaying) return;
        try { CaptureStep(); }
        catch (System.Exception e) { Debug.LogError("[GreyboxGraphicsPass] 캡처 실패: " + e); EditorApplication.Exit(1); }
    }

    static void CaptureStep()
    {
        capFrame++;
        var cam = Camera.main;
        if (capPhase == 0)
        {
            if (capFrame < 150) return; // 씬 스크립트·텍스처·물 시뮬레이션 준비 대기
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null) pc.enabled = false;
            capRt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            capTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            capPhase = 5; capFrame = 0; capStart = Time.realtimeSinceStartup;
            return;
        }
        if (capPhase == 5)
        {
            // 예열: 캡처용 렌더 타깃으로 처음 그릴 때는 하늘 앰비언트(비동기 계산)가 아직 없어 그늘이 검게 나온다.
            // 첫 장을 찍기 전에 같은 타깃으로 여러 프레임 그려 둔다.
            cam.targetTexture = capRt; cam.Render(); cam.targetTexture = null;
            if (capFrame >= 60) { capPhase = 1; capFrame = 0; }
            return;
        }
        if (capPhase == 1)
        {
            while (capShot < Shots.Length && capOnly != null && !capOnly.Contains(Shots[capShot].name.Substring(0, Shots[capShot].name.IndexOf('-')))) capShot++;
            if (capShot >= Shots.Length) { capPhase = capWalk ? 2 : 9; capFrame = 0; return; }
            var s = Shots[capShot];
            if (capFrame == 1) { cam.transform.position = s.pos; cam.transform.LookAt(s.look); return; }
            if (capFrame < 45) return; // 모션 블러·노출·볼류메트릭 이력이 가라앉을 때까지
            cam.targetTexture = capRt; for (int k = 0; k < 3; k++) cam.Render(); cam.targetTexture = null;
            RenderTexture.active = capRt; capTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); capTex.Apply(); RenderTexture.active = null;
            File.WriteAllBytes($"{capDir}/{s.name}.png", capTex.EncodeToPNG());
            Debug.Log($"[GreyboxGraphicsPass] shot {s.name} fps~{1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):F0}");
            capShot++; capFrame = 0;
            return;
        }
        if (capPhase == 2) { WalkStep(); return; }
        if (capPhase == 3)
        {
            // 종료 트리거가 페이드아웃 뒤 종료 패널을 켜는지 확인한다(DemoEndTrigger는 수정하지 않고 결과만 읽는다).
            if (Time.time - capStart < 4f) return; // 페이드아웃(0.8초)이 끝날 때까지 게임 시간으로 기다린다
            var end = Object.FindFirstObjectByType<DemoEndTrigger>();
            bool panel = end != null && end.demoEndPanel != null && end.demoEndPanel.activeInHierarchy;
            walkLog.Add(panel ? "END 종료 패널이 켜졌다" : "END 종료 패널이 켜지지 않았다 (패널 연결: " + (end != null && end.demoEndPanel != null) + ")");
            capPhase = 9; return;
        }
        if (capPhase == 9)
        {
            if (walkLog.Count > 0) File.WriteAllLines($"{capDir}/walk-log.txt", walkLog);
            EditorApplication.Exit(0);
        }
    }

    // PlayerController를 끄고 CharacterController.Move로 길을 따라 민다. 직접 플레이가 아니라 스크립트 보행 검사다.
    static void WalkStep()
    {
        var cc = Object.FindFirstObjectByType<CharacterController>();
        if (cc == null) { walkLog.Add("FAIL CharacterController 없음"); capPhase = 9; return; }
        if (capFrame == 1)
        {
            cc.enabled = false; cc.transform.position = new Vector3(WalkPath[0].x, 0.05f, WalkPath[0].y); cc.enabled = true;
            walkIdx = 1; walkStuck = 0; walkLast = cc.transform.position; walkLog.Add("START " + cc.transform.position);
            return;
        }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        var target = new Vector3(WalkPath[walkIdx].x, cc.transform.position.y, WalkPath[walkIdx].y);
        var to = target - cc.transform.position; to.y = 0;
        if (to.magnitude < 0.35f)
        {
            walkLog.Add($"OK {walkIdx} {WalkPath[walkIdx]} y={cc.transform.position.y:F2}");
            walkIdx++;
            if (walkIdx >= WalkPath.Length)
            {
                var end = Object.FindFirstObjectByType<DemoEndTrigger>();
                walkLog.Add("DONE 끝 지점 도착. DemoEndTrigger와 거리 " + (end ? Vector3.Distance(end.transform.position, cc.transform.position).ToString("F1") : "없음"));
                capPhase = 3; capStart = Time.time;
            }
            return;
        }
        cc.Move(to.normalized * 5.5f * dt + Vector3.down * 6f * dt);
        if ((cc.transform.position - walkLast).magnitude < 0.5f * dt) walkStuck += dt; else walkStuck = 0;
        walkLast = cc.transform.position;
        if (walkStuck > 2f) { walkLog.Add($"FAIL 막힘: 웨이포인트 {walkIdx} {WalkPath[walkIdx]} 가는 중 위치 {cc.transform.position}"); capPhase = 9; }
    }

    // A-5 후보 비교용. 씬에 저장하지 않고 캡처할 때만 적용한다.
    //   hue      : ⑤ 수영장에서만 하늘과 빛의 색조가 살짝 다르다(청록 쪽).
    //   noshadow : 해는 비추는데 그림자가 없다("해가 없는데 빛이 있다").
    //   sunflip  : 해 방향을 180도 돌린다. ⑤는 인트로를 180도 돌려 놓은 복사본이라, 저장된 상태에서는 이미 그림자가 인트로와 반대다. 이 변형은 인트로와 같은 방향으로 되돌린 모습이다.
    static void ApplyEndingVariant(string name)
    {
        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional) sun = l;
        if (name == "noshadow") sun.shadows = LightShadows.None;
        else if (name == "sunflip") sun.transform.Rotate(0, 180, 0, Space.World);
        else if (name == "hue")
        {
            var go = new GameObject("__A5_HueVolume") { layer = 2 };
            go.transform.position = new Vector3(28.15f, 7.65f, -194f);
            var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(104f, 30f, 100f);
            var v = go.AddComponent<UnityEngine.Rendering.Volume>(); v.isGlobal = false; v.blendDistance = 3f; v.priority = 105f;
            var p = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var wb = p.Add<UnityEngine.Rendering.HighDefinition.WhiteBalance>(true);
            wb.temperature.Override(-14f); wb.tint.Override(-16f);
            v.sharedProfile = p;
        }
        Debug.Log("[GreyboxGraphicsPass] variant " + name);
    }
}
