using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도어뷰어(볼록렌즈)로 보는 화면. 텍스처를 둥근 원판에 그린다.
/// 가운데는 크게, 가장자리는 눌려 보이도록 텍스처 좌표를 휘고(볼록렌즈의 배럴 왜곡), 가장자리로 갈수록 어두워진다.
/// 셰이더 없이 원판 메시의 텍스처 좌표만 바꿔서 기본 UI 재질로 그린다(기획서 §6-3d).
/// </summary>
public class FisheyeDisc : RawImage
{
    [Tooltip("원판을 나누는 고리 수. 많을수록 휘어짐이 매끄럽다.")]
    public int rings = 28;
    [Tooltip("원판을 나누는 조각 수.")]
    public int segments = 72;
    [Tooltip("휘어짐 세기. 1이면 휘지 않고, 클수록 가운데가 부풀고 가장자리가 눌린다.")]
    public float bulge = 1.7f;
    [Tooltip("반지름 비율로 이 지점부터 가장자리까지 어두워진다.")]
    public float rimStart = 0.62f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        Vector2 c = r.center;
        float rad = Mathf.Min(r.width, r.height) * 0.5f;
        Color col = color;

        vh.AddVert(c, col, new Vector2(0.5f, 0.5f));
        for (int i = 1; i <= rings; i++)
        {
            float t = (float)i / rings;
            float uvR = 0.5f * Mathf.Pow(t, bulge);
            float shade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rimStart, 1f, t));
            var vc = new Color(col.r * shade, col.g * shade, col.b * shade, col.a);
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(c + dir * rad * t, vc, new Vector2(0.5f, 0.5f) + dir * uvR);
            }
        }
        int V(int ring, int seg) => 1 + (ring - 1) * segments + (seg % segments);
        for (int s = 0; s < segments; s++) vh.AddTriangle(0, V(1, s + 1), V(1, s));
        for (int i = 2; i <= rings; i++)
            for (int s = 0; s < segments; s++)
            {
                vh.AddTriangle(V(i - 1, s), V(i - 1, s + 1), V(i, s + 1));
                vh.AddTriangle(V(i - 1, s), V(i, s + 1), V(i, s));
            }
    }
}
