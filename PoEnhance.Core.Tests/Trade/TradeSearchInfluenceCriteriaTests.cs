using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchInfluenceCriteriaTests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();
    private readonly TradeSearchDraftValidator validator = new();

    [Fact]
    public void CreateDraft_OrdinaryItemWithoutInfluenceHasNoInfluenceCriteria()
    {
        var item = parser.Parse("""
Item Class: Body Armours
Rarity: Rare
Plain Shell
Astral Plate
--------
Item Level: 84
--------
+55 to maximum Life
""");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.Empty(draft.TraditionalInfluences);
        Assert.Empty(draft.EldritchInfluences);
        Assert.Empty(draft.InfluenceCriteria);
        Assert.True(validator.Validate(draft).IsValid);
    }

    [Fact]
    public void CreateDraft_SingleTraditionalInfluenceDefaultsEnabledIndependentOfRarity()
    {
        var item = parser.Parse("""
Item Class: Body Armours
Rarity: Rare
Shaper Shell
Astral Plate
--------
Shaper Item
--------
Item Level: 84
--------
+55 to maximum Life
""");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));
        var criterion = Assert.Single(draft.InfluenceCriteria);

        Assert.Equal(["Shaper Item"], draft.TraditionalInfluences);
        Assert.Equal(TradeSearchInfluenceKind.Shaper, criterion.Kind);
        Assert.Equal(TradeSearchInfluenceFamily.Traditional, criterion.Family);
        Assert.Equal("Shaper", criterion.DisplayLabel);
        Assert.True(criterion.IsEnabled);
        Assert.Equal(
            TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
            criterion.ProviderResolutionStatus);
        Assert.True(validator.Validate(draft).IsValid);
    }

    [Fact]
    public void CreateDraft_DoubleTraditionalInfluencesCreateIndependentDefaultOnCriteria()
    {
        var item = parser.Parse("""
Item Class: Body Armours
Rarity: Rare
Twin Shell
Astral Plate
--------
Shaper Item
Elder Item
--------
Item Level: 84
--------
+55 to maximum Life
""");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.Equal(["Shaper Item", "Elder Item"], draft.TraditionalInfluences);
        Assert.Equal(2, draft.InfluenceCriteria.Length);
        Assert.All(draft.InfluenceCriteria, criterion => Assert.True(criterion.IsEnabled));
        Assert.Equal(
            [TradeSearchInfluenceKind.Shaper, TradeSearchInfluenceKind.Elder],
            draft.InfluenceCriteria.Select(criterion => criterion.Kind).ToArray());
    }

    [Fact]
    public void CreateDraft_EldritchInfluencesAppearAsTopLevelCriteriaNotItemState()
    {
        var item = parser.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "Items", "eldritch-influenced-boots.txt")));

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.Empty(draft.TraditionalInfluences);
        Assert.Equal(["Searing Exarch Item", "Eater of Worlds Item"], draft.EldritchInfluences);
        Assert.Equal(2, draft.InfluenceCriteria.Length);
        Assert.All(
            draft.InfluenceCriteria,
            criterion =>
            {
                Assert.Equal(TradeSearchInfluenceFamily.Eldritch, criterion.Family);
                Assert.True(criterion.IsEnabled);
            });
        Assert.Equal(TradeTriState.No, draft.ItemStateCriteria.Mirrored);
        Assert.Equal(TradeTriState.No, draft.ItemStateCriteria.Corrupted);
    }

    [Fact]
    public void CreateDraft_NewUninfluencedItemDoesNotRetainPriorInfluenceCriteria()
    {
        var influenced = AssertSuccessfulDraft(mapper.CreateDraft(parser.Parse("""
Item Class: Rings
Rarity: Rare
Influenced Ring
Two-Stone Ring
--------
Hunter Item
--------
Item Level: 80
--------
+20 to maximum Life
""")));
        Assert.Single(influenced.InfluenceCriteria);

        var plain = AssertSuccessfulDraft(mapper.CreateDraft(parser.Parse("""
Item Class: Rings
Rarity: Rare
Plain Ring
Two-Stone Ring
--------
Item Level: 80
--------
+20 to maximum Life
""")));

        Assert.Empty(plain.TraditionalInfluences);
        Assert.Empty(plain.InfluenceCriteria);
    }

    [Fact]
    public void Validate_EnabledUnsupportedInfluenceBlocksSearch()
    {
        var draft = new TradeSearchDraft
        {
            ItemClass = "Rings",
            Rarity = "Rare",
            ParsedBaseType = "Two-Stone Ring",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseName = "Two-Stone Ring",
                Category = "Ring",
                ActiveCriterion = new BaseSearchCriterion
                {
                    Mode = BaseSearchMode.ExactBase,
                    Category = "Ring",
                    ExactBaseName = "Two-Stone Ring",
                },
            },
            InfluenceCriteria =
            [
                new TradeSearchInfluenceCriterion
                {
                    Kind = TradeSearchInfluenceKind.Shaper,
                    Family = TradeSearchInfluenceFamily.Traditional,
                    ObservedLine = "Shaper Item",
                    DisplayLabel = "Shaper",
                    IsEnabled = true,
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                    DiagnosticReason = "No official mapping.",
                },
            ],
        };

        var validation = validator.Validate(draft);

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Diagnostics,
            diagnostic => diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedInfluenceUnsupported);
    }

    [Fact]
    public void Validate_DisabledUnsupportedInfluenceDoesNotBlockSearch()
    {
        var draft = new TradeSearchDraft
        {
            ItemClass = "Rings",
            Rarity = "Rare",
            ParsedBaseType = "Two-Stone Ring",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseName = "Two-Stone Ring",
                Category = "Ring",
                ActiveCriterion = new BaseSearchCriterion
                {
                    Mode = BaseSearchMode.ExactBase,
                    Category = "Ring",
                    ExactBaseName = "Two-Stone Ring",
                },
            },
            InfluenceCriteria =
            [
                new TradeSearchInfluenceCriterion
                {
                    Kind = TradeSearchInfluenceKind.Shaper,
                    Family = TradeSearchInfluenceFamily.Traditional,
                    ObservedLine = "Shaper Item",
                    DisplayLabel = "Shaper",
                    IsEnabled = false,
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                    DiagnosticReason = "No official mapping.",
                },
            ],
        };

        Assert.True(validator.Validate(draft).IsValid);
    }

    private static TradeSearchDraft AssertSuccessfulDraft(TradeSearchDraftResult result)
    {
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.NotNull(result.Draft);
        return result.Draft!;
    }
}
