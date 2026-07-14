namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSelectedModifierFilter
{
    public required int SourceIndex { get; init; }

    public required string StatId { get; init; }

    public required string OriginalText { get; init; }

    public string NormalizedItemTemplate { get; init; } = string.Empty;

    public IReadOnlyList<decimal> ExtractedNumericValues { get; init; } = [];
}
