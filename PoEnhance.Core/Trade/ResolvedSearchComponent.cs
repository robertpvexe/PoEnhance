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
    /// Complete copied text before terminal Advanced Copy metadata or textual option-range
    /// annotations are separated for semantic matching.
    /// </summary>
    public string RawCopiedText { get; init; } = string.Empty;

    /// <summary>
    /// Optional display-only text derived from catalog-backed export cleanup. The original
    /// clipboard text remains authoritative in <see cref="RawCopiedText"/> and provenance.
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

    public UniqueModifierSourceSemantics UniqueSourceSemantics { get; init; }

    public IReadOnlyList<string> UniqueCandidatePoolMembershipIds { get; init; } = [];

    public IReadOnlyList<UniqueModifierOptionChoiceMembership> UniqueOptionChoiceMemberships { get; init; } = [];

    /// <summary>
    /// Parser-separated textual option-range annotations accepted only after exact generated
    /// candidate resolution. These are proof metadata and never provider filter identities.
    /// </summary>
    public IReadOnlyList<string> UniqueTextualOptionRangeAnnotations { get; init; } = [];

    public IReadOnlyList<string> UniqueFoulbornRelationshipIds { get; init; } = [];

    public IReadOnlyList<string> UniqueNormalCounterpartModifierIds { get; init; } = [];

    public IReadOnlyList<string> UniqueSourceObservationIds { get; init; } = [];

    public string? UniqueResolutionDiagnosticCode { get; init; }

    /// <summary>
    /// True when the Unique source block was proven from the resolved Unique identity even though the
    /// copied metadata carried no Unique modifier kind. Provenance only: <see cref="UniqueOrigin"/>
    /// and the parsed metadata still report what the client actually emitted.
    /// </summary>
    public bool UsesIdentityBoundUniqueRecovery { get; init; }

    /// <summary>
    /// Source kind proven by resolution when the copied metadata did not carry one. Null whenever the
    /// raw client metadata already stated the kind, which keeps every existing row on its raw truth.
    /// </summary>
    public ParsedModifierKind? RecoveredSourceKind { get; init; }

    /// <summary>
    /// Unique source origin proven by resolution. Only set when the resolved source evidence actually
    /// proves the origin; never inferred from text or item identity.
    /// </summary>
    public ParsedUniqueModifierOrigin? RecoveredSourceUniqueOrigin { get; init; }

    /// <summary>
    /// Authoritative source classification for provider, query and UI decisions: what exact source
    /// evidence proved, falling back to what the client emitted. <see cref="ParsedKind"/> stays raw
    /// client truth and must be used for diagnostics and provenance only.
    /// </summary>
    public ParsedModifierKind ResolvedSourceKind => HasProvenRecoveredUniqueSourceSemantics
        ? RecoveredSourceKind!.Value
        : ParsedKind;

    /// <summary>
    /// Authoritative Unique source origin, resolved counterpart of <see cref="UniqueOrigin"/>.
    /// </summary>
    public ParsedUniqueModifierOrigin ResolvedSourceUniqueOrigin =>
        HasProvenRecoveredUniqueSourceSemantics
            ? RecoveredSourceUniqueOrigin!.Value
            : UniqueOrigin;

    /// <summary>
    /// True only when raw metadata or exact recovered evidence establishes a supported Unique
    /// source classification. Unknown/Unspecified rows remain false unless recovery proved both
    /// dimensions.
    /// </summary>
    public bool HasResolvedUniqueSourceSemantics =>
        ResolvedSourceKind == ParsedModifierKind.Unique &&
        ResolvedSourceUniqueOrigin is
            ParsedUniqueModifierOrigin.Ordinary or ParsedUniqueModifierOrigin.Foulborn;

    /// <summary>
    /// Exact source-block provenance shared by normal and identity-bound recovered Unique rows.
    /// Provider mapping, UI availability and serialization use this fail-closed proof.
    /// </summary>
    public bool HasExactUniqueSourceProvenance =>
        HasResolvedUniqueSourceSemantics &&
        ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
        (UniqueCatalogBlockIds.Count > 0 || UniqueFoulbornRelationshipIds.Count > 0) &&
        UniqueSourceObservationIds.Count > 0 &&
        string.IsNullOrWhiteSpace(UniqueResolutionDiagnosticCode) &&
        ResolvedStatIds.Count > 0;

    private bool HasProvenRecoveredUniqueSourceSemantics =>
        UsesIdentityBoundUniqueRecovery &&
        ParsedKind == ParsedModifierKind.Unknown &&
        UniqueOrigin == ParsedUniqueModifierOrigin.Unspecified &&
        RecoveredSourceKind == ParsedModifierKind.Unique &&
        RecoveredSourceUniqueOrigin is
            ParsedUniqueModifierOrigin.Ordinary or ParsedUniqueModifierOrigin.Foulborn &&
        ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
        (UniqueCatalogBlockIds.Count > 0 || UniqueFoulbornRelationshipIds.Count > 0) &&
        UniqueSourceObservationIds.Count > 0 &&
        string.IsNullOrWhiteSpace(UniqueResolutionDiagnosticCode) &&
        ResolvedStatIds.Count > 0;

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

    /// <summary>
    /// Identity-fixed numeric query value. When set, Search serializes an exact min=max constraint
    /// and Min/Max remain non-editable (for example fixed support gem level identity).
    /// Distinct from editable exact-initialized bounds, which use
    /// <see cref="SupportsValueBounds"/> with both <see cref="RequestedMinimum"/> and
    /// <see cref="RequestedMaximum"/> set to the observed value and leave
    /// <see cref="FixedQueryValue"/> null.
    /// </summary>
    public decimal? FixedQueryValue { get; init; }

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
