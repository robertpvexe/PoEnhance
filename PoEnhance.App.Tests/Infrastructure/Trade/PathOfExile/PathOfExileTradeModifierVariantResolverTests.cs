using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

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
    }

    [Fact]
    public void Apply_CraftedScalarDefaultsToRealCraftedFilterAndOffersCompatiblePseudo()
    {
        var catalog = AttackSpeedCatalog();
        var source = Candidate(catalog, "crafted.stat_210067635");

        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component(isCrafted: true),
            catalog,
            source);

        Assert.Equal("Crafted", Selected(result).Label);
        Assert.Equal("crafted.stat_210067635", result.ProviderStatId);
        Assert.Contains(result.FilterVariants, option => option.Label == "Explicit");
        Assert.Contains(result.FilterVariants, option => option.Label == "Pseudo");
        Assert.Contains(result.FilterVariants, option => option.Label == "Enchant");
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
    public void Apply_PresenceOnlyVariantDisablesBoundsWithoutRemovingTheOtherOption()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "explicit.stat_210067635", "#% increased Attack Speed (Local)", "Explicit"),
            Entry(1, "implicit.has-attack-speed", "increased Attack Speed (Local)", "Implicit"),
        ]);
        var presenceIdentity = PathOfExileTradeModifierVariantResolver.IdentityFor("implicit.has-attack-speed");
        var result = PathOfExileTradeModifierVariantResolver.Apply(
            Component() with { SelectedFilterVariantIdentity = presenceIdentity },
            catalog,
            Candidate(catalog, "explicit.stat_210067635"));

        Assert.Equal("implicit.has-attack-speed", result.ProviderStatId);
        Assert.False(result.SupportsValueBounds);
        Assert.Null(result.RequestedMinimum);
        Assert.Null(result.RequestedMaximum);
        Assert.Contains("retained Min/Max text", result.ValueBoundsUnsupportedReason);
        Assert.Contains(result.FilterVariants, option => option.Label == "Explicit" && option.SupportsValueBounds);
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

    [Theory]
    [InlineData("crafted.stat_210067635")]
    [InlineData("explicit.stat_210067635")]
    [InlineData("implicit.stat_210067635")]
    [InlineData("fractured.stat_210067635")]
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
            Entry(5, "pseudo.pseudo_total_attack_speed", "#% increased total Attack Speed", "Pseudo"),
            Entry(6, "pseudo.pseudo_total_cast_speed", "#% increased total Cast Speed", "Pseudo"),
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
            RequestedMinimum = 20m,
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
