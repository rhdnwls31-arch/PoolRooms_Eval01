using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scene_Intro 배치 점검 — "오브젝트 끼임 / 위치값 오류"를 눈이 아니라 숫자로 잡는다(2026-11-12).
//   1) 소품(Dummies 그룹)의 경계가 구조물(Environment 콜라이더)과 겹치는가
//   2) 소품끼리 겹치는가 (의도된 겹침: 수건/라디오, 물 위 구체/플라밍고는 제외)
//   3) 소품 아래에 바닥이 있는가 / 엉뚱한 높이에 얹혀 있는가
//   4) 스폰이 물속이 아닌 바닥인가
//   5) 플레이어 캡슐(CharacterController)을 실제로 이동시켜 경로가 막히지 않는지(계단/아치/터널)
// 결과는 Console 로그. 실행: [DEPTH 5m > Audit Intro Layout]
//   또는 Unity.exe -batchmode -projectPath . -executeMethod IntroMapAudit.AuditBatch
public static class IntroMapAudit
{
    const string ScenePath = "Assets/Scenes/Scene_Intro.unity";

    [MenuItem("DEPTH 5m/Audit Intro Layout")]
    public static void Audit() { Run(); }

    public static void AuditBatch()
    {
        try { Run(); }
        catch (System.Exception e) { Debug.LogError("[IntroAudit] 실패: " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();
        var sb = new StringBuilder("[IntroAudit] 결과\n");

        GameObject env = null, dummies = null, player = null;
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name == "Environment") env = r;
            else if (r.name == "Dummies") dummies = r;
            else if (r.name == "Player") player = r;
        }
        if (env == null || dummies == null) { Debug.LogError("[IntroAudit] Environment/Dummies 없음"); return; }

        // ---- 1·3) 소품 vs 구조물, 바닥 확인 ----
        int overlapCount = 0, floorIssues = 0;
        var bounds = new List<(string name, Bounds b)>();
        foreach (Transform g in dummies.transform)
        {
            var rends = g.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) continue;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            bounds.Add((g.name, b));

            // 소품 최상단(파라솔 캐노피 등 머리 위)은 제외하고 사람 키 높이 구간만 본다.
            var testMin = new Vector3(b.min.x + 0.08f, Mathf.Max(b.min.y, -0.2f) + 0.05f, b.min.z + 0.08f);
            var testMax = new Vector3(b.max.x - 0.08f, Mathf.Min(b.max.y, 1.8f), b.max.z - 0.08f);
            if (testMax.y > testMin.y && testMax.x > testMin.x && testMax.z > testMin.z)
            {
                var center = (testMin + testMax) / 2f; var half = (testMax - testMin) / 2f;
                foreach (var c in Physics.OverlapBox(center, half))
                {
                    if (c.isTrigger || c.transform.IsChildOf(g) || !c.transform.IsChildOf(env.transform)) continue;
                    if (IsFloorLike(c.name)) continue;
                    // 물 속 소품(야자수/기둥)은 수영장 바닥·벽 안으로 파고드는 게 정상이지만 계단/벽과는 겹치면 안 된다.
                    sb.AppendLine($"  겹침: {g.name} ↔ {c.name}");
                    overlapCount++;
                }
            }

            // 소품 중심 아래 바닥
            if (!g.name.Contains("Towels") && !g.name.Contains("Radio"))
            {
                var o = new Vector3(g.position.x, 8f, g.position.z);
                if (Physics.Raycast(o, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                {
                    bool ok = hit.collider.transform.IsChildOf(env.transform) && hit.point.y < 0.45f && hit.point.y > -1.2f
                              || hit.collider.transform.IsChildOf(g);
                    // 바닥이 아니라 코핑/계단 위에 얹힌 경우를 잡는다.
                    if (hit.collider.name.Contains("Curb") && !g.name.Contains("Lounger")) { ok = false; }
                    if (!ok) { sb.AppendLine($"  바닥 이상: {g.name} 아래 → {hit.collider.name} (y {hit.point.y:0.00})"); floorIssues++; }
                }
                else { sb.AppendLine($"  바닥 없음: {g.name} 아래에 아무것도 없음"); floorIssues++; }
            }
        }

        // ---- 2) 소품끼리 ----
        int propPair = 0;
        for (int i = 0; i < bounds.Count; i++)
            for (int j = i + 1; j < bounds.Count; j++)
            {
                string a = bounds[i].name, b2 = bounds[j].name;
                if (a.Contains("Orb") || b2.Contains("Orb") || a.Contains("Towels") || b2.Contains("Towels") || a.Contains("Radio") || b2.Contains("Radio")) continue;
                var ba = bounds[i].b; var bb = bounds[j].b;
                // 위에서 본 평면 겹침만(캐노피 등 높은 부분은 y 제한)
                var ca = new Bounds(ba.center, new Vector3(ba.size.x - 0.1f, 1f, ba.size.z - 0.1f));
                var cb = new Bounds(bb.center, new Vector3(bb.size.x - 0.1f, 1f, bb.size.z - 0.1f));
                ca.center = new Vector3(ba.center.x, 0.5f, ba.center.z); cb.center = new Vector3(bb.center.x, 0.5f, bb.center.z);
                if (ca.Intersects(cb)) { sb.AppendLine($"  소품끼리 겹침: {a} ↔ {b2}"); propPair++; }
            }

        sb.AppendLine($"  요약: 구조물 겹침 {overlapCount}건, 바닥 이상 {floorIssues}건, 소품끼리 {propPair}건 (소품 {bounds.Count}개)");

        // ---- 4) 스폰 ----
        if (player != null)
        {
            var p = player.transform.position;
            foreach (var h2 in System.Linq.Enumerable.OrderByDescending(Physics.RaycastAll(new Vector3(p.x, 6f, p.z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore), h => h.point.y))
            {
                if (h2.collider.transform.IsChildOf(player.transform)) continue;   // 자기 캡슐은 제외
                sb.AppendLine($"  스폰 {p}: 아래 바닥 {h2.collider.name} y={h2.point.y:0.00} {(h2.point.y > -0.05f ? "OK" : "← 물속/구덩이!")}");
                break;
            }
        }

        // ---- 5) 이동 시뮬레이션 ----
        sb.AppendLine("  경로 시뮬레이션(CharacterController 캡슐):");
        sb.Append(SimulatePaths(player != null ? player.transform.position : new Vector3(0, 0, 22)));

        Debug.Log(sb.ToString());
    }

    static bool IsFloorLike(string n)
    {
        return n.EndsWith("_Floor") || n.StartsWith("Deck_") || n.Contains("Floor") && !n.Contains("Pool_Wall");
    }

    struct Path { public string name; public Vector3[] pts; }

    static string SimulatePaths(Vector3 spawn)
    {
        var paths = new[]
        {
            new Path { name = "스폰→수영장 계단→수영장 속→북쪽 계단→북 아치→터널→목표 방", pts = new[] {
                new Vector3(0,0,15), new Vector3(0,-0.9f,6), new Vector3(0,-0.9f,-8), new Vector3(0,-0.9f,-12),
                new Vector3(0,0,-16), new Vector3(0,0,-26), new Vector3(0,0,-38), new Vector3(0,0,-47), new Vector3(0,0,-56) } },
            new Path { name = "스폰→동쪽 아치→수로 홀 둘레", pts = new[] {
                new Vector3(14,0,8), new Vector3(18,0,3), new Vector3(24,0,0), new Vector3(24,0,-6.5f), new Vector3(34,0,-6.5f), new Vector3(46,0,-6.5f), new Vector3(46,0,0) } },
            new Path { name = "동쪽 아치→수로 계단→수로를 걸어 건너기", pts = new[] {
                new Vector3(14,0,8), new Vector3(24,0,0), new Vector3(30,-0.6f,0), new Vector3(42,-0.6f,0), new Vector3(48,0,0) } },
            new Path { name = "스폰→서쪽 아치→석호 정원", pts = new[] {
                new Vector3(-14,0,8), new Vector3(-18,0,3), new Vector3(-24,0,0), new Vector3(-26,0,9), new Vector3(-36,0,11) } },
            new Path { name = "수영장 둘레 걷기(데크 한 바퀴)", pts = new[] {
                new Vector3(8,0,20), new Vector3(8,0,-20), new Vector3(-8,0,-20), new Vector3(-8,0,20) } },
        };

        var sb = new StringBuilder();
        var go = new GameObject("__AuditCC");
        try
        {
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.3f; cc.slopeLimit = 45f;
            foreach (var path in paths)
            {
                go.transform.position = spawn;
                Physics.SyncTransforms();
                bool ok = true; string why = "";
                for (int wi = 0; wi < path.pts.Length && ok; wi++)
                {
                    var target = path.pts[wi];
                    float vy = 0f; Vector3 last = go.transform.position; int still = 0; int it = 0;
                    while (it++ < 3000)
                    {
                        var pos = go.transform.position;
                        var d = target - pos; d.y = 0;
                        if (d.magnitude < 0.5f) break;
                        var v = d.normalized * 3f;
                        vy = cc.isGrounded ? -1f : vy + Physics.gravity.y * 0.02f;
                        v.y = vy;
                        cc.Move(v * 0.02f);
                        if ((go.transform.position - last).sqrMagnitude < 1e-6f) still++; else { still = 0; last = go.transform.position; }
                        if (still > 40) { ok = false; why = $"웨이포인트 {wi + 1} {target} 앞 {go.transform.position:0.0}에서 막힘"; break; }
                    }
                    if (it >= 3000 && ok) { ok = false; why = $"웨이포인트 {wi + 1} {target} 시간 초과(현재 {go.transform.position:0.0})"; }
                }
                sb.AppendLine($"    {(ok ? "통과" : "막힘")}: {path.name}{(ok ? "" : " — " + why)}");
            }
        }
        finally { Object.DestroyImmediate(go); }
        return sb.ToString();
    }
}
