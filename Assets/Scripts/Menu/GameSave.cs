using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 가장 단순한 저장/이어하기: 지금 씬 이름과 플레이어 위치·방향을 PlayerPrefs에 한 칸 저장한다.
/// 스테이지 진행도(주운 아이템 등)는 아직 시스템이 없어서 포함하지 않는다 — 생기면 Data에 필드를 추가.
/// </summary>
public static class GameSave
{
    [Serializable]
    class Data { public string scene; public float x, y, z, yaw; public string savedAt; }

    const string Key = "save.slot0";
    static bool pendingLoad;
    static Data pending;

    /// <summary>저장된 게임이 있는가.</summary>
    public static bool Exists => PlayerPrefs.HasKey(Key);

    /// <summary>저장 시각 문자열(없으면 빈 문자열).</summary>
    public static string SavedAt()
    {
        if (!Exists) return "";
        var d = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key));
        return d != null ? d.savedAt : "";
    }

    /// <summary>플레이어의 현재 위치로 저장한다. 플레이어가 없으면 false.</summary>
    public static bool Save(Transform player)
    {
        if (player == null) return false;
        var d = new Data
        {
            scene = SceneManager.GetActiveScene().name,
            x = player.position.x, y = player.position.y, z = player.position.z,
            yaw = player.eulerAngles.y,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        };
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>저장된 씬을 불러오고, 씬이 열리면 플레이어를 저장 위치로 옮긴다.</summary>
    public static void Continue()
    {
        if (!Exists) return;
        pending = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key));
        if (pending == null || string.IsNullOrEmpty(pending.scene)) return;
        pendingLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(pending.scene);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded += OnSceneLoaded; }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pendingLoad || pending == null) return;
        pendingLoad = false;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false; // 켜진 채로는 위치를 옮겨도 밀려난다
        player.transform.position = new Vector3(pending.x, pending.y, pending.z);
        player.transform.rotation = Quaternion.Euler(0f, pending.yaw, 0f);
        if (cc != null) cc.enabled = true;
    }
}
