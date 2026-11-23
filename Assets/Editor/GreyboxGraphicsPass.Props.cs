using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// 리미널 배경 소품(C)과 데칼(B-2, B-5), 물 위 발광 구체(D-2).
// 소품은 전부 프리미티브 더미이고 각 방의 Placeholder_Props/GFX_Props 아래에 둔다(나중에 VARCO 모델로 교체).
// "이상한 것"이 아니라 평범한 배경이다. 동선(문과 문 사이)은 비워 둔다.
public static partial class GreyboxGraphicsPass
{
    static Material pMetal, pWhite, pDark, pWood, pRed, pBlue, pGrey, pCanvas;

    static void PropMaterials()
    {
        pMetal = Lit("GB_Prop_LockerMetal", m => Plain(m, new Color(0.45f, 0.53f, 0.56f), 0.5f, 0.6f));
        pWhite = Lit("GB_Prop_White", m => Plain(m, new Color(0.86f, 0.86f, 0.82f), 0.35f));
        pDark = Lit("GB_Prop_Dark", m => Plain(m, new Color(0.03f, 0.03f, 0.035f), 0.85f));
        pWood = Lit("GB_Prop_Wood", m => Plain(m, new Color(0.36f, 0.25f, 0.14f), 0.3f));
        pRed = Lit("GB_Prop_Orange", m => Plain(m, new Color(0.85f, 0.3f, 0.12f), 0.4f));
        pBlue = Lit("GB_Prop_SignBlue", m => Plain(m, new Color(0.15f, 0.32f, 0.55f), 0.3f));
        pGrey = Lit("GB_Prop_Grey", m => Plain(m, new Color(0.42f, 0.42f, 0.4f), 0.3f));
        pCanvas = Lit("GB_Prop_Canvas", m => Plain(m, new Color(0.72f, 0.68f, 0.56f), 0.1f));
    }

    static uint propLayer;

    /// <summary>방의 Placeholder_Props(없으면 만든다) 아래 GFX_Props를 새로 만든다.</summary>
    static Transform PropsRoot(GameObject room, uint layer)
    {
        propLayer = layer;
        var ph = room.transform.Find("Placeholder_Props");
        if (ph == null) { ph = new GameObject("Placeholder_Props").transform; ph.SetParent(room.transform, false); }
        return Fresh(ph, PropsName);
    }

