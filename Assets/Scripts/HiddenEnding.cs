using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마지막 수영장의 숨은 엔딩(기획서 §6-6). 처음 수영장과 똑같은데 튜브 자리에 마네킹이 등을 돌리고 서 있다.
/// 수영장 앞까지 와서 마네킹을 응시하면(<see cref="GazeTarget"/>, 거리로 "멀리서"는 막는다):
/// 1) 조작이 멈추고 시선이 마네킹에 붙잡힌다. 마네킹이 천천히 돌아서 플레이어를 노려본다.
/// 2) 시야각이 점점 좁아지고 가장자리가 조여 들어와 마네킹 얼굴로 빨려 들어간다(카메라는 움직이지 않는다).
/// 3) 화면이 하얗게 터진다(showTubeReveal이면 다시 밝아질 때 마네킹 자리에 튜브가 떠 있다. 지금은 끔).
/// 4) 지금까지 지나온 방들이 3초씩 하나씩 나온다. vhsMontage(기본)면 검은 화면 가운데 4:3 옛날 TV에 튼 비디오테이프처럼
///    노이즈·주사선·흔들림과 함께, 방 사이는 지지직 노이즈로 넘어간다. 마지막 방이 검게 사라지면 "It was all a dream...?" 자막으로 끝난다.
/// 2026-11-19 기본 엔딩(수영장 끝 DemoEndTrigger)은 씬에서 꺼서 이 엔딩 하나만 남았다. 스크립트는 고치지 않는다.
/// </summary>
public class HiddenEnding : MonoBehaviour
{
    [Tooltip("마네킹 응시 판정. 출구(gate)는 비워 둔다. maxDistance로 '수영장 앞'을 정한다.")]
    public GazeTarget gaze;
    [Tooltip("튜브 자리에 서 있는 마네킹.")]
    public GameObject mannequin;
    [Tooltip("마네킹이 사라지면 돌아올 튜브. 처음에는 꺼 둔다.")]
    public GameObject tube;
    [Tooltip("숨은 엔딩을 찾으면 꺼 둘 기본 엔딩 트리거.")]
    public Collider normalEndTrigger;
    [Tooltip("마지막에 켤 엔딩 화면.")]
    public GameObject endPanel;
    public ScreenFader fader;
    public PlayerController player;

    [Header("노려보기")]
    [Tooltip("시선이 마네킹 쪽으로 끌려가는 시간(초).")]
    public float lockOnTime = 0.8f;
    [Tooltip("마네킹이 돌아서는 시간(초).")]
    public float turnTime = 1.3f;   // 2026-11-18 플레이 피드백: 2.8초는 느렸다
    [Tooltip("다 돌아선 뒤 마주 보고 있는 시간(초).")]
    public float stareHold = 1.2f;

    [Header("빨려 들어가기")]
    [Tooltip("빨려 들어가는 시간(초). 처음엔 느리고 끝으로 갈수록 빨라진다.")]
    public float pullTime = 3.5f;
    [Tooltip("끝에서의 시야각. 작을수록 얼굴이 화면을 꽉 채운다.")]
    public float pullFov = 4f;
    [Tooltip("하얗게 터지는 시간(초).")]
    public float flashTime = 0.35f;

    [Header("깨어나기")]
    [Tooltip("하얗게 터진 뒤 튜브가 떠 있는 수영장을 보여 줄지. 2026-11-18 사용자 요청으로 끔(바로 지나온 방 돌아보기로 간다).")]
    public bool showTubeReveal = false;
    [Tooltip("하얀 화면에서 튜브가 있는 수영장이 보이기까지(초).")]
    public float revealTime = 2.5f;
    [Tooltip("튜브를 보고 있는 시간(초).")]
    public float tubeHold = 3f;
    [Tooltip("마지막으로 하얗게 밝아지는 시간(초).")]
    public float wakeFadeTime = 1.2f;   // 2026-11-18 플레이 피드백: 튜브 뒤 하얀 디졸브 3초는 길었다

