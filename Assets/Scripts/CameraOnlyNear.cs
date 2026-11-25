using UnityEngine;

/// <summary>
/// 텍스처에 그리는 보조 카메라(거실 TV 화면)를 플레이어가 그 근처에 있을 때만 켠다(2026-11-25 성능 단계).
/// 이 카메라는 HDRP 전체 과정을 한 번 더 돌려, 거실과 상관없는 방에서도 프레임마다 그래픽 카드 약 2ms를 썼다.
/// 화면 텍스처는 꺼져 있는 동안 마지막 그림을 그대로 갖고 있다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraOnlyNear : MonoBehaviour
{
    [Tooltip("메인 카메라가 이 범위(+margin) 안에 있을 때만 그린다(보통 방 범위).")]
    public Bounds area;
    [Tooltip("범위를 이만큼(m) 넓힌다(문 앞에서 미리 켜지게).")]
    public float margin = 4f;

    Camera cam;

    void Awake() => cam = GetComponent<Camera>();

    /// <summary>눈 위치가 범위(+margin) 안이면 참.</summary>
    public static bool Near(Bounds area, float margin, Vector3 eye)
    {
        var b = area; b.Expand(margin * 2f);
        return b.Contains(eye);
    }

    void LateUpdate()
    {
        var main = Camera.main;
        if (main == null || main == cam) return;
        bool on = Near(area, margin, main.transform.position);
        if (cam.enabled != on) cam.enabled = on;
    }
}
