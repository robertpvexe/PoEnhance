using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeSelectedModifierMapperTests
{
    private readonly PathOfExileTradeSelectedModifierMapper mapper = new();

    [Fact]
    public void Map_NoSelectedModifiersDoesNotRequireCatalog()
    {
        var result = mapper.Map(
            Draft([Modifier("+55 to maximum Life", isSelected: false)]));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Map_PreResolvedExactSelectedModifiersPreservesSelectedOrderAndProviderIds()
    {
        var result = mapper.Map(
            Draft([
                Modifier("+55 to maximum Life", providerStatId: "explicit.stat_life"),
                Modifier("Adds 10 to 20 Fire Damage", providerStatId: "explicit.stat_fire"),
            ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(["explicit.stat_life", "explicit.stat_fire"], result.Filters.Select(filter => filter.StatId));
        Assert.Equal([0, 1], result.Filters.Select(filter => filter.SourceIndex));
        Assert.All(result.Filters, filter => Assert.Empty(filter.ExtractedNumericValues));
    }

    [Fact]
    public void Map_ResolvedScalarBoundsTravelWithTheExactProviderStat()
    {
        var result = mapper.Map(Draft([
            Modifier("52% increased Physical Damage", providerStatId: "explicit.physical") with
            {
                SupportsValueBounds = true,
                RequestedMinimum = 40m,
                RequestedMaximum = 60m,
            },
        ]));

        var filter = Assert.Single(result.Filters);
        Assert.Equal(40m, filter.Minimum);
        Assert.Equal(60m, filter.Maximum);
    }

    [Fact]
    public void Map_NegatedDisplayBoundsAreProjectedPerProviderAtFinalMapping()
    {
        var component = Modifier(
            "20% reduced Attribute Requirements",
            providerStatId: "explicit.attribute-requirements",
            canonicalSignature: "<number>% reduced Attribute Requirements") with
        {
            ProviderCanonicalSignature = "<number>% reduced Attribute Requirements",
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            RequestedMinimum = 10m,
            RequestedMaximum = 20m,
            ValueBoundTranslationHandlers = [["negate"]],
        };

        var result = mapper.Map(
            Draft([component]),
            Catalog(
                "explicit.attribute-requirements",
                "#% increased Attribute Requirements",
                "Explicit"));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal(-20m, filter.Minimum);
        Assert.Equal(-10m, filter.Maximum);
    }

    [Fact]
    public void Map_CanonicalNegatedBoundKeepsProviderMaximumWithoutDoubleNegation()
    {
        var component = Modifier(
            "14% reduced Charges per use",
            providerStatId: "explicit.charges-used",
            canonicalSignature: "<number>% reduced Charges per use") with
        {
            ProviderCanonicalSignature = "<number>% reduced Charges per use",
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [14m],
            CanonicalNumericValues = [-14m],
            RequestedMaximum = -14m,
            DefaultBoundDirection = ModifierBoundDirection.Maximum,
            ValueBoundTranslationHandlers = [["negate"]],
        };

        var result = mapper.Map(
            Draft([component]),
            Catalog("explicit.charges-used", "#% increased Charges per use", "Explicit"));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Null(filter.Minimum);
        Assert.Equal(-14m, filter.Maximum);
    }

    [Fact]
    public void Map_FixedLiteralProviderTextEmitsPresenceOnlyFilter()
    {
        var component = Modifier(
            "Has 3 Sockets",
            providerStatId: "explicit.socket-count",
            canonicalSignature: "Has <number> Sockets") with
        {
            ProviderSearchSignatures = ["Has 1 Socket", "Has <number> Sockets"],
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [3m],
            CanonicalNumericValues = [3m],
            RequestedMinimum = 3m,
        };

        var result = mapper.Map(
            Draft([component]),
            Catalog("explicit.socket-count", "Has 1 Socket", "Explicit"));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);
    }

    [Fact]
    public void Map_FixedNumericIdentityAppliesExactConstraintToEveryParametricAlternative()
    {
        var component = Modifier(
            "Socketed Gems are Supported by Level 10 Test Support",
            providerResolutionStatus: SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            providerStatId: null,
            canonicalSignature: "Socketed Gems are Supported by Level <number> Test Support") with
        {
            ProviderStatText = "Socketed Gems are Supported by Level # Test Support",
            ProviderStatAlternativeIds = ["explicit.test-support-a", "explicit.test-support-b"],
            ProviderCandidateStatIds = ["explicit.test-support-a", "explicit.test-support-b"],
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            RequestedMinimum = null,
            RequestedMaximum = null,
            ObservedNumericValues = [10m],
            CanonicalNumericValues = [10m],
            FixedQueryValue = 10m,
        };
        var draft = Draft([component]);
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry("explicit.test-support-a", "Socketed Gems are Supported by Level # Test Support"),
            Entry("explicit.test-support-b", "Socketed Gems are Supported by Level # Test Support"),
        ]);

        var result = mapper.Map(draft, catalog);

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal(10m, filter.Minimum);
        Assert.Equal(10m, filter.Maximum);
        Assert.Equal(2, filter.Alternatives.Count);
        Assert.All(filter.Alternatives, alternative =>
        {
            Assert.Equal(10m, alternative.Minimum);
            Assert.Equal(10m, alternative.Maximum);
        });

        var query = new PathOfExileTradeQueryBuilder().Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            "Standard",
            result.Filters);
        Assert.True(query.IsSuccess);
        using var document = JsonDocument.Parse(query.SerializedJson!);
        var filters = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray())
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(2, filters.Length);
        Assert.All(filters, serialized =>
        {
            var value = serialized.GetProperty("value");
            Assert.Equal(10m, value.GetProperty("min").GetDecimal());
            Assert.Equal(10m, value.GetProperty("max").GetDecimal());
        });
    }

    [Fact]
    public void Map_FixedPresenceSourceKeepsProviderFilterBoundlessAtFinalMapping()
    {
        var component = Modifier(
            "You can apply an additional Curse",
            providerStatId: "explicit.stat_30642521",
            canonicalSignature: "You can apply an additional Curse") with
        {
            ProviderCanonicalSignature = "You can apply an additional Curse",
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            ProviderFallbackNumericValues = [1m],
        };
        var draft = Draft([component]) with
        {
            Rarity = "Unique",
            DisplayName = "Doedre's Damning",
            ParsedBaseType = "Paua Ring",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "Metadata/Items/Rings/Ring2",
                ResolvedBaseName = "Paua Ring",
            },
        };

        var result = mapper.Map(
            draft,
            Catalog(
                "explicit.stat_30642521",
                "You can apply # additional Curses",
                "Explicit"));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);

        var query = new PathOfExileTradeQueryBuilder().Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            "Standard",
            result.Filters,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Doedre's Damning",
                CanonicalType = "Paua Ring",
            });

        Assert.True(query.IsSuccess);
        using var document = JsonDocument.Parse(query.SerializedJson!);
        var serializedFilter = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal("explicit.stat_30642521", serializedFilter.GetProperty("id").GetString());
        Assert.False(serializedFilter.TryGetProperty("value", out _));
    }

    [Fact]
    public void Map_ProviderOwnedUniqueExact_SerializesOneExactExplicitFilterWithoutGameDataProvenance()
    {
        var unique = Modifier(
            "+69 to maximum Life",
            kind: ParsedModifierKind.Unique,
            providerStatId: "explicit.stat_life",
            canonicalSignature: "+<number> to maximum Life",
            hasGameDataProvenance: false) with
        {
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            StatMappingProof = ModifierStatMappingProofStatus.ProviderExact,
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            RequestedMinimum = 69m,
        };
        var catalog = Catalog("explicit.stat_life", "+# to maximum Life", "Explicit");

        var result = mapper.Map(Draft([unique, unique with { ComponentId = "modifier:1:0" }]), catalog);

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_life", filter.StatId);
        Assert.Equal(69m, filter.Minimum);
        Assert.Equal([0, 1], filter.SourceIndexes);
    }

    [Fact]
    public void Map_ExactRecoveredUniqueWithNonExactProviderResolutionFailsClosed()
    {
        var recovered = new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = "You can apply an additional Curse",
            CanonicalSignature = "You can apply an additional Curse",
            ParsedKind = ParsedModifierKind.Unknown,
            UniqueOrigin = ParsedUniqueModifierOrigin.Unspecified,
            UsesIdentityBoundUniqueRecovery = true,
            RecoveredSourceKind = ParsedModifierKind.Unique,
            RecoveredSourceUniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            UniqueCatalogBlockIds = ["block:curse"],
            UniqueSourceObservationIds = ["observation:curse"],
            ResolvedStatIds = ["curse_stat"],
            IsSearchable = true,
            IsSelected = true,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Ambiguous,
            ProviderCandidateStatIds = ["explicit.curse", "implicit.curse"],
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
        };

        var result = mapper.Map(Draft([recovered]));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_UniqueProviderStatWithoutProviderOwnedProof_IsRejected()
    {
        var unique = Modifier(
            "+69 to maximum Life",
            kind: ParsedModifierKind.Unique,
            providerStatId: "explicit.stat_life",
            canonicalSignature: "+<number> to maximum Life",
            hasGameDataProvenance: false) with
        {
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            IsSearchable = true,
        };

        var result = mapper.Map(
            Draft([unique]),
            Catalog("explicit.stat_life", "+# to maximum Life", "Explicit"));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_ExactFracturedComponent_EmitsOneFracturedStatWithBounds()
    {
        var fracturedVariant = Variant("fractured.stat_life", "Fractured", "fractured", supportsBounds: true);
        var component = Modifier(
            "+84 to maximum Life",
            providerStatId: "fractured.stat_life",
            canonicalSignature: "+<number> to maximum Life") with
        {
            IsFractured = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            RequestedMinimum = 84m,
            FilterVariants = [fracturedVariant],
            SelectedFilterVariantIdentity = fracturedVariant.Identity,
        };

        var result = mapper.Map(
            Draft([component, component with { ComponentId = "modifier:1:0" }]),
            Catalog("fractured.stat_life", "+# to maximum Life", "Fractured"));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("fractured.stat_life", filter.StatId);
        Assert.Equal(84m, filter.Minimum);
        Assert.Equal([0, 1], filter.SourceIndexes);
    }

    [Fact]
    public void Map_ExactEquivalentFracturedComponentEmitsOneLogicalFilterWithAllAlternatives()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "fractured",
                GroupLabel = "Fractured",
                Id = "fractured.suppress.one",
                Text = "+#% chance to Suppress Spell Damage",
                Type = "fractured",
            },
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 1,
                GroupId = "fractured",
                GroupLabel = "Fractured",
                Id = "fractured.suppress.two",
                Text = "+#% chance to Suppress Spell Damage",
                Type = "fractured",
            },
        ]);
        var candidates = new[]
        {
            PathOfExileTradeStatCandidateClassifier.ToCandidate(catalog.Entries[0]),
            PathOfExileTradeStatCandidateClassifier.ToCandidate(catalog.Entries[1]),
        };
        var identity = PathOfExileTradeModifierVariantResolver.IdentityFor(candidates);
        var component = Modifier(
            "+12% chance to Suppress Spell Damage",
            providerResolutionStatus: SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            providerStatId: null,
            canonicalSignature: "+<number>% chance to Suppress Spell Damage") with
        {
            IsFractured = true,
            ProviderStatText = "+#% chance to Suppress Spell Damage",
            ProviderStatAlternativeIds = ["fractured.suppress.one", "fractured.suppress.two"],
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            RequestedMinimum = 12m,
            FilterVariants =
            [
                new SearchFilterVariant
                {
                    Identity = identity,
                    Label = "Fractured",
                    Description = "Suppress Spell Damage",
                    ProviderKind = "fractured",
                    ProviderAlternativeCount = 2,
                    SupportsValueBounds = true,
                },
            ],
            SelectedFilterVariantIdentity = identity,
        };

        var result = mapper.Map(Draft([component]), catalog);

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("fractured.suppress.one", filter.StatId);
        Assert.Equal(
            ["fractured.suppress.one", "fractured.suppress.two"],
            filter.Alternatives.Select(alternative => alternative.StatId));
        Assert.All(filter.Alternatives, alternative => Assert.Equal(12m, alternative.Minimum));
    }

    [Fact]
    public void Map_ApproximateFracturedSourceWithExplicitRepresentation_EmitsOnlyExplicit()
    {
        var explicitVariant = Variant("explicit.stat_life", "Explicit", "explicit", supportsBounds: true);
        var component = Modifier(
            "+84 to maximum Life",
            providerStatId: "explicit.stat_life",
            canonicalSignature: "+<number> to maximum Life") with
        {
            IsFractured = true,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Approximate,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            RequestedMinimum = 84m,
            FilterVariants = [explicitVariant],
            SelectedFilterVariantIdentity = explicitVariant.Identity,
        };

        var result = mapper.Map(
            Draft([component]),
            Catalog("explicit.stat_life", "+# to maximum Life", "Explicit"));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_life", filter.StatId);
        Assert.Equal(84m, filter.Minimum);
        Assert.Equal([0], filter.SourceIndexes);
        Assert.DoesNotContain(result.Filters, candidate =>
            candidate.StatId.StartsWith("fractured.", StringComparison.Ordinal));
    }

    [Fact]
    public void Map_ExactFracturedSourceCannotUseOrdinaryExplicitRepresentation()
    {
        var explicitVariant = Variant("explicit.stat_life", "Explicit", "explicit", supportsBounds: true);
        var component = Modifier(
            "+84 to maximum Life",
            providerStatId: "explicit.stat_life",
            canonicalSignature: "+<number> to maximum Life") with
        {
            IsFractured = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            RequestedMinimum = 84m,
            FilterVariants = [explicitVariant],
            SelectedFilterVariantIdentity = explicitVariant.Identity,
        };

        var result = mapper.Map(
            Draft([component]),
            Catalog("explicit.stat_life", "+# to maximum Life", "Explicit"));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_ManualExplicitRequestFromFracturedSourceUsesExplicitAndPreservesCoverage()
    {
        var explicitVariant = Variant("explicit.stat_life", "Explicit", "explicit", supportsBounds: true);
        var component = Modifier(
            "+84 to maximum Life",
            providerStatId: "explicit.stat_life",
            canonicalSignature: "+<number> to maximum Life") with
        {
            IsFractured = true,
            RequestedFilterVariantIdentity = explicitVariant.Identity,
            RequestedFilterVariantKind = explicitVariant.ProviderKind,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            RequestedMinimum = 84m,
            FilterVariants = [explicitVariant],
            SelectedFilterVariantIdentity = explicitVariant.Identity,
        };

        var result = mapper.Map(
            Draft([component]),
            Catalog("explicit.stat_life", "+# to maximum Life", "Explicit"));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_life", filter.StatId);
        Assert.Equal([0], filter.SourceIndexes);
        Assert.Equal(84m, filter.Minimum);
    }

    [Fact]
    public void Map_VeiledPrefixAndSuffixPresence_CollapseToOneUnboundedProviderFilter()
    {
        var veiledVariant = Variant("veiled.general", "Veiled", "veiled", supportsBounds: false);
        var suffix = Modifier(
            "Veiled Suffix",
            providerStatId: "veiled.general",
            canonicalSignature: "Veiled Suffix",
            hasGameDataProvenance: false) with
        {
            IsVeiled = true,
            StatMappingProof = ModifierStatMappingProofStatus.ProviderExact,
            IsSearchable = true,
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            FilterVariants = [veiledVariant],
            SelectedFilterVariantIdentity = veiledVariant.Identity,
        };
        var prefix = suffix with
        {
            ComponentId = "modifier:1:0",
            OriginalText = "Veiled Prefix",
            CanonicalSignature = "Veiled Prefix",
            ParsedKind = ParsedModifierKind.Prefix,
        };

        var result = mapper.Map(
            Draft([suffix, prefix]),
            Catalog("veiled.general", "Veiled", "Veiled"));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("veiled.general", filter.StatId);
        Assert.Equal([0, 1], filter.SourceIndexes);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);
    }

    [Theory]
    [InlineData(true, false, "explicit.stat_life", "Explicit")]
    [InlineData(true, false, "pseudo.total_life", "Pseudo")]
    [InlineData(false, true, "explicit.stat_life", "Explicit")]
    public void Map_SpecialProvenanceWithWrongProviderDomain_IsRejected(
        bool isFractured,
        bool isVeiled,
        string providerStatId,
        string providerGroup)
    {
        var component = Modifier(
            "+84 to maximum Life",
            providerStatId: providerStatId,
            canonicalSignature: "+<number> to maximum Life") with
        {
            IsFractured = isFractured,
            IsVeiled = isVeiled,
        };

        var result = mapper.Map(
            Draft([component]),
            Catalog(providerStatId, "+# to maximum Life", providerGroup));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_ForgedBroadPseudoForReviewedLocalDisplayedPropertyIsRejected()
    {
        var component = Modifier(
            "20% increased Attack Speed",
            locality: ModifierLocality.Local,
            statIds: ["attack_speed_+%"],
            providerStatId: "pseudo.pseudo_total_attack_speed",
            canonicalSignature: "<number>% increased Attack Speed") with
        {
            ReviewedItemPropertySemantic = LocalDisplayedSemantic(),
            SupportsValueBounds = true,
            RequestedMinimum = 20m,
        };
        var catalog = Catalog(
            "pseudo.pseudo_total_attack_speed",
            "+#% total Attack Speed",
            "Pseudo");

        var result = mapper.Map(Draft([component]), catalog);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.UnsafeLocalDisplayedProviderScope,
            diagnostic.Code);
        Assert.Equal(
            PathOfExileTradeProviderLocalityCompatibility.LocalDisplayedScopeUnproven,
            diagnostic.SourceCode);
    }

    [Fact]
    public void Map_ValidLocalProviderIdentityForReviewedPropertySerializesExactlyOnce()
    {
        var component = Modifier(
            "Adds 10 to 20 Lightning Damage",
            locality: ModifierLocality.Local,
            statIds: ["local_minimum_added_lightning_damage", "local_maximum_added_lightning_damage"],
            providerStatId: "explicit.stat_3336890334",
            canonicalSignature: "Adds <number> to <number> Lightning Damage") with
        {
            ReviewedItemPropertySemantic = LocalDisplayedSemantic(),
            SupportsValueBounds = true,
            RequestedMinimum = 15m,
        };
        var catalog = Catalog(
            "explicit.stat_3336890334",
            "Adds # to # Lightning Damage (Local)",
            "Explicit");

        var result = mapper.Map(Draft([component]), catalog);

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_3336890334", filter.StatId);
        Assert.Equal(15m, filter.Minimum);
    }

    [Fact]
    public void Map_AggregatedPhysicalDamageEmitsOnceAndHybridAccuracyOnlyWhenSelected()
    {
        var physical = Modifier(
            "91% increased Physical Damage",
            providerStatId: "explicit.physical",
            canonicalSignature: "<number>% increased Physical Damage") with
        {
            SupportsValueBounds = true,
            RequestedMinimum = 91m,
        };
        var accuracy = Modifier(
            "+93 to Accuracy Rating",
            providerStatId: "explicit.accuracy.local",
            canonicalSignature: "+<number> to Accuracy Rating") with
        {
            IsSelected = false,
            SupportsValueBounds = true,
            RequestedMinimum = 93m,
        };

        var physicalOnly = mapper.Map(Draft([physical, accuracy]));
        var bothSelected = mapper.Map(Draft([physical, accuracy with { IsSelected = true }]));

        var physicalFilter = Assert.Single(physicalOnly.Filters);
        Assert.Equal("explicit.physical", physicalFilter.StatId);
        Assert.Equal(91m, physicalFilter.Minimum);
        Assert.DoesNotContain(physicalOnly.Filters, filter => filter.StatId == "explicit.accuracy.local");
        Assert.Equal(
            ["explicit.physical", "explicit.accuracy.local"],
            bothSelected.Filters.Select(filter => filter.StatId));
        Assert.Equal(93m, bothSelected.Filters[1].Minimum);
    }

    [Theory]
    [InlineData(
        SearchComponentProviderResolutionStatus.Ambiguous,
        PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorSourceIdentityAmbiguous)]
    [InlineData(
        SearchComponentProviderResolutionStatus.Unsupported,
        PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorSourceIdentityUnavailable)]
    public void Map_SelectedOpaqueContributorWithoutExactCoverageBlocksInsteadOfFallingBack(
        SearchComponentProviderResolutionStatus status,
        string expectedCode)
    {
        var pseudo = new SearchFilterVariant
        {
            Identity = "pseudo-parent",
            Label = "Pseudo",
            Description = "#% increased total Physical Damage",
            ProviderKind = "pseudo",
            SupportsContributorComposition = true,
            SupportsValueBounds = true,
        };
        var source = new SearchComponentSourceProvenance
        {
            ComponentId = "modifier:0:0",
            OriginalText = "30% increased Physical Damage",
            CanonicalSignature = "<number>% increased Physical Damage",
            ParsedKind = ParsedModifierKind.Prefix,
            ProviderDomain = "Explicit",
            ResolvedModifierId = "physical-hybrid",
            ResolvedStatIds = ["local_physical_damage_+%"],
            CanonicalNumericValues = [30m],
        };
        var parent = Modifier(
            "146% increased Physical Damage",
            providerStatId: "pseudo.total-physical",
            canonicalSignature: "<number>% increased Physical Damage") with
        {
            SupportsValueBounds = true,
            RequestedMinimum = 30m,
            FilterVariants = [pseudo],
            SelectedFilterVariantIdentity = pseudo.Identity,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Contributors =
            [
                new SearchComponentContributor
                {
                    ContributorId = "contributor:0",
                    Source = source,
                    DisplayText = source.OriginalText,
                    IsSelected = true,
                    SupportsValueBounds = true,
                    RequestedMinimum = 30m,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    ProviderResolutionStatus = status,
                    ProviderDiagnosticMessage = "Contributor coverage is not exact.",
                },
            ],
        };

        var result = mapper.Map(Draft([parent]), ContributorCatalog());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData(9, 22, 31)]
    [InlineData(10, 25, 35)]
    public void Map_StunRecoveryChildrenSharingSourceIdentityAggregateIntoOneAndFilter(
        int firstMinimum,
        int secondMinimum,
        int expectedMinimum)
    {
        var parent = ContributorAggregate(
            pseudo: true,
            parentMinimum: expectedMinimum,
            firstMinimum,
            secondMinimum);

        var result = mapper.Map(Draft([parent]), ContributorCatalog());

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["pseudo.total-stun-recovery", "explicit.stun-recovery"],
            result.Filters.Select(filter => filter.StatId));
        Assert.Equal([expectedMinimum, expectedMinimum], result.Filters.Select(filter => filter.Minimum));
        Assert.Equal(2, result.Filters.Select(filter => filter.StatId).Distinct(StringComparer.Ordinal).Count());

        var draft = Draft([parent]);
        var build = new PathOfExileTradeQueryBuilder().Build(
            draft,
            new TradeSearchDraftValidator().Validate(draft),
            "Mirage",
            result.Filters);
        Assert.True(build.IsSuccess);
        using var document = JsonDocument.Parse(build.SerializedJson!);
        var group = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray());
        Assert.Equal("and", group.GetProperty("type").GetString());
        Assert.Equal(2, group.GetProperty("filters").GetArrayLength());
    }

    [Fact]
    public void Map_NonPseudoParentIgnoresSelectedChildrenIncludingMissingSourceIdentity()
    {
        var resolvedParent = ContributorAggregate(
            pseudo: false,
            parentMinimum: 31m,
            firstMinimum: 9m,
            secondMinimum: 22m);
        var parent = resolvedParent with
        {
            Contributors = resolvedParent.Contributors
                .Select(contributor => contributor with
                {
                    ProviderResolutionStatus = SearchComponentProviderResolutionStatus.NotFound,
                    ProviderIdentity = null,
                    ProviderDiagnosticMessage = "Missing retained source identity.",
                })
                .ToArray(),
        };

        var result = mapper.Map(Draft([parent]));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("fractured.stun-recovery", filter.StatId);
        Assert.Equal(31m, filter.Minimum);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Map_ExactOpaqueContributorMissingFromCurrentCatalogFailsWithoutFallback()
    {
        var resolvedParent = ContributorAggregate(
            pseudo: true,
            parentMinimum: 9m,
            firstMinimum: 9m,
            secondMinimum: 22m);
        var parent = resolvedParent with
        {
            Contributors = resolvedParent.Contributors
                .Select((contributor, index) => index == 0
                    ? contributor with
                    {
                        IsSelected = true,
                        ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
                        ProviderIdentity = "variant-missing-from-catalog",
                    }
                    : contributor with { IsSelected = false })
                .ToArray(),
        };

        var result = mapper.Map(Draft([parent]), ContributorCatalog());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorSourceIdentityUnavailable,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_SharedProviderStatWithIncompatibleBoundsFailsExplicitly()
    {
        var result = mapper.Map(Draft([
            Modifier("52% increased Physical Damage", providerStatId: "explicit.physical") with { SupportsValueBounds = true, RequestedMinimum = 40m },
            Modifier("39% increased Physical Damage", providerStatId: "explicit.physical") with { SupportsValueBounds = true, RequestedMinimum = 50m },
        ]));

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeSelectedModifierMappingDiagnosticCodes.IncompatibleBounds, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_SelectedComponentsSharingPresenceStatProduceOneFilterWithBothSources()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "52% increased Physical Damage",
                    providerStatId: "explicit.physical",
                    canonicalSignature: "<number>% increased Physical Damage"),
                Modifier(
                    "39% increased Physical Damage",
                    providerStatId: "explicit.physical",
                    canonicalSignature: "<number>% increased Physical Damage"),
            ]));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.physical", filter.StatId);
        Assert.Equal(0, filter.SourceIndex);
        Assert.Equal([0, 1], filter.SourceIndexes);
        Assert.Equal("#% increased Physical Damage", filter.NormalizedItemTemplate);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Map_SharedPresenceFilterIsIndependentOfSelectionSequence()
    {
        var components = new[]
        {
            Modifier("52% increased Physical Damage", providerStatId: "explicit.physical"),
            Modifier("39% increased Physical Damage", providerStatId: "explicit.physical"),
        };

        var selectedSecondThenFirst = mapper.Map(Draft([
            components[0] with { IsSelected = false },
            components[1],
        ]));
        selectedSecondThenFirst = mapper.Map(Draft(components));

        var selectedFirstThenSecond = mapper.Map(Draft([
            components[0],
            components[1] with { IsSelected = false },
        ]));
        selectedFirstThenSecond = mapper.Map(Draft(components));

        var secondThenFirstFilter = Assert.Single(selectedSecondThenFirst.Filters);
        var firstThenSecondFilter = Assert.Single(selectedFirstThenSecond.Filters);
        Assert.Equal(secondThenFirstFilter.StatId, firstThenSecondFilter.StatId);
        Assert.Equal(secondThenFirstFilter.SourceIndex, firstThenSecondFilter.SourceIndex);
        Assert.Equal(secondThenFirstFilter.SourceIndexes, firstThenSecondFilter.SourceIndexes);
        Assert.Equal([0, 1], secondThenFirstFilter.SourceIndexes);
    }

    [Fact]
    public void Map_PreResolvedExactSelectedModifierConvertsCanonicalSignatureToProviderTemplate()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "+55 to maximum Life",
                    providerStatId: "explicit.stat_life",
                    canonicalSignature: "+<number> to maximum Life"),
            ]));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_life", filter.StatId);
        Assert.Equal("+# to maximum Life", filter.NormalizedItemTemplate);
        Assert.Empty(filter.ExtractedNumericValues);
    }

    [Fact]
    public void Map_PreResolvedAdvancedRangeSelectedModifierSerializesPresenceOnly()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "Adds 46(41-55) to 81(81-95) Cold Damage",
                    providerStatId: "explicit.stat_cold",
                    canonicalSignature: "Adds <number> to <number> Cold Damage"),
            ]));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_cold", filter.StatId);
        Assert.Equal("Adds # to # Cold Damage", filter.NormalizedItemTemplate);
        Assert.Empty(filter.ExtractedNumericValues);
    }

    [Fact]
    public void Map_PreResolvedRangerBowFireDamageUsesResolvedOfficialLocalStatId()
    {
        var result = mapper.Map(
            Draft(
                [
                    Modifier(
                        "Adds 70(63-85) to 139(128-148) Fire Damage",
                        providerStatId: "explicit.stat_709508406",
                        canonicalSignature: "Adds <number> to <number> Fire Damage",
                        locality: ModifierLocality.Local,
                        statIds: ["local_minimum_added_fire_damage", "local_maximum_added_fire_damage"]),
                ],
                itemClass: "Bows",
                parsedBaseType: "Ranger Bow"));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_709508406", filter.StatId);
        Assert.Equal("Adds # to # Fire Damage", filter.NormalizedItemTemplate);
        Assert.Empty(filter.ExtractedNumericValues);
        Assert.Empty(result.Traces);
    }

    [Fact]
    public void Map_UnselectedModifiersAreNotSerialized()
    {
        var result = mapper.Map(
            Draft([
                Modifier("+55 to maximum Life", isSelected: false, providerStatId: "explicit.stat_life"),
                Modifier("+21 to maximum Life", providerStatId: "explicit.stat_life"),
            ]));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal(1, filter.SourceIndex);
        Assert.Equal("explicit.stat_life", filter.StatId);
    }

    [Fact]
    public void Map_PreResolvedAmbiguousSelectedModifierFailsWholeMappingWithoutChoosingCandidate()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "+55 to maximum Life",
                    providerResolutionStatus: SearchComponentProviderResolutionStatus.Ambiguous,
                    providerStatId: null,
                    providerCandidateStatIds: ["explicit.stat_life_one", "explicit.stat_life_two"],
                    providerDiagnosticCode: PathOfExileTradeStatMatchDiagnosticCodes.AmbiguousCandidates),
            ]));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous, diagnostic.Code);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.AmbiguousCandidates, diagnostic.SourceCode);
    }

    [Fact]
    public void Map_PreResolvedUnknownLocalityAmbiguityPreservesSourceDiagnostic()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "Adds 10 to 20 Fire Damage",
                    providerResolutionStatus: SearchComponentProviderResolutionStatus.Ambiguous,
                    providerStatId: null,
                    providerCandidateStatIds: ["explicit.global_fire", "explicit.local_fire"],
                    providerDiagnosticCode: PathOfExileTradeStatMatchDiagnosticCodes.LocalityAmbiguous),
            ]));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous, diagnostic.Code);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.LocalityAmbiguous, diagnostic.SourceCode);
    }

    [Fact]
    public void Map_PreResolvedNotFoundSelectedModifierFailsWholeMapping()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "+55 to maximum Life",
                    providerResolutionStatus: SearchComponentProviderResolutionStatus.NotFound,
                    providerStatId: null,
                    providerDiagnosticCode: PathOfExileTradeStatMatchDiagnosticCodes.NoCandidate),
            ]));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound, diagnostic.Code);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.NoCandidate, diagnostic.SourceCode);
    }

    [Fact]
    public void Map_PreResolvedBaseGuaranteedSelectedModifierEmitsNoProviderFilter()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "Cannot roll Caster Modifiers",
                    kind: ParsedModifierKind.Implicit,
                    providerResolutionStatus: SearchComponentProviderResolutionStatus.BaseGuaranteed,
                    providerStatId: null),
            ]));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Map_PreResolvedKindMismatchFailsWholeMapping()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "+55 to maximum Life",
                    kind: ParsedModifierKind.Implicit,
                    providerResolutionStatus: SearchComponentProviderResolutionStatus.NotFound,
                    providerStatId: null,
                    providerDiagnosticCode: PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch),
            ]));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch, diagnostic.Code);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch, diagnostic.SourceCode);
    }

    [Fact]
    public void Map_SelectedModifierWithoutGameDataProvenanceFailsBeforeProviderSerialization()
    {
        var result = mapper.Map(
            Draft([Modifier("+55 to maximum Life", hasGameDataProvenance: false)]));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_SelectedImplicitWithoutExactGameDataProvenanceFailsBeforeProviderSerialization()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "+24 to maximum Energy Shield",
                    kind: ParsedModifierKind.Implicit,
                    hasGameDataProvenance: false,
                    providerResolutionStatus: SearchComponentProviderResolutionStatus.Exact,
                    providerStatId: "implicit.synthesis.energy-shield") with
                {
                    ImplicitOrigin = ParsedImplicitModifierOrigin.Synthesis,
                },
            ]));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_SelectedModifierWithoutProviderResolutionFailsWholeMapping()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "+55 to maximum Life",
                    providerResolutionStatus: SearchComponentProviderResolutionStatus.NotResolved,
                    providerStatId: null),
            ]));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.InvalidInput,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Map_PreResolvedExactModifierWithoutProviderStatIdFailsWholeMapping()
    {
        var result = mapper.Map(
            Draft([
                Modifier(
                    "+55 to maximum Life",
                    providerResolutionStatus: SearchComponentProviderResolutionStatus.Exact,
                    providerStatId: null),
            ]));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.InvalidInput,
            Assert.Single(result.Diagnostics).Code);
    }

    private static ResolvedSearchComponent Modifier(
        string originalText,
        bool isSelected = true,
        ParsedModifierKind kind = ParsedModifierKind.Prefix,
        ModifierLocality locality = ModifierLocality.Unknown,
        IReadOnlyList<string>? statIds = null,
        SearchComponentProviderResolutionStatus providerResolutionStatus =
            SearchComponentProviderResolutionStatus.Exact,
        string? providerStatId = "explicit.stat_test",
        IReadOnlyList<string>? providerCandidateStatIds = null,
        string? providerDiagnosticCode = null,
        string? canonicalSignature = null,
        bool hasGameDataProvenance = true)
    {
        var resolvedStatIds = statIds ?? ["stat.test"];
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = originalText,
            CanonicalSignature = canonicalSignature ??
                PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(originalText).NormalizedTemplate,
            ParsedKind = kind,
            Locality = locality,
            ResolutionStatus = hasGameDataProvenance
                ? ModifierCandidateResolutionStatus.Exact
                : null,
            ResolvedModifierId = hasGameDataProvenance ? "mod.test" : null,
            ResolvedStatIds = hasGameDataProvenance ? resolvedStatIds : [],
            IsSearchable = hasGameDataProvenance,
            IsSelected = isSelected,
            ProviderResolutionStatus = providerResolutionStatus,
            ProviderStatId = providerStatId,
            ProviderStatText = providerStatId is null ? null : originalText,
            ProviderCandidateStatIds = providerCandidateStatIds ?? [],
            ProviderDiagnosticCode = providerDiagnosticCode,
        };
    }

    private static ResolvedSearchComponent ContributorAggregate(
        bool pseudo,
        decimal parentMinimum,
        decimal firstMinimum,
        decimal secondMinimum)
    {
        var parentVariant = new SearchFilterVariant
        {
            Identity = pseudo ? "parent-pseudo" : "parent-fractured",
            Label = pseudo ? "Pseudo" : "Fractured",
            Description = "#% increased Stun and Block Recovery",
            ProviderKind = pseudo ? "pseudo" : "fractured",
            SupportsContributorComposition = pseudo,
            SupportsValueBounds = true,
        };

        SearchComponentContributor Child(string id, decimal minimum) => new()
        {
            ContributorId = id,
            Source = new SearchComponentSourceProvenance
            {
                ComponentId = id,
                OriginalText = $"{minimum}% increased Stun and Block Recovery",
                CanonicalSignature = "<number>% increased Stun and Block Recovery",
                ParsedKind = ParsedModifierKind.Suffix,
                Locality = ModifierLocality.Global,
                ProviderDomain = "Explicit",
                ResolvedModifierId = id,
                ResolvedStatIds = ["stun_and_block_recovery_+%"],
                CanonicalNumericValues = [minimum],
                ValueBoundShape = ModifierBoundShape.Scalar,
                TranslationHandlers = [[]],
                TranslationIdentity = "identity:stun-recovery",
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
                ProviderIdentity = PathOfExileTradeProviderIdentity.Create("explicit.stun-recovery"),
            },
            DisplayText = $"{minimum}% increased Stun and Block Recovery",
            IsSelected = true,
            RequestedMinimum = minimum,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderIdentity = PathOfExileTradeProviderIdentity.Create("explicit.stun-recovery"),
        };

        return Modifier(
            $"{firstMinimum + secondMinimum}% increased Stun and Block Recovery",
            providerStatId: pseudo ? "pseudo.total-stun-recovery" : "fractured.stun-recovery",
            canonicalSignature: "<number>% increased Stun and Block Recovery") with
        {
            RequestedMinimum = parentMinimum,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            CanonicalNumericValues = [firstMinimum + secondMinimum],
            FilterVariants = [parentVariant],
            SelectedFilterVariantIdentity = parentVariant.Identity,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Contributors =
            [
                Child("stun-source-1", firstMinimum),
                Child("stun-source-2", secondMinimum),
            ],
        };
    }

    private static PathOfExileTradeStatCatalog ContributorCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.stun-recovery",
                Text = "#% increased Stun and Block Recovery",
                Type = "explicit",
            },
        ]);
    }

    private static SearchFilterVariant Variant(
        string statId,
        string label,
        string providerKind,
        bool supportsBounds)
    {
        return new SearchFilterVariant
        {
            Identity = PathOfExileTradeProviderIdentity.Create(statId),
            Label = label,
            Description = label,
            ProviderKind = providerKind,
            SupportsValueBounds = supportsBounds,
        };
    }

    private static PathOfExileTradeStatCatalog Catalog(string id, string text, string kind) => new(
    [
        new PathOfExileTradeStatEntry
        {
            ProviderOrder = 0,
            GroupId = kind.ToLowerInvariant(),
            GroupLabel = kind,
            Id = id,
            Text = text,
            Type = kind.ToLowerInvariant(),
        },
    ]);

    private static PathOfExileTradeStatEntry Entry(string id, string text) => new()
    {
        ProviderOrder = 0,
        GroupId = "explicit",
        GroupLabel = "Explicit",
        Id = id,
        Text = text,
        Type = "explicit",
    };

    private static ItemPropertySemanticDescriptor LocalDisplayedSemantic() => new()
    {
        Id = "reviewed.local-displayed",
        Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
    };

    private static TradeSearchDraft Draft(
        IReadOnlyList<ResolvedSearchComponent> modifiers,
        string itemClass = "Body Armours",
        string parsedBaseType = "Titan Plate")
    {
        return new TradeSearchDraft
        {
            ItemClass = itemClass,
            Rarity = "Rare",
            DisplayName = "Test Item",
            ParsedBaseType = parsedBaseType,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.test",
                ResolvedBaseName = parsedBaseType,
            },
            ModifierFilters = modifiers,
        };
    }
}
