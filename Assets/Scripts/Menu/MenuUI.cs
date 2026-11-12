using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>메뉴가 쓰이는 자리: 메인 메뉴(검은 배경) / 일시정지(게임 위에 덮임).</summary>
public enum MenuMode { Main, Pause }

/// <summary>팩토리가 만든 UI 요소 참조. 컨트롤러가 이벤트를 연결하고, 미리보기 캡처가 패널을 켠다.</summary>
public class MenuUIRefs
{
    public RectTransform root;
    public GameObject settingsPanel;
    public Button primaryButton;   // Main: 시작하기 / Pause: 계속하기
    public Button continueButton;  // Main: 이어하기 (Pause에서는 null)
    public Button saveButton;      // Pause: 저장하기 (Main에서는 null)
    public Button settingsButton;
    public Button quitButton;
    public Text toast;
    public List<Action> refreshers = new List<Action>(); // 설정 행의 선택 표시를 다시 그린다
    public List<Button> menuButtons = new List<Button>();

    // 화면 모드/해상도는 '적용하기'를 누를 때까지 보류 값으로 들고 있는다.
    public int pendingScreenMode, pendingResolution, pendingFrameRate, pendingMotionBlur;
    public float pendingVolume, pendingBrightness, pendingSensitivity;
    public void ResetPending()
    {
        pendingScreenMode = GameSettings.ScreenMode; pendingResolution = GameSettings.ResolutionIndex;
        pendingFrameRate = GameSettings.FrameRateIndex; pendingMotionBlur = GameSettings.MotionBlurOn;
        pendingVolume = GameSettings.Volume01; pendingBrightness = GameSettings.Brightness01; pendingSensitivity = GameSettings.Sensitivity01;
    }
    public bool IsDirty()
    {
        return pendingScreenMode != GameSettings.ScreenMode || pendingResolution != GameSettings.ResolutionIndex
            || pendingFrameRate != GameSettings.FrameRateIndex || pendingMotionBlur != GameSettings.MotionBlurOn
            || !Mathf.Approximately(pendingVolume, GameSettings.Volume01) || !Mathf.Approximately(pendingBrightness, GameSettings.Brightness01)
            || !Mathf.Approximately(pendingSensitivity, GameSettings.Sensitivity01);
    }
}

/// <summary>작은 흑백 노이즈 텍스처를 약 12fps로 다시 채운다(일시정지 중에도 동작).</summary>
public class NoiseFlicker : MonoBehaviour
{
    public RawImage target;
    Texture2D tex; Color32[] px; float next;
    const int W = 480, H = 270;

    void OnEnable() { Ensure(); }
    void Ensure()
    {
        if (tex != null) return;
        tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        px = new Color32[W * H];
        Refill();
    }
    public void Prime() { Ensure(); if (target != null) target.texture = tex; }
    void OnDestroy() { if (tex != null) { if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex); } }
    void Update() { if (target != null && target.texture != tex) target.texture = tex; if (Time.unscaledTime >= next) { next = Time.unscaledTime + 1f / 12f; Refill(); } }

    void Refill()
    {
        if (tex == null) return;
        for (int i = 0; i < px.Length; i++) { byte v = (byte)UnityEngine.Random.Range(0, 256); px[i] = new Color32(v, v, v, 255); }
        tex.SetPixels32(px); tex.Apply(false);
    }
}

/// <summary>마우스를 올리면 부드럽게 커지고 벗어나면 돌아오는 효과(일시정지 중에도 동작하도록 unscaled 시간 사용).</summary>
public class HoverScale : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public float hoverScale = 1.12f;
    public float speed = 14f;
    bool hovered;
    Selectable sel;

    void Awake() { sel = GetComponent<Selectable>(); }
    void OnDisable() { hovered = false; transform.localScale = Vector3.one; }
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { hovered = true; }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { hovered = false; }

    void Update()
    {
        float target = hovered && (sel == null || sel.IsInteractable()) ? hoverScale : 1f;
        float cur = transform.localScale.x;
        if (Mathf.Abs(cur - target) < 0.001f) { if (cur != target) transform.localScale = Vector3.one * target; return; }
        transform.localScale = Vector3.one * Mathf.Lerp(cur, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }
}

