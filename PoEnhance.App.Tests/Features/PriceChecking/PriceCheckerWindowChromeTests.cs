using System.Windows;
using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerWindowChromeTests
{
    private const int ExtendedNoActivateStyle = 0x08000000;

    [Fact]
    public void ChromeSettings_ConfigureOverlayToolWindowWithoutPermanentNoActivate()
    {
        Assert.False(PriceCheckerWindowChrome.ShowActivated);
        Assert.False(PriceCheckerWindowChrome.ShowInTaskbar);
        Assert.True(PriceCheckerWindowChrome.Topmost);
        Assert.Equal(ResizeMode.NoResize, PriceCheckerWindowChrome.ResizeMode);
        Assert.Equal(WindowStyle.None, PriceCheckerWindowChrome.WindowStyle);
        Assert.Equal(0x00000080, PriceCheckerWindowChrome.ExtendedToolWindowStyle);
        Assert.Equal(0x00040000, PriceCheckerWindowChrome.ExtendedAppWindowStyle);
        Assert.Equal(0, PriceCheckerWindowChrome.ExtendedToolWindowStyle & ExtendedNoActivateStyle);
    }
}
