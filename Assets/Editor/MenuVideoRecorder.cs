using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 메인 메뉴 배경 영상 녹화(2026-11-27). 본편 씬을 Play로 열고, 장면마다 카메라를 고정한 채
/// Unity Recorder로 MP4를 찍는다. 녹화 중에는 HUD를 끄고 플레이어 조작과 응시 판정(GazeTarget)을 멈춘다.
/// TV 효과는 2026-11-27 사용자 요청으로 '옅은 노이즈만' 영상에 넣는다: 주사선·RGB 줄무늬·내려가는 띠·밀리는 줄과
/// 인게임 화면 노이즈(InGameNoiseOverlay)는 남기고, 유리 휨·둥근 모서리·가장자리 어둠(TV 테두리)은 뺀다.
/// 실행: [DEPTH 5m > Menu > Record Menu Videos] (영상) / [Preview Menu Shots] (구도 확인용 정지 화면)
/// 결과: Assets/Videos/Menu/Resources/MenuVideos/menu_0N_이름.mp4 (메인 메뉴 MenuBackgroundVideo가 이름 순서로 튼다) / production/qa/evidence/menu-video/shot_0N.png
/// </summary>
[InitializeOnLoad]
public static class MenuVideoRecorder
{
    const string ScenePath = "Assets/Scenes/Greybox/Scene_DescentGreybox.unity";
    const string VideoDir = "Assets/Videos/Menu/Resources/" + MenuBackgroundVideo.ResourceFolder; // 메뉴가 Resources로 불러 쓴다
    const string StillDir = "production/qa/evidence/menu-video";
    const string ModeKey = "MenuVideoRecorder.Mode"; // 0 = 꺼짐, 1 = 영상, 2 = 정지 화면
    const int Width = 1920, Height = 1080, Fps = 30;
    const float WarmupSeconds = 2.5f; // 노출·물결·볼륨이 자리 잡을 때까지

    // 영상에 넣는 옅은 TV 노이즈(인게임 값 GreyboxGraphicsPass.Crt보다 약하게, 테두리 없이)
    const float VideoScanline = 0.18f;   // 인게임 0.3
    const float VideoPhosphor = 0.08f;   // 인게임 0.12
    const float VideoRollBand = 0.05f;   // 인게임과 같음
    const float VideoJitter = 0.5f;      // 인게임과 같음

    /// <summary>고정 카메라 한 장면. room = RoomRoute 방 번호(그 방 상태로 맞춘 뒤 찍는다).</summary>
    public struct Shot
    {
        public string name; public int room; public Vector3 pos; public Vector3 look; public float fov; public float seconds;
        /// <summary>찍는 동안 숨길 오브젝트 이름(인물·이상한 점 — 메뉴에는 무서운 요소와 스포를 넣지 않는다).</summary>
        public string[] hide;
        /// <summary>
        /// 0보다 크면 가라앉은 도시의 물(FloodRise)을 켜고 초당 이만큼(m) 일정하게 차오르게 한다.
        /// 2026-11-27 사용자 피드백: 게임처럼 처음엔 느리다 빨라지는 곡선이면 물이 바닥 위로 '갑자기' 나타나 어색했다 →
        /// 영상에서는 처음부터 물이 조금 찬 상태(floodStartY)에서 같은 속도로 쭉 차오른다.
        /// </summary>
        public float floodRate;
        /// <summary>물이 켜질 때의 높이(m, 바닥 0). 녹화 시작 때는 대기 시간만큼 더 차 있다.</summary>
        public float floodStartY;
        /// <summary>녹화 시작 전 기다리는 시간(노출·물결이 자리 잡을 때까지).</summary>
        public float Warmup => WarmupSeconds;
    }

    /// <summary>
    /// 장면 목록. 구도: 제목이 위 가운데, 메뉴가 오른쪽 가운데라 주인공(튜브 등)은 가운데~왼쪽에,
    /// 오른쪽은 비교적 조용하게 둔다.
    /// </summary>
    public static readonly Shot[] Shots =
    {
        new Shot { name = "flamingo", room = 0, pos = new Vector3(0.4f, 0.95f, 11.0f), look = new Vector3(-0.6f, 0.4f, 0.5f), fov = 50f, seconds = 14f },
        new Shot { name = "poolhall", room = 1, pos = new Vector3(-2.0f, 1.5f, -88.5f), look = new Vector3(1.0f, 4.0f, -112f), fov = 55f, seconds = 14f,
                   hide = new[] { "VARCO_SwimCapFigure" } },
        new Shot { name = "city", room = 9, pos = new Vector3(130f, 2.4f, -143f), look = new Vector3(126f, 2.2f, -165f), fov = 60f, seconds = 14f,
                   hide = new[] { "Oddity_FloodedWindow" }, floodStartY = 0.08f, floodRate = 0.05f }, // 녹화 시작 약 0.2m → 끝 약 0.9m
    };

