using System.Collections.Immutable;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeItemPropertyResolver
{
    private const string ProviderWeaponCategoryPrefix = "weapon.";
    private const string ProviderArmourCategoryPrefix = "armour.";
    private readonly PathOfExileTradeItemPropertyMappingCatalog mappingCatalog;

    public PathOfExileTradeItemPropertyResolver()
        : this(new PathOfExileTradeItemPropertyMappingResourceLoader().LoadDefaultOrThrow())
    {
    }

    internal PathOfExileTradeItemPropertyResolver(
        PathOfExileTradeItemPropertyMappingCatalog mappingCatalog)
    {
        this.mappingCatalog = mappingCatalog ?? throw new ArgumentNullException(nameof(mappingCatalog));
    }

    public TradeSearchDraft Resolve(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(catalog);
        if (draft.ItemProperties.IsDefaultOrEmpty)
        {
            return draft;
        }

        var isWeaponDraft = IsWeaponDraft(draft, catalog);
        var isArmourDraft = IsArmourDraft(draft, catalog);
        return draft with
        {
            ItemProperties = draft.ItemProperties
                .Select(property => ResolveProperty(property, catalog, isWeaponDraft, isArmourDraft))
                .ToImmutableArray(),
        };
    }

    public PathOfExileTradeSelectedItemPropertyMappingResult MapSelected(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(catalog);
        var verifiedDraft = Resolve(draft, catalog);
        var filters = new List<PathOfExileTradeSelectedItemPropertyFilter>();
        var diagnostics = new List<PathOfExileTradeSelectedItemPropertyMappingDiagnostic>();

        foreach (var (property, index) in verifiedDraft.ItemProperties.Select(
                     (property, index) => (property, index)))
        {
            if (!property.IsSelected)
            {
                continue;
            }

            if (property.ProviderResolutionStatus != TradeSearchItemPropertyProviderResolutionStatus.Exact ||
                !property.IsSearchable ||
                !mappingCatalog.TryGet(property.Kind, out var mapping) ||
                !mapping.IsSupported ||
                string.IsNullOrWhiteSpace(mapping.ProviderGroupId) ||
                string.IsNullOrWhiteSpace(mapping.ProviderFilterId))
            {
                diagnostics.Add(new PathOfExileTradeSelectedItemPropertyMappingDiagnostic(
                    PathOfExileTradeSelectedItemPropertyMappingDiagnosticCodes.NotExactlyResolved,
                    property.NotSearchableReason ??
                        $"Selected item property '{property.Label}' has no exact verified Trade filter mapping.",
                    index));
                continue;
            }

            if (!property.RequestedMinimum.HasValue && !property.RequestedMaximum.HasValue)
            {
                continue;
            }

            filters.Add(new PathOfExileTradeSelectedItemPropertyFilter
            {
                SourceItemPropertyIndex = index,
                ProviderGroupId = mapping.ProviderGroupId,
                ProviderFilterId = mapping.ProviderFilterId,
                RequestedMinimum = property.RequestedMinimum,
                RequestedMaximum = property.RequestedMaximum,
            });
        }

        return diagnostics.Count == 0
            ? PathOfExileTradeSelectedItemPropertyMappingResult.Success(filters)
            : PathOfExileTradeSelectedItemPropertyMappingResult.Failure(diagnostics);
    }

    public TradeSearchDraft MarkCatalogUnavailable(TradeSearchDraft draft, string reason)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return draft with
        {
            ItemProperties = draft.ItemProperties
                .Select(property => mappingCatalog.TryGet(property.Kind, out var mapping) &&
                    !mapping.IsSupported
                        ? property with
                        {
                            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                            IsSearchable = false,
                            NotSearchableReason = mapping.UnsupportedReason ??
                                "The Trade provider does not support this item property.",
                        }
                        : Unresolved(property, reason))
                .ToImmutableArray(),
        };
    }

    private TradeSearchItemProperty ResolveProperty(
        TradeSearchItemProperty property,
        PathOfExileTradeFilterCatalog catalog,
        bool isWeaponDraft,
        bool isArmourDraft)
    {
        if (!mappingCatalog.TryGet(property.Kind, out var mapping))
        {
            return Unresolved(property, "No reviewed provider mapping exists for this item property.");
        }

        if (!mapping.IsSupported)
        {
            return property with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                IsSearchable = false,
                NotSearchableReason = mapping.UnsupportedReason ??
                    "The Trade provider does not support this item property.",
            };
        }

        if (!string.IsNullOrWhiteSpace(property.DerivationUnsupportedReason))
        {
            return property with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                IsSearchable = false,
                NotSearchableReason = property.DerivationUnsupportedReason,
            };
        }

        var categoryMatches = property.Kind == TradeSearchItemPropertyKind.ChanceToBlock
            ? isArmourDraft || isWeaponDraft
            : IsDefensive(property.Kind) ? isArmourDraft : isWeaponDraft;
        if (!categoryMatches || !HasSuccessfulDerivationEvidence(property))
        {
            return property with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                IsSearchable = false,
                NotSearchableReason =
                    "This item property is not backed by successful derivation on a compatible item category.",
            };
        }

        var definitions = catalog.FindNumericFilterDefinitions(
            mapping.ProviderGroupId,
            mapping.ProviderFilterId);
        if (definitions.Count == 0)
        {
            return Unresolved(
                property,
                "The current Trade filter catalog does not contain the reviewed numeric item-property mapping.");
        }

        if (definitions.Count != 1)
        {
            return property with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Ambiguous,
                IsSearchable = false,
                NotSearchableReason =
                    "The current Trade filter catalog contains duplicate or conflicting definitions for this item property.",
            };
        }

        var definition = definitions[0];
        var reviewedPresentationMetadataMatches = !mapping.RequiresExactOfficialTextMatch ||
            string.Equals(definition.Text, mapping.ExpectedOfficialText, StringComparison.Ordinal);
        if (definition.SupportsMinMax != mapping.RequiresNumericMinMax ||
            !reviewedPresentationMetadataMatches)
        {
            return Unresolved(
                property,
                "The current Trade filter catalog entry is incompatible with the reviewed item-property mapping.");
        }

        return property with
        {
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
            IsSearchable = true,
            NotSearchableReason = null,
        };
    }

    private static bool IsWeaponDraft(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog catalog)
    {
        var category = draft.Base.AvailableCriteria.Category?.Category ??
            draft.Base.ActiveCriterion?.Category ??
            draft.Base.Category;
        return catalog.TryFindCategoryOption(category, out var option) &&
            option.Id.StartsWith(ProviderWeaponCategoryPrefix, StringComparison.Ordinal);
    }

    private static bool IsArmourDraft(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog catalog)
    {
        var category = draft.Base.AvailableCriteria.Category?.Category ??
            draft.Base.ActiveCriterion?.Category ??
            draft.Base.Category;
        return catalog.TryFindCategoryOption(category, out var option) &&
            option.Id.StartsWith(ProviderArmourCategoryPrefix, StringComparison.Ordinal);
    }

    private static bool IsDefensive(TradeSearchItemPropertyKind kind) => kind is
        TradeSearchItemPropertyKind.EnergyShield or
        TradeSearchItemPropertyKind.Armour or
        TradeSearchItemPropertyKind.EvasionRating or
        TradeSearchItemPropertyKind.Ward or
        TradeSearchItemPropertyKind.ChanceToBlock;

    private static bool HasSuccessfulDerivationEvidence(TradeSearchItemProperty property)
    {
        var names = property.SourceProperties
            .Select(source => source.NormalizedName?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
        var hasAps = names.Contains("attacks per second");
        return property.Kind switch
        {
            TradeSearchItemPropertyKind.TotalDps => hasAps &&
                names.Overlaps(["physical damage", "elemental damage", "chaos damage"]),
            TradeSearchItemPropertyKind.PhysicalDps => hasAps && names.Contains("physical damage"),
            TradeSearchItemPropertyKind.ElementalDps => hasAps && names.Contains("elemental damage"),
            TradeSearchItemPropertyKind.ChaosDps => hasAps && names.Contains("chaos damage"),
            TradeSearchItemPropertyKind.AttacksPerSecond => names.Contains("attacks per second"),
            TradeSearchItemPropertyKind.CriticalStrikeChance => names.Contains("critical strike chance"),
            TradeSearchItemPropertyKind.EnergyShield => names.Contains("energy shield"),
            TradeSearchItemPropertyKind.Armour => names.Contains("armour"),
            TradeSearchItemPropertyKind.EvasionRating => names.Contains("evasion rating"),
            TradeSearchItemPropertyKind.Ward => names.Contains("ward"),
            TradeSearchItemPropertyKind.ChanceToBlock => names.Contains("chance to block"),
            _ => false,
        };
    }

    private static TradeSearchItemProperty Unresolved(
        TradeSearchItemProperty property,
        string reason)
    {
        return property with
        {
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
            IsSearchable = false,
            NotSearchableReason = reason,
        };
    }
}

internal static class PathOfExileTradeSelectedItemPropertyMappingDiagnosticCodes
{
    public const string NotExactlyResolved = "POE_TRADE_SELECTED_ITEM_PROPERTY_NOT_EXACTLY_RESOLVED";
}

internal sealed record PathOfExileTradeSelectedItemPropertyMappingDiagnostic(
    string Code,
    string Message,
    int SourceItemPropertyIndex);

internal sealed record PathOfExileTradeSelectedItemPropertyMappingResult
{
    public bool IsSuccess { get; init; }

    public IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter> Filters { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeSelectedItemPropertyMappingDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeSelectedItemPropertyMappingResult Success(
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter> filters)
    {
        return new PathOfExileTradeSelectedItemPropertyMappingResult
        {
            IsSuccess = true,
            Filters = filters,
        };
    }

    public static PathOfExileTradeSelectedItemPropertyMappingResult Failure(
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyMappingDiagnostic> diagnostics)
    {
        return new PathOfExileTradeSelectedItemPropertyMappingResult { Diagnostics = diagnostics };
    }
}
