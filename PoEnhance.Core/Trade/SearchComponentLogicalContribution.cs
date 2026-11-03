namespace PoEnhance.Core.Trade;

/// <summary>
/// One independently selectable logical effect criterion (for example Fire Resistance) derived
/// from a compact physical source such as all-Elemental-Resistances. Physical provenance stays
/// shared; checkbox selection and Trade serialization are per logical criterion (Stage 03.11A).
/// </summary>
public sealed record SearchComponentLogicalContribution
{
    public required string ContributionId { get; init; }

    public required string LogicalStatId { get; init; }

    public required string DisplayText { get; init; }

    public required string CanonicalSignature { get; init; }

    public required string PhysicalSourceIdentity { get; init; }

    public IReadOnlyList<decimal> CanonicalNumericValues { get; init; } = [];

    public bool IsSelected { get; init; }

    public bool SupportsValueBounds { get; init; }

    public string? ValueBoundsUnsupportedReason { get; init; }

    public ModifierBoundShape ValueBoundShape { get; init; } = ModifierBoundShape.Unsupported;

    public ModifierBoundDirection DefaultBoundDirection { get; init; } = ModifierBoundDirection.Minimum;

    public decimal? RequestedMinimum { get; init; }

    public decimal? RequestedMaximum { get; init; }

    public IReadOnlyList<SearchFilterVariant> FilterVariants { get; init; } = [];

    public string? SelectedFilterVariantIdentity { get; init; }

    public string? RequestedFilterVariantIdentity { get; init; }

    public string? RequestedFilterVariantKind { get; init; }

    public SearchComponentProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        SearchComponentProviderResolutionStatus.NotResolved;

    public string? ProviderStatId { get; init; }

    public string? ProviderStatText { get; init; }

    public IReadOnlyList<string> ProviderStatAlternativeIds { get; init; } = [];

    public string? ProviderDiagnosticCode { get; init; }

    public string? ProviderDiagnosticMessage { get; init; }
}
