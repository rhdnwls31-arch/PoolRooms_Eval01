using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 2026-11-24 후처리 4단계(나머지 방). 기준 방 = 인트로. 방마다 재 본 결과(증거 20-rooms-before.png)에서
// 손볼 곳은 두 방뿐이었다. 나머지 방은 밝기·채도가 고르게 나와서 건드리지 않는다.
// - 거실: 가운데 80% 중 23.7%가 검게 뭉개졌다(밝기 0.04 미만). 그림자만 살짝 띄운다(밤 분위기는 유지).
// - 파란 침실: 채도 0.94로 파랑이 형광처럼 떴고 14.1%가 뭉개졌다. 채도를 낮추고 그림자를 조금 띄운다.
// 방 프로파일(RoomProfile)은 Apply 때마다 새로 만들어지므로, 그 뒤에 보정 컴포넌트만 덧붙인다.
// Add(false): 덮고 싶은 값만 Override한다(Add(true)는 모든 값을 기본값으로 덮는다 — 인트로 안개 사고).
// 비네팅으로 어둠을 가리지 않는다(사용자 규칙).
public static partial class GreyboxGraphicsPass
{
    const float LivingLift = 0.012f;                 // 그림자 띄우기(LiftGammaGain lift w)
    const float BedroomLift = 0.004f;
    const float BedroomSaturation = -45f;           // 방 값이 전역(-12)을 덮어 쓴다(더해지지 않는다)

    static void GradeRooms()
    {
        Grade("Living", LivingLift, null);
        Grade("Bedroom", BedroomLift, BedroomSaturation);
    }

    static void Grade(string room, float lift, float? saturation)
    {
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"Assets/Settings/HDRP_GB_{room}_Profile.asset");
        if (p == null) { Debug.LogWarning($"[GradeRooms] {room} 프로파일 없음"); return; }
        if (p.Has<LiftGammaGain>()) p.Remove<LiftGammaGain>(); // 여러 번 Apply해도 하나만
        var lgg = Persist(p, p.Add<LiftGammaGain>(false));
        lgg.lift.Override(new Vector4(1f, 1f, 1f, lift));
        if (saturation.HasValue)
        {
            if (p.Has<ColorAdjustments>()) p.Remove<ColorAdjustments>();
            var ca = Persist(p, p.Add<ColorAdjustments>(false));
            ca.saturation.Override(saturation.Value);
        }
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
    }
}
