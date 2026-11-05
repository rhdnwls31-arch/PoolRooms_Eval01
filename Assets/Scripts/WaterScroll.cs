using UnityEngine;

// 물 텍스처를 천천히 흘려서 물이 일렁이는 것처럼 보이게 하는 스크립트
public class WaterScroll : MonoBehaviour
{
    public Vector2 scrollSpeed = new Vector2(0.015f, 0.025f);   // 초당 이동량 (UV)
    Material mat;        // 이 물의 머티리얼
    Vector2 offset;      // 현재 오프셋

    void Start()
    {
        mat = GetComponent<Renderer>().material;   // 이 오브젝트만의 머티리얼 사본
    }

    void Update()
    {
        offset += scrollSpeed * Time.deltaTime;    // 매 프레임 조금씩 이동
        mat.SetTextureOffset("_BaseMap", offset); // 색 무늬와 물결(노멀맵)이 함께 움직임
    }
}
