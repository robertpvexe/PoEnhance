using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using System.Globalization;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeStatTemplateNormalizerTests
{
    [Theory]
    [InlineData("+87 to maximum Life", "+# to maximum Life", "87")]
    [InlineData("87 to maximum Life", "# to maximum Life", "87")]
    [InlineData("-12% to Fire Resistance", "-#% to Fire Resistance", "-12")]
    [InlineData("1.5% of Physical Attack Damage Leeched as Life", "#% of Physical Attack Damage Leeched as Life", "1.5")]
    [InlineData("Adds 10 to 20 Fire Damage", "Adds # to # Fire Damage", "10,20")]
    [InlineData("  Adds   10\u00a0to   20 Fire Damage  ", "Adds # to # Fire Damage", "10,20")]
    [InlineData("\u221212% to Fire Resistance", "-#% to Fire Resistance", "-12")]
    [InlineData("(10-20) Fire Damage", "(#-#) Fire Damage", "10,20")]
    public void NormalizeModifierText_NormalizesSupportedNumericAndWhitespaceForms(
        string input,
        string expectedTemplate,
        string expectedValuesCsv)
    {
        var result = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(input);

        Assert.Null(result.Diagnostic);
        Assert.Equal(expectedTemplate, result.NormalizedTemplate);
        Assert.Equal(
            expectedValuesCsv
                .Split(',')
                .Select(value => decimal.Parse(value, CultureInfo.InvariantCulture))
                .ToArray(),
            result.ExtractedNumericValues);
    }

    [Fact]
    public void NormalizeModifierText_PunctuationAndWordOrderRemainSignificant()
    {
        var first = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText("Adds 10 to 20 Fire Damage");
        var punctuation = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText("Adds 10, to 20 Fire Damage");
        var wordOrder = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText("Fire Damage Adds 10 to 20");

        Assert.NotEqual(first.NormalizedTemplate, punctuation.NormalizedTemplate);
        Assert.NotEqual(first.NormalizedTemplate, wordOrder.NormalizedTemplate);
    }

    [Fact]
    public void NormalizeModifierText_DifferentWordsDoNotFuzzyMatch()
    {
        var life = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText("+87 to maximum Life");
        var mana = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText("+87 to maximum Mana");

        Assert.NotEqual(life.NormalizedTemplate, mana.NormalizedTemplate);
    }

    [Fact]
    public void NormalizeModifierText_UnsupportedCommaNumberIsDiagnosed()
    {
        var result = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText("Adds 1,000 Fire Damage");

        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.UnsupportedNumericTokenFormat,
            result.Diagnostic?.Code);
    }
}