    [Header("지나온 방 돌아보기")]
    [Tooltip("깨어난 뒤 차례로 보여 줄 카메라 자리(지나온 방 순서).")]
    public Transform[] montageShots = new Transform[0];
    [Tooltip("방 하나를 보여 주는 시간(초). 페이드 포함.")]
    public float shotTime = 3f;
    [Tooltip("방 사이 하얗게 넘어가는 시간(초).")]
    public float shotFade = 0.5f;
    [Tooltip("마지막 방에서 검게 사라지는 시간(초).")]
    public float finalFadeTime = 2.5f;
    [Tooltip("끝에 엔딩 문구 화면을 띄울지. 2026-11-18 사용자 요청으로 끔(문구 없이 검은 화면으로 끝난다).")]
    public bool showEndPanel = false;

    [Header("비디오테이프 돌아보기(2026-11-19)")]
    [Tooltip("지나온 방들을 옛날 TV(4:3, 노이즈, 주사선)에 튼 비디오테이프처럼 보여 준다.")]
    public bool vhsMontage = true;
    [Tooltip("TV 화면 높이(화면 높이 대비).")]
    public float tvHeight = 0.82f;
    [Tooltip("평소 노이즈 세기(0~1).")]
    public float noiseBase = 0.3f;
    [Tooltip("방 사이 지지직 넘어가는 시간(초).")]
    public float staticTime = 0.45f;
    [Tooltip("마지막 방이 검게 사라지는 시간(초).")]
    public float tvFinalFade = 2.5f;
    [Tooltip("검게 된 뒤 주인공의 마지막 혼잣말(영어 자막). 2026-11-19 사용자 결정: 꿈이었다고 믿고 싶지만 스스로 의심하는 말투.")]
    public string finalLine = "It was all a dream...?";
    [Tooltip("나중에 넣을 목소리. 비어 있으면 자막만 나온다.")]
    public AudioClip finalVoice;
    [Tooltip("마지막 자막을 보여 주는 시간(초).")]
    public float finalLineHold = 3f;

    /// <summary>지금 단계. 0 = 아직 못 찾음, 1 = 연출 중, 2 = 끝남.</summary>
    public int Stage { get; private set; }

    Quaternion mannequinStartRot;
    Camera cam;
    float camFov;
    Vector3 camLocalPos;
    Quaternion camLocalRot;
    bool camSaved;
    Image vignette;
    Texture2D vignetteTex;
    const int VigSize = 96;

    void Awake()
    {
        if (mannequin != null) mannequinStartRot = mannequin.transform.rotation;
        ResetEnding();
    }

    /// <summary>처음 상태로 되돌린다(개발용 방 옮기기에서도 쓴다).</summary>
    public void ResetEnding()
    {
        StopAllCoroutines();
        Stage = 0;
        if (normalEndTrigger != null) normalEndTrigger.enabled = true;
        if (mannequin != null) { mannequin.SetActive(true); mannequin.transform.rotation = mannequinStartRot; }
        if (tube != null) tube.SetActive(false);
        if (endPanel != null) endPanel.SetActive(false);
        if (cam != null && camFov > 0f) cam.fieldOfView = camFov;
        if (cam != null && camSaved) { cam.transform.localPosition = camLocalPos; cam.transform.localRotation = camLocalRot; }
        SetVignette(0f);
        if (tvCanvas != null) Destroy(tvCanvas);
        if (cam != null && cam.targetTexture == tvRt) cam.targetTexture = null;
        if (tvRt != null) { tvRt.Release(); Destroy(tvRt); tvRt = null; }
    }

    // ---------------------------------------------------------------- 비디오테이프 돌아보기
    GameObject tvCanvas;
    RenderTexture tvRt;
    RawImage tvScreen, tvNoise, tvBand;
    Image tvBlack;
    Text tvCounter;
    Texture2D noiseTex;
    float noiseAmount, tapeTime, bandY;
    const int NoiseW = 320, NoiseH = 240;

