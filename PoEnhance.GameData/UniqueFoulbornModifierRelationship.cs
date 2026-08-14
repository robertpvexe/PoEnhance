namespace PoEnhance.GameData;

public sealed record UniqueFoulbornModifierRelationship
{
    public string? Id { get; init; }

    public string? ItemName { get; init; }

    public string? CanonicalItemName { get; init; }

    public string? CanonicalIdentityKey { get; init; }

    public string? IdentityNormalizationRule { get; init; }

    public string? IdentityLinkageEvidence { get; init; }

    public string? CurrentHistoryDecisionReason { get; init; }

    public string? UniqueItemId { get; init; }

    public string? NormalModifierId { get; init; }

    public string? FoulbornModifierId { get; init; }

    public IReadOnlyList<string> NormalModifierBlockIds { get; init; } = [];

    public UniqueItemVersionRole AppliesToRole { get; init; }

    public string? SourceObservationId { get; init; }

    public UniqueFoulbornModifierRelationshipStatus Status { get; init; }

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }
}
