using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-18 컴퓨터 속 맵(사용자 요청, 기획서 §6-3e): 파란 침실 → 컴퓨터 속 → 천사 방.
// 침실 책상의 켜진 모니터 앞에서 잠든 것처럼, 복도 끝 문을 지나면 컴퓨터 속 풀밭이다.
// 모습: 맑은 바탕화면 같은 하늘 아래 초록 언덕, 남쪽 끝에 반쯤 땅에 묻힌 거대한 옛날 모니터, 떠 있는 옛날 운영체제 창과 아이콘,
// 서쪽 벽 전체를 덮은 거대한 파란 화면, 하늘을 도는 종이비행기. 다른 게임의 맵은 따라 하지 않고 분위기만 빌려 우리 배치로 짰다.
// 이상한 점(§4-1): 아무도 잡지 않은 거대한 마우스 커서가 혼자 돌아다닌다. 찾으면 멈칫했다가 모니터 화면을 더블클릭하고,
// 시선을 떼면 화면이 열린다. 화면 속 빛으로 들어가면 천사 방으로 옮겨진다(TeleportZone).
// 모두 그레이박스 더미다. 나중에 VARCO 3D 모델로 바꾼다.
public static partial class GreyboxGraphicsPass
{
    const uint LComputer = 1u << 7;
    const float PcX0 = 43f, PcX1 = 73f, PcZ0 = -170f, PcZ1 = -140f, PcH = 12f, PcCx = 58f;
    static readonly Vector3 PcDoorPos = new Vector3(PcCx, 0f, PcZ1 + 0.15f);   // 북쪽 벽(두께 가운데). 파란 복도 끝 문이 여기로 이어진다
    const float ScrW = 6.4f, ScrH = 4.6f, ScrZ = -162f;                       // 거대한 모니터 화면(폭, 높이, 앞면 z). 화면 아래가 땅에 닿는다