    /// <summary>pos에 yaw로 돌린 소품 그룹. 자식은 로컬 좌표(앞 = +Z)로 붙인다.</summary>
    static Transform PropGroup(Transform parent, string name, Vector3 pos, float yaw, Vector3? collider = null)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, true);
        g.position = pos; g.rotation = Quaternion.Euler(0, yaw, 0);
        if (collider.HasValue)
        {
            var bc = g.gameObject.AddComponent<BoxCollider>();
            bc.size = collider.Value; bc.center = new Vector3(0, collider.Value.y / 2, 0);
        }
        return g;
    }

    /// <summary>
    /// 2026-11-20 방 채우기 소품. VARCO 프리팹(Assets/VARCO/&lt;room&gt;/Prefabs/VAR_&lt;room&gt;_&lt;name&gt;)이 있으면 그것을, 없으면 dummy(로컬 좌표, 앞 = +Z)를 만든다.
    /// collider = 바닥 기준 상자 크기(null이면 콜라이더 없음). 렌더링 레이어는 propLayer.
    /// </summary>
    static Transform FillProp(Transform parent, string room, string name, Vector3 pos, float yaw, Vector3? collider, System.Action<Transform> dummy)
    {
        var g = PropGroup(parent, name, pos, yaw, collider);
        if (VarcoInto(g, room, name, propLayer, default, default, collider.HasValue) == null) dummy(g);
        return g;
    }

    static void Part(Transform g, PrimitiveType type, string name, Vector3 localCenter, Vector3 size, Material m, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(g, false);
        go.transform.localPosition = localCenter; go.transform.localScale = size;
        if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = m; r.renderingLayerMask = propLayer;
    }

    static void PBox(Transform g, string name, Vector3 c, Vector3 s, Material m) => Part(g, PrimitiveType.Cube, name, c, s, m);

    static void FoldingChair(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "FoldingChair", pos, yaw, new Vector3(0.45f, 0.85f, 0.45f));
        if (VarcoInto(g, "PoolHall", "FoldingChair", propLayer)) return;
        PBox(g, "Seat", new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.03f, 0.4f), pGrey);
        PBox(g, "Back", new Vector3(0, 0.74f, -0.2f), new Vector3(0.42f, 0.22f, 0.025f), pGrey);
        foreach (float x in new[] { -0.2f, 0.2f })
        {
            Part(g, PrimitiveType.Cube, "Leg", new Vector3(x, 0.42f, 0), new Vector3(0.02f, 0.95f, 0.02f), pMetal, new Vector3(22, 0, 0));
            Part(g, PrimitiveType.Cube, "Leg", new Vector3(x, 0.23f, 0), new Vector3(0.02f, 0.5f, 0.02f), pMetal, new Vector3(-28, 0, 0));
        }
    }

    static void Lockers(Transform parent, Vector3 pos, float yaw, int count)
    {
        const float w = 0.38f, h = 1.8f, d = 0.45f;
        var g = PropGroup(parent, "Lockers", pos, yaw, new Vector3(w * count, h, d));
        if (VarcoInto(g, "PoolHall", "Lockers", propLayer)) return; // VARCO 사물함은 4칸 한 덩어리(폭 1.38m)
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) / 2f) * w;
            PBox(g, "Body", new Vector3(x, h / 2, 0), new Vector3(w - 0.012f, h, d), pMetal);
            PBox(g, "Vent", new Vector3(x, h - 0.22f, d / 2 + 0.002f), new Vector3(w * 0.55f, 0.1f, 0.004f), pDark);
            PBox(g, "Handle", new Vector3(x + w * 0.3f, h * 0.52f, d / 2 + 0.01f), new Vector3(0.02f, 0.1f, 0.02f), pGrey);
        }
    }

    // 벽걸이 소품: pos는 벽면 위의 점, yaw는 벽에서 방 안쪽을 보는 방향.
    static void LifeRing(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "LifeRingHolder", pos, yaw);
        // 2026-11-23 사용자 피드백: 받침(걸이) 쪽이 방을 보고 있었다. 모델 앞뒤가 반대라 180도 돌리고, 원점(뒷면)이 벽에서 떨어지지 않게 두께만큼 앞으로.
        var lr = VarcoPrefab("PoolHall", "LifeRingHolder");
        if (lr != null)
        {
            float depth = 0f;
            foreach (var mr in lr.GetComponentsInChildren<MeshRenderer>())
            {
                var mfb = mr.GetComponent<MeshFilter>().sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mfb.center + Vector3.Scale(mfb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    depth = Mathf.Max(depth, lr.transform.InverseTransformPoint(mr.transform.TransformPoint(c)).z);
                }
            }
            VarcoInto(g, "PoolHall", "LifeRingHolder", propLayer, new Vector3(0f, 0f, depth), new Vector3(0f, 180f, 0f));
            return;
        }
        PBox(g, "Board", new Vector3(0, 0, 0.012f), new Vector3(0.8f, 0.8f, 0.024f), pWhite);
        Part(g, PrimitiveType.Cylinder, "Ring", new Vector3(0, 0, 0.07f), new Vector3(0.62f, 0.045f, 0.62f), pRed, new Vector3(90, 0, 0));
        Part(g, PrimitiveType.Cylinder, "RingHole", new Vector3(0, 0, 0.072f), new Vector3(0.34f, 0.046f, 0.34f), pWhite, new Vector3(90, 0, 0));
    }

    // VARCO 안전 수칙판은 바닥에 세우는 입간판이다. 바닥에 놓을 자리가 있으면(floorOk) 벽 앞 0.35m 바닥에 세운다.
    static void SignBoard(Transform parent, Vector3 pos, float yaw, bool floorOk = true)
    {
        if (floorOk && VarcoPrefab("PoolHall", "SafetySign_Blank") != null)
        {
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var fg = PropGroup(parent, "SafetySign_Blank", new Vector3(pos.x, 0f, pos.z) + fwd * 0.35f, yaw, new Vector3(0.6f, 0.85f, 0.48f));
            VarcoInto(fg, "PoolHall", "SafetySign_Blank", propLayer);
            return;
        }
        var g = PropGroup(parent, "SafetySign_Blank", pos, yaw);
        PBox(g, "Board", new Vector3(0, 0, 0.01f), new Vector3(0.6f, 0.8f, 0.02f), pWhite);
        PBox(g, "Header", new Vector3(0, 0.31f, 0.022f), new Vector3(0.6f, 0.16f, 0.004f), pBlue);
        for (int i = 0; i < 5; i++) // 글자 없는 줄
            PBox(g, "Line", new Vector3(-0.04f * (i % 2), 0.14f - i * 0.1f, 0.022f), new Vector3(0.44f - 0.08f * (i % 2), 0.025f, 0.004f), pGrey);
    }

    static void Clock(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "WallClock_NoHands", pos, yaw);
        if (VarcoInto(g, "PoolHall", "WallClock_NoHands", propLayer)) { g.localScale = Vector3.one * 1.5f; return; } // 2026-11-23 사용자 요청: 1.5배(원점이 뒷면이라 벽에 붙은 채 커진다)
        Part(g, PrimitiveType.Cylinder, "Rim", new Vector3(0, 0, 0.02f), new Vector3(0.4f, 0.02f, 0.4f), pDark, new Vector3(90, 0, 0));
        Part(g, PrimitiveType.Cylinder, "Face", new Vector3(0, 0, 0.03f), new Vector3(0.35f, 0.021f, 0.35f), pWhite, new Vector3(90, 0, 0));
    }

    static void Drain(Transform parent, Vector3 pos)
    {
        var g = PropGroup(parent, "DrainCover", pos, 0);
        PBox(g, "Frame", new Vector3(0, 0.004f, 0), new Vector3(0.32f, 0.008f, 0.32f), pGrey);
        for (int i = 0; i < 5; i++) PBox(g, "Slot", new Vector3(-0.1f + i * 0.05f, 0.009f, 0), new Vector3(0.022f, 0.004f, 0.26f), pDark);
    }

    static void Frame(Transform parent, Vector3 pos, float yaw, float w, float h, Texture2D img = null)
    {
        var g = PropGroup(parent, img != null ? "PictureFrame_Poster" : "PictureFrame_Blank", pos, yaw);
        PBox(g, "Frame", new Vector3(0, 0, 0.015f), new Vector3(w, h, 0.03f), pWood);
        if (img == null) { PBox(g, "Canvas", new Vector3(0, 0, 0.031f), new Vector3(w - 0.08f, h - 0.08f, 0.004f), pCanvas); return; }
        // 2026-11-23 VARCO 2D 포스터: 액자 앞(+Z)에 그림 판. Quad의 보이는 면(-Z)이 앞을 보게 180도.
        var mat = Lit("GB_Prop_Poster_" + img.name, m => { Plain(m, Color.white, 0.15f); m.SetTexture("_BaseColorMap", img); });
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = "Poster"; q.transform.SetParent(g, false);
        q.transform.localPosition = new Vector3(0, 0, 0.032f);
        q.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        q.transform.localScale = new Vector3(w - 0.08f, h - 0.08f, 1f);
        var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.renderingLayerMask = propLayer;
    }

    /// <summary>틀 없는 종이 포스터 한 장. pos = 벽 앞 그림 가운데, yaw = 방 안쪽을 보는 방향.</summary>
    static void Poster(Transform parent, Vector3 pos, float yaw, float w, float h, Texture2D img)
    {
        var g = PropGroup(parent, "Poster_" + img.name, pos, yaw);
        var mat = Lit("GB_Prop_Poster_" + img.name, m => { Plain(m, Color.white, 0.15f); m.SetTexture("_BaseColorMap", img); });
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = "Paper"; q.transform.SetParent(g, false);
        q.transform.localRotation = Quaternion.Euler(0, 180f, 0); // Quad의 보이는 면(-Z)이 방 안(+Z)을 보게
        q.transform.localScale = new Vector3(w, h, 1f);
        var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.renderingLayerMask = propLayer;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void DeskAndChair(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "EmptyDesk", pos, yaw, new Vector3(1.4f, 0.76f, 0.7f));
        // 2026-11-20 VARCO 사무용 책상(1.75×0.74×0.81)·회전의자(0.73×0.95×0.72). 책상 정면(+Z)이 의자 쪽,
        // 의자는 책상을 보게 180도. 책상이 깊어져 의자를 0.85m 앞에 둔다.
        if (VarcoPrefab("Yellow", "OfficeDesk") != null && VarcoPrefab("Yellow", "SwivelChair") != null)
        {
            VarcoInto(g, "Yellow", "OfficeDesk", propLayer);
            var vc = PropGroup(parent, "DeskChair", pos + g.forward * 0.85f, yaw + 180f, new Vector3(0.6f, 0.95f, 0.6f));
            VarcoInto(vc, "Yellow", "SwivelChair", propLayer, default, default, false);
            return;
        }
        PBox(g, "Top", new Vector3(0, 0.74f, 0), new Vector3(1.4f, 0.04f, 0.7f), pWood);
        PBox(g, "SideL", new Vector3(-0.67f, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.66f), pWood);
        PBox(g, "SideR", new Vector3(0.67f, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.66f), pWood);
        PBox(g, "Back", new Vector3(0, 0.45f, -0.32f), new Vector3(1.3f, 0.5f, 0.02f), pWood);
        var c = PropGroup(parent, "DeskChair", pos + g.forward * 0.75f, yaw + 180f, new Vector3(0.45f, 0.9f, 0.45f));
        PBox(c, "Seat", new Vector3(0, 0.46f, 0), new Vector3(0.44f, 0.06f, 0.44f), pGrey);
        PBox(c, "Back", new Vector3(0, 0.8f, -0.2f), new Vector3(0.42f, 0.4f, 0.04f), pGrey);
        Part(c, PrimitiveType.Cylinder, "Post", new Vector3(0, 0.22f, 0), new Vector3(0.05f, 0.22f, 0.05f), pMetal);
        Part(c, PrimitiveType.Cylinder, "Base", new Vector3(0, 0.015f, 0), new Vector3(0.5f, 0.015f, 0.5f), pMetal);
    }

    static void TvOnStand(Transform parent, Vector3 pos, float yaw)
    {
        var g = PropGroup(parent, "TV_Off", pos, yaw, new Vector3(1.1f, 1.15f, 0.45f));
        // 2026-11-20 VARCO 받침대 달린 브라운관 TV(한 덩어리, 1.07×1.1×0.78)가 있으면 그것 하나로.
        if (VarcoInto(g, "Yellow", "CRTTV_Stand", propLayer) != null) { TvStandTopBoard(g, propLayer); return; } // 2026-11-23 번진 받침 윗면을 윗판으로 덮는다
        if (VarcoPrefab("LivingRoom", "CRTTV") != null)
        {
            // 받침대(더미) 위에 VARCO 브라운관 TV. 콜라이더는 받침대 + TV 높이.
            PBox(g, "Stand", new Vector3(0, 0.25f, 0), new Vector3(1.1f, 0.5f, 0.7f), pWood);
            VarcoInto(g, "LivingRoom", "CRTTV", propLayer, new Vector3(0, 0.5f, 0), default, false);
            var tc = g.GetComponent<BoxCollider>(); tc.size = new Vector3(1.1f, 1.09f, 0.7f); tc.center = new Vector3(0, 0.545f, 0);
            return;
        }
        PBox(g, "Stand", new Vector3(0, 0.25f, 0), new Vector3(1.1f, 0.5f, 0.42f), pWood);
        PBox(g, "Body", new Vector3(0, 0.82f, 0), new Vector3(0.9f, 0.6f, 0.34f), pGrey);
        PBox(g, "Screen", new Vector3(0, 0.83f, 0.172f), new Vector3(0.76f, 0.48f, 0.004f), pDark);
    }

    // 2026-11-20 노란 방 채우기(넓은데 휑하다는 피드백). 모두 벽에 붙인다. 비워 두는 곳:
    // 입구(서쪽 z -101.5) → 삐에로(32, -109.5) → 출구(동쪽 z -112.4)의 대각선 동선, 삐에로 → 출구 문 위로 풍선이 날아가는 길.
    // 북쪽 벽: 책상 옆 서류함 2개, 북동쪽 정수기 / 남쪽 벽: 낡은 소파, 쌓인 의자, 남서쪽 모서리 상자 더미.
    static void YellowFill(Transform props)
    {
        var cardboard = Lit("GB_Prop_Cardboard", m => Plain(m, new Color(0.55f, 0.42f, 0.26f), 0.15f));
        var couch = Lit("GB_Prop_OfficeCouch", m => Plain(m, new Color(0.36f, 0.34f, 0.22f), 0.12f));
        var bottle = Lit("GB_Prop_WaterBottle", m => Plain(m, new Color(0.45f, 0.62f, 0.75f), 0.85f));

        foreach (float x in new[] { 25.0f, 25.55f })
            FillProp(props, "Yellow", "FilingCabinet", new Vector3(x, 0, -99.05f), 180, new Vector3(0.5f, 1.32f, 0.6f), g =>
            {
                PBox(g, "Body", new Vector3(0, 0.66f, 0), new Vector3(0.48f, 1.32f, 0.6f), pGrey);
                for (int i = 0; i < 4; i++)
                {
                    PBox(g, "Drawer", new Vector3(0, 0.17f + i * 0.32f, 0.302f), new Vector3(0.42f, 0.28f, 0.006f), pMetal);
                    PBox(g, "Handle", new Vector3(0, 0.25f + i * 0.32f, 0.31f), new Vector3(0.12f, 0.02f, 0.02f), pDark);
                }
            });

        FillProp(props, "Yellow", "WaterCooler", new Vector3(34.6f, 0, -98.97f), 180, new Vector3(0.34f, 1.35f, 0.34f), g =>
        {
            PBox(g, "Cabinet", new Vector3(0, 0.47f, 0), new Vector3(0.32f, 0.94f, 0.32f), pWhite);
            PBox(g, "Tap", new Vector3(0, 0.82f, 0.17f), new Vector3(0.06f, 0.05f, 0.04f), pDark);
            PBox(g, "DripTray", new Vector3(0, 0.6f, 0.15f), new Vector3(0.2f, 0.02f, 0.06f), pGrey);
            Part(g, PrimitiveType.Cylinder, "Bottle", new Vector3(0, 1.14f, 0), new Vector3(0.27f, 0.2f, 0.27f), bottle);
        });

        FillProp(props, "Yellow", "OfficeCouch", new Vector3(27.6f, 0, -113.92f), 0, new Vector3(1.9f, 0.85f, 0.85f), g =>
        {
            PBox(g, "Seat", new Vector3(0, 0.22f, 0.05f), new Vector3(1.9f, 0.44f, 0.75f), couch);
            PBox(g, "Back", new Vector3(0, 0.55f, -0.33f), new Vector3(1.9f, 0.6f, 0.19f), couch);
            PBox(g, "Arm_L", new Vector3(-0.87f, 0.33f, 0.05f), new Vector3(0.16f, 0.24f, 0.75f), couch);
            PBox(g, "Arm_R", new Vector3(0.87f, 0.33f, 0.05f), new Vector3(0.16f, 0.24f, 0.75f), couch);
            PBox(g, "Cushion_Sag", new Vector3(0.4f, 0.45f, 0.08f), new Vector3(0.75f, 0.03f, 0.6f), couch);
        });

        FillProp(props, "Yellow", "StackedChairs", new Vector3(23.0f, 0, -114.05f), 0, new Vector3(0.48f, 1.25f, 0.5f), g =>
        {
            foreach (var o in new[] { new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f), new Vector2(-0.2f, 0.2f), new Vector2(0.2f, 0.2f) })
                PBox(g, "Leg", new Vector3(o.x, 0.23f, o.y), new Vector3(0.025f, 0.46f, 0.025f), pMetal);
            for (int i = 0; i < 5; i++)
            {
                PBox(g, "Seat", new Vector3(0, 0.46f + i * 0.08f, 0.01f * i), new Vector3(0.44f, 0.04f, 0.42f), pGrey);
                PBox(g, "Back", new Vector3(0, 0.72f + i * 0.08f, -0.2f + 0.01f * i), new Vector3(0.42f, 0.3f, 0.025f), pGrey);
            }
        });

        FillProp(props, "Yellow", "CardboardBoxes", new Vector3(21.0f, 0, -113.85f), 0, new Vector3(1.15f, 0.75f, 0.55f), g =>
        {
            PBox(g, "Box_A", new Vector3(-0.28f, 0.2f, 0), new Vector3(0.58f, 0.4f, 0.5f), cardboard);
            Part(g, PrimitiveType.Cube, "Box_B", new Vector3(-0.25f, 0.55f, 0.02f), new Vector3(0.48f, 0.3f, 0.42f), cardboard, new Vector3(0, 9f, 0));
            PBox(g, "Box_C", new Vector3(0.33f, 0.17f, 0.04f), new Vector3(0.45f, 0.34f, 0.4f), cardboard);
            PBox(g, "Tape", new Vector3(-0.28f, 0.401f, 0), new Vector3(0.06f, 0.003f, 0.5f), pCanvas);
        });
    }

    // ---------------------------------------------------------------- 데칼

    static Material DecalMat(string name, Texture2D tex, Color color, float smoothness, bool albedo)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("HDRP/Decal")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseColorMap", tex); m.SetColor("_BaseColor", color);
        m.SetFloat("_AffectAlbedo", albedo ? 1 : 0); m.SetFloat("_AffectNormal", 0); m.SetFloat("_AffectMetal", 0); m.SetFloat("_AffectAO", 0);
        m.SetFloat("_AffectSmoothness", 1); m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_MaskBlendSrc", 0); // 알파(텍스처 모양)로 섞는다
        m.SetFloat("_DecalBlend", 1f);
        HDShaderUtils.ResetMaterialKeywords(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>forward 방향으로 투영하는 데칼. pos는 표면에서 0.15m 떨어진 점.</summary>
    static void Decal(Transform parent, string name, Material m, Vector3 pos, Vector3 euler, float w, float h, float fade = 1f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(euler);
        var d = go.AddComponent<DecalProjector>();
        d.material = m; d.size = new Vector3(w, h, 0.4f); d.pivot = new Vector3(0, 0, 0.2f); d.fadeFactor = fade;
        d.startAngleFade = 60f; d.endAngleFade = 85f; // 옆면으로 번지지 않게
    }

    // 금 간 자국: 위에서 아래로 갈라지며 가지를 친다. 알파 = 금.
    static Texture2D CrackTexture(string name, int seed)
    {
        const int size = 512;
        var segs = new List<(Vector2 a, Vector2 b, float w)>();
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();
        void Grow(Vector2 p, float ang, float width, int depth)
        {
            int steps = 10 + rng.Next(14);
            for (int i = 0; i < steps && width > 0.12f; i++)
            {
                ang += (R() - 0.5f) * 0.9f;
                var q = p + new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang)) * (0.02f + 0.03f * R());
                if (q.x < 0.06f || q.x > 0.94f || q.y < 0.05f || q.y > 0.97f) break;
                segs.Add((p, q, width));
                if (depth < 3 && R() < 0.2f) Grow(q, ang + (R() < 0.5f ? -1 : 1) * (0.5f + 0.6f * R()), width * 0.6f, depth + 1);
                p = q; width *= 0.965f;
            }
        }
        Grow(new Vector2(0.5f, 0.96f), 0f, 1f, 0);
        Grow(new Vector2(0.42f + 0.16f * R(), 0.8f), (R() - 0.5f) * 1.2f, 0.7f, 1);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float a = 0, halo = 0;
                foreach (var s in segs)
                {
                    var ab = s.b - s.a; float t = Mathf.Clamp01(Vector2.Dot(p - s.a, ab) / ab.sqrMagnitude);
                    float dist = (p - (s.a + ab * t)).magnitude * size; // 픽셀 단위
                    a = Mathf.Max(a, Mathf.Clamp01(1.4f * s.w + 0.3f - dist));
                    halo = Mathf.Max(halo, Mathf.Clamp01(1f - dist / (14f * s.w + 4f)) * 0.22f);
                }
                px[y * size + x] = new Color(0.02f, 0.02f, 0.02f, Mathf.Clamp01(a + halo));
            }
        return Save(name, size, px, TexKind.Alpha, 0, true);
    }

    // 젖은 얼룩: 가장자리가 불규칙한 웅덩이 모양.
    static Texture2D BlotchTexture(string name, int seed)
    {
        const int size = 256;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                float n = Fbm(u, v, 3, 5, seed);
                float a = SStep(0.0f, 0.22f, (1f - r) - 0.55f * (1f - n) + 0.1f);
                px[y * size + x] = new Color(0.25f, 0.27f, 0.26f, a);
            }
        return Save(name, size, px, TexKind.Alpha, 0, true);
    }

    // ---------------------------------------------------------------- 방별 배치

    static void ApplyPropsAndDecals()
    {
        PropMaterials();
        var crackA = DecalMat("GB_Decal_CrackA", CrackTexture("GB_Decal_CrackA", 7), new Color(1, 1, 1, 0.95f), 0.1f, true);
        var crackB = DecalMat("GB_Decal_CrackB", CrackTexture("GB_Decal_CrackB", 23), new Color(1, 1, 1, 0.95f), 0.1f, true);
        var blotch = BlotchTexture("GB_Decal_Blotch", 5);
        var wet = DecalMat("GB_Decal_Wet", blotch, new Color(0.72f, 0.77f, 0.77f, 0.3f), 0.9f, false);       // 젖은 반사 자국: 색은 그대로, 반짝임만(색을 어둡게 하면 그림자처럼 보였다)
        var damp = DecalMat("GB_Decal_Damp", blotch, new Color(0.35f, 0.3f, 0.2f, 0.6f), 0.5f, true);       // 벽·카펫의 눅눅한 얼룩

        // ① 수영장 홀 ------------------------------------------------------------
        {
            var room = Root("GB_01_PoolHall");
            var props = PropsRoot(room, LOutdoor);
            // 입구 방(넓다): 사물함, 접이식 의자, 안전 수칙판, 시계
            Lockers(props, new Vector3(5.6f, 0, -75.4f), 270, 5);
            FoldingChair(props, new Vector3(-5.2f, 0, -79.6f), 90);
            FoldingChair(props, new Vector3(-5.2f, 0, -80.3f), 78);
            SignBoard(props, new Vector3(3.6f, 1.6f, -83.54f), 0);
            Clock(props, new Vector3(-3.6f, 2.7f, -83.54f), 0);
            // 홀: 데크 폭이 좁아(벽과 기둥 사이 0.9m) 바닥에는 얇은 배수구 덮개만, 나머지는 벽걸이
            LifeRing(props, new Vector3(-5.84f, 1.5f, -94.7f), 90);
            LifeRing(props, new Vector3(5.84f, 1.5f, -112.7f), 270);
            SignBoard(props, new Vector3(-5.84f, 1.6f, -100.7f), 90, false); // 데크가 좁아 입간판을 세우면 길을 막는다
            foreach (float z in new[] { -89.7f, -95.7f, -101.7f, -107.7f, -113.7f })
            { Drain(props, new Vector3(3.72f, 0, z)); Drain(props, new Vector3(-3.72f, 0, z)); }
            FoldingChair(props, new Vector3(-4.9f, 0, -122.4f), 20);

            // D-2 물 위 발광 구체(장식만, 줍기 기능 없음)
            var orbMat = Lit("GB_GlowOrb_Hall", m => Emissive(m, new Color(0.82f, 0.95f, 1f), 2200f));
            var orbs = Group(props, "WaterGlowOrbs");
            var spots = new[] { new Vector3(-1.4f, 0.56f, -91.2f), new Vector3(1.6f, 0.7f, -94.9f), new Vector3(-0.3f, 0.6f, -99.3f), new Vector3(1.9f, 0.52f, -103.4f),
                                new Vector3(-1.8f, 0.74f, -106.1f), new Vector3(0.6f, 0.58f, -109.8f), new Vector3(-1.1f, 0.66f, -113.3f) };
            foreach (var s in spots)
                Prim(PrimitiveType.Sphere, orbs, "GlowOrb", new Vector3(s.x, -0.12f, s.z), Vector3.one * s.y, orbMat, LOutdoor);
            foreach (float z in new[] { -95f, -109f })
                MakeLight(orbs, "OrbGlowLight", LightType.Point, new Vector3(0, 0.5f, z), Vector3.zero, new Color(0.75f, 0.92f, 1f), 30000f, 7f, LOutdoor);

            // B-5 젖은 바닥 자국
            var decals = Group(Fresh(room.transform.Find(GenName), "Decals"), "Wet");
            var wetSpots = new[] { new Vector4(4.1f, -88.6f, 1.5f, 30), new Vector4(-3.9f, -108.4f, 1.6f, 300), new Vector4(0.6f, -119.6f, 2.2f, 150) };
            foreach (var w in wetSpots) Decal(decals, "WetPatch", wet, new Vector3(w.x, 0.15f, w.y), new Vector3(90, w.w, 0), w.z, w.z * 0.8f);
        }

        // ② 갈라진 복도 ----------------------------------------------------------
        {
            var room = Root("GB_02_Crack");
            var props = PropsRoot(room, LCrack);
            Lockers(props, new Vector3(9f, 0, -109.45f), 0, 4);
            FoldingChair(props, new Vector3(11.3f, 0, -104.15f), 170);
            SignBoard(props, new Vector3(8.4f, 1.6f, -103.72f), 180);
            Clock(props, new Vector3(12.2f, 3.1f, -109.68f), 0);
            Drain(props, new Vector3(13.1f, 0, -106.7f));
            Drain(props, new Vector3(8.2f, 0, -106.7f));

            var decals = Fresh(room.transform.Find(GenName), "Decals");
            // B-2 벽의 금: 북쪽 벽 큰 금 하나, 남쪽 벽 하나, 바닥으로 이어지는 금
            Decal(decals, "Crack_NorthWall", crackA, new Vector3(17.6f, 2.1f, -103.85f), new Vector3(0, 0, 0), 2.6f, 3.8f);
            Decal(decals, "Crack_SouthWall", crackB, new Vector3(13.6f, 2.3f, -109.55f), new Vector3(0, 180, 0), 2.2f, 3.4f);
            Decal(decals, "Crack_Floor", crackB, new Vector3(17.2f, 0.15f, -105.2f), new Vector3(90, 200, 0), 2.4f, 3f);
            Decal(decals, "Crack_Ceiling", crackA, new Vector3(13.4f, 3.85f, -108.4f), new Vector3(-90, 60, 0), 2f, 2.6f, 0.8f);
            // 금 아래로 번진 물 얼룩과 젖은 바닥
            Decal(decals, "Damp_NorthWall", damp, new Vector3(17.5f, 1.0f, -103.85f), new Vector3(0, 0, 0), 1.8f, 2f, 0.7f);
            Decal(decals, "Wet_Floor_A", wet, new Vector3(17.3f, 0.15f, -104.9f), new Vector3(90, 40, 0), 2.6f, 2f);
            Decal(decals, "Wet_Floor_B", wet, new Vector3(13.4f, 0.15f, -108.8f), new Vector3(90, 130, 0), 2f, 1.5f);
            Decal(decals, "Wet_Floor_C", wet, new Vector3(7.6f, 0.15f, -106.4f), new Vector3(90, 250, 0), 2.2f, 1.8f);
        }

        // ③ 노란 방 --------------------------------------------------------------
        {
            var room = Root("GB_03_Yellow");
            var props = PropsRoot(room, LYellow);
            DeskAndChair(props, new Vector3(23.4f, 0, -99.2f), 180);
            TvOnStand(props, new Vector3(35.1f, 0, -112.4f), 300);
            Frame(props, new Vector3(27.4f, 1.7f, -98.72f), 180, 0.5f, 0.7f);
            Frame(props, new Vector3(28.4f, 1.75f, -98.72f), 180, 0.7f, 0.5f);
            Frame(props, new Vector3(29.6f, 1.65f, -98.72f), 180, 0.45f, 0.6f);
            // 2026-11-23 사용자 요청: 포스터는 액자 틀 없이(틀 텍스처가 깨져 보였다) 1.5배(0.78×1.04). 벽 덮개(x 20.20) 1.5cm 앞에 붙인다.
            var posterImg = Img2D("Yellow", "Poster");
            if (posterImg != null) Poster(props, new Vector3(20.215f, 1.65f, -111.6f), 90, 0.78f, 1.04f, posterImg);
            else Frame(props, new Vector3(20.17f, 1.7f, -111.6f), 90, 0.6f, 0.8f);
            YellowFill(props);

            var decals = Fresh(room.transform.Find(GenName), "Decals");
            // 얼룩이 너무 많다는 피드백(2026-11-17): 카펫 하나만 옅게, 천장 얼룩은 뺀다.
            Decal(decals, "Damp_Carpet_A", damp, new Vector3(31.2f, 0.15f, -103.4f), new Vector3(90, 20, 0), 3.2f, 2.4f, 0.45f);
        }

        // ④ 천사 방 소품(날개 달린 눈, 구름)은 ApplyAngel(GreyboxGraphicsPass.Angel.cs)에서 만든다.
    }
}
