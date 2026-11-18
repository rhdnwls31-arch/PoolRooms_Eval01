using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-18 파란 침실 기믹 확장(기획서 §6-3d, 사용자 아이디어): 방 하나에서 이상한 일이 연달아 일어나며 다음 방으로 끌려간다.
// 1) 이불 속 사람을 응시하면 이불이 꺼지고, 그 순간 동쪽 벽(창문 옆)에 서 있는 사람 그림자가 생긴다. 방에는 그림자를 드리울 사람이 없다.
// 2) 그림자에 3m 안으로 다가가 바라보면 그림자가 깜빡이며 벽 속으로 사라진다. 시선을 떼면 그 벽이 열려 긴 파란 복도(약 15m)가 뻗어 있다.
// 3) 복도 끝 문에 바짝 다가가 바라보면 도어뷰어(볼록렌즈) 시야. 문밖은 천사 방 하늘이고, 키 큰 인외가 렌즈를 들여다보고 있다.
// 4) 다시 보면 인외는 없고, 시선을 뗀 사이 문이 열린다. 문 너머 빛으로 들어가면 컴퓨터 속 맵 북쪽 벽에 붙은 같은 문 앞으로 옮겨진다.
//    (2026-11-18 컴퓨터 속 맵 추가 전에는 천사 방으로 바로 갔다. 컴퓨터 속 → 천사 방은 Computer.cs)
// 문·인외·도어뷰어 동작은 Assets/Scripts/BlueRoomDoor.cs, 볼록렌즈 화면은 FisheyeDisc.cs. 모두 그레이박스 더미다.
public static partial class GreyboxGraphicsPass
{
    const float CrZ = -134.7f, CrW = 1.4f, CrH = 2.4f;   // 복도 가운데 z, 폭, 높이. 침실 동쪽 벽 남쪽 끝(창문·전화기 탁자 옆)에서 시작
    const float CrX0 = 49.3f, CrX1 = 64f;                 // 복도 안쪽 x 범위(침실 벽 바깥면 → 끝 벽 안쪽면)
    const float DoorW = 1.0f, DoorH = 2.1f;
    static readonly Vector3 AngelDoorPos = new Vector3(38.3f, 0f, BdOutZ);   // 천사 방 동쪽 벽(두께 가운데). 예전 침실 → 천사 방 구멍 자리