    static void BuildComputer()
    {
        NewRoomRoot("GB_03d_Computer", out var gen);
        float K = Mathf.Pow(2f, AngelEV - 10f); // 천사 방과 같은 노출(EV 8.7)에 맞춘 밝기 배율

        // ---- 재질
        var sky = Lit("GB_PC_Sky", m =>
        {
            m.SetColor("_BaseColor", new Color(0.4f, 0.4f, 0.4f));
            m.SetTexture("_BaseColorMap", PcSkyTexture());
            m.SetFloat("_UVBase", 0f); // 면마다 0~1: 아래는 옅고 위는 진한 하늘이 벽 하나에 한 번
            m.SetFloat("_Smoothness", 0f);
            m.SetTexture("_EmissiveColorMap", m.GetTexture("_BaseColorMap"));
            m.SetFloat("_UVEmissive", 0f);
            m.SetColor("_EmissiveColor", Color.white * (950f * K));
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
        });
        var skyTop = Lit("GB_PC_SkyTop", m => Emissive(m, new Color(0.16f, 0.4f, 0.9f), 700f * K));
        var grass = Lit("GB_PC_Grass", m => Textured(m, PcGrassSet(), 6f, Color.white, 0.6f));
        var hill = Lit("GB_PC_Hill", m => Textured(m, PcGrassSet(), 9f, new Color(0.92f, 1f, 0.9f), 0.4f));
        var beige = Lit("GB_PC_Beige", m => Plain(m, new Color(0.82f, 0.79f, 0.68f), 0.25f));
        var beigeDark = Lit("GB_PC_BeigeDark", m => Plain(m, new Color(0.55f, 0.52f, 0.44f), 0.3f));
        var led = Lit("GB_PC_Led", m => Emissive(m, new Color(0.3f, 1f, 0.35f), 1500f * K));
        var screen = Lit("GB_PC_Screen", m =>
        {
            Plain(m, Color.white, 0.6f);
            m.SetTexture("_BaseColorMap", PcScreenTexture("GB_PC_ScreenText", 7));
            m.SetFloat("_UVBase", 0f);
            m.SetTexture("_EmissiveColorMap", m.GetTexture("_BaseColorMap"));
            m.SetFloat("_UVEmissive", 0f);
            m.SetColor("_EmissiveColor", Color.white * (1100f * K));
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
        });
        var desktopWall = Lit("GB_PC_DesktopWall", m =>
        {
            Plain(m, Color.white, 0.3f);
            m.SetTexture("_BaseColorMap", PcScreenTexture("GB_PC_DesktopText", 31));
            m.SetFloat("_UVBase", 0f);
            m.SetTexture("_EmissiveColorMap", m.GetTexture("_BaseColorMap"));
            m.SetFloat("_UVEmissive", 0f);
            m.SetColor("_EmissiveColor", Color.white * (900f * K));
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
        });
        var glow = Lit("GB_PC_ScreenLight", m => Emissive(m, new Color(0.75f, 0.88f, 1f), 2600f * K));
        var window = Lit("GB_PC_Window", m =>
        {
            Plain(m, Color.white, 0.2f);
            m.SetTexture("_BaseColorMap", PcWindowTexture("GB_PC_WindowTex", false));
            m.SetFloat("_UVBase", 0f);
            m.SetTexture("_EmissiveColorMap", m.GetTexture("_BaseColorMap"));
            m.SetFloat("_UVEmissive", 0f);
            m.SetColor("_EmissiveColor", Color.white * (250f * K));
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
        });
        var dialog = Lit("GB_PC_Dialog", m =>
        {
            Plain(m, Color.white, 0.2f);
            m.SetTexture("_BaseColorMap", PcWindowTexture("GB_PC_DialogTex", true));
            m.SetFloat("_UVBase", 0f);
            m.SetTexture("_EmissiveColorMap", m.GetTexture("_BaseColorMap"));
            m.SetFloat("_UVEmissive", 0f);
            m.SetColor("_EmissiveColor", Color.white * (250f * K));
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
        });
        var folder = Lit("GB_PC_Folder", m => Plain(m, new Color(0.98f, 0.8f, 0.25f), 0.3f));
        var paper = Lit("GB_PC_Paper", m => Plain(m, new Color(0.97f, 0.97f, 0.95f), 0.15f));
        var bin = Lit("GB_PC_Bin", m => Plain(m, new Color(0.7f, 0.74f, 0.78f), 0.5f, 0.3f));
        var cursorMat = Lit("GB_PC_Cursor", m =>
        {
            Plain(m, Color.white, 0.1f);
            m.SetTexture("_BaseColorMap", PcCursorTexture());
            m.SetFloat("_UVBase", 0f);
            m.SetFloat("_AlphaCutoffEnable", 1f); m.SetFloat("_AlphaCutoff", 0.5f);
            m.SetFloat("_DoubleSidedEnable", 1f); m.SetFloat("_CullMode", 0f); m.SetFloat("_CullModeForward", 0f);
            m.SetTexture("_EmissiveColorMap", m.GetTexture("_BaseColorMap"));
            m.SetFloat("_UVEmissive", 0f);
            m.SetColor("_EmissiveColor", Color.white * (700f * K));
            m.SetFloat("_UseEmissiveIntensity", 0f); m.SetFloat("_EmissiveExposureWeight", 1f);
        });

        // ---- 껍데기: 하늘 벽 네 개 + 하늘 천장 + 풀밭
        var shell = Group(gen, "Shell");
        float w = PcX1 - PcX0, d = PcZ1 - PcZ0;
        Box(shell, "Floor", new Vector3(PcCx, -0.15f, (PcZ0 + PcZ1) / 2), new Vector3(w + 0.6f, 0.3f, d + 0.6f), grass, LComputer, true);
        Box(shell, "Ceiling", new Vector3(PcCx, PcH + 0.15f, (PcZ0 + PcZ1) / 2), new Vector3(w + 0.6f, 0.3f, d + 0.6f), skyTop, LComputer);
        Box(shell, "North", new Vector3(PcCx, PcH / 2, PcZ1 + 0.15f), new Vector3(w + 0.6f, PcH, 0.3f), sky, LComputer, true);
        Box(shell, "South", new Vector3(PcCx, PcH / 2, PcZ0 - 0.15f), new Vector3(w + 0.6f, PcH, 0.3f), sky, LComputer, true);
        Box(shell, "West", new Vector3(PcX0 - 0.15f, PcH / 2, (PcZ0 + PcZ1) / 2), new Vector3(0.3f, PcH, d), sky, LComputer, true);
        Box(shell, "East", new Vector3(PcX1 + 0.15f, PcH / 2, (PcZ0 + PcZ1) / 2), new Vector3(0.3f, PcH, d), sky, LComputer, true);

        // 언덕: 땅에 반쯤 묻힌 납작한 초록 공. 가운데 길(x 52~64)은 비워 둔다. 오를 수 있게 메시 콜라이더.
        var hills = Group(gen, "Hills");
        void Hill(string n, Vector3 c, Vector3 s)
        {
            var go = Prim(PrimitiveType.Sphere, hills, n, c, s, hill, LComputer);
            go.AddComponent<MeshCollider>();
        }
        Hill("Hill_NW", new Vector3(46f, -2.2f, -147f), new Vector3(16f, 6f, 14f));
        Hill("Hill_NE", new Vector3(71f, -2.6f, -146f), new Vector3(14f, 7f, 16f));
        Hill("Hill_W", new Vector3(45f, -1.8f, -158f), new Vector3(10f, 5f, 12f));
        Hill("Hill_E", new Vector3(70f, -2f, -157f), new Vector3(12f, 6f, 14f));
        Hill("Hill_MonL", new Vector3(49.5f, -2.6f, -166f), new Vector3(14f, 8f, 10f));
        Hill("Hill_MonR", new Vector3(66.5f, -2.6f, -166f), new Vector3(14f, 8f, 10f));

        // ---- 거대한 옛날 모니터: 남쪽 끝에 화면 아래가 땅에 닿게 묻혀 있다. 화면 = 출구 벽, 화면 속은 빛으로 가득한 방.
        var mon = Group(gen, "GiantMonitor");
        float sx0 = PcCx - ScrW / 2, sx1 = PcCx + ScrW / 2, bz0 = ScrZ, bz1 = ScrZ - 1f, back = ScrZ - 6f;
        Box(mon, "Bezel_L", new Vector3(sx0 - 0.75f, 3f, ScrZ - 0.5f), new Vector3(1.5f, 7.6f, 1f), beige, LComputer, true);
        Box(mon, "Bezel_R", new Vector3(sx1 + 0.75f, 3f, ScrZ - 0.5f), new Vector3(1.5f, 7.6f, 1f), beige, LComputer, true);
        Box(mon, "Bezel_Top", new Vector3(PcCx, (ScrH + 6.8f) / 2, ScrZ - 0.5f), new Vector3(ScrW, 6.8f - ScrH, 1f), beige, LComputer, true);
        Box(mon, "Body_L", new Vector3(sx0 - 0.75f, 3f, (bz1 + back) / 2), new Vector3(1.5f, 7.6f, bz1 - back), beige, LComputer, true);
        Box(mon, "Body_R", new Vector3(sx1 + 0.75f, 3f, (bz1 + back) / 2), new Vector3(1.5f, 7.6f, bz1 - back), beige, LComputer, true);
        Box(mon, "Body_Top", new Vector3(PcCx, (ScrH + 6.8f) / 2, (bz1 + back) / 2), new Vector3(ScrW, 6.8f - ScrH, bz1 - back), beige, LComputer, true);
        Box(mon, "Body_Back", new Vector3(PcCx, 2f, back + 0.5f), new Vector3(ScrW, 5.6f, 1f), beige, LComputer, true);
        Box(mon, "Tube", new Vector3(PcCx, 3.2f, (back + PcZ0) / 2), new Vector3(6.2f, 5f, back - PcZ0), beigeDark, LComputer, true);
        Box(mon, "Badge", new Vector3(PcCx, 5.75f, ScrZ + 0.01f), new Vector3(1.4f, 0.28f, 0.03f), beigeDark, LComputer);
        Box(mon, "PowerLed", new Vector3(sx1 + 1.05f, 0.6f, ScrZ + 0.01f), new Vector3(0.16f, 0.16f, 0.03f), led, LComputer);
        Box(mon, "PowerButton", new Vector3(sx1 + 0.6f, 0.6f, ScrZ + 0.02f), new Vector3(0.36f, 0.24f, 0.05f), beigeDark, LComputer);
        // 화면 속: 빛으로 가득한 방(바닥은 밟을 수 있다)
        float cz0 = bz1, cz1 = back + 1f;
        Box(mon, "Inside_Floor", new Vector3(PcCx, 0.01f, (cz0 + cz1) / 2), new Vector3(ScrW, 0.02f, cz0 - cz1), glow, LComputer, true);
        Box(mon, "Inside_Back", new Vector3(PcCx, ScrH / 2, cz1 + 0.01f), new Vector3(ScrW, ScrH, 0.02f), glow, LComputer);
        Box(mon, "Inside_Top", new Vector3(PcCx, ScrH - 0.01f, (cz0 + cz1) / 2), new Vector3(ScrW, 0.02f, cz0 - cz1), glow, LComputer);
        Box(mon, "Inside_L", new Vector3(sx0 + 0.01f, ScrH / 2, (cz0 + cz1) / 2), new Vector3(0.02f, ScrH, cz0 - cz1), glow, LComputer);
        Box(mon, "Inside_R", new Vector3(sx1 - 0.01f, ScrH / 2, (cz0 + cz1) / 2), new Vector3(0.02f, ScrH, cz0 - cz1), glow, LComputer);
        // 2026-11-20 VARCO 거대 브라운관: 유리를 지운 메쉬를 화면 아래가 땅에 닿게 1.5m 묻고, 유리 자리가 출구 벽(화면) 바로 뒤에 오게 둔다.
        // 더미 상자들은 콜라이더(벽)로만 남기고 보이지 않게 한다. 화면 속 빛의 방(Inside_*)은 그대로 보인다.
        var openMon = GiantMonitorOpenMesh(out var monMat);
        if (openMon != null)
        {
            foreach (Transform c in mon)
                if (!c.name.StartsWith("Inside_") && c.TryGetComponent<MeshRenderer>(out var mr)) mr.enabled = false;
            var vm = MeshPart(mon, "VARCO_GiantMonitor", openMon, monMat, LComputer, 1f);
            vm.transform.position = new Vector3(PcCx, -GiantGlassBottom, ScrZ - 0.02f - GiantGlassZ);
            // 유리 구멍(x ±3.3, 높이 약 4.6)이 빛의 방(폭 ScrW, 1m 뒤에서 시작)보다 넓고 앞이라 열리면 가장자리에 어두운 틈이 보였다.
            // 빛의 방을 화면 바로 뒤까지 당기고 조금 넓힌다(벽들은 콜라이더 없음, 바닥은 그대로 밟힌다).
            float iz0 = ScrZ - 0.03f, iw = ScrW / 2 + 0.25f, ih = ScrH + 0.3f;
            void Fit(string n, Vector3 c, Vector3 s) { var t = mon.Find(n); t.position = c; t.localScale = s; }
            Fit("Inside_Floor", new Vector3(PcCx, 0.01f, (iz0 + cz1) / 2), new Vector3(iw * 2, 0.02f, iz0 - cz1));
            Fit("Inside_Back", new Vector3(PcCx, ih / 2, cz1 + 0.01f), new Vector3(iw * 2, ih, 0.02f));
            Fit("Inside_Top", new Vector3(PcCx, ih - 0.01f, (iz0 + cz1) / 2), new Vector3(iw * 2, 0.02f, iz0 - cz1));
            Fit("Inside_L", new Vector3(PcCx - iw + 0.01f, ih / 2, (iz0 + cz1) / 2), new Vector3(0.02f, ih, iz0 - cz1));
            Fit("Inside_R", new Vector3(PcCx + iw - 0.01f, ih / 2, (iz0 + cz1) / 2), new Vector3(0.02f, ih, iz0 - cz1));
        }

        // ---- 서쪽 벽: 벽 전체를 덮은 거대한 파란 화면
        var deco = Group(gen, "Desktop");
        Box(deco, "BlueScreenWall", new Vector3(PcX0 + 0.06f, 5.2f, -153f), new Vector3(0.1f, 8.4f, 20f), desktopWall, LComputer);

        // 떠 있는 창: 옛날 운영체제 창 모양 판. 천천히 위아래로 흔들린다.
        void Win(string n, Vector3 c, float ww, float hh, float yaw, Material m, float phase)
        {
            var go = Box(deco, n, c, new Vector3(ww, hh, 0.06f), m, LComputer, false, new Vector3(0f, yaw, 0f));
            var fd = go.AddComponent<FloatDrift>(); fd.bobHeight = 0.25f; fd.bobPeriod = 7f + phase % 3f; fd.swayDegrees = 1.5f; fd.phase = phase;
        }
        Win("Window_A", new Vector3(49.5f, 6.2f, -148f), 5f, 3.5f, 25f, window, 0f);
        Win("Window_B", new Vector3(65f, 8.2f, -151f), 6f, 4f, -18f, window, 2.1f);
        Win("Window_C", new Vector3(55f, 9.6f, -157f), 4f, 3f, 6f, window, 4.3f);
        Win("Window_D", new Vector3(52.5f, 7.6f, -162.5f), 4.2f, 3f, 12f, window, 1.2f);
        Win("Dialog_A", new Vector3(67.5f, 4.6f, -158f), 3.4f, 2f, -35f, dialog, 3.3f);
        Win("Dialog_B", new Vector3(47.5f, 3.9f, -156f), 3f, 1.8f, 42f, dialog, 5.7f);
        Win("Dialog_C", new Vector3(61.5f, 5.4f, -145.5f), 3.2f, 1.9f, -10f, dialog, 6.1f);

        // 바탕화면 아이콘: 폴더, 문서, 휴지통, 작은 모니터. 사람 키보다 크다.
        propLayer = LComputer;
        var fo = PropGroup(deco, "Icon_Folder", new Vector3(50.5f, 1.2f, -151f), 30f);
        // 2026-11-20 VARCO 폴더 아이콘(2.2×1.7×0.89, 더미와 같은 폭). 떠서 흔들리는 건 그대로(FloatDrift).
        if (VarcoInto(fo, "Computer", "FolderIcon", LComputer, default, default, false) == null)
        {
        PBox(fo, "Back", new Vector3(0f, 0.8f, 0f), new Vector3(2.2f, 1.6f, 0.12f), folder);
        PBox(fo, "Tab", new Vector3(-0.6f, 1.7f, 0f), new Vector3(0.9f, 0.25f, 0.12f), folder);
        PBox(fo, "Front", new Vector3(0f, 0.7f, 0.1f), new Vector3(2.2f, 1.4f, 0.08f), Lit("GB_PC_FolderFront", m => Plain(m, new Color(1f, 0.87f, 0.4f), 0.3f)));
        }
        fo.gameObject.AddComponent<FloatDrift>().phase = 1.7f;
        var doc = PropGroup(deco, "Icon_Document", new Vector3(65.5f, 1.4f, -146.5f), -25f);
        PBox(doc, "Sheet", new Vector3(0f, 1f, 0f), new Vector3(1.4f, 2f, 0.06f), paper);
        for (int i = 0; i < 5; i++) PBox(doc, "Line", new Vector3(-0.05f, 1.55f - i * 0.25f, 0.04f), new Vector3(1f, 0.06f, 0.01f), beigeDark);
        PBox(doc, "Fold", new Vector3(0.55f, 1.85f, 0.04f), new Vector3(0.3f, 0.3f, 0.02f), Lit("GB_PC_PaperFold", m => Plain(m, new Color(0.8f, 0.8f, 0.78f), 0.15f)));
        var fd2 = doc.gameObject.AddComponent<FloatDrift>(); fd2.phase = 3.9f; fd2.bobHeight = 0.4f;
        // 2026-11-20 VARCO 철망 휴지통(1.84×2.0×1.85). 더미보다 넓어 옆 언덕(Hill_E)에 가장자리가 묻혀서 (67, -152.5)에서 언덕 반대쪽으로 옮겼다.
        var bn = PropGroup(deco, "Icon_RecycleBin", new Vector3(66.4f, 0f, -151.9f), -40f, new Vector3(1.6f, 2.2f, 1.6f));
        if (VarcoInto(bn, "Computer", "RecycleBinIcon", LComputer) == null)
        {
        Part(bn, PrimitiveType.Cylinder, "Can", new Vector3(0f, 1f, 0f), new Vector3(1.5f, 1f, 1.5f), bin);
        Part(bn, PrimitiveType.Cylinder, "Rim", new Vector3(0f, 2.02f, 0f), new Vector3(1.62f, 0.04f, 1.62f), beigeDark);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            Part(bn, PrimitiveType.Cube, "Rib", new Vector3(Mathf.Sin(a) * 0.76f, 1f, Mathf.Cos(a) * 0.76f), new Vector3(0.05f, 1.9f, 0.05f), beigeDark, new Vector3(0f, a * Mathf.Rad2Deg, 0f));
        }
        }
        var pcIcon = PropGroup(deco, "Icon_MyComputer", new Vector3(46.5f, 2.6f, -151.5f), 50f);
        PBox(pcIcon, "Case", new Vector3(0f, 0.6f, 0f), new Vector3(1.4f, 1.2f, 1.1f), beige);
        PBox(pcIcon, "Screen", new Vector3(0f, 0.65f, 0.56f), new Vector3(1.05f, 0.8f, 0.02f), screen);
        PBox(pcIcon, "Stand", new Vector3(0f, -0.1f, 0f), new Vector3(0.9f, 0.2f, 0.8f), beigeDark);
        pcIcon.gameObject.AddComponent<FloatDrift>().phase = 5.2f;

