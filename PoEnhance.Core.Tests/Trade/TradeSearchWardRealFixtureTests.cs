using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Tests.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchWardRealFixtureTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();
    private readonly Lazy<GameDataCatalog> catalog = new(LoadPackagedCatalog);

    public static TheoryData<int, string, string, decimal, decimal, string[]> Cases => new()
    {
        // clipboardObserved, derivedQ20 — genuine copies from zfortests_v2.txt
        { 0, "Hypnotic Keep", "Runic Crown", 320m, 384m, ["item.ward.added.local", "item.ward.increased-percent.local"] },
        { 1, "Onslaught Cowl", "Runic Crown", 235m, 282m, ["item.ward.added.local"] },
        { 2, "Havoc Grip", "Runic Gauntlets", 256m, 307m, ["item.ward.increased-percent.local", "item.ward.added.local"] },
        { 3, "Damnation Stride", "Runic Sollerets", 118m, 142m, ["item.ward.added.local", "item.ward.increased-percent.local"] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void CreateDraft_GenuineWardFixtures_NormalizeQ0ClipboardToQ20(
        int fixtureIndex,
        string expectedName,
        string expectedBase,
        decimal clipboardObserved,
        decimal expectedQ20,
        string[] expectedWardSemantics)
    {
        var draft = CreateDraft(WardRealFixtures.LoadItems()[fixtureIndex]);

        Assert.Equal(expectedName, draft.DisplayName);
        Assert.Equal(expectedBase, draft.ParsedBaseType);
        var ward = Assert.Single(
            draft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.Ward);
        Assert.Equal(expectedQ20, ward.ObservedValue);
        Assert.Equal("Q20", ward.CalculationBasisLabel);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.NormalizedToTwenty,
            ward.QualityNormalizationStatus);
        Assert.Null(ward.DerivationUnsupportedReason);
        Assert.Equal(expectedQ20, ward.RequestedMinimum);
        Assert.Equal(
            clipboardObserved,
            Assert.Single(ward.SourceProperties).NumericGroups[0].ScalarValue);
        Assert.DoesNotContain(
            draft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.ChanceToBlock);

        Assert.Equal(
            expectedWardSemantics,
            draft.ModifierFilters
                .Where(component => component.ReviewedItemPropertySemantic is not null &&
                    component.ReviewedItemPropertySemantic.Contributions.Any(contribution =>
                        contribution.Targets.Contains(ItemPropertyTarget.Ward)))
                .Select(component => component.ReviewedItemPropertySemantic!.Id)
                .ToArray());

        var wardGroup = Assert.Single(
            draft.ItemPropertyContributionGroups,
            group => group.ParentKind == TradeSearchItemPropertyKind.Ward);
        Assert.Equal(expectedWardSemantics.Length, wardGroup.Contributions.Length);
        Assert.All(
            wardGroup.Contributions,
            contribution => Assert.Equal(ItemPropertyTarget.Ward, contribution.Target));
    }

    [Fact]
    public void CreateDraft_DamnationStride_KeepsWardAndStunRecoveryWithoutInventingBlockParent()
    {
        var draft = CreateDraft(WardRealFixtures.DamnationStride);

        var increasedWard = Assert.Single(
            draft.ModifierFilters,
            component => component.ReviewedItemPropertySemantic?.Id == "item.ward.increased-percent.local");
        Assert.Contains("26(21-26)% increased Ward", increasedWard.OriginalText, StringComparison.Ordinal);
        Assert.Contains(
            draft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Stun and Block Recovery",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            draft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.ChanceToBlock);
        Assert.DoesNotContain(
            draft.ItemPropertyContributionGroups,
            group => group.ParentKind == TradeSearchItemPropertyKind.ChanceToBlock);
    }

    private TradeSearchDraft CreateDraft(string itemText)
    {
        var item = parser.Parse(itemText);
        var baseResolution = baseResolver.Resolve(item, catalog.Value);
        Assert.Equal(ItemBaseResolutionStatus.Exact, baseResolution.Status);
        var resolutions = modifierResolver.Resolve(item, catalog.Value, baseResolution);
        var result = mapper.CreateDraft(
            item,
            itemBaseResolution: baseResolution,
            modifierResolutions: resolutions,
            gameDataCatalog: catalog.Value);

        Assert.True(
            result.IsSuccess,
            string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static GameDataCatalog LoadPackagedCatalog()
    {
        var packagePath = FindRepoFile("artifacts", "poenhance-game-data.json");
        var loaded = GameDataPackageLoader
            .LoadFromFileAsync(packagePath)
            .GetAwaiter()
            .GetResult();
        Assert.True(loaded.IsSuccess);
        Assert.NotNull(loaded.Package);
        return GameDataCatalog.FromPackage(loaded.Package!);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(relativeParts)}.");
    }
}
