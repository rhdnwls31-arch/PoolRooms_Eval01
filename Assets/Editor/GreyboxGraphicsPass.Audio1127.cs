using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 2026-11-27 사운드 단계. VARCO Sound에서 만든 소리(사용자가 골라 Assets/Audio/SFX에 넣음)를 다듬어 씬에 연결한다.
// 1) 다듬기: 앞 무음 자르기, 크기 맞추기(작게 들리던 소리 키움), 너무 긴 소리 자르기, 환경음은 이음매 없이 반복되게, 발소리는 한 걸음씩 자르기.
//    결과는 Assets/Audio/Game에 새 파일로 쓴다(원본은 그대로). 원본 이름 뒤 (GEN_n)은 무시하고 앞 이름으로 찾는다.
// 2) 연결: GameAudio(한 번 나는 소리, 방 환경음), 플레이어 FootstepAudio(발소리), 방마다 AudioZone.
public static partial class GreyboxGraphicsPass
{
    const string SfxSourceDir = "Assets/Audio/SFX";
    const string SfxGameDir = "Assets/Audio/Game";
    const string SfxMenuDir = "Assets/Audio/Menu/Resources/" + MenuAudio.ResourceFolder; // 메뉴 씬이 Resources로 불러온다

    enum SfxKind { OneShot, Loop, Steps, Music }

    struct SfxSpec
    {
        public string name, source; public SfxKind kind; public float maxSeconds, drive; public bool menu, menuCopy;
        public SfxSpec(string n, string s, SfxKind k, float max = 0f, float drive = 0f, bool menu = false, bool menuCopy = false)
        { name = n; source = s; kind = k; maxSeconds = max; this.drive = drive; this.menu = menu; this.menuCopy = menuCopy; }
        public string Dir => menu ? SfxMenuDir : SfxGameDir;
    }

    static readonly SfxSpec[] SfxSpecs =
    {
        new SfxSpec("found", "dream_chime", SfxKind.OneShot, 3.5f),
        new SfxSpec("hint", "music_box_note", SfxKind.OneShot, 2.5f),
        new SfxSpec("exit_open", "tile_passage", SfxKind.OneShot, 3f),
        new SfxSpec("doll_step", "doll_creak", SfxKind.OneShot, 2.5f),
        new SfxSpec("doll_final", "wood_doll_move", SfxKind.OneShot, 4.5f),
        new SfxSpec("knock", "door_knock", SfxKind.OneShot, 5f, drive: 3f), // 사용자: 노크는 중요하니 크게 → 눌러 키움
        new SfxSpec("fall", "faint_thud", SfxKind.OneShot, 3f),
        new SfxSpec("flicker", "flickering_light", SfxKind.OneShot, 3.5f),
        new SfxSpec("ending_swell", "dream_swell", SfxKind.OneShot),
        // 2026-11-27 2차 피드백
        new SfxSpec("door_close", "stone_slam", SfxKind.OneShot, 3.5f, drive: 1.5f), // 등 뒤 통로가 벽으로 막힘: 움찔할 "쾅"
        new SfxSpec("peep_eerie", "creepy_hum", SfxKind.OneShot, 9f),
        new SfxSpec("click", "mouse_click", SfxKind.OneShot, 1f),
        new SfxSpec("amb_blue", "bedroom_night", SfxKind.Loop, drive: 5f), // 4차: 그래도 작다 → 더 눌러 키움(조용한 틈을 끌어올림) // 사용자: 파란방이 여전히 안 들림 → 새로 뽑고 눌러 키움
        new SfxSpec("amb_flood", "flood_loop", SfxKind.Loop),
        // 3차: 물 들어감, 문 삐걱, 엔딩 비디오테이프, 떨어지는 바람
        new SfxSpec("water_enter", "pool_step", SfxKind.OneShot, 4f),
        new SfxSpec("door_creak", "door_creak", SfxKind.OneShot, 5f),
        new SfxSpec("tv_switch", "tv_switch", SfxKind.OneShot, 2.5f),
        new SfxSpec("amb_vhs", "vhs_loop", SfxKind.Loop),
        new SfxSpec("fall_wind", "falling_wind", SfxKind.OneShot, 5f),
        new SfxSpec("amb_pool", "pool_ambience", SfxKind.Loop, menuCopy: true),
        new SfxSpec("amb_corridor", "fluorescent_hum", SfxKind.Loop),
        new SfxSpec("amb_shower", "shower_drops", SfxKind.Loop),
        new SfxSpec("amb_yellow", "music_box", SfxKind.Loop),
        new SfxSpec("amb_living", "tv_static", SfxKind.Loop),
        new SfxSpec("amb_computer", "computer_loop", SfxKind.Loop),
        new SfxSpec("amb_city", "sunken_city", SfxKind.Loop, menuCopy: true),
        new SfxSpec("amb_sky", "open_sky", SfxKind.Loop),
        new SfxSpec("steps_tile", "tile_steps", SfxKind.Steps),
        new SfxSpec("steps_wet", "wet_steps", SfxKind.Steps), // 사용자: 젖은 발소리는 샤워실·물속에만
        new SfxSpec("steps_carpet", "carpet_steps", SfxKind.Steps),
        new SfxSpec("steps_wood", "barefoot_steps", SfxKind.Steps),
        new SfxSpec("steps_stone", "stone_steps", SfxKind.Steps),
        // 메인 메뉴(Resources/MenuAudio). 버튼에 마우스 올림·누름 소리는 사용자 결정으로 뺐다(2026-11-27).
        new SfxSpec("ui_start", "menu_start", SfxKind.OneShot, 3.5f, menu: true),
        new SfxSpec("amb_outdoor_pool", "outdoor_pool", SfxKind.Loop, menu: true),
        new SfxSpec("amb_crt", "crt_whine", SfxKind.Loop, menu: true),
        new SfxSpec("menu_theme", "menu_theme", SfxKind.Music, menu: true),
    };

