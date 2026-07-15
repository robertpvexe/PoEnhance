using PoEnhance.Core.Items.GameData;

namespace PoEnhance.Core.Trade;

/// <summary>
/// Editable state for one direct source of an aggregated canonical component.
/// Contributors are deliberately flat; they cannot contain nested contributors.
/// </summary>
public sealed record SearchComponentContributor
{
    public required string ContributorId { get; init; }

    public required SearchComponentSourceProvenance Source { get; init; }

    public string DisplayText { get; init; } = string.Empty;

    public bool IsSelected { get; init; }

    public decimal? RequestedMinimum { get; init; }

    public decimal? RequestedMaximum { get; init; }

    public bool SupportsValueBounds { get; init; }

    public string? ValueBoundsUnsupportedReason { get; init; }

    public ModifierBoundShape ValueBoundShape { get; init; }

    public ModifierBoundDirection DefaultBoundDirection { get; init; } = ModifierBoundDirection.Minimum;

    public SearchComponentProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        SearchComponentProviderResolutionStatus.NotResolved;

    public string? ProviderIdentity { get; init; }

    public string? ProviderDiagnosticCode { get; init; }

    public string? ProviderDiagnosticMessage { get; init; }
}
