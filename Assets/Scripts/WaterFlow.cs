using UnityEngine;

/// <summary>
/// 물줄기처럼 보이게: 줄무늬 텍스처를 한 방향으로 흘리고(_BaseColorMap·_EmissiveColorMap 오프셋),
/// 굵기를 조금씩 출렁인다. 잠긴 샤워실의 거꾸로 솟는 물(배수구 → 천장)에 쓴다.
/// 재질은 실행 중 이 렌더러 전용 사본을 써서 에셋을 바꾸지 않는다.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class WaterFlow : MonoBehaviour
{
    [Tooltip("초당 텍스처가 흐르는 양(타일 단위). 양수 = 위로 흐른다.")]
    public float speed = 1.6f;
    [Tooltip("굵기 출렁임 비율과 주기(초).")]
    public float wobble = 0.12f, wobblePeriod = 0.35f;
    [Tooltip("출렁임 위상(줄기마다 다르게).")]
    public float phase;

    Material mat;
    Vector3 baseScale;

    void Awake()
    {
        mat = GetComponent<Renderer>().material; // 실행 중 사본
        baseScale = transform.localScale;
    }

    void Update()
    {
        var o = new Vector2(0f, -Time.time * speed); // 무늬를 위로 민다
        mat.SetTextureOffset("_BaseColorMap", o);
        mat.SetTextureOffset("_EmissiveColorMap", o);
        float k = 1f + wobble * Mathf.Sin((Time.time + phase) * 2f * Mathf.PI / wobblePeriod);
        transform.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z * (2f - k));
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
