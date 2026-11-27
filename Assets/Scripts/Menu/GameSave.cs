using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 저장/이어하기. 2026-11-25 사용자 결정: 방에 들어서면 자동 저장(<see cref="RoomCheckpoint"/>)하고,
/// 이어하기는 그 방 입구에서 앞 방들을 모두 푼 상태로 시작한다(<see cref="RoomRoute.Restore"/>).
/// 예전에는 ESC 메뉴 "저장하기"로 위치만 저장해서, 이어하면 앞 방 출구·통로가 처음 상태로 돌아갔다.
/// </summary>
public static class GameSave
{
    [Serializable]
    class Data { public string scene; public int room = -1; public string savedAt; }

    const string Key = "save.slot0";

    /// <summary>저장된 게임이 있는가.</summary>
    public static bool Exists => PlayerPrefs.HasKey(Key);

    /// <summary>저장된 방 번호(없으면 -1).</summary>
    public static int SavedRoom()
    {
        var d = Read();
        return d != null ? d.room : -1;
    }

    /// <summary>저장 시각 문자열(없으면 빈 문자열).</summary>
    public static string SavedAt()
    {
        var d = Read();
        return d != null ? d.savedAt : "";
    }

    /// <summary>방에 들어섰을 때 자동 저장한다.</summary>
    public static void SaveRoom(string scene, int room)
    {
        var d = new Data { scene = scene, room = room, savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
    }

    /// <summary>저장을 지운다(새로 시작할 때).</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    /// <summary>저장된 씬을 열고, 저장된 방의 시작 상태로 맞춘다.</summary>
    public static void Continue()
    {
        var d = Read();
        if (d == null || string.IsNullOrEmpty(d.scene)) return;
        RoomCheckpoint.ContinueFrom(d.room);
        Time.timeScale = 1f;
        SceneManager.LoadScene(d.scene);
    }

    static Data Read()
    {
        if (!Exists) return null;
        try { return JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key)); }
        catch (ArgumentException) { return null; }
    }
}
