namespace PoEnhance.Core.Trade;

public sealed record TradeSearchDraftResult
{
    public bool IsSuccess { get; init; }

    public TradeSearchDraft? Draft { get; init; }

    public IReadOnlyList<TradeSearchDraftDiagnostic> Diagnostics { get; init; } = [];

    public static TradeSearchDraftResult Success(TradeSearchDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new TradeSearchDraftResult
        {
            IsSuccess = true,
            Draft = draft,
        };
    }

    public static TradeSearchDraftResult Failure(params TradeSearchDraftDiagnostic[] diagnostics)
    {
        return new TradeSearchDraftResult
        {
            IsSuccess = false,
            Diagnostics = diagnostics,
        };
    }
}
