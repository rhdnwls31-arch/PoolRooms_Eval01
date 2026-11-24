using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// ④ 천사 방: 하늘 속 + 날개 달린 눈 여러 개(2026-11-18 사용자 방향, design/references/weirdcore-mood-2026-11-18/09-angel-winged-eyes).
// 이전(2026-11-17)의 어두운 방 + 스포트라이트 천사(A-3)를 대체한다. 방 크기, 벽, 콜라이더는 그대로 두고 재질만 하늘로 바꾼다.
// 천사는 더미다. 나중에 VARCO 3D 모델로 바꾼다(GFX_Props/WingedEyes 아래 그룹 하나 = 천사 하나).
public static partial class GreyboxGraphicsPass
{
    // 방 범위(월드): x 18.15~38.15, z -114.7~-142.7, 천장 12m
    const float AngelCx = 28.15f;
    const float AngelFogNits = 1f; // 안개 색 배율. 캡처를 보고 맞춘다.
    // 노출: 바깥(전역 8.7)과 같게 둬서, 천사 방에서 다음 방(같은 수영장)을 볼 때 어둡게 보이지 않게 한다(2026-11-18 피드백).
    // 처음에는 EV 10에 맞춰 밝기를 정했으므로, 빛과 발광 값에 2^(EV-10)을 곱해 화면 밝기는 그대로 둔다.
    const float AngelEV = 8.7f;
    static float AngelK => Mathf.Pow(2f, AngelEV - 10f);

