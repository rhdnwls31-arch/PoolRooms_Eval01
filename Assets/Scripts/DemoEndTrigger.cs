using System.Collections;
using UnityEngine;

// Story 009 — 두 번째 꿈 공간(Pool Hall)을 관찰한 뒤 "이번 MVP 빌드는 여기까지"임을
// 알리는 종료 트리거. CorridorTrigger.cs와 같은 모양(트리거 콜라이더 + OnTriggerEnter +
// 태그 검사)이지만, 이 씬에는 GameManager가 없으므로 라운드/판정 로직 없이 페이드아웃과
// 정적 종료 화면만 보여준다. 기존 SURFACE/DROWNED 승패 판정 화면과는 별도의 패널·문구를
// 써서 혼동되지 않게 한다(AC-3). 두 번째 꿈 자체의 클리어/실패 조건은 구현하지 않는다
// (범위 밖 — 향후 확장, Story 010 이후).
public class DemoEndTrigger : MonoBehaviour
{
    public ScreenFader fader;                  // FadeCanvas의 ScreenFader (Inspector에서 연결)
    public GameObject demoEndPanel;            // 정적 종료 화면 패널 (Inspector에서 연결)
    public PlayerController playerController;  // 종료 시 조작을 멈출 대상 (Inspector에서 연결)

    bool triggered; // 중복 발동 방지 — 트리거 경계에서 들락거려도 한 번만 실행

    // 들어온 게 플레이어가 아니면 무시 (CorridorTrigger.cs와 동일한 패턴)
    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        StartCoroutine(EndSequence());
    }

    // 조작 정지 -> 페이드아웃 -> 정적 종료 화면. 어색하게 끊기지 않도록(AC-2) 암전이
    // 끝난 뒤에만 화면을 보여준다.
    IEnumerator EndSequence()
    {
        if (playerController != null) playerController.enabled = false;

        if (fader != null) yield return StartCoroutine(fader.FadeOut());

        if (demoEndPanel != null) demoEndPanel.SetActive(true);
    }
}
