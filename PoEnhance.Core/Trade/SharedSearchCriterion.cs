namespace PoEnhance.Core.Trade;

/// <summary>
/// One draft-level search criterion that may appear as mirrored UI under several modifier rows
/// (for example Pseudo Total Elemental Resistance under Fire/Cold/Lightning) while remaining a
/// single selection identity with one editable Min/Max and one emitted Trade filter.
/// </summary>
public sealed record SharedSearchCriterion
{
    public const string TotalElementalResistanceId = "shared:pseudo.pseudo_total_elemental_resistance";

    public const string TotalElementalResistanceProviderStatId =
        "pseudo.pseudo_total_elemental_resistance";

    public const string TotalElementalResistanceDisplayText = "+#% total Elemental Resistance";

    public required string CriterionId { get; init; }

    public required string ProviderStatId { get; init; }

    public required string DisplayText { get; init; }

    public bool IsSelected { get; init; }

    public bool SupportsValueBounds { get; init; } = true;

    public ModifierBoundDirection DefaultBoundDirection { get; init; } = ModifierBoundDirection.Minimum;

    public decimal? RequestedMinimum { get; init; }

    public decimal? RequestedMaximum { get; init; }

    public SearchComponentProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        SearchComponentProviderResolutionStatus.NotResolved;

    public string? ProviderDiagnosticMessage { get; init; }
}