/// <summary>
/// 메인 메뉴/일시정지 UI를 코드로 만든다. 같은 코드를 플레이 중(MenuController)과
/// 에디터 미리보기(캡처)에서 쓰므로, 프리팹 대신 코드가 단일 출처다.
/// 디자인: Pool Glow 레퍼런스 — 얇은 글꼴의 흰 텍스트 메뉴. 메인은 검은 화면에 오른쪽 위 메뉴와 왼쪽 아래 제목,
/// 일시정지는 게임 화면을 어둡게 덮고 왼쪽 메뉴 + 오른쪽 설정 패널.
/// </summary>
public static class MenuUIFactory
{
    // 기준 해상도 1920x1080 (CanvasScaler가 맞춘다)
    static readonly Color Bright = new Color(1f, 1f, 1f, 0.95f);
    static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);
    static readonly Color Disabled = new Color(1f, 1f, 1f, 0.2f);
    static Font font;

    /// <summary>한글이 나오는 얇은 OS 글꼴(맑은 고딕 Semilight 우선). 설치 글꼴이라 파일을 프로젝트에 넣지 않는다.</summary>
    public static Font GetFont()
    {
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic Semilight", "Malgun Gothic", "NanumGothic", "Arial" }, 32);
        return font;
    }

    // ------------------------------------------------------------------------
    public static MenuUIRefs Build(RectTransform canvasRoot, MenuMode mode)
    {
        var refs = new MenuUIRefs();

        // 배경: 메인은 완전한 검정, 일시정지는 게임 화면 위 반투명 검정
        var bg = NewRect("Background", canvasRoot);
        Stretch(bg);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = mode == MenuMode.Main ? Color.black : new Color(0f, 0f, 0f, 0.72f);
        bgImg.raycastTarget = true; // 일시정지 중 뒤 UI가 클릭되지 않게
        refs.root = bg;

        if (mode == MenuMode.Main) AddNoise(bg);
        if (mode == MenuMode.Main) BuildMainLayout(bg, refs);
        else BuildPauseLayout(bg, refs);

        BuildSettingsPanel(bg, refs, mode);

        // 알림 문구(저장됨 등)
        refs.toast = NewText("Toast", bg, "", 26, Bright, TextAnchor.LowerCenter);
        var trt = refs.toast.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 0f); trt.anchorMax = new Vector2(0.5f, 0f);
        trt.pivot = new Vector2(0.5f, 0f); trt.anchoredPosition = new Vector2(0, 60); trt.sizeDelta = new Vector2(900, 50);
        refs.toast.raycastTarget = false;
        return refs;
    }

    // 메인 화면의 옅은 필름 노이즈(작은 랜덤 텍스처를 늘려 깔고, 몇 프레임마다 다시 뽑는다)
    static void AddNoise(RectTransform parent)
    {
        var rt = NewRect("Noise", parent);
        Stretch(rt);
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        raw.color = new Color(1, 1, 1, 0.035f);
        var nf = rt.gameObject.AddComponent<NoiseFlicker>(); nf.target = raw; nf.Prime();
    }

    // ---- 메인: 오른쪽 위 메뉴, 왼쪽 아래 제목 ---------------------------------
    static void BuildMainLayout(RectTransform parent, MenuUIRefs refs)
    {
        const float top = -110f, pitch = 76f, right = -190f;
        refs.primaryButton = MenuItem(parent, "시작하기", new Vector2(1, 1), new Vector2(right, top), TextAnchor.MiddleRight, 42, refs);
        refs.continueButton = MenuItem(parent, "이어하기", new Vector2(1, 1), new Vector2(right, top - pitch), TextAnchor.MiddleRight, 42, refs);
        refs.settingsButton = MenuItem(parent, "설정", new Vector2(1, 1), new Vector2(right, top - pitch * 2), TextAnchor.MiddleRight, 42, refs);
        refs.quitButton = MenuItem(parent, "종료하기", new Vector2(1, 1), new Vector2(right, top - pitch * 3), TextAnchor.MiddleRight, 42, refs);

        var title = NewText("Title", parent, "DEPTH 5m", 150, Bright, TextAnchor.LowerLeft);
        var rt = title.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(120, 110); rt.sizeDelta = new Vector2(1100, 190);
        title.raycastTarget = false;
    }

    // ---- 일시정지: 왼쪽 메뉴 ----------------------------------------------------
    static void BuildPauseLayout(RectTransform parent, MenuUIRefs refs)
    {
        const float top = -70f, pitch = 64f, left = 90f;
        refs.primaryButton = MenuItem(parent, "계속하기", new Vector2(0, 1), new Vector2(left, top), TextAnchor.MiddleLeft, 34, refs);
        refs.saveButton = MenuItem(parent, "저장하기", new Vector2(0, 1), new Vector2(left, top - pitch), TextAnchor.MiddleLeft, 34, refs);
        refs.settingsButton = MenuItem(parent, "설정", new Vector2(0, 1), new Vector2(left, top - pitch * 2), TextAnchor.MiddleLeft, 34, refs);
        refs.quitButton = MenuItem(parent, "메인으로 나가기", new Vector2(0, 1), new Vector2(left, top - pitch * 3), TextAnchor.MiddleLeft, 34, refs);

        var hint = NewText("PauseTitle", parent, "일시정지", 26, Dim, TextAnchor.LowerLeft);
        var hr = hint.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0, 0); hr.pivot = new Vector2(0, 0);
        hr.anchoredPosition = new Vector2(left, 60); hr.sizeDelta = new Vector2(400, 40);
        hint.raycastTarget = false;
    }

    // ---- 설정 패널 ----------------------------------------------------------------
    static void BuildSettingsPanel(RectTransform parent, MenuUIRefs refs, MenuMode mode)
    {
        var panel = NewRect("SettingsPanel", parent);
        // 메인: 왼쪽 중앙(오른쪽 메뉴와 겹치지 않게) / 일시정지: 왼쪽 메뉴 오른쪽 넓은 영역
        if (mode == MenuMode.Main) { panel.anchorMin = new Vector2(0.05f, 0.27f); panel.anchorMax = new Vector2(0.64f, 0.92f); }
        else { panel.anchorMin = new Vector2(0.22f, 0.10f); panel.anchorMax = new Vector2(0.96f, 0.90f); }
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        refs.settingsPanel = panel.gameObject;

        float y = -40f; const float pitch = 82f;
        float labelW = 440f;

        refs.ResetPending();
        // 화면 모드/해상도는 고르기만 하고 '적용하기'를 눌러야 바뀐다.
        AddOptionRow(panel, refs, "화면 모드", GameSettings.ScreenModeLabels, () => refs.pendingScreenMode, i => refs.pendingScreenMode = i, ref y, pitch, labelW, true);
        AddOptionRow(panel, refs, "해상도", GameSettings.ResolutionLabels(), () => refs.pendingResolution, i => refs.pendingResolution = i, ref y, pitch, labelW, true);
        AddOptionRow(panel, refs, "프레임 제한", GameSettings.FrameRateLabels, () => refs.pendingFrameRate, i => refs.pendingFrameRate = i, ref y, pitch, labelW, true);
        AddOptionRow(panel, refs, "모션 블러", GameSettings.OnOffLabels, () => refs.pendingMotionBlur, i => refs.pendingMotionBlur = i, ref y, pitch, labelW, true);
        AddSliderRow(panel, refs, "음량", () => refs.pendingVolume, v => refs.pendingVolume = v, ref y, pitch, labelW, v => Mathf.RoundToInt(v * 100) + "%");
        AddSliderRow(panel, refs, "밝기", () => refs.pendingBrightness, v => refs.pendingBrightness = v, ref y, pitch, labelW, v => ((v - 0.5f) * 2f).ToString("+0.0;-0.0;0.0"));
        AddSliderRow(panel, refs, "마우스 감도", () => refs.pendingSensitivity, v => refs.pendingSensitivity = v, ref y, pitch, labelW, v => Mathf.Lerp(0.3f, 3f, v).ToString("0.0") + "x");

        // 적용하기(화면 모드/해상도)
        var apply = NewText("Apply", panel, "적용하기", 30, Color.white, TextAnchor.MiddleRight);
        var ar = apply.rectTransform;
        ar.anchorMin = ar.anchorMax = new Vector2(1, 1); ar.pivot = new Vector2(1, 1);
        ar.anchoredPosition = new Vector2(-40f, y - 10f); ar.sizeDelta = new Vector2(300, 50);
        var ab = apply.gameObject.AddComponent<Button>();
        ab.targetGraphic = apply; ab.transition = Selectable.Transition.None;
        ab.onClick.AddListener(() =>
        {
            if (!Application.isPlaying) return;
            GameSettings.ScreenMode = refs.pendingScreenMode;
            GameSettings.ResolutionIndex = refs.pendingResolution;
            GameSettings.FrameRateIndex = refs.pendingFrameRate; GameSettings.MotionBlurOn = refs.pendingMotionBlur;
            GameSettings.Volume01 = refs.pendingVolume; GameSettings.Brightness01 = refs.pendingBrightness; GameSettings.Sensitivity01 = refs.pendingSensitivity;
            GameSettings.Save(); GameSettings.Apply(); RefreshAll(refs);
        });
        apply.gameObject.AddComponent<HoverScale>();
        refs.refreshers.Add(() =>
        {
            bool dirty = refs.IsDirty();
            apply.color = dirty ? Bright : Disabled; ab.interactable = dirty;
        });
        refs.refreshers[refs.refreshers.Count - 1]();

        panel.gameObject.SetActive(false);
    }

    static void AddOptionRow(RectTransform panel, MenuUIRefs refs, string label, string[] options, Func<int> get, Action<int> set,
                             ref float y, float pitch, float labelW, bool deferred = false)
    {
        var row = NewRow(panel, label, y, labelW);
        var holder = NewRect("Options", row);
        holder.anchorMin = new Vector2(0, 0); holder.anchorMax = new Vector2(1, 1);
        holder.offsetMin = new Vector2(labelW, 0); holder.offsetMax = Vector2.zero;
        var h = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleRight;
        h.spacing = 56f; h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = true;

        var texts = new List<Text>();
        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            var t = NewText("Opt" + i, holder, options[i], 28, Dim, TextAnchor.MiddleCenter);
            var fitter = t.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var b = t.gameObject.AddComponent<Button>();
            b.targetGraphic = t;
            b.transition = Selectable.Transition.None;
            t.gameObject.AddComponent<HoverScale>();
            b.onClick.AddListener(() =>
            {
                if (!Application.isPlaying) return;
                set(idx);
                if (!deferred) { GameSettings.Save(); GameSettings.Apply(); }
                RefreshAll(refs);
            });
            texts.Add(t);
        }
        refs.refreshers.Add(() => { int cur = get(); for (int i = 0; i < texts.Count; i++) texts[i].color = i == cur ? Bright : Dim; });
        refs.refreshers[refs.refreshers.Count - 1]();
        y -= pitch;
    }

    static void AddSliderRow(RectTransform panel, MenuUIRefs refs, string label, Func<float> get, Action<float> set,
                             ref float y, float pitch, float labelW, Func<float, string> fmt)
    {
        var row = NewRow(panel, label, y, labelW);

        var valueText = NewText("Value", row, "", 26, Dim, TextAnchor.MiddleRight);
        var vr = valueText.rectTransform;
        vr.anchorMin = new Vector2(1, 0); vr.anchorMax = new Vector2(1, 1); vr.pivot = new Vector2(1, 0.5f);
        vr.sizeDelta = new Vector2(110, 0); vr.anchoredPosition = Vector2.zero;
        valueText.raycastTarget = false;

        var sl = NewRect("Slider", row);
        sl.anchorMin = new Vector2(0, 0.5f); sl.anchorMax = new Vector2(1, 0.5f); sl.pivot = new Vector2(0.5f, 0.5f);
        sl.offsetMin = new Vector2(labelW, -15f); sl.offsetMax = new Vector2(-130f, 15f);

        var line = NewRect("Line", sl);
        line.anchorMin = new Vector2(0, 0.5f); line.anchorMax = new Vector2(1, 0.5f);
        line.offsetMin = new Vector2(0, -1f); line.offsetMax = new Vector2(0, 1f);
        var lineImg = line.gameObject.AddComponent<Image>(); lineImg.color = new Color(1, 1, 1, 0.55f); lineImg.raycastTarget = false;

        var area = NewRect("HandleArea", sl); Stretch(area);
        area.offsetMin = new Vector2(7, 0); area.offsetMax = new Vector2(-7, 0);
        var handle = NewRect("Handle", area);
        handle.sizeDelta = new Vector2(14, 26);
        handle.anchorMin = new Vector2(0, 0.5f); handle.anchorMax = new Vector2(0, 0.5f);
        var hImg = handle.gameObject.AddComponent<Image>(); hImg.color = Bright;

        var slider = sl.gameObject.AddComponent<Slider>();
        slider.handleRect = handle; slider.targetGraphic = hImg; slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(get());
        valueText.text = fmt(get());
        slider.onValueChanged.AddListener(v => { valueText.text = fmt(v); if (!Application.isPlaying) return; set(v); RefreshAll(refs); });

        refs.refreshers.Add(() => { slider.SetValueWithoutNotify(get()); valueText.text = fmt(get()); });
        y -= pitch;
    }

    static RectTransform NewRow(RectTransform panel, string label, float y, float labelW)
    {
        var row = NewRect("Row_" + label, panel);
        row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(0.5f, 1);
        row.anchoredPosition = new Vector2(0, y); row.sizeDelta = new Vector2(-80f, 66f);
        var t = NewText("Label", row, label, 30, Bright, TextAnchor.MiddleLeft);
        var lr = t.rectTransform;
        lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(0, 1); lr.pivot = new Vector2(0, 0.5f);
        lr.sizeDelta = new Vector2(labelW, 0); lr.anchoredPosition = Vector2.zero;
        t.raycastTarget = false;
        return row;
    }

    public static void RefreshAll(MenuUIRefs refs) { foreach (var r in refs.refreshers) r(); }

    // ---- 메뉴 항목(호버 시 밝아지는 텍스트 버튼) ------------------------------------
    static Button MenuItem(RectTransform parent, string label, Vector2 anchor, Vector2 pos, TextAnchor align, int size, MenuUIRefs refs)
    {
        var t = NewText("Menu_" + label, parent, label, size, Dim, align);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(520, size * 1.6f);
        var b = t.gameObject.AddComponent<Button>();
        b.targetGraphic = t;
        b.transition = Selectable.Transition.ColorTint;
        var c = b.colors;
        c.normalColor = new Color(1, 1, 1, 0.78f); c.highlightedColor = Color.white; c.pressedColor = new Color(1, 1, 1, 0.6f);
        c.selectedColor = new Color(1, 1, 1, 0.78f); c.disabledColor = Disabled; c.fadeDuration = 0.08f;
        b.colors = c;
        t.color = Color.white; // ColorTint는 그래픽 색에 곱해진다
        t.gameObject.AddComponent<HoverScale>();
        refs.menuButtons.Add(b);
        return b;
    }

    // ---- 생성 헬퍼 ------------------------------------------------------------------
    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Text NewText(string name, Transform parent, string text, int size, Color color, TextAnchor align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GetFont();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = false;
        return t;
    }
}
