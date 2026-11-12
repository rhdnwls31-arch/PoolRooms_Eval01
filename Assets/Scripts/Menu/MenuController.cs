using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 메인 메뉴와 일시정지 메뉴의 동작. UI는 <see cref="MenuUIFactory"/>가 코드로 만든다.
/// 메인: 시작하기 / 이어하기 / 설정 / 종료하기.  일시정지(ESC): 계속하기 / 저장하기 / 설정 / 종료하기.
/// 설정은 버튼을 누르면 설정 패널이 열리고(메인은 왼쪽, 일시정지는 오른쪽), ESC로 닫는다.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Tooltip("Main: 검은 배경의 메인 메뉴 / Pause: ESC로 여는 일시정지 메뉴")]
    public MenuMode mode = MenuMode.Pause;
    [Tooltip("메인 메뉴의 '시작하기'가 여는 씬")]
    public string firstSceneName = "Scene_Intro";

    public MenuUIRefs Refs { get; private set; }

    Canvas canvas;
    bool open;
    bool settingsOpen;
    PlayerController player;
    bool playerWasEnabled;
    float toastHideAt;

    /// <summary>메뉴용 캔버스(1920x1080 기준 스케일, 맨 위 정렬)를 만든다. 미리보기 캡처도 쓴다.</summary>
    public static Canvas CreateCanvas(Transform parent)
    {
        var go = new GameObject("MenuCanvas", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 500;
        var s = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        s.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return c;
    }

    void Awake()
    {
        EnsureEventSystem();
        canvas = CreateCanvas(transform);
        Refs = MenuUIFactory.Build((RectTransform)canvas.transform, mode);
        Wire();
    }

    void Start()
    {
        Time.timeScale = 1f;
        GameSettings.Apply();
        if (mode == MenuMode.Main)
        {
            SetVisible(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refs.continueButton.interactable = GameSave.Exists;
        }
        else
        {
            SetVisible(false);
        }
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Time.unscaledTime > toastHideAt && Refs.toast.text.Length > 0) Refs.toast.text = "";
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (mode == MenuMode.Main) { if (settingsOpen) SetSettings(false); return; }

        if (!open)
        {
            var result = GameObject.Find("ResultPanel"); // 결과 화면 중에는 일시정지 금지(기존 규칙)
            if (result != null && result.activeInHierarchy) return;
            Open();
        }
        else if (settingsOpen) SetSettings(false);
        else Close();
    }

    // ---- 연결 --------------------------------------------------------------------
    void Wire()
    {
        Refs.settingsButton.onClick.AddListener(() => SetSettings(!settingsOpen));
        Refs.quitButton.onClick.AddListener(mode == MenuMode.Main ? Quit : (UnityEngine.Events.UnityAction)ToMainMenu);
        if (mode == MenuMode.Main)
        {
            Refs.primaryButton.onClick.AddListener(() => { Time.timeScale = 1f; SceneManager.LoadScene(firstSceneName); });
            Refs.continueButton.onClick.AddListener(GameSave.Continue);
        }
        else
        {
            Refs.primaryButton.onClick.AddListener(Close);
            Refs.saveButton.onClick.AddListener(Save);
        }
    }

    // ---- 일시정지 -------------------------------------------------------------------
    void Open()
    {
        open = true;
        Time.timeScale = 0f; // 이상현상/페이드 등 게임 시간을 모두 멈춘다
        player = FindFirstObjectByType<PlayerController>();
        if (player != null) { playerWasEnabled = player.enabled; player.enabled = false; }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetSettings(false);
        MenuUIFactory.RefreshAll(Refs);
        SetVisible(true);
    }

    void Close()
    {
        open = false;
        Time.timeScale = 1f;
        if (player != null) player.enabled = playerWasEnabled; // 페이드 전환 중이었다면 꺼진 채로 유지
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetVisible(false);
    }

    void Save()
    {
        bool ok = GameSave.Save(player != null ? player.transform : null);
        ShowToast(ok ? "저장되었습니다" : "저장할 수 없습니다");
    }

    void ToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("Scene_MainMenu");
    }

    void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Play만 끈다
#else
        Application.Quit();
#endif
    }

    // ---- 표시 -------------------------------------------------------------------------
    void SetVisible(bool v) { Refs.root.gameObject.SetActive(v); }

    void SetSettings(bool v)
    {
        settingsOpen = v;
        Refs.settingsPanel.SetActive(v);
        if (v) { Refs.ResetPending(); MenuUIFactory.RefreshAll(Refs); }
    }

    void ShowToast(string msg)
    {
        Refs.toast.text = msg;
        toastHideAt = Time.unscaledTime + 1.8f;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions(); // 새 Input System용 기본 UI 액션(클릭/이동)을 연결
    }
}
