using System.Reflection;
using UnityEngine;

/// <summary>
/// 본편 방 순서와 "이 방에서 시작" 상태 맞추기. 이어하기(<see cref="RoomCheckpoint"/>, 2026-11-25)와
/// 개발용 방 옮기기(RoomStartDebug, 에디터 전용)가 함께 쓴다.
/// 방을 옮기면 그 방까지 지나왔어야 할 통로는 실제로 지나온 것처럼 막고(SealBehindPassage.Seal),
/// 그 뒤 통로는 처음 상태로 되돌린다(ResetSeal). 앞 방들의 잠긴 출구(RevealWhenExplored)는 열어 두고, 이 방부터는 다시 잠근다.
/// </summary>
public static class RoomRoute
{
    /// <summary>본편 씬 이름.</summary>
    public const string SceneName = "Scene_DescentGreybox";

    /// <summary>방 이름(진행 순서). 0번은 처음 시작 자리, 나머지는 그 방으로 들어가는 통로 막힘(또는 도착 자리) 이름과 짝이다.</summary>
    public static readonly string[] RoomNames = { "인트로 메인", "수영장 홀", "금 간 복도", "잠긴 샤워실", "노란 방", "꽃무늬 복도", "밤의 거실", "파란 침실", "컴퓨터 속", "가라앉은 도시", "천사 방", "하늘 계단", "마지막 수영장" };
    /// <summary>방마다 들어가는 통로 막힘 이름. Spawn_으로 시작하면 순간이동으로 도착하는 방의 도착 자리 표시다.</summary>
    public static readonly string[] EntryMarkers = { null, "Seal_IntroToHall", "Seal_HallToCrack", "Seal_CrackToShower", "Seal_ShowerToYellow", "Spawn_CorridorArrival", "Seal_YellowToLiving", "Seal_LivingToBedroom", "Spawn_ComputerArrival", "Spawn_CityArrival", "Spawn_AngelArrival", "Seal_AngelToStairs", "Seal_StairsToSame" };

    /// <summary>통로 막힘 판 면에서 방 안쪽으로 이만큼 들어간 곳에 선다(미터). 보이지 않는 벽(0.45m)보다 충분히 멀어야 한다.</summary>
    const float SpawnDepth = 2f;

    /// <summary>방 번호가 올바른지.</summary>
    public static bool IsValidRoom(int room) => room >= 0 && room < RoomNames.Length;

    /// <summary>
    /// 방 번호(0~12)의 시작 상태로 맞추고 플레이어를 그 방 입구에 세운다. 0이면 introPos/introRot에 세운다.
    /// 성공하면 참.
    /// </summary>
    public static bool Restore(int room, Transform player, Vector3 introPos, Quaternion introRot)
    {
        if (player == null || !IsValidRoom(room)) return false;
        var seals = Object.FindObjectsByType<SealBehindPassage>(FindObjectsInactive.Include);

        for (int i = 1; i < EntryMarkers.Length; i++)
        {
            var s = FindSeal(seals, EntryMarkers[i]);
            if (s == null) continue;
            if (i < room) s.Seal(); else s.ResetSeal();
        }
        // 출구 잠금(§4-1): 앞 방들의 출구는 찾은 상태로 열어 두고, 이 방부터는 처음 상태로 되돌린다.
        foreach (var gate in Object.FindObjectsByType<RevealWhenExplored>(FindObjectsInactive.Include))
        {
            if (gate.order < room) { if (!gate.Revealed) gate.Reveal(true); }
            else gate.ResetReveal();
        }
        foreach (var g in Object.FindObjectsByType<GazeTarget>(FindObjectsInactive.Include))
        {
            if (g.gate != null && g.gate.order < room) g.CompleteSilently();
            else g.ResetGaze();
        }
        foreach (var he in Object.FindObjectsByType<HiddenEnding>(FindObjectsInactive.Include)) he.ResetEnding();
        foreach (var bd in Object.FindObjectsByType<BlueRoomDoor>(FindObjectsInactive.Include)) bd.ResetDoor();
        foreach (var tz in Object.FindObjectsByType<TeleportZone>(FindObjectsInactive.Include)) tz.ResetZone();
        foreach (var wc in Object.FindObjectsByType<WanderCursor>(FindObjectsInactive.Include)) wc.ResetCursor();
        foreach (var lc in Object.FindObjectsByType<LoopCorridor>(FindObjectsInactive.Include)) lc.ResetLoop();
        // 물 차오름(가라앉은 도시)은 GazeTarget.ResetGaze가 Flood를 끄면서 마른 도시로 되돌린다.

        Vector3 pos = introPos;
        Quaternion rot = introRot;
        if (room >= 1)
        {
            var entry = FindSeal(seals, EntryMarkers[room]);
            var spawn = entry == null ? GameObject.Find(EntryMarkers[room]) : null;
            if (spawn != null)
            {
                // 통로 없이 순간이동으로 들어오는 방은 도착 자리 표시를 쓴다.
                Teleport(player, spawn.transform.position, spawn.transform.rotation);
                return true;
            }
            if (entry == null) { Debug.LogWarning($"[RoomRoute] {EntryMarkers[room]}을(를) 찾지 못했습니다."); return false; }
            entry.ResetSeal(); // 들어가는 통로는 열어 두고, 들어가면 원래대로 막힌다.
            Vector3 dir = entry.arrivalDirection.normalized;
            var b = entry.plugBounds;
            Vector3 face = b.center + Vector3.Scale(dir, b.extents);
            pos = new Vector3(face.x, b.min.y + 0.05f, face.z) + dir * SpawnDepth;
            rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
        }
        Teleport(player, pos, rot);
        return true;
    }

    /// <summary>이름으로 통로 막힘을 찾는다.</summary>
    public static SealBehindPassage FindSeal(SealBehindPassage[] seals, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var s in seals) if (s.name == name) return s;
        return null;
    }

    static void Teleport(Transform player, Vector3 pos, Quaternion rot)
    {
        var pc = player.GetComponent<PlayerController>();
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(pos, rot);
        if (cc != null) cc.enabled = true;
        // 떨어지던 속도가 남지 않게 지운다(PlayerController는 고치지 않는다).
        var vv = typeof(PlayerController).GetField("verticalVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        if (vv != null && pc != null && vv.FieldType == typeof(float)) vv.SetValue(pc, 0f);
    }
}