        // 종이비행기: 하늘을 천천히 돈다.
        foreach (var (c, r, period, phase) in new[] { (new Vector3(58f, 7f, -152f), 7f, 26f, 0f), (new Vector3(56f, 9f, -150f), 9f, 34f, 11f), (new Vector3(60f, 5.5f, -154f), 5f, 21f, 6f) })
        {
            var pl = new GameObject("PaperPlane").transform;
            pl.SetParent(deco, true); pl.position = c;
            Part(pl, PrimitiveType.Cube, "Wing_L", new Vector3(-0.22f, 0f, 0f), new Vector3(0.45f, 0.02f, 1.1f), paper, new Vector3(0f, 0f, 12f));
            Part(pl, PrimitiveType.Cube, "Wing_R", new Vector3(0.22f, 0f, 0f), new Vector3(0.45f, 0.02f, 1.1f), paper, new Vector3(0f, 0f, -12f));
            Part(pl, PrimitiveType.Cube, "Keel", new Vector3(0f, -0.08f, -0.05f), new Vector3(0.02f, 0.16f, 1f), paper);
            var fd = pl.gameObject.AddComponent<FloatDrift>();
            fd.orbitRadius = r; fd.orbitPeriod = period; fd.phase = phase; fd.bobHeight = 0.5f; fd.swayDegrees = 8f;
        }