    static void ApplyAngel()
    {
        var root = Root("GB_04_Angel");
        DisableOldLights(root);
        SetLayers(root, LAngel);
        AddLayer(root, "Wall_North", LYellow);

        // 벽과 천장: 구름 낀 하늘. 빛을 받지 않아도 밝게 보이도록 같은 그림을 발광으로도 입힌다.
        var skyMat = Lit("GB_Angel_Sky", m =>
        {
            Textured(m, SkySet(), 26f, new Color(0.35f, 0.35f, 0.35f), 0f, false);
            m.SetFloat("_Smoothness", 0f);
            m.SetTexture("_EmissiveColorMap", m.GetTexture("_BaseColorMap"));
            m.SetFloat("_UVEmissive", 6f); // 기본 맵과 같은 좌표(월드 Triplanar)
            m.SetColor("_EmissiveColor", Color.white * (900f * AngelK));
            m.SetFloat("_UseEmissiveIntensity", 0f);
            m.SetFloat("_EmissiveExposureWeight", 1f);
        });
        // 바닥: 구름 위. 살짝 발광시켜 그림자 속에서도 하얗게.
        var cloudMat = Lit("GB_Angel_CloudFloor", m =>
        {
            Textured(m, CloudFloorSet(), 9f, Color.white, 0.4f);
            m.SetTexture("_EmissiveColorMap", m.GetTexture("_BaseColorMap"));
            m.SetFloat("_UVEmissive", 6f);
            m.SetColor("_EmissiveColor", Color.white * (220f * AngelK));
            m.SetFloat("_UseEmissiveIntensity", 0f);
            m.SetFloat("_EmissiveExposureWeight", 1f);
        });
        SetStructureMaterials(root.transform, cloudMat, skyMat, skyMat);
        // 출구 통로: 다음 구역(하늘 계단)과 같은 하늘 재질. 검은 통로는 깨진 구멍처럼 보였다(2026-11-18 피드백).
        SetStructureMaterials(root.transform.Find("Passage_To_SameIntro"), cloudMat, skyMat, skyMat);
        // 출구 문턱 표시(검은 판)도 구름 바닥으로 칠한다. 바닥 틈을 덮고 있어 끄지 않는다.
        FindMarker("GB_04_Angel", -142.7f, AngelCx).GetComponent<MeshRenderer>().sharedMaterial = cloudMat;

        var gen = Fresh(root.transform, GenName);
        var lights = Group(gen, "Lights");
        // 하늘에서 내리는 빛: 입구 쪽 위에서 방 안쪽으로. 천사들의 얼굴(입구를 향함)이 밝게 보인다.
        // MakeLight는 루멘 단위라 방향광에 쓸 수 없다. 직접 만든다.
        var sunGo = new GameObject("SkySun");
        sunGo.transform.SetParent(lights, true);
        sunGo.transform.SetPositionAndRotation(new Vector3(AngelCx, 11f, -116f), Quaternion.Euler(42f, 172f, 0f));
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional; sun.color = new Color(1f, 0.98f, 0.94f); sun.shadows = LightShadows.None;
        var sunHd = sunGo.AddComponent<HDAdditionalLightData>();
        sunHd.SetIntensity(2600f * AngelK, LightUnit.Lux);
        sunHd.affectsVolumetric = false;
        sunHd.lightlayersMask = (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)LAngel;
        // 아래에서 올라오는 구름 반사광: 날개 아랫면이 검게 뭉개지지 않게
        foreach (float z in new[] { -121f, -129f, -137f })
            MakeLight(lights, "CloudBounce", LightType.Point, new Vector3(AngelCx, 0.6f, z), Vector3.zero, new Color(0.86f, 0.92f, 1f), 400000f * AngelK, 16f, LAngel, false, false);

        // 노란 방 바닥·천장·벽 끝이 북쪽 벽을 0.3m 뚫고 나온 자리를 덮는 띠(이전과 같은 자리, 재질만 하늘)
        var caps = Group(gen, "NorthWallTrim");
        Box(caps, "Top", new Vector3(AngelCx, 3.15f, -114.86f), new Vector3(17f, 0.38f, 0.36f), skyMat, LAngel);
        Box(caps, "West", new Vector3(20f, 1.67f, -114.86f), new Vector3(0.38f, 3.34f, 0.36f), skyMat, LAngel);
        Box(caps, "East", new Vector3(36.3f, 1.67f, -114.86f), new Vector3(0.38f, 3.34f, 0.36f), skyMat, LAngel);
        // 노란 방 카펫 바닥도 북쪽 벽을 0.3m 뚫고 나와(z -114.7~-115.0) 구름 바닥과 같은 높이에서 갈색 줄로 보였다. 얇은 구름판으로 덮는다.
        Box(caps, "Floor", new Vector3(AngelCx, 0.006f, -114.87f), new Vector3(17f, 0.012f, 0.36f), cloudMat, LAngel);

        // 밝은 하늘: 노출을 올리고, 흰 안개를 옅게 깔아 벽 모서리가 멀리 흐려지게 한다.
        var profile = RoomProfile("Angel", AngelEV, 0.25f, 0.1f, new Color(0.92f, 0.95f, 1f), 40f, new Color(0.75f, 0.84f, 1f), 0f, 0f);
        // 볼류메트릭 안개는 빛이 없으면 회색으로 탁해진다. 이 방은 일반 안개(하늘색 상수)만 쓴다.
        if (profile.TryGet<Fog>(out var fog))
        {
            fog.enableVolumetricFog.Override(false);
            fog.color.Override(new Color(0.75f, 0.84f, 1f) * AngelFogNits);
            UnityEditor.EditorUtility.SetDirty(fog);
            UnityEditor.EditorUtility.SetDirty(profile);
        }
        LocalVolume(gen, "LocalVolume", new Vector3(AngelCx, 6f, -128.6f), new Vector3(20f, 12f, 27.8f), 3f, 125f, profile);

        // ---- 소품: 날개 달린 눈, 벽 아래 구름 ----
        var props = PropsRoot(root, LAngel);
        var oldAngel = root.transform.Find("Placeholder_Props/Angel");
        if (oldAngel != null) oldAngel.gameObject.SetActive(false); // 예전 회색 더미는 끄기만 한다

        var sclera = Lit("GB_Angel_EyeWhite", m => { Plain(m, new Color(0.93f, 0.88f, 0.86f), 0.85f); });
        var iris = Lit("GB_Angel_EyeIris", m => Plain(m, new Color(0.62f, 0.06f, 0.05f), 0.7f));
        var pupil = Lit("GB_Angel_EyePupil", m => Plain(m, new Color(0.01f, 0.01f, 0.01f), 0.9f));
        var lid = Lit("GB_Angel_EyeLid", m => Plain(m, new Color(0.72f, 0.32f, 0.3f), 0.4f));
        var feather = Lit("GB_Angel_Feather", m => Plain(m, new Color(0.92f, 0.92f, 0.94f), 0.15f));

        // 입구(플레이어가 들어오는 자리)를 처음부터 바라본다. 플레이 중에는 FacePlayer가 플레이어를 따라 돈다.
        var entry = new Vector3(AngelCx, 1.6f, -116.5f);
        var eyes = Group(props, "WingedEyes");
        var spots = new (Vector3 pos, float scale)[]
        {
            (new Vector3(AngelCx, 7.6f, -136.5f), 1.7f),  // 정면 안쪽, 가장 큰 눈
            (new Vector3(22.6f, 5.2f, -127.5f), 1.0f),
            (new Vector3(33.8f, 6.4f, -125.0f), 1.15f),
            (new Vector3(23.4f, 9.4f, -134.0f), 0.9f),
            (new Vector3(33.2f, 9.8f, -133.5f), 1.0f),
            (new Vector3(25.0f, 4.3f, -139.8f), 0.8f),
            (new Vector3(32.0f, 4.0f, -140.2f), 0.75f),
            (new Vector3(AngelCx, 10.3f, -124.5f), 0.7f), // 머리 위
        };
        for (int i = 0; i < spots.Length; i++)
        {
            var g = WingedEye(eyes, $"WingedEye_{i:00}", spots[i].pos, spots[i].scale, sclera, iris, pupil, lid, feather);
            g.rotation = Quaternion.LookRotation(entry - g.position, Vector3.up);
        }

        // 벽 아래 구름 덩어리(콜라이더 없음). 빽빽하면 벽처럼 보여서(2026-11-18 피드백) 띄엄띄엄 놓는다.
        // 방 범위는 x 18.15~38.15. 나중에 VARCO 구름 모델로 바꿀 때도 이 자리를 쓴다.
        var puffs = Group(props, "CloudBanks");
        var spotsW = new[] { -117.5f, -122.8f, -126.0f, -132.5f, -137.0f, -140.6f };
        // 동쪽 벽 z -133.4는 도시에서 넘어오는 문틀 자리(AngelDoorPos). 그 앞(-133.6)의 구름이 문틀을 가려서 뺐다(2026-11-18).
        var spotsE = new[] { -118.8f, -124.5f, -129.8f, -139.2f };
        for (int i = 0; i < spotsW.Length + spotsE.Length; i++)
        {
            bool west = i < spotsW.Length;
            float z = west ? spotsW[i] : spotsE[i - spotsW.Length];
            float r = Hash(i, 3, 501), r2 = Hash(i, 7, 503);
            float w = 1.8f + 1.8f * r;
            var p = new Vector3(west ? 19.2f + 0.5f * r2 : 37.1f - 0.5f * r2, 0.2f, z);
            if (!CloudVarco(puffs, $"Puff_{i:00}", p, w, i, Hash(i, 11, 505) * 360f))
                Prim(PrimitiveType.Sphere, puffs, $"Puff_{i:00}", p, new Vector3(w * 0.8f, 0.8f + 0.8f * r2, w), cloudMat, LAngel);
            // 2026-11-23 사용자 피드백 "띄엄띄엄 비어 보인다": 큰 구름마다 작은 구름 하나를 방 안쪽으로 붙여 무리를 만든다(VARCO가 있을 때만).
            // 가벼운 CloudBank(1.8만 삼각형)만 써서 늘려도 부담이 작다. 동쪽 문틀(z -135~-131.8) 앞은 비운다.
            float cz = z + (Hash(i, 13, 507) > 0.5f ? 1.4f : -1.4f) * (0.8f + 0.4f * r);
            if (!west && cz > -135.2f && cz < -131.6f) cz = z + (z < -133.4f ? -1.4f : 1.4f);
            var cp = new Vector3(west ? p.x + 0.9f : p.x - 0.9f, 0.2f, cz);
            float bw = w * (0.45f + 0.2f * r2);
            if (!CloudVarco(puffs, $"Puff_{i:00}_b", cp, bw, i + 1, Hash(i, 17, 509) * 360f))
                Prim(PrimitiveType.Sphere, puffs, $"Puff_{i:00}_b", cp, new Vector3(bw * 0.8f, 0.5f + 0.5f * r, bw), cloudMat, LAngel); // 더미도 큰 구름 옆에 작은 덩어리를 붙여 무리를 만든다
        }
        int si = 0;
        foreach (float x in new[] { 21.5f, 34.6f }) // 출구 벽 양옆(문 자리 26.65~29.65는 비운다)
            if (!CloudVarco(puffs, $"Puff_S_{x:F0}", new Vector3(x, 0.25f, -142.2f), 2.6f, si++, x < 28f ? 10f : 190f))
                Prim(PrimitiveType.Sphere, puffs, $"Puff_S_{x:F0}", new Vector3(x, 0.25f, -142.2f), new Vector3(2.6f, 1.1f, 1.4f), cloudMat, LAngel);
        // 2026-11-24 사용자 피드백 "구름방은 그냥 구름 없애자. 아래 안개 깔려 있는 것만 있어도 될 듯": 구름 덩어리는 두지 않는다(바닥 안개 AngelCloudFog는 그대로).
        if (!AngelCloudPuffs) Object.DestroyImmediate(puffs.gameObject);
    }

