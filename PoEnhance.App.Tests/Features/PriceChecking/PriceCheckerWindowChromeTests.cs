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

    [Fact]
    public void PriceCheckerWindowXaml_LeftResizeGripUsesCenteredSizeWeAffordance()
    {
        var xaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerWindow.xaml"));
        var resizeThumb = ExtractElement(xaml, "<Thumb x:Name=\"HorizontalResizeThumb\"", "</Thumb>");
        var resizeThumbTriggers = ExtractElement(
            resizeThumb,
            "<ControlTemplate.Triggers>",
            "</ControlTemplate.Triggers>");

        Assert.Equal(1, CountOccurrences(xaml, "x:Name=\"HorizontalResizeThumb\""));
        Assert.Contains("Width=\"14\"", resizeThumb);
        Assert.Contains("HorizontalAlignment=\"Left\"", resizeThumb);
        Assert.DoesNotContain("HorizontalAlignment=\"Right\"", resizeThumb);
        Assert.Contains("Cursor=\"SizeWE\"", resizeThumb);
        Assert.DoesNotContain("Cursor=\"SizeNS\"", resizeThumb);
        Assert.Contains("ToolTip=\"Drag to resize\"", resizeThumb);
        Assert.Contains("x:Name=\"ResizeGripGlyph\"", resizeThumb);
        Assert.Contains("VerticalAlignment=\"Center\"", resizeThumb);
        Assert.Contains("Property=\"IsMouseOver\"", resizeThumb);
        Assert.Contains("Property=\"IsDragging\"", resizeThumb);
        Assert.Contains("Property=\"Opacity\"", resizeThumbTriggers);
        Assert.DoesNotContain("Property=\"Width\"", resizeThumbTriggers);
        Assert.DoesNotContain("Property=\"Height\"", resizeThumbTriggers);
        Assert.DoesNotContain("Property=\"Margin\"", resizeThumbTriggers);
    }

    private static string ExtractElement(
        string source,
        string startMarker,
        string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find {startMarker}.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find {endMarker}.");
        return source[start..(end + endMarker.Length)];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
