namespace PoEnhance.GameData;

public sealed record ItemPropertySemanticEvidence
{
    public ItemPropertySemanticEvidenceMethod Method { get; init; }

    public string? SourceId { get; init; }

    public string? ReviewVersion { get; init; }

    public string? ReviewReference { get; init; }

    public string? CompatibleSourceId { get; init; }

    public string? CompatibleSourceVersion { get; init; }
}
