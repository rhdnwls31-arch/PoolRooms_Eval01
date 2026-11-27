using NUnit.Framework;

/// <summary>방에 들어서면 자동 저장(RoomCheckpoint): 어느 방을 저장할지 고르는 규칙.</summary>
public class RoomCheckpointTests
{
    const int RoomCount = 13; // RoomRoute.RoomNames 수(인트로 + 12개 방)

    static bool[] Entered(params int[] rooms)
    {
        var e = new bool[RoomCount];
        foreach (var r in rooms) e[r] = true;
        return e;
    }

    [Test]
    public void NextReached_NoNewRoom_KeepsCurrent()
    {
        Assert.AreEqual(3, RoomCheckpoint.NextReached(3, Entered()));
    }

    [Test]
    public void NextReached_EnteredNextRoom_AdvancesToIt()
    {
        Assert.AreEqual(4, RoomCheckpoint.NextReached(3, Entered(4)));
    }

    [Test]
    public void NextReached_EarlierRoomEnteredAgain_DoesNotGoBack()
    {
        // 앞 방 통로를 다시 지나가도 저장된 방이 뒤로 가지 않는다
        Assert.AreEqual(5, RoomCheckpoint.NextReached(5, Entered(2)));
    }

    [Test]
    public void NextReached_SeveralEntered_TakesFurthest()
    {
        Assert.AreEqual(9, RoomCheckpoint.NextReached(6, Entered(7, 9)));
    }

    [Test]
    public void RoomRoute_EveryRoomHasEntryMarker()
    {
        Assert.AreEqual(RoomRoute.RoomNames.Length, RoomRoute.EntryMarkers.Length);
        for (int i = 1; i < RoomRoute.EntryMarkers.Length; i++)
            Assert.IsFalse(string.IsNullOrEmpty(RoomRoute.EntryMarkers[i]), "방 " + i + "에 들어가는 표시가 없다");
    }
}
