using UnityEngine;
using NUnit.Framework;

/// <summary>사운드 단계(2026-11-27): 소리 다듬기 계산, 방 범위 고르기, 발소리 고르기, 힌트 소리 시간.</summary>
public class GameAudioTests
{
    const int Freq = 1000; // 계산 확인용 작은 샘플 속도(1초 = 1000프레임)

    static float[] Silence(int frames) => new float[frames];

    static float[] WithClicks(int frames, params int[] at)
    {
        var s = new float[frames];
        foreach (var a in at) for (int i = a; i < a + 20 && i < frames; i++) s[i] = 0.5f;
        return s;
    }

    [Test]
    public void Normalize_RaisesPeakToTarget()
    {
        var s = new[] { 0.1f, -0.2f, 0.05f };
        AudioCraft.Normalize(s, 0.8f);
        Assert.AreEqual(0.8f, AudioCraft.Peak(s), 1e-4f);
        Assert.AreEqual(0.4f, s[0], 1e-4f);
    }

    [Test]
    public void Normalize_SilenceStaysSilent()
    {
        var s = Silence(10);
        AudioCraft.Normalize(s, 0.8f);
        Assert.AreEqual(0f, AudioCraft.Peak(s));
    }

    [Test]
    public void TrimLeadingSilence_StartsJustBeforeSound()
    {
        var s = WithClicks(1000, 500);
        var o = AudioCraft.TrimLeadingSilence(s, 1, Freq, 0.1f, 0.02f); // 20프레임 남김
        Assert.AreEqual(1000 - 480, o.Length);
        Assert.AreEqual(0.5f, o[20]);
    }

    [Test]
    public void TrimWithFade_CutsToMaxAndEndsSilent()
    {
        var s = new float[3000];
        for (int i = 0; i < s.Length; i++) s[i] = 1f;
        var o = AudioCraft.TrimWithFade(s, 1, Freq, 2f, 0.1f);
        Assert.AreEqual(2000, o.Length);
        Assert.AreEqual(0f, o[o.Length - 1], 1e-4f);
        Assert.AreEqual(1f, o[0]);
    }

    [Test]
    public void MakeLoop_ShortensByCrossfadeAndJoinsSmoothly()
    {
        var s = new float[3000];
        for (int i = 0; i < s.Length; i++) s[i] = i < 1500 ? 0f : 1f; // 끝은 1, 처음은 0
        var o = AudioCraft.MakeLoop(s, 1, Freq, 0.5f);
        Assert.AreEqual(2500, o.Length);
        // 이음매: 마지막 샘플(1)에서 첫 샘플로 넘어갈 때 첫 샘플은 끝 쪽 소리(1)에 가깝다
        Assert.AreEqual(1f, o[0], 1e-3f);
        Assert.AreEqual(o[o.Length - 1], o[0], 0.01f);
    }

    [Test]
    public void FindOnsets_FindsEachStep()
    {
        var s = WithClicks(3000, 200, 900, 1700, 2500);
        var on = AudioCraft.FindOnsets(s, 1, Freq, 0.35f, 0.25f);
        Assert.AreEqual(4, on.Count);
        Assert.AreEqual(200, on[0], 10);
        Assert.AreEqual(2500, on[3], 10);
    }

    [Test]
    public void FindOnsets_IgnoresStepsCloserThanMinGap()
    {
        var s = WithClicks(2000, 200, 300, 1200);
        var on = AudioCraft.FindOnsets(s, 1, Freq, 0.35f, 0.25f);
        Assert.AreEqual(2, on.Count);
    }

    [Test]
    public void ToWav_HeaderAndSize()
    {
        var b = AudioCraft.ToWav(new float[] { 0f, 1f, -1f, 0.5f }, 2, 44100);
        Assert.AreEqual(44 + 8, b.Length);
        Assert.AreEqual((byte)'R', b[0]);
        Assert.AreEqual((byte)'W', b[8]);
        Assert.AreEqual(2, b[22]); // 채널
    }

    [Test]
    public void PickSmallest_SmallerRoomWins()
    {
        Assert.AreEqual(1, AudioZone.PickSmallest(new[] { true, true, false }, new[] { 1000f, 50f, 10f }));
        Assert.AreEqual(-1, AudioZone.PickSmallest(new[] { false, false }, new[] { 1f, 2f }));
    }

    [Test]
    public void StrideDone_OneStepPerStride()
    {
        float t = 1.0f;
        Assert.IsTrue(FootstepAudio.StrideDone(ref t, 0.9f));
        Assert.AreEqual(0.1f, t, 1e-4f);
        Assert.IsFalse(FootstepAudio.StrideDone(ref t, 0.9f));
    }

