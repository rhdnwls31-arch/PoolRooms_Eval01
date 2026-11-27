using UnityEngine;

/// <summary>게임 안 한 번 나는 소리 종류.</summary>
public enum Cue
{
    /// <summary>이상한 점을 찾았다.</summary>
    Found,
    /// <summary>오래 못 찾을 때 이상한 점 쪽에서 아주 작게.</summary>
    Hint,
    /// <summary>출구 벽이 걷힌다.</summary>
    ExitOpen,
    /// <summary>목각인형이 한 단계 움직였다.</summary>
    DollStep,
    /// <summary>목각인형들이 한꺼번에 길을 연다.</summary>
    DollFinal,
    /// <summary>파란 침실 문 너머 노크.</summary>
    Knock,
    /// <summary>하늘 계단에서 떨어진다.</summary>
    Fall,
    /// <summary>형광등이 깜빡인다.</summary>
    Flicker,
    /// <summary>다른 곳으로 이어지는 순간(막 전환).</summary>
    Transition,
    /// <summary>엔딩에서 하얗게 밝아진다.</summary>
    EndingSwell,
    /// <summary>지나온 통로가 뒤에서 닫힌다.</summary>
    DoorClose,
    /// <summary>도어뷰어 너머에 인외가 서 있는 동안.</summary>
    PeepEerie,
    /// <summary>컴퓨터 속 맵의 거대한 커서가 누른다.</summary>
    Click,
    /// <summary>플레이어가 물에 들어간다(수영장, 차오르는 물).</summary>
    WaterEnter,
    /// <summary>낡은 나무 문이 천천히 삐걱이며 열린다(파란 침실 문, 인형 화실 출구).</summary>
    DoorCreak,
    /// <summary>엔딩 비디오테이프: TV 채널이 바뀌며 지직.</summary>
    TvSwitch,
    /// <summary>엔딩 비디오테이프가 도는 동안 테이프 소리(반복).</summary>
    VhsHiss,
    /// <summary>하늘 계단에서 떨어지는 동안 귀를 스치는 바람.</summary>
    FallWind,
}

