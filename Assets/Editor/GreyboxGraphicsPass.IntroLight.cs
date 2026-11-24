using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-24 후처리 3단계(기준 방 = 인트로). 멘토 피드백 "방 안 라이팅은 좋은데 바깥 라이팅이 아쉽다(반사광 등)".
// 바꾸기 전(증거 10-intro-before-*): 새벽 해가 20° 높이라 24m 벽에 가려 중정 전체가 그늘 → 하늘빛만 받아 평평했다
// (가운데 80% 밝기 가장 어두운 20% 평균 0.45, 상위 5% 0.75). 안개도 대비를 깎았다.
// 인트로와 같은 수영장(마지막 방)에만 씌우는 전용 볼륨(HDRP_GB_Intro_Profile)을 둔다. 전역 프로파일은 모든 방에 쓰이므로 건드리지 않는다.
// - 블룸을 낮춘다(해 든 흰 면이 화면 전체를 뿌옇게 덮었다).
// - SSR: 매끈함 0.9 이상만 비추던 것을 0.55부터 비추게(젖은 타일·물가 바닥에 하늘·벽이 비친다).
// - 안개를 옅게(전역의 IntroFogScale배 거리).
// - 해(DawnLight): 새벽빛 색·방향은 그대로 두고 높이만 IntroSunElevation으로 올려 바닥 일부에 해가 들게, 그림자 가장자리는 부드럽게.
public static partial class GreyboxGraphicsPass
{
    const float IntroSunElevation = 32f;   // 원래 20°. 새벽빛(낮은 해)은 유지(메모리: 한낮 해로 바꾸지 않는다)
    const float IntroSunAngularDiameter = 1.5f; // 원래 0.5°. 그림자 가장자리를 부드럽게
    const float IntroSsrMinSmoothness = 0.55f, IntroSsrFadeStart = 0.7f;
    const float IntroFogScale = 2.5f;
    const float IntroBloom = 0.08f, IntroBloomScatter = 0.5f; // 전역 0.35 / 0.75

    static void ApplyIntroLight()
    {
        var global = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HDRP_GB_Global_Profile.asset");
        float globalFog = global != null && global.TryGet<Fog>(out var gf) ? gf.meanFreePath.value : 100f;

        var profile = Profile("Intro", p =>
        {
            // SSGI는 넣어 봤지만 켜고 끈 차이가 없었다(가장 어두운 20% 0.549 → 0.552). 하늘빛이 그늘을 이미 고르게 채워서다. 성능만 써서 뺐다.

            // 해가 들자 흰 바닥·벽이 블룸 문턱을 넘어 화면 전체에 뿌연 막이 꼈다(안개를 꺼도 그대로였다). 인트로만 블룸을 낮춘다.
            var bloom = Persist(p, p.Add<Bloom>(false));
            bloom.intensity.Override(IntroBloom);
            bloom.scatter.Override(IntroBloomScatter);

            var ssr = Persist(p, p.Add<ScreenSpaceReflection>(true));
            ssr.enabled.Override(true);
            var so = new SerializedObject(ssr);
            void F(string name, float v) { var sp = so.FindProperty(name); if (sp == null) return; sp.FindPropertyRelative("m_OverrideState").boolValue = true; sp.FindPropertyRelative("m_Value").floatValue = v; }
            F("m_MinSmoothness", IntroSsrMinSmoothness);
            F("m_SmoothnessFadeStart", IntroSsrFadeStart);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Add(false): 다른 안개 값(볼류메트릭, 분홍 albedo, 높이)은 전역 것을 그대로 쓰고 거리만 덮는다.
            // Add(true)로 넣었더니 모든 값이 기본값으로 덮여 볼륨 안개가 꺼지고 하얀 일반 안개가 됐다(2026-11-24 플레이 확인).
            var fog = Persist(p, p.Add<Fog>(false));
            fog.meanFreePath.Override(globalFog * IntroFogScale);
        });

        // 볼륨: 인트로(중정·옆방, Environment 범위)와 같은 수영장. 방 볼륨(120~140)보다 낮고 전역(100)보다 높게.
        void Vol(string rootName, string name, Vector3 center, Vector3 size)
        {
            var root = Root(rootName); // 씬 루트(같은 수영장 안에도 "Environment"가 있어 이름 검색은 헷갈린다)
            var old = root.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            LocalVolume(root.transform, name, center, size, 2f, 105f, profile);
        }
        Vol("Environment", "IntroLightVolume", new Vector3(0f, 14f, -21f), new Vector3(104f, 32f, 98f));        // z -70 ~ 28
        Vol("GB_05_SameIntro", "IntroLightVolume", new Vector3(28f, 20f, -222f), new Vector3(104f, 22f, 98f)); // 같은 수영장 범위

        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.name != "DawnLight" || l.type != LightType.Directional) continue;
            var e = l.transform.eulerAngles;
            l.transform.rotation = Quaternion.Euler(IntroSunElevation, e.y, 0f);
            var hd = l.GetComponent<HDAdditionalLightData>();
            if (hd != null) { hd.angularDiameter = IntroSunAngularDiameter; EditorUtility.SetDirty(hd); }
            EditorUtility.SetDirty(l.transform);
        }
    }
}
