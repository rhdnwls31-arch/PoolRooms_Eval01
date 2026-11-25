using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 2026-11-25 목각인형 화실(기획서 §6-3b2, 사용자 결정): 밤의 거실 → 화실 → 파란 침실.
// 빈 땅(x -40~-32, z -140~-122)에 따로 짓고 순간이동으로 잇는다. 거실·침실의 벽과 문 좌표는 바꾸지 않는다.
// - 들어오기: 거실 → 침실 구멍 안(검은 막) → 화실 남쪽 벽 앞(등 뒤에 닫힌 문).
// - 나가기: 북쪽 문(마지막 단계에 열림) → 침실 구멍 바로 안쪽.
// 분위기: 어두운 거실·침실 사이에서 혼자 희뿌옇고 창백한 화실. 천장 채광창의 흐린 회색 낮빛, 흰 천 덮은 가구, 크로키 종이.
// 인형 7명(VARCO 자세 5종)은 DollStepper가 돌아볼 때마다 한 단계씩 옮긴다.
public static partial class GreyboxGraphicsPass
{
    const uint LDoll = 1u << 10;
    const float DrX0 = -40f, DrX1 = -32f, DrZ0 = -140f, DrZ1 = -122f, DrH = 3f;
    const float DrCx = (DrX0 + DrX1) / 2f;
    const float DrDoorW = 1.2f, DrDoorH = 2.1f;
    static readonly string[] DollPoses = { "Bowed", "HeadUp", "ArmHalf", "ArmReach", "PointSide" };

    // 인형 7명의 단계 자리(x = 가운데에서 옆, y = 입구에서 앞). 0 처음(북쪽 끝) → 1 11.5m → 2 8m → 3 5m → 4 양옆 벽(가리킴)
    // 마지막: 서쪽 벽(x -3.2, 동쪽을 보고 왼손 = 북쪽을 가리킴), 동쪽 벽(x +3.2, 좌우 뒤집어 같은 자세).
    static readonly float[] DollLat = { -2.4f, -1.2f, 0f, 1.2f, 2.4f, -0.6f, 0.6f };
    static readonly float[,] DollDepth =
    {
        { 15.4f, 12.4f, 9.0f, 5.8f }, { 15.9f, 12.0f, 8.6f, 5.2f }, { 15.2f, 11.5f, 8.0f, 5.0f }, { 15.8f, 12.1f, 8.7f, 5.3f },
        { 15.3f, 12.5f, 9.1f, 5.9f }, { 16.7f, 13.2f, 9.8f, 6.4f }, { 16.8f, 13.3f, 9.9f, 6.5f },
    };
    static readonly float[] DollJitter = { -8f, 5f, 0f, -4f, 9f, -12f, 10f };
    static readonly (bool west, float depth)[] DollFinal = { (true, 6f), (false, 7.5f), (true, 9f), (false, 10.5f), (true, 12f), (false, 13.5f), (true, 15f) };

