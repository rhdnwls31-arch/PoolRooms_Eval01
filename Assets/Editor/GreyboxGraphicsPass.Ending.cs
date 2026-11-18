using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 2026-11-18 마지막 수영장 숨은 엔딩(기획서 §6-6). 튜브 자리의 마네킹을 응시하면 기본 엔딩 대신 "깨어나는" 엔딩으로 바뀐다.
// 마네킹은 씬 원본 오브젝트라 GFX_Generated처럼 새로 만들어지지 않는다. 그래서 붙이기 전에 예전 것을 지워 여러 번 Apply해도 하나만 남게 한다.
// 기본 엔딩 DemoEndTrigger 스크립트는 고치지 않는다. 숨은 엔딩이 그 콜라이더만 끄고 켠다.
public static partial class GreyboxGraphicsPass
{
    static void ApplyHiddenEnding()
    {
        var gen = Root("GB_05_SameIntro").transform.Find(GenName);
        var mannequin = Find("GB_05_SameIntro/Placeholder_Props/Mannequin_InTubeSpot").gameObject;
        var tube = Find("GB_05_SameIntro/Dummies/Prop_Flamingo_B").gameObject;
        var normalEnd = Find("GB_05_SameIntro/DemoEndTrigger");

        foreach (var old in mannequin.GetComponents<GazeTarget>()) Object.DestroyImmediate(old);
        var g = mannequin.AddComponent<GazeTarget>();
        g.id = "HiddenEnding";
        g.gate = null;                 // 출구가 아니라 엔딩을 바꾼다
        g.roomBounds = new Bounds();   // 숨은 엔딩이라 힌트 깜빡임은 없다
        // 멀리(계단 쪽)서 보는 것으로는 안 되고, 수영장 앞까지 와야 한다(2026-11-18 피드백).
        // 수영장 북쪽 끝(z -231)에서 마네킹(z -249)까지 약 18m라 21m로 잡는다.
        g.maxDistance = 21f;
        g.dwellTime = 1.5f;
        g.boundsPadding = 0.3f;        // 20m 거리에서도 겨눌 수 있게
        EditorUtility.SetDirty(g);
        tube.SetActive(false);
        // 처음엔 등을 돌리고 서 있다(들어오는 플레이어 반대쪽). 찾으면 천천히 돌아서 노려본다.
        mannequin.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // 엔딩 화면: 기본 엔딩 화면을 복제해 글자만 바꾼다(한글 글꼴이 없어 영어, 기본 엔딩과 같은 형식).
        var demoPanel = Find("FadeCanvas/DemoEndPanel");
        var oldPanel = demoPanel.parent.Find("WakeEndPanel");
        if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);
        var panel = Object.Instantiate(demoPanel.gameObject, demoPanel.parent);
        panel.name = "WakeEndPanel";
        panel.transform.SetAsLastSibling(); // 하얀 화면(FadeImage) 위에 그려지게
        var dark = new Color(0.12f, 0.14f, 0.18f, 1f); // 하얀 바탕 위라 어두운 글자
        foreach (var t in panel.GetComponentsInChildren<Text>(true))
        {
            t.color = dark;
            t.verticalOverflow = VerticalWrapMode.Overflow; // 두 줄 안내가 잘리지 않게
            if (t.name == "DemoEndTitle") { t.name = "WakeEndTitle"; t.text = "[You woke up.]"; }
            else if (t.name == "DemoEndInfo") { t.name = "WakeEndInfo"; t.text = "[The tube was there all along.]\n[Thank you for playing.]"; }
        }
        panel.SetActive(false);

        var go = Fresh(gen, "HiddenEnding").gameObject;
        var nb = normalEnd.GetComponent<BoxCollider>();
        var he = go.AddComponent<HiddenEnding>();
        he.gaze = g;
        he.mannequin = mannequin;
        he.tube = tube;
        he.normalEndTrigger = nb;
        he.endPanel = panel;
        he.fader = Object.FindAnyObjectByType<ScreenFader>(FindObjectsInactive.Include);
        he.player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        // 깨어난 뒤 지나온 방들을 3초씩 돌아본다(2026-11-18 사용자 요청). 방마다 들어선 자리에서 방 안쪽을 보는 카메라 자리.
        var shots = new System.Collections.Generic.List<Transform>();
        void Shot(string n, Vector3 eye, Vector3 look)
        {
            var t = new GameObject("Shot_" + n).transform;
            t.SetParent(go.transform, true);
            t.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
            shots.Add(t);
        }
        var p = he.player.transform;
        Shot("01_Intro", p.position + Vector3.up * 1.6f, p.position + Vector3.up * 1.3f + p.forward * 10f);
        Shot("02_PoolHall", new Vector3(0f, 1.65f, -75f), new Vector3(0f, 1.8f, -100f));
        Shot("03_Crack", new Vector3(7.2f, 1.6f, -106.7f), new Vector3(19f, 1.2f, -106.7f));
        Shot("04_Shower", new Vector3(9.5f, 1.6f, -102.2f), new Vector3(15f, 0.9f, -94f));
        Shot("05_Yellow", new Vector3(21.2f, 1.6f, -101.5f), new Vector3(31f, 1.2f, -108f));
        Shot("06_Living", new Vector3(39.6f, 1.6f, -112.4f), new Vector3(46f, 0.9f, -118f));
        Shot("07_Bedroom", new Vector3(40.6f, 1.6f, -125.2f), new Vector3(45f, 0.7f, -131.5f));
        Shot("08_Corridor", new Vector3(49.8f, 1.5f, CrZ), new Vector3(64f, 1.1f, CrZ));
        Shot("09_Computer", new Vector3(PcCx, 1.65f, PcZ1 - 2f), new Vector3(PcCx, 3.2f, ScrZ));
        Shot("10_Angel", new Vector3(36.3f, 1.65f, BdOutZ), new Vector3(28f, 3.2f, -128f));
        Shot("11_SkyStairs", new Vector3(28.15f, 1.7f, -146.5f), new Vector3(28.15f, 3.5f, -165f));
        he.montageShots = shots.ToArray();
        EditorUtility.SetDirty(he);
    }
}
