using UnityEngine;

/// <summary>
/// 플레이어가 이 트리거 안에 들어오면 <see cref="RevealWhenExplored"/>에 "여기를 둘러봤다"고 알린다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class VisitZone : MonoBehaviour
{
    [Tooltip("이 곳의 이름. RevealWhenExplored.requiredZones에 같은 이름을 넣는다.")]
    public string zoneId;
    [Tooltip("알릴 대상.")]
    public RevealWhenExplored target;

    void Awake() => GetComponent<BoxCollider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        if (target != null && other.CompareTag("Player")) target.MarkVisited(zoneId);
    }
}
