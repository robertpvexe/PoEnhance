using System.Collections.Immutable;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeRequestedItemFilterResolver
{
    private readonly PathOfExileTradeRequestedItemFilterMappingCatalog mappingCatalog;

    public PathOfExileTradeRequestedItemFilterResolver()
        : this(new PathOfExileTradeRequestedItemFilterMappingResourceLoader().LoadDefaultOrThrow())
    {
    }

    internal PathOfExileTradeRequestedItemFilterResolver(
        PathOfExileTradeRequestedItemFilterMappingCatalog mappingCatalog)
    {
        this.mappingCatalog = mappingCatalog ?? throw new ArgumentNullException(nameof(mappingCatalog));
    }

    public TradeSearchDraft Resolve(TradeSearchDraft draft, PathOfExileTradeFilterCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(catalog);
        return draft with
        {
            RequestedItemFilters = draft.RequestedItemFilters
                .Select(filter => ResolveFilter(filter, catalog))
                .ToImmutableArray(),
        };
    }

    public TradeSearchDraft MarkCatalogUnavailable(TradeSearchDraft draft, string reason)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return draft with
        {
            RequestedItemFilters = draft.RequestedItemFilters
                .Select(filter => filter with
                {
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
                    DiagnosticReason = filter.LocalValidationStatus ==
                        TradeSearchRequestedItemFilterValidationStatus.Valid
                            ? reason
                            : filter.DiagnosticReason,
                })
                .ToImmutableArray(),
        };
    }

    public PathOfExileTradeSelectedRequestedItemFilterMappingResult MapSelected(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog catalog)
    {
        var verified = Resolve(draft, catalog);
        var filters = new List<PathOfExileTradeSelectedRequestedItemFilter>();
        var diagnostics = new List<string>();
        foreach (var filter in verified.RequestedItemFilters.Where(filter => filter.IsActive))
        {
            if (filter.ProviderResolutionStatus != TradeSearchItemPropertyProviderResolutionStatus.Exact ||
                filter.LocalValidationStatus != TradeSearchRequestedItemFilterValidationStatus.Valid ||
                !filter.RequestedMinimum.HasValue ||
                !mappingCatalog.TryGet(filter.Kind, out var mapping))
            {
                diagnostics.Add(filter.DiagnosticReason ??
                    $"Selected requested filter '{filter.Label}' has no exact verified provider mapping.");
                continue;
            }

            filters.Add(new PathOfExileTradeSelectedRequestedItemFilter
            {
                SourceKind = filter.Kind,
                ProviderGroupId = mapping.ProviderGroupId,
                ProviderFilterId = mapping.ProviderFilterId,
                MinimumValue = filter.RequestedMinimum.Value,
            });
        }

        return diagnostics.Count == 0
            ? PathOfExileTradeSelectedRequestedItemFilterMappingResult.Success(filters)
            : PathOfExileTradeSelectedRequestedItemFilterMappingResult.Failure(diagnostics);
    }

    private TradeSearchRequestedItemFilter ResolveFilter(
        TradeSearchRequestedItemFilter filter,
        PathOfExileTradeFilterCatalog catalog)
    {
        if (filter.LocalValidationStatus != TradeSearchRequestedItemFilterValidationStatus.Valid ||
            !filter.RequestedMinimum.HasValue)
        {
            return filter with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
            };
        }

        if (!mappingCatalog.TryGet(filter.Kind, out var mapping))
        {
            return Unsupported(filter, "No reviewed provider mapping exists for this requested item filter.");
        }

        if (filter.RequestedMinimum < mapping.MinimumSupportedValue ||
            filter.RequestedMinimum > mapping.MaximumSupportedValue)
        {
            return Unsupported(
                filter,
                $"{filter.Label} must be between {mapping.MinimumSupportedValue} and {mapping.MaximumSupportedValue} for this Trade provider.");
        }

        var definitions = catalog.FindNumericFilterDefinitions(
            mapping.ProviderGroupId,
            mapping.ProviderFilterId);
        if (definitions.Count != 1)
        {
            return filter with
            {
                ProviderResolutionStatus = definitions.Count > 1
                    ? TradeSearchItemPropertyProviderResolutionStatus.Ambiguous
                    : TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
                DiagnosticReason = definitions.Count > 1
                    ? "The official Trade catalog contains conflicting definitions for this requested item filter."
                    : "The official Trade catalog does not contain this reviewed requested item filter.",
            };
        }

        var definition = definitions[0];
        if (definition.SupportsMinMax != mapping.RequiresNumericMinMax ||
            !string.Equals(definition.Text, mapping.ExpectedOfficialText, StringComparison.Ordinal))
        {
            return Unsupported(
                filter,
                "The official Trade catalog definition is incompatible with the reviewed requested item filter mapping.");
        }

        return filter with
        {
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
            DiagnosticReason = null,
        };
    }

    private static TradeSearchRequestedItemFilter Unsupported(
        TradeSearchRequestedItemFilter filter,
        string reason)
    {
        return filter with
        {
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
            DiagnosticReason = reason,
        };
    }
}

internal sealed record PathOfExileTradeSelectedRequestedItemFilterMappingResult
{
    public bool IsSuccess { get; init; }

    public IReadOnlyList<PathOfExileTradeSelectedRequestedItemFilter> Filters { get; init; } = [];

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public static PathOfExileTradeSelectedRequestedItemFilterMappingResult Success(
        IReadOnlyList<PathOfExileTradeSelectedRequestedItemFilter> filters) =>
        new() { IsSuccess = true, Filters = filters };

    public static PathOfExileTradeSelectedRequestedItemFilterMappingResult Failure(
        IReadOnlyList<string> diagnostics) =>
        new() { Diagnostics = diagnostics };
}
