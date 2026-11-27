using UnityEngine;

/// <summary>
/// 발소리(2026-11-27 사운드 단계). 플레이어에 붙인다. PlayerController는 고치지 않고 플레이어가 프레임마다 실제로 옮겨 간 거리만 본다
/// (순간이동·계단 기믹처럼 한 번에 크게 옮기는 것은 걸음으로 세지 않는다).
/// 2026-11-27 피드백: CharacterController.velocity는 Move를 부르지 않으면(마네킹 엔딩처럼 조작이 멈추면) 마지막 값이 남아
/// 서 있는데도 발소리가 계속 났다 → 위치 변화로 바꿨다.
/// 바닥에 붙어 strideLength만큼 걸을 때마다 한 걸음 소리를 낸다. 바닥 종류는 지금 방의 <see cref="AudioZone"/>이 정한다.
/// 걸음 소리는 VARCO 소리에서 한 걸음씩 잘라 둔 것들이고, 같은 소리가 연달아 나지 않게 고른다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FootstepAudio : MonoBehaviour
{
    public AudioClip[] tile = new AudioClip[0];
    public AudioClip[] carpet = new AudioClip[0];
    public AudioClip[] wood = new AudioClip[0];
    public AudioClip[] stone = new AudioClip[0];
    [Tooltip("젖은 타일(샤워실만).")]
    public AudioClip[] wet = new AudioClip[0];
    [Tooltip("한 걸음 거리(미터). 걷기 속도일 때.")]
    public float strideLength = 1.45f;
    [Tooltip("뛸 때 한 걸음 거리(미터). 2026-11-27: 0.9m 고정이면 걷기 초당 3.3걸음·뛰기 6걸음으로 너무 잦아 이상했다 → 걷기 약 2걸음, 뛰기 약 2.7걸음.")]
    public float runStrideLength = 2.05f;
    [Tooltip("걷기·뛰기 속도(PlayerController와 같게). 그 사이는 걸음 거리를 섞는다.")]
    public float walkSpeed = 3f, runSpeed = 5.5f;
    [Tooltip("뛸 때 발소리를 이만큼 더 크게.")]
    public float runVolumeBoost = 1.2f;
    [Range(0f, 1f)] public float volume = 0.45f;
    [Tooltip("이보다 느리면(미터/초) 걷는 것으로 치지 않는다.")]
    public float minSpeed = 0.5f;
    [Tooltip("이보다 빠르면(미터/초) 순간이동으로 보고 걸음으로 세지 않는다.")]
    public float maxSpeed = 12f;
    [Tooltip("이보다 빨리(미터/초) 떨어지는 것이 fallWindDelay초 이어지면 바람 소리(하늘 계단에서 떨어질 때).")]
    public float fallSpeed = 6f, fallWindDelay = 0.25f;

    CharacterController cc;
    AudioSource src;
    float travelled;
    int lastIndex = -1;
    Vector3 lastPos;
    bool hasLast;
    float fallingFor;
    bool windOn;
    WaterAudio water;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        water = GetComponent<WaterAudio>();
    }

    void Update()
    {
        Vector3 pos = transform.position;
        Vector3 d = pos - lastPos;
        float vy = Time.deltaTime > 0f ? d.y / Time.deltaTime : 0f;
        d.y = 0f;
        bool first = !hasLast;
        lastPos = pos; hasLast = true;
        if (first || cc == null || !cc.enabled || Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        float speed = d.magnitude / Time.deltaTime;
        UpdateFallWind(vy);
        float stride = StrideFor(speed, walkSpeed, runSpeed, strideLength, runStrideLength);
        if (!cc.isGrounded || !IsWalking(speed, minSpeed, maxSpeed)) { travelled = Mathf.Min(travelled, stride * 0.6f); return; }
        travelled += speed * Time.deltaTime;
        if (!StrideDone(ref travelled, stride)) return;
        var zone = AudioZone.At(transform.position + Vector3.up * 1f);
        bool wading = water != null && water.InWater;
        var set = Pick(wading ? FloorSurface.Wet : zone != null ? zone.surface : FloorSurface.Tile);
        if (set == null || set.Length == 0) return;
        int i = NextIndex(set.Length, lastIndex, Random.Range(0, 1000));
        lastIndex = i;
        float run = Mathf.InverseLerp(walkSpeed, runSpeed, speed);
        src.pitch = Random.Range(0.96f, 1.04f) + run * 0.03f;
        if (set[i] != null) src.PlayOneShot(set[i], volume * Random.Range(0.85f, 1f) * Mathf.Lerp(1f, runVolumeBoost, run));
    }

    void UpdateFallWind(float vy)
    {
        bool falling = !cc.isGrounded && vy < -fallSpeed && vy > -60f; // 아주 빠른 건 순간이동
        fallingFor = falling ? fallingFor + Time.deltaTime : 0f;
        if (!windOn && fallingFor >= fallWindDelay) { windOn = true; GameAudio.Play(Cue.FallWind, transform.position); }
        else if (windOn && cc.isGrounded) { windOn = false; GameAudio.Stop(Cue.FallWind, 0.3f); }
    }

    /// <summary>속도(미터/초)에 맞는 한 걸음 거리. 걷기 속도 이하면 걷기 걸음, 뛰기 속도 이상이면 뛰기 걸음, 사이는 섞는다.</summary>
    public static float StrideFor(float speed, float walk, float run, float walkStride, float runStride)
        => Mathf.Lerp(walkStride, runStride, Mathf.InverseLerp(walk, run, speed));

    /// <summary>이 속도(미터/초)를 걷는 것으로 볼지. 너무 느리면 서 있는 것, 너무 빠르면 순간이동.</summary>
    public static bool IsWalking(float speed, float min, float max) => speed >= min && speed <= max;

    AudioClip[] Pick(FloorSurface s)
    {
        // 기본 타일 발소리가 아직 없으면 돌바닥 소리로 대신한다(젖은 소리는 샤워실에만).
        var dry = tile.Length > 0 ? tile : stone;
        switch (s)
        {
            case FloorSurface.Wet: return wet.Length > 0 ? wet : dry;
            case FloorSurface.Carpet: return carpet.Length > 0 ? carpet : dry;
            case FloorSurface.Wood: return wood.Length > 0 ? wood : dry;
            case FloorSurface.Stone: return stone.Length > 0 ? stone : dry;
            default: return dry;
        }
    }

    /// <summary>쌓인 거리가 한 걸음을 넘었으면 한 걸음만큼 빼고 참.</summary>
    public static bool StrideDone(ref float travelled, float stride)
    {
        if (stride <= 0f || travelled < stride) return false;
        travelled -= stride;
        return true;
    }

    /// <summary>count개 중 하나를 고른다. 바로 앞(last)과는 다르게(하나뿐이면 0).</summary>
    public static int NextIndex(int count, int last, int roll)
    {
        if (count <= 1) return 0;
        int i = Mathf.Abs(roll) % (count - 1);
        if (last >= 0 && i >= last) i++;
        return i % count;
    }
}
