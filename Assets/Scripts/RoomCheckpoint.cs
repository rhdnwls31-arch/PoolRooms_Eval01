using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 방에 들어서면 자동 저장하고(2026-11-25 사용자 결정), 이어하기로 열렸으면 저장된 방의 시작 상태로 맞춘다.
/// 본편 씬(<see cref="RoomRoute.SceneName"/>)이 열릴 때 스스로 생긴다.
/// 방에 들어섰다 = 그 방의 들어가는 통로 막힘이 막히기 시작함(Blocked/Sealed), 순간이동으로 들어오는 방은 도착 자리 가까이 옴.
/// </summary>
public class RoomCheckpoint : MonoBehaviour
{
    /// <summary>도착 자리 표시에서 이 거리(m) 안이면 그 방에 들어선 것으로 본다.</summary>
    public const float ArrivalRadius = 3f;
    /// <summary>씬이 열린 뒤 이어하기 방을 맞추기까지 기다리는 프레임(다른 스크립트들의 Start가 끝난 뒤).</summary>
    const int RestoreDelayFrames = 3;

    /// <summary>이번에 이어하기로 맞출 방(-1이면 새로 시작).</summary>
    static int pendingRoom = -1;

    /// <summary>지금까지 들어선 가장 뒤 방.</summary>
    public int Reached { get; private set; }

    Transform player;
    SealBehindPassage[] entrySeals;
    Transform[] arrivals;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        pendingRoom = -1; // 에디터에서 도메인을 다시 불러오지 않아 지난 Play 값이 남지 않게
        SceneManager.sceneLoaded -= OnSceneLoaded; // 같은 이유로 두 번 걸리지 않게
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Ensure(s);

    // 에디터는 Play 들어갈 때 씬을 다시 열지 않게 설정되어 있어(Enter Play Mode Options) 첫 씬의 sceneLoaded가 오지 않는다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootFirstScene() => Ensure(SceneManager.GetActiveScene());

    static void Ensure(Scene s)
    {
        if (s.name != RoomRoute.SceneName || FindAnyObjectByType<RoomCheckpoint>() != null) return;
        new GameObject(nameof(RoomCheckpoint)).AddComponent<RoomCheckpoint>();
    }

    /// <summary>이어하기: 다음에 본편 씬이 열리면 이 방에서 시작한다.</summary>
    public static void ContinueFrom(int room) => pendingRoom = room;

    /// <summary>
    /// 들어선 방 번호를 고른다: 지금까지 가장 뒤 방(reached)보다 뒤에서 들어선 방이 있으면 그중 가장 뒤 방.
    /// entered[i]가 참이면 i번 방에 들어선 것이다.
    /// </summary>
    public static int NextReached(int reached, bool[] entered)
    {
        int best = reached;
        for (int i = reached + 1; i < entered.Length; i++) if (entered[i]) best = i;
        return best;
    }

    IEnumerator Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) { enabled = false; yield break; }
        player = pc.transform;
        var introPos = player.position; var introRot = player.rotation;

        var seals = FindObjectsByType<SealBehindPassage>(FindObjectsInactive.Include);
        entrySeals = new SealBehindPassage[RoomRoute.EntryMarkers.Length];
        arrivals = new Transform[RoomRoute.EntryMarkers.Length];
        for (int i = 1; i < RoomRoute.EntryMarkers.Length; i++)
        {
            entrySeals[i] = RoomRoute.FindSeal(seals, RoomRoute.EntryMarkers[i]);
            if (entrySeals[i] == null) { var go = GameObject.Find(RoomRoute.EntryMarkers[i]); if (go != null) arrivals[i] = go.transform; }
        }

        int room = pendingRoom;
        pendingRoom = -1;
        if (room > 0)
        {
            for (int i = 0; i < RestoreDelayFrames; i++) yield return null;
            RoomRoute.Restore(room, player, introPos, introRot);
            Reached = room;
        }
    }

    bool[] entered;

    void Update()
    {
        if (player == null || entrySeals == null) return;
        if (entered == null) entered = new bool[RoomRoute.EntryMarkers.Length];
        System.Array.Clear(entered, 0, entered.Length);
        for (int i = Reached + 1; i < entered.Length; i++)
        {
            var s = entrySeals[i];
            if (s != null) entered[i] = s.Blocked || s.Sealed;
            else if (arrivals[i] != null) entered[i] = (arrivals[i].position - player.position).sqrMagnitude <= ArrivalRadius * ArrivalRadius;
        }
        int next = NextReached(Reached, entered);
        if (next == Reached) return;
        Reached = next;
        GameSave.SaveRoom(SceneManager.GetActiveScene().name, next);
    }
}
