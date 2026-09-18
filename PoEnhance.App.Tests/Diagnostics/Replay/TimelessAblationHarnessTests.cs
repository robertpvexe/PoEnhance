using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

public sealed class TimelessAblationHarnessTests
{
    [Fact]
    public void Transform_NormalizeHistoric_PreservesValidClipboard()
    {
        var result = TimelessAblationClipboard.NormalizeHistoricWording(SampleRealLethalPride);
        Assert.True(result.IsValid, result.InvalidReason);
        Assert.Contains("\nHistoric\n", "\n" + result.Text!.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("Unscalable Value", result.Text, StringComparison.Ordinal);
        Assert.True(TimelessAblationClipboard.LooksLikeValidJewelClipboard(result.Text));
    }

    [Fact]
    public void Transform_InvalidMutator_IsRejected()
    {
        var result = TimelessAblationClipboard.Apply(
            "not-a-clipboard",
            "bad",
            _ => ["nope"]);
        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.InvalidReason));
    }

    [Fact]
    public void ReverseAddition_SupportsHistoricUnscalableInsert()
    {
        var result = TimelessAblationClipboard.BuildGreenPlusFeature(
            SampleFixtureLethalPride,
            SampleRealLethalPride,
            "add_real_historic_wording");
        Assert.True(result.IsValid, result.InvalidReason);
        Assert.Contains("Unscalable Value", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_RealVsFixture_SameValueLines_ButRealHasUnscalableFlag()
    {
        var real = new ItemTextParser().Parse(SampleRealLethalPride);
        var fixture = new ItemTextParser().Parse(SampleFixtureLethalPride);
        var realSeed = Assert.Single(real.Modifiers);
        var fixtureSeed = Assert.Single(fixture.Modifiers);
        Assert.Equal(3, realSeed.ValueLines.Count);
        Assert.Equal(3, fixtureSeed.ValueLines.Count);
        Assert.Equal(fixtureSeed.ValueLines, realSeed.ValueLines);
        Assert.True(realSeed.HasUnscalableValue);
        Assert.False(fixtureSeed.HasUnscalableValue);
    }

    [Fact]
    public void AblationSources_DoNotContainItemNameHardcodedProductionBranches()
    {
        var files = Directory.GetFiles(
            Path.Combine(FindRepoRoot(), "PoEnhance.App.Tests", "Diagnostics", "Replay"),
            "TimelessAblation*.cs");
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("if (itemName == \"Lethal Pride\")", source, StringComparison.Ordinal);
            Assert.DoesNotContain("case \"Lethal Pride\":", source, StringComparison.Ordinal);
        }
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")) ||
                File.Exists(Path.Combine(directory.FullName, "PoEnhance.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private const string SampleFixtureLethalPride = """
Item Class: Jewels
Rarity: Unique
Lethal Pride
Timeless Jewel
--------
Item Level: 86
--------
{ Unique Modifier }
Commanded leadership over 14245(10000-18000) warriors under Rakiata(Akoya-Rakiata)
Passives in radius are Conquered by the Karui
Historic
""";

    private const string SampleRealLethalPride = """
Item Class: Jewels
Rarity: Unique
Lethal Pride
Timeless Jewel
--------
Limited to: 1 Historic
Radius: Large
--------
Item Level: 84
--------
{ Unique Modifier }
Commanded leadership over 14245(10000-18000) warriors under Rakiata(Akoya-Rakiata)
Passives in radius are Conquered by the Karui
(Conquered Passive Skills cannot be modified by other Jewels)
Historic — Unscalable Value
--------
They believed themselves the greatest warriors, but that savagery turned upon their own.
--------
Place into an allocated Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket.
""";
}
