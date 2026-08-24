namespace PoEnhance.DataTool.UniqueCorpusGate;

public sealed record UniqueCorpusGateOptions
{
    public bool DeduplicateLatestCapturePerItem { get; init; } = true;

    public bool Strict { get; init; }

    public int? MaxUnclassifiedClusterComponents { get; init; }

    public decimal? MaxSupportedCoverageDropPercent { get; init; }

    public string? BaselineReportPath { get; init; }
}

public sealed class UniqueCorpusGateReport
{
    public string Schema { get; init; } = UniqueCorpusGateSchema.ReportSchemaId;

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string InputDirectory { get; init; } = string.Empty;

    public UniqueCorpusGateIdentity Identity { get; init; } = new();

    public UniqueCorpusGateOutcomeCounts Outcomes { get; init; } = new();

    public IReadOnlyList<UniqueCorpusGateBreakdownCount> OutcomesByParsedKind { get; init; } = [];

    public IReadOnlyList<UniqueCorpusGateBreakdownCount> OutcomesByResolvedSourceKind { get; init; } = [];

    public IReadOnlyList<UniqueCorpusGateBreakdownCount> OutcomesBySourceFamily { get; init; } = [];

    public IReadOnlyList<UniqueCorpusGateBreakdownCount> FailureStages { get; init; } = [];

    public IReadOnlyList<UniqueCorpusGateCluster> RootCauseClusters { get; init; } = [];

    public IReadOnlyList<UniqueCorpusGateSignatureFamily> SignatureFamilies { get; init; } = [];

    public IReadOnlyList<UniqueCorpusGateCluster> RankedBacklog { get; init; } = [];

    public UniqueCorpusGateComparison? Comparison { get; init; }

    public UniqueCorpusGateStrictResult? StrictGate { get; init; }
}

public sealed class UniqueCorpusGateIdentity
{
    public int CaptureFileCount { get; init; }

    public int ParsedCaptureCount { get; init; }

    public int SkippedFileCount { get; init; }

    public IReadOnlyList<UniqueCorpusGateSkippedFile> SkippedFiles { get; init; } = [];

    public int DeduplicatedCaptureCount { get; init; }

    public int AnalyzedCaptureCount { get; init; }

    public int DistinctItemNameCount { get; init; }

    public int DistinctItemIdentityCount { get; init; }

    public int ModifierComponentCount { get; init; }

    public string DeduplicationPolicy { get; init; } =
        "keep-latest-capture-per-item-identity";
}

public sealed class UniqueCorpusGateSkippedFile
{
    public required string FileName { get; init; }

    public required string Reason { get; init; }
}

public sealed class UniqueCorpusGateOutcomeCounts
{
    public int Supported { get; init; }

    public int Ambiguous { get; init; }

    public int Unsupported { get; init; }

    public int Other { get; init; }

    public int Total { get; init; }

    public decimal SupportedPercent { get; init; }

    public decimal AmbiguousPercent { get; init; }

    public decimal UnsupportedPercent { get; init; }

    public decimal OtherPercent { get; init; }
}

public sealed class UniqueCorpusGateBreakdownCount
{
    public required string Key { get; init; }

    public int Supported { get; init; }

    public int Ambiguous { get; init; }

    public int Unsupported { get; init; }

    public int Other { get; init; }

    public int Total { get; init; }
}

public sealed class UniqueCorpusGateCluster
{
    public required string RootCauseKey { get; init; }

    public required string Stage { get; init; }

    public required string SourceFamily { get; init; }

    public IReadOnlyList<string> DiagnosticCodes { get; init; } = [];

    public int ComponentCount { get; init; }

    public int DistinctItemCount { get; init; }

    public int DistinctSignatureCount { get; init; }

    public IReadOnlyList<string> TopSignatures { get; init; } = [];

    public IReadOnlyList<string> TopItemNames { get; init; } = [];
}

public sealed class UniqueCorpusGateSignatureFamily
{
    public required string NormalizedSignature { get; init; }

    public required string SourceFamily { get; init; }

    public int ComponentCount { get; init; }

    public int DistinctItemCount { get; init; }

    public UniqueCorpusGateOutcomeCounts Outcomes { get; init; } = new();

    public IReadOnlyList<string> TopItemNames { get; init; } = [];

    public IReadOnlyList<string> RootCauseKeys { get; init; } = [];
}

public sealed class UniqueCorpusGateComparison
{
    public string Schema { get; init; } = UniqueCorpusGateSchema.ComparisonSchemaId;

    public UniqueCorpusGateOutcomeDelta Outcomes { get; init; } = new();

    public IReadOnlyList<UniqueCorpusGateClusterDelta> ClusterDeltas { get; init; } = [];

    public IReadOnlyList<string> IntroducedClusterKeys { get; init; } = [];

    public IReadOnlyList<string> ResolvedClusterKeys { get; init; } = [];

    public IReadOnlyList<UniqueCorpusGateRegression> SignatureFamilyRegressions { get; init; } = [];
}

public sealed class UniqueCorpusGateOutcomeDelta
{
    public int SupportedDelta { get; init; }

    public int AmbiguousDelta { get; init; }

    public int UnsupportedDelta { get; init; }

    public int OtherDelta { get; init; }

    public decimal SupportedPercentDelta { get; init; }
}

public sealed class UniqueCorpusGateClusterDelta
{
    public required string RootCauseKey { get; init; }

    public int BaselineComponentCount { get; init; }

    public int CurrentComponentCount { get; init; }

    public int ComponentDelta { get; init; }

    public int BaselineDistinctItemCount { get; init; }

    public int CurrentDistinctItemCount { get; init; }

    public int DistinctItemDelta { get; init; }
}

public sealed class UniqueCorpusGateRegression
{
    public required string NormalizedSignature { get; init; }

    public required string SourceFamily { get; init; }

    public int BaselineSupported { get; init; }

    public int CurrentSupported { get; init; }

    public int BaselineFailed { get; init; }

    public int CurrentFailed { get; init; }
}

public sealed class UniqueCorpusGateStrictResult
{
    public bool Passed { get; init; }

    public IReadOnlyList<string> Failures { get; init; } = [];
}

public static class UniqueCorpusGateSchema
{
    public const string ReportSchemaId = "poenhance.unique-corpus-gate.v1";

    public const string ComparisonSchemaId = "poenhance.unique-corpus-gate.comparison.v1";
}

public static class UniqueCorpusGateStages
{
    public const string None = "none";

    public const string ParserSourceShape = "parser-source-shape";

    public const string UniqueSourceMechanics = "unique-source-mechanics";

    public const string VersionBlockMatching = "version-source-block-matching";

    public const string ProvenanceGate = "provenance-gate";

    public const string ProviderDiscovery = "provider-discovery";

    public const string ProviderAmbiguity = "provider-ambiguity-or-equivalent-set";

    public const string BoundProjection = "bound-projection";

    public const string Serialization = "serialization";

    public const string Unclassified = "unknown-unclassified";
}

public static class UniqueCorpusGateOutcomes
{
    public const string Supported = "Supported";

    public const string Ambiguous = "Ambiguous";

    public const string Unsupported = "Unsupported";

    public const string Other = "Other";
}

public static class UniqueCorpusGateSourceFamilies
{
    public const string Unique = "Unique";

    public const string Implicit = "Implicit";

    public const string CorruptedImplicit = "CorruptedImplicit";

    public const string Enchantment = "Enchantment";

    public const string Other = "Other";
}
