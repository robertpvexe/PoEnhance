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

    /// <summary>
    /// Optional display-only text derived from catalog-backed export cleanup. The original
    /// clipboard text remains authoritative in <see cref="OriginalText"/> and provenance.
    /// </summary>
    public string? PresentationText { get; init; }

    public string CanonicalSignature { get; init; } = string.Empty;

    public ParsedModifierKind ParsedKind { get; init; }

    public ParsedImplicitModifierOrigin ImplicitOrigin { get; init; }

    public ParsedUniqueModifierOrigin UniqueOrigin { get; init; }

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

    public bool IsUnveiled { get; init; }

    public bool IsBaseImplicit { get; init; }

    public SearchComponentBaseImplicitProvenance? BaseImplicitProvenance { get; init; }

    public string? GuaranteedExactBaseName { get; init; }

    public ModifierCandidateResolutionStatus? ResolutionStatus { get; init; }

    public string? ResolvedModifierId { get; init; }

    public string? ResolvedModifierName { get; init; }

    public IReadOnlyList<string> ResolvedStatIds { get; init; } = [];

    public IReadOnlyList<ModifierLocality> ResolvedStatLocalities { get; init; } = [];

    /// <summary>
    /// Provider-neutral, mechanically backed renderings that may be used for provider
    /// candidate discovery. They are evidence inputs, not provider identities.
    /// </summary>
    public IReadOnlyList<string> ProviderSearchSignatures { get; init; } = [];

    public IReadOnlyList<string> UniqueCatalogBlockIds { get; init; } = [];

    public IReadOnlyList<string> UniqueSourceObservationIds { get; init; } = [];

    public string? UniqueResolutionDiagnosticCode { get; init; }

    public bool IsSearchable { get; init; }

    public string? NotSearchableReason { get; init; }

    public decimal? RequestedMinimum { get; init; }

    public decimal? RequestedMaximum { get; init; }

    public bool SupportsValueBounds { get; init; }

    public string? ValueBoundsUnsupportedReason { get; init; }

    public ModifierBoundShape ValueBoundShape { get; init; }

    public IReadOnlyList<decimal> ObservedNumericValues { get; init; } = [];

    public IReadOnlyList<ModifierSourceRollRange> OriginalSourceRollRanges { get; init; } = [];

    public IReadOnlyList<decimal> CanonicalNumericValues { get; init; } = [];

    public IReadOnlyList<decimal> ProviderFallbackNumericValues { get; init; } = [];

    public string? ProviderCanonicalSignature { get; init; }

    public IReadOnlyList<IReadOnlyList<string>> ValueBoundTranslationHandlers { get; init; } = [];

    public string? ValueBoundTranslationIdentity { get; init; }

    public StatTranslationRecognitionEvidence? TranslationRecognition { get; init; }

    public ModifierBoundDirection DefaultBoundDirection { get; init; } = ModifierBoundDirection.Minimum;

    public IReadOnlyList<SearchFilterVariant> FilterVariants { get; init; } = [];

    public string? SelectedFilterVariantIdentity { get; init; }

    public string? RequestedFilterVariantIdentity { get; init; }

    public string? RequestedFilterVariantKind { get; init; }

    public bool IsSelected { get; init; }

    public SearchComponentProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        SearchComponentProviderResolutionStatus.NotResolved;

    public string? ProviderStatId { get; init; }

    public string? ProviderStatText { get; init; }

    public IReadOnlyList<string> ProviderStatAlternativeIds { get; init; } = [];

    public IReadOnlyList<string> ProviderCandidateStatIds { get; init; } = [];

    public string? ProviderDiagnosticCode { get; init; }

    public string? ProviderDiagnosticMessage { get; init; }

    public IReadOnlyList<SearchComponentProviderDomainEvidence> ProviderDomainEvidence { get; init; } = [];

    public IReadOnlyList<SearchComponentSourceProvenance> Sources { get; init; } = [];

    public int SourceCount => Sources.Count == 0 ? 1 : Sources.Count;

    /// <summary>
    /// Multiple retained sources proved the same canonical effect. They are provenance,
    /// not separate user-selectable effects.
    /// </summary>
    public bool IsEquivalentSourceSet { get; init; }

    public IReadOnlyList<SearchComponentContributor> Contributors { get; init; } = [];

    public SearchComponentContributorProjection ContributorProjection { get; init; }
}