    const bool AngelCloudPuffs = false;

    // 2026-11-23 VARCO 구름 두 종(납작한 CloudBank 2.5×1.0, 봉긋한 CloudBank2 2.0×1.4)을 번갈아 쓴다.
    // 폭이 더미 구(w)와 같게 배율을 맞추고, 밑이 구름 바닥에 묻히게 10cm 내린다. 콜라이더 없음(더미와 같음).
    // 2026-11-23: CloudBank2는 50만 삼각형이라(6개면 300만) 빼고, 모두 CloudBank(1.8만)로 쓴다. 똑같아 보이지 않게 높이를 0.8~1.6배로 바꾼다.
    // 2026-11-24 사용자 결정: 천사 방 구름은 VARCO 대신 더미(흰 구름 바닥 재질의 납작한 타원 덩어리)로 되돌린다. 모델 파일은 그대로 둔다.
    const bool AngelCloudsVarco = false;

    static bool CloudVarco(Transform parent, string name, Vector3 pos, float width, int i, float yaw)
    {
        const bool flat = true;
        string model = "CloudBank";
        if (!AngelCloudsVarco || VarcoPrefab("Angel", model) == null) return false;
        var g = new GameObject(name).transform;
        g.SetParent(parent, true);
        g.SetPositionAndRotation(new Vector3(pos.x, -0.1f, pos.z), Quaternion.Euler(0f, yaw, 0f));
        float s = width / 2.5f;
        g.localScale = new Vector3(s, s * (0.8f + 0.8f * Hash(i, 19, 511)), s);
        var go = VarcoInto(g, "Angel", model, LAngel, default, default, false);
        // 오목한 뭉게 틈이 그늘져 회색 돌처럼 보였다(2026-11-23 Play). 바닥 구름처럼 같은 그림을 발광으로도 입히고, 자기 그림자는 끈다.
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            var m = r.sharedMaterial;
            m.SetTexture("_EmissiveColorMap", m.GetTexture("_BaseColorMap"));
            m.EnableKeyword("_EMISSIVE_COLOR_MAP");
            m.SetFloat("_UVEmissive", 0f);
            m.SetColor("_EmissiveColor", Color.white * ((flat ? 700f : 900f) * AngelK)); // CloudBank2가 더 어두운 그림이라 더 세게
            m.SetFloat("_UseEmissiveIntensity", 0f);
            m.SetFloat("_EmissiveExposureWeight", 1f);
            m.enableInstancing = true; // 같은 메쉬·재질 22개를 한 번에 그린다
            UnityEditor.EditorUtility.SetDirty(m);
        }
        return true;
    }

    /// <summary>날개 달린 눈 하나(더미). 앞(+Z)이 눈동자 방향. 날개는 양옆에서 아래로 늘어진다(참고 이미지 09).</summary>
    static Transform WingedEye(Transform parent, string name, Vector3 pos, float scale, Material sclera, Material iris, Material pupil, Material lid, Material feather)
    {
        var g = PropGroup(parent, name, pos, 0);
        g.localScale = Vector3.one * scale;
        g.gameObject.AddComponent<FacePlayer>();
        // 2026-11-19 VARCO 날개 달린 눈: 원점 = 눈알 가운데, 앞(+Z) = 눈동자, 날개 폭 4.5m(scale 1 기준). 있으면 더미 대신 쓴다.
        if (VarcoInto(g, "Angel", "WingedEye", propLayer, default, default, false)) return g;

        // 눈: 가로로 긴 눈알, 붉은 홍채, 검은 동공, 위아래 붉은 눈꺼풀 테
        Part(g, PrimitiveType.Sphere, "Eye", Vector3.zero, new Vector3(1.5f, 0.95f, 0.9f), sclera);
        Part(g, PrimitiveType.Cylinder, "Iris", new Vector3(0, 0, 0.425f), new Vector3(0.62f, 0.02f, 0.62f), iris, new Vector3(90, 0, 0));
        Part(g, PrimitiveType.Cylinder, "Pupil", new Vector3(0, 0, 0.455f), new Vector3(0.27f, 0.02f, 0.27f), pupil, new Vector3(90, 0, 0));
        Part(g, PrimitiveType.Capsule, "LidTop", new Vector3(0, 0.43f, 0.12f), new Vector3(0.22f, 0.78f, 0.22f), lid, new Vector3(0, 0, 90));
        Part(g, PrimitiveType.Capsule, "LidBottom", new Vector3(0, -0.43f, 0.12f), new Vector3(0.18f, 0.7f, 0.18f), lid, new Vector3(0, 0, 90));

        // 날개: 어깨에서 깃털이 부채꼴로 늘어진다. 바깥쪽일수록 길고 더 벌어진다. 위로 솟은 짧은 덮깃 3개.
        for (int s = -1; s <= 1; s += 2)
        {
            var wing = new GameObject(s < 0 ? "Wing_L" : "Wing_R").transform;
            wing.SetParent(g, false);
            for (int i = 0; i < 7; i++)
            {
                float len = 2.6f + 0.22f * i;
                float ang = s * (2f + 4.5f * i);
                var pivot = new Vector3(s * (0.8f + 0.2f * i), 0.65f + 0.05f * i, -0.18f - 0.03f * i);
                var rot = Quaternion.Euler(0, 0, ang);
                Part(wing, PrimitiveType.Capsule, $"Feather_{i}", pivot + rot * new Vector3(0, -len / 2f, 0), new Vector3(0.34f, len / 2f, 0.1f), feather, rot.eulerAngles);
            }
            for (int j = 0; j < 3; j++)
            {
                float len = 1.0f + 0.25f * j;
                float ang = s * (125f + 18f * j);
                var pivot = new Vector3(s * (0.95f + 0.2f * j), 0.8f, -0.12f);
                var rot = Quaternion.Euler(0, 0, ang);
                Part(wing, PrimitiveType.Capsule, $"Covert_{j}", pivot + rot * new Vector3(0, -len / 2f, 0), new Vector3(0.3f, len / 2f, 0.1f), feather, rot.eulerAngles);
            }
        }
        return g;
    }

    /// <summary>하늘: 파란 바탕에 흰 뭉게구름. 가로세로 반복.</summary>
    static (Texture2D, Texture2D, Texture2D) SkySet()
    {
        var blue = new Color(0.33f, 0.58f, 0.94f);
        return MakeSet("GB_Sky", 1024, 0f, (float u, float v, out Color col, out float h, out Color mk) =>
        {
            float n = Fbm(u, v, 3, 5, 811);
            float c = SStep(0.5f, 0.7f, n);
            float shade = 0.82f + 0.18f * SStep(0.55f, 0.8f, Fbm(u, v, 6, 3, 823));
            col = Color.Lerp(blue, new Color(shade, shade, shade * 1.02f), c); col.a = 1;
            h = 0.5f;
            mk = new Color(0, 1, 0, 0);
        });
    }

    /// <summary>구름 바닥: 흰색에 옅은 회청색 그늘.</summary>
    static (Texture2D, Texture2D, Texture2D) CloudFloorSet()
    {
        return MakeSet("GB_CloudFloor", 1024, 0.04f, (float u, float v, out Color col, out float h, out Color mk) =>
        {
            float n = Fbm(u, v, 4, 5, 907);
            col = Color.Lerp(new Color(0.74f, 0.8f, 0.9f), new Color(1f, 1f, 1f), SStep(0.3f, 0.7f, n)); col.a = 1;
            h = n;
            mk = new Color(0, 1, 0, 0.05f);
        });
    }
}
