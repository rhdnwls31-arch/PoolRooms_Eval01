using UnityEditor;
using UnityEngine;

// 2026-11-27 사운드 단계 2차 플레이 피드백 중 소리 밖의 것.
// 1) 처음 터널에서 멀리 보이는 출구 벽(GB_00_IntroExit)과 그 너머 수영장 홀이 30m 밖이라 그려지지 않아 하늘만 보였다 → 멀리서도 그린다.
// 2) 수영장 홀 입구·노란 방 천장(같은 천장 타일)이 프레임마다 깨져 보였다: 반점 무늬에 매끈함 0.5 + 화면 반사(SSR)가 겹쳐 반사 잡음이 끓었다
//    → 천장답게 거칠게(0.12), 화면 반사 받지 않게.
public static partial class GreyboxGraphicsPass
{
    static readonly string[] FarVisibleRoots = { "GB_00_IntroExit", "GB_01_PoolHall" };
    static readonly string[] MatteCeilings = { "GB_Hall_EntryCeiling", "GB_Yellow_CeilingTile" };

    static void ApplyFeedback1127b()
    {
        var rc = Object.FindAnyObjectByType<RoomCulling>(FindObjectsInactive.Include);
        if (rc != null)
        {
            rc.farRoots = FarVisibleRoots;
            rc.farDistance = 90f;
            EditorUtility.SetDirty(rc);
        }
        else Debug.LogWarning("[1127b] RoomCulling 없음");

        foreach (var n in MatteCeilings)
        {
            var guids = AssetDatabase.FindAssets(n + " t:Material");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != n) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
                if (m.HasProperty("_SmoothnessRemapMax")) m.SetFloat("_SmoothnessRemapMax", 0.12f);
                if (m.HasProperty("_ReceivesSSR")) m.SetFloat("_ReceivesSSR", 0f);
                m.EnableKeyword("_DISABLE_SSR");
                ValidateHdMaterial(m);
                EditorUtility.SetDirty(m);
            }
        }
        AssetDatabase.SaveAssets();

        // 3차 피드백(같은 날)
        // 천장이 여전히 프레임마다 깨짐: 반사 문제가 아니라 계단 현상. 카메라가 SMAA(가장자리만 다듬음)라
        // 천장 타일의 가는 검은 줄무늬가 움직일 때마다 픽셀 단위로 깜빡였다 → 프레임을 이어 다듬는 TAA로 바꾸고, 줄무늬 텍스처 축소도 부드럽게.
        var cam = Camera.main;
        if (cam != null && cam.TryGetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>(out var hd))
        {
            hd.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            hd.TAAQuality = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.TAAQualityLevel.High;
            hd.taaSharpenStrength = 0.6f;
            EditorUtility.SetDirty(hd);
        }
        foreach (var texName in new[] { "GB_CeilingTile_Albedo", "GB_CeilingTile_Normal", "GB_CeilingTile_Mask" })
            foreach (var g in AssetDatabase.FindAssets(texName + " t:Texture"))
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g)) as TextureImporter;
                if (ti == null || ti.mipmapFilter == TextureImporterMipFilter.KaiserFilter) continue;
                ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                ti.SaveAndReimport();
            }

        // 거실 TV: 찾은 뒤 6초 기다리면 "밝은 화면으로 바뀌려다 원래로 돌아오는" 것처럼 보였다 → 찾자마자 이어서 밝은 화면으로 바뀐다.
        foreach (var g in Object.FindObjectsByType<GazeTarget>(FindObjectsInactive.Include))
            if (g.name == "Oddity_TV") { g.hideDelay = 0f; EditorUtility.SetDirty(g); }

        // 예전 막 전환 소리 결과(더는 쓰지 않음)
        AssetDatabase.DeleteAsset("Assets/Audio/Game/transition.wav");
    }

    /// <summary>HDRP 재질 키워드·스텐실을 속성에 맞게 다시 맞춘다(HDMaterial.ValidateMaterial, 이름공간이 버전마다 달라 찾아서 부른다).</summary>
    static void ValidateHdMaterial(Material m)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("UnityEngine.Rendering.HighDefinition.HDMaterial") ?? asm.GetType("UnityEditor.Rendering.HighDefinition.HDMaterial");
            var mi = t?.GetMethod("ValidateMaterial", new[] { typeof(Material) });
            if (mi == null) continue;
            mi.Invoke(null, new object[] { m });
            return;
        }
        Debug.LogWarning("[1127b] HDMaterial.ValidateMaterial 못 찾음: " + m.name);
    }
}
