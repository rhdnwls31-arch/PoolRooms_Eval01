using UnityEngine;

// ScreenFader.Awake()는 항상 완전히 검은 화면으로 시작한다 (Scene_Game에서는
// GameManager.FirstRound()가 FadeIn을 걸어 이를 보정한다). Scene_Intro에는 그런
// 라운드 매니저가 없으므로, 씬이 시작되면 한 번 FadeIn을 걸어 주는 역할만 한다.
public class IntroFadeIn : MonoBehaviour
{
    public ScreenFader fader; // 같은 FadeCanvas에 붙은 ScreenFader (Inspector에서 연결)

    void Start()
    {
        if (fader != null) StartCoroutine(fader.FadeIn());
    }
}
