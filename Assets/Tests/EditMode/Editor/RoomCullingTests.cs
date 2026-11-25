using NUnit.Framework;
using UnityEngine;

/// <summary>먼 방 안 그리기(RoomCulling)와 TV 카메라 근처에서만 그리기(CameraOnlyNear)의 판정 규칙.</summary>
public class RoomCullingTests
{
    const float CullDistance = 30f;   // RoomCulling 기본값
    static readonly Bounds Room = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f)); // 가로 10m 방(가장자리 x = ±5)

    [Test]
    public void ShouldDraw_InsideRoom_True()
    {
        // Arrange
        var eye = new Vector3(1f, 1.6f, 1f);
        // Act
        bool draw = RoomCulling.ShouldDraw(Room, eye, CullDistance);
        // Assert
        Assert.IsTrue(draw);
    }

    [Test]
    public void ShouldDraw_JustInsideCullDistance_True()
    {
        // Arrange: 방 가장자리(x = 5)에서 29m
        var eye = new Vector3(5f + 29f, 0f, 0f);
        // Act
        bool draw = RoomCulling.ShouldDraw(Room, eye, CullDistance);
        // Assert
        Assert.IsTrue(draw);
    }

    [Test]
    public void ShouldDraw_BeyondCullDistance_False()
    {
        // Arrange: 방 가장자리에서 31m
        var eye = new Vector3(5f + 31f, 0f, 0f);
        // Act
        bool draw = RoomCulling.ShouldDraw(Room, eye, CullDistance);
        // Assert
        Assert.IsFalse(draw);
    }

    [Test]
    public void Near_InsideMarginButOutsideRoom_True()
    {
        // Arrange: 방 가장자리에서 3m 밖, 여유 4m
        var eye = new Vector3(5f + 3f, 0f, 0f);
        // Act / Assert
        Assert.IsTrue(CameraOnlyNear.Near(Room, 4f, eye));
    }

    [Test]
    public void Near_BeyondMargin_False()
    {
        // Arrange: 방 가장자리에서 5m 밖, 여유 4m
        var eye = new Vector3(5f + 5f, 0f, 0f);
        // Act / Assert
        Assert.IsFalse(CameraOnlyNear.Near(Room, 4f, eye));
    }
}
