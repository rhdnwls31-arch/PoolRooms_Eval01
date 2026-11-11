using System.IO;
using UnityEditor;
using UnityEngine;

// Visual Polish 후속 조정 1 — 플레이 피드백 반영.
//   (a) 발광 구체가 흰 공처럼 보임 → 베이스 색을 어둡게, 에미션 세기를 낮춰 Bloom이 은은하게 번지게.
//   (b) 데크 타일 줄눈이 너무 진함 → 데크 전용 "옅은 줄눈" 텍스처(tile_deck_soft.png)를 만들어 교체.
//       원본 tile_pool.png는 수영장 안쪽용으로 그대로 둔다.
// 멱등. 실행: [DEPTH 5m > Visual Polish > Apply Tweak 1 (Orb + Deck Grout)]
public static class VisualPolishTweak1
{
    const string OrbMatPath = "Assets/Materials/Env/Env_GlowOrb.mat";
    const string DeckMatPath = "Assets/Materials/Env/Env_Court_Deck.mat";
    const string TilePath = "Assets/Textures/Env/tile_pool.png";
    const string SoftTilePath = "Assets/Textures/Env/tile_deck_soft.png";

    // 발광 구체: 이전 base(0.65,0.85,1) emission x2.2 → 흰색으로 클리핑됨.
    static readonly Color OrbBase = new Color(0.30f, 0.50f, 0.66f);
    static readonly Color OrbEmission = new Color(0.30f, 0.62f, 1.0f);
    const float OrbEmissionIntensity = 1.5f;

    // 줄눈 옅게: 픽셀을 평균 밝기 쪽으로 이 비율만큼 당긴다(0=원본, 1=줄눈 완전 제거).
    const float GroutSoften = 0.55f;

    [MenuItem("DEPTH 5m/Visual Polish/Apply Tweak 1 (Orb + Deck Grout)")]
    public static void Apply()
    {
        TweakOrb();
        TweakDeck();
        AssetDatabase.SaveAssets();
        Debug.Log("[VisualPolish] 조정 1 적용: 발광 구체 + 데크 줄눈.");
    }

    public static void ApplyBatch()
    {
        try { Apply(); }
        catch (System.Exception e) { Debug.LogError("[VisualPolish] 조정 1 실패: " + e); EditorApplication.Exit(1); }
    }

    static void TweakOrb()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(OrbMatPath);
        if (m == null) { Debug.LogWarning("[VisualPolish] Env_GlowOrb 없음"); return; }
        m.SetColor("_BaseColor", OrbBase);
        m.SetColor("_EmissionColor", OrbEmission * OrbEmissionIntensity);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
        m.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);
    }

    static void TweakDeck()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(DeckMatPath);
        if (m == null) { Debug.LogWarning("[VisualPolish] Env_Court_Deck 없음"); return; }

        if (!File.Exists(SoftTilePath)) BuildSoftTile();
        var soft = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftTilePath);
        if (soft == null) { Debug.LogWarning("[VisualPolish] tile_deck_soft.png 생성 실패 — 데크 변경 생략"); return; }

        m.SetTexture("_BaseMap", soft);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", soft);
        EditorUtility.SetDirty(m);
    }

    static void BuildSoftTile()
    {
        // 원본 PNG 바이트를 직접 읽어 디코드한다(임포트 설정의 Read/Write 여부와 무관).
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(TilePath));
        var px = src.GetPixels();

        Color avg = Color.black;
        foreach (var c in px) avg += c;
        avg /= px.Length;

        for (int i = 0; i < px.Length; i++)
        {
            var c = Color.Lerp(px[i], avg, GroutSoften);
            c.a = 1f;
            px[i] = c;
        }
        src.SetPixels(px);
        src.Apply();
        File.WriteAllBytes(SoftTilePath, src.EncodeToPNG());
        Object.DestroyImmediate(src);
        AssetDatabase.ImportAsset(SoftTilePath);
    }
}
