using System.IO;
using UnityEditor;
using UnityEngine;

// 그레이박스 재질용 절차 생성 텍스처. 외부 에셋을 쓰지 않고 코드로 만든다(모두 가로세로 반복 가능).
public static partial class GreyboxGraphicsPass
{
    const string TexDir = "Assets/Textures/Greybox";

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
        }
    }

    /// <summary>GLSL식 smoothstep: x가 e0에서 e1로 갈 때 0에서 1. (Mathf.SmoothStep은 인자 뜻이 다르다.)</summary>
    static float SStep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

    // 주기 period로 반복되는 값 노이즈. x, y는 격자 단위.
    static float VNoise(float x, float y, int period, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(Mod(xi, period), Mod(yi, period), seed), b = Hash(Mod(xi + 1, period), Mod(yi, period), seed);
        float c = Hash(Mod(xi, period), Mod(yi + 1, period), seed), d = Hash(Mod(xi + 1, period), Mod(yi + 1, period), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    // u, v는 0~1. 결과 0~1.
    static float Fbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * VNoise(u * period, v * period, period, seed + i * 17);
            norm += amp; amp *= 0.5f; period *= 2;
        }
        return sum / norm;
    }

    delegate void PixelFn(float u, float v, out Color albedo, out float height, out Color mask);

    enum TexKind { Color, Height, Mask, Alpha }

    /// <summary>알베도·높이(노멀로 변환)·마스크 텍스처 세 장을 만들어 저장한다. 반환: (albedo, normal, mask).</summary>
    static (Texture2D, Texture2D, Texture2D) MakeSet(string name, int size, float bump, PixelFn fn)
    {
        Directory.CreateDirectory(TexDir);
        var a = new Color[size * size]; var h = new Color[size * size]; var m = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                fn((x + 0.5f) / size, (y + 0.5f) / size, out var col, out float hh, out var mk);
                a[y * size + x] = col; h[y * size + x] = new Color(hh, hh, hh, 1); m[y * size + x] = mk;
            }
        return (Save(name + "_Albedo", size, a, TexKind.Color, 0), Save(name + "_Normal", size, h, TexKind.Height, bump), Save(name + "_Mask", size, m, TexKind.Mask, 0));
    }

    static Texture2D Save(string name, int size, Color[] px, TexKind kind, float bump, bool clamp = false)
    {
        Directory.CreateDirectory(TexDir);
        string path = $"{TexDir}/{name}.png";
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false, kind != TexKind.Color && kind != TexKind.Alpha);
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        imp.anisoLevel = 8; imp.mipmapEnabled = true; imp.maxTextureSize = 2048;
        if (kind == TexKind.Height) { imp.textureType = TextureImporterType.NormalMap; imp.convertToNormalmap = true; imp.heightmapScale = bump; }
        else { imp.textureType = TextureImporterType.Default; imp.sRGBTexture = kind == TexKind.Color || kind == TexKind.Alpha; imp.alphaIsTransparency = kind == TexKind.Alpha; }
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 작은 정사각 타일. tiles = 텍스처 한 장에 들어가는 타일 수(한 변).
    static (Texture2D, Texture2D, Texture2D) TileSet(string name, int tiles, Color tile, Color grout, Color stain, float stainAmount, int seed)
    {
        const int size = 1024;
        float groutW = 0.045f; // 타일 한 칸 대비 줄눈 폭
        return MakeSet(name, size, 0.03f, (float u, float v, out Color col, out float h, out Color mk) =>
        {
            float tu = u * tiles, tv = v * tiles;
            int ix = Mathf.FloorToInt(tu), iy = Mathf.FloorToInt(tv);
            float fx = tu - ix, fy = tv - iy;
            float edge = Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy));
            float t = SStep(0, 1, Mathf.InverseLerp(groutW * 0.5f, groutW * 1.6f, edge)); // 0=줄눈, 1=타일
            float per = Hash(ix, iy, seed);
            float dirt = Fbm(u, v, 4, 5, seed + 3);
            float blot = SStep(0.6f, 0.85f, dirt) * stainAmount;
            Color tc = tile * (0.94f + 0.08f * per);
            if (per > 0.93f) tc *= 0.9f; // 가끔 색이 다른 타일
            tc = Color.Lerp(tc, stain, blot);
            Color gc = Color.Lerp(grout, stain * 0.6f, blot + 0.25f * Fbm(u, v, 16, 3, seed + 9));
            col = Color.Lerp(gc, tc, t); col.a = 1;
            h = Mathf.Lerp(0.15f, 1f, t) - 0.03f * Fbm(u, v, 64, 2, seed + 5);
            float smooth = Mathf.Lerp(0.25f, 0.86f - 0.3f * blot - 0.1f * Fbm(u, v, 32, 3, seed + 7), t);
            mk = new Color(0, Mathf.Lerp(0.75f, 1f, t), 0, smooth);
        });
    }

    // 낡은 노란 벽지: 옅은 세로 줄무늬 + 위에서 흘러내린 물 얼룩 + 바닥 쪽 때. v=1이 벽 위쪽(텍스처 한 장 = 벽 높이).
    static (Texture2D, Texture2D, Texture2D) WallpaperSet()
    {
        var baseCol = new Color(0.9f, 0.8f, 0.44f); var stain = new Color(0.55f, 0.42f, 0.18f);
        return MakeSet("GB_Wallpaper", 1024, 0.012f, (float u, float v, out Color col, out float h, out Color mk) =>
        {
            float stripe = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 24f);
            float seam = Mathf.Abs(Mathf.Repeat(u * 6f, 1f) - 0.5f) > 0.492f ? 1f : 0f;        // 0.5m마다 벽지 이음선
            float n = Fbm(u, v, 4, 5, 21);
            float drip = Fbm(u, v * 0.25f, 16, 3, 33);                                            // 세로로 길게 늘어진 얼룩
            float top = SStep(0.55f, 1f, v) * SStep(0.35f, 0.7f, drip);
            float low = SStep(0.12f, 0f, v) * 0.5f;
            float blot = Mathf.Clamp01(top * 0.55f + low + SStep(0.62f, 0.82f, n) * 0.18f);
            col = baseCol * (0.95f + 0.05f * stripe) * (0.92f + 0.16f * n);
            col = Color.Lerp(col, stain, blot); col *= 1f - 0.25f * seam; col.a = 1;
            h = 0.5f + 0.1f * stripe - 0.3f * seam + 0.15f * Fbm(u, v, 128, 2, 5);
            mk = new Color(0, 1, 0, 0.22f + 0.2f * blot);
        });
    }

    // 눅눅한 카펫: 잔 보풀 + 큰 얼룩.
    static (Texture2D, Texture2D, Texture2D) CarpetSet()
    {
        var baseCol = new Color(0.5f, 0.41f, 0.2f); var damp = new Color(0.25f, 0.2f, 0.1f);
        return MakeSet("GB_Carpet", 1024, 0.05f, (float u, float v, out Color col, out float h, out Color mk) =>
        {
            float fuzz = Hash(Mathf.FloorToInt(u * 1024), Mathf.FloorToInt(v * 1024), 3);
            float fine = Fbm(u, v, 256, 2, 7);
            float big = Fbm(u, v, 3, 5, 41);
            float wet = SStep(0.68f, 0.85f, big);
            col = baseCol * (0.8f + 0.25f * fine + 0.12f * fuzz);
            col = Color.Lerp(col, damp, wet * 0.2f); col.a = 1;
            h = 0.6f * fine + 0.4f * fuzz;
            mk = new Color(0, 0.85f + 0.15f * fine, 0, 0.06f + 0.3f * wet);
        });
    }

    // 사무실 천장 타일(0.6m 격자, 한 장에 4칸).
    static (Texture2D, Texture2D, Texture2D) CeilingTileSet()
    {
        var baseCol = new Color(0.82f, 0.76f, 0.56f);
        return MakeSet("GB_CeilingTile", 1024, 0.03f, (float u, float v, out Color col, out float h, out Color mk) =>
        {
            float fx = Mathf.Repeat(u * 4f, 1f), fy = Mathf.Repeat(v * 4f, 1f);
            float edge = Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy));
            float t = SStep(0.012f, 0.03f, edge);
            float pit = Hash(Mathf.FloorToInt(u * 340), Mathf.FloorToInt(v * 340), 9) > 0.9f ? 1f : 0f; // 흡음판의 잔 구멍
            float n = Fbm(u, v, 4, 4, 55);
            float blot = SStep(0.7f, 0.85f, n);
            col = baseCol * (0.9f + 0.1f * n) * (1f - 0.3f * pit);
            col = Color.Lerp(col, new Color(0.5f, 0.38f, 0.16f), blot * 0.3f);
            col = Color.Lerp(new Color(0.4f, 0.37f, 0.3f), col, t); col.a = 1;
            h = t * (1f - 0.5f * pit);
            mk = new Color(0, 1, 0, 0.12f);
        });
    }

    // 차가운 대리석: 어두운 청회색 바탕, 가는 결, 2m 판 이음선. 매끈하다.
    static (Texture2D, Texture2D, Texture2D) MarbleSet(string name, Color baseCol, Color vein, float veinAmount)
    {
        return MakeSet(name, 1024, 0.01f, (float u, float v, out Color col, out float h, out Color mk) =>
        {
            float wu = u + 0.08f * (Fbm(u, v, 3, 4, 71) - 0.5f), wv = v + 0.08f * (Fbm(u, v, 3, 4, 83) - 0.5f);
            wu = Mathf.Repeat(wu, 1f); wv = Mathf.Repeat(wv, 1f);
            float a = Mathf.Abs(Fbm(wu, wv, 4, 5, 91) - 0.5f);
            float b = Mathf.Abs(Fbm(wu, wv, 6, 4, 97) - 0.5f);
            float veins = SStep(0.035f, 0f, a) * 0.8f + SStep(0.02f, 0f, b) * 0.5f;
            float cloud = Fbm(u, v, 2, 4, 61);
            float fx = Mathf.Repeat(u * 2f, 1f), fy = Mathf.Repeat(v * 2f, 1f);
            float joint = SStep(0.004f, 0.001f, Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy)));
            col = baseCol * (0.7f + 0.6f * cloud);
            col = Color.Lerp(col, vein, Mathf.Clamp01(veins) * veinAmount);
            col = Color.Lerp(col, new Color(0.03f, 0.03f, 0.04f), joint); col.a = 1;
            h = 1f - joint;
            mk = new Color(0, 1f - 0.4f * joint, 0, Mathf.Lerp(0.9f - 0.12f * cloud, 0.3f, joint));
        });
    }
}