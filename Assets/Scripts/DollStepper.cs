using System;
using UnityEngine;

/// <summary>
/// 목각인형 화실(기획서 §6-3b2). 플레이어가 인형을 모두 화면 밖에 두면(뒤를 돌면) 인형들이 한 단계씩 다가오며 자세를 바꾼다.
/// 마지막 단계에서는 양옆 벽으로 비켜서 북쪽 문을 가리키고, 문이 열린다.
/// 보고 있는 동안에는 절대 움직이지 않는다. 플레이어 가까이(keepAway 안)로는 오지 않는다. 자리는 정해 둔 단계 자리로만 옮긴다.
/// 기준: 이 오브젝트의 위치 = 남쪽 입구 가운데, forward = 방 안쪽(북쪽). 단계 자리는 (옆으로 lateral, 앞으로 depth)로 적는다.
/// </summary>
public class DollStepper : MonoBehaviour
{
    [Serializable]
    public class Doll
    {
        [Tooltip("인형 뿌리(콜라이더가 붙어 같이 움직인다).")]
        public Transform root;
        [Tooltip("자세 모델들(0 고개 숙임, 1 고개 듦, 2 팔 반쯤, 3 팔 뻗음, 4 옆 가리킴). 하나만 켠다.")]
        public GameObject[] poses = new GameObject[0];
        [Tooltip("단계별 자리: x = 옆(+ 동쪽), y = 입구에서 앞으로 거리.")]
        public Vector2[] spots = new Vector2[0];
        [Tooltip("단계별 방향(도, 0 = 북쪽). 플레이어를 보는 단계에서는 플레이어 쪽에 이만큼 더한다.")]
        public float[] yaw = new float[0];
        [Tooltip("단계별 좌우 뒤집기(반대쪽 벽에서 같은 가리키기 자세를 쓸 때).")]
        public bool[] mirror = new bool[0];
    }

    public Doll[] dolls = new Doll[0];
    [Tooltip("단계별로 켤 자세 번호.")]
    public int[] stagePose = { 0, 1, 2, 3, 4 };
    [Tooltip("이 단계들에서는 인형이 플레이어를 바라보고 선다(마지막 가리키기 단계는 제외).")]
    public bool faceThePlayer = true;

    [Header("움직이는 조건")]
    [Tooltip("이 방의 범위. 플레이어가 안에 있을 때만 움직인다.")]
    public Bounds room;
    [Tooltip("인형이 모두 화면 밖에 이만큼(초) 있어야 한 단계 움직인다.")]
    public float hiddenTime = 1f;
    [Tooltip("단계 사이 최소 간격(초).")]
    public float minInterval = 2f;
    [Tooltip("다가오는 단계에서 플레이어 앞 이 거리(m) 안으로는 오지 않는다.")]
    public float keepAway = 3.5f;
    [Tooltip("마지막 단계 자리가 플레이어와 이 거리(m) 안이면 마지막 단계를 미룬다.")]
    public float finalKeepAway = 1.5f;

    [Header("막기")]
    [Tooltip("인형 무리 바로 앞(방 폭 전체)의 보이지 않는 벽. 인형과 함께 움직이고 마지막 단계에서 꺼진다. " +
             "없으면 인형 사이·옆으로 지나가 문 앞에서 돌아보기만 반복해 바로 나갈 수 있었다(2026-11-25 사용자 피드백).")]
    public BoxCollider blocker;
    [Tooltip("막는 벽을 인형 맨 앞에서 이만큼(m) 앞에 둔다.")]
    public float blockerAhead = 0.4f;

    [Header("문")]
    [Tooltip("마지막 단계에서 끄는 닫힌 문(콜라이더 포함).")]
    public GameObject doorClosed;
    [Tooltip("마지막 단계에서 켜는 것(문 너머 빛).")]
    public GameObject doorOpen;

    [Header("소리(보류, §8)")]
    [Tooltip("인형이 움직이는 순간 낼 소리. 파일이 생기면 넣는다. 비어 있으면 아무 소리도 내지 않는다.")]
    public AudioSource stepSound;

    /// <summary>지금 단계(0 = 처음, 마지막 = 문을 가리킴).</summary>
    public int Stage { get; private set; }
    public int LastStage => stagePose.Length - 1;

