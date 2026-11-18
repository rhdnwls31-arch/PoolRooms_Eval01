using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마지막 수영장의 숨은 엔딩(기획서 §6-6). 처음 수영장과 똑같은데 튜브 자리에 마네킹이 등을 돌리고 서 있다.
/// 수영장 앞까지 와서 마네킹을 응시하면(<see cref="GazeTarget"/>, 거리로 "멀리서"는 막는다):
/// 1) 조작이 멈추고 시선이 마네킹에 붙잡힌다. 마네킹이 천천히 돌아서 플레이어를 노려본다.
/// 2) 시야각이 점점 좁아지고 가장자리가 조여 들어와 마네킹 얼굴로 빨려 들어간다(카메라는 움직이지 않는다).
/// 3) 화면이 하얗게 터지고, 다시 밝아지면 마네킹은 없고 튜브가 떠 있다. 그리고 하얗게 깨어나며 엔딩 화면.
/// 기본 엔딩 DemoEndTrigger 스크립트는 고치지 않는다. 찾는 순간 그 콜라이더만 끈다.
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
    public float lockOnTime = 1.2f;
    [Tooltip("마네킹이 돌아서는 시간(초).")]
    public float turnTime = 2.8f;
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
    [Tooltip("하얀 화면에서 튜브가 있는 수영장이 보이기까지(초).")]
    public float revealTime = 2.5f;
    [Tooltip("튜브를 보고 있는 시간(초).")]
    public float tubeHold = 3f;
    [Tooltip("마지막으로 하얗게 밝아지는 시간(초).")]
    public float wakeFadeTime = 3f;

    /// <summary>지금 단계. 0 = 아직 못 찾음, 1 = 연출 중, 2 = 끝남.</summary>
    public int Stage { get; private set; }

    Quaternion mannequinStartRot;
    Camera cam;
    float camFov;
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
        SetVignette(0f);
    }

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

        if (fader != null) yield return StartCoroutine(fader.FadeIn(revealTime));
        yield return new WaitForSeconds(tubeHold);

        // 6) 깨어난다.
        if (fader != null) yield return StartCoroutine(fader.FadeOut(wakeFadeTime));
        if (endPanel != null) endPanel.SetActive(true);
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