    IEnumerator VhsMontage()
    {
        BuildTv();
        noiseAmount = 1f; // 하얗게 터진 화면에서 바로 지지직거리는 TV로 넘어간다
        tvBlack.color = new Color(0f, 0f, 0f, 0f);
        var ticker = StartCoroutine(TvTick());
        for (int i = 0; i < montageShots.Length; i++)
        {
            var shot = montageShots[i];
            if (shot == null) continue;
            cam.transform.SetPositionAndRotation(shot.position, shot.rotation);
            // 지지직 → 화면
            yield return Ramp(v => noiseAmount = v, 1f, noiseBase, staticTime);
            yield return new WaitForSeconds(Mathf.Max(0f, shotTime - staticTime * 2f));
            if (i == montageShots.Length - 1) break;
            // 화면 → 지지직(다음 방)
            yield return Ramp(v => noiseAmount = v, noiseBase, 1f, staticTime);
        }
        // 마지막 방은 검게 천천히 사라진다. 노이즈도 같이 꺼진다.
        // UI는 선형 색공간에서 섞이므로 보이는 밝기가 고르게 줄도록 알파 = 1 - (1-v)^2.2.
        yield return Ramp(v => { tvBlack.color = new Color(0f, 0f, 0f, 1f - Mathf.Pow(1f - v, 2.2f)); noiseAmount = noiseBase * (1f - v); }, 0f, 1f, tvFinalFade);
        StopCoroutine(ticker);
        tvScreen.enabled = false; tvNoise.enabled = false; tvBand.enabled = false; tvCounter.enabled = false;
        yield return new WaitForSeconds(0.8f);
        var text = Subtitle.Create(tvCanvas.transform, "FinalSubtitle");
        Subtitle.PlayVoice(finalVoice, cam.transform);
        yield return Subtitle.Show(text, finalLine, 0.8f, finalLineHold);
    }

