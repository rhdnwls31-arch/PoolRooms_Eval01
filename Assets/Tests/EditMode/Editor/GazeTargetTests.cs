using NUnit.Framework;
using UnityEngine;

/// <summary>응시로 찾기(기획서 §4-1)의 판정 규칙. GazeTarget의 정적 함수만 시험한다.</summary>
public class GazeTargetTests
{
    const float Need = 1.5f;      // 기획서: 1.5초 바라보면 찾음
    const float MaxDist = 6f;     // 기획서: 6m 안

    [Test]
    public void Accumulate_RequiresContinuousGaze()
    {
        float dwell = 0f;
        Assert.IsFalse(GazeTarget.Accumulate(ref dwell, true, 1.0f, Need));
        Assert.IsFalse(GazeTarget.Accumulate(ref dwell, false, 0.1f, Need), "눈을 떼면 다시 센다");
        Assert.AreEqual(0f, dwell);
        Assert.IsFalse(GazeTarget.Accumulate(ref dwell, true, 1.0f, Need));
        Assert.IsTrue(GazeTarget.Accumulate(ref dwell, true, 0.5f, Need));
    }

    [Test]
    public void SwapStep_OnlyAfterSeenAndThenOffscreen()
    {
        float unseen = 0f;
        Assert.IsFalse(GazeTarget.SwapStep(ref unseen, false, false, 5f, 3f), "본 적 없으면 바뀌지 않는다");
        Assert.IsFalse(GazeTarget.SwapStep(ref unseen, true, true, 5f, 3f), "보고 있는 동안은 바뀌지 않는다");
        Assert.IsFalse(GazeTarget.SwapStep(ref unseen, true, false, 2f, 3f));
        Assert.IsFalse(GazeTarget.SwapStep(ref unseen, true, true, 0.1f, 3f), "다시 보면 처음부터");
        Assert.IsFalse(GazeTarget.SwapStep(ref unseen, true, false, 2f, 3f));
        Assert.IsTrue(GazeTarget.SwapStep(ref unseen, true, false, 1f, 3f));
    }

    [Test]
    public void RayReaches_RespectsDistanceAndOccluder()
    {
        var target = new Bounds(new Vector3(0, 0, 4f), Vector3.one);
        var ray = new Ray(Vector3.zero, Vector3.forward);
        Assert.IsTrue(GazeTarget.RayReaches(ray, target, MaxDist, float.PositiveInfinity));
        Assert.IsFalse(GazeTarget.RayReaches(ray, target, 3f, float.PositiveInfinity), "너무 멀다");
        Assert.IsFalse(GazeTarget.RayReaches(ray, target, MaxDist, 2f), "앞이 가려져 있다");
        Assert.IsFalse(GazeTarget.RayReaches(new Ray(Vector3.zero, Vector3.right), target, MaxDist, float.PositiveInfinity), "다른 곳을 본다");
    }
}
