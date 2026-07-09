using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Tests.Items.Parsing;

public sealed class ItemTextParserTests
{
    private readonly ItemTextParser _parser = new();

    [Fact]
    public void Parse_LeatherBeltSample_ExtractsExpectedFieldsAndPreservesRawText()
    {
        var rawText = ReadSample("unique-leather-belt.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Belts", result.ItemClass);
        Assert.Equal("Unique", result.Rarity);
        Assert.Equal("Screams of the Desiccated", result.Name);
        Assert.Equal("Leather Belt", result.BaseType);
        Assert.Equal(85, result.ItemLevel);
        Assert.Contains("Requirements:", result.PropertyLines);
        Assert.Contains("Level: 56", result.PropertyLines);
        Assert.Contains("+35 to maximum Life (implicit)", result.ModifierLines);
        Assert.Contains("You have Diamond Shrine Buff while affected by no Flasks", result.ModifierLines);
        Assert.Contains("+32 to Intelligence", result.ModifierLines);
        Assert.Contains("+26% to Chaos Resistance", result.ModifierLines);
        Assert.Contains("\"I staggered on, dying of thirst... I thought I found respite,", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_IncompleteSample_DoesNotThrowAndKeepsUnclassifiedLines()
    {
        var rawText = ReadSample("incomplete-item.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Rare", result.Rarity);
        Assert.Equal("Half Remembered Item", result.Name);
        Assert.Null(result.BaseType);
        Assert.Null(result.ItemLevel);
        Assert.Contains("Unexpected loose line", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_WhitespaceOnlySample_PreservesRawTextAndReturnsEmptyResult()
    {
        var rawText = ReadSample("whitespace-only.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Null(result.ItemClass);
        Assert.Null(result.Rarity);
        Assert.Null(result.Name);
        Assert.Null(result.BaseType);
        Assert.Null(result.ItemLevel);
        Assert.Empty(result.PropertyLines);
        Assert.Empty(result.ModifierLines);
        Assert.Empty(result.UnclassifiedLines);
    }

    private static string ReadSample(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Items", fileName));
    }
}
