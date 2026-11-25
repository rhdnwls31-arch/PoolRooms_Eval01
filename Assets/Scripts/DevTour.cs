using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 개발용 빌드 점검(플레이어에게는 보이지 않는다). 실행 인수 <c>--devtour 출력폴더</c>가 있을 때만 켜진다.
/// 본편 씬을 열고, 방마다(이상한 점이 있는 방 + 시작 자리 + 인형 방) 플레이어를 옮겨
/// 한 바퀴 돌며 FPS(평균, 가장 느린 1% 프레임)를 재고, 이상한 점 쪽을 본 화면을 저장한 뒤 끈다.
/// 결과: 출력폴더/tour.csv, 출력폴더/NN-방이름.png
/// </summary>
public class DevTour : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Arg("--devtour") == null || FindAnyObjectByType<DevTour>() != null) return;
        var go = new GameObject(nameof(DevTour));
        DontDestroyOnLoad(go);
        go.AddComponent<DevTour>();
    }

    static string Arg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        return null;
    }

    struct Stop { public string name; public Vector3 pos; public Vector3 look; public GazeTarget gaze; }
    const float GazeMeasure = 1.2f; // 바라보며 재는 시간(초)
    readonly FrameTiming[] timing = new FrameTiming[1];

    // ---- --ab 설정 비교(2026-11-25 성능 단계) ----
    const float AbSeconds = 1.2f;
    string abSetting;
    string[] abSettings = new string[0];
    UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData abHd;
    readonly StringBuilder ab = new StringBuilder("room,setting,baseMs,variantMs,base2Ms,widthBase,widthVariant\n");

    /// <summary>
    /// 비교할 설정의 기본(false)/바꾼 상태(true).
    /// dlss: 바꾼 상태 = DLSS 켬(기본 = 끔). fs:이름: 바꾼 상태 = 그 HDRP 기능(FrameSettingsField)만 끔(기본 = 프로젝트 설정 그대로).
    /// </summary>
    void SetAb(string setting, bool variant)
    {
        var hd = abHd;
        if (setting == "dlss") { hd.allowDeepLearningSuperSampling = variant; hd.allowDynamicResolution = variant; return; }
        hd.allowDeepLearningSuperSampling = false; hd.allowDynamicResolution = false; // 해상도 영향 없이 기능만 비교
        if (setting == "cams")
        {
            // 플레이어 눈 말고 다른 카메라(거실 TV 화면용)를 끈다
            foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                if (c != Camera.main) { if (variant) { if (c.enabled) { c.enabled = false; abCams.Add(c); } } }
            if (!variant) { foreach (var c in abCams) if (c != null) c.enabled = true; abCams.Clear(); }
            return;
        }
        if (setting == "rooms") { SetOtherRoomsHidden(variant); return; }
        if (setting == "cull") { RoomCulling.Active = variant; return; } // 바꾼 상태 = 먼 방 안 그리기 켬(기본 = 끔)
        if (setting == "tvnear")
        {
            // 바꾼 상태 = TV 카메라 근처에서만(지금 설정), 기본 = 늘 켬(예전)
            foreach (var n in FindObjectsByType<CameraOnlyNear>(FindObjectsInactive.Include))
            {
                n.enabled = variant;
                if (!variant) n.GetComponent<Camera>().enabled = true;
            }
            return;
        }
        if (setting.StartsWith("fs:") && System.Enum.TryParse(setting.Substring(3), out UnityEngine.Rendering.HighDefinition.FrameSettingsField f))
        {
            hd.customRenderingSettings = variant;
            hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)f] = variant;
            hd.renderingPathCustomFrameSettings.SetEnabled(f, !variant);
        }
    }

    readonly List<Camera> abCams = new List<Camera>();
    readonly List<Behaviour> abHidden = new List<Behaviour>();
    readonly List<Renderer> abHiddenR = new List<Renderer>();

    /// <summary>
    /// 방 나눠 그리기의 최대 효과 가늠: 카메라가 들어 있지 않은 방(이름이 GB_로 시작하는 맨 위 오브젝트, 범위 5m 여유)의
    /// 렌더러·빛·물을 모두 끈다.
    /// </summary>
    void SetOtherRoomsHidden(bool hide)
    {
        if (!hide)
        {
            foreach (var r in abHiddenR) if (r != null) r.enabled = true;
            foreach (var b in abHidden) if (b != null) b.enabled = true;
            abHiddenR.Clear(); abHidden.Clear();
            return;
        }
        var eye = Camera.main.transform.position;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!root.name.StartsWith("GB_")) continue;
            var rs = root.GetComponentsInChildren<Renderer>(false);
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            b.Expand(10f);
            if (b.Contains(eye)) continue;
            foreach (var r in rs) if (r.enabled) { r.enabled = false; abHiddenR.Add(r); }
            foreach (var l in root.GetComponentsInChildren<Light>(false)) if (l.enabled) { l.enabled = false; abHidden.Add(l); }
            foreach (var w in root.GetComponentsInChildren<UnityEngine.Rendering.HighDefinition.WaterSurface>(false)) if (w.enabled) { w.enabled = false; abHidden.Add(w); }
        }
    }

    /// <summary>플레이어 카메라가 실제로 그리는 가로 해상도(DLSS면 낮다).</summary>
    static int HdWidth(Camera c)
    {
        var h = UnityEngine.Rendering.HighDefinition.HDCamera.GetOrCreate(c);
        return h != null ? h.actualWidth : 0;
    }

    /// <summary>secs초 동안 그래픽 카드 시간 평균(ms) → into[0], CPU 메인 → into[1].</summary>
    IEnumerator MeasureGpu(float secs, float[] into)
    {
        double g = 0, c = 0; int n = 0;
        for (float t = 0f; t < secs; t += Time.unscaledDeltaTime)
        {
            yield return null;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0) { g += timing[0].gpuFrameTime; c += timing[0].cpuMainThreadFrameTime; n++; }
        }
        into[0] = n > 0 ? (float)(g / n) : 0f; into[1] = n > 0 ? (float)(c / n) : 0f;
    }

    /// <summary>한 구간 프레임 시간들에서 평균 FPS와 가장 느린 1% 평균 FPS를 낸다.</summary>
    public static Vector2 Summarize(List<float> frameTimes)
    {
        if (frameTimes.Count == 0) return Vector2.zero;
        float sum = 0f; foreach (var t in frameTimes) sum += t;
        var sorted = new List<float>(frameTimes); sorted.Sort(); sorted.Reverse();
        int n = Mathf.Max(1, sorted.Count / 100); float worst = 0f;
        for (int i = 0; i < n; i++) worst += sorted[i];
        return new Vector2(frameTimes.Count / sum, n / worst);
    }

    IEnumerator Start()
    {
        string outDir = Arg("--devtour");
        Directory.CreateDirectory(outDir);
        string scene = Arg("--scene") ?? "Scene_DescentGreybox";
        if (SceneManager.GetActiveScene().name != scene) yield return SceneManager.LoadSceneAsync(scene);
        for (int i = 0; i < 90; i++) yield return null; // 첫 프레임들(어두움, 시작 연출이 플레이어를 다시 놓음)

        var pc = FindAnyObjectByType<PlayerController>();
        var cc = pc != null ? pc.GetComponent<CharacterController>() : null;
        var cam = Camera.main;
        abSetting = Arg("--ab");
        if (abSetting != null && cam != null && cam.TryGetComponent(out abHd)) abSettings = abSetting.Split(',');
        // --nodlss: 같은 빌드에서 DLSS 끈 것과 비교하려고(원래 해상도로 그린다)
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--nodlss") >= 0 && cam != null &&
            cam.TryGetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>(out var hdCam))
        {
            hdCam.allowDeepLearningSuperSampling = false;
            hdCam.allowDynamicResolution = false;
        }
        var stops = new List<Stop>();
        if (pc != null) stops.Add(new Stop { name = "Start", pos = pc.transform.position, look = pc.transform.position + pc.transform.forward * 5f });
        var gazes = new List<GazeTarget>(FindObjectsByType<GazeTarget>(FindObjectsInactive.Include));
        gazes.Sort((a, b) => (a.gate != null ? a.gate.order : 99).CompareTo(b.gate != null ? b.gate.order : 99));
        foreach (var g in gazes)
        {
            if (g.roomBounds.size == Vector3.zero) continue;
            stops.Add(new Stop { name = g.id, pos = Floor(g.roomBounds.center, g.roomBounds), look = g.GazeCenter, gaze = g });
        }
        foreach (var d in FindObjectsByType<DollStepper>(FindObjectsInactive.Include))
            stops.Add(new Stop { name = "DollRoom", pos = Floor(d.transform.position + d.transform.forward * 2f, d.room), look = d.transform.position + d.transform.forward * 12f + Vector3.up * 1.5f });

        if (Arg("--cullcheck") != null)
        {
            yield return CullCheck(outDir, pc, cc, cam, stops);
            Application.Quit();
            yield break;
        }

        var csv = new StringBuilder("index,room,avgFps,low1Fps,cpuMainMs,cpuRenderMs,gpuMs,gazeMaxMs,gazeReached,x,y,z\n");
        var frames = new List<float>();
        for (int s = 0; s < stops.Count; s++)
        {
            var st = stops[s];
            if (cc != null) cc.enabled = false;
            pc.transform.position = st.pos;
            Vector3 flat = st.look - st.pos; flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) pc.transform.rotation = Quaternion.LookRotation(flat);
            if (cc != null) cc.enabled = true;
            for (int i = 0; i < 30; i++) yield return null; // 볼륨·노출이 자리 잡을 때까지
            // 한 바퀴 돌며 잰다(가장 무거운 방향도 들어가게)
            frames.Clear();
            float t = 0f, spin = 4f;
            double cpuMain = 0, cpuRender = 0, gpu = 0; int timed = 0;
            while (t < spin)
            {
                pc.transform.Rotate(0f, 360f / spin * Time.unscaledDeltaTime, 0f);
                frames.Add(Time.unscaledDeltaTime); t += Time.unscaledDeltaTime;
                // CPU(게임 코드·그리기 준비)와 GPU(그래픽 카드) 중 어디가 느린지(Player Settings의 Frame Timing Stats 필요)
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0)
                {
                    cpuMain += timing[0].cpuMainThreadFrameTime; cpuRender += timing[0].cpuRenderThreadFrameTime; gpu += timing[0].gpuFrameTime; timed++;
                }
                yield return null;
            }
            if (timed > 0) { cpuMain /= timed; cpuRender /= timed; gpu /= timed; }
            var r = Summarize(frames);
            if (flat.sqrMagnitude > 0.01f) pc.transform.rotation = Quaternion.LookRotation(flat);
            // --ab 설정: 같은 자리·같은 방향에서 설정을 켬 → 끔 → 켬으로 바로 이어 재서, 다른 프로그램 때문에 오르내리는 그래픽 카드 부하를 상쇄한다.
            if (abHd != null)
            {
                var m = new float[3];
                cam.transform.LookAt(st.look);
                foreach (var setting in abSettings)
                {
                    SetAb(setting, false); for (int i = 0; i < 15; i++) yield return null;
                    yield return MeasureGpu(AbSeconds, m); float a1 = m[0]; int wA = HdWidth(cam);
                    SetAb(setting, true); for (int i = 0; i < 15; i++) yield return null;
                    yield return MeasureGpu(AbSeconds, m); float b = m[0]; int wB = HdWidth(cam);
                    SetAb(setting, false); for (int i = 0; i < 15; i++) yield return null;
                    yield return MeasureGpu(AbSeconds, m); float a2 = m[0];
                    ab.AppendLine(string.Join(",", st.name, setting, a1.ToString("F2", CultureInfo.InvariantCulture), b.ToString("F2", CultureInfo.InvariantCulture),
                        a2.ToString("F2", CultureInfo.InvariantCulture), wA.ToString(), wB.ToString()));
                }
            }
            // 이상한 점 가까이(찾을 수 있는 거리 안) 가서 바라보는 동안 가장 긴 프레임(ms). 바라보기 시작할 때 끊기는지 본다(2026-11-25).
            float gazeMax = 0f, previewMax = 0f;
            if (st.gaze != null)
            {
                Vector3 c = st.gaze.GazeCenter;
                Vector3 dir = c - st.pos; dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = pc.transform.forward;
                dir.Normalize();
                float d = Mathf.Min(st.gaze.maxDistance * 0.6f, 3.5f);
                if (cc != null) cc.enabled = false;
                pc.transform.position = Floor(c - dir * d, st.gaze.roomBounds);
                pc.transform.rotation = Quaternion.LookRotation(dir);
                if (cc != null) cc.enabled = true;
                for (int i = 0; i < 2; i++) yield return null;
                for (float g = 0f; g < GazeMeasure; g += Time.unscaledDeltaTime)
                {
                    if (cam != null) cam.transform.LookAt(c);
                    yield return null;
                    gazeMax = Mathf.Max(gazeMax, Time.unscaledDeltaTime * 1000f);
                    previewMax = Mathf.Max(previewMax, st.gaze.Found ? 1f : st.gaze.PreviewAmount);
                }
            }
            else
            {
                if (cam != null) cam.transform.LookAt(st.look);
                for (int i = 0; i < 20; i++) yield return null;
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, s.ToString("00") + "-" + st.name + ".png"));
            csv.AppendLine(string.Join(",", s.ToString(), st.name, r.x.ToString("F1", CultureInfo.InvariantCulture), r.y.ToString("F1", CultureInfo.InvariantCulture),
                cpuMain.ToString("F1", CultureInfo.InvariantCulture), cpuRender.ToString("F1", CultureInfo.InvariantCulture), gpu.ToString("F1", CultureInfo.InvariantCulture),
                gazeMax.ToString("F1", CultureInfo.InvariantCulture), previewMax.ToString("F2", CultureInfo.InvariantCulture),
                st.pos.x.ToString("F1", CultureInfo.InvariantCulture), st.pos.y.ToString("F1", CultureInfo.InvariantCulture), st.pos.z.ToString("F1", CultureInfo.InvariantCulture)));
            yield return null;
        }
        File.WriteAllText(Path.Combine(outDir, "tour.csv"), csv.ToString());
        if (abHd != null) File.WriteAllText(Path.Combine(outDir, "ab.csv"), ab.ToString());
        // 어떤 그래픽 API로, DLSS가 실제로 켜졌는지(해상도 배율)
        File.WriteAllText(Path.Combine(outDir, "info.txt"),
            "api " + SystemInfo.graphicsDeviceType + "\ngpu " + SystemInfo.graphicsDeviceName +
            "\ndlssDetected " + UnityEngine.Rendering.HighDefinition.HDDynamicResolutionPlatformCapabilities.DLSSDetected +
            "\nscale " + UnityEngine.Rendering.DynamicResolutionHandler.instance.GetCurrentScale().ToString("F3", CultureInfo.InvariantCulture) +
            "\nmainRenderWidth " + (Camera.main != null ? HdWidth(Camera.main) : 0) + " dlssSetting " + GameSettings.DlssOn + "\n");
        for (int i = 0; i < 5; i++) yield return null;
        Application.Quit();
    }

    // ---- --cullcheck: 먼 방 안 그리기(RoomCulling)가 보이는 화면을 바꾸지 않는지(2026-11-25) ----
    const float CullDiffThreshold = 0.2f;   // 이보다 밝기 차이가 큰 픽셀만 센다(필름 그레인 같은 잔 떨림은 빼고)
    const float CullDiffSave = 0.005f;      // 이 비율보다 많이 다르면 두 장을 저장해 둔다

    IEnumerator CullCheck(string outDir, PlayerController pc, CharacterController cc, Camera cam, List<Stop> stops)
    {
        var points = new List<Stop>(stops);
        foreach (var s in FindObjectsByType<SealBehindPassage>(FindObjectsInactive.Include))
            points.Add(new Stop { name = "Seal_" + s.name, pos = s.transform.position, look = s.transform.position + s.transform.forward });
        var csv = new StringBuilder("point,dir,diffFrac,hiddenRooms\n");
        var rc = FindAnyObjectByType<RoomCulling>();
        Vector3[] dirs = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        int saved = 0;
        foreach (var p in points)
        {
            if (cc != null) cc.enabled = false;
            pc.transform.position = p.pos.y > -500f ? FloorNear(p.pos) : p.pos;
            if (cc != null) cc.enabled = true;
            for (int d = 0; d < dirs.Length; d++)
            {
                pc.transform.rotation = Quaternion.LookRotation(dirs[d]);
                if (cam != null) cam.transform.localRotation = Quaternion.identity;
                RoomCulling.Active = true;
                for (int i = 0; i < 12; i++) yield return null;
                yield return new WaitForEndOfFrame();
                var on = ScreenCapture.CaptureScreenshotAsTexture();
                int hidden = rc != null ? rc.HiddenCount : -1;
                RoomCulling.Active = false;
                for (int i = 0; i < 12; i++) yield return null;
                yield return new WaitForEndOfFrame();
                var off = ScreenCapture.CaptureScreenshotAsTexture();
                float frac = DiffFraction(on, off);
                csv.AppendLine(string.Join(",", p.name, d.ToString(), frac.ToString("F4", CultureInfo.InvariantCulture), hidden.ToString()));
                if (frac > CullDiffSave && saved < 20)
                {
                    File.WriteAllBytes(Path.Combine(outDir, p.name + "-" + d + "-on.jpg"), on.EncodeToJPG(80));
                    File.WriteAllBytes(Path.Combine(outDir, p.name + "-" + d + "-off.jpg"), off.EncodeToJPG(80));
                    saved++;
                }
                Destroy(on); Destroy(off);
            }
        }
        RoomCulling.Active = true;
        File.WriteAllText(Path.Combine(outDir, "cullcheck.csv"), csv.ToString());
    }

    /// <summary>두 화면에서 밝기가 CullDiffThreshold보다 크게 다른 픽셀의 비율.</summary>
    public static float DiffFraction(Texture2D a, Texture2D b)
    {
        var pa = a.GetPixels32(); var pb = b.GetPixels32();
        int n = Mathf.Min(pa.Length, pb.Length), diff = 0;
        if (n == 0) return 0f;
        for (int i = 0; i < n; i++)
        {
            float la = (pa[i].r * 0.299f + pa[i].g * 0.587f + pa[i].b * 0.114f) / 255f;
            float lb = (pb[i].r * 0.299f + pb[i].g * 0.587f + pb[i].b * 0.114f) / 255f;
            if (Mathf.Abs(la - lb) > CullDiffThreshold) diff++;
        }
        return diff / (float)n;
    }

    /// <summary>통로 표시 위치 아래 바닥(없으면 그대로).</summary>
    static Vector3 FloorNear(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)) return hit.point + Vector3.up * 0.1f;
        return p;
    }

    /// <summary>방 가운데 아래 바닥(없으면 방 바닥 높이).</summary>
    static Vector3 Floor(Vector3 p, Bounds room)
    {
        Vector3 top = new Vector3(p.x, Mathf.Min(room.max.y, p.y + 1f), p.z);
        if (Physics.Raycast(top, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)) return hit.point + Vector3.up * 0.1f;
        return new Vector3(p.x, room.min.y + 0.1f, p.z);
    }
}
