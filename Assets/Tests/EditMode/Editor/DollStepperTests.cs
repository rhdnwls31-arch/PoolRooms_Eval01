using NUnit.Framework;

/// <summary>목각인형 화실(§6-3b2)의 다가오기 규칙(DollStepper.Approach). 깊이 = 입구에서 앞(북쪽)으로 거리.</summary>
public class DollStepperTests
{
    const float KeepAway = 3.5f; // 그레이박스 값

    [Test]
    public void PlayerStaysAtEntry_DollsReachStageTarget()
    {
        // Arrange
        float current = 15f, target = 11.5f, player = 1f;

        // Act
        float next = DollStepper.Approach(current, target, player, KeepAway);

        // Assert
        Assert.AreEqual(11.5f, next, 1e-5f);
    }

    [Test]
    public void PlayerWalkedForward_DollsStopKeepAwayInFront()
    {
        // Arrange: 플레이어가 7m까지 걸어왔다
        float current = 12f, target = 5f, player = 7f;

        // Act
        float next = DollStepper.Approach(current, target, player, KeepAway);

        // Assert: 목표 5m가 아니라 7 + 3.5 = 10.5m에서 멈춘다
        Assert.AreEqual(10.5f, next, 1e-5f);
    }

    [Test]
    public void PlayerCloseToDolls_DollsDoNotMoveBack()
    {
        // Arrange: 인형 바로 앞(14m)
        float current = 15f, target = 11.5f, player = 14f;

        // Act
        float next = DollStepper.Approach(current, target, player, KeepAway);

        // Assert
        Assert.AreEqual(15f, next, 1e-5f);
    }

    [Test]
    public void PlayerBehindDolls_DollsDoNotMoveBack()
    {
        // Arrange: 인형을 지나 북쪽(17m)
        float current = 15f, target = 11.5f, player = 17f;

        // Act
        float next = DollStepper.Approach(current, target, player, KeepAway);

        // Assert
        Assert.AreEqual(15f, next, 1e-5f);
    }
}
