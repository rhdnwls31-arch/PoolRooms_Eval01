using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Visual Polish 3~4단계를 한 번에 적용하는 에디터 스크립트.
//   3단계: Scene_Intro 전용 후처리 프로파일(Intro_PostFX) + 전역 Volume 추가.
//          Horror_PostFX.asset은 수정하지 않고 복사본을 만들어 값만 낮춘다.
//          (CollapseSequence의 눈꺼풀 Vignette는 런타임에 priority 100 Volume을 따로 만들기
//           때문에 이 Volume과 충돌하지 않는다.)
//   4단계: Scene_SecondDream 안개색을 밝은 회청색으로 변경(거리는 그대로).
//          Scene_Intro 안개(밀도 0.012)는 화면을 보고 정하므로 이번에는 건드리지 않는다.
//
// 각 단계는 따로도 실행할 수 있다. 실행: 메뉴 [DEPTH 5m > Visual Polish > ...]
public static class VisualPolishStage34
{
    const string IntroScenePath = "Assets/Scenes/Scene_Intro.unity";
    const string SecondDreamScenePath = "Assets/Scenes/Scene_SecondDream.unity";
    const string HorrorProfilePath = "Assets/Settings/Horror_PostFX.asset";
    const string IntroProfilePath = "Assets/Settings/Intro_PostFX.asset";
    const string VolumeObjectName = "PostFX Volume";

    // 권장 시작값(문서 3단계). 화면을 보고 조정한 뒤 여기 값을 갱신한다.
    const float BloomIntensity = 0.3f;     // Horror 0.7 → 구체만 부드럽게 빛나게
    const float Saturation = -15f;         // Horror -25
    const float VignetteIntensity = 0.18f; // Horror 0.2
    const float FilmGrainIntensity = 0.07f;// Horror 0.1
    const float ChromaticAberration = 0.02f;// Horror 0.03

    [MenuItem("DEPTH 5m/Visual Polish/Apply Stage 3 + 4 (Intro Volume + Hall Fog)")]
    public static void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyStage3Internal();
        ApplyStage4Internal();
        Debug.Log("[VisualPolish] 3~4단계 적용 완료. Scene_Intro/Scene_SecondDream 각각 Play로 확인.");
    }

    [MenuItem("DEPTH 5m/Visual Polish/Apply Stage 3 only (Intro Volume)")]
    public static void ApplyStage3Only()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyStage3Internal();
    }

    [MenuItem("DEPTH 5m/Visual Polish/Apply Stage 4 only (Hall Fog Color)")]
    public static void ApplyStage4Only()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyStage4Internal();
    }

    static void ApplyStage3Internal()
    {
        var profile = EnsureIntroProfile();
        if (profile == null) return;

        var scene = EditorSceneManager.OpenScene(IntroScenePath, OpenSceneMode.Single);
        GameObject go = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == VolumeObjectName) go = root;
        if (go == null)
        {
            go = new GameObject(VolumeObjectName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        }
        var volume = go.GetComponent<Volume>();
        if (volume == null) volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f; // 눈꺼풀 Vignette(priority 100)가 항상 위에 덮인다
        volume.sharedProfile = profile;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[VisualPolish] 3단계: Scene_Intro에 Intro_PostFX Volume 적용.");
    }

    static VolumeProfile EnsureIntroProfile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(IntroProfilePath);
        if (existing == null)
        {
            if (!AssetDatabase.CopyAsset(HorrorProfilePath, IntroProfilePath))
            {
                Debug.LogError("[VisualPolish] Horror_PostFX 복사 실패 — 중단.");
                return null;
            }
            AssetDatabase.Refresh();
            existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(IntroProfilePath);
        }

        // 복사본의 값만 낮춘다(항상 이 값으로 덮어써서 재실행해도 같은 결과).
        if (existing.TryGet(out Bloom bloom))
        {
            bloom.intensity.overrideState = true;
            bloom.intensity.value = BloomIntensity;
        }
        if (existing.TryGet(out ColorAdjustments ca))
        {
            ca.saturation.overrideState = true;
            ca.saturation.value = Saturation;
        }
        if (existing.TryGet(out Vignette vig))
        {
            vig.intensity.overrideState = true;
            vig.intensity.value = VignetteIntensity;
        }
        if (existing.TryGet(out FilmGrain fg))
        {
            fg.intensity.overrideState = true;
            fg.intensity.value = FilmGrainIntensity;
        }
        if (existing.TryGet(out ChromaticAberration chroma))
        {
            chroma.intensity.overrideState = true;
            chroma.intensity.value = ChromaticAberration;
        }
        // Tonemapping(Neutral), Bloom Threshold(0.9), Exposure(-0.2), Motion Blur는 Horror 값 유지.

        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        return existing;
    }

    static void ApplyStage4Internal()
    {
        var scene = EditorSceneManager.OpenScene(SecondDreamScenePath, OpenSceneMode.Single);
        SecondDreamSceneBuilder.ApplyFogColorToOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[VisualPolish] 4단계: Scene_SecondDream 안개색 → 밝은 회청색(거리 20~38m 유지).");
    }
}
