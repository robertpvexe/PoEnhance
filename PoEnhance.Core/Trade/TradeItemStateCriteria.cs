namespace PoEnhance.Core.Trade;

public sealed record TradeItemStateCriteria
{
    public TradeTriState Mirrored { get; init; } = TradeTriState.Any;

    public TradeTriState Corrupted { get; init; } = TradeTriState.Any;

    public TradeTriState Identified { get; init; } = TradeTriState.Any;

    public TradeTriState Fractured { get; init; } = TradeTriState.Any;

    public TradeTriState Synthesised { get; init; } = TradeTriState.Any;

    public TradeTriState Split { get; init; } = TradeTriState.Any;

    public TradeTriState Crafted { get; init; } = TradeTriState.Any;

    public TradeTriState Veiled { get; init; } = TradeTriState.Any;

    public TradeTriState Foreseeing { get; init; } = TradeTriState.Any;

    public TradeTriState Crucible { get; init; } = TradeTriState.Any;

    public TradeTriState Foulborn { get; init; } = TradeTriState.Any;

    public TradeTriState Vestigial { get; init; } = TradeTriState.Any;

    public TradeTriState Get(TradeItemStateKind kind) => kind switch
    {
        TradeItemStateKind.Mirrored => Mirrored,
        TradeItemStateKind.Corrupted => Corrupted,
        TradeItemStateKind.Identified => Identified,
        TradeItemStateKind.Fractured => Fractured,
        TradeItemStateKind.Synthesised => Synthesised,
        TradeItemStateKind.Split => Split,
        TradeItemStateKind.Crafted => Crafted,
        TradeItemStateKind.Veiled => Veiled,
        TradeItemStateKind.Foreseeing => Foreseeing,
        TradeItemStateKind.Crucible => Crucible,
        TradeItemStateKind.Foulborn => Foulborn,
        TradeItemStateKind.Vestigial => Vestigial,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public TradeItemStateCriteria With(TradeItemStateKind kind, TradeTriState state) => kind switch
    {
        TradeItemStateKind.Mirrored => this with { Mirrored = state },
        TradeItemStateKind.Corrupted => this with { Corrupted = state },
        TradeItemStateKind.Identified => this with { Identified = state },
        TradeItemStateKind.Fractured => this with { Fractured = state },
        TradeItemStateKind.Synthesised => this with { Synthesised = state },
        TradeItemStateKind.Split => this with { Split = state },
        TradeItemStateKind.Crafted => this with { Crafted = state },
        TradeItemStateKind.Veiled => this with { Veiled = state },
        TradeItemStateKind.Foreseeing => this with { Foreseeing = state },
        TradeItemStateKind.Crucible => this with { Crucible = state },
        TradeItemStateKind.Foulborn => this with { Foulborn = state },
        TradeItemStateKind.Vestigial => this with { Vestigial = state },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
