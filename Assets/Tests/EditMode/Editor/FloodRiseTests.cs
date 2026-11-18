using NUnit.Framework;

/// <summary>가라앉은 집들의 도시의 물 차오름 곡선(FloodRise.Fill). 처음엔 느리고 끝으로 갈수록 빨라야 한다.</summary>
public class FloodRiseTests
{
    const float Power = 3f;      // 그레이박스 값: 발목~무릎에 10초쯤 머문다
    const float Top = 70.3f;     // 수위 범위(-0.3 → 70)
    const float Duration = 40f;

    [Test]
    public void Fill_StartsEmptyAndEndsFull()
    {
        Assert.AreEqual(0f, FloodRise.Fill(0f, Power), 1e-5f);
        Assert.AreEqual(1f, FloodRise.Fill(1f, Power), 1e-5f);
        Assert.AreEqual(1f, FloodRise.Fill(2f, Power), 1e-5f, "다 찬 뒤로는 그대로");
        Assert.AreEqual(0f, FloodRise.Fill(-1f, Power), 1e-5f);
    }

    [Test]
    public void Fill_StaysBelowKneeForFirstEightSeconds()
    {
        float level = FloodRise.Fill(8f / Duration, Power) * Top;
        Assert.Less(level, 0.6f, "처음 8초는 무릎 아래(약 0.3m 올라온 바닥 기준)");
    }

    [Test]
    public void Fill_GetsFasterOverTime()
    {
        float early = FloodRise.Fill(0.2f, Power) - FloodRise.Fill(0.1f, Power);
        float late = FloodRise.Fill(0.9f, Power) - FloodRise.Fill(0.8f, Power);
        Assert.Greater(late, early);
    }
}
