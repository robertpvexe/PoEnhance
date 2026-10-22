using System.Reflection;
using System.Text.RegularExpressions;
using PoEnhance.DataImport;

namespace PoEnhance.DataImport.Tests;

public sealed class ImpossibleEscapeDynamicPatternMatchTests
{
    [Fact]
    public void MatchesDynamicPattern_FoldsPassageChromeAndIgnoresCase()
    {
        var patternLines = new[]
        {
            Regex.Escape("Passive Skills in Radius of {0} can be Allocated")
                .Replace(Regex.Escape("{0}"), "[^\\r\\n]+?", StringComparison.Ordinal),
            Regex.Escape("without being connected to your tree"),
            Regex.Escape("Passage"),
        };
        var dynamicText = string.Join("\\n", patternLines);
        var pattern = new Regex($"\\A{dynamicText}\\z", RegexOptions.CultureInvariant);
        var lines = new[]
        {
            "Passive Skills in radius of Chaos Inoculation can be allocated without being connected to your tree",
        };

        var method = typeof(PoBUniqueCatalogImporter).GetMethod(
            "MatchesDynamicPattern",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var matched = Assert.IsType<bool>(method!.Invoke(null, [pattern, lines]));
        Assert.True(
            matched,
            $"pattern.ToString()={pattern}; hasRealNl={pattern.ToString().Contains('\n')}; hasEscapedNl={pattern.ToString().Contains("\\n")}");
    }
}
