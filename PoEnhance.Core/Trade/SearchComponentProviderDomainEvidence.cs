using PoEnhance.Core.Items.GameData;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

public sealed record SearchComponentProviderDomainEvidence
{
    public required string ProviderDomain { get; init; }

    public required string ModifierId { get; init; }

    public ModifierGenerationType GenerationType { get; init; }

    public ModifierLocality Locality { get; init; } = ModifierLocality.Unknown;

    public string? SourceGenerationType { get; init; }

    public bool IsSourceExact { get; init; }

    public bool IsProjectedDomain { get; init; }

    public int EvidenceStrength { get; init; }

    public string? ItemBaseId { get; init; }

    public string? ItemClass { get; init; }

    public string? MatchedTag { get; init; }

    public string? ApplicabilityReasonCode { get; init; }

    public required string ApplicabilityReason { get; init; }
}