    [MenuItem("DEPTH 5m/Audio/Apply Sound")]
    public static void ApplyAudioMenu()
    {
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyAudio();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GreyboxGraphicsPass] 소리 연결 완료");
    }

    static void ApplyAudio()
    {
        var made = ProcessSfx();
        WireGameAudio(made);
        WireFootsteps(made);
        WireAudioZones(made);
    }

    // ---------------------------------------------------------------- 1) 다듬기

    /// <summary>다듬은 소리들. 이름 → 클립(발소리는 이름_00, 이름_01 …).</summary>
    static Dictionary<string, AudioClip> ProcessSfx()
    {
        Directory.CreateDirectory(SfxGameDir);
        Directory.CreateDirectory(SfxMenuDir);
        var made = new Dictionary<string, AudioClip>();
        var written = new List<string>();
        foreach (var spec in SfxSpecs)
        {
            var sources = FindSfxSources(spec.source);
            if (sources.Count == 0)
            {
                Debug.LogWarning("[소리] 원본 없음: " + spec.source);
                // 원본이 바뀌었는데 예전 결과가 남아 그대로 쓰이지 않게 지운다
                if (spec.kind == SfxKind.Steps)
                    foreach (var old in Directory.GetFiles(spec.Dir, spec.name + "_*.wav")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
                else AssetDatabase.DeleteAsset($"{spec.Dir}/{spec.name}.wav");
                continue;
            }

            if (spec.kind == SfxKind.Steps)
            {
                // 같은 이름 원본이 여럿이면(예: tile_steps GEN_1, GEN_2) 모두에서 걸음을 잘라 모은다
                foreach (var old in Directory.GetFiles(spec.Dir, spec.name + "_*.wav")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
                int n = 0;
                foreach (var src in sources)
                {
                    if (!ReadSamples(src, out var s, out int ch, out int fq)) continue;
                    AudioCraft.Normalize(s, 0.89f);
                    var onsets = AudioCraft.FindOnsets(s, ch, fq, 0.35f, 0.28f);
                    for (int i = 0; i < onsets.Count; i++)
                    {
                        var part = AudioCraft.Slice(s, ch, fq, Mathf.Max(0, onsets[i] - fq / 100), 0.45f, 0.12f);
                        AudioCraft.Normalize(part, 0.89f);
                        written.Add(WriteSfx(spec.Dir, $"{spec.name}_{n++:00}", part, ch, fq));
                    }
                }
                Debug.Log($"[소리] {spec.name}: 걸음 {n}개 (원본 {sources.Count})");
                continue;
            }

            {
                if (!ReadSamples(sources[0], out var s, out int ch, out int fq)) continue;
                s = AudioCraft.TrimLeadingSilence(s, ch, fq, 0.02f, 0.02f);
                if (spec.kind == SfxKind.Loop || spec.kind == SfxKind.Music)
                {
                    s = AudioCraft.MakeLoop(s, ch, fq, spec.kind == SfxKind.Music ? 2.5f : 0.75f);
                    AudioCraft.Normalize(s, 1f);
                    AudioCraft.Drive(s, spec.drive);
                    AudioCraft.Normalize(s, spec.kind == SfxKind.Music ? 0.8f : 0.7f);
                }
                else
                {
                    if (spec.maxSeconds > 0f) s = AudioCraft.TrimWithFade(s, ch, fq, spec.maxSeconds, 0.25f);
                    AudioCraft.Normalize(s, 1f);
                    AudioCraft.Drive(s, spec.drive);
                    AudioCraft.Normalize(s, 0.89f);
                }
                written.Add(WriteSfx(spec.Dir, spec.name, s, ch, fq));
                if (spec.menuCopy) written.Add(WriteSfx(SfxMenuDir, spec.name, s, ch, fq)); // 메뉴 배경 영상 환경음으로도
            }
        }

        AssetDatabase.Refresh();
        foreach (var path in written)
        {
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) continue;
            var fileName = Path.GetFileName(path);
            bool loop = fileName.StartsWith("amb_");
            bool music = fileName.StartsWith("menu_theme");
            var st = imp.defaultSampleSettings;
            st.loadType = music ? AudioClipLoadType.Streaming : loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            st.compressionFormat = AudioCompressionFormat.Vorbis;
            st.quality = 0.7f;
            imp.defaultSampleSettings = st;
            imp.loadInBackground = loop || music;
            imp.SaveAndReimport();
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (c != null && !path.StartsWith(SfxMenuDir + "/amb_pool") && !path.StartsWith(SfxMenuDir + "/amb_city"))
                made[Path.GetFileNameWithoutExtension(path)] = c;
        }
        return made;
    }

    static bool ReadSamples(string src, out float[] s, out int ch, out int fq)
    {
        s = null; ch = fq = 0;
        var clip = LoadDecompressed(src);
        if (clip == null) return false;
        ch = clip.channels; fq = clip.frequency;
        s = new float[clip.samples * ch];
        if (clip.GetData(s, 0)) return true;
        Debug.LogWarning("[소리] 읽기 실패: " + src);
        return false;
    }

    /// <summary>"이름(GEN_n).wav"처럼 앞 이름이 같은 원본들(사용자가 고른 것). wav·mp3·ogg.</summary>
    static List<string> FindSfxSources(string prefix)
    {
        var list = new List<string>();
        if (!Directory.Exists(SfxSourceDir)) return list;
        foreach (var f in Directory.GetFiles(SfxSourceDir))
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext != ".wav" && ext != ".mp3" && ext != ".ogg") continue;
            var n = Path.GetFileNameWithoutExtension(f);
            int cut = n.IndexOf('(');
            if ((cut >= 0 ? n.Substring(0, cut) : n).Trim() == prefix) list.Add(f.Replace('\\', '/'));
        }
        list.Sort(System.StringComparer.Ordinal);
        return list;
    }

    /// <summary>샘플을 읽을 수 있게(압축 풀어 불러오기) 원본 가져오기 설정을 맞추고 불러온다.</summary>
    static AudioClip LoadDecompressed(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp == null) return null;
        var st = imp.defaultSampleSettings;
        if (st.loadType != AudioClipLoadType.DecompressOnLoad || !st.preloadAudioData)
        {
            st.loadType = AudioClipLoadType.DecompressOnLoad;
            st.preloadAudioData = true;
            imp.defaultSampleSettings = st;
            imp.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip != null && clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        return clip;
    }

    static string WriteSfx(string dir, string name, float[] s, int ch, int fq)
    {
        string path = $"{dir}/{name}.wav";
        File.WriteAllBytes(path, AudioCraft.ToWav(s, ch, fq));
        return path;
    }

    // ---------------------------------------------------------------- 2) 연결

    static void WireGameAudio(Dictionary<string, AudioClip> made)
    {
        var go = GameObject.Find("GameAudio") ?? new GameObject("GameAudio");
        if (!go.TryGetComponent<GameAudio>(out var ga)) ga = go.AddComponent<GameAudio>();
        GameAudio.CueClip C(Cue cue, string name, float vol, float spatial, float maxD) =>
            new GameAudio.CueClip { cue = cue, clip = made.TryGetValue(name, out var c) ? c : null, volume = vol, spatial = spatial, maxDistance = maxD };
        ga.cues = new[]
        {
            // 2026-11-27 2차 피드백: 전체적으로 크다, 찾음 소리 크다 → 낮춤
            C(Cue.Found, "found", 0.45f, 0.5f, 30f),
            C(Cue.Hint, "hint", 0.35f, 0.8f, 20f),
            C(Cue.ExitOpen, "exit_open", 0.6f, 0.5f, 30f),
            C(Cue.DollStep, "doll_step", 1f, 0.4f, 25f),   // 사용자: 작게 들려 키움(크기 맞추기 + 최대 크기)
            C(Cue.DollFinal, "doll_final", 1f, 0.4f, 30f),
            C(Cue.Knock, "knock", 1f, 0.35f, 30f),            // 사용자: 노크는 중요 → 크게, 덜 멀어지게
            C(Cue.Fall, "fall", 0.9f, 0f, 10f),
            C(Cue.Flicker, "flicker", 0.3f, 0.9f, 15f),
            // 막 전환(순간이동) 소리는 뺐다: 물 없는 방에서 물거품 소리로 들렸다(2026-11-27 사용자). 이어지는 느낌도 살린다.
            C(Cue.Transition, "transition", 0f, 0f, 10f),
            C(Cue.EndingSwell, "ending_swell", 0.8f, 0f, 10f),
            C(Cue.DoorClose, "door_close", 0.9f, 0.5f, 25f),     // 등 뒤에서 쾅(움찔할 만큼)
            C(Cue.WaterEnter, "water_enter", 0.7f, 0.3f, 15f),
            C(Cue.DoorCreak, "door_creak", 0.75f, 0.6f, 25f),
            C(Cue.TvSwitch, "tv_switch", 0.5f, 0f, 10f),
            C(Cue.VhsHiss, "amb_vhs", 0.35f, 0f, 10f),
            C(Cue.FallWind, "fall_wind", 0.7f, 0f, 10f),
            C(Cue.PeepEerie, "peep_eerie", 0.6f, 0f, 10f),     // 렌즈 너머라 귀 옆에서
            C(Cue.Click, "click", 0.7f, 0.6f, 60f),            // 하늘 위 거대한 커서라 멀리까지
        };
        ga.ambienceFade = 1.5f;
        ga.music = made.TryGetValue("menu_theme", out var theme) ? theme : null; // 메뉴와 같은 주제곡(인트로 처음, 숨겨진 엔딩)
        ga.musicVolume = 0.35f;
        EditorUtility.SetDirty(ga);
    }

    static AudioClip[] StepClips(Dictionary<string, AudioClip> made, string name)
    {
        var list = new List<AudioClip>();
        for (int i = 0; i < 64; i++) if (made.TryGetValue($"{name}_{i:00}", out var c)) list.Add(c);
        return list.ToArray();
    }

    static void WireFootsteps(Dictionary<string, AudioClip> made)
    {
        var pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) { Debug.LogWarning("[소리] 플레이어 없음"); return; }
        if (!pc.TryGetComponent<FootstepAudio>(out var fs)) fs = pc.gameObject.AddComponent<FootstepAudio>();
        fs.tile = StepClips(made, "steps_tile");
        fs.carpet = StepClips(made, "steps_carpet");
        fs.wood = StepClips(made, "steps_wood");
        fs.stone = StepClips(made, "steps_stone");
        fs.wet = StepClips(made, "steps_wet");
        fs.strideLength = 1.45f;     // 걷기 약 초당 2걸음(전 0.9m = 3.3걸음이라 잦고 이상했다)
        fs.runStrideLength = 2.05f;  // 뛰기 약 초당 2.7걸음
        if (!pc.TryGetComponent<WaterAudio>(out _)) pc.gameObject.AddComponent<WaterAudio>(); // 물에 들어가면 첨벙, 물속은 먹먹하게
        fs.volume = 0.4f;
        EditorUtility.SetDirty(fs);
    }

    static void WireAudioZones(Dictionary<string, AudioClip> made)
    {
        var old = GameObject.Find("AudioZones");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("AudioZones").transform;

        AudioClip A(string n) => made.TryGetValue(n, out var c) ? c : null;
        Bounds Gaze(string name, Bounds fallback)
        {
            foreach (var g in Object.FindObjectsByType<GazeTarget>(FindObjectsInactive.Include))
                if (g.name == name && g.roomBounds.size != Vector3.zero) { var b = g.roomBounds; b.Expand(new Vector3(2f, 3f, 2f)); return b; }
            Debug.LogWarning("[소리] 방 범위 못 찾음: " + name);
            return fallback;
        }
        AudioZone Z(string n, Bounds b, string loop, float vol, FloorSurface floor, Transform parent = null)
        {
            var go = new GameObject("Zone_" + n);
            go.transform.SetParent(parent != null ? parent : root, false);
            var z = go.AddComponent<AudioZone>();
            // 부모가 움직여도 따라가게 범위는 이 오브젝트 기준으로 바꿔 넣는다
            z.localBounds = new Bounds(go.transform.InverseTransformPoint(b.center), b.size);
            z.loop = loop != null ? A(loop) : null;
            z.volume = vol;
            z.surface = floor;
            EditorUtility.SetDirty(z);
            return z;
        }

        var none = new Bounds(Vector3.zero, Vector3.zero);
        // 크기는 소리마다 평균 크기(측정 2026-11-27)를 보고 맞췄다: 수영장 물소리·샤워 물방울은 원래 듬성듬성해서 크게 둔다.
        // 2026-11-27 2차 피드백: 환경음이 전체적으로 크다(특히 처음 물소리, 꽃무늬 복도 지직) → 낮춤. 젖은 발소리는 샤워실만.
        var intro = Z("Intro", new Bounds(new Vector3(0f, 10f, -21f), new Vector3(104f, 40f, 100f)), "amb_pool", 0.35f, FloorSurface.Tile);
        intro.introMusic = true; // 새로 시작하면 주제곡을 잠깐 틀고 물소리만 남긴다
        Z("PoolHall", new Bounds(new Vector3(0f, 6f, -97f), new Vector3(16f, 16f, 58f)), "amb_pool", 0.35f, FloorSurface.Tile);
        Z("Crack", Gaze("Oddity_Depth_5m", none), "amb_corridor", 0.3f, FloorSurface.Tile);
        var shower = Z("Shower", Gaze("Oddity_Shower_Up", none), "amb_shower", 0.55f, FloorSurface.Wet);
        var showerGate = GameObject.Find("FindGate_Shower");
        if (showerGate != null) shower.silenceWhenRevealed = showerGate.GetComponent<RevealWhenExplored>(); // 기믹을 풀면 물방울이 멎는다
        Z("Yellow", Gaze("Oddity_Clown", none), "amb_yellow", 0.35f, FloorSurface.Carpet);
        Z("Corridor", new Bounds(new Vector3(112f, 1.5f, -232f), new Vector3(152f, 6f, 6f)), "amb_corridor", 0.18f, FloorSurface.Wood);
        Z("Living", Gaze("Oddity_TV", none), "amb_living", 0.4f, FloorSurface.Carpet);
        Z("Bedroom", Gaze("Oddity_BlanketFigure", none), "amb_blue", 0.7f, FloorSurface.Carpet);
        // 파란 침실 복도(도어뷰어 문까지): 카펫. 들어오면 문 너머에서 한 번 노크(BlueRoomDoor.knockZone).
        var blueCorridor = new Bounds(new Vector3(56.8f, 1.5f, -134.7f), new Vector3(15f, 3f, 2f));
        Z("BlueCorridor", blueCorridor, "amb_blue", 0.7f, FloorSurface.Carpet);
        var door = Object.FindAnyObjectByType<BlueRoomDoor>(FindObjectsInactive.Include);
        if (door != null) { door.knockZone = blueCorridor; EditorUtility.SetDirty(door); }
        var doll = Object.FindAnyObjectByType<DollStepper>();
        if (doll != null) { var b = doll.room; b.Expand(new Vector3(1f, 2f, 1f)); Z("DollRoom", b, null, 0f, FloorSurface.Wood); }
        Z("Computer", Gaze("Oddity_WanderCursor", none), "amb_computer", 0.35f, FloorSurface.Carpet);
        // 가라앉은 도시: 물이 차오르기 전엔 조용하고, 물(FloodRise)이 켜지면 차오르는 물소리
        var city = Z("City", Gaze("Oddity_FloodedWindow", none), A("amb_flood") != null ? "amb_flood" : "amb_city", 0.2f, FloorSurface.Tile); // 사용자: 많이 줄이기
        var flood = Object.FindAnyObjectByType<FloodRise>(FindObjectsInactive.Include);
        if (flood != null) city.onlyWhileActive = flood.gameObject; else Debug.LogWarning("[소리] 물 차오름(FloodRise) 못 찾음");
        Z("Angel", Gaze("WingedEye_02", none), "amb_sky", 0.35f, FloorSurface.Stone);
        Z("SkyStairs", new Bounds(new Vector3(28f, 0f, -159f), new Vector3(14f, 140f, 30f)), "amb_sky", 0.25f, FloorSurface.Stone);
        Z("FinalPool", new Bounds(new Vector3(28f, 20f, -222f), new Vector3(104f, 30f, 100f)), "amb_pool", 0.35f, FloorSurface.Tile); // 사용자: 마지막 수영장 물소리는 인트로와 같게

        // 마지막 마네킹은 찾아도 찾음 소리 없이(사용자)
        foreach (var g in Object.FindObjectsByType<GazeTarget>(FindObjectsInactive.Include))
            if (g.name == "Mannequin_InTubeSpot") { g.foundSound = false; EditorUtility.SetDirty(g); }

        ApplyFeedback1127b();
    }
}
