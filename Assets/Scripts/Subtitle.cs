using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주인공의 혼잣말 자막(2026-11-19). 화면 아래 가운데에 영어 한 줄을 띄웠다가 지운다.
/// 한글 글꼴이 아직 없어서 영어로 쓴다. 목소리는 나중에 넣는다(지금은 소리를 넣지 않는 단계).
/// </summary>
public static class Subtitle
{
    /// <summary>캔버스 아래에 자막 글자를 만든다. 처음엔 투명하다.</summary>
    public static Text Create(Transform canvas, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        rt.anchorMin = new Vector2(0.1f, 0.08f); rt.anchorMax = new Vector2(0.9f, 0.2f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 40; t.fontStyle = FontStyle.Italic;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.color = new Color(0.86f, 0.86f, 0.84f, 0f);
        var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.8f); sh.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    /// <summary>글자를 fade초 동안 나타냈다가 hold초 보여 주고 fade초 동안 지운다.</summary>
    public static IEnumerator Show(Text t, string line, float fade, float hold)
    {
        t.text = line;
        yield return Alpha(t, 0f, 1f, fade);
        yield return new WaitForSeconds(hold);
        yield return Alpha(t, 1f, 0f, fade);
    }

    static IEnumerator Alpha(Text t, float from, float to, float time)
    {
        for (float x = 0f; x < time; x += Time.deltaTime) { Set(t, Mathf.Lerp(from, to, x / time)); yield return null; }
        Set(t, to);
    }

    static void Set(Text t, float a) { var c = t.color; c.a = a; t.color = c; }

    /// <summary>목소리 파일이 있으면 귀 옆에서 한 번 튼다. 지금은 비어 있어 아무 소리도 나지 않는다.</summary>
    public static void PlayVoice(AudioClip voice, Transform ear)
    {
        if (voice != null && ear != null) AudioSource.PlayClipAtPoint(voice, ear.position);
    }
}