    IEnumerator Ramp(System.Action<float> set, float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime) { set(Mathf.Lerp(from, to, t / time)); yield return null; }
        set(to);
    }

    // 매 프레임: 노이즈를 새로 뿌리고, 화면을 살짝 흔들고, 트래킹 띠를 내리고, 테이프 카운터를 올린다.
    IEnumerator TvTick()
    {
        var px = new Color32[NoiseW * NoiseH];
        var screenRt = (RectTransform)tvScreen.transform;
        while (true)
        {
            // UI는 선형 색공간에서 섞여 흰 점이 실제보다 훨씬 밝게 보인다. 보이는 세기(p)를 알파 = p^2.2로 바꿔 넣는다.
            float amount = Mathf.Clamp01(noiseAmount);
            for (int i = 0; i < px.Length; i++)
            {
                float g = Random.value;
                byte gb = (byte)(g * 255f);
                px[i] = new Color32(gb, gb, gb, (byte)(Mathf.Pow(amount * (0.3f + 0.7f * g), 2.2f) * 255f));
            }
            noiseTex.SetPixels32(px); noiseTex.Apply(false);
            float jitter = Random.value < 0.08f ? Random.Range(-6f, 6f) : Random.Range(-1f, 1f);
            screenRt.anchoredPosition = new Vector2(jitter, 0f);
            bandY = Mathf.Repeat(bandY + Time.deltaTime * 0.12f, 1.3f);
            var band = (RectTransform)tvBand.transform;
            band.anchorMin = new Vector2(0f, Mathf.Clamp01(1.1f - bandY)); band.anchorMax = new Vector2(1f, Mathf.Clamp01(1.18f - bandY));
            band.offsetMin = band.offsetMax = Vector2.zero; // TV 화면 밖으로 삐져나오지 않게
            tvBand.uvRect = new Rect(Random.value, Random.value, 1f, 0.1f);
            tapeTime += Time.deltaTime;
            int s = (int)tapeTime;
            tvCounter.text = $"PLAY\n{s / 3600}:{s / 60 % 60:00}:{s % 60:00}";
            yield return null;
        }
    }

    void BuildTv()
    {
        tvCanvas = new GameObject("VhsTvCanvas", typeof(RectTransform));
        var canvas = tvCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // 페이드 화면(100) 위
        var scaler = tvCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;

        var bg = NewUi<Image>("Black", tvCanvas.transform);
        Stretch(bg.rectTransform); bg.color = Color.black;

        // 4:3 TV 화면(가운데). 크기는 화면 높이 기준.
        var frame = new GameObject("Tv", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(tvCanvas.transform, false);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
        float h = 1080f * tvHeight; frame.sizeDelta = new Vector2(h * 4f / 3f, h);

        tvRt = new RenderTexture(640, 480, 24) { name = "VhsTv" };
        cam.targetTexture = tvRt;
        tvScreen = NewUi<RawImage>("Screen", frame); Stretch(tvScreen.rectTransform);
        tvScreen.texture = tvRt; tvScreen.color = new Color(0.93f, 0.95f, 1f);

        // 주사선: 1x4 텍스처를 세로로 240번 반복
        var scan = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        scan.SetPixels32(new[] { new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 90), new Color32(0, 0, 0, 90) }); scan.Apply();
        var lines = NewUi<RawImage>("Scanlines", frame); Stretch(lines.rectTransform);
        lines.texture = scan; lines.uvRect = new Rect(0, 0, 1, 240);

        noiseTex = new Texture2D(NoiseW, NoiseH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        tvNoise = NewUi<RawImage>("Noise", frame); Stretch(tvNoise.rectTransform); tvNoise.texture = noiseTex;
        tvBand = NewUi<RawImage>("TrackingBand", frame); tvBand.texture = noiseTex; tvBand.color = new Color(1f, 1f, 1f, 0.6f);

        tvCounter = NewUi<Text>("Counter", frame);
        var cr = tvCounter.rectTransform; cr.anchorMin = new Vector2(0.05f, 0.75f); cr.anchorMax = new Vector2(0.5f, 0.95f); cr.offsetMin = cr.offsetMax = Vector2.zero;
        tvCounter.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tvCounter.fontSize = 34; tvCounter.alignment = TextAnchor.UpperLeft; tvCounter.color = new Color(0.9f, 0.95f, 0.9f, 0.85f);
        tvCounter.verticalOverflow = VerticalWrapMode.Overflow;

        tvBlack = NewUi<Image>("FadeToBlack", frame); Stretch(tvBlack.rectTransform);
    }

    static T NewUi<T>(string name, Transform parent) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        go.transform.SetParent(parent, false);
        var g = go.GetComponent<T>(); g.raycastTarget = false;
        return g;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    void Update()
    {
        if (Stage == 0 && gaze != null && gaze.Found)
        {
            Stage = 1;
            if (normalEndTrigger != null) normalEndTrigger.enabled = false;
            StartCoroutine(Sequence());
        }
    }

    IEnumerator Sequence()
    {
        cam = Camera.main;
        if (cam == null || mannequin == null) yield break;
        camFov = cam.fieldOfView;
        camLocalPos = cam.transform.localPosition; camLocalRot = cam.transform.localRotation; camSaved = true;
        if (player != null) player.enabled = false;
        EnsureVignette();

        var head = FindDeep(mannequin.transform, "Head");
        Vector3 Face() => head != null ? head.position + Vector3.up * 0.05f : mannequin.transform.position + Vector3.up * 1.6f;

        // 1) 시선이 끌려가고, 마네킹이 돌아선다(두 가지를 겹쳐서).
        Quaternion camFrom = cam.transform.rotation;
        Vector3 toPlayer = cam.transform.position - mannequin.transform.position; toPlayer.y = 0f;
        Quaternion mFrom = mannequin.transform.rotation, mTo = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        float total = Mathf.Max(lockOnTime, turnTime);
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / lockOnTime));
            // 돌아서는 동안 잠깐씩 멈칫한다(사람이 아닌 것 같은 움직임).
            float b = Mathf.Clamp01(t / turnTime);
            b = Mathf.SmoothStep(0f, 1f, b) + Mathf.Sin(b * Mathf.PI * 3f) * 0.03f;
            mannequin.transform.rotation = Quaternion.Slerp(mFrom, mTo, b);
            cam.transform.rotation = Quaternion.Slerp(camFrom, Quaternion.LookRotation(Face() - cam.transform.position), a);
            yield return null;
        }
        mannequin.transform.rotation = mTo;

        // 2) 마주 본다.
        for (float t = 0f; t < stareHold; t += Time.deltaTime) { LookAtFace(Face()); yield return null; }

        // 3) 빨려 들어간다: 시야각이 좁아지고 가장자리가 조여 온다. 끝으로 갈수록 빨라진다.
        for (float t = 0f; t < pullTime; t += Time.deltaTime)
        {
            float k = t / pullTime, e = k * k * k;
            cam.fieldOfView = Mathf.Lerp(camFov, pullFov, e);
            SetVignette(Mathf.Lerp(0f, 0.85f, k * k));
            LookAtFace(Face());
            yield return null;
        }

        // 4) 하얗게 터진다.
        if (fader != null)
        {
            fader.SetColor(Color.white);
            yield return StartCoroutine(fader.FadeOut(flashTime));
        }

        // 5) 하얀 화면 뒤에서 바꾼다: 마네킹은 없고 튜브가 있다. 시선은 튜브 자리를 본다.
        mannequin.SetActive(false);
        if (tube != null) tube.SetActive(true);
        cam.fieldOfView = camFov;
        SetVignette(0f);
        Vector3 spot = tube != null ? tube.transform.position : mannequin.transform.position;
        cam.transform.rotation = Quaternion.LookRotation(spot - cam.transform.position);
        yield return new WaitForSeconds(0.8f);

        if (showTubeReveal)
        {
            if (fader != null) yield return StartCoroutine(fader.FadeIn(revealTime));
            yield return new WaitForSeconds(tubeHold);
            // 6) 깨어난다.
            if (fader != null) yield return StartCoroutine(fader.FadeOut(wakeFadeTime));
        }

        // 7) 지나온 방들. 비디오테이프 모드면 옛날 TV 화면으로, 아니면 하얀 화면 사이로 하나씩.
        if (vhsMontage)
        {
            yield return StartCoroutine(VhsMontage());
            Stage = 2;
            yield break;
        }
        for (int i = 0; i < montageShots.Length; i++)
        {
            var shot = montageShots[i];
            if (shot == null) continue;
            bool last = i == montageShots.Length - 1;
            cam.transform.SetPositionAndRotation(shot.position, shot.rotation);
            if (fader != null) yield return StartCoroutine(fader.FadeIn(shotFade));
            yield return new WaitForSeconds(Mathf.Max(0f, shotTime - shotFade * 2f));
            if (fader == null) continue;
            // 마지막 방은 하얗게가 아니라 검게, 천천히 사라진다(화면이 보이는 동안 색을 바꾸므로 튀지 않는다).
            if (last) fader.SetColor(Color.black);
            yield return StartCoroutine(fader.FadeOut(last ? finalFadeTime : shotFade));
        }
        if (fader != null && montageShots.Length == 0) { fader.SetColor(Color.black); yield return StartCoroutine(fader.FadeOut(finalFadeTime)); }
        // 8) 문구 없이 검은 화면으로 끝난다(showEndPanel이면 예전 엔딩 문구 화면).
        if (showEndPanel && endPanel != null) endPanel.SetActive(true);
        Stage = 2;
    }

    void LookAtFace(Vector3 face) => cam.transform.rotation = Quaternion.LookRotation(face - cam.transform.position);

    static Transform FindDeep(Transform t, string name)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
        return null;
    }

    // 화면 가장자리를 어둡게 조이는 막. 페이드 화면(하얀색) 바로 아래에 둔다.
    void EnsureVignette()
    {
        if (vignette != null || fader == null || fader.fadeImage == null) return;
        var go = new GameObject("StareVignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(fader.fadeImage.transform.parent, false);
        rt.SetSiblingIndex(fader.fadeImage.transform.GetSiblingIndex());
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        vignetteTex = new Texture2D(VigSize, VigSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        vignette = go.GetComponent<Image>();
        vignette.sprite = Sprite.Create(vignetteTex, new Rect(0, 0, VigSize, VigSize), new Vector2(0.5f, 0.5f));
        vignette.raycastTarget = false;
        vignette.color = Color.black;
        SetVignette(0f);
    }

    /// <summary>amount 0 = 막 없음, 1 = 화면 한가운데만 남는다.</summary>
    void SetVignette(float amount)
    {
        if (vignette == null) return;
        vignette.enabled = amount > 0.001f;
        if (!vignette.enabled) return;
        float inner = Mathf.Lerp(1.1f, 0.05f, amount), outer = inner + Mathf.Lerp(0.6f, 0.25f, amount);
        var px = new Color32[VigSize * VigSize];
        for (int y = 0; y < VigSize; y++)
            for (int x = 0; x < VigSize; x++)
            {
                float u = (x + 0.5f) / VigSize * 2f - 1f, v = (y + 0.5f) / VigSize * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                byte a = (byte)(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, r)) * 255f);
                px[y * VigSize + x] = new Color32(255, 255, 255, a);
            }
        vignetteTex.SetPixels32(px);
        vignetteTex.Apply(false);
    }
}