        // ---- 빛: 맑은 하늘의 해(이 방 레이어만) + 풀밭에서 올라오는 반사광
        var lights = Group(gen, "Lights");
        var sunGo = new GameObject("DesktopSun");
        sunGo.transform.SetParent(lights, true);
        sunGo.transform.SetPositionAndRotation(new Vector3(PcCx, 11f, -142f), Quaternion.Euler(48f, 165f, 0f));
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional; sun.color = new Color(1f, 0.97f, 0.9f); sun.shadows = LightShadows.None; // 그림자 내는 방향광은 하나만 된다(바깥 DawnLight가 쓴다)
        var sunHd = sunGo.AddComponent<HDAdditionalLightData>();
        sunHd.SetIntensity(3000f * K, LightUnit.Lux);
        sunHd.affectsVolumetric = false;
        sunHd.lightlayersMask = (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)LComputer;
        foreach (var z in new[] { -146f, -156f })
            MakeLight(lights, "GrassBounce", LightType.Point, new Vector3(PcCx, 0.8f, z), Vector3.zero, new Color(0.8f, 1f, 0.8f), 350000f * K, 18f, LComputer, false, false);
        MakeLight(lights, "ScreenSpill", LightType.Point, new Vector3(PcCx, 2.3f, ScrZ + 1.2f), Vector3.zero, new Color(0.5f, 0.65f, 1f), 120000f * K, 8f, LComputer, false, false);