    static MenuVideoRecorder()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("DEPTH 5m/Menu/Record Menu Videos")]
    static void RecordVideos() => Begin(1);

    [MenuItem("DEPTH 5m/Menu/Preview Menu Shots")]
    static void PreviewShots() => Begin(2);

    static void Begin(int mode)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[MenuVideoRecorder] Play를 멈춘 뒤 다시 실행하세요."); return; }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        SessionState.SetInt(ModeKey, mode);
        EditorApplication.isPlaying = true;
    }

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(ModeKey, 0) != 0)
        {
            runner = new Runner(SessionState.GetInt(ModeKey, 0));
            SessionState.SetInt(ModeKey, 0);
            EditorApplication.update += Tick;
        }
        else if (s == PlayModeStateChange.ExitingPlayMode && runner != null)
        {
            runner.Abort();
            runner = null;
            EditorApplication.update -= Tick;
        }
    }

    static Runner runner;
    static void Tick()
    {
        if (runner == null) { EditorApplication.update -= Tick; return; }
        if (!runner.Step())
        {
            runner = null;
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            AssetDatabase.Refresh();
        }
    }

    /// <summary>장면을 하나씩: 맞추기 → 기다리기 → 녹화(또는 정지 화면) → 다음.</summary>
    class Runner
    {
        readonly int mode;
        int index = -1;
        int phase; // 0 = 맞추기, 1 = 기다리기, 2 = 녹화 중
        float phaseStart;
        RecorderController recorder;
        PlayerController pc;
        CharacterController cc;
        Camera cam;
        Vector3 introPos; Quaternion introRot;
        readonly List<Behaviour> disabled = new List<Behaviour>();
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        GameObject overrideVolume;

        public Runner(int mode) { this.mode = mode; }

        public bool Step()
        {
            if (index < 0)
            {
                if (Time.frameCount < 5) return true; // 씬 Start가 끝나도록
                if (!Setup()) return false;
                index = 0; phase = 0;
            }
            if (index >= Shots.Length) { Abort(); Debug.Log("[MenuVideoRecorder] 완료"); return false; }
            var shot = Shots[index];

            if (phase == 0)
            {
                Stage(shot);
                phase = 1; phaseStart = Time.time;
            }
            else if (phase == 1)
            {
                Aim(shot);
                if (Time.time - phaseStart < shot.Warmup) return true;
                // 정지 화면은 물이 차오르는 장면이면 영상 끝무렵(가장 많이 찬 때)을 보여 준다
                if (mode == 2 && shot.floodRate > 0f &&Time.time - phaseStart < shot.Warmup + shot.seconds) return true;
                if (mode == 2)
                {
                    Directory.CreateDirectory(StillDir);
                    ScreenCapture.CaptureScreenshot($"{StillDir}/shot_{index + 1:00}_{shot.name}.png");
                    Debug.Log($"[MenuVideoRecorder] 정지 화면 {shot.name}: 카메라 {cam.transform.position} {cam.transform.eulerAngles} fov {cam.fieldOfView}");
                    phase = 3; phaseStart = Time.time; // 캡처가 다음 프레임에 저장되도록 잠깐 쉰다
                    return true;
                }
                Debug.Log($"[MenuVideoRecorder] {shot.name}: 카메라 {cam.transform.position} {cam.transform.eulerAngles} fov {cam.fieldOfView}");
                StartRecording(index, shot);
                Directory.CreateDirectory(StillDir); // 영상 첫 화면을 증거로 남긴다
                ScreenCapture.CaptureScreenshot($"{StillDir}/shot_{index + 1:00}_{shot.name}.png");
                phase = 2; phaseStart = Time.time;
            }
            else if (phase == 2)
            {
                Aim(shot);
                if (Time.time - phaseStart < shot.seconds) return true;
                recorder.StopRecording(); recorder = null;
                Debug.Log($"[MenuVideoRecorder] 녹화 끝 {shot.name}");
                index++; phase = 0;
            }
            else if (phase == 3)
            {
                if (Time.time - phaseStart < 0.3f) return true;
                index++; phase = 0;
            }
            return true;
        }

        bool Setup()
        {
            pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("[MenuVideoRecorder] PlayerController를 찾지 못했다."); return false; }
            // 플레이어 눈 카메라(PlayerController.cameraTransform). 자식에 다른 카메라가 있어 GetComponentInChildren은 틀린 것을 잡는다.
            cam = pc.cameraTransform != null ? pc.cameraTransform.GetComponent<Camera>() : Camera.main;
            if (cam == null) cam = Camera.main;
            cc = pc.GetComponent<CharacterController>();
            introPos = pc.transform.position; introRot = pc.transform.rotation;

            // 플레이어 조작·응시 판정을 멈춘다(오래 바라봐서 이상한 점이 '찾음' 처리되지 않게)
            Disable(pc);
            foreach (var g in Object.FindObjectsByType<GazeTarget>(FindObjectsInactive.Include)) Disable(g);
            DisableHud();

            // TV 효과는 노이즈만 옅게: 우선순위 높은 전역 볼륨으로 덮는다(프로필 에셋은 건드리지 않음)
            overrideVolume = new GameObject("__MenuVideoCrtNoiseOnly");
            var v = overrideVolume.AddComponent<Volume>();
            v.isGlobal = true; v.priority = 10000f;
            v.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var crt = v.profile.Add<CrtScreen>(true);
            crt.intensity.Override(1f);
            crt.curvature.Override(0f);     // 테두리(휜 유리·둥근 모서리·가장자리 어둠)는 빼고
            crt.cornerRadius.Override(0f);
            crt.edgeDarken.Override(0f);
            crt.scanline.Override(VideoScanline); // 노이즈만 남긴다
            crt.phosphorMask.Override(VideoPhosphor);
            crt.rollBand.Override(VideoRollBand);
            crt.jitter.Override(VideoJitter);
            return true;
        }

        /// <summary>화면 위 UI(HUD 등)를 끈다. 인게임 화면 노이즈(그레인·주사선)는 영상에 남긴다.</summary>
        void DisableHud()
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                if (c.GetComponentInParent<InGameNoiseOverlay>(true) == null) Disable(c);
        }

        void Disable(Behaviour b)
        {
            if (b == null || !b.enabled) return;
            b.enabled = false; disabled.Add(b);
        }

        void Stage(Shot shot)
        {
            if (cc != null) cc.enabled = false;
            if (shot.room > 0) RoomRoute.Restore(shot.room, pc.transform, introPos, introRot);
            DisableHud(); // 방 맞추기가 새로 켠 UI까지
            if (shot.hide != null)
                foreach (var n in shot.hide)
                {
                    var go = GameObject.Find(n);
                    if (go == null) { Debug.LogWarning($"[MenuVideoRecorder] 숨길 '{n}'을(를) 찾지 못했다."); continue; }
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
                }
            if (shot.floodRate > 0f)
            {
                var flood = Object.FindAnyObjectByType<FloodRise>(FindObjectsInactive.Include);
                if (flood != null)
                {
                    // Play 중에만 바꾸는 값이라 씬에는 남지 않는다. 곡선을 직선(easePower 1)으로 두고 속도에 맞춰 시간을 정한다.
                    flood.startY = shot.floodStartY;
                    flood.easePower = 1f;
                    flood.duration = (flood.endY - flood.startY) / shot.floodRate;
                    flood.gameObject.SetActive(true); // 켜질 때(OnEnable) startY에서 시작한다
                }
            }
            Vector3 flat = shot.look - shot.pos; flat.y = 0f;
            pc.transform.SetPositionAndRotation(new Vector3(shot.pos.x, shot.pos.y - cam.transform.localPosition.y, shot.pos.z),
                                                flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat) : pc.transform.rotation);
            Aim(shot);
        }

        void Aim(Shot shot)
        {
            // 방 맞추기(RoomRoute.Restore)가 플레이어 조작을 다시 켜면 상하 시점을 덮어쓰므로 매 프레임 끈다
            if (pc.enabled) pc.enabled = false;
            cam.transform.SetPositionAndRotation(shot.pos, Quaternion.LookRotation(shot.look - shot.pos));
            cam.fieldOfView = shot.fov;
        }

        void StartRecording(int i, Shot shot)
        {
            Directory.CreateDirectory(VideoDir);
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "MenuVideo";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movie.CaptureAudio = false;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = Width, OutputHeight = Height };
            movie.OutputFile = $"{VideoDir}/menu_{i + 1:00}_{shot.name}";
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = Fps;
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.CapFrameRate = true;
            RecorderOptions.VerboseMode = false;
            recorder = new RecorderController(settings);
            recorder.PrepareRecording();
            recorder.StartRecording();
            Debug.Log($"[MenuVideoRecorder] 녹화 시작 {shot.name} ({shot.seconds}s)");
        }

        public void Abort()
        {
            if (recorder != null && recorder.IsRecording()) recorder.StopRecording();
            recorder = null;
            foreach (var b in disabled) if (b != null) b.enabled = true;
            disabled.Clear();
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            hiddenRenderers.Clear();
            if (overrideVolume != null) Object.Destroy(overrideVolume);
            if (cc != null) cc.enabled = true;
        }
    }
}
