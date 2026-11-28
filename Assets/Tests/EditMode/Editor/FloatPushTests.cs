using NUnit.Framework;
using UnityEngine;

/// <summary>물에 뜬 튜브·공 밀기(FloatPush, 2026-11-28 QA)의 속도 규칙.</summary>
public class FloatPushTests
{
    const float Factor = 0.85f;   // FloatPush 기본값
    const float MaxSpeed = 3f;    // FloatPush 기본값

    [Test]
    public void PushedVelocity_FromRest_MovesAlongPushAtFactorSpeed()
    {
        // Arrange
        var dir = Vector3.forward;
        // Act
        var v = FloatPush.PushedVelocity(Vector3.zero, dir, 2f, Factor, MaxSpeed);
        // Assert
        Assert.AreEqual(2f * Factor, v.z, 1e-4f);
        Assert.AreEqual(0f, v.x, 1e-4f);
    }

    [Test]
    public void PushedVelocity_AlreadyFaster_KeepsSpeed()
    {
        // Arrange: 이미 2.5m/s로 밀려 가는 중
        var current = new Vector3(0f, 0f, 2.5f);
        // Act
        var v = FloatPush.PushedVelocity(current, Vector3.forward, 1f, Factor, MaxSpeed);
        // Assert
        Assert.AreEqual(2.5f, v.z, 1e-4f);
    }

    [Test]
    public void PushedVelocity_IgnoresVerticalAndClampsToMax()
    {
        // Arrange
        var dir = new Vector3(1f, 5f, 0f);
        // Act
        var v = FloatPush.PushedVelocity(Vector3.zero, dir, 10f, Factor, MaxSpeed);
        // Assert
        Assert.AreEqual(0f, v.y, 1e-4f);
        Assert.AreEqual(MaxSpeed, v.magnitude, 1e-4f);
    }

    [Test]
    public void PushedVelocity_ZeroSpeed_Unchanged()
    {
        // Arrange
        var current = new Vector3(0.3f, 0f, 0f);
        // Act
        var v = FloatPush.PushedVelocity(current, Vector3.forward, 0f, Factor, MaxSpeed);
        // Assert
        Assert.AreEqual(current, v);
    }

    [Test]
    public void Damped_SlowsDownOverTime()
    {
        // Arrange
        var v0 = new Vector3(1f, 0f, 0f);
        // Act: 끌림 1.4/초로 1초
        var v = FloatPush.Damped(v0, 1.4f, 1f, 0.02f);
        // Assert
        Assert.AreEqual(Mathf.Exp(-1.4f), v.x, 1e-4f);
    }

    [Test]
    public void Damped_BelowStopSpeed_Stops()
    {
        // Arrange
        var v0 = new Vector3(0.01f, 0f, 0f);
        // Act
        var v = FloatPush.Damped(v0, 1.4f, 0.016f, 0.02f);
        // Assert
        Assert.AreEqual(Vector3.zero, v);
    }
}