    static void BuildDollRoom()
    {
        if (VarcoPrefab("DollRoom", "Doll_" + DollPoses[0]) == null) return;
        NewRoomRoot("GB_03b2_DollRoom", out var gen);
        NormalizeDollTint();

        var wall = Lit("GB_Doll_Wall", m => Plain(m, new Color(0.76f, 0.75f, 0.72f), 0.12f));
        var floor = Lit("GB_Doll_Floor", m => Plain(m, new Color(0.58f, 0.55f, 0.5f), 0.22f));
        var ceil = Lit("GB_Doll_Ceiling", m => Plain(m, new Color(0.8f, 0.8f, 0.78f), 0.1f));
        var sheet = Lit("GB_Doll_Sheet", m => Plain(m, new Color(0.9f, 0.89f, 0.86f), 0.08f));
        var wood = Lit("GB_Doll_Wood", m => Plain(m, new Color(0.4f, 0.32f, 0.25f), 0.3f));
        var paper = Lit("GB_Doll_Paper", m => { Plain(m, Color.white, 0.05f); m.SetTexture("_BaseColorMap", Img2D("DollRoom", "Sketch") ?? SketchTexture()); }); // VARCO 2D가 오면 그 그림
        var sky = Lit("GB_Doll_Skylight", m => Emissive(m, new Color(0.86f, 0.9f, 0.96f), 700f));
        var glow = Lit("GB_Doll_ExitGlow", m => Emissive(m, new Color(1f, 0.97f, 0.9f), 2500f));
        var black = Lit("GB_Doll_Veil", m => Plain(m, Color.black, 0f));

        // ---- 껍데기: 남쪽 벽(입구, 닫힌 문) · 북쪽 벽(출구 문 구멍)
        var shell = Group(gen, "Shell");
        var center = new Vector3(DrCx, 0f, (DrZ0 + DrZ1) / 2);
        Box(shell, "Floor", new Vector3(DrCx, -0.15f, center.z), new Vector3(DrX1 - DrX0 + 0.6f, 0.3f, DrZ1 - DrZ0 + 0.6f), floor, LDoll, true);
        Box(shell, "Ceiling", new Vector3(DrCx, DrH + 0.15f, center.z), new Vector3(DrX1 - DrX0 + 0.6f, 0.3f, DrZ1 - DrZ0 + 0.6f), ceil, LDoll);
        WallWithHoles(shell, "West", false, DrX0 - 0.15f, 0.3f, DrZ0 - 0.3f, DrZ1 + 0.3f, 0f, DrH, new (float, float, float)[0], wall, LDoll);
        WallWithHoles(shell, "East", false, DrX1 + 0.15f, 0.3f, DrZ0 - 0.3f, DrZ1 + 0.3f, 0f, DrH, new (float, float, float)[0], wall, LDoll);
        WallWithHoles(shell, "South", true, DrZ0 - 0.15f, 0.3f, DrX0 - 0.3f, DrX1 + 0.3f, 0f, DrH, new (float, float, float)[0], wall, LDoll);
        WallWithHoles(shell, "North", true, DrZ1 + 0.15f, 0.3f, DrX0 - 0.3f, DrX1 + 0.3f, 0f, DrH, new[] { (DrCx - DrDoorW / 2, DrCx + DrDoorW / 2, DrDoorH) }, wall, LDoll);
        // 천장 채광창: 2026-11-25 사용자 "두 줄 → 한 줄을 가운데, 띄엄띄엄". 가운데 한 줄에 작은 창 4개(1.2×1.6m, 나무 틀)를 4m 간격으로.
        foreach (float d in new[] { 3f, 7f, 11f, 15f })
        {
            float z = DrZ0 + d;
            Box(shell, $"Skylight_{d:F0}", new Vector3(DrCx, DrH - 0.01f, z), new Vector3(1.2f, 0.02f, 1.6f), sky, LDoll);
            Box(shell, $"SkylightFrame_{d:F0}_N", new Vector3(DrCx, DrH - 0.03f, z + 0.82f), new Vector3(1.32f, 0.05f, 0.08f), wood, LDoll);
            Box(shell, $"SkylightFrame_{d:F0}_S", new Vector3(DrCx, DrH - 0.03f, z - 0.82f), new Vector3(1.32f, 0.05f, 0.08f), wood, LDoll);
            Box(shell, $"SkylightFrame_{d:F0}_W", new Vector3(DrCx - 0.62f, DrH - 0.03f, z), new Vector3(0.08f, 0.05f, 1.72f), wood, LDoll);
            Box(shell, $"SkylightFrame_{d:F0}_E", new Vector3(DrCx + 0.62f, DrH - 0.03f, z), new Vector3(0.08f, 0.05f, 1.72f), wood, LDoll);
        }

        // ---- 입구: 등 뒤 닫힌 문(남쪽 벽)
        var entryDoor = Group(gen, "EntryDoor");
        Box(entryDoor, "Leaf", new Vector3(DrCx, DrDoorH / 2, DrZ0 + 0.03f), new Vector3(DrDoorW, DrDoorH, 0.05f), wood, LDoll, true);
        Box(entryDoor, "Knob", new Vector3(DrCx + 0.45f, 1.0f, DrZ0 + 0.08f), new Vector3(0.06f, 0.06f, 0.06f), wood, LDoll);
        // 2026-11-25 재활용: 꽃무늬 복도 VARCO 현관문(낡은 갈색 나무, 원점 = 뒷면 가운데, 1.10×2.20)을 문 구멍 크기로 맞춰 씌운다
        DollSwap(entryDoor, "FloralCorridor", "Door", new Vector3(DrCx, DrDoorH / 2, DrZ0), 0f, new Vector3(DrDoorW / 1.10f, DrDoorH / 2.20f, 1f));

        // ---- 출구: 북쪽 문(마지막 단계에 사라짐) + 문 너머 흰 빛 칸
        var door = Group(gen, "ExitDoorClosed");
        Box(door, "Leaf", new Vector3(DrCx, DrDoorH / 2, DrZ1 + 0.15f), new Vector3(DrDoorW, DrDoorH, 0.1f), wood, LDoll, true);
        Box(door, "Knob", new Vector3(DrCx + 0.45f, 1.0f, DrZ1 + 0.08f), new Vector3(0.06f, 0.06f, 0.06f), wood, LDoll);
        DollSwap(door, "FloralCorridor", "Door", new Vector3(DrCx, DrDoorH / 2, DrZ1 + 0.2f), 180f, new Vector3(DrDoorW / 1.10f, DrDoorH / 2.20f, 1f));
        var alcove = Group(gen, "ExitAlcove");
        float az0 = DrZ1 + 0.3f, az1 = DrZ1 + 2.3f;
        Box(alcove, "Floor", new Vector3(DrCx, -0.15f, (az0 + az1) / 2), new Vector3(1.8f, 0.3f, 2f), floor, LDoll, true);
        Box(alcove, "Ceiling", new Vector3(DrCx, DrDoorH + 0.15f, (az0 + az1) / 2), new Vector3(1.8f, 0.3f, 2f), glow, LDoll);
        Box(alcove, "West", new Vector3(DrCx - 0.75f, DrDoorH / 2, (az0 + az1) / 2), new Vector3(0.3f, DrDoorH, 2f), glow, LDoll, true);
        Box(alcove, "East", new Vector3(DrCx + 0.75f, DrDoorH / 2, (az0 + az1) / 2), new Vector3(0.3f, DrDoorH, 2f), glow, LDoll, true);
        Box(alcove, "Back", new Vector3(DrCx, DrDoorH / 2, az1 + 0.15f), new Vector3(1.8f, DrDoorH, 0.3f), glow, LDoll, true);
        var openLight = MakeLight(alcove, "ExitSpill", LightType.Point, new Vector3(DrCx, 1.4f, DrZ1 + 0.6f), Vector3.zero, new Color(1f, 0.96f, 0.88f), 9000f, 7f, LDoll, false, false);
        openLight.gameObject.SetActive(false);

        // ---- 소품: 흰 천 덮은 가구·이젤(남쪽 모서리, 북쪽 모서리 — 인형 길 x ±3.2, 깊이 5~15m는 비운다), 벽의 크로키 종이
        var props = Group(gen, "Props");
        // varco: 흰 천 덮은 가구 VARCO 모델 이름(DollRoom). 모델이 오면 더미 대신 높이를 맞춰 씌운다.
        // modelYaw: VARCO 모델(정면 +Z)이 방 안쪽을 보도록 돌리는 각도.
        void Draped(string n, Vector3 c, Vector3 s, float yaw, string varco, float modelYaw, float maxFoot)
        {
            var g = Group(props, n);
            Box(g, "Body", c + Vector3.up * (s.y / 2), s, sheet, LDoll, true, new Vector3(0f, yaw, 0f));
            Box(g, "Hem", c + Vector3.up * 0.04f, new Vector3(s.x + 0.08f, 0.08f, s.z + 0.08f), sheet, LDoll, false, new Vector3(0f, yaw, 0f));
            DollSwapFitHeight(g, "DollRoom", varco, c, modelYaw, s.y, maxFoot);
        }
        Draped("Sheet_Cabinet_SW", new Vector3(DrX0 + 0.6f, 0f, DrZ0 + 1.4f), new Vector3(0.9f, 1.1f, 1.6f), 0f, "SheetLow", 90f, 1.6f);
        Draped("Sheet_Chair_SE", new Vector3(DrX1 - 0.7f, 0f, DrZ0 + 1.2f), new Vector3(0.7f, 0.95f, 0.7f), 18f, "SheetChair", 300f, 1.0f);
        Draped("Sheet_Table_SE", new Vector3(DrX1 - 0.8f, 0f, DrZ0 + 3.0f), new Vector3(1.1f, 0.78f, 1.5f), -6f, "SheetLow", 264f, 1.5f);
        Draped("Sheet_Tall_NW", new Vector3(DrX0 + 0.55f, 0f, DrZ1 - 1.0f), new Vector3(0.8f, 1.9f, 0.9f), 4f, "SheetTall", 94f, 1.45f);
        Draped("Sheet_Chest_NE", new Vector3(DrX1 - 0.6f, 0f, DrZ1 - 1.2f), new Vector3(0.8f, 0.7f, 1.3f), -10f, "SheetLow", 260f, 1.3f);
        // 재활용: 노란 방 상자 더미(0.93×0.75) — 남서·북동 구석
        var boxesA = Group(props, "Boxes_S"); DollSwap(boxesA, "Yellow", "CardboardBoxes", new Vector3(DrX0 + 0.65f, 0f, DrZ0 + 3.0f), 80f, Vector3.one);
        var boxesB = Group(props, "Boxes_N"); DollSwap(boxesB, "Yellow", "CardboardBoxes", new Vector3(DrX1 - 0.6f, 0f, DrZ1 - 2.9f), 265f, Vector3.one);
        foreach (var bx in new[] { boxesA, boxesB }) if (bx.childCount > 0) { var bc = bx.gameObject.AddComponent<BoxCollider>(); bc.center = bx.GetChild(0).position + Vector3.up * 0.38f; bc.size = new Vector3(0.95f, 0.75f, 0.95f); }
        void Easel(string n, Vector3 at, float yaw)
        {
            var g = new GameObject(n).transform; g.SetParent(props, true); g.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            void L(string pn, Vector3 lp, Vector3 sz, Vector3 e, Material m)
            {
                var go = Box(g, pn, Vector3.zero, sz, m, LDoll);
                go.transform.localPosition = lp; go.transform.localRotation = Quaternion.Euler(e);
            }
            L("LegL", new Vector3(-0.3f, 0.8f, 0f), new Vector3(0.04f, 1.65f, 0.04f), new Vector3(0, 0, -8), wood);
            L("LegR", new Vector3(0.3f, 0.8f, 0f), new Vector3(0.04f, 1.65f, 0.04f), new Vector3(0, 0, 8), wood);
            L("LegB", new Vector3(0f, 0.78f, -0.28f), new Vector3(0.04f, 1.6f, 0.04f), new Vector3(-18, 0, 0), wood);
            L("Ledge", new Vector3(0f, 0.85f, 0.04f), new Vector3(0.7f, 0.04f, 0.08f), Vector3.zero, wood);
            L("Canvas", new Vector3(0f, 1.25f, 0.02f), new Vector3(0.62f, 0.78f, 0.03f), new Vector3(-6, 0, 0), sheet);
            DollSwapFitHeight(g, "DollRoom", "Easel", at, yaw, 1.7f, 0.9f);
        }
        Easel("Easel_S", new Vector3(DrX0 + 1.6f, 0f, DrZ0 + 3.2f), 35f);
        Easel("Easel_SE", new Vector3(DrX1 - 1.8f, 0f, DrZ0 + 4.6f), 230f); // 2026-11-25: 북쪽 자리는 처음 인형(오른쪽 끝)과 겹쳐 남동쪽으로
        // 크로키 종이: 서쪽·동쪽 벽(눈높이 근처, 조금씩 기울게)
        int k = 0;
        foreach (bool westWall in new[] { true, false })
            for (float d = 1.5f; d < 17.5f; d += 1.35f)
            {
                k++;
                float h = 1.35f + 0.35f * Mathf.Sin(k * 2.1f), tilt = 7f * Mathf.Sin(k * 1.7f);
                if ((k % 5) == 0) continue; // 군데군데 빈 자리
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = $"Sketch_{k}"; q.transform.SetParent(props, true);
                float x = westWall ? DrX0 + 0.012f : DrX1 - 0.012f;
                q.transform.SetPositionAndRotation(new Vector3(x, h, DrZ0 + d), Quaternion.LookRotation(westWall ? Vector3.left : Vector3.right) * Quaternion.Euler(0f, 0f, tilt));
                q.transform.localScale = new Vector3(0.42f, 0.56f, 1f);
                var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = paper; r.renderingLayerMask = LDoll; r.shadowCastingMode = ShadowCastingMode.Off;
            }

        // ---- 인형 7명(자세 5종을 자식으로 두고 하나만 켠다)
        var dollGroup = Group(gen, "Dolls");
        var stepperGo = new GameObject("DollStepper"); stepperGo.transform.SetParent(gen, true);
        stepperGo.transform.SetPositionAndRotation(new Vector3(DrCx, 0f, DrZ0), Quaternion.identity);
        var stepper = stepperGo.AddComponent<DollStepper>();
        var dolls = new DollStepper.Doll[DollLat.Length];
        for (int i = 0; i < dolls.Length; i++)
        {
            var root = new GameObject($"Doll_{i}").transform; root.SetParent(dollGroup, true);
            var cap = root.gameObject.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0f, 0.875f, 0f); cap.height = 1.75f; cap.radius = 0.25f;
            var poses = new GameObject[DollPoses.Length];
            for (int p = 0; p < DollPoses.Length; p++)
            {
                var pf = VarcoPrefab("DollRoom", "Doll_" + DollPoses[p]);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root);
                go.name = DollPoses[p];
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = LDoll;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                poses[p] = go;
            }
            var spots = new Vector2[5]; var yaw = new float[5]; var mirror = new bool[5];
            for (int s = 0; s < 4; s++) { spots[s] = new Vector2(DollLat[i], DollDepth[i, s]); yaw[s] = 180f + DollJitter[i]; }
            var fin = DollFinal[i];
            spots[4] = new Vector2(fin.west ? -3.2f : 3.2f, fin.depth);
            yaw[4] = fin.west ? 90f : 270f; // 서쪽 벽은 동쪽을 보고 왼손으로 북쪽을, 동쪽 벽은 서쪽을 보고(뒤집어) 북쪽을 가리킨다
            mirror[4] = !fin.west;
            dolls[i] = new DollStepper.Doll { root = root, poses = poses, spots = spots, yaw = yaw, mirror = mirror };
        }
        stepper.dolls = dolls;
        stepper.room = new Bounds(new Vector3(DrCx, DrH / 2, center.z), new Vector3(DrX1 - DrX0, DrH, DrZ1 - DrZ0));
        stepper.doorClosed = door.gameObject;
        stepper.doorOpen = openLight.gameObject;
        // 2026-11-25 피드백: 인형 사이로 지나가 문 앞에서 돌아보기만 반복하면 바로 나갈 수 있었다 →
        // 인형 무리 바로 앞에 방 폭 전체를 막는 보이지 않는 벽(인형과 함께 움직이고, 마지막 단계에서 꺼짐). 다가오는 거리 3.5 → 2m.
        var blockGo = new GameObject("DollLineBlocker"); blockGo.transform.SetParent(gen, true);
        blockGo.layer = IgnoreRaycastLayer;
        var blockBox = blockGo.AddComponent<BoxCollider>();
        blockBox.center = new Vector3(0f, DrH / 2, 0f); blockBox.size = new Vector3(DrX1 - DrX0, DrH, 0.2f);
        stepper.blocker = blockBox;
        stepper.keepAway = 2f;
        stepper.ResetDolls(); // 씬에 처음 자리로 저장
        EditorUtility.SetDirty(stepper);

        // ---- 빛: 채광창에서 내려오는 흐린 낮빛(그림자 하나) + 고른 채움
        // 2026-11-25 사용자 "조명이 너무 밝아, 살짝 어둡게": 채광창 판 1400→700, 스포트 60000→32000, 채움 14000→6000
        var lights = Group(gen, "Lights");
        var skyLight = MakeLight(lights, "SkylightSpot", LightType.Spot, new Vector3(DrCx, DrH + 2.5f, center.z + 1f), new Vector3(90f, 0f, 0f), new Color(0.88f, 0.92f, 1f), 32000f, 12f, LDoll, true, false);
        skyLight.spotAngle = 120f; skyLight.innerSpotAngle = 60f;
        MakeLight(lights, "Fill_S", LightType.Point, new Vector3(DrCx, 2.2f, DrZ0 + 4.5f), Vector3.zero, new Color(0.85f, 0.88f, 0.95f), 6000f, 9f, LDoll, false, false);
        MakeLight(lights, "Fill_N", LightType.Point, new Vector3(DrCx, 2.2f, DrZ1 - 4.5f), Vector3.zero, new Color(0.85f, 0.88f, 0.95f), 6000f, 9f, LDoll, false, false);
        var profile = RoomProfile("DollRoom", 7.6f, 0.02f, 0.05f, new Color(0.9f, 0.9f, 0.92f), 35f, new Color(0.06f, 0.06f, 0.065f), -8f, 0f);
        LocalVolume(gen, "LocalVolume", new Vector3(DrCx, DrH / 2, center.z + 1f), new Vector3(DrX1 - DrX0 + 0.6f, DrH + 0.6f, DrZ1 - DrZ0 + 2.6f), 0.5f, 125f, profile);

        // ---- 순간이동: 거실 → 침실 구멍 안 → 화실 입구 / 화실 북쪽 문 → 침실 구멍 바로 안쪽
        // 구멍 안 검은 막: 거실에서 구멍 너머 침실이 보이지 않게(꽃무늬 복도 입구와 같은 방식). 침실 쪽 통로 막기 판이 나중에 덮는다.
        Box(gen, "LivingHoleVeil", new Vector3(BdInX, 1.2f, -124.2f), new Vector3(1.8f, 2.4f, 0.02f), black, LLiving | LBedroom);
        var inFrom = new GameObject("LivingHoleAnchor").transform; inFrom.SetParent(gen, true);
        inFrom.SetPositionAndRotation(new Vector3(BdInX, 0f, LvZ0), Quaternion.LookRotation(Vector3.back));
        var inTo = new GameObject("DollEntryAnchor").transform; inTo.SetParent(gen, true);
        inTo.SetPositionAndRotation(new Vector3(DrCx, 0f, DrZ0), Quaternion.LookRotation(Vector3.forward));
        var tzIn = new GameObject("Teleport_LivingToDoll").AddComponent<TeleportZone>();
        tzIn.transform.SetParent(gen, true);
        tzIn.zone = new Bounds(new Vector3(BdInX, 1.2f, -124.1f), new Vector3(1.8f, 2.4f, 0.3f));
        tzIn.fromAnchor = inFrom; tzIn.toAnchor = inTo;
        EditorUtility.SetDirty(tzIn);
        var outFrom = new GameObject("DollExitAnchor").transform; outFrom.SetParent(gen, true);
        outFrom.SetPositionAndRotation(new Vector3(DrCx, 0f, DrZ1 + 0.3f), Quaternion.LookRotation(Vector3.forward));
        var outTo = new GameObject("BedroomArrivalAnchor").transform; outTo.SetParent(gen, true);
        outTo.SetPositionAndRotation(new Vector3(BdInX, 0f, BdZ1), Quaternion.LookRotation(Vector3.back));
        var tzOut = new GameObject("Teleport_DollToBedroom").AddComponent<TeleportZone>();
        tzOut.transform.SetParent(gen, true);
        tzOut.zone = new Bounds(new Vector3(DrCx, 1.05f, DrZ1 + 1.4f), new Vector3(1.2f, 2.1f, 1.2f));
        tzOut.fromAnchor = outFrom; tzOut.toAnchor = outTo;
        EditorUtility.SetDirty(tzOut);

        var spawn = new GameObject("Spawn_DollRoomArrival").transform; spawn.SetParent(gen, true);
        spawn.SetPositionAndRotation(new Vector3(DrCx, 0.05f, DrZ0 + 1f), Quaternion.LookRotation(Vector3.forward));
        Debug.Log("[GreyboxGraphicsPass] 목각인형 화실: 인형 7, 순간이동 2");
    }

    /// <summary>VARCO 모델이 있으면 holder의 더미 렌더러를 끄고(콜라이더는 남김) 그 자리에 세운다. scale = 모델에 곱할 배율.</summary>
    static GameObject DollSwap(Transform holder, string room, string name, Vector3 pos, float yaw, Vector3 scale)
    {
        var pf = VarcoPrefab(room, name);
        if (pf == null) return null;
        foreach (var r in holder.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, holder);
        go.name = "VARCO_" + name;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = scale;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = LDoll;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return go;
    }

    /// <summary>DollSwap + 같은 배율로 높이는 height, 바닥 긴 변은 maxFoot을 넘지 않게 맞춘다(둘 중 작은 배율).
    /// 2026-11-25: 낮은 장 VARCO 모델은 폭 1.93m라 높이로만 맞추면 자리(1.3~1.6m)보다 커졌다.</summary>
    static void DollSwapFitHeight(Transform holder, string room, string name, Vector3 pos, float yaw, float height, float maxFoot)
    {
        var pf = VarcoPrefab(room, name);
        if (pf == null) return;
        Bounds b = default; bool any = false;
        foreach (var r in pf.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        if (!any) return;
        float k = Mathf.Min(height / Mathf.Max(0.01f, b.max.y), maxFoot / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)));
        DollSwap(holder, room, name, pos, yaw, Vector3.one * k);
    }

    /// <summary>자세 5종의 텍스처 밝기가 달라(고개 숙임이 더 누렇다) 바꿔 끼울 때 티가 났다. 평균 밝기를 맞추도록 재질 색을 곱한다.</summary>
    static void NormalizeDollTint()
    {
        var mats = new Material[DollPoses.Length]; var lum = new float[DollPoses.Length]; float sum = 0f; int n = 0;
        for (int i = 0; i < DollPoses.Length; i++)
        {
            mats[i] = AssetDatabase.LoadAssetAtPath<Material>($"Assets/VARCO/DollRoom/Materials/VAR_DollRoom_Doll_{DollPoses[i]}.mat");
            if (mats[i] == null) continue;
            lum[i] = BaseMapLum(mats[i]); sum += lum[i]; n++;
        }
        if (n == 0) return;
        float avg = sum / n;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null || lum[i] <= 0f) continue;
            float k = Mathf.Pow(avg / lum[i], 2.2f); // sRGB 밝기 비 → 선형 배율
            mats[i].SetColor("_BaseColor", new Color(k, k, k, 1f).gamma);
            EditorUtility.SetDirty(mats[i]);
        }
    }

    // 크로키 종이: 미색 종이에 흐린 연필 선으로 서 있는 사람(같은 사람). 모두 같은 그림이다.
    static Texture2D SketchTexture()
    {
        string path = $"{TexDir}/GB_Doll_Sketch.png";
        var have = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (have != null) return have;
        const int w = 192, h = 256;
        var px = new Color[w * h];
        var bg = new Color(0.93f, 0.91f, 0.86f);
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        void Line(float x0, float y0, float x1, float y1, float a)
        {
            int steps = 300;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t) * w), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t) * h);
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                    px[yy * w + xx] = Color.Lerp(px[yy * w + xx], new Color(0.35f, 0.34f, 0.33f), a * (dx == 0 && dy == 0 ? 1f : 0.35f));
                }
            }
        }
        // 머리(타원), 몸, 팔, 다리 — 정면으로 선 사람
        for (int s = 0; s < 40; s++)
        {
            float a0 = s / 40f * Mathf.PI * 2f, a1 = (s + 1) / 40f * Mathf.PI * 2f;
            Line(0.5f + 0.07f * Mathf.Cos(a0), 0.82f + 0.08f * Mathf.Sin(a0), 0.5f + 0.07f * Mathf.Cos(a1), 0.82f + 0.08f * Mathf.Sin(a1), 0.7f);
        }
        Line(0.5f, 0.74f, 0.5f, 0.45f, 0.8f);
        Line(0.38f, 0.68f, 0.62f, 0.68f, 0.6f);
        Line(0.38f, 0.68f, 0.33f, 0.48f, 0.7f); Line(0.62f, 0.68f, 0.67f, 0.48f, 0.7f);
        Line(0.5f, 0.45f, 0.42f, 0.12f, 0.75f); Line(0.5f, 0.45f, 0.58f, 0.12f, 0.75f);
        Line(0.44f, 0.45f, 0.56f, 0.45f, 0.5f);
        System.IO.Directory.CreateDirectory(TexDir);
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
