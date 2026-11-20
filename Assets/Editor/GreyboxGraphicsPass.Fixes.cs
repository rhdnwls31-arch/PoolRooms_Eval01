using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 2026-11-17 플레이 피드백 버그 수정(카메라 최소 거리, 아치 위 모서리 구멍).
public static partial class GreyboxGraphicsPass
{
    // 2026-11-19 사용자 피드백 "계단이랑 물 닿았을 때 깨진다 / 움직일 때마다 보인다": 원인은 물이 아니라 같은 면에 겹친 두 상자였다.
    // 수영장 벽(타일)과 데크 띠(데크 돌)가 윗면(y 0)도, 물 쪽 안쪽 면도 한 평면에 겹쳐 있어서, 보는 각도마다 둘이 번갈아 그려졌다(z-fighting).
    // 계단 자리처럼 턱이 없는 곳에서는 데크 끝에 40cm 띠로, 맞은편 벽에서는 물 바로 위 띠로 보였다.
    // 벽을 물 쪽으로 3mm, 아래로 3mm 옮겨 안쪽 면은 벽 타일이, 윗면은 데크가 이기게 한다(타일 UV가 구워진 메쉬는 크기를 바꾸지 않는다).
    // 위치는 지금 값을 1cm 단위로 반올림한 자리에서 3mm라 여러 번 돌려도 같다. 인트로·라군·수로·홀 수조·같은 수영장(복사본) 모두.
    static void ApplyPoolWallNudge()
    {
        const float nudge = 0.003f;
        var waters = Object.FindObjectsByType<UnityEngine.Rendering.HighDefinition.WaterSurface>(FindObjectsInactive.Include);
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            string n = r.name;
            if (!(n.StartsWith("Pool_Wall_") || n.StartsWith("Lagoon_Wall_") || n.StartsWith("Channel_Wall_") || n.StartsWith("Basin_Wall_"))) continue;
            var b = r.bounds;
            // 이 벽이 둘러싼 물: 벽 가운데가 물 범위(1m 넓힌) 안에 있는 것
            UnityEngine.Rendering.HighDefinition.WaterSurface w = null;
            foreach (var ws in waters)
            {
                if (!ws.name.EndsWith("_HDRP")) continue;
                var s = ws.transform.lossyScale; var c = ws.transform.position;
                if (Mathf.Abs(b.center.x - c.x) < s.x / 2 + 1f && Mathf.Abs(b.center.z - c.z) < s.z / 2 + 1f && Mathf.Abs(b.max.y - c.y) < 1f) { w = ws; break; }
            }
            if (w == null) continue;
            var wc = w.transform.position;
            var t = r.transform;
            Vector3 p = t.position;
            // 윗면: 1cm 반올림 자리보다 3mm 아래
            float top = Mathf.Round(b.max.y * 100f) / 100f - nudge;
            p.y += top - b.max.y;
            // 안쪽 면: 얇은 축에서 물 쪽 면을 1cm 반올림 자리보다 3mm 물 쪽으로
            bool thinX = b.size.x < b.size.z;
            if (thinX)
            {
                float dir = Mathf.Sign(wc.x - b.center.x);
                float inner = dir > 0 ? b.max.x : b.min.x;
                float target = Mathf.Round(inner * 100f) / 100f + dir * nudge;
                p.x += target - inner;
            }
            else
            {
                float dir = Mathf.Sign(wc.z - b.center.z);
                float inner = dir > 0 ? b.max.z : b.min.z;
                float target = Mathf.Round(inner * 100f) / 100f + dir * nudge;
                p.z += target - inner;
            }
            if ((p - t.position).sqrMagnitude > 1e-10f) { t.position = p; EditorUtility.SetDirty(t); }
        }
    }

    /// <summary>
    /// 소품 바닥 붙이기(2026-11-20). 인트로 소품은 IntroMapBuilder가 한 번 만든 것이라 여기서 바로잡는다(인트로·같은 수영장 둘 다).
    /// 경계 상자 기준이라 몇 번을 돌려도 결과가 같다.
    /// </summary>
    static void ApplyPropGrounding()
    {
        Renderer[] All(Transform t) => t.GetComponentsInChildren<Renderer>(false);
        float MinY(Transform t) { float y = float.MaxValue; foreach (var r in All(t)) y = Mathf.Min(y, r.bounds.min.y); return y; }
        // 아래에 있는 메시(자기 자신 제외)의 윗면 높이. 선베드 콜라이더는 등받이까지 덮는 큰 상자라 콜라이더가 아니라 렌더러로 본다.
        var allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
        float SurfaceBelow(Transform t, Bounds b)
        {
            float y = float.NaN;
            foreach (var r in allRenderers)
            {
                if (!r.enabled || r.transform.IsChildOf(t)) continue;
                var rb = r.bounds;
                if (b.center.x < rb.min.x || b.center.x > rb.max.x || b.center.z < rb.min.z || b.center.z > rb.max.z) continue;
                if (rb.max.y > b.min.y + 0.2f || rb.size.y > 3f) continue; // 소품보다 위로 솟은 것(등받이)·큰 구조물 제외
                if (float.IsNaN(y) || rb.max.y > y) y = rb.max.y;
            }
            return y;
        }
        // 라디오는 옆 탁자 상판에 12cm 묻혀 있었고(모델 피벗이 가운데), 수건은 선베드 위로 3cm 떠 있었다.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.parent == null || t.parent.name != "Dummies") continue;
            if (t.name != "Prop_Radio" && !t.name.StartsWith("Prop_Towels")) continue;
            var rs = All(t); if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            // 선베드 등받이에 맞지 않도록 소품 발자국 가운데 아래 표면만 본다
            float top = SurfaceBelow(t, b);
            if (float.IsNaN(top)) continue;
            float dy = top - MinY(t);
            if (Mathf.Abs(dy) > 0.001f && Mathf.Abs(dy) < 0.3f) { t.position += Vector3.up * dy; EditorUtility.SetDirty(t); }
        }
        // 물 위 빛 구슬 중 계단에 박힌 것: 계단이 깊어지는 쪽으로 계단에서 비켜날 때까지 옮긴다.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.parent == null || t.parent.name != "Dummies" || !(t.name.Contains("_Orb_") || t.name.StartsWith("Prop_GlowOrb"))) continue;
            if (!t.TryGetComponent<Renderer>(out var orb)) continue;
            Vector3 dir = Vector3.zero;
            for (int i = 0; i < 60; i++)
            {
                var b = orb.bounds; float rad = b.extents.x;
                Collider hit = null;
                foreach (var c in Physics.OverlapSphere(b.center, rad, ~0, QueryTriggerInteraction.Ignore))
                    if (c.name.StartsWith("Step_") && c.bounds.max.y > b.min.y + 0.01f) { hit = c; break; }
                if (hit == null) break;
                if (dir == Vector3.zero)
                {
                    // 계단은 한 축으로 늘어서 있다: 짧은 축(디딤판 깊이) 방향 중 더 낮은 계단 쪽
                    var hb = hit.bounds; bool alongX = hb.size.x < hb.size.z;
                    var probe = hb.center + (alongX ? Vector3.right : Vector3.forward) * (alongX ? hb.size.x : hb.size.z);
                    float yNext = float.NaN;
                    foreach (var c in Physics.OverlapSphere(probe, 0.05f, ~0, QueryTriggerInteraction.Ignore)) if (c.name.StartsWith("Step_")) yNext = c.bounds.max.y;
                    var axis = alongX ? Vector3.right : Vector3.forward;
                    dir = (!float.IsNaN(yNext) && yNext < hb.max.y) ? axis : -axis;
                }
                t.position += dir * 0.05f;
                EditorUtility.SetDirty(t);
            }
        }
    }

    static void ApplyPlaytestFixes()
    {
        ApplyPoolWallNudge();
        ApplyPropGrounding();
        // 4번: 카메라가 0.3m 안쪽을 그리지 않는데 플레이어 반지름도 0.3m라, 벽에 붙으면 벽이 잘려 밖이 보였다.
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (cam.GetComponentInParent<CharacterController>(true) != null)
            {
                cam.nearClipPlane = 0.05f;
                EditorUtility.SetDirty(cam);
            }

        // 7번: 천사 방 → 같은 수영장 출구는 3×4.3m 네모 구멍인데 앞에 아치 발광판만 있어, 아치 위 양쪽 모서리로 통로가 보였다.
        // 같은 수영장 북쪽 벽 두께(z -145.3 ~ -144.7)만큼 아치 바깥 모서리를 벽 재질로 채운다.
        PlaceSameIntro(); // 2026-11-18: 하늘 계단 구역을 넣으려고 같은 수영장을 뒤·위로 옮긴다(Layout.cs)
        var root = Root("GB_05_SameIntro");
        var gen = Fresh(root.transform, GenName);
        var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Env/Env_Court_Wall.mat");
        ArchSpandrels(gen, "ExitArch_Spandrels", new Vector3(28.15f, 0f, -145.3f) + SameOffset, -144.7f + SameOffset.z, 3f, 2.8f, wallMat, LOutdoor);

        ApplySkyStairs();
        ApplyHallEntryRoof();
        ApplyHeights();
        FlattenThresholds();
        ApplyNewRooms(); // 2026-11-18 잠긴 샤워실·밤의 거실, 예전 길 막기(NewRooms.cs). 통로 막기보다 먼저 벽에 구멍을 낸다.
        ApplySeals();
        ApplyIntroExitCue();
        ApplyFindRooms(); // 2026-11-18 찾기 규칙(기획서 §4-1): 이상한 점을 응시해야 출구가 열린다(Find.cs)
        PatchDoorwayTrims(); // 2026-11-18 문 자리를 채우는 판마다 끊긴 걸레받이를 잇는다(Trims.cs). 항상 마지막에 둔다.
    }

    // ---------------------------------------------------------------- 1번: 인트로 출구 안내
    // 인트로 마지막 방 북쪽 벽에 똑같이 빛나는 아치가 3개 있고 가운데만 진짜 출구라, 어디로 가야 할지 알 수 없었다.
    // 가운데 아치의 발광판을 끄면 그 자리만 뚫려 보이고, 너머의 파란 타일 방이 보인다. 통로 안에 차가운 빛을 둔다.
    static void ApplyIntroExitCue()
    {
        Transform arch = null;
        foreach (var t in Root("Environment").GetComponentsInChildren<Transform>(true))
            if (t.name == "Goal_ArchN2") { arch = t; break; }
        if (arch == null) throw new System.Exception("Goal_ArchN2 없음");
        arch.gameObject.SetActive(false);

        var gen = Fresh(Root("GB_00_IntroExit").transform, GenName);
        // 통로 안(바닥 위 2m)에서 양쪽으로 퍼지는 차가운 빛: 마지막 방에서 보면 문 안쪽이 은은하게 밝다.
        var glow = MakeLight(gen, "ExitPassageGlow", LightType.Point, new Vector3(0f, 2f, -70.9f), Vector3.zero,
            new Color(0.72f, 0.9f, 1f), 9000f, 6f, LOutdoor, false, false);
        ApplyIntroExitGate(glow); // 2026-11-18: 처음엔 막혀 있다가 옆 공간을 둘러보면 열린다(Layout.cs)

        // 2026-11-18 피드백: 흰 통로(폭 3m) 끝의 홀 벽 구멍이 2m라서, 남는 0.5m씩 홀의 타일 벽 겉면이 기둥처럼 보였다.
        // 사용자 결정(2026-11-18): 홀 구멍을 통로와 같은 3m로 넓힌다. 홀 입구 북쪽 벽 양쪽을 0.5m씩 줄인다(콜라이더도 같이 줄어든다).
        // GB_01_PoolHall은 180도 돌아 있어서 로컬 x가 월드 x와 반대다. 로컬 -6~-1.5 / 1.5~6 이 남는 벽이다.
        var hall = Root("GB_01_PoolHall").transform;
        // 2026-11-18 2차: 통로 벽 안쪽 면(x ±1.5)과 홀 벽 끝면이 같은 자리라 겹친 10cm에서 깨져 보였다. 홀 쪽을 1cm씩 더 물린다(구멍 3.02m).
        foreach (var (n, x) in new[] { ("Entry_Wall_North_L", -3.755f), ("Entry_Wall_North_R", 3.755f) })
        {
            var w = hall.Find(n);
            w.localPosition = new Vector3(x, 2f, -6f); w.localScale = new Vector3(4.49f, 4f, 0.3f);
        }
        var top = hall.Find("Entry_Wall_North_Top");
        top.localPosition = new Vector3(0f, 3.5f, -6f); top.localScale = new Vector3(3.02f, 1f, 0.3f);
        // 통로 바닥: 홀 입구 바닥과 같은 타일(월드 좌표 Triplanar라 줄눈이 이어진다).
        // 문턱 표시(어두운 판)는 마당 바닥 끝(z -70)과 통로 바닥 사이 20cm 틈을 덮고 있어서 끄지 않고 같은 타일로 칠한다.
        var hallTile = hall.Find("Entry_Floor").GetComponent<MeshRenderer>().sharedMaterial;
        Find("GB_00_IntroExit/Passage_To_Hall/Floor").GetComponent<MeshRenderer>().sharedMaterial = hallTile;
        // 문턱 표시는 바닥보다 2cm 솟아 들떠 보였다(2026-11-18 2차). 끄고, 마당 바닥 끝(z -70)부터 홀 바닥(z -71.7)까지
        // 바닥과 같은 높이의 타일 판 하나로 틈을 메운다. 같은 재질(월드 Triplanar)이라 겹친 곳도 같은 무늬다.
        Find("GB_00_IntroExit/DoorSlot_Marker").gameObject.SetActive(false);
        Box(gen, "PassageFloorFill", new Vector3(0f, -0.15f, -70.875f), new Vector3(3f, 0.3f, 1.75f), hallTile, LOutdoor);
    }

    // ---------------------------------------------------------------- 2·8번: 지나온 통로 막기
    // 통로마다 도착한 방의 벽과 같은 판을 숨겨 두고, 플레이어가 방 안으로 들어와 뒤를 보지 않을 때 켠다(SealBehindPassage).
    // 보이지 않는 벽은 더 일찍(판 면에서 0.45m) 켜져서, 들어오자마자 뒤돌아도 나갈 수 없다.
    // 트리거는 판 면에서 0.6m 떨어진 곳부터 3m 깊이다. 그보다 가까우면 보이지 않는 벽이 플레이어 몸(반지름 0.3m)과 겹친다.
    // 판은 벽 구멍을 정확히 채운다(벽면과 겹치지 않게). 재질·렌더링 레이어는 도착한 방의 벽에서 그대로 가져온다.
    static void ApplySeals()
    {
        // 인트로 → 수영장 홀 입구 방: 입구 방 북쪽 벽(z -71.85~-71.55)의 3×3m 구멍
        Seal("Seal_IntroToHall", "GB_01_PoolHall", "GB_01_PoolHall/Entry_Wall_North_L", Vector3.back,
            new Vector3(0f, 1.5f, -71.7f), new Vector3(3.02f, 3f, 0.3f), // 구멍을 3.02m로 넓힘(2026-11-18)
            new Vector3(0f, 1.5f, -73.9f), new Vector3(11f, 3f, 2.9f), null);

        // 홀 → 갈라진 복도: 홀 서쪽 벽(x 5.85~6.15) + 복도 덧판(~6.19)의 1.2×2.2m 구멍
        Seal("Seal_HallToCrack", "GB_02_Crack", "GB_02_Crack/GFX_Generated/WallCovers/West_A", Vector3.right,
            new Vector3(6.02f, 1.1f, -106.7f), new Vector3(0.34f, 2.2f, 1.2f),
            new Vector3(8.29f, 2f, -106.7f), new Vector3(3f, 4f, 5.8f), FindMarker("GB_02_Crack", -106.7f, 6.15f));

        // 2026-11-18: 복도 → 노란 방, 노란 방 → 천사 방 통로는 없앴다. 새 길(복도 → 샤워실 → 노란 방 → 거실 → 천사 방)의 막기는 NewRooms.cs.
        NewRoomSeals();

        // 천사 방 → 하늘 계단: 계단 구역 북쪽 하늘 벽의 3×4.3m 구멍. 판은 하늘 벽과 같은 면(두께 0.04)에 붙여 테두리가 드러나지 않게 한다.
        var angelToStairs = Seal("Seal_AngelToStairs", StairsRoot, StairsRoot + "/" + GenName + "/Shell/North_W", Vector3.back,
            new Vector3(28.15f, 2.15f, -144.82f), new Vector3(3f, 4.3f, 0.04f),
            new Vector3(28.15f, 2f, -146.9f), new Vector3(3f, 4f, 2.2f), null);
        // 계단에서 떨어지면 천사 방에서 다시 시작하므로, 그때 이 통로의 막힘을 풀어 준다.
        var fall = Find(StairsRoot + "/" + GenName + "/FallRespawn").GetComponent<FallRespawn>();
        fall.resetSeals = new[] { angelToStairs };
        EditorUtility.SetDirty(fall);

        // 하늘 계단 → 같은 수영장: 북쪽 벽(옮기기 전 z -145.3~-144.7)의 3×4.3m 구멍. 앞면을 2cm 들여 아치 모서리 덩어리와 겹치지 않게 한다.
        // 아치 발광판(Goal_ArchN2)은 그대로라, 막히면 양옆 막힌 아치(N1, N3)와 똑같아 보인다.
        Seal("Seal_StairsToSame", "GB_05_SameIntro", "GB_05_SameIntro/Goal_Wall_North_0", Vector3.back,
            new Vector3(28.15f, 2.15f, -144.99f) + SameOffset, new Vector3(3f, 4.3f, 0.58f),
            new Vector3(28.15f, 2f, -147.38f) + SameOffset, new Vector3(22f, 4f, 3f), null);
    }

    static SealBehindPassage Seal(string name, string roomName, string refWallPath, Vector3 arrivalDir,
        Vector3 plugCenter, Vector3 plugSize, Vector3 triggerCenter, Vector3 triggerSize, GameObject marker)
    {
        var gen = Root(roomName).transform.Find(GenName);
        var refWall = Find(refWallPath).GetComponent<MeshRenderer>();

        var go = new GameObject(name);
        go.transform.SetParent(gen, true);
        go.transform.position = triggerCenter;
        go.layer = IgnoreRaycastLayer;
        var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = triggerSize;

        var plug = new GameObject("Plug");
        plug.transform.SetParent(go.transform, true);
        plug.transform.position = Vector3.zero;
        Box(plug.transform, "Wall", plugCenter, plugSize, refWall.sharedMaterial, refWall.renderingLayerMask);
        plug.SetActive(false);

        var blocker = new GameObject("InvisibleBlocker");
        blocker.transform.SetParent(go.transform, true);
        blocker.transform.position = plugCenter;
        blocker.AddComponent<BoxCollider>().size = plugSize;
        blocker.SetActive(false);

        var s = go.AddComponent<SealBehindPassage>();
        s.plug = plug;
        s.blocker = blocker;
        s.plugBounds = new Bounds(plugCenter, plugSize);
        s.arrivalDirection = arrivalDir;
        s.hideOnSeal = marker != null ? new[] { marker } : new GameObject[0];
        EditorUtility.SetDirty(s);
        return s;
    }

    static Transform Find(string path)
    {
        int slash = path.IndexOf('/');
        var t = Root(path.Substring(0, slash)).transform.Find(path.Substring(slash + 1));
        if (t == null) throw new System.Exception("오브젝트 없음: " + path);
        return t;
    }

    // 같은 이름의 문턱 표시가 방마다 여러 개라 위치로 고른다. z 또는 x가 맞는 것.
    static GameObject FindMarker(string roomName, float z, float x)
    {
        foreach (Transform t in Root(roomName).transform)
            if (t.name == "DoorSlot_Marker" && Mathf.Abs(t.position.z - z) < 0.05f && Mathf.Abs(t.position.x - x) < 0.05f)
            {
                t.gameObject.SetActive(true); // 다시 적용할 때를 위해 켜 둔 상태로 저장한다
                return t.gameObject;
            }
        throw new System.Exception($"문턱 표시 없음: {roomName} ({x}, {z})");
    }

    /// <summary>
    /// 폭 w 네모 구멍 위쪽 중, 반원 아치(시작 높이 hs, 반지름 w/2) 바깥 두 모서리를 채우는 덩어리.
    /// basePos: 구멍 아래 가운데, z는 벽 앞면. zBack: 벽 뒷면 z. 콜라이더 없음(머리 위라 걸리지 않는다).
    /// </summary>
    static void ArchSpandrels(Transform parent, string name, Vector3 basePos, float zBack, float w, float hs, Material m, uint layer)
    {
        const int seg = 16;
        float r = w / 2f, top = hs + r;
        var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
        float z0 = basePos.z, z1 = zBack;

        // 양면: 면마다 정점을 따로 두고 앞뒤 두 번 그린다(감는 방향 실수로 면이 사라지지 않게).
        void Face(Vector3[] ps, int[] order)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                int i = verts.Count;
                foreach (var p in ps) { verts.Add(p); uvs.Add(new Vector2(p.x + p.z, p.y) / 0.6f); }
                for (int t = 0; t < order.Length; t += 3)
                    if (pass == 0) tris.AddRange(new[] { i + order[t], i + order[t + 1], i + order[t + 2] });
                    else tris.AddRange(new[] { i + order[t], i + order[t + 2], i + order[t + 1] });
            }
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) => Face(new[] { a, b, c, d }, new[] { 0, 1, 2, 0, 2, 3 });
        void Tri(Vector3 a, Vector3 b, Vector3 c) => Face(new[] { a, b, c }, new[] { 0, 1, 2 });

        foreach (int side in new[] { -1, 1 })
        {
            // 아치 곡선: 옆 기둥 꼭대기(±r, hs) → 정점(0, top)
            var arc = new List<Vector2>();
            for (int k = 0; k <= seg; k++)
            {
                float a = Mathf.PI / 2f * k / seg;           // 0 → 90°
                arc.Add(new Vector2(side * r * Mathf.Cos(a), hs + r * Mathf.Sin(a)));
            }
            var corner = new Vector2(side * r, top);
            for (int k = 0; k < seg; k++)
            {
                Vector2 p = arc[k], q = arc[k + 1];
                // 앞면, 뒷면, 아치 안쪽 곡면
                Vector3 c0 = V(corner, z0), p0 = V(p, z0), q0 = V(q, z0);
                Vector3 c1 = V(corner, z1), p1 = V(p, z1), q1 = V(q, z1);
                Tri(c0, p0, q0); Tri(c1, p1, q1);
                Quad(p0, q0, q1, p1);
            }
        }

        Vector3 V(Vector2 p, float z) => new Vector3(basePos.x + p.x, basePos.y + p.y, z);

        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.renderingLayerMask = layer;
    }
}
