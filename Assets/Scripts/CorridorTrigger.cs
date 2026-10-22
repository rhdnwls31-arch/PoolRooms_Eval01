using UnityEngine;

// 복도 끝에 붙이는 감지 구역 스크립트
public class CorridorTrigger : MonoBehaviour
{
    public bool isFrontEnd; // 체크하면 "앞쪽 끝", 체크 안 하면 "뒤쪽 끝"

    // 어떤 물체가 이 구역에 들어오면 자동으로 호출됨
    void OnTriggerEnter(Collider other)
    {
        // 들어온 게 플레이어가 아니면 무시
        if (!other.CompareTag("Player")) return;

        // GameManager에게 "어느 쪽 끝에 도착했다"고 알림
        GameManager.Instance.OnPlayerReachedEnd(isFrontEnd);
    }
}