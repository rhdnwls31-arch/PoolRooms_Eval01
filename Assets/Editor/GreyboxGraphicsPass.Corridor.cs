using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-18 꽃무늬 아파트 복도(사용자 요청): 노란 방 → 복도 → 밤의 거실.
// 어릴 적 살던 아파트 복도를 지나 우리 집(거실) 문으로 들어가는 기억 구간이다.
// 바랜 꽃무늬 벽지, 나무 징두리, 낡은 카펫, 줄지어 선 똑같은 현관문과 형광등. 저 끝 밝은 곳에 스마일 풍선이 떠 있다.
// 장치: 끝없는 복도(LoopCorridor). 이상한 점을 찾기 전까지는 아무리 걸어도 풍선에 가까워지지 않는다.
// 이상한 점(§4-1): 카펫 위 물웅덩이에 바로 위에 떠 있는 풍선이 비치는데, 올려다보면 아무것도 없다(반사만 틀림).
// 찾으면 웅덩이 속 풍선이 사라지고 반복이 멈춘다. 풍선 옆 마지막 문 안의 불빛으로 들어가면 거실이다.
// 공간이 맞지 않아 빈 땅에 짓고, 노란 방 출구 통로와 마지막 문을 순간이동(TeleportZone)으로 잇는다.
// 다른 게임의 맵은 따라 하지 않고 분위기만 빌렸다. 모두 그레이박스 더미다.
public static partial class GreyboxGraphicsPass
{
    const uint LCorr = 1u << 9;
    const float CoX0 = 100f, CoX1 = 185f, CoZ = -232f, CoW = 3f, CoH = 2.6f;
    const float CoSeg = 20f, CoLoopAt = 40f, CoAhead = 35f;   // 되풀이 길이, 반복 시작 거리, 끝이 앞서 있는 거리
    const float CoDoorEvery = 5f;                              // 문·등 간격(CoSeg를 나누어떨어지게)

