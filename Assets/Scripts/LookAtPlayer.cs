using UnityEngine;

/// <summary>
/// 머리처럼 목(이 오브젝트의 원점)을 축으로 돌아 카메라(플레이어 눈)를 천천히 쳐다본다.
/// 얼굴 방향(faceAxis, 로컬)이 쉬는 자세에서 maxAngle도 넘게는 돌지 않는다.
/// 가라앉은 도시 창의 인외 머리에 쓴다(2026-11-19 사용자 요청: 머리만 나를 쳐다보게).
/// </summary>
public class LookAtPlayer : MonoBehaviour
{
    [Tooltip("이 오브젝트 로컬에서 얼굴이 보는 방향.")]
    public Vector3 faceAxis = Vector3.down;
    [Tooltip("쉬는 자세에서 최대 몇 도까지 돌아볼지.")]
    public float maxAngle = 70f;
    [Tooltip("이만큼 가까워야 쳐다본다(m).")]
    public float range = 18f;
    [Tooltip("돌아보는 빠르기(클수록 빠름).")]
    public float turnSpeed = 1.6f;

    Quaternion restLocal;

    void Awake() { restLocal = transform.localRotation; }

    void LateUpdate()
    {
        var cam = Camera.main;
        var parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        var restWorld = parentRot * restLocal;
        var target = restWorld;
        if (cam != null)
        {
            Vector3 to = cam.transform.position - transform.position;
            if (to.sqrMagnitude < range * range && to.sqrMagnitude > 0.0001f)
            {
                Vector3 face = restWorld * faceAxis.normalized;
                var full = Quaternion.FromToRotation(face, to.normalized);
                float ang = Quaternion.Angle(Quaternion.identity, full);
                var turn = ang > maxAngle ? Quaternion.Slerp(Quaternion.identity, full, maxAngle / ang) : full;
                target = turn * restWorld;
            }
        }
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
    }
}
