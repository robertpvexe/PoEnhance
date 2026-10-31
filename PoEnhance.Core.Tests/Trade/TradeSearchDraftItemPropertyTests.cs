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
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();

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
            [159.6m, 109.8m, 49.8m, 1.20m, 5.00m]
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
            [459.55m, 134.55m, 325m, 1.30m, 6.00m]
        },
        {
            3,
            [
                TradeSearchItemPropertyKind.TotalDps,
                TradeSearchItemPropertyKind.PhysicalDps,
                TradeSearchItemPropertyKind.AttacksPerSecond,
                TradeSearchItemPropertyKind.CriticalStrikeChance,
            ],
            [202.725m, 202.725m, 1.53m, 5.00m]
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
            [170.4m, 57.6m, 112.8m, 1.60m, 8.50m]
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
            Assert.Equal(
                TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimumIfDps(
                    property.Kind,
                    property.ObservedValue),
                property.RequestedMinimum);
            Assert.Null(property.RequestedMaximum);
            Assert.False(property.IsSearchable);
            Assert.Equal(
                TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
                property.ProviderResolutionStatus);
            Assert.NotEmpty(property.SourceProperties);
            Assert.All(property.SourceProperties, source =>
                Assert.Contains(item.Properties, candidate => ReferenceEquals(candidate, source)));
        });
        Assert.All(
            draft.ItemProperties.Where(property => property.Kind is
                TradeSearchItemPropertyKind.TotalDps or TradeSearchItemPropertyKind.PhysicalDps),
            property =>
            {
                Assert.Equal("Q20", property.CalculationBasisLabel);
                Assert.Equal(
                    TradeSearchItemPropertyQualityNormalizationStatus.NormalizedToTwenty,
                    property.QualityNormalizationStatus);
            });
        Assert.All(
            draft.ItemProperties.Where(property => property.Kind is not
                (TradeSearchItemPropertyKind.TotalDps or TradeSearchItemPropertyKind.PhysicalDps)),
            property =>
            {
                Assert.Null(property.CalculationBasisLabel);
                Assert.Equal(
                    TradeSearchItemPropertyQualityNormalizationStatus.NotApplicable,
                    property.QualityNormalizationStatus);
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

        Assert.Equal(170.4m, Property(draft, TradeSearchItemPropertyKind.TotalDps).ObservedValue);
        Assert.Contains(draft.ModifierFilters, modifier =>
            modifier.OriginalText.Contains("Lightning Damage to Spells", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Property(draft, TradeSearchItemPropertyKind.TotalDps).SourceProperties,
            source => spellModifier.ValueLines.Contains(source.OriginalText));
    }

    [Fact]
    public void CreateDraft_SyntheticChaosRare_TotalIncludesChaosAndExposesDisplayOnlyChaosDps()
    {
        // SYNTHETIC clipboard — Stage 03.8; no N/M/R chaos weapon in zfortests_v2.txt.
        var item = parser.Parse("""
Item Class: One Hand Swords
Rarity: Rare
Synthetic Chaos Edge
Corsair Sword
--------
One Handed Sword
Physical Damage: 20-40
Chaos Damage: 10-20 (augmented)
Critical Strike Chance: 5.00%
Attacks per Second: 1.50
--------
Item Level: 70
""");

        var draft = CreateDraft(item);

        Assert.Equal(67.5m, Property(draft, TradeSearchItemPropertyKind.TotalDps).ObservedValue);
        Assert.Equal(45m, Property(draft, TradeSearchItemPropertyKind.PhysicalDps).ObservedValue);
        Assert.Equal(22.5m, Property(draft, TradeSearchItemPropertyKind.ChaosDps).ObservedValue);
        Assert.DoesNotContain(
            draft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.ElementalDps);
        Assert.Equal(1.50m, Property(draft, TradeSearchItemPropertyKind.AttacksPerSecond).ObservedValue);
        Assert.Equal(5.00m, Property(draft, TradeSearchItemPropertyKind.CriticalStrikeChance).ObservedValue);
        Assert.Contains(
            Property(draft, TradeSearchItemPropertyKind.TotalDps).SourceProperties,
            source => source.NormalizedName == "chaos damage");
    }

    [Fact]
    public void CreateDraft_NecroticArmourCreatesDefensiveParentsButNoWeaponDiagnostics()
    {
        var item = ParseFixture(0);

        var draft = CreateDraft(item);

        Assert.Equal(
            [TradeSearchItemPropertyKind.EnergyShield, TradeSearchItemPropertyKind.EvasionRating],
            draft.ItemProperties.Select(property => property.Kind));
        Assert.All(draft.ItemProperties, property =>
        {
            Assert.NotNull(property.DerivationUnsupportedReason);
            Assert.Equal(
                TradeSearchItemPropertyQualityNormalizationStatus.Unavailable,
                property.QualityNormalizationStatus);
            Assert.Null(property.RequestedMinimum);
            Assert.Null(property.RequestedMaximum);
            Assert.True(property.ObservedValue > 0m);
        });
        Assert.Empty(draft.ItemPropertyDiagnostics);
        Assert.Equal(item.Modifiers.Count, draft.ModifierFilters.Count);
    }

    [Fact]
    public void CreateDraft_MalformedQualityKeepsObservedPhysTotalButClearsTradeDefaultMin()
    {
        var item = parser.Parse("""
Item Class: One Hand Axes
Rarity: Rare
Broken Edge
Reaver Axe
--------
Quality: unknown
Physical Damage: 38-114
Elemental Damage: 10-20 (augmented)
Attacks per Second: 1.20
Critical Strike Chance: 5.00%
--------
Item Level: 85
--------
{ Suffix Modifier "of Strength" (Tier: 8) - Attribute }
+10(8-12) to Strength
""");

        var draft = CreateDraft(item);

        var physical = Property(draft, TradeSearchItemPropertyKind.PhysicalDps);
        var total = Property(draft, TradeSearchItemPropertyKind.TotalDps);
        var elemental = Property(draft, TradeSearchItemPropertyKind.ElementalDps);
        var aps = Property(draft, TradeSearchItemPropertyKind.AttacksPerSecond);
        var crit = Property(draft, TradeSearchItemPropertyKind.CriticalStrikeChance);

        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.Unavailable,
            physical.QualityNormalizationStatus);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.Unavailable,
            total.QualityNormalizationStatus);
        Assert.True(physical.ObservedValue > 0m);
        Assert.True(total.ObservedValue > 0m);
        Assert.Null(physical.RequestedMinimum);
        Assert.Null(total.RequestedMinimum);
        Assert.Null(physical.CalculationBasisLabel);
        Assert.Null(total.CalculationBasisLabel);
        Assert.Contains("Quality", physical.NotSearchableReason, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.NotApplicable,
            elemental.QualityNormalizationStatus);
        Assert.Equal(
            TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimumIfDps(
                elemental.Kind,
                elemental.ObservedValue),
            elemental.RequestedMinimum);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.NotApplicable,
            aps.QualityNormalizationStatus);
        Assert.Equal(aps.ObservedValue, aps.RequestedMinimum);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.NotApplicable,
            crit.QualityNormalizationStatus);
        Assert.Equal(crit.ObservedValue, crit.RequestedMinimum);
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

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void CreateDraft_EquivalentSourceAlternativesContributeOneCanonicalMechanicalEffect(
        int alternativeCount)
    {
        var item = parser.Parse("""
Item Class: One Hand Axes
Rarity: Rare
Alternative Edge
Reaver Axe
--------
Physical Damage: 88-265 (augmented)
Attacks per Second: 1.20
--------
Item Level: 85
--------
{ Prefix Modifier "Reaver's" (Tier: 1) - Damage, Physical, Attack }
94% increased Physical Damage
""");
        var catalog = TradeSearchModifierSemanticProvenanceTests.ReviewedWeaponCatalog();
        var canonicalSource = Assert.Single(catalog.Modifiers, modifier => modifier.Id == "reavers");
        var alternatives = Enumerable.Range(0, alternativeCount)
            .Select(index => canonicalSource with { Id = $"equivalent-source-{index}" })
            .ToArray();
        var parsedModifier = Assert.Single(item.Modifiers);
        var resolution = new ModifierCandidateResolutionResult(
            0,
            parsedModifier,
            parsedModifier.Name,
            parsedModifier.Kind,
            ModifierGenerationType.Prefix,
            ModifierCandidateResolutionStatus.Exact,
            alternatives,
            [],
            Locality: ModifierLocality.Local);

        var draft = CreateDraft(item, [resolution], catalog);

        var component = Assert.Single(draft.ModifierFilters);
        Assert.True(component.IsEquivalentSourceSet);
        Assert.Equal(alternativeCount, component.Sources.Count);
        Assert.All(component.Sources, source =>
        {
            Assert.Equal(component.ResolvedStatIds, source.ResolvedStatIds);
            Assert.Equal([94m], source.CanonicalNumericValues);
            Assert.Equal(component.ReviewedItemPropertySemantic, source.ReviewedItemPropertySemantic);
        });
        Assert.Equal(211.8m, Property(draft, TradeSearchItemPropertyKind.PhysicalDps).ObservedValue);
    }

    [Fact]
    public void EquivalentSourceProjection_DisagreeingCanonicalMechanicsFailClosedForQ20()
    {
        var semantic = new ItemPropertySemanticDescriptor
        {
            Id = "test.local-physical-percent",
            OrderedStatIds = ["local_physical_damage_percent"],
            Contributions =
            [
                new ItemPropertyContribution
                {
                    Operation = ItemPropertyOperation.IncreasedPercent,
                    Targets = [ItemPropertyTarget.PhysicalDamage],
                },
            ],
            Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
        };
        SearchComponentSourceProvenance Source(string id, decimal value) => new()
        {
            ComponentId = "modifier:0:0",
            ResolvedModifierId = id,
            Locality = ModifierLocality.Local,
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            ResolvedStatIds = ["local_physical_damage_percent"],
            CanonicalNumericValues = [value],
            ReviewedItemPropertySemantic = semantic,
        };
        var component = new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            Locality = ModifierLocality.Local,
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            ResolvedStatIds = ["local_physical_damage_percent"],
            CanonicalNumericValues = [94m],
            ReviewedItemPropertySemantic = semantic,
            IsEquivalentSourceSet = true,
            Sources = [Source("alternative-a", 94m), Source("alternative-b", 95m)],
        };

        var effect = Assert.Single(DerivedWeaponModifierEffectProjector.Project([component]));
        var result = new DerivedWeaponPropertyCalculator().CalculateQ20(
            parser.Parse("""
Item Class: One Hand Axes
Rarity: Rare
Conflict Edge
Reaver Axe
--------
Physical Damage: 81-244 (augmented)
Attacks per Second: 1.20
"""),
            new ItemBaseRecord
            {
                Id = "test-base",
                WeaponProperties = new ItemBaseWeaponProperties
                {
                    PhysicalDamageMinimum = 38,
                    PhysicalDamageMaximum = 114,
                    Sources = [new GameDataSourceReference { SourceId = "test" }],
                },
            },
            [effect]);

        Assert.NotNull(effect.CanonicalizationUnsupportedReason);
        Assert.Equal(DerivedWeaponQ20Status.Unsupported, result.Q20Status);
        Assert.NotNull(result.PhysicalDps);
        Assert.Equal(
            effect.CanonicalizationUnsupportedReason,
            Assert.IsType<DerivedWeaponQ20Provenance>(result.Q20Provenance).UnsupportedReason);
    }

    [Fact]
    public void CreateDraft_DpsAutoMinimumFloorsToTwoDecimalsWithoutChangingObserved()
    {
        var draft = CreateDraft(ParseFixture(3));
        var total = Property(draft, TradeSearchItemPropertyKind.TotalDps);
        var physical = Property(draft, TradeSearchItemPropertyKind.PhysicalDps);

        Assert.Equal(202.725m, total.ObservedValue);
        Assert.Equal(202.725m, physical.ObservedValue);
        Assert.Equal(202.72m, total.RequestedMinimum);
        Assert.Equal(202.72m, physical.RequestedMinimum);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.NormalizedToTwenty,
            total.QualityNormalizationStatus);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.NormalizedToTwenty,
            physical.QualityNormalizationStatus);
    }

    [Fact]
    public void CreateDraft_NyctaStyleDpsValuesFloorAutoMinimumExactly()
    {
        // Synthetic values matching the manual Nycta Q20 display row precision targets.
        var total = TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimum(335.625m);
        var physical = TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimum(168.125m);
        var elemental = TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimum(167.5m);
        Assert.Equal(335.62m, total);
        Assert.Equal(168.12m, physical);
        Assert.Equal(167.5m, elemental);
    }

    [Fact]
    public void UserEditedDpsMinimumIsNotRewrittenByAutoDefaultPolicy()
    {
        var draft = CreateDraft(ParseFixture(3));
        var physical = Property(draft, TradeSearchItemPropertyKind.PhysicalDps);
        Assert.Equal(202.72m, physical.RequestedMinimum);

        var edited = physical with { RequestedMinimum = 202.725m };
        Assert.Equal(202.725m, edited.RequestedMinimum);
        Assert.Equal(202.725m, edited.ObservedValue);
        Assert.NotEqual(edited.RequestedMinimum, physical.RequestedMinimum);
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

            Assert.Equal(459.55m, Property(draft, TradeSearchItemPropertyKind.TotalDps).ObservedValue);
            Assert.Equal(134.55m, Property(draft, TradeSearchItemPropertyKind.PhysicalDps).ObservedValue);
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
        var effectiveCatalog = catalog ?? TradeSearchModifierSemanticProvenanceTests.ReviewedWeaponCatalog();
        var effectiveModifierResolutions = modifierResolutions ?? modifierResolver.Resolve(item, effectiveCatalog);
        var result = mapper.CreateDraft(
            item,
            itemBaseResolution: baseResolver.Resolve(item, effectiveCatalog),
            modifierResolutions: effectiveModifierResolutions,
            gameDataCatalog: effectiveCatalog);
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
