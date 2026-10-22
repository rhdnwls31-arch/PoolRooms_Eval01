using UnityEngine;
using UnityEngine.InputSystem;     // ESC, Q 키 입력용
using UnityEngine.SceneManagement;

// ESC로 게임을 멈추고 다시 시작하는 최소한의 일시정지 스크립트
public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;            // PausePanel (Inspector에서 연결)
    public GameObject resultPanel;           // ResultPanel (결과 화면 중에는 일시정지 금지)
    public PlayerController playerController; // 일시정지 중 시점이 돌아가지 않게 막을 대상

    bool paused;                 // 지금 멈춘 상태인가
    bool controllerWasEnabled;   // 멈추기 전에 플레이어 조작이 켜져 있었는가

    void Start()
    {
        // 씬을 다시 불러왔을 때(재시작) 시간이 멈춰 있지 않도록 항상 1로 시작
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
    }

    void Update()
    {
        // 결과 화면이 떠 있으면 일시정지를 쓰지 않음
        if (resultPanel != null && resultPanel.activeInHierarchy) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (paused) Resume(); else Pause();
        }

        // 멈춘 상태에서 Q를 누르면 게임 종료
        if (paused && Keyboard.current.qKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Play만 끔
#else
            Application.Quit();                              // 빌드(exe)에서는 프로그램 종료
#endif
        }
    }

    void Pause()
    {
        paused = true;
        Time.timeScale = 0f; // 게임 시간을 멈춤 (이상현상, 페이드 등 모두 정지)

        // 플레이어 조작 잠시 끄기 (원래 켜져 있었는지 기억해 둠)
        controllerWasEnabled = playerController.enabled;
        playerController.enabled = false;

        pausePanel.SetActive(true);
    }

    void Resume()
    {
        paused = false;
        Time.timeScale = 1f;

        // 멈추기 전 상태로 되돌림 (페이드 전환 중이었다면 꺼진 채로 유지)
        playerController.enabled = controllerWasEnabled;

        pausePanel.SetActive(false);
        // 마우스 커서는 건드리지 않음: 계속 숨겨지고 고정된 상태
    }
}