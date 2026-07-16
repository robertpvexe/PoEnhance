using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

public sealed record ResolvedSearchComponent
{
    public required string ComponentId { get; init; }

    public int SourceModifierIndex { get; init; } = -1;

    public int SourceLineIndex { get; init; } = -1;

    public int SourceComponentIndex { get; init; }

    public string OriginalText { get; init; } = string.Empty;

    public string CanonicalSignature { get; init; } = string.Empty;

    public ParsedModifierKind ParsedKind { get; init; }

    public ModifierGenerationType? GenerationType { get; init; }

    public ModifierLocality Locality { get; init; } = ModifierLocality.Unknown;

    public ModifierStatMappingProofStatus StatMappingProof { get; init; }

    public ItemPropertySemanticDescriptor? ReviewedItemPropertySemantic { get; init; }

    public string? ParsedModifierName { get; init; }

    public string? CategoryText { get; init; }

    public int? Tier { get; init; }

    public int? Rank { get; init; }

    public bool IsCrafted { get; init; }

    public bool IsFractured { get; init; }

    public bool IsVeiled { get; init; }

    public bool IsBaseImplicit { get; init; }

    public string? GuaranteedExactBaseName { get; init; }

    public ModifierCandidateResolutionStatus? ResolutionStatus { get; init; }

    public string? ResolvedModifierId { get; init; }

    public string? ResolvedModifierName { get; init; }

    public IReadOnlyList<string> ResolvedStatIds { get; init; } = [];

    public bool IsSearchable { get; init; }

    public string? NotSearchableReason { get; init; }

    public decimal? RequestedMinimum { get; init; }

    public decimal? RequestedMaximum { get; init; }

    public bool SupportsValueBounds { get; init; }

    public string? ValueBoundsUnsupportedReason { get; init; }

    public ModifierBoundShape ValueBoundShape { get; init; }

    public IReadOnlyList<decimal> ObservedNumericValues { get; init; } = [];

    public IReadOnlyList<decimal> CanonicalNumericValues { get; init; } = [];

    public IReadOnlyList<IReadOnlyList<string>> ValueBoundTranslationHandlers { get; init; } = [];

    public string? ValueBoundTranslationIdentity { get; init; }

    public ModifierBoundDirection DefaultBoundDirection { get; init; } = ModifierBoundDirection.Minimum;

    public IReadOnlyList<SearchFilterVariant> FilterVariants { get; init; } = [];

    public string? SelectedFilterVariantIdentity { get; init; }

    public bool IsSelected { get; init; }

    public SearchComponentProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        SearchComponentProviderResolutionStatus.NotResolved;

    public string? ProviderStatId { get; init; }

    public string? ProviderStatText { get; init; }

    public IReadOnlyList<string> ProviderCandidateStatIds { get; init; } = [];

    public string? ProviderDiagnosticCode { get; init; }

    public string? ProviderDiagnosticMessage { get; init; }

    public IReadOnlyList<SearchComponentProviderDomainEvidence> ProviderDomainEvidence { get; init; } = [];

    public IReadOnlyList<SearchComponentSourceProvenance> Sources { get; init; } = [];

    public int SourceCount => Sources.Count == 0 ? 1 : Sources.Count;

    public IReadOnlyList<SearchComponentContributor> Contributors { get; init; } = [];

    public SearchComponentContributorProjection ContributorProjection { get; init; }
}
