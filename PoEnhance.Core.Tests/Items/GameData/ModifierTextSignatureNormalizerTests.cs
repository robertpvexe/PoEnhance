using PoEnhance.Core.Items.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ModifierTextSignatureNormalizerTests
{
    [Theory]
    [InlineData("Adds 46(41-55) to 81(81-95) Cold Damage", "Adds <number> to <number> Cold Damage")]
    [InlineData("Adds 70(63-85) to 139(128-148) Fire Damage", "Adds <number> to <number> Fire Damage")]
    [InlineData("Adds 9(8-10) to 155(148-173) Lightning Damage", "Adds <number> to <number> Lightning Damage")]
    [InlineData("+53(51-55) to Dexterity", "+<number> to Dexterity")]
    [InlineData("20(16-20)% increased Global Accuracy Rating", "<number>% increased Global Accuracy Rating")]
    [InlineData("15(14-16)% increased Trap Damage", "<number>% increased Trap Damage")]
    [InlineData("5(3-5)% increased Attack Speed", "<number>% increased Attack Speed")]
    [InlineData("+2(2-3)% Chance to Block Spell Damage while holding a Shield", "+<number>% Chance to Block Spell Damage while holding a Shield")]
    [InlineData("+101(100-114) to maximum Life", "+<number> to maximum Life")]
    [InlineData("+47(46-48)% to Lightning Resistance", "+<number>% to Lightning Resistance")]
    [InlineData("25(23-25)% increased Stun and Block Recovery", "<number>% increased Stun and Block Recovery")]
    [InlineData("Regenerate 29.2(24.1-32) Life per second", "Regenerate <number> Life per second")]
    [InlineData("-29(13-15)% reduced Damage Taken", "-<number>% reduced Damage Taken")]
    [InlineData("10(-5--3)% changed Example", "<number>% changed Example")]
    public void NormalizeLine_RemovesAttachedNumericRollRangeAnnotations(string input, string expected)
    {
        var normalized = ModifierTextSignatureNormalizer.NormalizeLine(input);

        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("Adds 46 (41-55) to 81 Cold Damage", "Adds <number> (<number>-<number>) to <number> Cold Damage")]
    [InlineData("Condition (41-55) applies", "Condition (<number>-<number>) applies")]
    [InlineData("Skills fire an additional Projectile (Bow Attacks only)", "Skills fire an additional Projectile (Bow Attacks only)")]
    public void NormalizeLine_DoesNotRemoveOrdinaryParentheses(string input, string expected)
    {
        var normalized = ModifierTextSignatureNormalizer.NormalizeLine(input);

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void CreateParsedSignature_IgnoresVerifiedReminderLines()
    {
        var result = ModifierTextSignatureNormalizer.CreateParsedSignature(
            [
                "10(8-10)% of Damage taken Recouped as Life",
                "(Only Damage from Hits can be Recouped, over 4 seconds following the Hit)",
            ]);

        Assert.False(result.HasUnsupportedExplanatoryLine);
        Assert.Equal(["<number>% of Damage taken Recouped as Life"], result.Signature.Lines);
    }

    [Fact]
    public void CreateParsedSignature_MarksUnverifiedParenthesizedLinesUnsupported()
    {
        var result = ModifierTextSignatureNormalizer.CreateParsedSignature(
            [
                "10(8-10)% increased Damage",
                "(Unverified explanatory text)",
            ]);

        Assert.True(result.HasUnsupportedExplanatoryLine);
        Assert.Equal(["<number>% increased Damage"], result.Signature.Lines);
    }
}
