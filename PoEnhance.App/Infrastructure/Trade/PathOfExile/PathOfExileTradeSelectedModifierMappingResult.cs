namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSelectedModifierMappingResult
{
    public bool IsSuccess => Diagnostics.Count == 0;

    public IReadOnlyList<PathOfExileTradeSelectedModifierFilter> Filters { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeSelectedModifierMappingDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeSelectedModifierMappingResult Success(
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter> filters)
    {
        return new PathOfExileTradeSelectedModifierMappingResult
        {
            Filters = filters,
        };
    }

    public static PathOfExileTradeSelectedModifierMappingResult Failure(
        IReadOnlyList<PathOfExileTradeSelectedModifierMappingDiagnostic> diagnostics)
    {
        return new PathOfExileTradeSelectedModifierMappingResult
        {
            Diagnostics = diagnostics,
        };
    }
}