    Transform player;
    Camera cam;
    float hiddenFor, sinceStep;
    bool seenSinceStep;
    readonly Plane[] planes = new Plane[6];

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        ResetDolls();
    }

    /// <summary>처음 상태로(인형은 북쪽 끝, 문은 닫힘).</summary>
    public void ResetDolls()
    {
        Stage = 0; hiddenFor = 0f; sinceStep = 0f; seenSinceStep = false;
        for (int i = 0; i < dolls.Length; i++) Place(dolls[i], 0, Spot(dolls[i], 0), dolls[i].yaw.Length > 0 ? dolls[i].yaw[0] : 180f);
        if (doorClosed != null) doorClosed.SetActive(true);
        if (doorOpen != null) doorOpen.SetActive(false);
        MoveBlocker();
    }

    /// <summary>막는 벽을 지금 인형 맨 앞에 맞춘다(마지막 단계면 끈다).</summary>
    void MoveBlocker()
    {
        if (blocker == null) return;
        bool on = Stage < LastStage;
        blocker.gameObject.SetActive(on);
        if (!on) return;
        float front = float.MaxValue;
        foreach (var d in dolls) if (d.root != null) front = Mathf.Min(front, transform.InverseTransformPoint(d.root.position).z);
        if (front == float.MaxValue) return;
        var t = blocker.transform;
        t.SetPositionAndRotation(transform.TransformPoint(new Vector3(0f, 0f, front - blockerAhead)), transform.rotation);
    }

    void Update()
    {
        if (player == null || Stage >= LastStage) return;
        if (!room.Contains(player.position)) { hiddenFor = 0f; return; }
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        sinceStep += Time.deltaTime;
        if (AnyInView())
        {
            hiddenFor = 0f; seenSinceStep = true;
            // 2026-11-27 피드백: 다시 앞을 보면 움직이던 소리가 멎는다(보는 동안엔 움직이지 않으니까)
            if (stepSound == null || stepSound.clip == null) GameAudio.Stop(Cue.DollStep, 0.15f);
            return;
        }
        hiddenFor += Time.deltaTime;
        // 돌아볼 때마다 한 단계: 지난 단계 뒤에 인형을 한 번은 다시 봐야 다음 단계로 간다(계속 등 돌리고 있으면 한 단계만)
        if (seenSinceStep && hiddenFor >= hiddenTime && sinceStep >= minInterval) TryStep();
    }

    /// <summary>다음 단계로(조건이 맞지 않으면 그대로). 개발·테스트용으로도 부른다.</summary>
    public bool TryStep()
    {
        if (Stage >= LastStage) return false;
        int next = Stage + 1;
        Vector3 p = player != null ? transform.InverseTransformPoint(player.position) : new Vector3(0f, 0f, -100f);
        if (next == LastStage)
        {
            foreach (var d in dolls)
            {
                Vector2 s = Spot(d, next);
                if (new Vector2(s.x - p.x, s.y - p.z).magnitude < finalKeepAway) return false;
            }
        }
        foreach (var d in dolls)
        {
            Vector2 target = Spot(d, next);
            float yaw = Yaw(d, next);
            if (next < LastStage)
            {
                Vector3 cur = transform.InverseTransformPoint(d.root.position);
                target.y = Approach(cur.z, target.y, p.z, keepAway);
                if (faceThePlayer && player != null)
                {
                    Vector3 to = p - new Vector3(target.x, 0f, target.y); to.y = 0f;
                    if (to.sqrMagnitude > 0.01f) yaw += Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg - 180f;
                }
            }
            Place(d, next, target, yaw);
        }
        Stage = next; hiddenFor = 0f; sinceStep = 0f; seenSinceStep = false;
        MoveBlocker();
        if (Stage == LastStage)
        {
            if (doorClosed != null) doorClosed.SetActive(false);
            if (doorOpen != null) doorOpen.SetActive(true);
            if (doorClosed != null) GameAudio.Play(Cue.DoorCreak, doorClosed.transform.position + Vector3.up); // 2026-11-27 사운드: 문이 열린다
        }
        // 소리 자리(§8): 인형이 움직인 순간, 등 뒤 인형 쪽에서
        if (stepSound != null && stepSound.clip != null) stepSound.Play();
        else GameAudio.Play(Stage == LastStage ? Cue.DollFinal : Cue.DollStep, DollCenter()); // 2026-11-27 사운드: 마지막엔 여럿이 한꺼번에
        return true;
    }

    /// <summary>
    /// 다가오는 단계의 새 깊이(입구에서 앞으로 거리). 목표까지 다가오되, 플레이어 앞 keepAway 안으로는 오지 않고, 뒤로 물러나지도 않는다.
    /// </summary>
    public static float Approach(float current, float target, float player, float keepAway)
        => Mathf.Min(current, Mathf.Max(target, player + keepAway));

    /// <summary>인형들 가운데(소리 낼 자리). 인형이 없으면 이 오브젝트 자리.</summary>
    Vector3 DollCenter()
    {
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var d in dolls) if (d != null && d.root != null) { sum += d.root.position; n++; }
        return n > 0 ? sum / n + Vector3.up * 1f : transform.position;
    }

    static Vector2 Spot(Doll d, int stage) => d.spots.Length == 0 ? Vector2.zero : d.spots[Mathf.Min(stage, d.spots.Length - 1)];
    static float Yaw(Doll d, int stage) => d.yaw.Length == 0 ? 180f : d.yaw[Mathf.Min(stage, d.yaw.Length - 1)];

    void Place(Doll d, int stage, Vector2 spot, float yaw)
    {
        if (d.root == null) return;
        d.root.SetPositionAndRotation(transform.TransformPoint(new Vector3(spot.x, 0f, spot.y)), transform.rotation * Quaternion.Euler(0f, yaw, 0f));
        int pose = stagePose[Mathf.Clamp(stage, 0, stagePose.Length - 1)];
        bool flip = d.mirror.Length > 0 && d.mirror[Mathf.Min(stage, d.mirror.Length - 1)];
        for (int i = 0; i < d.poses.Length; i++)
        {
            if (d.poses[i] == null) continue;
            d.poses[i].SetActive(i == pose);
            var s = d.poses[i].transform.localScale;
            d.poses[i].transform.localScale = new Vector3(flip ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);
        }
    }

    bool AnyInView()
    {
        GeometryUtility.CalculateFrustumPlanes(cam, planes);
        foreach (var d in dolls)
        {
            if (d.root == null) continue;
            foreach (var r in d.root.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && GeometryUtility.TestPlanesAABB(planes, r.bounds)) return true;
        }
        return false;
    }
}
