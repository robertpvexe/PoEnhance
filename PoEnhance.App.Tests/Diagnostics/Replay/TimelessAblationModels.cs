namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal sealed class TimelessAblationReport
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public string? GameDataVersion { get; init; }

    public string? GameDataSha256 { get; init; }

    public IReadOnlyList<TimelessInputDiffEntry> InputDiffs { get; init; } = [];

    public IReadOnlyList<TimelessParserDiffEntry> ParserDiffs { get; init; } = [];

    public IReadOnlyList<TimelessAblationRow> AblationRows { get; init; } = [];

    public IReadOnlyList<TimelessAblationRow> ReverseAdditionRows { get; init; } = [];

    public TimelessMinimalTrigger MinimalTrigger { get; init; } = new();

    public TimelessCorpusBlastReport? CorpusBlast { get; init; }

    public IReadOnlyList<TimelessAblationRow> IntentionalMismatchControls { get; init; } = [];
}

internal sealed class TimelessInputDiffEntry
{
    public required string ItemName { get; init; }

    public int RealLineCount { get; init; }

    public int FixtureLineCount { get; init; }

    public IReadOnlyList<string> StructuralDifferences { get; init; } = [];

    public Dictionary<string, string> FeatureFlags { get; init; } = new();
}

internal sealed class TimelessParserDiffEntry
{
    public required string ItemName { get; init; }

    public int RealUniqueModifierCount { get; init; }

    public int FixtureUniqueModifierCount { get; init; }

    public int RealSeedValueLineCount { get; init; }

    public int FixtureSeedValueLineCount { get; init; }

    public IReadOnlyList<string> RealSeedValueLines { get; init; } = [];

    public IReadOnlyList<string> FixtureSeedValueLines { get; init; } = [];

    public string? FirstLikelyInfluencingDifference { get; init; }
}

internal sealed class TimelessAblationRow
{
    public required string ItemName { get; init; }

    public required string TransformName { get; init; }

    public string Direction { get; init; } = "baseline";

    public string? InputSha256 { get; init; }

    public int ParserUniqueModifierCount { get; init; }

    public int ParserSeedValueLineCount { get; init; }

    public IReadOnlyList<string> ParserSeedValueLines { get; init; } = [];

    public string? UniqueIdentityStatus { get; init; }

    public string? UniqueIdentityDiagnostic { get; init; }

    public string? SeedBlockDiagnostic { get; init; }

    public bool HasUnscalableValue { get; init; }

    public IReadOnlyList<string> ModifierIds { get; init; } = [];

    public IReadOnlyList<string> StatIds { get; init; } = [];

    public bool? IsSearchable { get; init; }

    public required string Outcome { get; init; }

    public string? InvalidReason { get; init; }
}

internal sealed class TimelessMinimalTrigger
{
    public IReadOnlyList<string> Features { get; init; } = [];

    /// <summary>
    /// Current post-A.5.6 evidence classification (not the historical pre-fix strength).
    /// </summary>
    public string EvidenceStrength { get; init; } = "mixed";

    public string FamilyClassification { get; init; } = "MULTIPLE_SUBTYPES";

    /// <summary>
    /// A.5.5 historical root-cause identity retained for provenance after the production fix.
    /// </summary>
    public string? HistoricalTrigger { get; init; }

    /// <summary>
    /// A.5.5 historical evidence strength (bidirectional before the fix). Distinct from
    /// <see cref="EvidenceStrength"/>, which describes current runtime behavior.
    /// </summary>
    public string? HistoricalEvidenceStrength { get; init; }

    /// <summary>
    /// Factual summary of current Timeless seed behavior after A.5.6.
    /// </summary>
    public string? PostFixBehavior { get; init; }

    public IReadOnlyList<TimelessTriggerTransformStat> ForwardExactRestoringTransforms { get; init; } = [];

    public IReadOnlyList<TimelessTriggerTransformStat> ReverseMismatchCausingTransforms { get; init; } = [];

    public IReadOnlyList<TimelessTriggerTransformStat> AnnotationExactPreservingTransforms { get; init; } = [];

    public string? EarliestResponsibleLayer { get; init; }

    public string? DiagnosticTransition { get; init; }

    public string? FutureClassInvariant { get; init; }

    public string? ProposedGenericRepairLayer { get; init; }

    public string? ProposedGenericRepairBehavior { get; init; }
}

internal sealed class TimelessTriggerTransformStat
{
    public required string TransformName { get; init; }

    public int AffectedItemCount { get; init; }

    public IReadOnlyList<string> ItemNames { get; init; } = [];
}

internal sealed class TimelessCorpusBlastReport
{
    public int TimelessAffectedItemCount { get; init; }

    public int BroaderAffectedItemCount { get; init; }

    public int BroaderAffectedModifierCount { get; init; }

    public IReadOnlyList<string> TopExampleItems { get; init; } = [];

    public int VersionMismatchRowCount { get; init; }

    public int VersionMismatchRowsSharingTrigger { get; init; }

    public int VersionMismatchRowsNotSharingTrigger { get; init; }

    public IReadOnlyList<TimelessStructuralSubclass> StructuralSubclasses { get; init; } = [];
}

internal sealed class TimelessStructuralSubclass
{
    public required string ClassId { get; init; }

    public required string Description { get; init; }

    public int ItemCount { get; init; }

    public int ModifierCount { get; init; }

    public IReadOnlyList<string> ExampleItems { get; init; } = [];
}
