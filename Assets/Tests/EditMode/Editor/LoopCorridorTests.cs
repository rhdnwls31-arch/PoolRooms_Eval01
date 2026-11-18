using NUnit.Framework;

/// <summary>꽃무늬 복도의 반복 규칙(LoopCorridor.LoopShift). 찾기 전에는 loopAt을 넘으면 segment 단위로 되돌린다.</summary>
public class LoopCorridorTests
{
    const float LoopAt = 40f;    // 그레이박스 값
    const float Segment = 20f;

    [Test]
    public void LoopShift_BeforeLoopPoint_NoShift()
    {
        Assert.AreEqual(0f, LoopCorridor.LoopShift(39.9f, LoopAt, Segment, false));
        Assert.AreEqual(0f, LoopCorridor.LoopShift(40f, LoopAt, Segment, false), "딱 경계에서는 그대로");
    }

    [Test]
    public void LoopShift_PastLoopPoint_ShiftsOneSegment()
    {
        Assert.AreEqual(Segment, LoopCorridor.LoopShift(40.3f, LoopAt, Segment, false));
    }

    [Test]
    public void LoopShift_FarPast_ShiftsWholeSegmentsBackIntoBand()
    {
        float d = 75f;
        float shifted = d - LoopCorridor.LoopShift(d, LoopAt, Segment, false);
        Assert.LessOrEqual(shifted, LoopAt);
        Assert.Greater(shifted, LoopAt - Segment);
    }

    [Test]
    public void LoopShift_AfterFound_NeverShifts()
    {
        Assert.AreEqual(0f, LoopCorridor.LoopShift(60f, LoopAt, Segment, true));
    }
}
