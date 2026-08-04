namespace PoEnhance.Core.Trade;

public sealed record TradeItemStateCriteria
{
    public TradeTriState Mirrored { get; init; } = TradeTriState.Any;

    public TradeTriState Corrupted { get; init; } = TradeTriState.Any;

    public TradeTriState Identified { get; init; } = TradeTriState.Any;

    public TradeTriState Fractured { get; init; } = TradeTriState.Any;

    public TradeTriState Get(TradeItemStateKind kind) => kind switch
    {
        TradeItemStateKind.Mirrored => Mirrored,
        TradeItemStateKind.Corrupted => Corrupted,
        TradeItemStateKind.Identified => Identified,
        TradeItemStateKind.Fractured => Fractured,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public TradeItemStateCriteria With(TradeItemStateKind kind, TradeTriState state) => kind switch
    {
        TradeItemStateKind.Mirrored => this with { Mirrored = state },
        TradeItemStateKind.Corrupted => this with { Corrupted = state },
        TradeItemStateKind.Identified => this with { Identified = state },
        TradeItemStateKind.Fractured => this with { Fractured = state },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