    static void BuildCorridor()
    {
        NewRoomRoot("GB_02c_Corridor", out var gen);

        // ---- 재질
        var paper = Lit("GB_Corr_Wallpaper", m => Textured(m, FloralWallpaperSet(), 1f, Color.white, 0.4f));
        var carpet = Lit("GB_Corr_Carpet", m => Textured(m, CorridorCarpetSet(), 1f, Color.white, 0.6f));
        var ceil = Lit("GB_Corr_Ceiling", m => Plain(m, new Color(0.86f, 0.85f, 0.8f), 0.15f));
        var wood = Lit("GB_Corr_Wainscot", m => Plain(m, new Color(0.42f, 0.27f, 0.17f), 0.35f));
        var rail = Lit("GB_Corr_Rail", m => Plain(m, new Color(0.88f, 0.84f, 0.74f), 0.3f));
        var doorMat = Lit("GB_Corr_Door", m => Plain(m, new Color(0.4f, 0.19f, 0.15f), 0.45f, 0.3f));
        var frameMat = Lit("GB_Corr_DoorFrame", m => Plain(m, new Color(0.55f, 0.52f, 0.48f), 0.35f, 0.2f));
        var plate = Lit("GB_Corr_Plate", m => Plain(m, new Color(0.08f, 0.08f, 0.08f), 0.5f));
        var chrome = Lit("GB_Corr_Chrome", m => Plain(m, new Color(0.75f, 0.76f, 0.76f), 0.85f, 1f));
        var tube = Lit("GB_Corr_Tube", m => Emissive(m, new Color(0.95f, 1f, 0.94f), 260f));
        var homeLight = Lit("GB_Corr_HomeLight", m => Emissive(m, new Color(1f, 0.76f, 0.5f), 900f));
        var balloonMat = Lit("GB_Corr_Balloon", m => { Emissive(m, new Color(1f, 0.86f, 0.18f), 35f); m.SetFloat("_Smoothness", 0.65f); });
        var black = Lit("GB_Corr_Black", m => Plain(m, new Color(0.02f, 0.02f, 0.02f), 0.2f));
        var puddleMat = Lit("GB_Corr_Puddle", m => Plain(m, new Color(0.05f, 0.07f, 0.09f), 0.95f));
        var reflMat = Lit("GB_Corr_PuddleReflection", m =>
        {
            var t = PuddleReflectionTexture();
            Plain(m, Color.white, 0.9f);
            m.SetTexture("_BaseColorMap", t); m.SetFloat("_UVBase", 0f);
            m.SetTexture("_EmissiveColorMap", t); m.SetFloat("_UVEmissive", 0f);
            m.SetColor("_EmissiveColor", Color.white * 25f);
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
            m.SetFloat("_SurfaceType", 1f); m.SetFloat("_BlendMode", 0f);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_TransparentZWrite", 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
        });

        // ---- 껍데기: 바닥·천장·양옆 벽·시작 벽·먼 끝 벽
        var shell = Group(gen, "Shell");
        float len = CoX1 - CoX0, cx = (CoX0 + CoX1) / 2, zN = CoZ + CoW / 2, zS = CoZ - CoW / 2;
        Box(shell, "Floor", new Vector3(cx, -0.15f, CoZ), new Vector3(len + 0.6f, 0.3f, CoW + 0.6f), carpet, LCorr, true);
        Box(shell, "Ceiling", new Vector3(cx, CoH + 0.15f, CoZ), new Vector3(len + 0.6f, 0.3f, CoW + 0.6f), ceil, LCorr);
        Box(shell, "North", new Vector3(cx, CoH / 2, zN + 0.15f), new Vector3(len + 0.6f, CoH, 0.3f), paper, LCorr, true);
        Box(shell, "South", new Vector3(cx, CoH / 2, zS - 0.15f), new Vector3(len + 0.6f, CoH, 0.3f), paper, LCorr, true);
        Box(shell, "StartWall", new Vector3(CoX0 - 0.15f, CoH / 2, CoZ), new Vector3(0.3f, CoH, CoW), paper, LCorr, true);
        Box(shell, "FarWall", new Vector3(CoX1 + 0.15f, CoH / 2, CoZ), new Vector3(0.3f, CoH, CoW), paper, LCorr, true);
        // 징두리(아래 0.9m 나무판) + 위 몰딩
        // 문 자리(문틀 폭 1.22m)는 비운다. 2026-11-18 사용자 피드백: 몰딩이 문을 가로질렀다.
        const float doorHalf = 0.62f;
        foreach (var (z, s, first) in new[] { (zN, -1f, CoX0 + 1.25f), (zS, 1f, CoX0 + 3.75f) })
        {
            float a = CoX0;
            for (float dx = first; a < CoX1; dx += CoDoorEvery)
            {
                float b = dx < CoX1 - 1f ? dx - doorHalf : CoX1;
                if (b > a + 0.01f)
                {
                    float mid = (a + b) / 2, l = b - a;
                    Box(shell, "Wainscot", new Vector3(mid, 0.45f, z + s * 0.02f), new Vector3(l, 0.9f, 0.04f), wood, LCorr);
                    Box(shell, "ChairRail", new Vector3(mid, 0.92f, z + s * 0.035f), new Vector3(l, 0.06f, 0.07f), rail, LCorr);
                }
                a = dx < CoX1 - 1f ? dx + doorHalf : CoX1;
            }
        }

        // ---- 똑같은 현관문(양쪽, 5m마다 엇갈려) + 형광등(5m마다)
        var doors = Group(gen, "Doors");
        void Door(Transform parent, string n, Vector3 face, Vector3 normal)
        {
            // face = 벽면 위 문 가운데 바닥점, normal = 복도 쪽
            var rot = Quaternion.LookRotation(-normal);
            var g = new GameObject(n).transform;
            g.SetParent(parent, true); g.SetPositionAndRotation(face, rot);
            GameObject P(string pn, Vector3 local, Vector3 size, Material m)
            {
                var go = Box(g, pn, g.TransformPoint(local), size, m, LCorr, false, rot.eulerAngles);
                return go;
            }
            // 로컬: x = 문 가로, y = 위, -z = 복도 쪽
            P("Frame_L", new Vector3(-0.56f, 1.1f, -0.03f), new Vector3(0.1f, 2.2f, 0.06f), frameMat);
            P("Frame_R", new Vector3(0.56f, 1.1f, -0.03f), new Vector3(0.1f, 2.2f, 0.06f), frameMat);
            P("Frame_T", new Vector3(0f, 2.15f, -0.03f), new Vector3(1.22f, 0.1f, 0.06f), frameMat);
            P("Leaf", new Vector3(0f, 1.05f, -0.04f), new Vector3(1.0f, 2.1f, 0.06f), doorMat);
            P("Plate", new Vector3(0f, 1.75f, -0.075f), new Vector3(0.26f, 0.08f, 0.01f), plate);
            P("Peephole", new Vector3(0f, 1.5f, -0.075f), new Vector3(0.04f, 0.04f, 0.01f), chrome);
            P("Handle", new Vector3(0.38f, 1.0f, -0.1f), new Vector3(0.14f, 0.04f, 0.05f), chrome);
        }
        for (float x = CoX0 + 1.25f; x < CoX1 - 1f; x += CoDoorEvery)
            Door(doors, "Door_N", new Vector3(x, 0f, zN), Vector3.back);
        for (float x = CoX0 + 3.75f; x < CoX1 - 1f; x += CoDoorEvery)
            Door(doors, "Door_S", new Vector3(x, 0f, zS), Vector3.forward);
        // 시작 벽: 들어온 문(닫혀 있음)
        Door(gen, "Start", new Vector3(CoX0, 0f, CoZ), Vector3.right);

        var lights = Group(gen, "Lights");
        for (float x = CoX0 + 2.5f; x < CoX1; x += CoDoorEvery)
        {
            Box(lights, "Tube", new Vector3(x, CoH - 0.03f, CoZ), new Vector3(1.2f, 0.05f, 0.22f), tube, LCorr);
            MakeLight(lights, "TubeLight", LightType.Point, new Vector3(x, CoH - 0.35f, CoZ), Vector3.zero, new Color(0.95f, 1f, 0.92f), 16000f, 6.5f, LCorr, false, false);
        }

        var room = new Bounds(new Vector3(cx, CoH / 2, CoZ), new Vector3(len, CoH + 0.4f, CoW));
        var profile = RoomProfile("Corridor", 7.6f, 0.02f, 0.05f, new Color(0.92f, 0.86f, 0.76f), 22f, new Color(0.05f, 0.045f, 0.035f), 5f, 2f);
        {
            // 끝없는 복도에서 플레이어를 옮길 때 모션 블러가 번지면 들킨다.
            var mb = Persist(profile, profile.Add<MotionBlur>(true));
            mb.intensity.Override(0f);
            RoomChromatic(profile, VhsChromaticUncanny);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
        }
        LocalVolume(gen, "LocalVolume", room.center, room.size, 0.5f, 125f, profile);

        // ---- 끝(풍선 + 마지막 문 + 문 안 불빛). LoopCorridor가 플레이어 앞 CoAhead에 둔다. 로컬 +x = 문 너머.
        var end = new GameObject("End").transform;
        end.SetParent(gen, true);
        end.SetPositionAndRotation(new Vector3(CoX0 + 2f + CoAhead, 0f, CoZ), Quaternion.identity);
        Vector3 E(float x, float y, float z) => end.TransformPoint(new Vector3(x, y, z));
        const float dw = 1.0f, dh = 2.1f;
        Box(end, "Wall_L", E(0.1f, CoH / 2, -(CoW / 2 + dw / 2) / 2 - 0.0f), new Vector3(0.2f, CoH, CoW / 2 - dw / 2), paper, LCorr, true);
        Box(end, "Wall_R", E(0.1f, CoH / 2, (CoW / 2 + dw / 2) / 2), new Vector3(0.2f, CoH, CoW / 2 - dw / 2), paper, LCorr, true);
        Box(end, "Wall_T", E(0.1f, (dh + CoH) / 2, 0f), new Vector3(0.2f, CoH - dh, dw), paper, LCorr, true);
        Box(end, "Wainscot_L", E(-0.02f, 0.45f, -(CoW / 2 + dw / 2) / 2), new Vector3(0.04f, 0.9f, CoW / 2 - dw / 2), wood, LCorr);
        Box(end, "Wainscot_R", E(-0.02f, 0.45f, (CoW / 2 + dw / 2) / 2), new Vector3(0.04f, 0.9f, CoW / 2 - dw / 2), wood, LCorr);
        Box(end, "Frame_L", E(-0.03f, 1.1f, -0.56f), new Vector3(0.06f, 2.2f, 0.1f), frameMat, LCorr);
        Box(end, "Frame_R", E(-0.03f, 1.1f, 0.56f), new Vector3(0.06f, 2.2f, 0.1f), frameMat, LCorr);
        Box(end, "Frame_T", E(-0.03f, 2.15f, 0f), new Vector3(0.06f, 0.1f, 1.22f), frameMat, LCorr);
        // 문 안: 우리 집 불빛
        float tz0 = 0.25f, tz1 = 1.7f, tl = tz1 - tz0, tc = (tz0 + tz1) / 2;
        Box(end, "In_Floor", E(tc, 0.01f, 0f), new Vector3(tl, 0.02f, dw), homeLight, LCorr, true);
        Box(end, "In_Back", E(tz1, dh / 2, 0f), new Vector3(0.02f, dh, dw), homeLight, LCorr);
        Box(end, "In_Top", E(tc, dh - 0.01f, 0f), new Vector3(tl, 0.02f, dw), homeLight, LCorr);
        Box(end, "In_L", E(tc, dh / 2, -dw / 2 + 0.01f), new Vector3(tl, dh, 0.02f), homeLight, LCorr);
        Box(end, "In_R", E(tc, dh / 2, dw / 2 - 0.01f), new Vector3(tl, dh, 0.02f), homeLight, LCorr);
        // 위 벽등과 그 빛(끝이 멀리서도 밝게 보이게)
        Box(end, "Lamp", E(-0.06f, 2.35f, 0f), new Vector3(0.12f, 0.12f, 0.3f), homeLight, LCorr);
        MakeLight(end, "EndLight", LightType.Point, E(-0.7f, 2.1f, 0f), Vector3.zero, new Color(1f, 0.8f, 0.55f), 22000f, 8f, LCorr, false, false);
        // 스마일 풍선(문 옆)
        var bal = new GameObject("SmileBalloon").transform;
        bal.SetParent(end, true); bal.position = E(-0.55f, 1.75f, -0.95f);
        Prim(PrimitiveType.Sphere, bal, "Ball", E(-0.55f, 1.75f, -0.95f), new Vector3(0.56f, 0.6f, 0.56f), balloonMat, LCorr);
        Prim(PrimitiveType.Sphere, bal, "Eye_L", E(-0.82f, 1.84f, -1.03f), new Vector3(0.04f, 0.08f, 0.04f), black, LCorr);
        Prim(PrimitiveType.Sphere, bal, "Eye_R", E(-0.82f, 1.84f, -0.87f), new Vector3(0.04f, 0.08f, 0.04f), black, LCorr);
        for (int i = 0; i < 7; i++)
        {
            float a = Mathf.Lerp(-0.9f, 0.9f, i / 6f);
            Box(bal, "Mouth", E(-0.83f + Mathf.Cos(a) * 0.0f, 1.66f - Mathf.Cos(a) * 0.06f, -0.95f + Mathf.Sin(a) * 0.13f), new Vector3(0.02f, 0.025f, 0.04f), black, LCorr);
        }
        Box(bal, "Knot", E(-0.55f, 1.43f, -0.95f), new Vector3(0.05f, 0.05f, 0.05f), balloonMat, LCorr);
        Box(bal, "String", E(-0.55f, 1.0f, -0.95f), new Vector3(0.008f, 0.86f, 0.008f), plate, LCorr);
        // 2026-11-23 VARCO 스마일 풍선(원점 = 줄 끝, 얼굴 +Z, 풍선 가운데 1.23m)이 있으면 더미를 지우고 바꾼다. 얼굴은 더미 눈이 보던 쪽.
        if (VarcoPrefab("FloralCorridor", "SmileBalloon") != null)
        {
            for (int i = bal.childCount - 1; i >= 0; i--) Object.DestroyImmediate(bal.GetChild(i).gameObject);
            var face = E(-1f, 0f, 0f) - E(0f, 0f, 0f);
            VarcoInto(bal, "FloralCorridor", "SmileBalloon", LCorr, new Vector3(0f, -1.23f, 0f), new Vector3(0f, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 0f), false);
        }

        // 마지막 문 = 출구 벽(찾기 규칙). 끝 묶음 아래에 두어 같이 움직인다.
        var startLeafPath = "GB_02c_Corridor/GFX_Generated/Start/Leaf";
        var gate = FindGate(end, "FindGate_Corridor", 5, "CorridorPuddle",
            new Bounds(E(0.1f, dh / 2, 0f), new Vector3(0.2f, dh, dw)), startLeafPath);
        Box(gate.wall.transform, "Handle", E(-0.04f, 1.0f, 0.36f), new Vector3(0.05f, 0.04f, 0.14f), chrome, LCorr);
        Box(gate.wall.transform, "Peephole", E(-0.01f, 1.5f, 0f), new Vector3(0.01f, 0.04f, 0.04f), chrome, LCorr);

        // ---- 이상한 점: 카펫 위 물웅덩이(반복 구간 안). 위에 떠 있는 풍선이 비치는데 위에는 아무것도 없다.
        var pud = new GameObject("Oddity_PuddleReflection").transform;
        pud.SetParent(gen, true);
        pud.position = new Vector3(CoX0 + 30f, 0f, CoZ + 0.35f);
        var water = Prim(PrimitiveType.Cylinder, pud, "Water", pud.position + new Vector3(0f, 0.004f, 0f), new Vector3(1.1f, 0.004f, 0.8f), puddleMat, LCorr);
        var refl = Prim(PrimitiveType.Quad, pud, "Reflection", pud.position + new Vector3(0f, 0.01f, 0f), new Vector3(0.8f, 0.8f, 1f), reflMat, LCorr, false, new Vector3(90f, 90f, 0f));
        var gz = AddGaze(pud.gameObject, "CorridorPuddle", gate, room);
        gz.gazeRenderers = new[] { water.GetComponent<Renderer>() };
        gz.maxDistance = 4f; gz.dwellTime = 1.5f; gz.boundsPadding = 0.15f;
        gz.blinkRenderers = new[] { refl.GetComponent<Renderer>() };
        gz.blinkCount = 2; gz.blinkOff = 0.15f;
        gz.hideOnFound = new[] { refl };
        EditorUtility.SetDirty(gz);

        // ---- 순간이동 두 개: 노란 방 출구 통로 → 복도 시작, 마지막 문 안 → 거실 서쪽 벽
        // 노란 방 통로 안쪽은 검은 막으로 가려 거실이 보이지 않게 한다(거실 쪽에서는 통로 막기 판이 덮는다).
        var veil = Box(gen, "YellowPassageVeil", new Vector3(38.3f, 1.2f, LvInZ), new Vector3(0.02f, 2.4f, 1.8f), black, LYellow | LLiving);
        var inFrom = new GameObject("YellowPassageAnchor").transform;
        inFrom.SetParent(gen, true);
        inFrom.SetPositionAndRotation(new Vector3(36.45f, 0f, LvInZ), Quaternion.LookRotation(Vector3.right));
        var inTo = new GameObject("CorridorStartAnchor").transform;
        inTo.SetParent(gen, true);
        inTo.SetPositionAndRotation(new Vector3(CoX0, 0f, CoZ), Quaternion.LookRotation(Vector3.right));
        var tzIn = new GameObject("Teleport_YellowToCorridor").AddComponent<TeleportZone>();
        tzIn.transform.SetParent(gen, true);
        tzIn.zone = new Bounds(new Vector3(37.55f, 1.2f, LvInZ), new Vector3(1.5f, 2.4f, 1.8f));
        tzIn.fromAnchor = inFrom; tzIn.toAnchor = inTo;
        EditorUtility.SetDirty(tzIn);

        var outFrom = new GameObject("EndDoorAnchor").transform;
        outFrom.SetParent(end, true);
        outFrom.SetPositionAndRotation(E(0.1f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
        var outTo = new GameObject("LivingArrivalAnchor").transform;
        outTo.SetParent(gen, true);
        outTo.SetPositionAndRotation(new Vector3(LvX0, 0f, LvInZ), Quaternion.LookRotation(Vector3.right));
        // 거실에 잠깐 남는 것: 방금 지나온 닫힌 현관문(통로 구멍을 가린다). 멀어지고 화면 밖일 때 사라진다.
        var arrival = new GameObject("DoorArrival").transform;
        arrival.SetParent(outTo, false);
        var rotL = outTo.rotation.eulerAngles;
        Vector3 L(float x, float y, float z) => outTo.TransformPoint(new Vector3(x, y, z));
        Box(arrival, "Leaf", L(0f, 1.2f, 0.03f), new Vector3(1.8f, 2.4f, 0.04f), doorMat, LLiving, false, rotL);
        Box(arrival, "Frame_L", L(-0.95f, 1.25f, 0.05f), new Vector3(0.1f, 2.5f, 0.06f), frameMat, LLiving, false, rotL);
        Box(arrival, "Frame_R", L(0.95f, 1.25f, 0.05f), new Vector3(0.1f, 2.5f, 0.06f), frameMat, LLiving, false, rotL);
        Box(arrival, "Frame_T", L(0f, 2.45f, 0.05f), new Vector3(2f, 0.1f, 0.06f), frameMat, LLiving, false, rotL);
        Box(arrival, "Handle", L(0.7f, 1.0f, 0.08f), new Vector3(0.14f, 0.04f, 0.05f), chrome, LLiving, false, rotL);
        arrival.gameObject.SetActive(false);
        var tzOut = end.gameObject.AddComponent<TeleportZone>();
        var zoneLocal = new Bounds(new Vector3(tc + 0.15f, dh / 2, 0f), new Vector3(tl - 0.3f, dh, dw));
        tzOut.zone = new Bounds(end.TransformPoint(zoneLocal.center), zoneLocal.size);
        tzOut.fromAnchor = outFrom; tzOut.toAnchor = outTo; tzOut.arrivalObject = arrival.gameObject;
        EditorUtility.SetDirty(tzOut);

        // ---- 끝없는 복도
        var loop = gen.gameObject.AddComponent<LoopCorridor>();
        loop.gaze = gz; loop.corridor = room; loop.axis = Vector3.right; loop.origin = new Vector3(CoX0, 0f, CoZ);
        loop.loopAt = CoLoopAt; loop.segment = CoSeg; loop.endGroup = end; loop.endAhead = CoAhead;
        loop.exitGate = gate; loop.exitGateLocal = new Bounds(new Vector3(0.1f, dh / 2, 0f), new Vector3(0.2f, dh, dw));
        loop.exitZone = tzOut; loop.exitZoneLocal = zoneLocal;
        EditorUtility.SetDirty(loop);

        // 개발용 방에서 시작(F6 꽃무늬 복도)의 시작 자리
        var spawn = new GameObject("Spawn_CorridorArrival").transform;
        spawn.SetParent(gen, true);
        spawn.SetPositionAndRotation(new Vector3(CoX0 + 2f, 0.05f, CoZ), Quaternion.LookRotation(Vector3.right));
    }

    // 바랜 꽃무늬 벽지: 크림·분홍 바탕에 옅은 세로줄, 25cm마다 엇갈린 장미 송이와 잎. 1m에 한 장(복도 되풀이 길이 20m를 나눈다).
    static (Texture2D, Texture2D, Texture2D) FloralWallpaperSet()
    {
        var bg = new Color(0.9f, 0.82f, 0.78f);
        var stripe = new Color(0.86f, 0.77f, 0.74f);
        var rose = new Color(0.72f, 0.42f, 0.45f);
        var core = new Color(0.86f, 0.68f, 0.44f);
        var leaf = new Color(0.47f, 0.56f, 0.42f);
        return MakeSet("GB_Corr_Wallpaper", 512, 1.5f, (float u, float v, out Color a, out float h, out Color mk) =>
        {
            a = Mathf.Repeat(u * 8f, 1f) < 0.12f ? stripe : bg; h = 0f;
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                {
                    float fx = (col + (row % 2) * 0.5f + 0.5f) / 4f, fy = (row + 0.5f) / 4f;
                    float dx = Mathf.Repeat(u - fx + 0.5f, 1f) - 0.5f, dy = Mathf.Repeat(v - fy + 0.5f, 1f) - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                    float petal = 0.04f * (1f + 0.35f * Mathf.Cos(5f * th + row));
                    if (r < petal) { a = r < 0.012f ? core : Color.Lerp(rose, rose * 0.85f, r / petal); h = 0.6f; }
                    // 잎: 송이 아래 비스듬한 타원
                    float lx = dx - 0.045f, ly = dy + 0.035f;
                    if ((lx * lx) / (0.03f * 0.03f) + (ly * ly) / (0.012f * 0.012f) < 1f) { a = leaf; h = 0.4f; }
                }
            mk = new Color(0f, 1f, 0f, 0.22f);
        });
    }

    // 낡은 카펫: 갈색 바탕에 어두운 마름모 무늬. 1m에 한 장.
    static (Texture2D, Texture2D, Texture2D) CorridorCarpetSet()
    {
        var bg = new Color(0.34f, 0.28f, 0.22f);
        var line = new Color(0.24f, 0.2f, 0.16f);
        var dot = new Color(0.46f, 0.36f, 0.24f);
        return MakeSet("GB_Corr_Carpet", 512, 2f, (float u, float v, out Color a, out float h, out Color mk) =>
        {
            float du = Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f), dv = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f);
            float dia = du + dv;
            a = Mathf.Abs(dia - 0.45f) < 0.04f ? line : (dia < 0.1f ? dot : bg);
            float n = Mathf.PerlinNoise(u * 60f, v * 60f);
            a *= 0.9f + 0.2f * n;
            h = n * 0.5f;
            mk = new Color(0f, 1f, 0f, 0.05f);
        });
    }

