using System.Collections.Immutable;
using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Trade;

public sealed record TradeSearchItemProperty
{
    public required TradeSearchItemPropertyKind Kind { get; init; }

    public required string Label { get; init; }

    public string? CalculationBasisLabel { get; init; }

    public required decimal ObservedValue { get; init; }

    public decimal? RequestedMinimum { get; init; }

    public decimal? RequestedMaximum { get; init; }

    public bool IsSelected { get; init; }

    public TradeSearchItemPropertyProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        TradeSearchItemPropertyProviderResolutionStatus.Unresolved;

    public bool IsSearchable { get; init; }

    public string? NotSearchableReason { get; init; }

    public string? DerivationUnsupportedReason { get; init; }

    public ImmutableArray<ParsedItemProperty> SourceProperties { get; init; } = [];
}
