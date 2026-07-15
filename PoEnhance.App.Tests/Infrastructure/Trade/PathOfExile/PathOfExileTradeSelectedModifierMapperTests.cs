using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

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
