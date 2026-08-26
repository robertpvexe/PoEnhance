using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Trade;

public sealed class GemSkillLevelQuerySemanticsTests
{
    [Theory]
    [InlineData("Socketed Gems are Supported by Level <number> Spell Echo")]
    [InlineData("Socketed Gems are Supported by Level # Controlled Destruction")]
    [InlineData("Grants Level <number> Clarity")]
    [InlineData("Grants Level # Wrath")]
    [InlineData("+<number> to Level of all Skill Gems")]
    [InlineData("+# to Level of Socketed Gems")]
    [InlineData("<number> to Level of all Raise Zombie Gems")]
    public void IsGemOrSkillLevelQuery_RecognizesNormalizedLevelShapes(string signature)
    {
        Assert.True(GemSkillLevelQuerySemantics.IsGemOrSkillLevelQuery(signature));
    }

    [Theory]
    [InlineData("75% reduced Maximum number of Summoned Raging Spirits")]
    [InlineData("<number>% increased Armour")]
    [InlineData("Has <number> Abyssal Sockets")]
    [InlineData("Commanded leadership over <number> warriors under Rakiata")]
    [InlineData("You can apply an additional Curse")]
    [InlineData("Level")]
    [InlineData("")]
    [InlineData(null)]
    public void IsGemOrSkillLevelQuery_RejectsNonGemSkillLevelShapes(string? signature)
    {
        Assert.False(GemSkillLevelQuerySemantics.IsGemOrSkillLevelQuery(signature));
    }
}
