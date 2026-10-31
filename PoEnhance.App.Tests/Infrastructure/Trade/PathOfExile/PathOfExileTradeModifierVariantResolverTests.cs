using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeModifierVariantResolverTests
{
    [Fact]
    public void Apply_OrdinaryExplicitScalarDefaultsToExplicit()
    {
        var catalog = AttackSpeedCatalog();
        var source = Candidate(catalog, "explicit.stat_210067635");

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component(),
            catalog,
            source);

        Assert.Equal("Explicit", Selected(result).Label);
        Assert.Equal("explicit.stat_210067635", result.ProviderStatId);
        Assert.Contains(result.FilterVariants, option =>
            option.Label == "Pseudo" &&
            option.Description == "+#% total Attack Speed");
    }

    [Fact]
    public void Apply_CraftedScalarDefaultsToCraftedAndOffersOnlyProvenDistinctKinds()
    {
        var catalog = AttackSpeedCatalog();
        var source = Candidate(catalog, "crafted.stat_210067635");

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component(isCrafted: true),
            catalog,
            source);

        Assert.Equal("Crafted", Selected(result).Label);
        Assert.Equal("crafted.stat_210067635", result.ProviderStatId);
        Assert.Equal(["Crafted", "Explicit", "Pseudo"], result.FilterVariants
            .Select(option => option.Label)
            .OrderBy(label => label, StringComparer.Ordinal));
        Assert.Equal(
            result.FilterVariants.Count,
            result.FilterVariants.Select(option => option.Label).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(result.FilterVariants, option =>
            option.Label is "Implicit" or "Fractured" or "Enchant");
        Assert.DoesNotContain(result.FilterVariants, option =>
            option.Description.Contains("Cast Speed", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_DeduplicatesSharedProviderIdentityIncludingVeiledAndCraftedMetadata()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "crafted.shared", "#% increased Attack Speed (Local)", "Crafted"),
            Entry(1, "crafted.shared", "#% increased Attack Speed (Local)", "Veiled"),
            Entry(2, "crafted.shared", "#% increased Attack Speed (Local)", "Essence"),
            Entry(3, "explicit.stat_210067635", "#% increased Attack Speed (Local)", "Explicit"),
        ]);

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component(isCrafted: true),
            catalog,
            Candidate(catalog, "crafted.shared"));

        Assert.Equal(2, result.FilterVariants.Count);
        Assert.Single(result.FilterVariants, option => option.Identity ==
            PathOfExileTradeModifierVariantResolver.IdentityFor("crafted.shared"));
        Assert.Equal(2, result.FilterVariants.Select(option => option.Identity).Distinct().Count());
        Assert.DoesNotContain(result.FilterVariants, option => option.Label is "Veiled" or "Essence");
    }

    [Fact]
    public void Apply_CompatibleVariantPreservesBoundsAndChangesExactlyOneResolvedProviderIdentity()
    {
        var catalog = AttackSpeedCatalog();
        var pseudoIdentity = PathOfExileTradeModifierVariantResolver.IdentityFor("pseudo.pseudo_total_attack_speed");
        var component = Component(isCrafted: true) with
        {
            RequestedMinimum = 20.125m,
            RequestedMaximum = 27.875m,
            SelectedFilterVariantIdentity = pseudoIdentity,
        };

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "crafted.stat_210067635"));

        Assert.Equal("pseudo.pseudo_total_attack_speed", result.ProviderStatId);
        Assert.Equal(pseudoIdentity, result.SelectedFilterVariantIdentity);
        Assert.True(result.SupportsValueBounds);
        Assert.Equal(20.125m, result.RequestedMinimum);
        Assert.Equal(27.875m, result.RequestedMaximum);
    }

    [Fact]
    public void Apply_ExplicitAttackSpeedCanSelectTheSameOfficialPseudoIdentity()
    {
        var catalog = AttackSpeedCatalog();
        var pseudoIdentity = PathOfExileTradeModifierVariantResolver.IdentityFor(
            "pseudo.pseudo_total_attack_speed");

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component() with { SelectedFilterVariantIdentity = pseudoIdentity },
            catalog,
            Candidate(catalog, "explicit.stat_210067635"));

        Assert.Equal("Pseudo", Selected(result).Label);
        Assert.Equal("pseudo.pseudo_total_attack_speed", result.ProviderStatId);
        Assert.Equal(20m, result.RequestedMinimum);
    }

    [Fact]
    public void Apply_SelectedPseudoReResolutionRetainsExactOptionsAndPseudoSelection()
    {
        var catalog = AttackSpeedCatalog();
        var pseudoIdentity = PathOfExileTradeModifierVariantResolver.IdentityFor(
            "pseudo.pseudo_total_attack_speed");
        var first = PathOfExileTradeModifierVariantResolver.Apply(
            Component(isCrafted: true) with { SelectedFilterVariantIdentity = pseudoIdentity },
            catalog,
            Candidate(catalog, "crafted.stat_210067635"));

        var repeated = PathOfExileTradeModifierVariantResolver.Apply(
            first,
            catalog,
            Candidate(catalog, "pseudo.pseudo_total_attack_speed"));

        Assert.Equal("Pseudo", Selected(repeated).Label);
        Assert.Equal("pseudo.pseudo_total_attack_speed", repeated.ProviderStatId);
        Assert.Contains(repeated.FilterVariants, option => option.Label == "Explicit");
        Assert.Contains(repeated.FilterVariants, option => option.Label == "Crafted");
        Assert.Contains(repeated.FilterVariants, option => option.Label == "Pseudo");
        Assert.Equal(20m, repeated.RequestedMinimum);
    }

    [Fact]
    public void Apply_DistinctSourceDomainsDeduplicateTheSamePseudoProviderIdentity()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.attack-speed", "#% increased Attack Speed (Local)", "Explicit"),
            Entry(1, "crafted.attack-speed", "#% increased Attack Speed (Local)", "Crafted"),
            Entry(2, "pseudo.total-attack-speed", "+#% total Attack Speed", "Pseudo"),
            Entry(3, "pseudo.total-attack-speed", "+#% total Attack Speed", "Pseudo"),
        ]);

        var explicitResult = PathOfExileTradeModifierVariantResolver.Apply(
            Component(),
            catalog,
            Candidate(catalog, "explicit.attack-speed"));
        var craftedResult = PathOfExileTradeModifierVariantResolver.Apply(
            Component(isCrafted: true),
            catalog,
            Candidate(catalog, "crafted.attack-speed"));

        var explicitPseudo = Assert.Single(explicitResult.FilterVariants, option => option.Label == "Pseudo");
        var craftedPseudo = Assert.Single(craftedResult.FilterVariants, option => option.Label == "Pseudo");
        Assert.Equal(explicitPseudo.Identity, craftedPseudo.Identity);
        Assert.Equal(
            PathOfExileTradeModifierVariantResolver.IdentityFor("pseudo.total-attack-speed"),
            explicitPseudo.Identity);
    }

    [Fact]
    public void Apply_PseudoReResolutionDoesNotTreatUnmarkedAsGlobalAndRejectsUnprovenKinds()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "crafted.local", "#% increased Attack Speed (Local)", "Crafted"),
            Entry(1, "crafted.global", "#% increased Attack Speed", "Crafted"),
            Entry(2, "explicit.local", "#% increased Attack Speed (Local)", "Explicit"),
            Entry(3, "explicit.global", "#% increased Attack Speed", "Explicit"),
            Entry(4, "implicit.local", "#% increased Attack Speed (Local)", "Implicit"),
            Entry(5, "implicit.global", "#% increased Attack Speed", "Implicit"),
            Entry(6, "fractured.local", "#% increased Attack Speed (Local)", "Fractured"),
            Entry(7, "fractured.global", "#% increased Attack Speed", "Fractured"),
            Entry(8, "enchant.local", "#% increased Attack Speed (Local)", "Enchant"),
            Entry(9, "scourge.global", "#% increased Attack Speed", "Scourge"),
            Entry(10, "pseudo.total", "+#% total Attack Speed", "Pseudo"),
        ]);
        var pseudoIdentity = PathOfExileTradeModifierVariantResolver.IdentityFor("pseudo.total");
        var first = PathOfExileTradeModifierVariantResolver.Apply(
            Component(isCrafted: true) with { SelectedFilterVariantIdentity = pseudoIdentity },
            catalog,
            Candidate(catalog, "crafted.local"));
        var repeated = PathOfExileTradeModifierVariantResolver.Apply(
            first,
            catalog,
            Candidate(catalog, "pseudo.total"));
        var audit = PathOfExileTradeModifierVariantResolver.DiscoverForAudit(
            repeated,
            catalog,
            Candidate(catalog, "pseudo.total"));

        Assert.Equal(["Crafted", "Explicit", "Pseudo"], repeated.FilterVariants
            .Select(option => option.Label)
            .OrderBy(label => label, StringComparer.Ordinal));
        Assert.Equal("Pseudo", Selected(repeated).Label);
        Assert.Equal(
            repeated.FilterVariants.Count,
            repeated.FilterVariants.Select(option => option.Label).Distinct(StringComparer.Ordinal).Count());
        Assert.All(audit.Trace.Where(entry => entry.ProviderStatId.EndsWith(".global", StringComparison.Ordinal)), entry =>
            Assert.False(entry.IsAccepted));
        Assert.All(audit.Trace.Where(entry => entry.ProviderStatId is "crafted.global" or "explicit.global"), entry =>
            Assert.Equal(PathOfExileTradeModifierVariantDiscovery.WeakerSemanticProvenance, entry.RejectionReason));
        Assert.All(audit.Trace.Where(entry =>
                entry.ProviderLocality == PathOfExileTradeProviderStatLocality.Local &&
                entry.ProviderKind is "implicit" or "fractured" or "enchant"), entry =>
            Assert.Equal(
                $"{PathOfExileTradeModifierVariantDiscovery.SemanticMismatch}:" +
                    PathOfExileTradeProviderLocalityCompatibility.InsufficientLocalityEvidence,
                entry.RejectionReason));
        Assert.DoesNotContain(audit.Trace, entry =>
            entry.ProviderKind == "scourge" && entry.IsAccepted);
    }

    [Fact]
    public void Apply_DistinctEquivalentIdsProduceOneExactEquivalentSet()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.source", "#% increased Attack Speed (Local)", "Explicit"),
            Entry(1, "explicit.other", "#% increased Attack Speed (Local)", "Explicit"),
        ]);

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component(),
            catalog,
            Candidate(catalog, "explicit.source"));
        var audit = PathOfExileTradeModifierVariantResolver.DiscoverForAudit(
            Component(),
            catalog,
            Candidate(catalog, "explicit.source"));

        var option = Assert.Single(result.FilterVariants);
        Assert.Equal(
            PathOfExileTradeModifierVariantResolver.IdentityFor(
            [
                Candidate(catalog, "explicit.source"),
                Candidate(catalog, "explicit.other"),
            ]),
            option.Identity);
        Assert.Equal(2, option.ProviderAlternativeCount);
        Assert.Equal(SearchComponentProviderResolutionStatus.ExactEquivalentSet, result.ProviderResolutionStatus);
        Assert.Null(result.ProviderStatId);
        Assert.Equal(["explicit.source", "explicit.other"], result.ProviderStatAlternativeIds);
        Assert.All(audit.Trace, entry => Assert.True(entry.IsAccepted));
    }

    [Fact]
    public void Apply_EquallyPlausibleEquivalentSameKindIdsProduceOneLogicalOption()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "crafted.source", "#% increased Attack Speed (Local)", "Crafted"),
            Entry(1, "implicit.one", "#% increased Attack Speed (Local)", "Implicit"),
            Entry(2, "implicit.two", "#% increased Attack Speed (Local)", "Implicit"),
        ]);
        var component = Component(isCrafted: true) with
        {
            ProviderDomainEvidence =
            [
                .. Component(isCrafted: true).ProviderDomainEvidence,
                Evidence("Implicit", "implicit.applicable-family"),
            ],
        };

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "crafted.source"));
        var audit = PathOfExileTradeModifierVariantResolver.DiscoverForAudit(
            component,
            catalog,
            Candidate(catalog, "crafted.source"));

        var option = Assert.Single(result.FilterVariants, option => option.Label == "Implicit");
        Assert.Equal(2, option.ProviderAlternativeCount);
        Assert.Empty(audit.Diagnostics);
        Assert.All(
            audit.Trace.Where(entry => entry.ProviderKind == "implicit"),
            entry => Assert.True(entry.IsAccepted));
    }

    [Fact]
    public void Apply_IncompatiblePresenceOnlyVariantIsNotOffered()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.stat_210067635", "#% increased Attack Speed (Local)", "Explicit"),
            Entry(1, "implicit.has-attack-speed", "increased Attack Speed (Local)", "Implicit"),
        ]);
        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component() with
            {
                ProviderDomainEvidence =
                [
                    .. Component().ProviderDomainEvidence,
                    Evidence("Implicit", "implicit.attack-speed-family"),
                ],
            },
            catalog,
            Candidate(catalog, "explicit.stat_210067635"));

        Assert.Equal("explicit.stat_210067635", result.ProviderStatId);
        Assert.True(result.SupportsValueBounds);
        Assert.DoesNotContain(result.FilterVariants, option => option.Label == "Implicit");
    }

    [Fact]
    public void Apply_ExactSourceWithoutProjectableBoundsFallsBackToSafePresence()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.unsupported", "#% increased Attack Speed (Local)", "Explicit"),
            Entry(1, "pseudo.total", "+#% total Attack Speed", "Pseudo"),
        ]);
        var component = Component() with
        {
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.Unsupported,
            RequestedMinimum = null,
            ValueBoundsUnsupportedReason = "Provider confirmation required.",
        };

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.unsupported"));

        var option = Assert.Single(result.FilterVariants);
        Assert.Equal("Explicit", option.Label);
        Assert.Equal("explicit.unsupported", result.ProviderStatId);
        Assert.True(result.IsSearchable);
        Assert.Null(result.NotSearchableReason);
        Assert.False(result.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.PresenceOnly, result.ValueBoundShape);
        Assert.Null(result.RequestedMinimum);
        Assert.Null(result.RequestedMaximum);
        Assert.Contains("incompatible numeric semantics", result.ValueBoundsUnsupportedReason);
    }

    [Fact]
    public void Apply_ExactProviderPresenceConvertsUnsupportedSourceToPresenceOnly()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.presence", "Hits are always Critical Strikes", "Explicit"),
        ]);
        var component = Component() with
        {
            OriginalText = "Hits are always Critical Strikes",
            CanonicalSignature = "Hits are always Critical Strikes",
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.Unsupported,
            ObservedNumericValues = [],
            CanonicalNumericValues = [],
            RequestedMinimum = null,
            ValueBoundsUnsupportedReason = "Provider confirmation required.",
        };

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.presence"));

        Assert.True(result.IsSearchable);
        Assert.Equal(ModifierBoundShape.PresenceOnly, result.ValueBoundShape);
        Assert.False(result.SupportsValueBounds);
        Assert.Null(result.RequestedMinimum);
        Assert.Null(result.RequestedMaximum);
    }

    [Fact]
    public void OpaqueOptionsExposeNoRawProviderStatIds()
    {
        var catalog = AttackSpeedCatalog();
        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component(),
            catalog,
            Candidate(catalog, "explicit.stat_210067635"));

        Assert.All(result.FilterVariants, option =>
        {
            Assert.StartsWith("variant-", option.Identity, StringComparison.Ordinal);
            Assert.DoesNotContain("attack-speed", option.Identity, StringComparison.Ordinal);
            Assert.DoesNotContain("explicit.", option.Identity, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Apply_UnavailableChosenIdentityIsPreservedAndBlocksLocalValidation()
    {
        const string missingIdentity = "variant-no-longer-in-catalog";
        var catalog = AttackSpeedCatalog();
        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component(isCrafted: true) with
            {
                IsSelected = true,
                SelectedFilterVariantIdentity = missingIdentity,
            },
            catalog,
            Candidate(catalog, "crafted.stat_210067635"));

        Assert.Equal(missingIdentity, result.SelectedFilterVariantIdentity);
        Assert.Equal(SearchComponentProviderResolutionStatus.NotFound, result.ProviderResolutionStatus);
        Assert.Null(result.ProviderStatId);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
            result.ProviderDiagnosticCode);
        var validation = new TradeSearchDraftValidator().Validate(Draft(result));
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierVariantUnresolved &&
            diagnostic.Severity == TradeSearchValidationSeverity.Error);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(Draft(result));
        Assert.False(mapping.IsSuccess);
        Assert.Contains(mapping.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable);
    }

    [Fact]
    public void Apply_ReviewedLocalDisplayedComponentHidesBroadPseudoAndRejectsItsStaleIdentity()
    {
        var catalog = AttackSpeedCatalog();
        var staleIdentity = PathOfExileTradeModifierVariantResolver.IdentityFor(
            "pseudo.pseudo_total_attack_speed");
        var component = Component(isCrafted: true) with
        {
            IsSelected = true,
            ReviewedItemPropertySemantic = new ItemPropertySemanticDescriptor
            {
                Id = "reviewed.local-attack-speed",
                Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
            },
            SelectedFilterVariantIdentity = staleIdentity,
        };

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "crafted.stat_210067635"));

        Assert.Equal(["Explicit", "Crafted"], resolved.FilterVariants.Select(option => option.Label));
        Assert.DoesNotContain(resolved.FilterVariants, option => option.Label == "Pseudo");
        Assert.Equal(staleIdentity, resolved.SelectedFilterVariantIdentity);
        Assert.Equal(SearchComponentProviderResolutionStatus.NotFound, resolved.ProviderResolutionStatus);
        Assert.Null(resolved.ProviderStatId);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
            resolved.ProviderDiagnosticCode);
        Assert.Contains(
            new TradeSearchDraftValidator().Validate(Draft(resolved)).Diagnostics,
            diagnostic => diagnostic.Code ==
                TradeSearchValidationDiagnosticCodes.SelectedModifierVariantUnresolved);
    }

    [Theory]
    [InlineData("crafted.stat_210067635")]
    [InlineData("explicit.stat_210067635")]
    [InlineData("pseudo.pseudo_total_attack_speed")]
    public void SelectedOpaqueVariant_MapsToExactlyOneAndFilterInFinalJson(string expectedStatId)
    {
        var catalog = AttackSpeedCatalog();
        var component = Component(isCrafted: true) with
        {
            IsSelected = true,
            SelectedFilterVariantIdentity =
                PathOfExileTradeModifierVariantResolver.IdentityFor(expectedStatId),
        };
        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "crafted.stat_210067635"));
        var draft = Draft(resolved);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(draft);
        var build = new PathOfExileTradeQueryBuilder().Build(
            draft,
            new TradeSearchDraftValidator().Validate(draft),
            "Mirage",
            mapping.Filters);

        Assert.True(mapping.IsSuccess);
        Assert.Single(mapping.Filters);
        Assert.True(build.IsSuccess);
        using var document = JsonDocument.Parse(build.SerializedJson!);
        var groups = document.RootElement.GetProperty("query").GetProperty("stats").EnumerateArray().ToArray();
        var group = Assert.Single(groups);
        Assert.Equal("and", group.GetProperty("type").GetString());
        var filter = Assert.Single(group.GetProperty("filters").EnumerateArray());
        Assert.Equal(expectedStatId, filter.GetProperty("id").GetString());
        Assert.Equal(20m, filter.GetProperty("value").GetProperty("min").GetDecimal());
    }

    [Fact]
    public void AggregatedPhysicalDamage_DefaultExplicitVariantEmitsOneFilterWithAggregateMinimum()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.physical", "#% increased Physical Damage", "Explicit"),
            Entry(1, "crafted.physical", "#% increased Physical Damage", "Crafted"),
            Entry(2, "pseudo.total-physical", "#% increased total Physical Damage", "Pseudo"),
        ]);
        var component = Component() with
        {
            ComponentId = "aggregate:modifier:0:0:2",
            OriginalText = "91% increased Physical Damage",
            CanonicalSignature = "<number>% increased Physical Damage",
            ResolvedModifierId = "pure-physical",
            ResolvedStatIds = ["local_physical_damage_+%"],
            ObservedNumericValues = [91m],
            CanonicalNumericValues = [91m],
            RequestedMinimum = 91m,
            IsSelected = true,
            Sources =
            [
                Source("modifier:0:0", 0, "52% increased Physical Damage", 52m, "pure-physical"),
                Source("modifier:1:0", 1, "39% increased Physical Damage", 39m, "hybrid-physical-accuracy"),
            ],
        };
        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.physical"));
        var draft = Draft(resolved);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(draft);
        var build = new PathOfExileTradeQueryBuilder().Build(
            draft,
            new TradeSearchDraftValidator().Validate(draft),
            "Mirage",
            mapping.Filters);

        Assert.Equal(2, resolved.SourceCount);
        Assert.Contains(resolved.FilterVariants, option => option.Label == "Explicit");
        Assert.Contains(resolved.FilterVariants, option => option.Label == "Pseudo");
        Assert.DoesNotContain(resolved.FilterVariants, option => option.Label == "Crafted");
        Assert.DoesNotContain(resolved.FilterVariants, option => option.Label == "Fractured");
        Assert.Equal(2, resolved.Contributors.Count);
        Assert.All(resolved.Contributors, contributor =>
        {
            Assert.False(contributor.IsSelected);
            Assert.Equal("Explicit", contributor.Source.ProviderDomain);
            Assert.Equal(
                PathOfExileTradeProviderIdentity.Create("explicit.physical"),
                contributor.ProviderIdentity);
        });
        Assert.Equal(
            PathOfExileTradeModifierVariantResolver.IdentityFor("explicit.physical"),
            resolved.SelectedFilterVariantIdentity);
        Assert.All(resolved.Sources, source =>
        {
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, source.ProviderResolutionStatus);
            Assert.Equal(
                PathOfExileTradeProviderIdentity.Create("explicit.physical"),
                source.ProviderIdentity);
        });
        var providerFilter = Assert.Single(mapping.Filters);
        Assert.Equal("explicit.physical", providerFilter.StatId);
        Assert.Equal(91m, providerFilter.Minimum);
        Assert.True(build.IsSuccess);
        using var document = JsonDocument.Parse(build.SerializedJson!);
        var filter = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal("explicit.physical", filter.GetProperty("id").GetString());
        Assert.Equal(91m, filter.GetProperty("value").GetProperty("min").GetDecimal());
    }

    [Fact]
    public void AggregatedPhysicalDamage_ManualBoundsAndChosenPseudoEmitOneChosenFilter()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.physical", "#% increased Physical Damage", "Explicit"),
            Entry(1, "pseudo.total-physical", "#% increased total Physical Damage", "Pseudo"),
        ]);
        var component = Component() with
        {
            ComponentId = "aggregate:modifier:0:0:2",
            OriginalText = "91% increased Physical Damage",
            CanonicalSignature = "<number>% increased Physical Damage",
            ResolvedModifierId = "pure-physical",
            ResolvedStatIds = ["local_physical_damage_+%"],
            ObservedNumericValues = [91m],
            CanonicalNumericValues = [91m],
            RequestedMinimum = 80m,
            RequestedMaximum = 100m,
            IsSelected = true,
            SelectedFilterVariantIdentity =
                PathOfExileTradeModifierVariantResolver.IdentityFor("pseudo.total-physical"),
            Sources =
            [
                Source("modifier:0:0", 0, "52% increased Physical Damage", 52m, "pure-physical"),
                Source("modifier:1:0", 1, "39% increased Physical Damage", 39m, "hybrid-physical-accuracy"),
            ],
        };
        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.physical"));
        var draft = Draft(resolved);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(draft);

        var filter = Assert.Single(mapping.Filters);
        Assert.Equal("pseudo.total-physical", filter.StatId);
        Assert.Equal(80m, filter.Minimum);
        Assert.Equal(100m, filter.Maximum);
    }

    [Fact]
    public void MixedDomainLifeAggregate_DefaultsToPseudoNotFracturedAndEmitsAggregateJson()
    {
        // Official Trade catalog anchors (2026-10-28):
        // explicit.stat_3299347043 "+# to maximum Life"
        // fractured.stat_3299347043 "+# to maximum Life"
        // pseudo.pseudo_total_life "+# total maximum Life" (wider: Strength/implicits)
        // Explicit +78 cannot represent Fractured+27 + Explicit+51; Pseudo is the verified aggregate.
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "fractured.stat_3299347043", "+# to maximum Life", "Fractured"),
            Entry(1, "explicit.stat_3299347043", "+# to maximum Life", "Explicit"),
            Entry(2, "pseudo.pseudo_total_life", "+# total maximum Life", "Pseudo"),
        ]);
        var component = LifeAggregateComponent(
            isFractured: false,
            sources:
            [
                LifeSource(
                    "modifier:0:0",
                    0,
                    "+27 to maximum Life",
                    27m,
                    "fractured-life",
                    "Fractured",
                    isFractured: true),
                LifeSource(
                    "modifier:1:0",
                    1,
                    "+51 to maximum Life",
                    51m,
                    "explicit-life",
                    "Explicit"),
            ]);

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.stat_3299347043"));
        var draft = Draft(resolved);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(draft);
        var build = new PathOfExileTradeQueryBuilder().Build(
            draft,
            new TradeSearchDraftValidator().Validate(draft),
            "Mirage",
            mapping.Filters);

        Assert.False(resolved.IsFractured);
        Assert.Equal(
            PathOfExileTradeModifierVariantResolver.IdentityFor("pseudo.pseudo_total_life"),
            resolved.SelectedFilterVariantIdentity);
        Assert.Equal("pseudo.pseudo_total_life", resolved.ProviderStatId);
        Assert.Equal(SearchFilterVariantMode.Aggregate, Selected(resolved).Mode);
        Assert.Contains(resolved.FilterVariants, option => option.Label == "Explicit");
        Assert.Contains(resolved.FilterVariants, option => option.Label == "Fractured");
        Assert.Contains(resolved.FilterVariants, option => option.Label == "Pseudo");
        Assert.Equal(["Fractured", "Explicit"], resolved.Sources.Select(source => source.ProviderDomain));
        Assert.Equal([27m, 51m], resolved.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.All(resolved.Contributors, contributor => Assert.False(contributor.IsSelected));

        var providerFilter = Assert.Single(mapping.Filters);
        Assert.Equal("pseudo.pseudo_total_life", providerFilter.StatId);
        Assert.Equal(78m, providerFilter.Minimum);
        Assert.NotEqual("fractured.stat_3299347043", providerFilter.StatId);
        Assert.True(build.IsSuccess);
        using var document = JsonDocument.Parse(build.SerializedJson!);
        var statGroup = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray());
        Assert.Equal("and", statGroup.GetProperty("type").GetString());
        var jsonFilter = Assert.Single(statGroup.GetProperty("filters").EnumerateArray());
        Assert.Equal("pseudo.pseudo_total_life", jsonFilter.GetProperty("id").GetString());
        Assert.Equal(78m, jsonFilter.GetProperty("value").GetProperty("min").GetDecimal());
    }

    [Fact]
    public void MixedDomainLifeAggregate_WithoutPseudoDoesNotEmitFracturedAggregateFilter()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "fractured.stat_3299347043", "+# to maximum Life", "Fractured"),
            Entry(1, "explicit.stat_3299347043", "+# to maximum Life", "Explicit"),
        ]);
        var component = LifeAggregateComponent(
            isFractured: false,
            sources:
            [
                LifeSource(
                    "modifier:0:0",
                    0,
                    "+27 to maximum Life",
                    27m,
                    "fractured-life",
                    "Fractured",
                    isFractured: true),
                LifeSource(
                    "modifier:1:0",
                    1,
                    "+51 to maximum Life",
                    51m,
                    "explicit-life",
                    "Explicit"),
            ]);

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.stat_3299347043"));
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(Draft(resolved));

        Assert.DoesNotContain(resolved.FilterVariants, option => option.Label == "Pseudo");
        Assert.All(resolved.FilterVariants, option =>
            Assert.Equal(SearchFilterVariantMode.Standalone, option.Mode));
        Assert.Equal(78m, resolved.RequestedMinimum);
        Assert.Equal(2, resolved.Contributors.Count);
        Assert.False(resolved.IsSelected);
        Assert.False(resolved.IsSearchable);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, resolved.ProviderResolutionStatus);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageUnavailable,
            resolved.ProviderDiagnosticCode);
        Assert.Null(resolved.ProviderStatId);
        Assert.DoesNotContain(resolved.FilterVariants, option => option.SupportsContributorComposition);
        Assert.True(mapping.IsSuccess);
        Assert.Empty(mapping.Filters);
    }

    [Fact]
    public void SameDomainExplicitLifeAggregate_PrefersFaithfulExplicitOverPseudo()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.stat_3299347043", "+# to maximum Life", "Explicit"),
            Entry(1, "pseudo.pseudo_total_life", "+# total maximum Life", "Pseudo"),
        ]);
        var component = LifeAggregateComponent(
            resolvedModifierId: "explicit-life",
            sources:
            [
                LifeSource("modifier:0:0", 0, "+27 to maximum Life", 27m, "explicit-life-a"),
                LifeSource("modifier:1:0", 1, "+51 to maximum Life", 51m, "explicit-life-b"),
            ]);

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.stat_3299347043"));
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(Draft(resolved));

        Assert.Equal("explicit.stat_3299347043", resolved.ProviderStatId);
        Assert.Equal(SearchFilterVariantMode.Aggregate, Selected(resolved).Mode);
        Assert.True(Selected(resolved).SupportsContributorComposition);
        Assert.Contains(resolved.FilterVariants, option => option.Label == "Pseudo");
        var filter = Assert.Single(mapping.Filters);
        Assert.Equal("explicit.stat_3299347043", filter.StatId);
        Assert.Equal(78m, filter.Minimum);
    }

    [Fact]
    public void SameDomainStunRecoveryAggregate_FourteenPlusTwentySevenUsesOfficialExplicitTotal()
    {
        // Official frozen catalog (2026-08-19): explicit.stat_2511217560 =
        // "#% increased Stun and Block Recovery". No pseudo total for this effect.
        // Same-domain Hybrid Prefix + Explicit Suffix may therefore search the Explicit
        // aggregate with one min on the summed effect (14+27=41), never AND 14 and 27.
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.stat_2511217560", "#% increased Stun and Block Recovery", "Explicit"),
            Entry(1, "fractured.stat_2511217560", "#% increased Stun and Block Recovery", "Fractured"),
        ]);
        var hybrid = StunSource(
            "modifier:0:0",
            0,
            "14% increased Stun and Block Recovery",
            14m,
            "hybrid-stun-recovery",
            ParsedModifierKind.Prefix,
            isHybrid: true);
        var suffix = StunSource(
            "modifier:1:0",
            1,
            "27% increased Stun and Block Recovery",
            27m,
            "suffix-stun-recovery",
            ParsedModifierKind.Suffix);
        var component = Component() with
        {
            ComponentId = "aggregate:modifier:0:0:2",
            OriginalText = "41% increased Stun and Block Recovery",
            CanonicalSignature = "<number>% increased Stun and Block Recovery",
            ParsedKind = ParsedModifierKind.Prefix,
            Locality = ModifierLocality.Global,
            ResolvedModifierId = "hybrid-stun-recovery",
            ResolvedStatIds = ["base_stun_recovery_+%"],
            ObservedNumericValues = [41m],
            CanonicalNumericValues = [41m],
            RequestedMinimum = 41m,
            IsSelected = true,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Sources = [hybrid, suffix],
        };

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.stat_2511217560"));
        var withRequestedMin = resolved with { RequestedMinimum = 45m };
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(Draft(withRequestedMin));
        var build = new PathOfExileTradeQueryBuilder().Build(
            Draft(withRequestedMin),
            new TradeSearchDraftValidator().Validate(Draft(withRequestedMin)),
            "Mirage",
            mapping.Filters);

        Assert.Equal("41% increased Stun and Block Recovery", resolved.OriginalText);
        Assert.Equal(41m, resolved.ObservedNumericValues.Single());
        Assert.Equal(2, resolved.Contributors.Count);
        Assert.Equal([14m, 27m], resolved.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.Equal([true, false], resolved.Sources.Select(source => source.IsHybrid));
        Assert.Equal("explicit.stat_2511217560", resolved.ProviderStatId);
        Assert.Equal(SearchFilterVariantMode.Aggregate, Selected(resolved).Mode);
        Assert.True(Selected(resolved).SupportsContributorComposition);
        Assert.DoesNotContain(resolved.FilterVariants, option => option.Label == "Pseudo");

        Assert.True(mapping.IsSuccess);
        var filter = Assert.Single(mapping.Filters);
        Assert.Equal("explicit.stat_2511217560", filter.StatId);
        Assert.Equal(45m, filter.Minimum);
        Assert.True(build.IsSuccess);
        using var document = JsonDocument.Parse(build.SerializedJson!);
        var group = Assert.Single(document.RootElement.GetProperty("query").GetProperty("stats").EnumerateArray());
        Assert.Equal("and", group.GetProperty("type").GetString());
        var jsonFilter = Assert.Single(group.GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_2511217560", jsonFilter.GetProperty("id").GetString());
        Assert.Equal(45m, jsonFilter.GetProperty("value").GetProperty("min").GetDecimal());
        Assert.Equal(1, group.GetProperty("filters").GetArrayLength());
    }

    [Fact]
    public void MixedDomainStunRecoveryAggregate_WithoutPseudoIsUnsupported()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.stat_2511217560", "#% increased Stun and Block Recovery", "Explicit"),
            Entry(1, "fractured.stat_2511217560", "#% increased Stun and Block Recovery", "Fractured"),
        ]);
        var fractured = StunSource(
            "modifier:0:0",
            0,
            "14% increased Stun and Block Recovery",
            14m,
            "fractured-stun-recovery",
            ParsedModifierKind.Suffix,
            providerDomain: "Fractured",
            isFractured: true);
        var explicitSource = StunSource(
            "modifier:1:0",
            1,
            "27% increased Stun and Block Recovery",
            27m,
            "explicit-stun-recovery",
            ParsedModifierKind.Suffix);
        var component = Component() with
        {
            ComponentId = "aggregate:modifier:0:0:2",
            OriginalText = "41% increased Stun and Block Recovery",
            CanonicalSignature = "<number>% increased Stun and Block Recovery",
            Locality = ModifierLocality.Global,
            ResolvedModifierId = "fractured-stun-recovery",
            ResolvedStatIds = ["base_stun_recovery_+%"],
            ObservedNumericValues = [41m],
            CanonicalNumericValues = [41m],
            RequestedMinimum = 41m,
            IsSelected = true,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Sources = [fractured, explicitSource],
        };

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.stat_2511217560"));
        var childOnly = resolved with
        {
            Contributors = resolved.Contributors
                .Select((contributor, index) => contributor with { IsSelected = index == 1 })
                .ToArray(),
        };
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(Draft(childOnly), catalog);

        Assert.Equal(41m, resolved.RequestedMinimum);
        Assert.Equal(2, resolved.Contributors.Count);
        Assert.False(resolved.IsSelected);
        Assert.False(resolved.IsSearchable);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, resolved.ProviderResolutionStatus);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageUnavailable,
            resolved.ProviderDiagnosticCode);
        Assert.Null(resolved.ProviderStatId);
        Assert.True(mapping.IsSuccess);
        var childFilter = Assert.Single(mapping.Filters);
        Assert.Equal("explicit.stat_2511217560", childFilter.StatId);
        Assert.Equal(27m, childFilter.Minimum);
    }

    [Fact]
    public void MixedDomainLifeAggregate_SelectedFracturedContributorKeepsExactFracturedIdentity()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "fractured.stat_3299347043", "+# to maximum Life", "Fractured"),
            Entry(1, "explicit.stat_3299347043", "+# to maximum Life", "Explicit"),
            Entry(2, "pseudo.pseudo_total_life", "+# total maximum Life", "Pseudo"),
        ]);
        var component = LifeAggregateComponent(
            sources:
            [
                LifeSource(
                    "modifier:0:0",
                    0,
                    "+27 to maximum Life",
                    27m,
                    "fractured-life",
                    "Fractured",
                    isFractured: true),
                LifeSource(
                    "modifier:1:0",
                    1,
                    "+51 to maximum Life",
                    51m,
                    "explicit-life",
                    "Explicit"),
            ]);

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.stat_3299347043"));
        var withFracturedChild = resolved with
        {
            Contributors = resolved.Contributors
                .Select((contributor, index) => contributor with { IsSelected = index == 0 })
                .ToArray(),
        };
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(
            Draft(withFracturedChild),
            catalog);

        Assert.True(mapping.IsSuccess);
        Assert.Equal(
            ["pseudo.pseudo_total_life", "fractured.stat_3299347043"],
            mapping.Filters.Select(filter => filter.StatId));
        Assert.Equal([78m, 27m], mapping.Filters.Select(filter => filter.Minimum));
        Assert.DoesNotContain(mapping.Filters, filter => filter.StatId == "explicit.stat_3299347043");

        var withExplicitChild = resolved with
        {
            Contributors = resolved.Contributors
                .Select((contributor, index) => contributor with { IsSelected = index == 1 })
                .ToArray(),
        };
        var explicitMapping = new PathOfExileTradeSelectedModifierMapper().Map(
            Draft(withExplicitChild),
            catalog);
        Assert.Equal(
            ["pseudo.pseudo_total_life", "explicit.stat_3299347043"],
            explicitMapping.Filters.Select(filter => filter.StatId));
        Assert.Equal([78m, 51m], explicitMapping.Filters.Select(filter => filter.Minimum));
        Assert.DoesNotContain(
            explicitMapping.Filters,
            filter => filter.StatId == "fractured.stat_3299347043");
    }

    [Fact]
    public void LifeAggregate_UnselectedMainRowEmitsNoLifeFilter()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.stat_3299347043", "+# to maximum Life", "Explicit"),
            Entry(1, "pseudo.pseudo_total_life", "+# total maximum Life", "Pseudo"),
        ]);
        var component = LifeAggregateComponent(
            isSelected: false,
            resolvedModifierId: "explicit-life",
            sources:
            [
                LifeSource("modifier:0:0", 0, "+27 to maximum Life", 27m, "explicit-life-a"),
                LifeSource("modifier:1:0", 1, "+51 to maximum Life", 51m, "explicit-life-b"),
            ]);

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.stat_3299347043"));
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(Draft(resolved));

        Assert.True(mapping.IsSuccess);
        Assert.Empty(mapping.Filters);
    }

    private static ResolvedSearchComponent LifeAggregateComponent(
        IReadOnlyList<SearchComponentSourceProvenance> sources,
        bool isFractured = false,
        bool isSelected = true,
        string resolvedModifierId = "fractured-life")
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "aggregate:modifier:0:0:2",
            OriginalText = "+78 to maximum Life",
            CanonicalSignature = "+<number> to maximum Life",
            ParsedKind = ParsedModifierKind.Prefix,
            Locality = ModifierLocality.Global,
            IsFractured = isFractured,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = resolvedModifierId,
            ResolvedStatIds = ["base_maximum_life"],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [78m],
            CanonicalNumericValues = [78m],
            RequestedMinimum = 78m,
            IsSelected = isSelected,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            ProviderDomainEvidence =
            [
                new SearchComponentProviderDomainEvidence
                {
                    ProviderDomain = "Explicit",
                    ModifierId = "explicit-life",
                    GenerationType = ModifierGenerationType.Prefix,
                    Locality = ModifierLocality.Global,
                    IsSourceExact = true,
                    ItemBaseId = "base.fixture",
                    ItemClass = "Ring",
                    ApplicabilityReason = "Exact Explicit Life source fixture.",
                },
                new SearchComponentProviderDomainEvidence
                {
                    ProviderDomain = "Fractured",
                    ModifierId = "fractured-life",
                    GenerationType = ModifierGenerationType.Prefix,
                    Locality = ModifierLocality.Global,
                    IsSourceExact = true,
                    ItemBaseId = "base.fixture",
                    ItemClass = "Ring",
                    ApplicabilityReason = "Exact Fractured Life source fixture.",
                },
            ],
            Sources = sources,
        };
    }

    [Fact]
    public void MixedDomainPhysicalAggregate_DefaultsToPseudoAndExposesContextualStandaloneModes()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.physical", "#% increased Physical Damage", "Explicit"),
            Entry(1, "crafted.physical", "#% increased Physical Damage", "Crafted"),
            Entry(2, "fractured.physical", "#% increased Physical Damage", "Fractured"),
            Entry(3, "pseudo.total-physical", "#% increased total Physical Damage", "Pseudo"),
        ]);
        var component = Component() with
        {
            ComponentId = "aggregate:modifier:0:0:2",
            OriginalText = "146% increased Physical Damage",
            CanonicalSignature = "<number>% increased Physical Damage",
            ResolvedModifierId = "explicit-hybrid",
            ResolvedStatIds = ["local_physical_damage_+%"],
            ObservedNumericValues = [146m],
            CanonicalNumericValues = [146m],
            RequestedMinimum = 146m,
            IsSelected = true,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            ProviderDomainEvidence =
            [
                Evidence("Explicit", "explicit-hybrid", isSourceExact: true),
                Evidence("Crafted", "crafted-physical"),
                Evidence("Fractured", "explicit-hybrid"),
            ],
            Sources =
            [
                Source("modifier:0:0", 0, "30% increased Physical Damage", 30m, "explicit-hybrid"),
                Source("modifier:1:0", 1, "116% increased Physical Damage", 116m, "crafted-physical", "Crafted"),
            ],
        };

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.physical"));
        var draft = Draft(resolved);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(draft);
        var build = new PathOfExileTradeQueryBuilder().Build(
            draft,
            new TradeSearchDraftValidator().Validate(draft),
            "Mirage",
            mapping.Filters);

        Assert.Equal(
            ["Crafted", "Explicit", "Fractured", "Pseudo"],
            resolved.FilterVariants.Select(option => option.Label).OrderBy(label => label, StringComparer.Ordinal));
        Assert.Single(resolved.FilterVariants, option => option.Mode == SearchFilterVariantMode.Aggregate);
        Assert.Single(resolved.FilterVariants, option => option.SupportsContributorComposition);
        Assert.True(resolved.FilterVariants.Single(option => option.Label == "Pseudo")
            .SupportsContributorComposition);
        Assert.All(
            resolved.FilterVariants.Where(option => option.Label != "Pseudo"),
            option => Assert.False(option.SupportsContributorComposition));
        Assert.Equal(3, resolved.FilterVariants.Count(option => option.Mode == SearchFilterVariantMode.Standalone));
        Assert.Equal(
            resolved.FilterVariants.Count,
            resolved.FilterVariants.Select(option => option.Label).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            PathOfExileTradeModifierVariantResolver.IdentityFor("pseudo.total-physical"),
            resolved.SelectedFilterVariantIdentity);
        Assert.Equal("pseudo.total-physical", resolved.ProviderStatId);
        Assert.Equal(146m, resolved.RequestedMinimum);
        Assert.Null(resolved.ProviderDiagnosticCode);
        Assert.Null(resolved.ProviderDiagnosticMessage);
        Assert.Equal(
            [
                PathOfExileTradeProviderIdentity.Create("explicit.physical"),
                PathOfExileTradeProviderIdentity.Create("crafted.physical"),
            ],
            resolved.Sources.Select(source => source.ProviderIdentity));
        Assert.All(resolved.Sources, source =>
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, source.ProviderResolutionStatus));
        Assert.Equal(2, resolved.Contributors.Count);
        Assert.All(resolved.Contributors, contributor => Assert.False(contributor.IsSelected));
        Assert.Equal([30m, 116m], resolved.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.Equal(
            [
                PathOfExileTradeProviderIdentity.Create("explicit.physical"),
                PathOfExileTradeProviderIdentity.Create("crafted.physical"),
            ],
            resolved.Contributors.Select(contributor => contributor.ProviderIdentity));

        var providerFilter = Assert.Single(mapping.Filters);
        Assert.Equal("pseudo.total-physical", providerFilter.StatId);
        Assert.Equal(146m, providerFilter.Minimum);
        Assert.True(build.IsSuccess);
        using var document = JsonDocument.Parse(build.SerializedJson!);
        var statGroup = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray());
        Assert.Equal("and", statGroup.GetProperty("type").GetString());
        var jsonFilter = Assert.Single(statGroup
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal("pseudo.total-physical", jsonFilter.GetProperty("id").GetString());
        Assert.Equal(146m, jsonFilter.GetProperty("value").GetProperty("min").GetDecimal());
        Assert.NotEqual("explicit.physical", jsonFilter.GetProperty("id").GetString());
        Assert.NotEqual("crafted.physical", jsonFilter.GetProperty("id").GetString());

        var withSelectedChild = resolved with
        {
            Contributors = resolved.Contributors
                .Select((contributor, index) => index == 0
                    ? contributor with { IsSelected = true }
                    : contributor)
                .ToArray(),
        };
        var childResolved = PathOfExileTradeModifierVariantResolver.Apply(
            withSelectedChild,
            catalog,
            Candidate(catalog, "pseudo.total-physical"));
        var childDraft = Draft(childResolved);
        var childMapping = new PathOfExileTradeSelectedModifierMapper().Map(childDraft, catalog);
        var childBuild = new PathOfExileTradeQueryBuilder().Build(
            childDraft,
            new TradeSearchDraftValidator().Validate(childDraft),
            "Mirage",
            childMapping.Filters);

        Assert.True(childMapping.IsSuccess);
        Assert.Equal(
            ["pseudo.total-physical", "explicit.physical"],
            childMapping.Filters.Select(filter => filter.StatId));
        Assert.Equal([146m, 30m], childMapping.Filters.Select(filter => filter.Minimum));
        Assert.True(childBuild.IsSuccess);
        using var childDocument = JsonDocument.Parse(childBuild.SerializedJson!);
        var childGroup = Assert.Single(childDocument.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray());
        Assert.Equal("and", childGroup.GetProperty("type").GetString());
        var childFilters = childGroup.GetProperty("filters").EnumerateArray().ToArray();
        Assert.Equal(2, childFilters.Length);
        Assert.Equal(
            ["pseudo.total-physical", "explicit.physical"],
            childFilters.Select(filter => filter.GetProperty("id").GetString()));
        Assert.Equal(
            [146m, 30m],
            childFilters.Select(filter => filter.GetProperty("value").GetProperty("min").GetDecimal()));
        Assert.Equal(2, childFilters.Select(filter => filter.GetProperty("id").GetString()).Distinct().Count());
    }

    [Fact]
    public void SameIdentityExactAggregate_ExplicitVariantSupportsCompositionAndFoldsChildrenIntoParent()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.physical", "#% increased Physical Damage", "Explicit"),
        ]);
        var component = Component() with
        {
            ComponentId = "aggregate:modifier:0:0:2",
            OriginalText = "31% increased Physical Damage",
            CanonicalSignature = "<number>% increased Physical Damage",
            ResolvedModifierId = "explicit-physical",
            ResolvedStatIds = ["local_physical_damage_+%"],
            ObservedNumericValues = [31m],
            CanonicalNumericValues = [31m],
            RequestedMinimum = 31m,
            IsSelected = true,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Sources =
            [
                Source("modifier:0:0", 0, "9% increased Physical Damage", 9m, "explicit-physical"),
                Source("modifier:1:0", 1, "22% increased Physical Damage", 22m, "explicit-physical"),
            ],
        };

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.physical"));
        resolved = resolved with
        {
            Contributors = resolved.Contributors
                .Select(contributor => contributor with { IsSelected = true })
                .ToArray(),
        };
        var draft = Draft(resolved);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(draft, catalog);

        var option = Assert.Single(resolved.FilterVariants);
        Assert.Equal("Explicit", option.Label);
        Assert.True(option.SupportsContributorComposition);
        Assert.Equal(SearchFilterVariantMode.Aggregate, option.Mode);
        Assert.True(SearchComponentContributorActivation.IsFilteringActive(resolved));
        Assert.True(mapping.IsSuccess);
        var filter = Assert.Single(mapping.Filters);
        Assert.Equal("explicit.physical", filter.StatId);
        Assert.Equal(31m, filter.Minimum);
    }

    [Fact]
    public void MixedDomainPhysicalAggregate_WithoutPseudoIsUnsupportedAndDoesNotEmitFalseTotal()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.physical", "#% increased Physical Damage", "Explicit"),
            Entry(1, "crafted.physical", "#% increased Physical Damage", "Crafted"),
        ]);
        var component = Component() with
        {
            ComponentId = "aggregate:modifier:0:0:2",
            OriginalText = "146% increased Physical Damage",
            CanonicalSignature = "<number>% increased Physical Damage",
            ResolvedModifierId = "explicit-hybrid",
            ResolvedStatIds = ["local_physical_damage_+%"],
            ObservedNumericValues = [146m],
            CanonicalNumericValues = [146m],
            RequestedMinimum = 146m,
            IsSelected = true,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Sources =
            [
                Source("modifier:0:0", 0, "30% increased Physical Damage", 30m, "explicit-hybrid"),
                Source("modifier:1:0", 1, "116% increased Physical Damage", 116m, "crafted-physical", "Crafted"),
            ],
        };

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.physical"));
        var draft = Draft(resolved);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(draft);

        Assert.Equal("146% increased Physical Damage", resolved.OriginalText);
        Assert.Equal(2, resolved.SourceCount);
        Assert.Equal(146m, resolved.RequestedMinimum);
        Assert.Equal([30m, 116m], resolved.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.False(resolved.IsSelected);
        Assert.False(resolved.IsSearchable);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, resolved.ProviderResolutionStatus);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageUnavailable,
            resolved.ProviderDiagnosticCode);
        Assert.Contains("Crafted, Explicit", resolved.ProviderDiagnosticMessage, StringComparison.Ordinal);
        Assert.Null(resolved.ProviderStatId);
        Assert.All(resolved.FilterVariants, option => Assert.Equal(SearchFilterVariantMode.Standalone, option.Mode));
        Assert.True(mapping.IsSuccess);
        Assert.Empty(mapping.Filters);
    }

    [Fact]
    public void MixedDomainPhysicalAggregate_WithAmbiguousPseudoKindIsUnsupportedWithoutFalseTotal()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.physical", "#% increased Physical Damage", "Explicit"),
            Entry(1, "crafted.physical", "#% increased Physical Damage", "Crafted"),
            Entry(2, "pseudo.total-physical", "#% increased total Physical Damage", "Pseudo"),
            Entry(3, "pseudo.combined-physical", "#% increased combined Physical Damage", "Pseudo"),
        ]);
        var component = Component() with
        {
            ComponentId = "aggregate:modifier:0:0:2",
            OriginalText = "146% increased Physical Damage",
            CanonicalSignature = "<number>% increased Physical Damage",
            ResolvedModifierId = "explicit-hybrid",
            ResolvedStatIds = ["local_physical_damage_+%"],
            ObservedNumericValues = [146m],
            CanonicalNumericValues = [146m],
            RequestedMinimum = 146m,
            IsSelected = true,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Sources =
            [
                Source("modifier:0:0", 0, "30% increased Physical Damage", 30m, "explicit-hybrid"),
                Source("modifier:1:0", 1, "116% increased Physical Damage", 116m, "crafted-physical", "Crafted"),
            ],
        };

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(
            component,
            catalog,
            Candidate(catalog, "explicit.physical"));
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(Draft(resolved));

        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, resolved.ProviderResolutionStatus);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageUnavailable,
            resolved.ProviderDiagnosticCode);
        Assert.Null(resolved.ProviderStatId);
        Assert.Equal(["Explicit", "Crafted"], resolved.FilterVariants.Select(option => option.Label));
        Assert.Equal(
            resolved.FilterVariants.Count,
            resolved.FilterVariants.Select(option => option.Label).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(resolved.FilterVariants, option => option.Label == "Pseudo");
        Assert.Equal(146m, resolved.RequestedMinimum);
        Assert.False(resolved.IsSelected);
        Assert.True(mapping.IsSuccess);
        Assert.Empty(mapping.Filters);
    }

    private static SearchFilterVariant Selected(ResolvedSearchComponent component)
    {
        return Assert.Single(component.FilterVariants, option =>
            option.Identity == component.SelectedFilterVariantIdentity);
    }

    private static PathOfExileTradeStatCatalog AttackSpeedCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.stat_210067635", "#% increased Attack Speed (Local)", "Explicit"),
            Entry(1, "crafted.stat_210067635", "#% increased Attack Speed (Local)", "Crafted"),
            Entry(2, "implicit.stat_210067635", "#% increased Attack Speed (Local)", "Implicit"),
            Entry(3, "fractured.stat_210067635", "#% increased Attack Speed (Local)", "Fractured"),
            Entry(4, "enchant.stat_210067635", "#% increased Attack Speed (Local)", "Enchant"),
            Entry(5, "pseudo.pseudo_total_attack_speed", "+#% total Attack Speed", "Pseudo"),
            Entry(6, "pseudo.pseudo_total_cast_speed", "+#% total Cast Speed", "Pseudo"),
        ]);
    }

    private static PathOfExileTradeStatMatchCandidate Candidate(
        PathOfExileTradeStatCatalog catalog,
        string id)
    {
        Assert.True(catalog.TryGetById(id, out var entry));
        return PathOfExileTradeStatCandidateClassifier.ToCandidate(entry);
    }

    private static PathOfExileTradeStatEntry Entry(
        int order,
        string id,
        string text,
        string kind)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = kind.ToLowerInvariant(),
            GroupLabel = kind,
            Id = id,
            Text = text,
            Type = kind.ToLowerInvariant(),
        };
    }

    private static ResolvedSearchComponent Component(bool isCrafted = false)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = "20% increased Attack Speed",
            CanonicalSignature = "#% increased Attack Speed",
            ParsedKind = ParsedModifierKind.Suffix,
            Locality = ModifierLocality.Local,
            IsCrafted = isCrafted,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "mod.attack-speed",
            ResolvedStatIds = ["attack_speed_+%"],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [20m],
            CanonicalNumericValues = [20m],
            RequestedMinimum = 20m,
            ProviderDomainEvidence = isCrafted
                ?
                [
                    Evidence("Crafted", "mod.attack-speed", isSourceExact: true),
                    Evidence("Explicit", "mod.explicit-attack-speed"),
                ]
                : [Evidence("Explicit", "mod.attack-speed", isSourceExact: true)],
        };
    }

    private static SearchComponentProviderDomainEvidence Evidence(
        string providerDomain,
        string modifierId,
        bool isSourceExact = false)
    {
        return new SearchComponentProviderDomainEvidence
        {
            ProviderDomain = providerDomain,
            ModifierId = modifierId,
            GenerationType = providerDomain == "Implicit"
                ? ModifierGenerationType.Implicit
                : ModifierGenerationType.Suffix,
            Locality = ModifierLocality.Local,
            IsSourceExact = isSourceExact,
            ItemBaseId = "base.fixture",
            ItemClass = "One Hand Axe",
            MatchedTag = "weapon",
            ApplicabilityReason = isSourceExact
                ? "Exact source fixture."
                : "Applicable GameData fixture.",
        };
    }

    private static SearchComponentSourceProvenance Source(
        string componentId,
        int sourceModifierIndex,
        string text,
        decimal value,
        string modifierId,
        string providerDomain = "Explicit")
    {
        return new SearchComponentSourceProvenance
        {
            ComponentId = componentId,
            SourceModifierIndex = sourceModifierIndex,
            OriginalText = text,
            CanonicalSignature = "<number>% increased Physical Damage",
            ParsedKind = ParsedModifierKind.Prefix,
            GenerationType = ModifierGenerationType.Prefix,
            Locality = ModifierLocality.Local,
            ProviderDomain = providerDomain,
            ResolvedModifierId = modifierId,
            ResolvedStatIds = ["local_physical_damage_+%"],
            ObservedNumericValues = [value],
            CanonicalNumericValues = [value],
            ValueBoundShape = ModifierBoundShape.Scalar,
            TranslationHandlers = [[]],
            ProviderIdentity = PathOfExileTradeProviderIdentity.Create(
                $"{providerDomain.ToLowerInvariant()}.physical"),
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
        };
    }

    private static SearchComponentSourceProvenance LifeSource(
        string componentId,
        int sourceModifierIndex,
        string text,
        decimal value,
        string modifierId,
        string providerDomain = "Explicit",
        bool isFractured = false)
    {
        return new SearchComponentSourceProvenance
        {
            ComponentId = componentId,
            SourceModifierIndex = sourceModifierIndex,
            OriginalText = text,
            CanonicalSignature = "+<number> to maximum Life",
            ParsedKind = ParsedModifierKind.Prefix,
            GenerationType = ModifierGenerationType.Prefix,
            Locality = ModifierLocality.Global,
            ProviderDomain = providerDomain,
            IsFractured = isFractured,
            ResolvedModifierId = modifierId,
            ResolvedStatIds = ["base_maximum_life"],
            ObservedNumericValues = [value],
            CanonicalNumericValues = [value],
            ValueBoundShape = ModifierBoundShape.Scalar,
            TranslationHandlers = [[]],
            ProviderIdentity = PathOfExileTradeProviderIdentity.Create(
                $"{providerDomain.ToLowerInvariant()}.stat_3299347043"),
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
        };
    }

    private static SearchComponentSourceProvenance StunSource(
        string componentId,
        int sourceModifierIndex,
        string text,
        decimal value,
        string modifierId,
        ParsedModifierKind parsedKind,
        string providerDomain = "Explicit",
        bool isHybrid = false,
        bool isFractured = false)
    {
        return new SearchComponentSourceProvenance
        {
            ComponentId = componentId,
            SourceModifierIndex = sourceModifierIndex,
            OriginalText = text,
            CanonicalSignature = "<number>% increased Stun and Block Recovery",
            ParsedKind = parsedKind,
            GenerationType = parsedKind == ParsedModifierKind.Suffix
                ? ModifierGenerationType.Suffix
                : ModifierGenerationType.Prefix,
            Locality = ModifierLocality.Global,
            ProviderDomain = providerDomain,
            IsHybrid = isHybrid,
            IsFractured = isFractured,
            ResolvedModifierId = modifierId,
            ResolvedStatIds = ["base_stun_recovery_+%"],
            ObservedNumericValues = [value],
            CanonicalNumericValues = [value],
            ValueBoundShape = ModifierBoundShape.Scalar,
            TranslationHandlers = [[]],
            ProviderIdentity = PathOfExileTradeProviderIdentity.Create(
                $"{providerDomain.ToLowerInvariant()}.stat_2511217560"),
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
        };
    }

    private static TradeSearchDraft Draft(ResolvedSearchComponent modifier)
    {
        var exactBase = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = "One-Handed Axe",
            ExactBaseName = "Reaver Axe",
        };
        return new TradeSearchDraft
        {
            ItemClass = "One Hand Axes",
            Rarity = "Rare",
            DisplayName = "Armageddon Thirst",
            ParsedBaseType = "Reaver Axe",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "Metadata/Items/Weapons/OneHandWeapons/OneHandAxes/ReaverAxe",
                ResolvedBaseName = "Reaver Axe",
                Category = "One-Handed Axe",
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = "Metadata/Items/Weapons/OneHandWeapons/OneHandAxes/ReaverAxe",
                    ExactBaseName = "Reaver Axe",
                    Category = "One-Handed Axe",
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    ExactBase = exactBase,
                },
                ActiveCriterion = exactBase,
            },
            ModifierFilters = [modifier],
        };
    }
}
