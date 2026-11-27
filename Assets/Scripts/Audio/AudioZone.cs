using System.Collections.Generic;
using UnityEngine;

/// <summary>바닥 종류(발소리를 고른다).</summary>
public enum FloorSurface { Tile, Carpet, Wood, Stone, Wet }

/// <summary>
/// 방 하나의 소리 범위(2026-11-27 사운드 단계). 플레이어가 안에 있으면 이 방의 환경음이 반복해서 나오고, 발소리는 이 방 바닥 소리를 쓴다.
/// 범위는 이 오브젝트 기준(local)이라 방과 함께 움직인다. 여러 범위에 들어 있으면 가장 작은 범위를 쓴다(방 안의 작은 방이 이긴다).
/// 환경음이 비어 있으면 그 방은 조용하다(무음도 연출, 기획서 §8).
/// </summary>
public class AudioZone : MonoBehaviour
{
    [Tooltip("범위(이 오브젝트 기준).")]
    public Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 10f);
    [Tooltip("반복해서 나오는 환경음. 비우면 조용하다.")]
    public AudioClip loop;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("이 방 바닥(발소리).")]
    public FloorSurface surface = FloorSurface.Tile;
    [Tooltip("이 출구가 열리면 환경음이 천천히 사라진다(샤워실: 기믹을 풀면 물방울 소리가 멎는다).")]
    public RevealWhenExplored silenceWhenRevealed;
    [Tooltip("이 오브젝트가 켜져 있을 때만 환경음이 난다(가라앉은 도시: 물이 차오르기 시작해야 물소리).")]
    public GameObject onlyWhileActive;
    [Tooltip("새로 시작해 이 범위에 있으면 주제곡을 잠깐 튼다(처음 수영장).")]
    public bool introMusic;

    static readonly List<AudioZone> all = new List<AudioZone>();

    /// <summary>지금 환경음을 낼 크기(출구가 열려 멎었으면 0).</summary>
    public float TargetVolume => Gate(volume, silenceWhenRevealed != null && silenceWhenRevealed.Revealed, onlyWhileActive == null || onlyWhileActive.activeInHierarchy);

    /// <summary>환경음 크기: 출구가 열려 멎었거나, 켜져야 하는 것이 아직 꺼져 있으면 0.</summary>
    public static float Gate(float volume, bool silenced, bool active) => silenced || !active ? 0f : volume;

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDisable() => all.Remove(this);

    /// <summary>월드 위치가 범위 안인지.</summary>
    public bool Contains(Vector3 world) => localBounds.Contains(transform.InverseTransformPoint(world));

    /// <summary>범위 부피(크기 비교용, 이 오브젝트의 크기 반영).</summary>
    public float Volume3D
    {
        get
        {
            var s = Vector3.Scale(localBounds.size, transform.lossyScale);
            return Mathf.Abs(s.x * s.y * s.z);
        }
    }

    /// <summary>world 위치를 담은 범위 중 가장 작은 것. 없으면 null.</summary>
    public static AudioZone At(Vector3 world)
    {
        AudioZone best = null; float bestVol = float.MaxValue;
        foreach (var z in all)
        {
            if (z == null || !z.Contains(world)) continue;
            float v = z.Volume3D;
            if (v < bestVol) { best = z; bestVol = v; }
        }
        return best;
    }

    /// <summary>
    /// 담고 있는지 목록(contains)과 부피 목록(volumes)에서 고를 칸 번호. 담은 것 중 가장 작은 것, 없으면 -1.
    /// </summary>
    public static int PickSmallest(bool[] contains, float[] volumes)
    {
        int best = -1; float bestVol = float.MaxValue;
        for (int i = 0; i < contains.Length; i++)
            if (contains[i] && volumes[i] < bestVol) { best = i; bestVol = volumes[i]; }
        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(localBounds.center, localBounds.size);
    }
}