        var profile = RoomProfile("Computer", AngelEV, 0.3f, 0.1f, new Color(0.92f, 0.96f, 1f), 70f, new Color(0.72f, 0.86f, 1f), 0f, 0f);
        if (profile.TryGet<Fog>(out var fog))
        {
            fog.enableVolumetricFog.Override(false);
            fog.color.Override(new Color(0.72f, 0.86f, 1f));
            EditorUtility.SetDirty(fog); EditorUtility.SetDirty(profile);
        }
        RoomChromatic(profile, VhsChromaticUncanny);
        AssetDatabase.SaveAssets();
        var roomCenter = new Vector3(PcCx, PcH / 2, (PcZ0 + PcZ1) / 2);
        var roomSize = new Vector3(w, PcH, d);
        LocalVolume(gen, "LocalVolume", roomCenter, roomSize, 2f, 125f, profile);

        // ---- 출구: 모니터 화면(찾기 규칙 출구 벽). 재질은 파란 화면으로 바꾼다.
        var gate = FindGate(gen, "FindGate_Computer", 8, "ComputerCursor",
            new Bounds(new Vector3(PcCx, ScrH / 2, ScrZ - 0.5f), new Vector3(ScrW, ScrH, 1f)), "GB_03d_Computer/GFX_Generated/GiantMonitor/Bezel_L");
        foreach (var r in gate.wall.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = screen;

        // ---- 이상한 점: 혼자 움직이는 커서. 이 오브젝트 위치 = 화살표 끝.
        var cursor = new GameObject("Oddity_WanderCursor");
        cursor.transform.SetParent(gen, true);
        cursor.transform.position = new Vector3(52f, 7f, -150f);
        var vis = new GameObject("Visual").transform;
        vis.SetParent(cursor.transform, false);
        const float cw = 1.7f, ch = 2.6f;
        var q = Prim(PrimitiveType.Quad, vis, "Arrow", cursor.transform.position, new Vector3(cw, ch, 1f), cursorMat, LComputer);
        q.transform.localPosition = new Vector3(cw / 2, -ch / 2, 0f); // 그림 왼쪽 위(화살표 끝)가 오브젝트 위치에 오게
        var gz = AddGaze(cursor, "ComputerCursor", gate, new Bounds(roomCenter, roomSize));
        gz.gazeRenderers = new[] { q.GetComponent<Renderer>() };
        gz.maxDistance = 30f; gz.dwellTime = 1.2f; gz.boundsPadding = 0.3f;
        gz.blinkRenderers = new[] { q.GetComponent<Renderer>() };
        gz.blinkCount = 1; gz.blinkOff = 0.2f;
        gz.notifyDelay = 6f; // 멈칫(1.6초) → 화면까지(2.4초) → 더블클릭 뒤에 열린다
        var wc = cursor.AddComponent<WanderCursor>();
        wc.gaze = gz; wc.visual = vis;
        wc.points = new[] { new Vector3(52f, 7f, -150f), new Vector3(63f, 8.5f, -153f), new Vector3(57f, 5.5f, -147f), new Vector3(66f, 6.5f, -158f), new Vector3(50f, 8f, -158f), new Vector3(60f, 9.5f, -149f) };
        wc.clickPoint = new Vector3(PcCx - 1.2f, 3.4f, ScrZ + 0.05f);
        EditorUtility.SetDirty(gz); EditorUtility.SetDirty(wc);

        // ---- 화면 속 빛 → 가라앉은 집들의 도시 북쪽 벽(2026-11-18 변경, 그 전에는 천사 방). 도시 벽에는 잠깐 빛나는 모니터 화면 틀이 남는다.
        var from = new GameObject("ScreenAnchor").transform;
        from.SetParent(gen, true);
        from.SetPositionAndRotation(new Vector3(PcCx, 0f, ScrZ - 0.5f), Quaternion.LookRotation(Vector3.back));
        var to = new GameObject("CityArrivalAnchor").transform;
        to.SetParent(gen, true);
        to.SetPositionAndRotation(CityArrivalPos, Quaternion.LookRotation(Vector3.back));
        var arrival = new GameObject("MonitorArrival").transform;
        arrival.SetParent(to, false);
        Vector3 Wp(Vector3 l) => to.TransformPoint(l);
        var rotE = to.rotation.eulerAngles;
        const float bw = 0.5f, fz = 0.15f;
        Box(arrival, "Bezel_L", Wp(new Vector3(-(ScrW / 2 + bw / 2), ScrH / 2 + bw / 2, fz + 0.2f)), new Vector3(bw, ScrH + bw, 0.4f), beige, LCity, false, rotE);
        Box(arrival, "Bezel_R", Wp(new Vector3(ScrW / 2 + bw / 2, ScrH / 2 + bw / 2, fz + 0.2f)), new Vector3(bw, ScrH + bw, 0.4f), beige, LCity, false, rotE);
        Box(arrival, "Bezel_Top", Wp(new Vector3(0f, ScrH + bw / 2, fz + 0.2f)), new Vector3(ScrW, bw, 0.4f), beige, LCity, false, rotE);
        Box(arrival, "Glow", Wp(new Vector3(0f, ScrH / 2, fz + 0.01f)), new Vector3(ScrW, ScrH, 0.02f), glow, LCity, false, rotE);
        arrival.gameObject.SetActive(false);
        var tz = gen.gameObject.AddComponent<TeleportZone>();
        tz.zone = new Bounds(new Vector3(PcCx, ScrH / 2, ScrZ - 2.6f), new Vector3(ScrW, ScrH, 2.6f));
        tz.fromAnchor = from; tz.toAnchor = to; tz.arrivalObject = arrival.gameObject;
        EditorUtility.SetDirty(tz);

        // 개발용 방에서 시작(F9 가라앉은 도시)의 시작 자리
        var spawn = new GameObject("Spawn_CityArrival").transform;
        spawn.SetParent(gen, true);
        spawn.SetPositionAndRotation(to.TransformPoint(new Vector3(0f, 0.05f, 2f)), to.rotation);
    }

