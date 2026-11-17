using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 2026-11-17 플레이 피드백 버그 수정(카메라 최소 거리, 아치 위 모서리 구멍).
public static partial class GreyboxGraphicsPass
{
    static void ApplyPlaytestFixes()
    {
        // 4번: 카메라가 0.3m 안쪽을 그리지 않는데 플레이어 반지름도 0.3m라, 벽에 붙으면 벽이 잘려 밖이 보였다.
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (cam.GetComponentInParent<CharacterController>(true) != null)
            {
                cam.nearClipPlane = 0.05f;
                EditorUtility.SetDirty(cam);
            }

        // 7번: 천사 방 → 같은 수영장 출구는 3×4.3m 네모 구멍인데 앞에 아치 발광판만 있어, 아치 위 양쪽 모서리로 통로가 보였다.
        // 같은 수영장 북쪽 벽 두께(z -145.3 ~ -144.7)만큼 아치 바깥 모서리를 벽 재질로 채운다.
        var root = Root("GB_05_SameIntro");
        var gen = Fresh(root.transform, GenName);
        var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Env/Env_Court_Wall.mat");
        ArchSpandrels(gen, "ExitArch_Spandrels", new Vector3(28.15f, 0f, -145.3f), -144.7f, 3f, 2.8f, wallMat, LOutdoor);

        ApplySeals();
        ApplyIntroExitCue();
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
        MakeLight(gen, "ExitPassageGlow", LightType.Point, new Vector3(0f, 2f, -70.9f), Vector3.zero,
            new Color(0.72f, 0.9f, 1f), 9000f, 6f, LOutdoor, false, false);
    }

    // ---------------------------------------------------------------- 2·8번: 지나온 통로 막기
    // 통로마다 도착한 방의 벽과 같은 판을 숨겨 두고, 플레이어가 방 안으로 들어와 뒤를 보지 않을 때 켠다(SealBehindPassage).
    // 보이지 않는 벽은 더 일찍(판 면에서 0.45m) 켜져서, 들어오자마자 뒤돌아도 나갈 수 없다.
    // 트리거는 판 면에서 0.6m 떨어진 곳부터 3m 깊이다. 그보다 가까우면 보이지 않는 벽이 플레이어 몸(반지름 0.3m)과 겹친다.
    // 판은 벽 구멍을 정확히 채운다(벽면과 겹치지 않게). 재질·렌더링 레이어는 도착한 방의 벽에서 그대로 가져온다.
    static void ApplySeals()
    {
        // 인트로 → 수영장 홀 입구 방: 입구 방 북쪽 벽(z -71.85~-71.55)의 2×3m 구멍
        Seal("Seal_IntroToHall", "GB_01_PoolHall", "GB_01_PoolHall/Entry_Wall_North_L", Vector3.back,
            new Vector3(0f, 1.5f, -71.7f), new Vector3(2f, 3f, 0.3f),
            new Vector3(0f, 1.5f, -73.9f), new Vector3(11f, 3f, 2.9f), null);

        // 홀 → 갈라진 복도: 홀 서쪽 벽(x 5.85~6.15) + 복도 덧판(~6.19)의 1.2×2.2m 구멍
        Seal("Seal_HallToCrack", "GB_02_Crack", "GB_02_Crack/GFX_Generated/WallCovers/West_A", Vector3.right,
            new Vector3(6.02f, 1.1f, -106.7f), new Vector3(0.34f, 2.2f, 1.2f),
            new Vector3(8.29f, 2f, -106.7f), new Vector3(3f, 4f, 5.8f), FindMarker("GB_02_Crack", -106.7f, 6.15f));

        // 복도 → 노란 방: 노란 방 서쪽 벽(x 19.85~20.15) + 벽지판(~20.20)의 2×2.6m 구멍, 걸레받이 한 토막
        var yellow = Seal("Seal_CrackToYellow", "GB_03_Yellow", "GB_03_Yellow/GFX_Generated/WallCovers/West_A", Vector3.right,
            new Vector3(20.025f, 1.3f, -106.7f), new Vector3(0.35f, 2.6f, 2f),
            new Vector3(22.3f, 1.5f, -106.55f), new Vector3(3f, 3f, 15f), FindMarker("GB_03_Yellow", -106.7f, 20.15f));
        var trim = Find("GB_03_Yellow/GFX_Generated/Baseboard/W_A").GetComponent<MeshRenderer>();
        Box(yellow.plug.transform, "Baseboard", new Vector3(20.22f, 0.05f, -106.7f), new Vector3(0.04f, 0.1f, 2f), trim.sharedMaterial, trim.renderingLayerMask);

        // 노란 방 → 천사 방: 천사 방 북쪽 벽(z -114.70~-114.40) + 노란 방 덧판(~-114.36)의 2×2.6m 구멍
        Seal("Seal_YellowToAngel", "GB_04_Angel", "GB_04_Angel/Wall_North_0", Vector3.back,
            new Vector3(28.15f, 1.3f, -114.53f), new Vector3(2f, 2.6f, 0.34f),
            new Vector3(28.15f, 2f, -116.8f), new Vector3(19.5f, 4f, 3f), FindMarker("GB_04_Angel", -114.7f, 28.15f));

        // 천사 방 → 같은 수영장: 북쪽 벽(z -145.3~-144.7)의 3×4.3m 구멍. 앞면을 2cm 들여 아치 모서리 덩어리와 겹치지 않게 한다.
        // 아치 발광판(Goal_ArchN2)은 그대로라, 막히면 양옆 막힌 아치(N1, N3)와 똑같아 보인다.
        Seal("Seal_AngelToSame", "GB_05_SameIntro", "GB_05_SameIntro/Goal_Wall_North_0", Vector3.back,
            new Vector3(28.15f, 2.15f, -144.99f), new Vector3(3f, 4.3f, 0.58f),
            new Vector3(28.15f, 2f, -147.38f), new Vector3(22f, 4f, 3f), null);
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
