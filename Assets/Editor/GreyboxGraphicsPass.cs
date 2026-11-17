using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 그레이박스 씬(Scene_DescentGreybox) 그래픽 다듬기. production/fable-graphics-checklist.md 참고.
// 실행: Unity.exe -batchmode -projectPath . -executeMethod GreyboxGraphicsPass.DumpBatch -quit
public static partial class GreyboxGraphicsPass
{
    public const string ScenePath = "Assets/Scenes/Greybox/Scene_DescentGreybox.unity";

    /// <summary>씬 구조(루트, 방별 자식, 조명, Volume, 물)를 텍스트로 떨군다. 씬은 저장하지 않는다.</summary>
    public static void DumpBatch()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sb = new StringBuilder();
            sb.AppendLine("== ROOTS ==");
            foreach (var root in scene.GetRootGameObjects())
            {
                var b = BoundsOf(root);
                sb.AppendLine($"{root.name} active={root.activeSelf} pos={root.transform.position} children={root.transform.childCount} bounds c={b.center} s={b.size}");
            }
            sb.AppendLine("== GB TREES ==");
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.StartsWith("GB_")) DumpTree(root.transform, 0, sb, root.name.Contains("PoolHall") || root.name.Contains("SameIntro") ? 2 : 6);
            sb.AppendLine("== LIGHTS ==");
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var hd = l.GetComponent<HDAdditionalLightData>();
                sb.AppendLine($"{PathOf(l.transform)} type={l.type} on={l.enabled && l.gameObject.activeInHierarchy} pos={l.transform.position} rot={l.transform.eulerAngles} color={l.color} intensity={l.intensity} unit={l.lightUnit} range={l.range} shadows={l.shadows} hd={(hd != null)}");
            }
            sb.AppendLine("== VOLUMES ==");
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"{PathOf(v.transform)} global={v.isGlobal} prio={v.priority} on={v.enabled} profile={(v.sharedProfile ? AssetDatabase.GetAssetPath(v.sharedProfile) : "null")} blend={v.blendDistance}");
            sb.AppendLine("== WATER ==");
            foreach (var w in Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"{PathOf(w.transform)} pos={w.transform.position} scale={w.transform.lossyScale} type={w.surfaceType} caustics={w.caustics} cInt={w.causticsIntensity} refr={w.refractionColor} scat={w.scatteringColor} absorb={w.absorptionDistance}");
            sb.AppendLine("== CAMERAS / PROBES / DECALS ==");
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"CAM {PathOf(c.transform)} pos={c.transform.position} fov={c.fieldOfView} far={c.farClipPlane}");
            foreach (var p in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"PROBE {PathOf(p.transform)} pos={p.transform.position} size={p.size} mode={p.mode}");
            sb.AppendLine("== MATERIALS IN USE (shader) ==");
            var seen = new System.Collections.Generic.Dictionary<Material, int>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials) { if (m == null) continue; seen.TryGetValue(m, out int n); seen[m] = n + 1; }
            foreach (var kv in seen)
                sb.AppendLine($"{kv.Key.name} x{kv.Value} shader={kv.Key.shader.name} supported={kv.Key.shader.isSupported} path={AssetDatabase.GetAssetPath(kv.Key)}");
            sb.AppendLine("== CUSTOM SHADER RENDERERS ==");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                    if (m != null && !m.shader.name.StartsWith("HDRP/"))
                        sb.AppendLine($"{PathOf(r.transform)} shader={m.shader.name} rendererEnabled={r.enabled} activeInHierarchy={r.gameObject.activeInHierarchy} passes={m.passCount} firstPassLightMode={m.GetPassName(0)}");
            sb.AppendLine("== MONOBEHAVIOURS (non-unity) ==");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var ns = mb.GetType().Namespace ?? "";
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("Unity.")) continue;
                sb.AppendLine($"{mb.GetType().Name} @ {PathOf(mb.transform)} pos={mb.transform.position}");
            }
            sb.AppendLine("== RENDER SETTINGS ==");
            sb.AppendLine($"pipeline={GraphicsSettings.defaultRenderPipeline?.name} quality={QualitySettings.renderPipeline?.name}");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/greybox-dump.txt", sb.ToString());
            Debug.Log("[GreyboxGraphicsPass] dump 완료");
        }
        catch (System.Exception e) { Debug.LogError("[GreyboxGraphicsPass] dump 실패: " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static void DumpTree(Transform t, int depth, StringBuilder sb, int maxDepth)
    {
        var r = t.GetComponent<Renderer>();
        var c = t.GetComponent<Collider>();
        string mat = r && r.sharedMaterial ? r.sharedMaterial.name : "-";
        string comps = "";
        foreach (var comp in t.GetComponents<Component>())
            if (comp != null && !(comp is Transform) && !(comp is MeshFilter) && !(comp is MeshRenderer)) comps += comp.GetType().Name + ",";
        sb.AppendLine($"{new string(' ', depth * 2)}{t.name} pos={t.position} rot={t.eulerAngles} scale={t.lossyScale} mat={mat} col={(c ? (c.isTrigger ? "trigger" : "solid") : "-")} [{comps}] kids={t.childCount}");
        if (depth >= maxDepth) return;
        foreach (Transform ch in t) DumpTree(ch, depth + 1, sb, maxDepth);
    }

    static string PathOf(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