/// <summary>
/// 게임 소리 담당(2026-11-27 사운드 단계). 씬에 하나 둔다(에디터 패스가 만든다).
/// 1) 한 번 나는 소리: <see cref="Play"/>로 부른다. 소리마다 크기·3D 정도를 정해 둔다.
/// 2) 방 환경음: 플레이어가 들어 있는 <see cref="AudioZone"/>의 반복음을 두 개의 소리 칸으로 천천히 바꿔 튼다.
/// 소리 파일은 VARCO Sound에서 만들고 Assets/Audio/Game에 다듬어 둔 것을 쓴다. 비어 있으면 아무 소리도 나지 않는다.
/// </summary>
public class GameAudio : MonoBehaviour
{
    [System.Serializable]
    public class CueClip
    {
        public Cue cue;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("0 = 어디서나 같은 크기(귀 옆), 1 = 소리 난 자리에서 들림.")]
        [Range(0f, 1f)] public float spatial = 0.6f;
        [Tooltip("이 거리(미터)까지 들린다(3D일 때).")]
        public float maxDistance = 25f;
    }

    public CueClip[] cues = new CueClip[0];
    [Tooltip("방이 바뀔 때 환경음이 바뀌는 시간(초).")]
    public float ambienceFade = 1.5f;
    [Tooltip("한 번 나는 소리를 동시에 낼 수 있는 수.")]
    public int voices = 12;

    [Header("음악(정한 순간에만)")]
    [Tooltip("주제곡. 메뉴·인트로 처음·숨겨진 엔딩에서 같은 곡을 쓴다.")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    [Tooltip("새로 시작해 처음 수영장(introMusic 범위)에 있으면 이만큼(초) 틀다가 줄인다.")]
    public float introMusicHold = 25f;
    [Tooltip("인트로 음악이 줄어드는 시간(초). 물소리만 남는다.")]
    public float introMusicFade = 10f;

    /// <summary>씬의 소리 담당. 없으면 null(소리 없이 동작).</summary>
    public static GameAudio Instance { get; private set; }

    AudioSource[] pool;
    Cue[] poolCue;
    float[] fadeOut; // 0이면 그대로, 0보다 크면 이 시간(초)에 걸쳐 줄여 멈춘다
    int next;
    AudioSource ambA, ambB; // ambA = 지금 방, ambB = 사라지는 중
    AudioSource musicSrc;
    float musicTarget, musicFadeSpeed;
    readonly System.Collections.Generic.Dictionary<Cue, AudioSource> loopSrc = new System.Collections.Generic.Dictionary<Cue, AudioSource>();
    readonly System.Collections.Generic.Dictionary<Cue, float> loopFade = new System.Collections.Generic.Dictionary<Cue, float>();
    AudioZone current;
    float ambAVol;

    void Awake()
    {
        Instance = this;
        pool = new AudioSource[Mathf.Max(1, voices)];
        poolCue = new Cue[pool.Length];
        fadeOut = new float[pool.Length];
        for (int i = 0; i < pool.Length; i++) pool[i] = MakeSource("Voice" + i, false);
        ambA = MakeSource("AmbienceA", true);
        ambB = MakeSource("AmbienceB", true);
        musicSrc = MakeSource("Music", true);
    }

    System.Collections.IEnumerator Start()
    {
        // 새로 시작해서 처음 수영장에 서 있으면 주제곡을 잠깐 틀고 물소리만 남게 줄인다(이어하기로 다른 방에서 시작하면 안 튼다).
        yield return null;
        var cam = Camera.main;
        var z = cam != null ? AudioZone.At(cam.transform.position) : null;
        if (music == null || z == null || !z.introMusic) yield break;
        PlayMusic(2f);
        for (float t = 0f; t < introMusicHold; t += Time.unscaledDeltaTime) yield return null;
        StopMusic(introMusicFade);
    }

    /// <summary>주제곡을 fadeIn초에 걸쳐 틀기 시작한다(이미 나고 있으면 크기만 맞춘다).</summary>
    public static void PlayMusic(float fadeIn = 2f)
    {
        var g = Instance;
        if (g == null || g.music == null) return;
        if (!g.musicSrc.isPlaying) { g.musicSrc.clip = g.music; g.musicSrc.volume = 0f; g.musicSrc.time = 0f; g.musicSrc.Play(); }
        g.musicTarget = g.musicVolume;
        g.musicFadeSpeed = g.musicVolume / Mathf.Max(0.01f, fadeIn);
    }

    /// <summary>주제곡을 fade초에 걸쳐 줄여 멈춘다.</summary>
    public static void StopMusic(float fade = 3f)
    {
        var g = Instance;
        if (g == null) return;
        g.musicTarget = 0f;
        g.musicFadeSpeed = Mathf.Max(0.0001f, g.musicSrc.volume) / Mathf.Max(0.01f, fade);
    }

    /// <summary>지금 주제곡이 나고 있는지.</summary>
    public static bool MusicPlaying => Instance != null && Instance.musicSrc.isPlaying;

    /// <summary>이 소리를 반복해서 튼다(이미 나고 있으면 그대로). 소리 파일이 없으면 아무것도 안 한다.</summary>
    public static void StartLoop(Cue cue, Vector3 at)
    {
        var g = Instance;
        var c = g != null ? g.Find(cue) : null;
        if (c == null || c.clip == null) return;
        if (!g.loopSrc.TryGetValue(cue, out var s)) { s = g.MakeSource("Loop_" + cue, true); g.loopSrc[cue] = s; }
        g.loopFade[cue] = 0f;
        s.transform.position = at;
        s.spatialBlend = c.spatial;
        s.maxDistance = Mathf.Max(1f, c.maxDistance);
        s.volume = c.volume;
        if (!s.isPlaying) { s.clip = c.clip; s.Play(); }
    }

    /// <summary>반복 중인 소리를 fade초에 걸쳐 줄여 멈춘다.</summary>
    public static void StopLoop(Cue cue, float fade = 0.5f)
    {
        var g = Instance;
        if (g == null || !g.loopSrc.ContainsKey(cue)) return;
        g.loopFade[cue] = Mathf.Max(0.01f, fade);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    AudioSource MakeSource(string n, bool loop)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.dopplerLevel = 0f;
        return s;
    }

    /// <summary>소리를 낸다. 소리 담당이 없거나 파일이 비어 있으면 아무것도 하지 않는다.</summary>
    public static void Play(Cue cue, Vector3 at, float volumeScale = 1f)
    {
        if (Instance != null) Instance.PlayCue(cue, at, volumeScale);
    }

    /// <summary>
    /// 이 종류로 나고 있는 소리를 fade초에 걸쳐 줄여 멈춘다(목각인형: 다시 앞을 보면 움직이던 소리가 멎는다). 없으면 아무것도 안 한다.
    /// </summary>
    public static void Stop(Cue cue, float fade = 0.2f)
    {
        if (Instance == null) return;
        var p = Instance.pool;
        for (int i = 0; i < p.Length; i++)
            if (p[i].isPlaying && Instance.poolCue[i] == cue && Instance.fadeOut[i] <= 0f) Instance.fadeOut[i] = Mathf.Max(0.01f, fade);
    }

    /// <summary>이 종류 소리가 지금 나고 있는지.</summary>
    public static bool IsPlaying(Cue cue)
    {
        if (Instance == null) return false;
        for (int i = 0; i < Instance.pool.Length; i++) if (Instance.pool[i].isPlaying && Instance.poolCue[i] == cue) return true;
        return false;
    }

    /// <summary>줄이는 중인 소리의 다음 크기. 0이 되면 멈춘다.</summary>
    public static float FadeStep(float volume, float startVolume, float fade, float dt)
        => fade <= 0f ? volume : Mathf.MoveTowards(volume, 0f, startVolume * dt / fade);

    /// <summary>이 소리의 길이(초). 없으면 0.</summary>
    public static float Length(Cue cue)
    {
        var c = Instance != null ? Instance.Find(cue) : null;
        return c != null && c.clip != null ? c.clip.length : 0f;
    }

    CueClip Find(Cue cue)
    {
        foreach (var c in cues) if (c != null && c.cue == cue) return c;
        return null;
    }

    void PlayCue(Cue cue, Vector3 at, float volumeScale)
    {
        var c = Find(cue);
        if (c == null || c.clip == null) return;
        int slot = next;
        var s = pool[slot];
        next = (next + 1) % pool.Length;
        poolCue[slot] = cue;
        fadeOut[slot] = 0f;
        s.transform.position = at;
        s.spatialBlend = c.spatial;
        s.maxDistance = Mathf.Max(1f, c.maxDistance);
        s.minDistance = Mathf.Min(2f, s.maxDistance * 0.5f);
        s.clip = c.clip;
        s.volume = Mathf.Clamp01(c.volume * volumeScale);
        s.time = 0f;
        s.Play();
    }

    void Update()
    {
        for (int i = 0; i < pool.Length; i++)
        {
            if (fadeOut[i] <= 0f) continue;
            var c = Find(poolCue[i]);
            float start = c != null ? c.volume : 1f;
            pool[i].volume = FadeStep(pool[i].volume, start, fadeOut[i], Time.unscaledDeltaTime);
            if (pool[i].volume <= 0f || !pool[i].isPlaying) { pool[i].Stop(); fadeOut[i] = 0f; }
        }
        musicSrc.volume = Mathf.MoveTowards(musicSrc.volume, musicTarget, musicFadeSpeed * Time.unscaledDeltaTime);
        if (musicTarget <= 0f && musicSrc.isPlaying && musicSrc.volume <= 0f) musicSrc.Stop();
        foreach (var kv in loopSrc)
        {
            if (!loopFade.TryGetValue(kv.Key, out float f) || f <= 0f) continue;
            var c = Find(kv.Key);
            kv.Value.volume = FadeStep(kv.Value.volume, c != null ? c.volume : 1f, f, Time.unscaledDeltaTime);
            if (kv.Value.volume <= 0f) { kv.Value.Stop(); fadeDone.Add(kv.Key); }
        }
        foreach (var k in fadeDone) loopFade[k] = 0f;
        fadeDone.Clear();
        var cam = Camera.main;
        if (cam == null) return;
        var zone = AudioZone.At(cam.transform.position);
        var clip = zone != null ? zone.loop : null;
        if (clip != ambA.clip)
        {
            // 지금 소리를 사라지는 칸으로 넘기고, 새 방 소리를 0에서 키운다
            (ambA, ambB) = (ambB, ambA);
            ambA.clip = clip;
            ambA.volume = 0f;
            ambAVol = 0f;
            if (clip != null) { ambA.time = 0f; ambA.Play(); } else ambA.Stop();
        }
        current = zone;
        float target = zone != null ? zone.TargetVolume : 0f;
        float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, ambienceFade);
        ambAVol = Mathf.MoveTowards(ambAVol, target, step);
        ambA.volume = ambAVol;
        if (ambB.isPlaying)
        {
            ambB.volume = Mathf.MoveTowards(ambB.volume, 0f, step);
            if (ambB.volume <= 0f) ambB.Stop();
        }
    }

    readonly System.Collections.Generic.List<Cue> fadeDone = new System.Collections.Generic.List<Cue>();

    /// <summary>지금 플레이어가 들어 있는 소리 범위(없으면 null).</summary>
    public AudioZone CurrentZone => current;
}
