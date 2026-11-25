using UnityEditor;
using UnityEngine;

// 2026-11-25 빌드 플레이 피드백.
// 1) 수영장 홀 남쪽(입구 쪽)은 타일 입구 상자(높이 4m)뿐이라, 홀 안에서 뒤돌면 그 위로 인트로 건물(출구 벽 11m)이 보였다.
//    나머지 세 벽(12m)과 같은 흰 벽을 입구 상자 위(4~12m)에 세워 홀을 닫는다.
// 2) 거실 TV는 방 가운데쯤에서 바라봐도 찾아지게(6m → 12m).
public static partial class GreyboxGraphicsPass
{
    const string HallSouthUpperName = "Hall_Wall_South_Upper";
    const float HallSouthWallTop = 12f;      // 홀 옆·끝 벽 높이
    const float HallEntryTop = 4f;      // 타일 입구 상자 높이
    const float HallSouthZ = -83.7f;    // 입구 상자 남쪽 벽(홀 쪽 면)
    const float TvFindDistance = 12f;   // 거실 TV를 찾을 수 있는 거리(m)

    static void ApplyBuildFeedback1125()
    {
        CloseHallSouth();
        MatteTunnels();
        BobHallOrbs();
        var tv = GameObject.Find("Oddity_TV");
        if (tv != null && tv.TryGetComponent<GazeTarget>(out var g))
        {
            g.maxDistance = TvFindDistance;
            EditorUtility.SetDirty(g);
        }
    }

    static void CloseHallSouth()
    {
        var hall = GameObject.Find("GB_01_PoolHall");
        if (hall == null) return;
        var old = hall.transform.Find(HallSouthUpperName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var side = hall.transform.Find("Hall_Wall_East")?.GetComponent<MeshRenderer>();
        if (side == null) return;
        float h = HallSouthWallTop - HallEntryTop;
        // 폭은 옆 벽 바깥면까지(x ±6.15).
        // 두께 0.34: 입구 상자 천장(4.0~4.3m)의 끝면이 이 벽 바깥면과 같은 평면이라 줄무늬로 깨져 보였다 → 벽이 천장 끝을 덮게 조금 두껍게
        Box(hall.transform, HallSouthUpperName, new Vector3(0f, HallEntryTop + h / 2f, HallSouthZ - 0.02f), new Vector3(12.3f, h, 0.34f),
            side.sharedMaterial, side.renderingLayerMask);
        // 홀에서 보이는 입구 상자 남쪽 벽(문 양옆)도 타일 대신 홀 벽과 같은 흰색(2026-11-25 사용자 피드백). 위 벽과 한 면으로 이어진다.
        foreach (var n in new[] { "Entry_Wall_South_East", "Entry_Wall_South_West" })
        {
            var r = hall.transform.Find(n)?.GetComponent<MeshRenderer>();
            if (r == null) continue;
            r.sharedMaterial = side.sharedMaterial;
            EditorUtility.SetDirty(r);
        }
    }

    /// <summary>
    /// 인트로·같은 수영장 아치 터널은 수영장 타일(매끈 0.65)을 같이 써서, 화면 반사(SSR)로 끝의 밝은 아치 문이 옆벽에 번져
    /// 깨진 얼룩처럼 보였다(2026-11-25). 터널만 덜 매끈한 복사본 재질을 쓴다.
    /// </summary>
    static void MatteTunnels()
    {
        Material matte = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || t.name != "Tunnel") continue;
            var r = t.GetComponent<MeshRenderer>();
            if (r == null || r.sharedMaterial == null) continue;
            if (matte == null)
            {
                var src = r.sharedMaterial.name == "GB_Tunnel_Tile" ? null : r.sharedMaterial;
                matte = Lit("GB_Tunnel_Tile", m =>
                {
                    if (src != null) { m.CopyPropertiesFromMaterial(src); }
                    m.SetFloat("_Smoothness", TunnelSmoothness);
                });
            }
            r.sharedMaterial = matte;
            EditorUtility.SetDirty(r);
        }
    }

    const float TunnelSmoothness = 0.25f;

    /// <summary>수영장 홀 빛 구슬도 인트로 구슬처럼 물에서 동동(2026-11-25 사용자 결정). 값은 AddWaterBobs의 공과 같다.</summary>
    static void BobHallOrbs()
    {
        var orbs = GameObject.Find("GB_01_PoolHall/Placeholder_Props/GFX_Props/WaterGlowOrbs");
        if (orbs == null) return;
        foreach (Transform t in orbs.transform)
        {
            if (!t.TryGetComponent<WaterBob>(out var wb)) wb = t.gameObject.AddComponent<WaterBob>();
            wb.bobHeight = 0.03f; wb.bobPeriod = 2.8f; wb.tiltDegrees = 6f; wb.driftRadius = 0.12f; wb.driftPeriod = 19f; wb.spinDegreesPerSecond = 4f;
            EditorUtility.SetDirty(wb);
        }
    }
}