    [Test]
    public void NextIndex_NeverRepeatsLast()
    {
        for (int roll = 0; roll < 50; roll++)
        {
            int i = FootstepAudio.NextIndex(5, 2, roll);
            Assert.AreNotEqual(2, i);
            Assert.That(i, Is.InRange(0, 4));
        }
        Assert.AreEqual(0, FootstepAudio.NextIndex(1, 0, 7));
    }

    [Test]
    public void HintSoundDue_FirstImmediatelyThenEveryInterval()
    {
        float clock = 0f;
        Assert.IsTrue(GazeTarget.HintSoundDue(ref clock, 0.016f, 30f));
        Assert.IsFalse(GazeTarget.HintSoundDue(ref clock, 29f, 30f));
        Assert.IsTrue(GazeTarget.HintSoundDue(ref clock, 1.5f, 30f));
    }

    [Test]
    public void IsWalking_StandingAndTeleportDontCount()
    {
        Assert.IsFalse(FootstepAudio.IsWalking(0f, 0.5f, 12f));   // 서 있음(마네킹 엔딩)
        Assert.IsTrue(FootstepAudio.IsWalking(3f, 0.5f, 12f));
        Assert.IsFalse(FootstepAudio.IsWalking(300f, 0.5f, 12f)); // 순간이동
    }

    [Test]
    public void Gate_SilentUntilActiveAndAfterReveal()
    {
        Assert.AreEqual(0f, AudioZone.Gate(0.5f, false, false)); // 물이 아직 안 차오름
        Assert.AreEqual(0.5f, AudioZone.Gate(0.5f, false, true));
        Assert.AreEqual(0f, AudioZone.Gate(0.5f, true, true));   // 샤워실 기믹 풀림
    }

    [Test]
    public void FadeStep_ReachesZeroInFadeTime()
    {
        float v = 0.8f;
        for (int i = 0; i < 10; i++) v = GameAudio.FadeStep(v, 0.8f, 0.2f, 0.02f);
        Assert.AreEqual(0f, v, 1e-4f);
        Assert.AreEqual(0.4f, GameAudio.FadeStep(0.8f, 0.8f, 0.2f, 0.1f), 1e-4f);
    }

    [Test]
    public void StrideFor_WalkAndRunCadence()
    {
        Assert.AreEqual(1.45f, FootstepAudio.StrideFor(3f, 3f, 5.5f, 1.45f, 2.05f), 1e-4f);
        Assert.AreEqual(2.05f, FootstepAudio.StrideFor(5.5f, 3f, 5.5f, 1.45f, 2.05f), 1e-4f);
        // 초당 걸음: 걷기 약 2, 뛰기 약 2.7
        Assert.That(3f / FootstepAudio.StrideFor(3f, 3f, 5.5f, 1.45f, 2.05f), Is.InRange(1.8f, 2.3f));
        Assert.That(5.5f / FootstepAudio.StrideFor(5.5f, 3f, 5.5f, 1.45f, 2.05f), Is.InRange(2.4f, 3f));
    }

    [Test]
    public void Drive_RaisesQuietPartsKeepsPeak()
    {
        var s = new[] { 0.1f, 1f, -1f };
        AudioCraft.Drive(s, 3f);
        Assert.AreEqual(1f, s[1], 1e-4f);
        Assert.AreEqual(-1f, s[2], 1e-4f);
        Assert.Greater(s[0], 0.25f); // 작은 소리가 커짐
    }

    [Test]
    public void WaterInside_AndDepth()
    {
        Assert.IsTrue(WaterAudio.Inside(Vector3.zero, new Vector3(10f, 1f, 4f), Quaternion.identity, new Vector3(4f, -1f, 1.9f)));
        Assert.IsFalse(WaterAudio.Inside(Vector3.zero, new Vector3(10f, 1f, 4f), Quaternion.identity, new Vector3(4f, 0f, 2.1f)));
        Assert.AreEqual(0.77f, WaterAudio.Depth(-0.23f, -1f), 1e-4f);
    }

    [Test]
    public void MenuAmbience_FollowsVideo()
    {
        Assert.AreEqual("amb_outdoor_pool", MenuAudio.AmbienceFor("menu_01_flamingo"));
        Assert.AreEqual("amb_pool", MenuAudio.AmbienceFor("menu_02_poolhall"));
        Assert.AreEqual("amb_city", MenuAudio.AmbienceFor("menu_03_city"));
    }
}
