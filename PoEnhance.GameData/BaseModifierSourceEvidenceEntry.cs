namespace PoEnhance.GameData;

public sealed record BaseModifierSourceEvidenceEntry
{
    public string? ModifierId { get; init; }

    /// <summary>
    /// Weight reported by the source generator for the base tag set. Zero may denote
    /// conditional or influence-context evidence and is not a global disable flag.
    /// </summary>
    public int ReportedWeight { get; init; }

    public bool IsConditional { get; init; }

    /// <summary>
    /// Source generator bucket retained for context such as delve or influence-specific evidence.
    /// </summary>
    public string? SourceGenerationBucket { get; init; }
}
