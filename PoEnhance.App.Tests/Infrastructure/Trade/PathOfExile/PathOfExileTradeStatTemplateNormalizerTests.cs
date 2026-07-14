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
    [InlineData("+53(51-55) to Dexterity", "+# to Dexterity", "53")]
    [InlineData("Adds 46(41-55) to 81(81-95) Cold Damage", "Adds # to # Cold Damage", "46,81")]
    [InlineData("+47(46-48)% to Lightning Resistance", "+#% to Lightning Resistance", "47")]
    [InlineData("+101(100-114) to maximum Life", "+# to maximum Life", "101")]
    [InlineData("28(26-28)% increased Stun and Block Recovery", "#% increased Stun and Block Recovery", "28")]
    [InlineData(
        "Reflects 10(5-10) Physical Damage to Melee Attackers",
        "Reflects # Physical Damage to Melee Attackers",
        "10")]
    [InlineData("28(26\u201328)% increased Stun and Block Recovery", "#% increased Stun and Block Recovery", "28")]
    [InlineData("Adds 1.5(1.0-2.0) to -2.5(-3.0--2.0) Fire Damage", "Adds # to -# Fire Damage", "1.5,-2.5")]
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
    public void NormalizeModifierText_StrictAdvancedRangeAnnotationsDoNotExtractTierBoundaries()
    {
        var result = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(
            "Adds 46(41-55) to 81(81-95) Cold Damage");

        Assert.Equal([46m, 81m], result.ExtractedNumericValues);
        Assert.DoesNotContain(41m, result.ExtractedNumericValues);
        Assert.DoesNotContain(55m, result.ExtractedNumericValues);
        Assert.DoesNotContain(95m, result.ExtractedNumericValues);
    }

    [Fact]
    public void NormalizeModifierText_OrdinaryParenthesesRemainSignificant()
    {
        var result = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(
            "Nearby Allies have (10-20)% increased Damage");

        Assert.Null(result.Diagnostic);
        Assert.Equal("Nearby Allies have (#-#)% increased Damage", result.NormalizedTemplate);
        Assert.Equal([10m, 20m], result.ExtractedNumericValues);
    }

    [Fact]
    public void NormalizeModifierText_MalformedAttachedParentheticalIsNotStripped()
    {
        var result = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(
            "Adds 10(foo) Fire Damage");

        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.MalformedAdvancedRangeAnnotation,
            result.Diagnostic?.Code);
        Assert.Equal("Adds 10(foo) Fire Damage", result.NormalizedTemplate);
        Assert.Empty(result.ExtractedNumericValues);
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

    [Fact]
    public void NormalizeLookupTemplate_StripsOnlyTerminalProviderLocalAnnotation()
    {
        Assert.Equal(
            "Adds # to # Fire Damage",
            PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate("Adds # to # Fire Damage (Local)"));
        Assert.True(PathOfExileTradeStatTemplateNormalizer.HasProviderLocalAnnotation(
            "Adds # to # Fire Damage (Local)"));
    }

    [Fact]
    public void NormalizeLookupTemplate_PreservesNonTerminalOrUnrelatedParentheses()
    {
        Assert.Equal(
            "Nearby Allies have (#-#)% increased Damage",
            PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                "Nearby Allies have (10-20)% increased Damage"));
        Assert.Equal(
            "Adds # to # (Local) Fire Damage",
            PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                "Adds # to # (Local) Fire Damage"));
        Assert.False(PathOfExileTradeStatTemplateNormalizer.HasProviderLocalAnnotation(
            "Adds # to # (Local) Fire Damage"));
    }

    [Theory]
    [InlineData("Adds # to # Fire Damage to Attacks")]
    [InlineData("Adds # to # Fire Damage to Spells")]
    [InlineData("Adds # to # Fire Damage with this Weapon")]
    [InlineData("Adds # to # Fire Damage while Channelling")]
    [InlineData("Adds # to # Fire Damage against Ignited Enemies")]
    public void NormalizeLookupTemplate_PreservesScopeWords(string providerText)
    {
        Assert.Equal(providerText, PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(providerText));
    }
}
