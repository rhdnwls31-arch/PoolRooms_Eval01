using UnityEditor;
using UnityEngine;

// 2026-11-24 VARCO 수영장 소품 3종: 데크 벤치, 화분 야자수, 큰 그릇 나무. 인트로·같은 수영장(IntroMapBuilder 더미, 복사본 포함)에 씌운다.
// 더미 조각 렌더러만 끄고 그룹 콜라이더는 화분·벤치에 맞춘다(잎·줄기는 통과).
// 모델 크기(가져올 때): DeckBench 2.47×0.9×0.75 / PlanterTree 높이 3.0, 화분 반지름 0.63·높이 0.7 / BowlTree 높이 6.0, 그릇 반지름 1.3·높이 0.85.
public static partial class GreyboxGraphicsPass
{
    const string PoolPropVarcoName = "VARCO_Prop";

    static void SwapPoolDeckProps()
    {
        int bench = 0, planter = 0, tree = 0, table = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null) continue;
            string kind = t.name.StartsWith("Prop_Bench_") ? "DeckBench"
                : (t.name.StartsWith("Prop_Planter_") || t.name.StartsWith("LagoonCourt_Planter_")) ? "PlanterTree"
                : t.name.StartsWith("ChannelHall_Tree_") ? "BowlTree"
                : (t.name.StartsWith("Prop_SideTable_") || t.name.StartsWith("LagoonCourt_Table_")) ? "SideTable" : null; // 2026-11-24 사이드 테이블(0.49×0.5)
            if (kind == null || VarcoPrefab("Pool", kind) == null) continue;

