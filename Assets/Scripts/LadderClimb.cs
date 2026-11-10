using UnityEngine;
using UnityEngine.InputSystem;

// TD-002: 사다리 밑에서 E를 누르면 플레이어를 사다리 위 데크 지점으로 옮긴다.
// 계단 콜라이더로 자동 등반(StepOffset)을 시도했으나, CharacterController의 캡슐
// 높이(1.8m)가 계단 간격(0.25m)보다 훨씬 커서 한 칸 올라서면 바로 위 칸과 몸이
// 겹쳐 막히는 문제가 있었다 — 그래서 명시적 입력으로 바꾼다.
public class LadderClimb : MonoBehaviour
{
    public Transform exitPoint; // 사다리를 다 오르면 도착할 데크 위 지점 (빌더가 자동 연결)
    public GameObject promptUI; // "E로 오르기" 안내 문구 — 여러 사다리가 공유 (빌더가 자동 연결)

    CharacterController playerInZone;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInZone = other.GetComponent<CharacterController>();
        if (promptUI != null) promptUI.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInZone = null;
        if (promptUI != null) promptUI.SetActive(false);
    }

    void Update()
    {
        if (playerInZone == null || exitPoint == null) return;
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            // CharacterController가 켜진 채로는 위치를 바로 옮겨도 다음 프레임에 밀려나므로
            // 잠깐 끄고 옮긴 뒤 다시 켠다.
            playerInZone.enabled = false;
            playerInZone.transform.position = exitPoint.position;
            playerInZone.enabled = true;
            playerInZone = null;
            if (promptUI != null) promptUI.SetActive(false);
        }
    }
}
