namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeItemIdentityMappingResult
{
    public bool IsSuccess => Identity is not null && Diagnostics.Count == 0;

    public PathOfExileTradeItemIdentity? Identity { get; init; }

    public IReadOnlyList<PathOfExileTradeItemIdentityMappingDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeItemIdentityMappingResult Success(
        PathOfExileTradeItemIdentity identity)
    {
        return new PathOfExileTradeItemIdentityMappingResult
        {
            Identity = identity,
        };
    }

    public static PathOfExileTradeItemIdentityMappingResult Failure(
        params PathOfExileTradeItemIdentityMappingDiagnostic[] diagnostics)
    {
        return new PathOfExileTradeItemIdentityMappingResult
        {
            Diagnostics = diagnostics,
        };
    }
}
