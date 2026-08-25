using PoEnhance.GameData;
using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Items.GameData;

public sealed record UniqueModifierBlockResolution
{
    public required int ParsedModifierIndex { get; init; }

    public bool IsResolved { get; init; }

    /// <summary>
    /// True when the parsed row carried no Unique modifier kind and was resolved solely from
    /// source blocks belonging to the already-resolved Unique identity. The parsed metadata,
    /// kind and origin remain untouched; semantic consumers may use the separately proven recovered
    /// classification below.
    /// </summary>
    public bool IsIdentityBoundRecovery { get; init; }

    /// <summary>
    /// Source classification proven by exact identity-bound recovery. These values are null for
    /// ordinary parsed Unique rows and for every unresolved or ambiguous recovery attempt.
    /// </summary>
    public ParsedModifierKind? RecoveredSourceKind { get; init; }

    public ParsedUniqueModifierOrigin? RecoveredSourceUniqueOrigin { get; init; }

    public bool HasRecoveredUniqueSourceSemantics =>
        IsIdentityBoundRecovery &&
        IsResolved &&
        RecoveredSourceKind == ParsedModifierKind.Unique &&
        RecoveredSourceUniqueOrigin is
            ParsedUniqueModifierOrigin.Ordinary or ParsedUniqueModifierOrigin.Foulborn;

    public bool IsEquivalentSourceSet { get; init; }

    public IReadOnlyList<UniqueModifierBlock> CatalogBlocks { get; init; } = [];

    public IReadOnlyList<string> FoulbornRelationshipIds { get; init; } = [];

    public IReadOnlyList<string> NormalCounterpartModifierIds { get; init; } = [];

    public IReadOnlyList<string> ModifierIds { get; init; } = [];

    public IReadOnlyList<string> StatIds { get; init; } = [];

    /// <summary>
    /// Locality evidence aligned by index with <see cref="StatIds"/>. Unknown is retained
    /// when GameData cannot prove one side; callers must not infer it from text or item class.
    /// </summary>
    public IReadOnlyList<ModifierLocality> StatLocalities { get; init; } = [];

    public IReadOnlyList<string> CanonicalSignatures { get; init; } = [];

    public UniqueModifierSourceSemantics SourceSemantics { get; init; }

    public IReadOnlyList<string> CandidatePoolMembershipIds { get; init; } = [];

    public IReadOnlyList<UniqueModifierOptionChoiceMembership> OptionChoiceMemberships { get; init; } = [];

    public IReadOnlyList<string> TextualOptionRangeAnnotations { get; init; } = [];

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];

    /// <summary>
    /// Optional display-only lines after a source-proven generated annotation was removed.
    /// Raw parsed lines are retained separately by the Trade draft.
    /// </summary>
    public IReadOnlyList<string> PresentationLines { get; init; } = [];

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }
}
