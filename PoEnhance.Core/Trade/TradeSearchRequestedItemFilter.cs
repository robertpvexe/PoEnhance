namespace PoEnhance.Core.Trade;

public sealed record TradeSearchRequestedItemFilter
{
    public required TradeSearchRequestedItemFilterKind Kind { get; init; }

    public required string Label { get; init; }

    public int? ObservedValue { get; init; }

    public required string CurrentText { get; init; }

    public int? RequestedMinimum { get; init; }

    public string MaximumText { get; init; } = string.Empty;

    public int? RequestedMaximum { get; init; }

    public string RedText { get; init; } = string.Empty;

    public string GreenText { get; init; } = string.Empty;

    public string BlueText { get; init; } = string.Empty;

    public string WhiteText { get; init; } = string.Empty;

    public int? RequestedRed { get; init; }

    public int? RequestedGreen { get; init; }

    public int? RequestedBlue { get; init; }

    public int? RequestedWhite { get; init; }

    public bool IsActive { get; init; }

    public TradeSearchRequestedItemFilterValidationStatus LocalValidationStatus { get; init; }

    public TradeSearchItemPropertyProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        TradeSearchItemPropertyProviderResolutionStatus.Unresolved;

    public string? DiagnosticReason { get; init; }

    public bool SupportsSocketColours =>
        Kind is TradeSearchRequestedItemFilterKind.Sockets or TradeSearchRequestedItemFilterKind.Links;

    /// <summary>
    /// Memory Strands and Intangibility are product Min-only UI filters even though Trade catalogs them as minMax.
    /// </summary>
    public bool IsMinimumOnly =>
        Kind is TradeSearchRequestedItemFilterKind.MemoryStrands
            or TradeSearchRequestedItemFilterKind.Intangibility;

    public bool HasRequestedConstraint =>
        RequestedMinimum.HasValue ||
        (!IsMinimumOnly && RequestedMaximum.HasValue) ||
        RequestedRed.HasValue ||
        RequestedGreen.HasValue ||
        RequestedBlue.HasValue ||
        RequestedWhite.HasValue;

    public string ColourText(TradeSearchRequestedItemFilterColour colour) => colour switch
    {
        TradeSearchRequestedItemFilterColour.Red => RedText,
        TradeSearchRequestedItemFilterColour.Green => GreenText,
        TradeSearchRequestedItemFilterColour.Blue => BlueText,
        TradeSearchRequestedItemFilterColour.White => WhiteText,
        _ => string.Empty,
    };

    public int? RequestedColour(TradeSearchRequestedItemFilterColour colour) => colour switch
    {
        TradeSearchRequestedItemFilterColour.Red => RequestedRed,
        TradeSearchRequestedItemFilterColour.Green => RequestedGreen,
        TradeSearchRequestedItemFilterColour.Blue => RequestedBlue,
        TradeSearchRequestedItemFilterColour.White => RequestedWhite,
        _ => null,
    };
}
