using NUnit.Framework;

/// <summary>설정 메뉴 DLSS 항목: 켬이고 그래픽 카드가 지원할 때만 DLSS로 그린다.</summary>
public class GameSettingsTests
{
    const int Off = 0, On = 1;

    [Test]
    public void UseDlss_OnAndSupported_True()
    {
        Assert.IsTrue(GameSettings.UseDlss(On, true));
    }

    [Test]
    public void UseDlss_OnButUnsupported_False()
    {
        Assert.IsFalse(GameSettings.UseDlss(On, false));
    }

    [Test]
    public void UseDlss_OffAndSupported_False()
    {
        Assert.IsFalse(GameSettings.UseDlss(Off, true));
    }
}