            var old = t.Find(PoolPropVarcoName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            uint layer = 1u;
            float dummyH = 0f;
            foreach (Transform c in t)
                if (c.TryGetComponent<MeshRenderer>(out var r))
                {
                    layer = r.renderingLayerMask; r.enabled = false; EditorUtility.SetDirty(r);
                    if (c.name == "Stem") dummyH = c.localScale.y * 2f;   // 화분 줄기 = plantHeight
                    if (c.name == "Trunk") dummyH = c.localScale.y * 2f;  // 큰 나무 줄기 = height
                }
            int h = 7; foreach (char ch in t.name) h = h * 31 + ch; // 실행마다 같은 값
            float yaw = (kind == "DeckBench" || kind == "SideTable") ? 0f : Mathf.Abs(h % 360);

            float s; Vector3 col, colC;
            if (kind == "SideTable")
            {
                s = 1f; // 가져올 때 높이 0.5m로 맞춤(더미 상판 0.495, 라디오 바닥 0.51)
                col = new Vector3(0.5f, 0.5f, 0.5f); colC = new Vector3(0f, 0.25f, 0f);
            }
            else if (kind == "DeckBench")
            {
                s = 2.1f / 2.47f; // 더미 폭에 맞춤
                col = new Vector3(2.47f, 0.9f, 0.75f) * s; colC = new Vector3(0f, col.y / 2f, 0f);
            }
            else if (kind == "PlanterTree")
            {
                // 더미 줄기 1.6~3.2m → 0.75~1.0배(화분이 너무 커지지 않게). 화분 반지름 0.47~0.63
                s = Mathf.Lerp(0.75f, 1.0f, Mathf.InverseLerp(1.6f, 3.2f, dummyH));
                float r = 0.63f * s; col = new Vector3(r * 2f, 0.7f * s, r * 2f); colC = new Vector3(0f, col.y / 2f, 0f);
            }
            else
            {
                // 더미 줄기 5.0~6.9m → 0.85~1.05배. 그릇 반지름 1.1~1.37(더미 1.1)
                s = Mathf.Lerp(0.85f, 1.05f, Mathf.InverseLerp(5.0f, 6.9f, dummyH));
                float r = 1.3f * s; col = new Vector3(r * 2f, 0.85f * s, r * 2f); colC = new Vector3(0f, col.y / 2f, 0f);
            }
            var go = VarcoInto(t, "Pool", kind, layer, default, new Vector3(0f, yaw, 0f), false);
            go.name = PoolPropVarcoName;
            go.transform.localScale = Vector3.one * s;
            if (t.TryGetComponent<BoxCollider>(out var bc)) { bc.center = colC; bc.size = col; EditorUtility.SetDirty(bc); }
            if (kind == "DeckBench") bench++; else if (kind == "PlanterTree") planter++; else if (kind == "SideTable") table++; else tree++;
        }
        Debug.Log($"[GreyboxGraphicsPass] VARCO 데크 벤치 {bench}, 화분 야자수 {planter}, 큰 그릇 나무 {tree}, 사이드 테이블 {table}");
    }

    // ---------------------------------------------------------------- 수영장 홀 끝의 마네킹 → 수영모 인외
    // 2026-11-24 사용자 결정: 마네킹은 기존 모델을 쓰고(VARCO는 몸선이 드러남), 마네킹이 앞쪽에 몰려 있어 수영장 홀 끝(Basin 너머)에
    // 멀리 선 Mannequin_Distant(SecondDreamSceneBuilder가 놓음, 참조하는 스크립트 없음)만 수영모 인외로 바꾼다.
    // VARCO 모델(PoolHall/Prefabs/VAR_PoolHall_SwimCapFigure, 원점 발밑, 정면 +Z)이 오면 마네킹 렌더러를 끄고 같은 자리·방향에 세운다.
    const string SwimCapName = "VARCO_SwimCapFigure";
    static void SwapDistantMannequin()
    {
        if (VarcoPrefab("PoolHall", "SwimCapFigure") == null) return;
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || t.name != "Mannequin_Distant") continue;
            var old = t.Find(SwimCapName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            uint layer = 1u;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) { layer = r.renderingLayerMask; r.enabled = false; EditorUtility.SetDirty(r); }
            var go = VarcoInto(t, "PoolHall", "SwimCapFigure", layer, default, default, false);
            go.name = SwimCapName;
            var clean = SwimCapCleanMesh(out var mats); // 떠 있는 조각 빼고 물안경 새로(GreyboxGraphicsPass.SwimCap.cs)
            if (clean != null)
            {
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) mr.enabled = false;
                // 2026-11-24 2차: 얼굴 앞 빛 판 대신 얼굴을 지운 듯 매끈한 살로(사용자 선택 "문질러 지운 얼굴", Feedback1124b.cs)
                var erased = SwimCapErasedMesh(clean);
                if (erased != null) { mats[1].SetFloat("_Smoothness", 0.05f); EditorUtility.SetDirty(mats[1]); } // 지운 얼굴에 반짝임(굴곡 단서)이 없게
                var useMats = mats;
                if (erased != null && erased.subMeshCount > mats.Length)
                {
                    // 4차: 얼굴 칸 = 검은 무광(얼굴이 안 보이게)
                    var voidMat = Lit("GB_SwimCap_FaceVoid", m => Plain(m, SwimVoidColor, 0f));
                    useMats = new Material[mats.Length + 1]; mats.CopyTo(useMats, 0); useMats[mats.Length] = voidMat;
                }
                MeshPart(go.transform, "Clean", erased ?? clean, mats[0], layer, 1f).GetComponent<MeshRenderer>().sharedMaterials = useMats;
            }
            if (SwimFaceGlowOn) AddSwimFaceGlow(go.transform, layer); // (꺼 둠) 남은 얼굴 깨짐을 빛 번짐으로 가리던 판
            AddSwimFaceShade(go.transform, layer); // 4차: 얼굴을 덮는 부드러운 검은 그림자(Feedback1124b.cs)
            n++;
        }
        Debug.Log($"[GreyboxGraphicsPass] 수영장 홀 끝 마네킹 → 수영모 인외 {n}개");
    }
}
