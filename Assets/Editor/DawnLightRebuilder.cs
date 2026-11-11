using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scene_Intro의 "DawnLight"(디렉셔널 라이트)를 삭제하고 똑같은 설정으로 새로 만든다.
//
// 왜: 선택할 때마다 콘솔에 "Assertion failed on expression: 'IsNormalized(dir, 0.0001f)'"
// (네이티브 콜스택: ComputeShadowSplitMasksForOneLight)가 반복적으로 떴다. 그림자를 꺼도,
// Scene 뷰 카메라 각도를 바꿔도 멈추지 않았다 — 저장된 수치 자체(회전/그림자 설정 등)는
// 전부 정상이라, 이 라이트 컴포넌트에 들러붙은 무언가(에디터 내부 캐시 등, 씬 YAML에는
// 보이지 않는 상태)가 원인으로 추정된다. 가장 확실한 해결책은 컴포넌트를 통째로 새로
// 만드는 것.
//
// 지금 열려 있는 씬(보통 Scene_Intro)에서 바로 동작한다 — 씬을 별도로 열지 않는다,
// 지금 에디터에서 작업 중인 세션을 방해하지 않기 위해서.
//
// 실행: 메뉴 [DEPTH 5m > Rebuild DawnLight]
public static class DawnLightRebuilder
{
    const string LightName = "DawnLight";
    const string ParentName = "Lighting";
    const float ShadowStrength = 0.85f; // 완전한 검정 그림자 대신 새벽빛에 어울리는 약간 옅은 그림자

    // Visual Polish 2단계 — 라이트를 새로 만들지 않고(= 저장된 값 유지) 그림자 설정만 켠다.
    // 열려 있는 Scene_Intro에서 실행. 켠 직후 Console에 IsNormalized/Assertion 오류가 뜨면 멈추고 보고.
    [MenuItem("DEPTH 5m/Enable DawnLight Shadows")]
    public static void EnableShadows()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Scene_Intro")
        {
            Debug.LogError($"[DawnLightRebuilder] 활성 씬이 Scene_Intro가 아니다({scene.name}) — 중단.");
            return;
        }
        Transform parent = FindInScene(scene, ParentName);
        var t = parent != null ? parent.Find(LightName) : null;
        var light = t != null ? t.GetComponent<Light>() : null;
        if (light == null) { Debug.LogError("[DawnLightRebuilder] Lighting/DawnLight를 찾지 못했다."); return; }

        Undo.RecordObject(light, "Enable DawnLight Shadows");
        light.shadows = LightShadows.Soft;
        light.shadowStrength = ShadowStrength;
        EditorUtility.SetDirty(light);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DawnLightRebuilder] DawnLight 그림자 ON (Soft, Strength " + ShadowStrength + "). 점/스팟 그림자는 그대로 OFF.");
    }

    [MenuItem("DEPTH 5m/Rebuild DawnLight")]
    public static void Rebuild()
    {
        var scene = EditorSceneManager.GetActiveScene();

        Transform parent = FindInScene(scene, ParentName);
        if (parent == null)
        {
            Debug.LogError($"[DawnLightRebuilder] '{ParentName}' 오브젝트를 활성 씬({scene.name})에서 찾지 못했다 — Scene_Intro가 열려 있는지 확인할 것.");
            return;
        }

        var old = parent.Find(LightName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject(LightName);
        Undo.RegisterCreatedObjectUndo(go, "Rebuild DawnLight");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 8f, 0f);
        go.transform.localRotation = Quaternion.Euler(20f, -38f, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.88f, 0.75f, 1f);
        light.intensity = 1.8f;
        light.shadows = LightShadows.Soft; // Visual Polish 2단계: 방향광 그림자 ON (점/스팟 그림자는 계속 OFF)
        light.shadowStrength = ShadowStrength;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DawnLightRebuilder] DawnLight 재생성 완료.");

        Selection.activeGameObject = go;
    }

    static Transform FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
            var t = root.transform.Find(name);
            if (t != null) return t;
        }
        return null;
    }
}
