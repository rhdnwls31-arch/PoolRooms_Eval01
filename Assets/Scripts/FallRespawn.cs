using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어가 이 트리거(계단 아래 허공)에 떨어지면 화면을 검게 바꾸고, 정해진 자리에서 검정이 걷히며 다시 시작하게 한다.
/// 하늘 계단에서 떨어지면 천사 방에서 다시 태어난다(2026-11-18 사용자 요청). 다시 지나가야 하는 통로의 막힘은 풀어 준다.
/// 화면 가림막은 이 스크립트가 직접 만든다(다른 페이드 연출과 섞이지 않게).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class FallRespawn : MonoBehaviour
{
    [Tooltip("다시 태어날 자리와 바라볼 방향.")]
    public Transform respawnPoint;
    [Tooltip("다시 지나가야 해서 막힘을 풀어 줄 통로들.")]
    public SealBehindPassage[] resetSeals = new SealBehindPassage[0];
    [Tooltip("떨어지는 동안 검게 바뀌는 시간(초).")]
    public float fadeOutTime = 0.7f;
    [Tooltip("완전히 검은 채로 머무는 시간(초).")]
    public float holdTime = 0.6f;
    [Tooltip("다시 태어난 뒤 검정이 걷히는 시간(초).")]
    public float fadeInTime = 2f;

    /// <summary>지금 검게 바뀌는 중이거나 다시 태어나는 중인지.</summary>
    public bool Busy { get; private set; }

    Image cover;

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var canvasGo = new GameObject("FallRespawn_Cover");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var imgGo = new GameObject("Black");
        imgGo.transform.SetParent(canvasGo.transform, false);
        cover = imgGo.AddComponent<Image>();
        cover.color = new Color(0f, 0f, 0f, 0f);
        cover.raycastTarget = false;
        var rt = cover.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Busy || respawnPoint == null || !other.CompareTag("Player")) return;
        StartCoroutine(Respawn(other.transform));
    }

    /// <summary>검정 가림막의 진하기(0~1). fadeOut 구간에서 t(0~1)에 따라 오르고, fadeIn 구간에서 내린다.</summary>
    public static float CoverAlpha(bool fadingOut, float t) => fadingOut ? Mathf.Clamp01(t) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

    IEnumerator Respawn(Transform player)
    {
        Busy = true;
        for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
        {
            cover.color = new Color(0f, 0f, 0f, CoverAlpha(true, t / fadeOutTime));
            yield return null;
        }
        cover.color = Color.black;

        var pc = player.GetComponent<PlayerController>();
        var cc = player.GetComponent<CharacterController>();
        if (pc != null) pc.enabled = false;
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(respawnPoint.position, Quaternion.Euler(0f, respawnPoint.eulerAngles.y, 0f));
        if (cc != null) cc.enabled = true;
        foreach (var s in resetSeals) if (s != null) s.ResetSeal();

        yield return new WaitForSecondsRealtime(holdTime);
        if (pc != null) pc.enabled = true;

        for (float t = 0f; t < fadeInTime; t += Time.unscaledDeltaTime)
        {
            cover.color = new Color(0f, 0f, 0f, CoverAlpha(false, t / fadeInTime));
            yield return null;
        }
        cover.color = new Color(0f, 0f, 0f, 0f);
        Busy = false;
    }
}