    // 바탕화면 하늘: 아래는 옅은 하늘색, 위로 갈수록 진한 파랑, 가운데 띠에 뭉게구름. 벽 한 면에 한 장(가로 반복 없음).
    static Texture2D PcSkyTexture()
    {
        const int n = 1024;
        var px = new Color[n * n];
        var low = new Color(0.78f, 0.9f, 1f); var high = new Color(0.1f, 0.34f, 0.9f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                var col = Color.Lerp(low, high, Mathf.Pow(v, 0.8f));
                float band = SStep(0.15f, 0.4f, v) * (1f - SStep(0.7f, 0.92f, v));
                float c = SStep(0.48f, 0.62f, Fbm(u * 0.5f, v, 4, 4, 1201)) * band;
                float shade = 0.86f + 0.14f * SStep(0.4f, 0.8f, Fbm(u, v, 8, 3, 1213));
                col = Color.Lerp(col, new Color(shade, shade, shade), c); col.a = 1f;
                px[y * n + x] = col;
            }
        return Save("GB_PC_Sky", n, px, TexKind.Color, 0f, true);
    }

    static (Texture2D, Texture2D, Texture2D) PcGrassSet()
    {
        return MakeSet("GB_PCGrass", 512, 0.05f, (float u, float v, out Color col, out float h, out Color mk) =>
        {
            float n = Fbm(u, v, 6, 5, 1307);
            float blade = Fbm(u, v, 48, 2, 1319);
            col = Color.Lerp(new Color(0.2f, 0.5f, 0.1f), new Color(0.42f, 0.74f, 0.2f), SStep(0.3f, 0.75f, n * 0.7f + blade * 0.3f)); col.a = 1f;
            h = blade;
            mk = new Color(0f, 1f, 0f, 0.15f);
        });
    }

