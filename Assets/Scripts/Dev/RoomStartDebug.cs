#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 개발용(에디터에서만): 그레이박스 맵의 원하는 방에서 바로 시작하거나, Play 중 F1~F12, Shift+F1로 방을 옮긴다.
/// 처음부터 다시 걷지 않고 방마다 확인하기 위한 도구(2026-11-18 사용자 요청). 빌드에는 들어가지 않는다.
/// 방 상태 맞추기는 이어하기와 같은 <see cref="RoomRoute.Restore"/>를 쓴다(2026-11-25 게임 쪽으로 옮김).
/// 메뉴는 Assets/Editor/RoomStartMenu.cs("DEPTH 5m/방에서 시작").
/// </summary>
public class RoomStartDebug : MonoBehaviour
{
    /// <summary>Play 전에 고른 방 번호를 넘겨받는 SessionState 키. 0 = 인트로, -1 = 고르지 않음.</summary>
    public const string PendingKey = "DEPTH5m.RoomStart.Pending";
    /// <summary>이 도구가 동작하는 씬 이름.</summary>
    public const string SceneName = RoomRoute.SceneName;

    /// <summary>방 이름(메뉴와 F키 순서).</summary>
    public static string[] RoomNames => RoomRoute.RoomNames;

    static RoomStartDebug instance;
    Vector3 introPos;
    Quaternion introRot;
    Transform player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (SceneManager.GetActiveScene().name != SceneName || instance != null) return;
        var go = new GameObject("__RoomStartDebug") { hideFlags = HideFlags.DontSave };
        instance = go.AddComponent<RoomStartDebug>();
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) { enabled = false; return; }
        player = pc.transform;
        introPos = player.position;
        introRot = player.rotation;

        int pending = UnityEditor.SessionState.GetInt(PendingKey, -1);
        UnityEditor.SessionState.EraseInt(PendingKey);
        if (pending > 0) GoTo(pending);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        // F1~F12 = 1~12번 방, Shift+F1 = 13번 방부터(방이 12개를 넘었다).
        int add = kb.shiftKey.isPressed ? 12 : 0;
        for (int i = 0; i < 12 && i + add < RoomNames.Length; i++)
            if (kb[Key.F1 + i].wasPressedThisFrame) { GoTo(i + add); return; }
    }

    /// <summary>Play 중이면 지금 바로 그 방으로 옮긴다(메뉴에서 부른다).</summary>
    public static void GoToNow(int room)
    {
        if (instance != null) instance.GoTo(room);
    }

    /// <summary>방 번호(0~12)로 옮긴다.</summary>
    public void GoTo(int room)
    {
        if (RoomRoute.Restore(room, player, introPos, introRot))
            Debug.Log($"[방에서 시작] {room + 1}. {RoomNames[room]}");
    }
}
#endif
