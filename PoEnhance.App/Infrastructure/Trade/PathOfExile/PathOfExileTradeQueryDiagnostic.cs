namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeQueryDiagnostic(
    string Code,
    string Message)
{
    public string? ProviderStatId { get; init; }

    public string? ProviderGroupId { get; init; }

    public string? ProviderFilterId { get; init; }

    public bool IsCatalogWide { get; init; }
}
