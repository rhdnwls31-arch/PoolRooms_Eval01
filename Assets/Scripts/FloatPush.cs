using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 물에 뜬 튜브·공을 플레이어가 몸으로 밀면 물 위로 천천히 밀려나게 한다(2026-11-28 QA).
/// 콜라이더(에디터 패스가 붙임)로 통과하지 못하게 막고, 플레이어가 부딪히면(<see cref="FloatPusher"/>가 알려 줌)
/// 미는 방향으로 속도를 받아 물의 끌림으로 서서히 멈춘다. 벽·다른 튜브에 닿으면 그 자리에 선다.
/// 위치를 "더하기"로만 옮기므로 <see cref="WaterBob"/>의 동동 뜨는 움직임은 그대로 이어진다.
/// </summary>
[DisallowMultipleComponent]
public class FloatPush : MonoBehaviour
{
    [Tooltip("플레이어가 미는 속도 중 물체에 전해지는 비율(0~1).")]
    public float pushFactor = 0.85f;
    [Tooltip("물의 끌림(1/초). 클수록 빨리 멈춘다.")]
    public float waterDrag = 1.4f;
    [Tooltip("이보다 느리면 멈춘 것으로 본다(m/s).")]
    public float stopSpeed = 0.02f;
    [Tooltip("최고 속도(m/s).")]
    public float maxSpeed = 3f;

    /// <summary>지금 밀려 움직이는 속도(수평).</summary>
    public Vector3 Velocity { get; private set; }

    Collider[] ownColliders;
    float radius;
    static readonly RaycastHit[] hits = new RaycastHit[16];
    static readonly HashSet<CharacterController> pushers = new HashSet<CharacterController>();

    void Awake()
    {
        ownColliders = GetComponentsInChildren<Collider>(true);
        radius = 0.3f;
        foreach (var c in ownColliders)
        {
            var e = c.bounds.extents;
            radius = Mathf.Max(radius, Mathf.Min(Mathf.Max(e.x, e.z), 1.5f));
        }
    }

    void Start()
    {
        // 플레이어 몸에 부딪힘 알림을 붙인다(PlayerController는 고치지 않는다).
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return;
        var cc = pc.GetComponent<CharacterController>();
        if (cc != null && !pushers.Contains(cc) && cc.GetComponent<FloatPusher>() == null) { cc.gameObject.AddComponent<FloatPusher>(); pushers.Add(cc); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => pushers.Clear(); // 도메인을 다시 불러오지 않아 지난 Play 값이 남지 않게

    /// <summary>
    /// 밀린 뒤의 새 속도: 미는 방향(수평)으로는 지금 속도와 미는 속도 중 큰 쪽을 쓰고, 옆 방향 속도는 그대로 둔다.
    /// </summary>
    public static Vector3 PushedVelocity(Vector3 current, Vector3 pushDir, float pushSpeed, float factor, float maxSpeed)
    {
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 1e-6f || pushSpeed <= 0f) return current;
        pushDir.Normalize();
        float along = Vector3.Dot(current, pushDir);
        float target = Mathf.Max(along, pushSpeed * factor);
        var v = current + pushDir * (target - along);
        v.y = 0f;
        return Vector3.ClampMagnitude(v, maxSpeed);
    }

    /// <summary>물의 끌림으로 줄어든 속도. 아주 느려지면 0.</summary>
    public static Vector3 Damped(Vector3 v, float drag, float dt, float stopSpeed)
    {
        v *= Mathf.Exp(-Mathf.Max(0f, drag) * dt);
        return v.sqrMagnitude < stopSpeed * stopSpeed ? Vector3.zero : v;
    }

    /// <summary>플레이어가 dir 방향으로 speed(m/s)로 부딪혔다.</summary>
    public void Push(Vector3 dir, float speed)
    {
        Velocity = PushedVelocity(Velocity, dir, speed, pushFactor, maxSpeed);
    }

    void Update()
    {
        if (Velocity == Vector3.zero) return;
        float dt = Time.deltaTime;
        var step = Velocity * dt;
        if (Blocked(step)) Velocity = Vector3.zero;
        else transform.position += step;
        Velocity = Damped(Velocity, waterDrag, dt, stopSpeed);
    }

    /// <summary>이번에 옮길 만큼 앞에 벽·다른 물체가 있는지. 바닥(위를 보는 면)과 시작부터 겹친 것, 플레이어는 무시한다.</summary>
    bool Blocked(Vector3 step)
    {
        float dist = step.magnitude;
        if (dist < 1e-5f) return false;
        // 물에 뜬 높이(콜라이더 아래쪽)에서 앞을 본다. 높이 보면 수영장 가장자리 턱을 넘어 데크로 나가 버린다.
        float r = radius * 0.8f;
        var origin = transform.position;
        if (ownColliders.Length > 0 && ownColliders[0] != null)
        {
            var b = ownColliders[0].bounds;
            origin = new Vector3(b.center.x, b.min.y + r + 0.05f, b.center.z);
        }
        int n = Physics.SphereCastNonAlloc(origin, r, step / dist, hits, dist + 0.05f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.distance <= 0f || Mathf.Abs(h.normal.y) > 0.6f) continue;
            if (System.Array.IndexOf(ownColliders, h.collider) >= 0) continue;
            if (h.collider is CharacterController) continue;
            return true;
        }
        return false;
    }
}

/// <summary>플레이어 몸(CharacterController)에 붙어, 부딪힌 튜브·공(<see cref="FloatPush"/>)에 미는 방향과 속도를 알린다.</summary>
public class FloatPusher : MonoBehaviour
{
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var fp = hit.collider.GetComponentInParent<FloatPush>();
        if (fp == null || Time.deltaTime <= 0f) return;
        // 부딪힌 면 안쪽(플레이어 → 물체)으로 민다. 스치기만 하면(움직임이 면과 나란하면) 거의 밀리지 않는다.
        var into = -hit.normal; into.y = 0f;
        var move = hit.moveDirection; move.y = 0f;
        if (into.sqrMagnitude < 1e-6f) return;
        into.Normalize();
        float speed = hit.moveLength / Time.deltaTime * Mathf.Max(0f, Vector3.Dot(move, into));
        fp.Push(into, speed);
    }
}
