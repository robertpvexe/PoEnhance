using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

public sealed record SearchComponentSourceProvenance
{
    public required string ComponentId { get; init; }

    public int SourceModifierIndex { get; init; } = -1;

    public int SourceLineIndex { get; init; } = -1;

    public int SourceComponentIndex { get; init; }

    public string OriginalText { get; init; } = string.Empty;

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

    public string ProviderDomain { get; init; } = string.Empty;

    public bool IsCrafted { get; init; }

    public bool IsFractured { get; init; }

    public bool IsVeiled { get; init; }

    public bool IsUnveiled { get; init; }

    public bool IsBaseImplicit { get; init; }

    public bool IsHybrid { get; init; }

    public string? ResolvedModifierId { get; init; }

    public string? ResolvedModifierName { get; init; }

    public IReadOnlyList<string> ResolvedStatIds { get; init; } = [];

    public IReadOnlyList<decimal> ObservedNumericValues { get; init; } = [];

    public IReadOnlyList<ModifierSourceRollRange> OriginalSourceRollRanges { get; init; } = [];

    public IReadOnlyList<decimal> CanonicalNumericValues { get; init; } = [];

    public string? ProviderCanonicalSignature { get; init; }

    public ModifierBoundShape ValueBoundShape { get; init; }

    public ModifierBoundDirection DefaultBoundDirection { get; init; } = ModifierBoundDirection.Minimum;

    public IReadOnlyList<IReadOnlyList<string>> TranslationHandlers { get; init; } = [];

    public string? TranslationIdentity { get; init; }

    public string? ProviderIdentity { get; init; }

    public SearchComponentProviderResolutionStatus ProviderResolutionStatus { get; init; } =
        SearchComponentProviderResolutionStatus.NotResolved;
}