    // 웅덩이에 비친 스마일 풍선(투명 바탕). 위쪽이 풍선, 아래로 줄이 내려온다. 조금 흐리게.
    static Texture2D PuddleReflectionTexture()
    {
        const int n = 256;
        var px = new Color[n * n];
        var yellow = new Color(0.95f, 0.82f, 0.2f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                Color c = new Color(0f, 0f, 0f, 0f);
                float dx = u - 0.5f, dy = v - 0.64f, r = Mathf.Sqrt(dx * dx + dy * dy);
                // 웅덩이 둘레로 갈수록 옅어진다
                float edge = Mathf.Clamp01(1f - Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) / 0.5f);
                if (r < 0.2f)
                {
                    c = yellow; c.a = Mathf.Clamp01((0.2f - r) / 0.02f) * 0.85f;
                    // 눈, 웃는 입
                    bool eye = (Mathf.Pow((u - 0.43f) / 0.02f, 2) + Mathf.Pow((v - 0.69f) / 0.035f, 2) < 1f) || (Mathf.Pow((u - 0.57f) / 0.02f, 2) + Mathf.Pow((v - 0.69f) / 0.035f, 2) < 1f);
                    float mr = Mathf.Sqrt(dx * dx + (v - 0.66f) * (v - 0.66f));
                    bool mouth = Mathf.Abs(mr - 0.1f) < 0.012f && v < 0.62f;
                    if (eye || mouth) c = new Color(0.08f, 0.07f, 0.04f, c.a);
                }
                else if (Mathf.Abs(u - 0.5f + (0.44f - v) * 0.05f) < 0.004f && v < 0.44f && v > 0.08f)
                    c = new Color(0.3f, 0.3f, 0.3f, 0.6f);
                c.a *= edge * 1.4f;
                px[y * n + x] = c;
            }
        return Save("GB_Corr_PuddleReflection", n, px, TexKind.Alpha, 0f, true);
    }
}
