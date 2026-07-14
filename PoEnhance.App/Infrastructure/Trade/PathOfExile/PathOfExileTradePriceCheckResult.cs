using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradePriceCheckResult
{
    public bool IsSuccess { get; init; }

    public PathOfExileTradePriceCheckStage Stage { get; init; }

    public string? SearchQueryId { get; init; }

    public int? ProviderTotal { get; init; }

    public bool? Inexact { get; init; }

    public TradeSearchDraft? EffectiveDraft { get; init; }

    public IReadOnlyList<PathOfExileTradeFetchedOffer> Offers { get; init; } = [];

    public PathOfExileTradeRateLimitSnapshot? CatalogRateLimitSnapshot { get; init; }

    public PathOfExileTradeRateLimitSnapshot? SearchRateLimitSnapshot { get; init; }

    public PathOfExileTradeRateLimitSnapshot? FetchRateLimitSnapshot { get; init; }

    public IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> Diagnostics { get; init; } = [];

    public bool IsCancelled { get; init; }

    public bool IsTimeout { get; init; }
}
