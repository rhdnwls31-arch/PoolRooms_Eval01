#if UNITY_EDITOR
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 개발용(에디터에서만): 그레이박스 맵의 원하는 방에서 바로 시작하거나, Play 중 F1~F9로 방을 옮긴다.
/// 처음부터 다시 걷지 않고 방마다 확인하기 위한 도구(2026-11-18 사용자 요청). 빌드에는 들어가지 않는다.
/// 방을 옮기면 그 방까지 지나왔어야 할 통로는 실제로 지나온 것처럼 막고(SealBehindPassage.Seal),
/// 그 뒤 통로는 처음 상태로 되돌린다(ResetSeal). 앞 방들의 잠긴 출구(RevealWhenExplored)는 열어 두고, 이 방부터는 다시 잠근다.
/// 메뉴는 Assets/Editor/RoomStartMenu.cs("DEPTH 5m/방에서 시작").
/// </summary>
public class RoomStartDebug : MonoBehaviour
{
    /// <summary>Play 전에 고른 방 번호를 넘겨받는 SessionState 키. 0 = 인트로, -1 = 고르지 않음.</summary>
    public const string PendingKey = "DEPTH5m.RoomStart.Pending";
    /// <summary>이 도구가 동작하는 씬 이름.</summary>
    public const string SceneName = "Scene_DescentGreybox";

    /// <summary>방 이름(메뉴와 F키 순서). 0번은 처음 시작 자리, 나머지는 그 방으로 들어가는 통로 막힘 이름과 짝이다.</summary>
    public static readonly string[] RoomNames = { "인트로 메인", "수영장 홀", "금 간 복도", "잠긴 샤워실", "노란 방", "밤의 거실", "천사 방", "하늘 계단", "마지막 수영장" };
    static readonly string[] EntrySeals = { null, "Seal_IntroToHall", "Seal_HallToCrack", "Seal_CrackToShower", "Seal_ShowerToYellow", "Seal_YellowToLiving", "Seal_LivingToAngel", "Seal_AngelToStairs", "Seal_StairsToSame" };

    /// <summary>통로 막힘 판 면에서 방 안쪽으로 이만큼 들어간 곳에 선다(미터). 보이지 않는 벽(0.45m)보다 충분히 멀어야 한다.</summary>
    const float SpawnDepth = 2f;

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
        for (int i = 0; i < RoomNames.Length; i++)
            if (kb[Key.F1 + i].wasPressedThisFrame) { GoTo(i); return; }
    }

    /// <summary>Play 중이면 지금 바로 그 방으로 옮긴다(메뉴에서 부른다).</summary>
    public static void GoToNow(int room)
    {
        if (instance != null) instance.GoTo(room);
    }

    /// <summary>방 번호(0~8)로 옮긴다. 앞 통로는 막고, 그 뒤 통로는 풀고, 방 입구에 방 안쪽을 보고 선다.</summary>
    public void GoTo(int room)
    {
        if (player == null || room < 0 || room >= RoomNames.Length) return;
        var seals = FindObjectsByType<SealBehindPassage>(FindObjectsInactive.Include);

        for (int i = 1; i < EntrySeals.Length; i++)
        {
            var s = FindSeal(seals, EntrySeals[i]);
            if (s == null) continue;
            if (i < room) s.Seal(); else s.ResetSeal();
        }
        // 출구 잠금(§4-1): 앞 방들의 출구는 찾은 상태로 열어 두고, 이 방부터는 처음 상태로 되돌린다.
        foreach (var gate in FindObjectsByType<RevealWhenExplored>(FindObjectsInactive.Include))
        {
            if (gate.order < room) { if (!gate.Revealed) gate.Reveal(); }
            else gate.ResetReveal();
        }
        foreach (var g in FindObjectsByType<GazeTarget>(FindObjectsInactive.Include))
        {
            if (g.gate != null && g.gate.order < room) g.CompleteSilently();
            else g.ResetGaze();
        }

        Vector3 pos = introPos;
        Quaternion rot = introRot;
        if (room >= 1)
        {
            var entry = FindSeal(seals, EntrySeals[room]);
            if (entry == null) { Debug.LogWarning($"[방에서 시작] {EntrySeals[room]}을(를) 찾지 못했습니다."); return; }
            entry.ResetSeal(); // 들어가는 통로는 열어 두고, 들어가면 원래대로 막힌다.
            Vector3 dir = entry.arrivalDirection.normalized;
            var b = entry.plugBounds;
            Vector3 face = b.center + Vector3.Scale(dir, b.extents);
            pos = new Vector3(face.x, b.min.y + 0.05f, face.z) + dir * SpawnDepth;
            rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
        }
        Teleport(pos, rot);
        Debug.Log($"[방에서 시작] {room + 1}. {RoomNames[room]}");
    }

    static SealBehindPassage FindSeal(SealBehindPassage[] seals, string name)
    {
        foreach (var s in seals) if (s.name == name) return s;
        return null;
    }

    void Teleport(Vector3 pos, Quaternion rot)
    {
        var pc = player.GetComponent<PlayerController>();
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(pos, rot);
        if (cc != null) cc.enabled = true;
        // 떨어지던 속도가 남지 않게 지운다(PlayerController는 고치지 않고 개발용으로만 건드린다).
        var vv = typeof(PlayerController).GetField("verticalVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        if (vv != null && pc != null && vv.FieldType == typeof(float)) vv.SetValue(pc, 0f);
    }
}
#endif