    // 파란 오류 화면: 진한 파랑 바탕에 흰 네모 글자 줄(읽을 수 없는 글자). 가운데 위에 회색 제목 띠.
    static Texture2D PcScreenTexture(string name, int seed)
    {
        const int n = 512;
        var px = new Color[n * n];
        var bg = new Color(0.02f, 0.12f, 0.66f); var fg = new Color(0.92f, 0.94f, 1f);
        var rnd = new System.Random(seed);
        var rows = new System.Collections.Generic.List<(int y, int x0, int x1, bool title)>();
        rows.Add((n - 70, n / 2 - 70, n / 2 + 70, true));
        for (int y = n - 120; y > 40; y -= 22)
        {
            if (rnd.NextDouble() < 0.18) continue;
            int x = 40;
            while (x < n - 60)
            {
                int len = 12 + rnd.Next(70);
                if (x + len > n - 40) break;
                rows.Add((y, x, x + len, false));
                x += len + 8 + rnd.Next(10);
                if (rnd.NextDouble() < 0.15) break;
            }
        }
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        foreach (var r in rows)
            for (int y = r.y; y < r.y + (r.title ? 22 : 10); y++)
                for (int x = r.x0; x < r.x1; x++)
                    if (y >= 0 && y < n) px[y * n + x] = r.title ? new Color(0.7f, 0.72f, 0.75f) : fg;
        return Save(name, n, px, TexKind.Color, 0f, true);
    }