    static void BuildBlueGimmick(Transform gen, Transform props, Transform lump, Bounds room, VolumeProfile profile,
        Material wall, Material carpet, Material ceil, Material furn, Material deep, Material black)
    {
        var root = Group(gen, "BlueCorridor");

        // ---- 출구 벽(찾기 규칙). 그림자를 찾아야 열린다. 걸레받이는 PatchDoorwayTrims가 잇는다.
        var gate = FindGate(gen, "FindGate_Bedroom", 6, "BedroomShadow",
            new Bounds(new Vector3(BdX1 + 0.15f, CrH / 2, CrZ), new Vector3(0.3f, CrH, CrW)), "GB_03c_Bedroom/GFX_Generated/Shell/East/Seg_0");

        // ---- 1) 이불 속 사람: 꺼질 때 그림자를 켠다. 출구 벽에 이 id를 남겨도 출구 조건(그림자)에는 상관없다.
        var gz = AddGaze(lump.gameObject, "BedroomFigure", gate, room);
        gz.blinkRenderers = lump.GetComponentsInChildren<Renderer>();
        gz.blinkCount = 2; gz.blinkOff = 0.18f;
        // 판정 컴포넌트가 붙은 lump 자체를 끄면 코루틴도 멈춘다. 그래서 몸 조각(자식)만 끈다.
        var parts = new GameObject[lump.childCount];
        for (int i = 0; i < parts.Length; i++) parts[i] = lump.GetChild(i).gameObject;
        gz.hideOnFound = parts;
        gz.hideDelay = 1.6f;   // 꿈틀한 뒤 꺼지고, 같은 순간 벽에 그림자가 생긴다

        // ---- 2) 벽의 그림자: 출구 벽의 방 쪽 면에 붙인 반투명 실루엣. 발끝이 바닥에 닿아 있다.
        var shadowMat = Lit("GB_Bedroom_WallShadow", m =>
        {
            Plain(m, new Color(0.008f, 0.012f, 0.04f, 0.82f), 0f);
            m.SetTexture("_BaseColorMap", ShadowTexture());
            m.SetFloat("_SurfaceType", 1f); m.SetFloat("_BlendMode", 0f);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_TransparentZWrite", 0f);
            m.SetFloat("_AlphaCutoffEnable", 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
        });
        var shadowRoot = new GameObject("Oddity_WallShadow");
        shadowRoot.transform.SetParent(props, true);
        shadowRoot.transform.position = new Vector3(BdX1 - 0.012f, 0.95f, CrZ + 0.05f);
        var sil = Prim(PrimitiveType.Quad, shadowRoot.transform, "Silhouette", shadowRoot.transform.position, new Vector3(0.8f, 1.86f, 1f), shadowMat, LBedroom, false,
            new Vector3(0f, 90f, 0f)); // Quad의 보이는 면(-Z)이 방 쪽(-X)을 향한다
        var sg = AddGaze(shadowRoot, "BedroomShadow", gate, room);
        sg.gazeRenderers = new[] { sil.GetComponent<Renderer>() };
        sg.maxDistance = 3f; sg.dwellTime = 1f; sg.boundsPadding = 0.2f;
        sg.blinkRenderers = new[] { sil.GetComponent<Renderer>() };
        sg.blinkCount = 2; sg.blinkOff = 0.15f;
        sg.hideOnFound = new[] { sil };   // 판정 컴포넌트가 붙은 shadowRoot는 끄지 않는다
        sg.hideDelay = 1f; sg.notifyDelay = 1.3f;
        gz.showOnFound = new[] { shadowRoot };
        shadowRoot.SetActive(false);
        EditorUtility.SetDirty(gz); EditorUtility.SetDirty(sg);

        // ---- 3) 복도: 침실과 같은 벽·카펫·천장. 끝 벽 가운데에 문.
        var shell = Group(root, "Shell");
        float xa = CrX0, xb = CrX1 + 0.3f, zs = CrZ - CrW / 2, zn = CrZ + CrW / 2;
        Box(shell, "Floor", new Vector3((xa + xb) / 2, -0.15f, CrZ), new Vector3(xb - xa, 0.3f, CrW + 0.6f), carpet, LBedroom, true);
        Box(shell, "Ceiling", new Vector3((xa + xb) / 2, CrH + 0.15f, CrZ), new Vector3(xb - xa, 0.3f, CrW + 0.6f), ceil, LBedroom);
        Box(shell, "North", new Vector3((xa + xb) / 2, CrH / 2, zn + 0.15f), new Vector3(xb - xa, CrH, 0.3f), wall, LBedroom, true);
        Box(shell, "South", new Vector3((xa + xb) / 2, CrH / 2, zs - 0.15f), new Vector3(xb - xa, CrH, 0.3f), wall, LBedroom, true);
        WallWithHoles(shell, "End", false, CrX1 + 0.15f, 0.3f, zs - 0.3f, zn + 0.3f, 0f, CrH, new[] { (CrZ - DoorW / 2 - 0.08f, CrZ + DoorW / 2 + 0.08f, DoorH + 0.08f) }, wall, LBedroom);
        var trim = Group(root, "Baseboard");
        Cover(trim, "N", true, zn - 0.02f, xa, CrX1, 0.1f, xa, xa, 0.1f, deep, LBedroom);
        Cover(trim, "S", true, zs + 0.02f, xa, CrX1, 0.1f, xa, xa, 0.1f, deep, LBedroom);
        Cover(trim, "End", false, CrX1 - 0.02f, zs, zn, 0.1f, CrZ - DoorW / 2 - 0.08f, CrZ + DoorW / 2 + 0.08f, 0.1f, deep, LBedroom);

        // 복도 빛: 어둡고 파란 천장 빛 두 개 + 문틈으로 새는 하늘빛.
        var lights = Group(root, "Lights");
        foreach (var x in new[] { 52.5f, 58.5f })
            MakeLight(lights, "CorridorFill", LightType.Point, new Vector3(x, CrH - 0.2f, CrZ), Vector3.zero, new Color(0.32f, 0.44f, 1f), 7000f, 6f, LBedroom, false, false);
        MakeLight(lights, "DoorLeak", LightType.Point, new Vector3(CrX1 - 0.35f, 0.25f, CrZ), Vector3.zero, new Color(0.8f, 0.9f, 1f), 2500f, 3.5f, LBedroom, false, false);
        LocalVolume(root, "LocalVolume", new Vector3((xa + xb + 2.2f) / 2, CrH / 2, CrZ), new Vector3(xb + 2.2f - xa, CrH, CrW), 0.5f, 125f, profile);

        // 문 너머: 하늘빛으로 가득한 작은 칸. 문틈으로 빛이 새고, 문이 열리면 빛만 보인다. 여기에 들어서면 천사 방으로 옮긴다.
        var glow = Lit("GB_Bedroom_DoorLight", m => Emissive(m, new Color(0.82f, 0.9f, 1f), 420f));
        var beyond = Group(root, "Beyond");
        float bx0 = CrX1 + 0.3f, bx1 = bx0 + 2f;
        Box(beyond, "Floor", new Vector3((bx0 + bx1) / 2, -0.15f, CrZ), new Vector3(bx1 - bx0, 0.3f, CrW + 0.6f), glow, LBedroom, true);
        Box(beyond, "Ceiling", new Vector3((bx0 + bx1) / 2, CrH + 0.15f, CrZ), new Vector3(bx1 - bx0, 0.3f, CrW + 0.6f), glow, LBedroom);
        Box(beyond, "Back", new Vector3(bx1 + 0.15f, CrH / 2, CrZ), new Vector3(0.3f, CrH, CrW + 0.6f), glow, LBedroom, true);
        Box(beyond, "North", new Vector3((bx0 + bx1) / 2, CrH / 2, zn + 0.15f), new Vector3(bx1 - bx0, CrH, 0.3f), glow, LBedroom, true);
        Box(beyond, "South", new Vector3((bx0 + bx1) / 2, CrH / 2, zs - 0.15f), new Vector3(bx1 - bx0, CrH, 0.3f), glow, LBedroom, true);

        // ---- 4) 문 두 개: 복도 끝(닫힘)과 컴퓨터 속 맵 북쪽 벽(열림, 도착할 때만 보임). 같은 모양이다.
        var from = new GameObject("DoorAnchor_Corridor").transform;
        from.SetParent(root, true);
        from.SetPositionAndRotation(new Vector3(CrX1 + 0.15f, 0f, CrZ), Quaternion.LookRotation(Vector3.right));
        var to = new GameObject("DoorAnchor_Computer").transform;
        to.SetParent(root, true);
        to.SetPositionAndRotation(PcDoorPos, Quaternion.LookRotation(Vector3.back));

        var brass = Lit("GB_Bedroom_Brass", m => Plain(m, new Color(0.75f, 0.62f, 0.35f), 0.7f, 1f));
        var hinge = BlueDoor(from, "Door", 0f, 0.34f, furn, deep, brass, black, LBedroom, true);
        var blocker = new GameObject("LeafBlocker");
        blocker.transform.SetParent(from, false);
        blocker.transform.localPosition = new Vector3(0f, DoorH / 2, 0f);
        var bbc = blocker.AddComponent<BoxCollider>(); bbc.size = new Vector3(DoorW, DoorH, 0.3f);

        // 컴퓨터 속 쪽 문: 하늘 벽면(앵커에서 0.15 앞)에 덧댄 문틀 + 방 쪽으로 열린 문짝. 문틀 안쪽은 어두운 복도처럼 보이는 판.
        var arrival = new GameObject("ArrivalDoor").transform;
        arrival.SetParent(to, false);
        var aHinge = BlueDoor(arrival, "Door", 0.18f, 0.06f, furn, deep, brass, black, LComputer, false);
        aHinge.localRotation = Quaternion.Euler(0f, 95f, 0f);
        var dark = Lit("GB_Bedroom_CorridorDark", m => Plain(m, new Color(0.02f, 0.03f, 0.1f), 0.05f));
        var dp = Box(arrival, "Dark", to.TransformPoint(new Vector3(0f, DoorH / 2, 0.152f)), new Vector3(DoorW, DoorH, 0.004f), dark, LComputer);
        dp.transform.rotation = to.rotation;
        arrival.gameObject.SetActive(false);

        // 개발용 방에서 시작(F8 컴퓨터 속)의 시작 자리: 도착 문 앞 2m에서 방 안쪽을 본다.
        var spawn = new GameObject("Spawn_ComputerArrival").transform;
        spawn.SetParent(root, true);
        spawn.SetPositionAndRotation(to.TransformPoint(new Vector3(0f, 0.05f, 2f)), to.rotation);

        // ---- 도어뷰어: 컴퓨터 속 쪽 문 바로 앞에서 방 안쪽을 넓게 찍는 카메라 + 문 앞에 서서 렌즈를 들여다보는 인외.
        var camGo = new GameObject("PeepCamera");
        camGo.transform.SetParent(root, true);
        camGo.transform.SetPositionAndRotation(to.TransformPoint(new Vector3(0f, 1.55f, 0.22f)), to.rotation);
        var pcam = camGo.AddComponent<Camera>();
        camGo.AddComponent<HDAdditionalCameraData>();
        pcam.fieldOfView = 105f; pcam.nearClipPlane = 0.03f; pcam.farClipPlane = 300f;
        pcam.targetTexture = PeepholeTexture();
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (c.GetComponentInParent<CharacterController>(true) != null) { pcam.cullingMask = c.cullingMask; break; }
        pcam.enabled = false;

        var figure = BuildPeepFigure(root, to);
        figure.SetActive(false);

        var door = root.gameObject.AddComponent<BlueRoomDoor>();
        var vp = new GameObject("ViewPoint").transform;
        vp.SetParent(from, false);
        vp.localPosition = new Vector3(0f, 1.55f, -0.15f);
        door.viewPoint = vp;
        door.peepCamera = pcam;
        door.figure = figure;
        door.leafHinge = hinge;
        door.leafBlocker = bbc;
        door.teleportZone = new Bounds(new Vector3(bx0 + 1.1f, CrH / 2, CrZ), new Vector3(1.8f, CrH, CrW));
        door.fromAnchor = from;
        door.toAnchor = to;
        door.arrivalDoor = arrival.gameObject;
        EditorUtility.SetDirty(door);
    }

    /// <summary>
    /// 앵커 기준 문 하나(문틀 + 경첩 + 문짝 + 손잡이 + 도어뷰어 렌즈). 앵커 forward = 문을 지나가는 방향, 오른쪽(+x)에 경첩.
    /// zc·depth = 문틀의 앵커 기준 앞뒤 가운데와 깊이. 돌려주는 경첩을 y축으로 +돌리면 문짝이 앞쪽으로 열린다.
    /// </summary>
    static Transform BlueDoor(Transform anchor, string name, float zc, float depth, Material leafMat, Material frameMat, Material brass, Material black, uint layer, bool lensOnBack)
    {
        var g = new GameObject(name).transform;
        g.SetParent(anchor, false);
        void Piece(Transform p, string n, Vector3 lc, Vector3 s, Material m, PrimitiveType type = PrimitiveType.Cube, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = n;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(p, false);
            go.transform.localPosition = lc; go.transform.localScale = s;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.renderingLayerMask = layer;
        }
        const float jw = 0.08f;
        Piece(g, "Jamb_L", new Vector3(-(DoorW / 2 + jw / 2), DoorH / 2, zc), new Vector3(jw, DoorH, depth), frameMat);
        Piece(g, "Jamb_R", new Vector3(DoorW / 2 + jw / 2, DoorH / 2, zc), new Vector3(jw, DoorH, depth), frameMat);
        Piece(g, "Header", new Vector3(0f, DoorH + jw / 2, zc), new Vector3(DoorW + jw * 2, jw, depth), frameMat);

        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(g, false);
        hinge.localPosition = new Vector3(DoorW / 2 - 0.008f, 0f, zc);
        // 문짝은 문틀보다 조금 작아서 둘레 틈으로 너머의 빛이 샌다.
        const float lw = DoorW - 0.03f, lh = DoorH - 0.035f, lt = 0.05f;
        Piece(hinge, "Leaf", new Vector3(-lw / 2 - 0.007f, 0.025f + lh / 2, 0f), new Vector3(lw, lh, lt), leafMat);
        foreach (var py in new[] { 0.55f, 1.45f })
            Piece(hinge, "Panel", new Vector3(-lw / 2 - 0.007f, py, -lt / 2 - 0.005f), new Vector3(lw - 0.24f, 0.7f, 0.01f), frameMat);
        float knobX = -lw + 0.08f, side = lensOnBack ? -1f : 1f;
        Piece(hinge, "Knob", new Vector3(knobX, 1.0f, side * (lt / 2 + 0.03f)), new Vector3(0.06f, 0.06f, 0.06f), brass, PrimitiveType.Sphere);
        Piece(hinge, "Viewer_Ring", new Vector3(-lw / 2 - 0.007f, 1.55f, side * (lt / 2 + 0.006f)), new Vector3(0.045f, 0.006f, 0.045f), brass, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
        Piece(hinge, "Viewer_Lens", new Vector3(-lw / 2 - 0.007f, 1.55f, side * (lt / 2 + 0.01f)), new Vector3(0.022f, 0.004f, 0.022f), black, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
        return hinge;
    }

    /// <summary>
    /// 도어뷰어로 보이는 인외: 얼굴 없는 흰 형체, 아주 긴 팔다리. 문 앞에 서서 상체를 문 쪽으로 깊이 숙여 렌즈를 들여다본다.
    /// 한 손은 렌즈 옆 문틀을 짚고 있다. 좌표는 도착 쪽 앵커 기준(forward = 방 안쪽, 렌즈는 z 0.22·높이 1.55).
    /// </summary>
    static GameObject BuildPeepFigure(Transform parent, Transform anchor)
    {
        var skin = Lit("GB_Peep_Figure", m => Plain(m, new Color(0.93f, 0.93f, 0.95f), 0.32f));
        var g = new GameObject("Peephole_Figure").transform;
        g.SetParent(parent, true);
        g.SetPositionAndRotation(anchor.position, anchor.rotation);
        Vector3 W(Vector3 l) => anchor.TransformPoint(l);
        void Limb(string n, Vector3 a, Vector3 b, float th)
        {
            Vector3 wa = W(a), wb = W(b), d = wb - wa;
            var go = Prim(PrimitiveType.Capsule, g, n, (wa + wb) / 2, new Vector3(th, d.magnitude / 2 + th / 2, th), skin, LComputer);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        }
        void Ball(string n, Vector3 c, Vector3 s, Vector3 euler)
        {
            var go = Prim(PrimitiveType.Sphere, g, n, W(c), s, skin, LComputer);
            go.transform.rotation = anchor.rotation * Quaternion.Euler(euler);
        }
        Vector3 hip = new Vector3(0f, 2.2f, 2.0f), chest = new Vector3(0f, 1.98f, 1.3f), neck = new Vector3(0.05f, 1.76f, 1.08f);
        // 다리: 문에서 2m 떨어져 선 아주 긴 두 다리
        Limb("Leg_L", new Vector3(-0.2f, 0.05f, 2.15f), hip + new Vector3(-0.12f, 0f, 0f), 0.11f);
        Limb("Leg_R", new Vector3(0.2f, 0.05f, 2.05f), hip + new Vector3(0.12f, 0f, 0f), 0.11f);
        Ball("Pelvis", hip, new Vector3(0.36f, 0.22f, 0.24f), Vector3.zero);
        // 몸통: 엉덩이에서 문 쪽 아래로 꺾여 숙인다
        Limb("Torso", hip, chest, 0.3f);
        Ball("Chest", chest, new Vector3(0.46f, 0.3f, 0.3f), new Vector3(-40f, 0f, 0f));
        Limb("Neck", chest, neck, 0.08f);
        // 머리: 얼굴 없는 길쭉한 머리, 렌즈 앞 약 0.7m에서 고개를 갸웃한다(너무 붙이면 렌즈를 가린 얼룩으로만 보였다)
        Ball("Head", new Vector3(0.1f, 1.6f, 0.95f), new Vector3(0.27f, 0.4f, 0.29f), new Vector3(-25f, 0f, 18f));
        // 왼팔: 무릎 아래까지 늘어진다
        Vector3 shL = chest + new Vector3(-0.26f, 0.02f, 0.02f), elL = new Vector3(-0.45f, 1.1f, 1.4f), haL = new Vector3(-0.4f, 0.3f, 1.35f);
        Limb("Arm_L_Upper", shL, elL, 0.075f);
        Limb("Arm_L_Lower", elL, haL, 0.065f);
        Ball("Hand_L", haL + new Vector3(0f, -0.06f, 0f), new Vector3(0.08f, 0.16f, 0.05f), Vector3.zero);
        for (int i = 0; i < 4; i++) Limb("Finger_L", haL + new Vector3(-0.03f + i * 0.02f, -0.12f, 0f), haL + new Vector3(-0.045f + i * 0.03f, -0.34f, 0.02f), 0.018f);
        // 오른팔: 렌즈 옆으로 길쭉한 손가락을 뻗어 문을 짚으려 한다
        Vector3 shR = chest + new Vector3(0.26f, 0.02f, 0.02f), elR = new Vector3(0.72f, 2.0f, 0.95f), haR = new Vector3(0.5f, 1.78f, 0.5f);
        Limb("Arm_R_Upper", shR, elR, 0.075f);
        Limb("Arm_R_Lower", elR, haR, 0.065f);
        Ball("Hand_R", haR, new Vector3(0.1f, 0.16f, 0.04f), Vector3.zero);
        for (int i = 0; i < 4; i++) Limb("Finger_R", haR + new Vector3(-0.03f, 0.04f - i * 0.035f, 0f), haR + new Vector3(-0.3f, 0.12f - i * 0.08f, -0.01f), 0.016f);
        return g.gameObject;
    }

    static RenderTexture PeepholeTexture()
    {
        const string path = "Assets/Textures/Greybox/GB_Bedroom_Peephole.renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (rt != null) return rt;
        rt = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGBHalf) { name = "GB_Bedroom_Peephole" };
        AssetDatabase.CreateAsset(rt, path);
        return rt;
    }

    // 서 있는 사람 그림자(알파): 머리, 어깨·몸통, 늘어뜨린 두 팔, 두 다리. 가장자리를 흐리게 해 벽에 드리운 그림자처럼 보이게 한다.
    static Texture2D ShadowTexture()
    {
        const int w = 128, h = 296;
        const string path = "Assets/Textures/Greybox/GB_Bedroom_Shadow.png";
        float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude - r;
        }
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x, y);
                float d = (p - new Vector2(64, 266)).magnitude - 18f;                         // 머리
                d = Mathf.Min(d, Seg(p, new Vector2(64, 240), new Vector2(64, 250), 8f));      // 목
                d = Mathf.Min(d, Seg(p, new Vector2(46, 228), new Vector2(82, 228), 12f));     // 어깨
                d = Mathf.Min(d, Seg(p, new Vector2(64, 222), new Vector2(64, 150), 23f));     // 몸통
                d = Mathf.Min(d, Seg(p, new Vector2(38, 226), new Vector2(31, 128), 8f));      // 왼팔
                d = Mathf.Min(d, Seg(p, new Vector2(90, 226), new Vector2(97, 128), 8f));      // 오른팔
                d = Mathf.Min(d, Seg(p, new Vector2(54, 150), new Vector2(51, 8), 11f));       // 왼다리
                d = Mathf.Min(d, Seg(p, new Vector2(74, 150), new Vector2(77, 8), 11f));       // 오른다리
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-3f, 4f, d));
                a *= Mathf.Lerp(1f, 0.8f, y / (float)h);                                     // 위로 갈수록 살짝 옅게
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
