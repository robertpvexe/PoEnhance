using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ParsedUniqueItemResolverTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedUniqueItemResolver resolver = new();

    [Fact]
    public void Resolve_HistoricalMultiLineBlock_RetainsOneSourceBlockAndMarksLegacy()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Foulborn Test Calling
            Calling Wand
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +1 to maximum number of Raised Zombies
            +1 to maximum number of Spectres
            """);
        var catalog = CreateCatalog("Test Calling", "Calling Wand", UniqueItemKind.Ordinary,
            Version("Pre 3.29.0", UniqueItemVersionRole.Historical,
                MultiBlock()));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, result.Status);
        Assert.True(result.IsFoulborn);
        Assert.True(result.IsLegacy);
        var sourceBlock = Assert.Single(result.ModifierBlocks);
        Assert.True(sourceBlock.IsResolved);
        Assert.Single(sourceBlock.CatalogBlocks);
        Assert.Equal(["spectre_stat", "zombie_stat"], sourceBlock.StatIds.Order().ToArray());
        Assert.Equal(2, sourceBlock.SourceObservationIds.Count);
        Assert.True(sourceBlock.IsEquivalentSourceSet);
    }

    [Fact]
    public void Resolve_AuthenticFoulbornRaw_ResolvesOrdinaryBlockAndFailsReplacementClosed()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Foulborn Midnight Bargain
            Calling Wand
            --------
            Item Level: 83
            --------
            { Unique Modifier — Minion }
            +1 to maximum number of Raised Zombies
            +1 to maximum number of Spectres
            +1 to maximum number of Skeletons
            { Foulborn Unique Modifier — Life, Defences, Energy Shield, Minion }
            Lose 0.5% Life and Energy Shield per Second per Minion
            """);
        var catalog = CreateCatalog("Midnight Bargain", "Calling Wand", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current, FoulbornOrdinaryBlock()));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, result.Status);
        Assert.True(result.IsFoulborn);
        Assert.Equal("Midnight Bargain", result.Identity?.CanonicalName);
        Assert.Collection(
            result.ModifierBlocks,
            ordinary =>
            {
                Assert.True(ordinary.IsResolved);
                Assert.Single(ordinary.CatalogBlocks);
                Assert.Equal(3, ordinary.StatIds.Count);
            },
            replacement =>
            {
                Assert.False(replacement.IsResolved);
                Assert.Empty(replacement.CatalogBlocks);
                Assert.Equal(
                    "FOULBORN_REPLACEMENT_RELATIONSHIP_NOT_FOUND",
                    replacement.DiagnosticCode);
            });

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        Assert.Equal(TradeTriState.Yes, draft.ItemVariantCriteria.Foulborn);
        Assert.Collection(
            draft.ModifierFilters,
            ordinary =>
            {
                Assert.True(ordinary.IsSearchable);
                Assert.Contains(Environment.NewLine, ordinary.OriginalText, StringComparison.Ordinal);
            },
            replacement =>
            {
                Assert.False(replacement.IsSearchable);
                Assert.False(replacement.IsSelected);
                Assert.Equal(
                    "FOULBORN_REPLACEMENT_RELATIONSHIP_NOT_FOUND",
                    replacement.UniqueResolutionDiagnosticCode);
            });
    }

    [Fact]
    public void CreateDraft_ExactFoulbornRelationship_UsesReplacementMechanicsAndCopiedValue()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Foulborn Test Calling
            Calling Wand
            --------
            Item Level: 83
            --------
            { Foulborn Unique Modifier }
            +25 to maximum Life
            """);
        var normalBlock = Block("normal-life", "+<number> to maximum Mana", "normal_mana");
        var replacement = ReplacementModifier();
        var catalog = CreateCatalog(
            "Test Calling",
            "Calling Wand",
            UniqueItemKind.Ordinary,
            [Version("Current", UniqueItemVersionRole.Current, normalBlock)],
            [replacement],
            [Translation("foulborn-life", "foulborn_life", "{0} to maximum Life")],
            [Relationship("modifier:normal-life", replacement.Id!, normalBlock.Id!)]);

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, result.Status);
        Assert.True(result.IsFoulborn);
        var resolved = Assert.Single(result.ModifierBlocks);
        Assert.True(resolved.IsResolved, $"{resolved.DiagnosticCode}: {resolved.Diagnostic}");
        Assert.Equal(["modifier:foulborn-life"], resolved.ModifierIds);
        Assert.Equal(["modifier:normal-life"], resolved.NormalCounterpartModifierIds);
        Assert.Equal(["foulborn_life"], resolved.StatIds);
        Assert.Equal(["foulborn-relationship:test"], resolved.FoulbornRelationshipIds);
        Assert.Empty(resolved.CatalogBlocks);

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var row = Assert.Single(draft.ModifierFilters);
        Assert.Equal(ParsedUniqueModifierOrigin.Foulborn, row.UniqueOrigin);
        Assert.True(row.IsSearchable);
        Assert.Equal("modifier:foulborn-life", row.ResolvedModifierId);
        Assert.Equal(["foulborn-relationship:test"], row.UniqueFoulbornRelationshipIds);
        Assert.Equal(["modifier:normal-life"], row.UniqueNormalCounterpartModifierIds);
        Assert.Equal(25m, row.RequestedMinimum);
    }

    [Fact]
    public void Resolve_NormalUnique_DoesNotSubstituteKnownFoulbornReplacement()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Test Calling
            Calling Wand
            --------
            Item Level: 83
            --------
            { Unique Modifier }
            +25 to maximum Life
            """);
        var normalBlock = Block("normal-life", "+<number> to maximum Mana", "normal_mana");
        var replacement = ReplacementModifier();
        var catalog = CreateCatalog(
            "Test Calling",
            "Calling Wand",
            UniqueItemKind.Ordinary,
            [Version("Current", UniqueItemVersionRole.Current, normalBlock)],
            [replacement],
            [Translation("foulborn-life", "foulborn_life", "{0} to maximum Life")],
            [Relationship("modifier:normal-life", replacement.Id!, normalBlock.Id!)]);

        var result = resolver.Resolve(parsed, catalog);

        Assert.False(result.IsFoulborn);
        var unresolved = Assert.Single(result.ModifierBlocks);
        Assert.False(unresolved.IsResolved);
        Assert.Empty(unresolved.FoulbornRelationshipIds);
        Assert.DoesNotContain(replacement.Id!, unresolved.ModifierIds);
    }

    [Fact]
    public void Resolve_HistoricalOrdinaryEvidence_DoesNotUseCurrentFoulbornRelationship()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Foulborn Test Calling
            Calling Wand
            --------
            Item Level: 83
            --------
            { Unique Modifier }
            +1 to maximum Mana
            { Foulborn Unique Modifier }
            +25 to maximum Life
            """);
        var historicalBlock = Block("normal-life", "+<number> to maximum Mana", "normal_mana");
        var currentBlock = Block("current", "Cannot be Stunned", "current_stat");
        var replacement = ReplacementModifier();
        var catalog = CreateCatalog(
            "Test Calling",
            "Calling Wand",
            UniqueItemKind.Ordinary,
            [
                Version("Current", UniqueItemVersionRole.Current, currentBlock),
                Version("Historical", UniqueItemVersionRole.Historical, historicalBlock),
            ],
            [replacement],
            [Translation("foulborn-life", "foulborn_life", "{0} to maximum Life")],
            [Relationship("modifier:normal-life", replacement.Id!, historicalBlock.Id!)]);

        var result = resolver.Resolve(parsed, catalog);

        Assert.All(result.CompatibleVersions, version =>
            Assert.Equal(UniqueItemVersionRole.Historical, version.Role));
        var replacementResolution = Assert.Single(result.ModifierBlocks, block =>
            block.ParsedModifierIndex == 1);
        Assert.False(replacementResolution.IsResolved);
        Assert.Equal("FOULBORN_REPLACEMENT_VERSION_MISMATCH", replacementResolution.DiagnosticCode);
    }

    [Fact]
    public void Resolve_ReplicaDoesNotFallBackToOrdinaryIdentity()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Replica Test Flight
            Onyx Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Cannot be Stunned
            """);
        var catalog = CreateCatalog("Test Flight", "Onyx Amulet", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                Block("stun", "Cannot be Stunned", "stun")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.Unsupported, result.Status);
        Assert.Equal("UNIQUE_IDENTITY_NOT_FOUND", result.DiagnosticCode);
    }

    [Fact]
    public void CreateDraft_FixedNumericUniqueSource_RetainsNonEditableQueryConstraintAndProviderSignatures()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Test Echo Wand
            Carved Wand
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Socketed Gems are Supported by Level 10 Spell Echo — Unscalable Value
            """);
        var catalog = CreateCatalog(
            "Test Echo Wand",
            "Carved Wand",
            UniqueItemKind.Ordinary,
            [
                Version("Current", UniqueItemVersionRole.Current,
                    EvidenceBlock(
                        "spell-echo",
                        "Socketed Gems are Supported by Level 10 Spell Echo",
                        "Socketed Gems are Supported by Level <number> Spell Echo",
                        "support_spell_echo")),
            ],
            additionalModifiers: [],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "translation:spell-echo",
                    StatIds = ["support_spell_echo"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["#"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                            FormatLines =
                            [
                                "Socketed Gems are Supported by Level {0} Spell Echo",
                            ],
                        },
                    ],
                },
            ],
            foulbornRelationships: []);

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var row = Assert.Single(draft.ModifierFilters);
        Assert.Contains(
            "Socketed Gems are Supported by Level <number> Spell Echo",
            row.ProviderSearchSignatures);
        Assert.Contains(
            "Socketed Gems are Supported by Level 10 Spell Echo",
            row.ProviderSearchSignatures);
        Assert.True(row.IsSearchable);
        Assert.False(row.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.Scalar, row.ValueBoundShape);
        Assert.Null(row.RequestedMinimum);
        Assert.Null(row.RequestedMaximum);
        Assert.Equal([10m], row.ObservedNumericValues);
        Assert.Equal([10m], row.CanonicalNumericValues);
        Assert.Equal(10m, row.FixedQueryValue);
    }

    [Fact]
    public void CreateDraft_ProvenMultilineUniqueBlock_ExpandsIntoIndependentComponents()
    {
        var presenceLine = "You do not inherently take less Damage for having Fortification";
        var suppressLine = "+4% chance to Suppress Spell Damage per Fortification";
        var parsed = parser.Parse($$"""
            Item Class: Amulets
            Rarity: Unique
            Test Fortify Amulet
            Jade Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{presenceLine}}
            {{suppressLine}}
            """);
        var catalog = CreateCatalog(
            "Test Fortify Amulet",
            "Jade Amulet",
            UniqueItemKind.Ordinary,
            [
                Version("Current", UniqueItemVersionRole.Current,
                    new UniqueModifierBlock
                    {
                        Id = "block:fortification",
                        Kind = UniqueModifierBlockKind.Unique,
                        Lines = [presenceLine, suppressLine],
                        CanonicalSignatures =
                        [
                            presenceLine,
                            "+<number>% chance to Suppress Spell Damage per Fortification",
                        ],
                        MechanicalMapping = new UniqueModifierMechanicalMapping
                        {
                            Status = UniqueModifierMechanicalMappingStatus.Exact,
                            ModifierIds = ["modifier:fortification"],
                            StatIds =
                            [
                                "should_use_alternate_fortify",
                                "spell_suppression_chance_%_per_fortification",
                            ],
                        },
                        SourceObservationIds = ["pob-observation:test"],
                    }),
            ],
            additionalModifiers: [],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "translation:fortify-presence",
                    StatIds = ["should_use_alternate_fortify"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["ignore"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                            FormatLines = [presenceLine],
                        },
                    ],
                },
                new StatTranslationDefinition
                {
                    Id = "translation:fortify-suppress",
                    StatIds = ["spell_suppression_chance_%_per_fortification"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["+#"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                            FormatLines =
                            [
                                "{0}% chance to Suppress Spell Damage per Fortification",
                            ],
                        },
                    ],
                },
            ],
            foulbornRelationships: []);

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);

        Assert.Equal(2, draft.ModifierFilters.Count);
        Assert.All(draft.ModifierFilters, row =>
        {
            Assert.Equal(0, row.SourceModifierIndex);
            Assert.Equal("block:fortification", Assert.Single(row.UniqueCatalogBlockIds));
            Assert.True(row.IsSearchable);
            Assert.Empty(row.Contributors);
            Assert.Equal(SearchComponentContributorProjection.None, row.ContributorProjection);
        });

        var presence = Assert.Single(draft.ModifierFilters, row => row.SourceLineIndex == 0);
        Assert.Equal(presenceLine, presence.OriginalText);
        Assert.Equal(["should_use_alternate_fortify"], presence.ResolvedStatIds);
        Assert.Equal(ModifierBoundShape.PresenceOnly, presence.ValueBoundShape);
        Assert.False(presence.SupportsValueBounds);

        var suppress = Assert.Single(draft.ModifierFilters, row => row.SourceLineIndex == 1);
        Assert.Equal(suppressLine, suppress.OriginalText);
        Assert.Equal(["spell_suppression_chance_%_per_fortification"], suppress.ResolvedStatIds);
        Assert.Equal(ModifierBoundShape.Scalar, suppress.ValueBoundShape);
        Assert.Equal(4m, suppress.RequestedMinimum);
        Assert.Contains(suppressLine, suppress.ProviderSearchSignatures);
    }

    [Fact]
    public void CreateDraft_MultiLineUniqueSourceBlock_RemainsOneProvenanceBackedRow()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Foulborn Test Calling
            Calling Wand
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +1 to maximum number of Raised Zombies
            +1 to maximum number of Spectres
            """);
        var catalog = CreateCatalog("Test Calling", "Calling Wand", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                MultiBlock()));

        var result = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog);

        var draft = Assert.IsType<TradeSearchDraft>(result.Draft);
        Assert.Equal(TradeTriState.Yes, draft.ItemVariantCriteria.Foulborn);
        Assert.Equal("Test Calling", draft.UniqueItemResolution?.Identity?.CanonicalName);
        var row = Assert.Single(draft.ModifierFilters);
        Assert.Contains(Environment.NewLine, row.OriginalText, StringComparison.Ordinal);
        Assert.Single(row.UniqueCatalogBlockIds);
        Assert.Equal(2, row.UniqueSourceObservationIds.Count);
        Assert.True(row.IsEquivalentSourceSet);
        Assert.True(row.IsSearchable);
        Assert.False(row.SupportsValueBounds);
    }

    [Fact]
    public void CreateDraft_ResolvedUniqueBlock_PreservesStatVectorPerStatLocalityAndSourceProvenance()
    {
        var parsed = parser.Parse("""
            Item Class: Shields
            Rarity: Unique
            Test Shield
            Test Round Shield
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +100 to Armour
            """);
        var catalog = CreateCatalog("Test Shield", "Test Round Shield", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                Block("local-armour", "+<number> to Armour", "local_armour_stat")));

        var result = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog);

        var row = Assert.Single(Assert.IsType<TradeSearchDraft>(result.Draft).ModifierFilters);
        Assert.Equal(["local_armour_stat"], row.ResolvedStatIds);
        Assert.Equal([ModifierLocality.Local], row.ResolvedStatLocalities);
        Assert.Equal(ModifierLocality.Local, row.Locality);
        Assert.Contains("+<number> to Armour", row.ProviderSearchSignatures);
        Assert.Single(row.UniqueCatalogBlockIds);
        Assert.Single(row.UniqueSourceObservationIds);
        var source = Assert.Single(row.Sources);
        Assert.Equal(["local_armour_stat"], source.ResolvedStatIds);
        Assert.Equal([ModifierLocality.Local], source.ResolvedStatLocalities);
    }

    [Fact]
    public void Resolve_CompatibleVersionsWithConflictingMechanics_FailsBlockClosed()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Test Calling
            Calling Wand
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Cannot be Stunned
            """);
        var catalog = CreateCatalog("Test Calling", "Calling Wand", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                Block("current-stun", "Cannot be Stunned", "current_stun_stat")),
            Version("Pre 3.29.0", UniqueItemVersionRole.Historical,
                Block("historical-stun", "Cannot be Stunned", "historical_stun_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, result.Status);
        Assert.Equal(2, result.CompatibleVersions.Count);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Empty(block.StatIds);
        Assert.Equal("UNIQUE_BLOCK_INDEPENDENT_DIMENSIONS", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_EvaluatedFixedAndRangeAnnotations_SelectsOneCoherentVersion()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Test Flight
            Onyx Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            20(25)% increased Movement Speed
            { Unique Modifier }
            +19(13-19)% to Chaos Resistance
            """);
        var catalog = CreateCatalog("Test Flight", "Onyx Amulet", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "current-speed",
                    "25% increased Movement Speed",
                    "<number>% increased Movement Speed",
                    "speed_stat"),
                EvidenceBlock(
                    "current-chaos",
                    "+(13-19)% to Chaos Resistance",
                    "+(<number>-<number>)% to Chaos Resistance",
                    "chaos_stat")),
            Version("Historical", UniqueItemVersionRole.Historical,
                EvidenceBlock(
                    "historical-speed",
                    "20% increased Movement Speed",
                    "<number>% increased Movement Speed",
                    "historical_speed_stat"),
                EvidenceBlock(
                    "historical-chaos",
                    "+(9-12)% to Chaos Resistance",
                    "+(<number>-<number>)% to Chaos Resistance",
                    "historical_chaos_stat")));

        var result = resolver.Resolve(parsed, catalog);

        var version = Assert.Single(result.CompatibleVersions);
        Assert.Equal("Current", version.Label);
        Assert.Equal(2, result.ModifierBlocks.Count);
        Assert.All(result.ModifierBlocks, block => Assert.True(block.IsResolved));
        Assert.Equal(
            ["chaos_stat", "speed_stat"],
            result.ModifierBlocks.SelectMany(block => block.StatIds).Order().ToArray());
    }

    [Fact]
    public void Resolve_NegativeEvaluatedResistanceRange_MatchesSignedCatalogRangeBlock()
    {
        var parsed = parser.Parse("""
            Item Class: Body Armours
            Rarity: Unique
            Test Negative Res Body
            Test Garb
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            -29(-30--20)% to Fire Resistance
            """);
        var catalog = CreateCatalog(
            "Test Negative Res Body",
            "Test Garb",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "negative-fire-res",
                    "-(30-20)% to Fire Resistance",
                    "-<number>% to Fire Resistance",
                    "base_fire_damage_resistance_%")));

        var result = resolver.Resolve(parsed, catalog);

        var version = Assert.Single(result.CompatibleVersions);
        Assert.Equal("Current", version.Label);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.NotEqual("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
        Assert.Single(block.CatalogBlocks);
        Assert.Contains("negative-fire-res", block.CatalogBlocks[0].Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_SignedCanonicalRange_AllowsOnlyTheBackedPolarityInversion()
    {
        var parsed = parser.Parse("""
            Item Class: Belts
            Rarity: Unique
            Test Distillation
            Heavy Belt
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            34(-35-35)% increased Duration of Elemental Ailments on you
            """);
        var catalog = CreateCatalog("Test Distillation", "Heavy Belt", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "ailment-duration",
                    "(-35-35)% reduced Duration of Elemental Ailments on you",
                    "<number>% reduced Duration of Elemental Ailments on you",
                    "ailment_duration_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Single(result.CompatibleVersions);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.True(block.IsResolved);
        Assert.Equal(["ailment_duration_stat"], block.StatIds);
    }

    [Fact]
    public void Resolve_UnsignedPolarityDifference_DoesNotBecomeACompatibleVersion()
    {
        var parsed = parser.Parse("""
            Item Class: Belts
            Rarity: Unique
            Test Distillation
            Heavy Belt
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            34% increased Duration of Elemental Ailments on you
            """);
        var catalog = CreateCatalog("Test Distillation", "Heavy Belt", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "ailment-duration",
                    "35% reduced Duration of Elemental Ailments on you",
                    "<number>% reduced Duration of Elemental Ailments on you",
                    "ailment_duration_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Empty(result.CompatibleVersions);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_AnnotatedBoundsDifferButObservedRollFitsExactSemanticSource_Resolves()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Test Bounded Crit
            Amber Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            48(30-50)% increased Global Critical Strike Chance
            """);
        var catalog = CreateCatalog("Test Bounded Crit", "Amber Amulet", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "bounded-crit",
                    "(40-50)% increased Global Critical Strike Chance",
                    "<number>% increased Global Critical Strike Chance",
                    "critical_strike_chance_+%")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Empty(result.CompatibleVersions);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.True(block.IsResolved, $"{block.DiagnosticCode}: {block.Diagnostic}");
        Assert.Equal(["critical_strike_chance_+%"], block.StatIds);
        Assert.Equal(["block:bounded-crit"], block.CatalogBlocks.Select(candidate => candidate.Id));
    }

    [Fact]
    public void Resolve_AnnotatedBoundsDifferAndObservedRollIsOutsideSource_RemainsVersionMismatch()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Test Bounded Crit
            Amber Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            38(30-50)% increased Global Critical Strike Chance
            """);
        var catalog = CreateCatalog("Test Bounded Crit", "Amber Amulet", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "bounded-crit",
                    "(40-50)% increased Global Critical Strike Chance",
                    "<number>% increased Global Critical Strike Chance",
                    "critical_strike_chance_+%")));

        var block = Assert.Single(resolver.Resolve(parsed, catalog).ModifierBlocks);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_AnnotatedBoundCandidatesWithConflictingSemanticFingerprints_RemainVersionMismatch()
    {
        var parsed = parser.Parse("""
            Item Class: Helmets
            Rarity: Unique
            Test Bounded Defence
            Test Circlet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            48(30-50)% increased Energy Shield
            """);
        var catalog = CreateCatalog("Test Bounded Defence", "Test Circlet", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "global-defence",
                    "(40-50)% increased Energy Shield",
                    "<number>% increased Energy Shield",
                    "energy_shield_+%",
                    UniqueModifierSemanticLocality.Global)),
            Version("Historical", UniqueItemVersionRole.Historical,
                RuntimeEvidenceBlock(
                    "local-defence",
                    "(40-50)% increased Energy Shield",
                    "<number>% increased Energy Shield",
                    "energy_shield_+%",
                    UniqueModifierSemanticLocality.Local)));

        var block = Assert.Single(resolver.Resolve(parsed, catalog).ModifierBlocks);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_AnnotatedBoundCandidatesWithConflictingValueTransforms_RemainVersionMismatch()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Test Bounded Recovery
            Coral Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            25(20-30)% increased Recovery Rate
            """);
        var catalog = CreateCatalog("Test Bounded Recovery", "Coral Amulet", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "ordinary-recovery",
                    "(22-28)% increased Recovery Rate",
                    "<number>% increased Recovery Rate",
                    "recovery_rate_+%")),
            Version("Historical", UniqueItemVersionRole.Historical,
                RuntimeEvidenceBlock(
                    "transformed-recovery",
                    "(22-28)% increased Recovery Rate",
                    "<number>% increased Recovery Rate",
                    "recovery_rate_+%",
                    matchedTransformations: ["negate"])));

        var block = Assert.Single(resolver.Resolve(parsed, catalog).ModifierBlocks);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_DeterministicNumericPluralPresentation_Resolves()
    {
        var parsed = parser.Parse("""
            Item Class: Utility Flasks
            Rarity: Unique
            Test Charge Flask
            Test Flask
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Gain 2(1-3) Power Charges on use
            """);
        var catalog = CreateCatalog("Test Charge Flask", "Test Flask", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "power-charge",
                    "Gain (1-3) Power Charge on use",
                    "Gain <number> Power Charge on use",
                    "gain_power_charges_on_use")));

        var block = Assert.Single(resolver.Resolve(parsed, catalog).ModifierBlocks);

        Assert.True(block.IsResolved, $"{block.DiagnosticCode}: {block.Diagnostic}");
        Assert.Equal(["gain_power_charges_on_use"], block.StatIds);
    }

    [Fact]
    public void Resolve_ArbitraryLexicalDifference_DoesNotUsePresentationFallback()
    {
        var parsed = parser.Parse("""
            Item Class: Utility Flasks
            Rarity: Unique
            Test Charge Flask
            Test Flask
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Gain 2(1-3) Power Charges when used
            """);
        var catalog = CreateCatalog("Test Charge Flask", "Test Flask", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "power-charge",
                    "Gain (1-3) Power Charge on use",
                    "Gain <number> Power Charge on use",
                    "gain_power_charges_on_use")));

        var block = Assert.Single(resolver.Resolve(parsed, catalog).ModifierBlocks);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_SignedMixedRangePresentationWithContainedObservedValue_Resolves()
    {
        var parsed = parser.Parse("""
            Item Class: One Hand Axes
            Rarity: Unique
            Test Signed Rage
            Test Axe
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            -3(-5-5) to Maximum Rage
            """);
        var catalog = CreateCatalog("Test Signed Rage", "Test Axe", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "maximum-rage",
                    "+(-5-5) to Maximum Rage",
                    "+<number> to Maximum Rage",
                    "maximum_rage")));

        var block = Assert.Single(resolver.Resolve(parsed, catalog).ModifierBlocks);

        Assert.True(block.IsResolved, $"{block.DiagnosticCode}: {block.Diagnostic}");
        Assert.Equal(["maximum_rage"], block.StatIds);
    }

    [Fact]
    public void Resolve_PositiveSourceDomainCannotProveNegativeObservedValue()
    {
        var parsed = parser.Parse("""
            Item Class: Belts
            Rarity: Unique
            Test Signed Attribute
            Chain Belt
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            -20(-25--15) to Intelligence
            """);
        var catalog = CreateCatalog("Test Signed Attribute", "Chain Belt", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "positive-intelligence",
                    "+(15-25) to Intelligence",
                    "+<number> to Intelligence",
                    "additional_intelligence")));

        var block = Assert.Single(resolver.Resolve(parsed, catalog).ModifierBlocks);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_SignedRangeDoesNotMakeIncreasedAndReducedInterchangeable()
    {
        var parsed = parser.Parse("""
            Item Class: Tinctures
            Rarity: Unique
            Test Mana Burn
            Prismatic Tincture
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            18(35--35)% reduced Mana Burn rate
            """);
        var catalog = CreateCatalog("Test Mana Burn", "Prismatic Tincture", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "mana-burn",
                    "(35--35)% increased Mana Burn rate",
                    "<number>% increased Mana Burn rate",
                    "mana_burn_rate_+%")));

        var block = Assert.Single(resolver.Resolve(parsed, catalog).ModifierBlocks);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_ParserSeparatedOptionAnnotationRetainsSelectedMechanicalIdentity()
    {
        var parsed = parser.Parse("""
            Item Class: Jewels
            Rarity: Unique
            Test Historic Jewel
            Timeless Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Commanded leadership over 14245(10000-18000) warriors under Rakiata(Akoya-Rakiata)
            Passives in radius are Conquered by the Karui
            Historic
            """);
        var catalog = CreateCatalog("Test Historic Jewel", "Timeless Jewel", UniqueItemKind.Ordinary,
            Version("Rakiata", UniqueItemVersionRole.Current,
                RuntimeMultiLineEvidenceBlock(
                    "rakiata",
                    [
                        "Commanded leadership over (10000-18000) warriors under Rakiata",
                        "Passives in radius are Conquered by the Karui",
                        "Historic",
                    ],
                    "rakiata_mechanics") with
                {
                    CanonicalSignatures =
                    [
                        "Commanded leadership over <number> warriors under Rakiata",
                        "Passives in radius are Conquered by the Karui",
                        "Historic",
                    ],
                }),
            Version("Akoya", UniqueItemVersionRole.Current,
                RuntimeMultiLineEvidenceBlock(
                    "akoya",
                    [
                        "Commanded leadership over (10000-18000) warriors under Akoya",
                        "Passives in radius are Conquered by the Karui",
                        "Historic",
                    ],
                    "akoya_mechanics") with
                {
                    CanonicalSignatures =
                    [
                        "Commanded leadership over <number> warriors under Akoya",
                        "Passives in radius are Conquered by the Karui",
                        "Historic",
                    ],
                }));

        var block = Assert.Single(resolver.Resolve(parsed, catalog).ModifierBlocks);

        Assert.True(block.IsResolved, $"{block.DiagnosticCode}: {block.Diagnostic}");
        Assert.Equal(["rakiata_mechanics"], block.StatIds);
        Assert.Equal(["block:rakiata"], block.CatalogBlocks.Select(candidate => candidate.Id));
        Assert.DoesNotContain(block.CatalogBlocks, candidate => candidate.Id == "block:akoya");

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var component = Assert.Single(draft.ModifierFilters);
        Assert.Equal(3, component.OriginalText.Split(Environment.NewLine).Length);
        Assert.Contains(
            "Commanded leadership over <number> warriors under Rakiata",
            component.ProviderSearchSignatures);
        Assert.DoesNotContain(
            "Commanded leadership over <number> warriors under Rakiata(Akoya-Rakiata)",
            component.ProviderSearchSignatures);
        Assert.Equal(14245m, component.ObservedNumericValues.Single());
        Assert.Equal([14245m], component.CanonicalNumericValues);
        Assert.Null(component.FixedQueryValue);
        Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(14245m, component.RequestedMinimum);
        Assert.Equal(14245m, component.RequestedMaximum);
        Assert.Contains("Rakiata(Akoya-Rakiata)", component.OriginalText, StringComparison.Ordinal);
        Assert.Contains("under Rakiata", component.PresentationText, StringComparison.Ordinal);
        Assert.DoesNotContain("(Akoya-Rakiata)", component.PresentationText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "Test Maraketh Jewel",
        "Denoted service of 4321(500-8000) dekhara in the akhara of Balbala(Nasima-Balbala)",
        "Denoted service of (500-8000) dekhara in the akhara of Balbala",
        "Denoted service of <number> dekhara in the akhara of Balbala",
        "Passives in radius are Conquered by the Maraketh",
        4321)]
    [InlineData(
        "Test Vaal Jewel",
        "Bathed in the blood of 6789(100-8000) sacrificed in the name of Doryani(Xibaqua-Doryani)",
        "Bathed in the blood of (100-8000) sacrificed in the name of Doryani",
        "Bathed in the blood of <number> sacrificed in the name of Doryani",
        "Passives in radius are Conquered by the Vaal",
        6789)]
    [InlineData(
        "Test Templar Jewel",
        "Carved to glorify 7654(2000-10000) new faithful converted by High Templar Avarius(Dominus-Avarius)",
        "Carved to glorify (2000-10000) new faithful converted by High Templar Avarius",
        "Carved to glorify <number> new faithful converted by High Templar Avarius",
        "Passives in radius are Conquered by the Templars",
        7654)]
    public void CreateDraft_ExactMultilineUniqueSourceExposesGenericPerLineProviderEvidence(
        string itemName,
        string copiedFirstLine,
        string sourceFirstLine,
        string providerSignature,
        string conqueredLine,
        int seed)
    {
        var parsed = parser.Parse($$"""
            Item Class: Jewels
            Rarity: Unique
            {{itemName}}
            Timeless Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{copiedFirstLine}}
            {{conqueredLine}}
            Historic
            """);
        var catalog = CreateCatalog(itemName, "Timeless Jewel", UniqueItemKind.Ordinary,
            Version("Selected", UniqueItemVersionRole.Current,
                RuntimeMultiLineEvidenceBlock(
                    "selected-timeless",
                    [sourceFirstLine, conqueredLine, "Historic"],
                    "selected_timeless_mechanics") with
                {
                    CanonicalSignatures = [providerSignature, conqueredLine, "Historic"],
                }));

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var component = Assert.Single(draft.ModifierFilters);

        Assert.True(component.HasExactUniqueSourceProvenance);
        Assert.Contains(providerSignature, component.ProviderSearchSignatures);
        Assert.Null(component.FixedQueryValue);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
        Assert.Equal(seed, component.RequestedMinimum);
        Assert.Equal(seed, component.RequestedMaximum);
        Assert.Equal([seed], component.ObservedNumericValues);
        Assert.Equal([seed], component.CanonicalNumericValues);
    }

    [Fact]
    public void Resolve_ExactCurrentAndHistoricalRowsRetainPerRowProvenanceWithoutSyntheticVersion()
    {
        var parsed = parser.Parse("""
            Item Class: One Hand Maces
            Rarity: Unique
            Test Hybrid Sceptre
            Crystal Sceptre
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            60% increased Intelligence Requirement
            { Unique Modifier }
            Attacks with this weapon inflict Hallowing Flame on Hit
            { Unique Modifier }
            3% increased Experience gain
            """);
        var catalog = CreateCatalog("Test Hybrid Sceptre", "Crystal Sceptre", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock(
                    "shared-requirement-current",
                    "60% increased Intelligence Requirement",
                    "<number>% increased Intelligence Requirement",
                    "int_requirement"),
                RuntimeEvidenceBlock(
                    "current-flame",
                    "Attacks with this weapon inflict Hallowing Flame on Hit",
                    "Attacks with this weapon inflict Hallowing Flame on Hit",
                    "hallowing_flame")),
            Version("Historical", UniqueItemVersionRole.Historical,
                RuntimeEvidenceBlock(
                    "shared-requirement-historical",
                    "60% increased Intelligence Requirement",
                    "<number>% increased Intelligence Requirement",
                    "int_requirement"),
                RuntimeEvidenceBlock(
                    "historical-experience",
                    "3% increased Experience gain",
                    "<number>% increased Experience gain",
                    "experience_gain_+%")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Empty(result.CompatibleVersions);
        Assert.Equal("UNIQUE_VERSION_NOT_FOUND", result.DiagnosticCode);
        Assert.Equal(3, result.ModifierBlocks.Count);
        Assert.All(result.ModifierBlocks, block => Assert.True(
            block.IsResolved,
            $"{block.DiagnosticCode}: {block.Diagnostic}"));
        Assert.Equal(
            ["experience_gain_+%", "hallowing_flame", "int_requirement"],
            result.ModifierBlocks.SelectMany(block => block.StatIds).Order().ToArray());
        Assert.Contains(result.ModifierBlocks, block =>
            block.CatalogBlocks.Any(candidate => candidate.Id == "block:current-flame"));
        Assert.Contains(result.ModifierBlocks, block =>
            block.CatalogBlocks.Any(candidate => candidate.Id == "block:historical-experience"));
    }

    [Fact]
    public void Resolve_HybridRowWithDifferentExactSemanticCandidates_RemainsAmbiguous()
    {
        var parsed = parser.Parse("""
            Item Class: One Hand Maces
            Rarity: Unique
            Test Hybrid Conflict
            Crystal Sceptre
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Current-only effect
            { Unique Modifier }
            Historical-only effect
            { Unique Modifier }
            Shared rendered effect
            """);
        var catalog = CreateCatalog("Test Hybrid Conflict", "Crystal Sceptre", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock("current-only", "Current-only effect", "Current-only effect", "current_stat"),
                RuntimeEvidenceBlock("shared-current", "Shared rendered effect", "Shared rendered effect", "first_stat")),
            Version("Historical", UniqueItemVersionRole.Historical,
                RuntimeEvidenceBlock("historical-only", "Historical-only effect", "Historical-only effect", "historical_stat"),
                RuntimeEvidenceBlock("shared-historical", "Shared rendered effect", "Shared rendered effect", "second_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Empty(result.CompatibleVersions);
        var shared = result.ModifierBlocks.Single(block => block.ParsedModifierIndex == 2);
        Assert.False(shared.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_INDEPENDENT_DIMENSIONS", shared.DiagnosticCode);
    }

    [Fact]
    public void Resolve_RangeOnlyHistoricalEvidenceDoesNotEnableHybridFallback()
    {
        var parsed = parser.Parse("""
            Item Class: One Hand Maces
            Rarity: Unique
            Test Range Hybrid
            Crystal Sceptre
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Current-only effect
            { Unique Modifier }
            Historical-only effect
            { Unique Modifier }
            4(3-5)% increased Experience gain
            """);
        var catalog = CreateCatalog("Test Range Hybrid", "Crystal Sceptre", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                RuntimeEvidenceBlock("current-only", "Current-only effect", "Current-only effect", "current_stat")),
            Version("Historical", UniqueItemVersionRole.Historical,
                RuntimeEvidenceBlock("historical-only", "Historical-only effect", "Historical-only effect", "historical_stat"),
                EvidenceBlock(
                    "historical-range",
                    "(1-10)% increased Experience gain",
                    "<number>% increased Experience gain",
                    "experience_gain_+%")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Empty(result.CompatibleVersions);
        var rangeOnly = result.ModifierBlocks.Single(block => block.ParsedModifierIndex == 2);
        Assert.False(rangeOnly.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", rangeOnly.DiagnosticCode);
    }

    [Fact]
    public void Resolve_IndependentCurrentOptionAxesRemainVersionMismatch()
    {
        var parsed = parser.Parse("""
            Item Class: Jewels
            Rarity: Unique
            Test Option Jewel
            Crimson Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Limited to: 2
            { Unique Modifier }
            +5 to maximum Energy Shield
            { Unique Modifier }
            +5 to Intelligence
            """);
        var shared = RuntimeEvidenceBlock(
            "shared-limit",
            "Limited to: 2",
            "Limited to: <number>",
            "limit_stat");
        var catalog = CreateCatalog("Test Option Jewel", "Crimson Jewel", UniqueItemKind.Ordinary,
            Version("Energy Shield", UniqueItemVersionRole.Current,
                shared,
                RuntimeEvidenceBlock("energy-shield", "+5 to maximum Energy Shield", "+<number> to maximum Energy Shield", "energy_shield")),
            Version("Intelligence", UniqueItemVersionRole.Current,
                shared with { Id = "block:shared-limit-intelligence" },
                RuntimeEvidenceBlock("intelligence", "+5 to Intelligence", "+<number> to Intelligence", "intelligence")),
            Version("Strength", UniqueItemVersionRole.Current,
                shared with { Id = "block:shared-limit-strength" },
                RuntimeEvidenceBlock("strength", "+5 to Strength", "+<number> to Strength", "strength")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(2, result.CompatibleVersions.Count);
        Assert.Equal(2, result.ModifierBlocks.Count(block =>
            block.DiagnosticCode == "UNIQUE_BLOCK_VERSION_MISMATCH"));
        Assert.DoesNotContain(result.ModifierBlocks.Where(block => block.ParsedModifierIndex > 0),
            block => block.IsResolved);
    }

    [Fact]
    public void Resolve_NoCoherentVersion_StillResolvesACommonBlockProvenAcrossEveryIdentityVersion()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Test Generated Crown
            Onyx Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Cannot be Stunned
            { Unique Modifier }
            +9(8-10) to Dexterity
            """);
        var catalog = CreateCatalog("Test Generated Crown", "Onyx Amulet", UniqueItemKind.Ordinary,
            Version("Generated", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "stable-stun",
                    "Cannot be Stunned",
                    "Cannot be Stunned",
                    "stun_stat"),
                EvidenceBlock(
                    "incompatible-dexterity",
                    "+(1-5) to Dexterity",
                    "+<number> to Dexterity",
                    "dexterity_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Empty(result.CompatibleVersions);
        Assert.Equal(2, result.ModifierBlocks.Count);
        var stable = Assert.Single(result.ModifierBlocks, block => block.IsResolved);
        Assert.Equal(["stun_stat"], stable.StatIds);
        var incompatible = Assert.Single(result.ModifierBlocks, block => !block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", incompatible.DiagnosticCode);
    }

    [Fact]
    public void Resolve_NoFullyMatchingVersion_UsesStrictlyBestExactBlockEvidenceWithoutRepairingUnmatchedRows()
    {
        var parsed = parser.Parse("""
            Item Class: Body Armours
            Rarity: Unique
            Test Fostering
            Exquisite Leather
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +100 to maximum Life
            { Unique Modifier }
            Grants Level 20 Summon Bestial Ursa Skill
            { Unique Modifier }
            Projectiles inflict Bleeding while you have a Bestial Minion
            """);
        var catalog = CreateCatalog("Test Fostering", "Exquisite Leather", UniqueItemKind.Ordinary,
            Version("Rhoa Current", UniqueItemVersionRole.Current,
                Block("shared-life-rhoa", "+<number> to maximum Life", "life_stat"),
                Block("rhoa-skill", "Grants Level 20 Summon Bestial Rhoa Skill", "rhoa_stat")),
            Version("Ursa Current", UniqueItemVersionRole.Current,
                Block("shared-life-ursa", "+<number> to maximum Life", "life_stat"),
                Block("ursa-skill", "Grants Level 20 Summon Bestial Ursa Skill", "ursa_stat"),
                Block(
                    "ursa-bleed-source",
                    "Projectiles have 100% chance to inflict Bleeding while you have a Bestial Minion",
                    "ursa_bleed_stat")));

        var result = resolver.Resolve(parsed, catalog);

        var version = Assert.Single(result.CompatibleVersions);
        Assert.Equal("Ursa Current", version.Label);
        Assert.Equal(3, result.ModifierBlocks.Count);
        Assert.Equal(2, result.ModifierBlocks.Count(block => block.IsResolved));
        var unmatched = Assert.Single(result.ModifierBlocks, block => !block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", unmatched.DiagnosticCode);
    }

    [Fact]
    public void Resolve_GeneratedCandidatePool_SelectsExactAnnotatedRollCandidate()
    {
        var parsed = parser.Parse("""
            Item Class: Helmets
            Rarity: Unique
            Test Generated Crown
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Socketed Gems are Supported by Level 26(25-35) Inspiration
            """);
        var version = Version("Generated", UniqueItemVersionRole.Current,
            GeneratedEvidenceBlock(
                "inspiration-low",
                "Socketed Gems are Supported by Level (1-10) Inspiration",
                "Socketed Gems are Supported by Level <number> Inspiration",
                "inspiration_stat",
                "pool:low"),
            GeneratedEvidenceBlock(
                "inspiration-high",
                "Socketed Gems are Supported by Level (25-35) Inspiration",
                "Socketed Gems are Supported by Level <number> Inspiration",
                "inspiration_stat",
                "pool:high")) with
        {
            GeneratedCandidateSelectionLimit = 2,
        };
        var catalog = CreateCatalog(
            "Test Generated Crown",
            "Great Crown",
            UniqueItemKind.Ordinary,
            version);

        var result = resolver.Resolve(parsed, catalog);

        Assert.Single(result.CompatibleVersions);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.True(block.IsResolved);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, block.SourceSemantics);
        Assert.Equal(["pool:high"], block.CandidatePoolMembershipIds);
        Assert.Equal(
            "Socketed Gems are Supported by Level (25-35) Inspiration",
            Assert.Single(block.CatalogBlocks).Lines[0]);
    }

    [Fact]
    public void Resolve_GeneratedCandidatePool_AbsentCandidatesAreNotRequired()
    {
        var parsed = parser.Parse("""
            Item Class: Helmets
            Rarity: Unique
            Test Generated Crown
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +30 to all Attributes
            """);
        var version = Version("Generated", UniqueItemVersionRole.Current,
            EvidenceBlock(
                "fixed-attributes",
                "+30 to all Attributes",
                "+<number> to all Attributes",
                "attributes_stat"),
            GeneratedEvidenceBlock(
                "optional-strength",
                "+(20-30) to Strength",
                "+<number> to Strength",
                "strength_stat",
                "pool:strength"),
            GeneratedEvidenceBlock(
                "optional-dexterity",
                "+(20-30) to Dexterity",
                "+<number> to Dexterity",
                "dexterity_stat",
                "pool:dexterity")) with
        {
            GeneratedCandidateSelectionLimit = 1,
        };
        var catalog = CreateCatalog(
            "Test Generated Crown",
            "Great Crown",
            UniqueItemKind.Ordinary,
            version);

        var result = resolver.Resolve(parsed, catalog);

        Assert.Single(result.CompatibleVersions);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.True(block.IsResolved);
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, block.SourceSemantics);
        Assert.DoesNotContain(result.ModifierBlocks.SelectMany(entry => entry.CatalogBlocks),
            candidate => candidate.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate);
    }

    [Fact]
    public void Resolve_UnknownCopiedGeneratedRow_FailsAtCandidatePoolLayer()
    {
        var parsed = parser.Parse("""
            Item Class: Helmets
            Rarity: Unique
            Test Generated Crown
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            This copied generated effect is not in the source pool
            """);
        var version = Version("Generated", UniqueItemVersionRole.Current,
            GeneratedEvidenceBlock(
                "optional-strength",
                "+(20-30) to Strength",
                "+<number> to Strength",
                "strength_stat",
                "pool:strength")) with
        {
            GeneratedCandidateSelectionLimit = 1,
        };
        var catalog = CreateCatalog(
            "Test Generated Crown",
            "Great Crown",
            UniqueItemKind.Ordinary,
            version);

        var result = resolver.Resolve(parsed, catalog);

        Assert.Empty(result.CompatibleVersions);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_GENERATED_CANDIDATE_NOT_FOUND", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_OrdinaryFixedDefinition_CannotBecomeGeneratedPool()
    {
        var parsed = parser.Parse("""
            Item Class: Helmets
            Rarity: Unique
            Test Fixed Crown
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            This copied effect is not in the fixed definition
            """);
        var catalog = CreateCatalog("Test Fixed Crown", "Great Crown", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "fixed-strength",
                    "+(20-30) to Strength",
                    "+<number> to Strength",
                    "strength_stat")));

        var result = resolver.Resolve(parsed, catalog);

        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_GeneratedSelectionLimit_RejectsImpossibleExcessCandidateCombination()
    {
        var parsed = parser.Parse("""
            Item Class: Helmets
            Rarity: Unique
            Test Generated Crown
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +20 to Strength
            { Unique Modifier }
            +20 to Dexterity
            { Unique Modifier }
            +20 to Intelligence
            """);
        var version = Version("Generated", UniqueItemVersionRole.Current,
            GeneratedEvidenceBlock("strength", "+(20-30) to Strength", "+<number> to Strength",
                "strength_stat", "pool:strength"),
            GeneratedEvidenceBlock("dexterity", "+(20-30) to Dexterity", "+<number> to Dexterity",
                "dexterity_stat", "pool:dexterity"),
            GeneratedEvidenceBlock("intelligence", "+(20-30) to Intelligence", "+<number> to Intelligence",
                "intelligence_stat", "pool:intelligence")) with
        {
            GeneratedCandidateSelectionLimit = 2,
        };
        var catalog = CreateCatalog(
            "Test Generated Crown",
            "Great Crown",
            UniqueItemKind.Ordinary,
            version);

        var result = resolver.Resolve(parsed, catalog);

        Assert.Empty(result.CompatibleVersions);
        Assert.Equal(3, result.ModifierBlocks.Count);
        Assert.All(result.ModifierBlocks, block =>
        {
            Assert.False(block.IsResolved);
            Assert.Equal("UNIQUE_GENERATED_SELECTION_LIMIT_EXCEEDED", block.DiagnosticCode);
        });
    }

    [Fact]
    public void Resolve_GeneratedSelectionLimit_CountsOneMembershipAcrossSeparateCandidateRows()
    {
        var parsed = parser.Parse("""
            Item Class: Jewels
            Rarity: Unique
            Test Generated Jewel
            Crimson Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Requires Class Witch
            { Unique Modifier }
            Allocates Test Passive if you have the matching modifier on Test Pair
            """);
        var version = Version("Generated", UniqueItemVersionRole.Current,
            GeneratedEvidenceBlock(
                "class",
                "Requires Class Witch",
                "Requires Class Witch",
                "class_stat",
                "pool:witch"),
            GeneratedEvidenceBlock(
                "passive",
                "Allocates Test Passive if you have the matching modifier on Test Pair",
                "Allocates Test Passive if you have the matching modifier on Test Pair",
                "passive_stat",
                "pool:witch")) with
        {
            GeneratedCandidateSelectionLimit = 1,
        };
        var catalog = CreateCatalog(
            "Test Generated Jewel",
            "Crimson Jewel",
            UniqueItemKind.Ordinary,
            version);

        var result = resolver.Resolve(parsed, catalog);

        Assert.Single(result.CompatibleVersions);
        Assert.Equal(2, result.ModifierBlocks.Count);
        Assert.All(result.ModifierBlocks, block => Assert.True(block.IsResolved));
    }

    [Theory]
    [InlineData("Pride(Fireball-Mana-Infused Staff) has no Reservation", "Pride has no Reservation")]
    [InlineData("Socketed Gems are Supported by Level 35 Ice Bite(Greater Multiple Projectiles-Hallow)", "Socketed Gems are Supported by Level 35 Ice Bite")]
    public void CreateDraft_GeneratedAttachedAnnotation_UsesCleanPresentationAndPreservesRawEvidence(
        string rawLine,
        string presentationLine)
    {
        var parsed = parser.Parse($$"""
            Item Class: Amulets
            Rarity: Unique
            Test Dragonflight
            Onyx Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{rawLine}}
            """);
        var catalog = CreateCatalog("Test Dragonflight", "Onyx Amulet", UniqueItemKind.Ordinary,
            Version("Generated", UniqueItemVersionRole.Current,
                GeneratedEvidenceBlock(
                    "generated-pride",
                    presentationLine,
                    presentationLine,
                    "pride_stat",
                    "pool:generated-pride")) with
            {
                GeneratedCandidateSelectionLimit = 1,
            },
            Version("Non-generated", UniqueItemVersionRole.Historical,
                EvidenceBlock(
                    "non-generated-pride",
                    presentationLine,
                    presentationLine,
                    "other_pride_stat")));

        var resolution = resolver.Resolve(parsed, catalog);

        var version = Assert.Single(resolution.CompatibleVersions);
        Assert.Equal("Generated", version.Label);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, block.SourceSemantics);
        Assert.Single(block.CandidatePoolMembershipIds);
        Assert.Equal([presentationLine], block.PresentationLines);

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var row = Assert.Single(draft.ModifierFilters);
        Assert.Equal(rawLine, row.OriginalText);
        Assert.Equal(presentationLine, row.PresentationText);
        Assert.Equal(rawLine, Assert.Single(row.Sources).OriginalText);
    }

    [Fact]
    public void CreateDraft_GeneratedAttachedAnnotation_CleansPresentationWhenRollEvidenceKeepsMechanicsUnresolved()
    {
        const string rawLine = "Socketed Gems are Supported by Level 26(25-35) Inspiration(Greater Multiple Projectiles-Hallow)";
        const string presentationLine = "Socketed Gems are Supported by Level 26(25-35) Inspiration";
        var parsed = parser.Parse($$"""
            Item Class: Helmets
            Rarity: Unique
            Test Shako
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{rawLine}}
            """);
        var catalog = CreateCatalog("Test Shako", "Great Crown", UniqueItemKind.Ordinary,
            Version("Generated", UniqueItemVersionRole.Current,
                GeneratedEvidenceBlock(
                    "generated-inspiration",
                    "Socketed Gems are Supported by Level (1-10) Inspiration",
                    "Socketed Gems are Supported by Level <number> Inspiration",
                    "inspiration_stat",
                    "pool:generated-inspiration")) with
            {
                GeneratedCandidateSelectionLimit = 1,
            });

        var resolution = resolver.Resolve(parsed, catalog);

        Assert.Empty(resolution.CompatibleVersions);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal([presentationLine], block.PresentationLines);

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var row = Assert.Single(draft.ModifierFilters);
        Assert.False(row.IsSearchable);
        Assert.Equal(rawLine, row.OriginalText);
        Assert.Equal(presentationLine, row.PresentationText);
        Assert.Equal(rawLine, Assert.Single(row.Sources).OriginalText);
    }

    [Fact]
    public void Resolve_MeaningfulParentheses_ExactGeneratedTextWinsOverTextualProjection()
    {
        const string rawLine = "Selected modifier(Alpha-Omega)";
        var parsed = parser.Parse($$"""
            Item Class: Helmets
            Rarity: Unique
            Test Crown
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{rawLine}}
            """);
        var version = Version("Generated", UniqueItemVersionRole.Current,
            GeneratedEvidenceBlock(
                "meaningful-parentheses",
                rawLine,
                rawLine,
                "meaningful_stat",
                "pool:meaningful"),
            GeneratedEvidenceBlock(
                "stripped-collision",
                "Selected modifier",
                "Selected modifier",
                "stripped_stat",
                "pool:stripped")) with
        {
            GeneratedCandidateSelectionLimit = 2,
        };
        var catalog = CreateCatalog(
            "Test Crown",
            "Great Crown",
            UniqueItemKind.Ordinary,
            version);

        var result = resolver.Resolve(parsed, catalog);

        var block = Assert.Single(result.ModifierBlocks);
        Assert.True(block.IsResolved, block.Diagnostic);
        Assert.Equal(["pool:meaningful"], block.CandidatePoolMembershipIds);
        Assert.Empty(block.PresentationLines);
        Assert.Empty(block.TextualOptionRangeAnnotations);
    }

    [Theory]
    [InlineData("Selected modifier(Alpha)")]
    [InlineData("Selected modifier(Alpha-)")]
    [InlineData("Selected modifier(-Omega)")]
    public void Resolve_MalformedTextualOptionRange_FailsClosed(string rawLine)
    {
        var parsed = parser.Parse($$"""
            Item Class: Helmets
            Rarity: Unique
            Test Crown
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{rawLine}}
            """);
        var version = Version("Generated", UniqueItemVersionRole.Current,
            GeneratedEvidenceBlock(
                "semantic-candidate",
                "Selected modifier",
                "Selected modifier",
                "selected_stat",
                "pool:selected")) with
        {
            GeneratedCandidateSelectionLimit = 1,
        };
        var catalog = CreateCatalog(
            "Test Crown",
            "Great Crown",
            UniqueItemKind.Ordinary,
            version);

        var result = resolver.Resolve(parsed, catalog);

        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_GENERATED_CANDIDATE_NOT_FOUND", block.DiagnosticCode);
        Assert.Empty(block.CandidatePoolMembershipIds);
        Assert.Empty(block.PresentationLines);
    }

    [Fact]
    public void Resolve_TextualOptionRangeProjectionWithMultipleCandidates_FailsAmbiguous()
    {
        const string rawLine = "Selected modifier(Alpha-Omega) — Unscalable Value";
        var parsed = parser.Parse($$"""
            Item Class: Helmets
            Rarity: Unique
            Test Crown
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{rawLine}}
            """);
        var version = Version("Generated", UniqueItemVersionRole.Current,
            GeneratedEvidenceBlock(
                "first-semantic-candidate",
                "Selected modifier",
                "Selected modifier",
                "first_stat",
                "pool:first"),
            GeneratedEvidenceBlock(
                "second-semantic-candidate",
                "Selected modifier",
                "Selected modifier",
                "second_stat",
                "pool:second")) with
        {
            GeneratedCandidateSelectionLimit = 2,
        };
        var catalog = CreateCatalog(
            "Test Crown",
            "Great Crown",
            UniqueItemKind.Ordinary,
            version);

        var result = resolver.Resolve(parsed, catalog);

        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_GENERATED_TEXTUAL_OPTION_RANGE_AMBIGUOUS", block.DiagnosticCode);
        Assert.Empty(block.TextualOptionRangeAnnotations);
    }

    [Fact]
    public void Resolve_KeystoneBlocksWithMultiLineReminders_ResolvesEveryCopiedBlock()
    {
        // Faithful reproduction of the captured shield body: the first two keystone rows carry
        // reminder text spanning several physical lines, the third closes on one line.
        var parsed = parser.Parse("""
            Item Class: Shields
            Rarity: Unique
            Test Machination
            Steel Kite Shield
            --------
            Chance to Block: 26%
            Armour: 164
            Energy Shield: 34
            --------
            Item Level: 85
            --------
            { Unique Modifier — Defences, Energy Shield, Chaos }
            Corrupted Soul — Unscalable Value
            (50% of Non-Chaos Damage taken bypasses Energy Shield
            Gain 15% of Maximum Life as Extra Maximum Energy Shield)
            { Unique Modifier — Chaos, Resistance }
            Divine Flesh — Unscalable Value
            (All Damage taken bypasses Energy Shield
            50% of Elemental Damage taken as Chaos Damage
            +5% to maximum Chaos Resistance)
            (Maximum Resistances cannot be raised above 90%)
            { Unique Modifier — Life }
            Vaal Pact — Unscalable Value
            (Life Leech from Melee Damage is Instant. Cannot Recover Life other than from Leech)
            --------
            Corrupted
            """);

        Assert.Collection(
            parsed.UniqueModifiers,
            corruptedSoul => Assert.Equal(["Corrupted Soul"], corruptedSoul.ValueLines),
            divineFlesh => Assert.Equal(["Divine Flesh"], divineFlesh.ValueLines),
            vaalPact => Assert.Equal(["Vaal Pact"], vaalPact.ValueLines));

        var catalog = CreateCatalog(
            "Test Machination",
            "Steel Kite Shield",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock("corrupted-soul", "Corrupted Soul", "Corrupted Soul", "corrupted_soul_stat"),
                EvidenceBlock("divine-flesh", "Divine Flesh", "Divine Flesh", "divine_flesh_stat"),
                EvidenceBlock("vaal-pact", "Vaal Pact", "Vaal Pact", "vaal_pact_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, result.Status);
        Assert.Collection(
            result.ModifierBlocks,
            corruptedSoul => AssertResolvedKeystoneBlock(corruptedSoul, "corrupted_soul_stat"),
            divineFlesh => AssertResolvedKeystoneBlock(divineFlesh, "divine_flesh_stat"),
            vaalPact => AssertResolvedKeystoneBlock(vaalPact, "vaal_pact_stat"));
    }

    private static void AssertResolvedKeystoneBlock(UniqueModifierBlockResolution block, string expectedStatId)
    {
        Assert.True(block.IsResolved);
        Assert.Null(block.DiagnosticCode);
        Assert.Single(block.CatalogBlocks);
        Assert.Equal([expectedStatId], block.StatIds);
    }

    [Fact]
    public void Resolve_UnrecognizedMetadataKindOnResolvedUnique_RecoversExactIdentityBoundBlock()
    {
        var parsed = ParseWithUnrecognizedKindRow("{ Monster Modifier — Caster, Curse }");
        var catalog = UnrecognizedKindCatalog(
            EvidenceBlock("armour", "59% increased Armour", "<number>% increased Armour", "armour_stat"),
            EvidenceBlock("curse", CurseLine, CurseLine, "curse_stat"));

        var result = resolver.Resolve(parsed, catalog);

        var row = Assert.Single(parsed.Modifiers, modifier => modifier.ValueLines.Contains(CurseLine));
        Assert.Equal(ParsedModifierKind.Unknown, row.Kind);
        Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, row.UniqueOrigin);
        Assert.Contains("Monster Modifier", row.RawMetadataLine!, StringComparison.Ordinal);

        var recovered = Assert.Single(result.ModifierBlocks, block => block.IsIdentityBoundRecovery);
        Assert.True(recovered.IsResolved);
        Assert.Null(recovered.DiagnosticCode);
        Assert.Equal(["curse_stat"], recovered.StatIds);
        Assert.Equal("block:curse", Assert.Single(recovered.CatalogBlocks).Id);
        Assert.NotEmpty(recovered.SourceObservationIds);
    }

    [Theory]
    // The label itself is irrelevant; any unrecognized kind is eligible on identity-bound proof.
    [InlineData("{ Monster Modifier — Caster, Curse }")]
    [InlineData("{ Corrupted Modifier — Caster, Curse }")]
    public void Resolve_AnyUnrecognizedMetadataKind_UsesTheSameIdentityBoundProof(string metadataLine)
    {
        var parsed = ParseWithUnrecognizedKindRow(metadataLine);
        var catalog = UnrecognizedKindCatalog(EvidenceBlock("curse", CurseLine, CurseLine, "curse_stat"));

        var result = resolver.Resolve(parsed, catalog);

        var recovered = Assert.Single(result.ModifierBlocks, block => block.IsIdentityBoundRecovery);
        Assert.True(recovered.IsResolved);
        Assert.Equal(["curse_stat"], recovered.StatIds);
    }

    [Fact]
    public void Resolve_UnrecognizedKindRowWithNoMatchingSourceBlock_DeclinesRecovery()
    {
        var parsed = ParseWithUnrecognizedKindRow("{ Monster Modifier — Caster, Curse }");
        var catalog = UnrecognizedKindCatalog(
            EvidenceBlock("other", "Some other unique line", "Some other unique line", "other_stat"));

        var result = resolver.Resolve(parsed, catalog);

        Assert.DoesNotContain(result.ModifierBlocks, block => block.IsIdentityBoundRecovery);
    }

    [Fact]
    public void Resolve_UnrecognizedKindRowMatchingAnotherIdentityOnly_DeclinesRecovery()
    {
        var parsed = ParseWithUnrecognizedKindRow("{ Monster Modifier — Caster, Curse }");
        // The block text exists in the catalog, but only under a different Unique identity.
        var catalog = CreateCatalog(
            "Other Identity",
            "Reinforced Greaves",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock("curse", CurseLine, CurseLine, "curse_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.Unsupported, result.Status);
        Assert.Equal("UNIQUE_IDENTITY_NOT_FOUND", result.DiagnosticCode);
        Assert.DoesNotContain(result.ModifierBlocks, block => block.IsIdentityBoundRecovery);
    }

    [Fact]
    public void Resolve_UnrecognizedKindRowWithTwoIndependentMatchingBlocks_DeclinesRecovery()
    {
        var parsed = ParseWithUnrecognizedKindRow("{ Monster Modifier — Caster, Curse }");
        var catalog = UnrecognizedKindCatalog(
            EvidenceBlock("curse-a", CurseLine, CurseLine, "curse_stat_a"),
            EvidenceBlock("curse-b", CurseLine, CurseLine, "curse_stat_b", "pob-observation:test-two"));

        var result = resolver.Resolve(parsed, catalog);

        Assert.DoesNotContain(result.ModifierBlocks, block => block.IsIdentityBoundRecovery);
    }

    [Fact]
    public void Resolve_UnrecognizedKindRowMatchingFixedAndGeneratedCandidate_DeclinesRecovery()
    {
        var parsed = ParseWithUnrecognizedKindRow("{ Monster Modifier — Caster, Curse }");
        var version = Version("Current", UniqueItemVersionRole.Current,
            EvidenceBlock("curse-fixed", CurseLine, CurseLine, "curse_stat"),
            GeneratedEvidenceBlock("curse-generated", CurseLine, CurseLine, "curse_stat", "pool:curse")) with
        {
            GeneratedCandidateSelectionLimit = 1,
        };
        var catalog = CreateCatalog(
            "Test Greaves",
            "Reinforced Greaves",
            UniqueItemKind.Ordinary,
            version);

        var result = resolver.Resolve(parsed, catalog);

        Assert.DoesNotContain(result.ModifierBlocks, block => block.IsIdentityBoundRecovery);
    }

    [Fact]
    public void Resolve_UnrecognizedKindRowWhenVersionsDisagree_DeclinesRecovery()
    {
        var parsed = ParseWithUnrecognizedKindRow("{ Monster Modifier — Caster, Curse }");
        // Both versions match the armour row equally, so neither can be narrowed away; only one of
        // them contains the curse block, so the identity-bound proof is not exact.
        var armour = EvidenceBlock("armour", "59% increased Armour", "<number>% increased Armour", "armour_stat");
        var catalog = CreateCatalog(
            "Test Greaves",
            "Reinforced Greaves",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                armour,
                EvidenceBlock("curse", CurseLine, CurseLine, "curse_stat")),
            Version("Pre 3.29.0", UniqueItemVersionRole.Historical, armour));

        var result = resolver.Resolve(parsed, catalog);

        Assert.DoesNotContain(result.ModifierBlocks, block => block.IsIdentityBoundRecovery);
    }

    [Fact]
    public void Resolve_UnrecognizedKindRowOnNonUniqueItem_IsNotApplicable()
    {
        var parsed = parser.Parse($$"""
            Item Class: Boots
            Rarity: Rare
            Test Tread
            Reinforced Greaves
            --------
            Item Level: 85
            --------
            { Monster Modifier — Caster, Curse }
            {{CurseLine}}
            """);
        var catalog = UnrecognizedKindCatalog(EvidenceBlock("curse", CurseLine, CurseLine, "curse_stat"));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.NotApplicable, result.Status);
        Assert.Empty(result.ModifierBlocks);
    }

    [Theory]
    // Rows the parser does recognize keep their own domain and are never diverted into the fallback.
    [InlineData("{ Prefix Modifier \"Test\" (Tier: 1) — Caster, Curse }")]
    [InlineData("{ Suffix Modifier \"Test\" (Tier: 1) — Caster, Curse }")]
    [InlineData("{ Implicit Modifier — Caster, Curse }")]
    [InlineData("{ Corruption Implicit Modifier — Caster, Curse }")]
    [InlineData("{ Crafted Modifier — Caster, Curse }")]
    [InlineData("{ Fractured Modifier — Caster, Curse }")]
    public void Resolve_RecognizedNonUniqueKindRow_IsNeverStolenByIdentityBoundRecovery(string metadataLine)
    {
        var parsed = ParseWithUnrecognizedKindRow(metadataLine);
        var catalog = UnrecognizedKindCatalog(EvidenceBlock("curse", CurseLine, CurseLine, "curse_stat"));

        var result = resolver.Resolve(parsed, catalog);

        var row = Assert.Single(parsed.Modifiers, modifier => modifier.ValueLines.Contains(CurseLine));
        Assert.NotEqual(ParsedModifierKind.Unique, row.Kind);
        Assert.DoesNotContain(result.ModifierBlocks, block => block.IsIdentityBoundRecovery);
    }

    [Fact]
    public void Resolve_UniqueModifierRow_IsNeverMarkedAsIdentityBoundRecovery()
    {
        var parsed = ParseWithUnrecognizedKindRow("{ Unique Modifier — Caster, Curse }");
        var catalog = UnrecognizedKindCatalog(EvidenceBlock("curse", CurseLine, CurseLine, "curse_stat"));

        var result = resolver.Resolve(parsed, catalog);

        var block = Assert.Single(result.ModifierBlocks, candidate => candidate.StatIds.Contains("curse_stat"));
        Assert.True(block.IsResolved);
        Assert.False(block.IsIdentityBoundRecovery);
    }

    [Fact]
    public void Resolve_UnrecognizedKindRowOnFoulbornItem_DoesNotAssumeOrdinaryOrigin()
    {
        var parsed = parser.Parse($$"""
            Item Class: Boots
            Rarity: Unique
            Foulborn Test Greaves
            Reinforced Greaves
            --------
            Item Level: 85
            --------
            { Monster Modifier — Caster, Curse }
            {{CurseLine}}
            """);
        var catalog = UnrecognizedKindCatalog(EvidenceBlock("curse", CurseLine, CurseLine, "curse_stat"));

        var result = resolver.Resolve(parsed, catalog);

        Assert.True(result.IsFoulborn);
        Assert.DoesNotContain(result.ModifierBlocks, block => block.IsIdentityBoundRecovery);
    }

    private const string CurseLine = "You can apply an additional Curse";

    private ParsedItem ParseWithUnrecognizedKindRow(string metadataLine)
    {
        return parser.Parse($$"""
            Item Class: Boots
            Rarity: Unique
            Test Greaves
            Reinforced Greaves
            --------
            Item Level: 85
            --------
            { Unique Modifier — Defences, Armour }
            59% increased Armour
            {{metadataLine}}
            {{CurseLine}}
            """);
    }

    private static GameDataCatalog UnrecognizedKindCatalog(params UniqueModifierBlock[] blocks)
    {
        return CreateCatalog(
            "Test Greaves",
            "Reinforced Greaves",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current, blocks));
    }

    private static GameDataCatalog CreateCatalog(
        string name,
        string baseType,
        UniqueItemKind kind,
        params UniqueItemVersionObservation[] versions)
    {
        return CreateCatalog(
            name,
            baseType,
            kind,
            versions,
            additionalModifiers: [],
            translations: [],
            foulbornRelationships: []);
    }

    private static GameDataCatalog CreateCatalog(
        string name,
        string baseType,
        UniqueItemKind kind,
        IReadOnlyList<UniqueItemVersionObservation> versions,
        IReadOnlyList<ModifierDefinition> additionalModifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<UniqueFoulbornModifierRelationship> foulbornRelationships)
    {
        const string observation = "pob-observation:test";
        var mappings = versions.SelectMany(version => version.ModifierBlocks)
            .Select(block => block.MechanicalMapping)
            .ToArray();
        var statIds = mappings.SelectMany(mapping => mapping.StatIds)
            .Concat(additionalModifiers.SelectMany(modifier => modifier.Stats)
                .Select(stat => stat.StatId)
                .Where(statId => !string.IsNullOrWhiteSpace(statId))
                .Select(statId => statId!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var modifiers = mappings.SelectMany(mapping => mapping.ModifierIds.Select(modifierId =>
                new ModifierDefinition
                {
                    Id = modifierId,
                    GroupId = $"group:{modifierId}",
                    Name = modifierId,
                    GenerationType = ModifierGenerationType.Prefix,
                    Domain = "item",
                    Stats = mapping.StatIds.Select((statId, index) => new ModifierStat
                    {
                        Index = index,
                        StatId = statId,
                        MinValue = 1,
                        MaxValue = 1,
                    }).ToArray(),
                }))
            .DistinctBy(modifier => modifier.Id, StringComparer.OrdinalIgnoreCase)
            .Concat(additionalModifiers)
            .DistinctBy(modifier => modifier.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = foulbornRelationships.Count > 0 ? 3 : 2,
                DataVersion = "test",
                CreatedAtUtc = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "path-of-building",
                        RetrievedAtUtc = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
                    },
                ],
            },
            Modifiers = modifiers,
            Stats = statIds.Select(statId => new StatDefinition
            {
                Id = statId,
                IsLocal = statId.StartsWith("local_", StringComparison.OrdinalIgnoreCase),
            }).ToArray(),
            StatTranslations = translations,
            UniqueItems = new UniqueItemCatalog
            {
                SourceObservations =
                [
                    new UniqueCatalogSourceObservation
                    {
                        Id = observation,
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/test.lua",
                        ObservedKind = kind,
                        RawEntrySha256 = new string('a', 64),
                    },
                    new UniqueCatalogSourceObservation
                    {
                        Id = "pob-observation:test-two",
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/test.lua",
                        ObservedKind = kind,
                        RawEntrySha256 = new string('b', 64),
                    },
                    new UniqueCatalogSourceObservation
                    {
                        Id = "generated-observation:test",
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/generated.lua",
                        IsGenerated = true,
                        ObservedKind = kind,
                        RawEntrySha256 = new string('c', 64),
                    },
                ],
                Items =
                [
                    new UniqueItemIdentity
                    {
                        Id = "unique:test",
                        CanonicalName = name,
                        Kind = kind,
                        BaseTypeEvidence = [baseType],
                        Versions = versions.Select(version => version with { BaseType = baseType }).ToArray(),
                        SourceObservationIds = [observation],
                    },
                ],
                FoulbornRelationshipSources = foulbornRelationships.Count == 0
                    ? []
                    :
                    [
                        new UniqueFoulbornRelationshipSourceObservation
                        {
                            Id = "pob-foulborn-source:test",
                            ManifestSourceId = "path-of-building",
                            RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                            Tag = "v2.67.2",
                            CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                            SourcePath = "src/Data/ModFoulbornMap.jsonc",
                            SourceFileSha256 = new string('d', 64),
                        },
                    ],
                FoulbornModifierRelationships = foulbornRelationships,
            },
        });
    }

    private static UniqueItemVersionObservation Version(
        string label,
        UniqueItemVersionRole role,
        params UniqueModifierBlock[] blocks) => new()
    {
        Id = $"version:{label}",
        Label = label,
        Role = role,
        BaseType = "Calling Wand",
        ModifierBlocks = blocks,
        SourceObservationIds = ["pob-observation:test"],
    };

    private static UniqueModifierBlock Block(string id, string signature, string statId) => new()
    {
        Id = $"block:{id}",
        Kind = UniqueModifierBlockKind.Unique,
        Lines = [signature],
        CanonicalSignatures = [signature],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = [$"modifier:{id}"],
            StatIds = [statId],
        },
        SourceObservationIds = id == "spectres"
            ? ["pob-observation:test-two"]
            : ["pob-observation:test"],
    };

    private static UniqueModifierBlock EvidenceBlock(
        string id,
        string line,
        string canonicalSignature,
        string statId,
        string sourceObservationId = "pob-observation:test") => new()
    {
        Id = $"block:{id}",
        Kind = UniqueModifierBlockKind.Unique,
        Lines = [line],
        CanonicalSignatures = [canonicalSignature],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = [$"modifier:{id}"],
            StatIds = [statId],
        },
        SourceObservationIds = [sourceObservationId],
    };

    private static UniqueModifierBlock RuntimeEvidenceBlock(
        string id,
        string line,
        string canonicalSignature,
        string statId,
        UniqueModifierSemanticLocality sourceLocality = UniqueModifierSemanticLocality.Global,
        IReadOnlyList<string>? matchedTransformations = null)
    {
        var block = EvidenceBlock(id, line, canonicalSignature, statId);
        var sourceFingerprint = new UniqueModifierSemanticFingerprint
        {
            Locality = sourceLocality,
            OrderedStatIds = matchedTransformations is null ? [] : [statId],
            ValueShape = matchedTransformations is null
                ? UniqueModifierSemanticValueShape.Unknown
                : UniqueModifierSemanticValueShape.Scalar,
            Values = matchedTransformations is null
                ? []
                :
                [
                    new UniqueModifierSemanticValue
                    {
                        Index = 0,
                        StatId = statId,
                        Format = "#",
                        Unit = "number",
                        Transformations = matchedTransformations,
                    },
                ],
            EvidenceMethods = ["pob-item-context-v1"],
        };
        return block with
        {
            SourceSemanticFingerprint = sourceFingerprint,
        };
    }

    private static UniqueModifierBlock RuntimeMultiLineEvidenceBlock(
        string id,
        IReadOnlyList<string> lines,
        string statId) => new()
    {
        Id = $"block:{id}",
        Kind = UniqueModifierBlockKind.Unique,
        Lines = lines,
        CanonicalSignatures = ModifierTextSignatureNormalizer.CreateSignature(lines).Lines,
        SourceSemanticFingerprint = new UniqueModifierSemanticFingerprint
        {
            Locality = UniqueModifierSemanticLocality.Local,
            EvidenceMethods = ["pob-item-context-v1"],
        },
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = [$"modifier:{id}"],
            StatIds = [statId],
        },
        SourceObservationIds = ["pob-observation:test"],
    };

    private static UniqueModifierBlock GeneratedEvidenceBlock(
        string id,
        string line,
        string canonicalSignature,
        string statId,
        string candidatePoolMembershipId) => new()
    {
        Id = $"block:{id}",
        Kind = UniqueModifierBlockKind.Unique,
        Lines = [line],
        CanonicalSignatures = [canonicalSignature],
        SourceSemantics = UniqueModifierSourceSemantics.GeneratedCandidate,
        CandidatePoolMembershipIds = [candidatePoolMembershipId],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = [$"modifier:{id}"],
            StatIds = [statId],
        },
        SourceObservationIds = ["generated-observation:test"],
    };

    private static UniqueModifierBlock MultiBlock() => new()
    {
        Id = "block:minions",
        Kind = UniqueModifierBlockKind.Unique,
        Lines =
        [
            "+1 to maximum number of Raised Zombies",
            "+1 to maximum number of Spectres",
        ],
        CanonicalSignatures =
        [
            "+<number> to maximum number of Raised Zombies",
            "+<number> to maximum number of Spectres",
        ],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = ["modifier:minions"],
            StatIds = ["zombie_stat", "spectre_stat"],
        },
        SourceObservationIds = ["pob-observation:test", "pob-observation:test-two"],
    };

    private static ModifierDefinition ReplacementModifier() => new()
    {
        Id = "modifier:foulborn-life",
        GroupId = "group:foulborn-life",
        Name = "Foulborn life",
        GenerationType = ModifierGenerationType.Prefix,
        Domain = "item",
        Stats =
        [
            new ModifierStat
            {
                Index = 0,
                StatId = "foulborn_life",
                MinValue = 10,
                MaxValue = 30,
            },
        ],
    };

    private static StatTranslationDefinition Translation(
        string id,
        string statId,
        string format) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                ValueFormats = ["+#"],
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                FormatLines = [format],
            },
        ],
    };

    private static UniqueFoulbornModifierRelationship Relationship(
        string normalModifierId,
        string foulbornModifierId,
        string normalBlockId) => new()
    {
        Id = "foulborn-relationship:test",
        ItemName = "Test Calling",
        UniqueItemId = "unique:test",
        NormalModifierId = normalModifierId,
        FoulbornModifierId = foulbornModifierId,
        NormalModifierBlockIds = [normalBlockId],
        AppliesToRole = UniqueItemVersionRole.Current,
        SourceObservationId = "pob-foulborn-source:test",
        Status = UniqueFoulbornModifierRelationshipStatus.Exact,
    };

    private static UniqueModifierBlock FoulbornOrdinaryBlock() => new()
    {
        Id = "block:midnight-bargain-minions",
        Kind = UniqueModifierBlockKind.Unique,
        Lines =
        [
            "+1 to maximum number of Raised Zombies",
            "+1 to maximum number of Spectres",
            "+1 to maximum number of Skeletons",
        ],
        CanonicalSignatures =
        [
            "+<number> to maximum number of Raised Zombies",
            "+<number> to maximum number of Spectres",
            "+<number> to maximum number of Skeletons",
        ],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = ["modifier:midnight-bargain-minions"],
            StatIds = ["zombie_stat", "spectre_stat", "skeleton_stat"],
        },
        SourceObservationIds = ["pob-observation:test"],
    };
}
