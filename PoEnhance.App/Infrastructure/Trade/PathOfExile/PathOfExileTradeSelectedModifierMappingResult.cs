namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSelectedModifierMappingResult
{
    public bool IsSuccess => Diagnostics.Count == 0;

    public IReadOnlyList<PathOfExileTradeSelectedModifierFilter> Filters { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeSelectedModifierMappingDiagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeStatResolutionTrace> Traces { get; init; } = [];

    public static PathOfExileTradeSelectedModifierMappingResult Success(
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter> filters,
        IReadOnlyList<PathOfExileTradeStatResolutionTrace>? traces = null)
    {
        return new PathOfExileTradeSelectedModifierMappingResult
        {
            Filters = filters,
            Traces = traces ?? [],
        };
    }

    public static PathOfExileTradeSelectedModifierMappingResult Failure(
        IReadOnlyList<PathOfExileTradeSelectedModifierMappingDiagnostic> diagnostics,
        IReadOnlyList<PathOfExileTradeStatResolutionTrace>? traces = null)
    {
        return new PathOfExileTradeSelectedModifierMappingResult
        {
            Diagnostics = diagnostics,
            Traces = traces ?? [],
        };
    }
}
