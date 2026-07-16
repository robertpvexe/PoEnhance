using System.Globalization;
using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Tests.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchDraftItemPropertyTests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();

    public static TheoryData<int, TradeSearchItemPropertyKind[], decimal[]> WeaponFixtures => new()
    {
        {
            1,
            [
                TradeSearchItemPropertyKind.TotalDps,
                TradeSearchItemPropertyKind.PhysicalDps,
                TradeSearchItemPropertyKind.ElementalDps,
                TradeSearchItemPropertyKind.AttacksPerSecond,
                TradeSearchItemPropertyKind.CriticalStrikeChance,
            ],
            [141.0m, 91.2m, 49.8m, 1.20m, 5.00m]
        },
        {
            2,
            [
                TradeSearchItemPropertyKind.TotalDps,
                TradeSearchItemPropertyKind.PhysicalDps,
                TradeSearchItemPropertyKind.ElementalDps,
                TradeSearchItemPropertyKind.AttacksPerSecond,
                TradeSearchItemPropertyKind.CriticalStrikeChance,
            ],
            [437.45m, 112.45m, 325m, 1.30m, 6.00m]
        },
        {
            3,
            [
                TradeSearchItemPropertyKind.TotalDps,
                TradeSearchItemPropertyKind.PhysicalDps,
                TradeSearchItemPropertyKind.AttacksPerSecond,
                TradeSearchItemPropertyKind.CriticalStrikeChance,
            ],
            [169.065m, 169.065m, 1.53m, 5.00m]
        },
        {
            4,
            [
                TradeSearchItemPropertyKind.TotalDps,
                TradeSearchItemPropertyKind.PhysicalDps,
                TradeSearchItemPropertyKind.ElementalDps,
                TradeSearchItemPropertyKind.AttacksPerSecond,
                TradeSearchItemPropertyKind.CriticalStrikeChance,
            ],
            [160.8m, 48m, 112.8m, 1.60m, 8.50m]
        },
    };

    [Theory]
    [MemberData(nameof(WeaponFixtures))]
    public void CreateDraft_RealWeaponFixturesPopulateOrderedExactItemProperties(
        int fixtureIndex,
        TradeSearchItemPropertyKind[] expectedKinds,
        decimal[] expectedValues)
    {
        var item = ParseFixture(fixtureIndex);

        var draft = CreateDraft(item);

        Assert.Equal(expectedKinds, draft.ItemProperties.Select(property => property.Kind));
        Assert.Equal(expectedValues, draft.ItemProperties.Select(property => property.ObservedValue));
        Assert.Equal(ExpectedLabels(expectedKinds), draft.ItemProperties.Select(property => property.Label));
        Assert.All(draft.ItemProperties, property =>
        {
            Assert.False(property.IsSelected);
            Assert.Equal(property.ObservedValue, property.RequestedMinimum);
            Assert.Null(property.RequestedMaximum);
            Assert.False(property.IsSearchable);
            Assert.Equal(
                TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
                property.ProviderResolutionStatus);
            Assert.NotEmpty(property.SourceProperties);
            Assert.All(property.SourceProperties, source =>
                Assert.Contains(item.Properties, candidate => ReferenceEquals(candidate, source)));
        });
        Assert.Empty(draft.ItemPropertyDiagnostics);
        Assert.DoesNotContain(draft.ItemProperties, property => property.Kind == TradeSearchItemPropertyKind.ChaosDps);
        Assert.False(typeof(ResolvedSearchComponent).IsAssignableFrom(typeof(TradeSearchItemProperty)));
    }

    [Fact]
    public void CreateDraft_WrathCrySpellDamageRemainsOnlyAModifier()
    {
        var item = ParseFixture(4);
        var spellModifier = Assert.Single(item.Modifiers, modifier =>
            modifier.ValueLines.Any(line => line.Contains("Lightning Damage to Spells", StringComparison.Ordinal)));

        var draft = CreateDraft(item);

        Assert.Equal(160.8m, Property(draft, TradeSearchItemPropertyKind.TotalDps).ObservedValue);
        Assert.Contains(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Lightning Damage to Spells", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Property(draft, TradeSearchItemPropertyKind.TotalDps).SourceProperties,
            source => spellModifier.ValueLines.Contains(source.OriginalText));
    }

    [Fact]
    public void CreateDraft_NecroticArmourHasNoWeaponPropertiesOrDiagnostics()
    {
        var item = ParseFixture(0);

        var draft = CreateDraft(item);

        Assert.Empty(draft.ItemProperties);
        Assert.Empty(draft.ItemPropertyDiagnostics);
        Assert.Equal(item.Modifiers.Count, draft.ModifierFilters.Count);
    }

    [Theory]
    [InlineData("Physical Damage: 10-20", DerivedWeaponPropertyDiagnosticCodes.MissingAttacksPerSecond)]
    [InlineData("Physical Damage: 10 to 20\nAttacks per Second: 2.00", DerivedWeaponPropertyDiagnosticCodes.UnsupportedDamage)]
    public void CreateDraft_InvalidOrUnsupportedWeaponHasNoPartialPropertiesAndRetainsDiagnostics(
        string propertyLines,
        string expectedDiagnosticCode)
    {
        var item = parser.Parse($$"""
Item Class: One Hand Axes
Rarity: Rare
Broken Edge
Reaver Axe
--------
{{propertyLines}}
--------
Item Level: 85
--------
{ Suffix Modifier "of Strength" (Tier: 8) - Attribute }
+10(8-12) to Strength
""");

        var draft = CreateDraft(item);

        Assert.Empty(draft.ItemProperties);
        var diagnostic = Assert.Single(draft.ItemPropertyDiagnostics);
        Assert.Equal(expectedDiagnosticCode, diagnostic.Code);
        Assert.Single(draft.ModifierFilters);
        Assert.Equal("+10(8-12) to Strength", draft.ModifierFilters[0].OriginalText);
        if (diagnostic.SourceProperty is not null)
        {
            Assert.Contains(item.Properties, property => ReferenceEquals(property, diagnostic.SourceProperty));
        }
    }

    [Fact]
    public void CreateDraft_ItemPropertiesDoNotChangeModifierAggregationOrContributorProvenance()
    {
        var item = parser.Parse("""
Item Class: One Hand Axes
Rarity: Rare
Layered Edge
Reaver Axe
--------
Physical Damage: 10-20
Attacks per Second: 2.00
--------
Item Level: 85
--------
{ Prefix Modifier "Pure" (Tier: 7) - Damage, Physical, Attack }
30(25-35)% increased Physical Damage
{ Master Crafted Prefix Modifier "Craft" (Rank: 3) - Damage, Physical, Attack }
20(18-22)% increased Physical Damage
""");
        var withoutProperties = item with { Properties = [] };
        var modifier = new ModifierDefinition
        {
            Id = "modifier.physical",
            Name = "Physical Damage",
            GenerationType = ModifierGenerationType.Prefix,
            Stats = [new ModifierStat { Index = 0, StatId = "local_physical_damage_percent" }],
        };
        var resolutions = item.Modifiers
            .Select((parsedModifier, index) => new ModifierCandidateResolutionResult(
                index,
                parsedModifier,
                parsedModifier.Name,
                parsedModifier.Kind,
                ModifierGenerationType.Prefix,
                ModifierCandidateResolutionStatus.Exact,
                [modifier],
                [],
                Locality: ModifierLocality.Local))
            .ToArray();
        var catalog = PhysicalModifierCatalog();

        var weaponDraft = CreateDraft(item, resolutions, catalog);
        var baselineDraft = CreateDraft(withoutProperties, resolutions, catalog);

        Assert.Equivalent(baselineDraft.ModifierFilters, weaponDraft.ModifierFilters, strict: true);
        Assert.Equal([7, null], item.Modifiers.Select(parsedModifier => parsedModifier.Tier));
        Assert.Equal([null, 3], item.Modifiers.Select(parsedModifier => parsedModifier.Rank));
        Assert.Same(item.Modifiers, withoutProperties.Modifiers);
        var aggregate = Assert.Single(weaponDraft.ModifierFilters);
        Assert.False(aggregate.IsSelected);
        Assert.Equal(50m, aggregate.RequestedMinimum);
        Assert.Null(aggregate.RequestedMaximum);
        Assert.Equal(2, aggregate.Sources.Count);
        Assert.Equal(2, aggregate.Contributors.Count);
        Assert.Equal([0, 1], aggregate.Sources.Select(source => source.SourceModifierIndex));
        Assert.Equal([false, true], aggregate.Sources.Select(source => source.IsCrafted));
        Assert.Equal([30m, 20m], aggregate.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.All(aggregate.Contributors, contributor => Assert.False(contributor.IsSelected));
    }

    [Fact]
    public void TradeSearchDraft_RecordCopyUsesAnImmutableItemPropertyCollection()
    {
        var original = CreateDraft(ParseFixture(1));
        var selected = original.ItemProperties.SetItem(
            0,
            original.ItemProperties[0] with { IsSelected = true });

        var copy = original with { ItemProperties = selected };

        Assert.False(original.ItemProperties[0].IsSelected);
        Assert.True(copy.ItemProperties[0].IsSelected);
        Assert.NotEqual(original.ItemProperties, copy.ItemProperties);
    }

    [Fact]
    public void CreateDraft_PolishCultureDoesNotChangeExactDecimalValues()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

            var draft = CreateDraft(ParseFixture(2));

            Assert.Equal(437.45m, Property(draft, TradeSearchItemPropertyKind.TotalDps).ObservedValue);
            Assert.Equal(112.45m, Property(draft, TradeSearchItemPropertyKind.PhysicalDps).ObservedValue);
            Assert.Equal(325m, Property(draft, TradeSearchItemPropertyKind.ElementalDps).ObservedValue);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private ParsedItem ParseFixture(int index)
    {
        return parser.Parse(CopiedItemCorpus.LoadItems()[index]);
    }

    private TradeSearchDraft CreateDraft(
        ParsedItem item,
        IReadOnlyList<ModifierCandidateResolutionResult>? modifierResolutions = null,
        GameDataCatalog? catalog = null)
    {
        var result = mapper.CreateDraft(
            item,
            modifierResolutions: modifierResolutions,
            gameDataCatalog: catalog);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static TradeSearchItemProperty Property(
        TradeSearchDraft draft,
        TradeSearchItemPropertyKind kind)
    {
        return Assert.Single(draft.ItemProperties, property => property.Kind == kind);
    }

    private static IEnumerable<string> ExpectedLabels(
        IEnumerable<TradeSearchItemPropertyKind> kinds)
    {
        return kinds.Select(kind => kind switch
        {
            TradeSearchItemPropertyKind.TotalDps => "Total DPS",
            TradeSearchItemPropertyKind.PhysicalDps => "Physical DPS",
            TradeSearchItemPropertyKind.ElementalDps => "Elemental DPS",
            TradeSearchItemPropertyKind.ChaosDps => "Chaos DPS",
            TradeSearchItemPropertyKind.AttacksPerSecond => "Attacks per Second",
            TradeSearchItemPropertyKind.CriticalStrikeChance => "Critical Strike Chance",
            _ => throw new ArgumentOutOfRangeException(nameof(kinds)),
        });
    }

    private static GameDataCatalog PhysicalModifierCatalog()
    {
        const string statId = "local_physical_damage_percent";
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "test",
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                League = "test",
                Patch = "test",
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "test",
                        RetrievedAtUtc = DateTimeOffset.UnixEpoch,
                        SourceVersion = "test",
                        SourceUri = "https://example.test",
                    },
                ],
            },
            Stats = [new StatDefinition { Id = statId }],
            StatTranslations =
            [
                new StatTranslationDefinition
                {
                    Id = "physical-percent",
                    StatIds = [statId],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["#"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0, Handlers = [] }],
                            FormatLines = ["{0}% increased Physical Damage"],
                        },
                    ],
                },
            ],
        });
    }
}
