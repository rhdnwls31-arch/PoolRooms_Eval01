using System.Collections;
using UnityEngine;

/// <summary>
/// 컴퓨터 속 맵(기획서 §6-3e)의 이상한 점: 아무도 잡지 않은 거대한 마우스 커서가 하늘을 혼자 돌아다닌다.
/// 몇 번에 한 번은 거대한 모니터 화면으로 가서 한 번 누른다(힌트).
/// 응시해서 찾으면(<see cref="GazeTarget"/>) 그 자리에 멈칫 멈췄다가, 모니터 화면으로 미끄러져 가 두 번 누른다(더블클릭).
/// 출구(화면)는 GazeTarget.notifyDelay 뒤에, 플레이어가 화면을 보고 있지 않을 때 열린다(RevealWhenExplored).
/// 이 오브젝트의 위치 = 커서 끝(화살표 끝). 그림(visual)은 늘 플레이어 쪽을 향한다.
/// </summary>
public class WanderCursor : MonoBehaviour
{
    [Tooltip("돌아다닐 자리들(커서 끝 위치).")]
    public Vector3[] points = new Vector3[0];
    [Tooltip("모니터 화면 위의 누를 자리.")]
    public Vector3 clickPoint;
    [Tooltip("커서 응시 판정.")]
    public GazeTarget gaze;
    [Tooltip("커서 그림(이 오브젝트의 자식). 플레이어 쪽으로 돌고, 누를 때 작아졌다 커진다.")]
    public Transform visual;
    public float moveTime = 2.2f;
    public float pauseTime = 0.9f;
    [Tooltip("이만큼 옮겨 다닐 때마다 한 번은 화면을 누르러 간다.")]
    public int hintEvery = 4;
    [Tooltip("찾은 뒤 그 자리에 멈춰 있는 시간(초).")]
    public float freezeTime = 1.6f;
    [Tooltip("멈춘 뒤 화면까지 미끄러져 가는 시간(초).")]
    public float glideTime = 2.4f;

    Vector3 startPos;
    Camera cam;

    void Awake() { startPos = transform.position; }

    void OnEnable() { StartCoroutine(Run()); }

    /// <summary>처음 자리에서 다시 돌아다니게 한다(개발용 방 옮기기에서 쓴다).</summary>
    public void ResetCursor()
    {
        StopAllCoroutines();
        transform.position = startPos;
        if (visual != null) visual.localScale = Vector3.one;
        if (isActiveAndEnabled) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        int i = 0, moves = 0;
        while (gaze == null || !gaze.Found)
        {
            bool hint = hintEvery > 0 && ++moves % hintEvery == 0;
            Vector3 target = hint ? clickPoint : points.Length > 0 ? points[i++ % points.Length] : transform.position;
            yield return Move(target, moveTime, true);
            if (Found()) break;
            if (hint) yield return Click(1);
            for (float t = 0f; t < pauseTime && !Found(); t += Time.deltaTime) yield return null;
        }
        // 찾았다: 멈칫 → 화면으로 → 더블클릭
        yield return new WaitForSeconds(freezeTime);
        yield return Move(clickPoint, glideTime, false);
        yield return Click(2);
    }

    bool Found() => gaze != null && gaze.Found;

    IEnumerator Move(Vector3 to, float time, bool stopOnFound)
    {
        Vector3 from = transform.position;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            if (stopOnFound && Found()) yield break;
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
            yield return null;
        }
        transform.position = to;
    }

    IEnumerator Click(int count)
    {
        if (visual == null) yield break;
        for (int c = 0; c < count; c++)
        {
            visual.localScale = Vector3.one * 0.82f;
            yield return new WaitForSeconds(0.12f);
            visual.localScale = Vector3.one;
            yield return new WaitForSeconds(0.16f);
        }
    }

    void LateUpdate()
    {
        if (visual == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Vector3 away = transform.position - cam.transform.position; away.y = 0f;
        if (away.sqrMagnitude > 0.01f) visual.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
    }
}