    // 옛날 운영체제 창: 회색 테두리, 파란 제목 막대(오른쪽에 네모 단추 셋), 흰 본문에 회색 줄. dialog면 경고 아이콘과 확인 단추.
    static Texture2D PcWindowTexture(string name, bool dialog)
    {
        const int n = 512;
        var px = new Color[n * n];
        var frame = new Color(0.75f, 0.75f, 0.75f); var title0 = new Color(0f, 0.05f, 0.5f); var title1 = new Color(0.1f, 0.5f, 0.85f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Color c = frame;
                bool inner = x > 10 && x < n - 10 && y > 10 && y < n - 10;
                if (inner && y > n - 70) c = Color.Lerp(title0, title1, x / (float)n);                 // 제목 막대
                if (inner && y > n - 62 && y < n - 18)
                    for (int b = 0; b < 3; b++) { int bx = n - 30 - b * 44; if (x > bx - 34 && x < bx) c = frame; } // 단추
                if (inner && y <= n - 80 && x > 22 && x < n - 22 && y > 22)
                {
                    c = dialog ? frame : Color.white;
                    if (!dialog && (y % 34) < 8 && x < n - 60 - (y * 37 % 120)) c = new Color(0.62f, 0.62f, 0.65f); // 본문 줄
                    if (dialog)
                    {
                        float dx = x - 110, dy = y - (n - 200);
                        if (Mathf.Abs(dx) * 1.15f + -dy * 0.6f < 60f && dy < 50f && dy > -55f) c = new Color(0.98f, 0.82f, 0.1f); // 경고 세모
                        if (Mathf.Abs(dx) < 6 && dy > -20 && dy < 30) c = Color.black;
                        if (x > 200 && x < n - 60 && (y % 40) < 10 && y > n - 260 && y < n - 140) c = new Color(0.25f, 0.25f, 0.28f); // 글자 줄
                        if (x > n / 2 - 70 && x < n / 2 + 70 && y > 50 && y < 110) c = (x < n / 2 - 64 || x > n / 2 + 64 || y < 56 || y > 104) ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.82f, 0.82f, 0.82f); // 확인 단추
                    }
                }
                c.a = 1f;
                px[y * n + x] = c;
            }
        return Save(name, n, px, TexKind.Color, 0f, true);
    }

    // 마우스 커서: 흰 화살표에 검은 테두리. 그림 왼쪽 위가 화살표 끝. 바깥은 투명(알파 잘라내기).
    static Texture2D PcCursorTexture()
    {
        const int n = 256;
        // 화살표 꼭짓점(그림 좌표, 위가 0): 끝 → 왼쪽 아래 → 꼬리 → 오른쪽
        var poly = new[] { new Vector2(0.06f, 0.03f), new Vector2(0.06f, 0.78f), new Vector2(0.24f, 0.62f), new Vector2(0.38f, 0.94f), new Vector2(0.5f, 0.89f), new Vector2(0.37f, 0.58f), new Vector2(0.6f, 0.58f) };
        bool Inside(Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }
        float EdgeDist(Vector2 p)
        {
            float best = 9f;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
            }
            return best;
        }
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f) / n, 1f - (y + 0.5f) / n);
                bool inside = Inside(p);
                float e = EdgeDist(p);
                Color c;
                if (inside) c = e < 0.025f ? Color.black : Color.white;
                else c = e < 0.012f ? Color.black : new Color(0, 0, 0, 0);
                if (c.a > 0f) c.a = 1f;
                px[y * n + x] = c;
            }
        return Save("GB_PC_Cursor", n, px, TexKind.Alpha, 0f, true);
    }
}
