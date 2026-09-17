using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoEnhance.DataTool.UniqueCorpusGate;

public static class UniqueCorpusGateSchema
{
    public const string ReportSchemaId = "poenhance.unique-corpus-gate.v1";

    public const string ComparisonSchemaId = "poenhance.unique-corpus-gate.comparison.v1";

    public const string ObservationalBaselineSchemaId = "poenhance.unique-corpus-gate.observational-baseline.v2";

    public const string ObservationalDiffSchemaId = "poenhance.unique-corpus-gate.observational-diff.v2";

    public const string ToolVersion = "A.5.2";
}

public sealed class UniqueCorpusGateObservationalBaseline
{
    public string Schema { get; init; } = UniqueCorpusGateSchema.ObservationalBaselineSchemaId;

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string ToolVersion { get; init; } = UniqueCorpusGateSchema.ToolVersion;

    public string? SourceCorpusDirectory { get; init; }

    public int SourceCorpusFileCount { get; init; }

    public int ItemCount { get; init; }

    public int ModifierRowCount { get; init; }

    public string? GameDataSha { get; init; }

    public string DeduplicationPolicy { get; init; } = "keep-latest-capture-per-item-identity";

    public IReadOnlyList<UniqueCorpusGateObservationalBaselineRow> Rows { get; init; } = [];
}

public sealed class UniqueCorpusGateObservationalBaselineRow
{
    public required string RowKey { get; init; }

    public required string Fingerprint { get; init; }

    public required string ItemIdentityKey { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public string CoreStatus { get; init; } = "-";

    public bool IsEquivalentSourceSet { get; init; }

    public string SourceDiagnosticFamily { get; init; } = "-";

    public string AggregateDiagnosticFamily { get; init; } = "-";

    public string ProviderStatus { get; init; } = "-";

    public string ProviderIdSetHash { get; init; } = "-";

    public int ModifierIdCount { get; init; }

    public string ModifierIdSetHash { get; init; } = "-";

    public int StatIdCount { get; init; }

    public string StatIdSetHash { get; init; } = "-";

    public int ComponentCount { get; init; }

    public int OmittedComponentCount { get; init; }

    public string CompositionKind { get; init; } = "-";

    public bool IsSearchable { get; init; }

    public string NotSearchableReasonFamily { get; init; } = "-";

    public string RoleSummary { get; init; } = "-";

    public bool IsBaseImplicit { get; init; }

    public bool HasExactProvenance { get; init; }
}

public enum UniqueCorpusGateChangeKind
{
    Neutral,
    HardRegression,
    ReviewRequired,
    Improvement,
    New,
}

public sealed class UniqueCorpusGateRowChange
{
    public required string RowKey { get; init; }

    public required UniqueCorpusGateChangeKind Kind { get; init; }

    public required string ReasonCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? ItemName { get; init; }

    public string? FingerprintBefore { get; init; }

    public string? FingerprintAfter { get; init; }
}

public sealed class UniqueCorpusGateObservationalDiff
{
    public string Schema { get; init; } = UniqueCorpusGateSchema.ObservationalDiffSchemaId;

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public int BaselineRowCount { get; init; }

    public int CurrentRowCount { get; init; }

    public int HardRegressionCount { get; init; }

    public int ReviewRequiredCount { get; init; }

    public int ImprovementCount { get; init; }

    public int NewRowCount { get; init; }

    public int NeutralCount { get; init; }

    public IReadOnlyList<UniqueCorpusGateRowChange> Changes { get; init; } = [];
}

public sealed class UniqueCorpusGateInvariantResult
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int Evaluable { get; init; }

    public int Pass { get; init; }

    public int Violations { get; init; }

    public bool StrictFailure => Violations > 0;

    public IReadOnlyList<UniqueCorpusGateInvariantViolationSample> TopViolatingFingerprints { get; init; } = [];
}

public sealed class UniqueCorpusGateInvariantViolationSample
{
    public required string Fingerprint { get; init; }

    public int Count { get; init; }

    public IReadOnlyList<string> ExampleItems { get; init; } = [];
}

public sealed class UniqueCorpusGateGoldenControlResult
{
    public required string Id { get; init; }

    public required string Category { get; init; }

    public required string Title { get; init; }

    public bool Passed { get; init; }

    public int MatchedRowCount { get; init; }

    public int PassingRowCount { get; init; }

    public int FailingRowCount { get; init; }

    public string Detail { get; init; } = string.Empty;

    public IReadOnlyList<string> ExampleItems { get; init; } = [];
}

public sealed class UniqueCorpusGateStructuralFailureClass
{
    public required string Fingerprint { get; init; }

    public required string DiagnosticFamily { get; init; }

    public required string ProviderFamily { get; init; }

    public required string EarliestLayer { get; init; }

    public int AffectedItemCount { get; init; }

    public int AffectedModifierCount { get; init; }

    public IReadOnlyList<string> ExampleItems { get; init; } = [];
}

public static class UniqueCorpusGateBaselineIO
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static UniqueCorpusGateObservationalBaseline Create(
        IReadOnlyList<UniqueCorpusGateObservationalRow> rows,
        string? sourceCorpusDirectory,
        int sourceCorpusFileCount,
        string deduplicationPolicy,
        string? gameDataSha = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new UniqueCorpusGateObservationalBaseline
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            ToolVersion = UniqueCorpusGateSchema.ToolVersion,
            SourceCorpusDirectory = sourceCorpusDirectory,
            SourceCorpusFileCount = sourceCorpusFileCount,
            ItemCount = rows.Select(row => row.Facts.ItemIdentityKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            ModifierRowCount = rows.Count,
            GameDataSha = gameDataSha,
            DeduplicationPolicy = deduplicationPolicy,
            Rows = rows
                .OrderBy(row => row.RowKey, StringComparer.Ordinal)
                .Select(ToBaselineRow)
                .ToArray(),
        };
    }

    public static UniqueCorpusGateObservationalBaselineRow ToBaselineRow(UniqueCorpusGateObservationalRow row) =>
        new()
        {
            RowKey = row.RowKey,
            Fingerprint = row.Fingerprint,
            ItemIdentityKey = row.Facts.ItemIdentityKey,
            ItemName = row.Facts.ItemName,
            CoreStatus = UniqueCorpusGateStatusFamilies.Core(
                row.Facts.CoreStatus,
                row.Facts.IsEquivalentSourceSet),
            IsEquivalentSourceSet = row.Facts.IsEquivalentSourceSet,
            SourceDiagnosticFamily = UniqueCorpusGateStatusFamilies.DiagnosticFamily(row.Facts.SourceDiagnosticCode),
            AggregateDiagnosticFamily = UniqueCorpusGateStatusFamilies.DiagnosticFamily(row.Facts.AggregateDiagnosticCode),
            ProviderStatus = UniqueCorpusGateStatusFamilies.Provider(row.Facts.ProviderStatus),
            ProviderIdSetHash = row.ProviderIdSetHash,
            ModifierIdCount = row.Facts.ModifierIdCount,
            ModifierIdSetHash = row.ModifierIdSetHash,
            StatIdCount = row.Facts.StatIdCount,
            StatIdSetHash = row.StatIdSetHash,
            ComponentCount = row.Facts.ComponentCount,
            OmittedComponentCount = row.Facts.OmittedComponentCount,
            CompositionKind = row.FingerprintParts.CompositionKind,
            IsSearchable = row.Facts.IsSearchable,
            NotSearchableReasonFamily = row.FingerprintParts.NotSearchableReasonFamily,
            RoleSummary = row.Facts.RoleSummary,
            IsBaseImplicit = row.Facts.IsBaseImplicit,
            HasExactProvenance = row.Facts.HasExactProvenance,
        };

    public static void Write(UniqueCorpusGateObservationalBaseline baseline, string path)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(baseline, JsonOptions));
    }

    public static UniqueCorpusGateObservationalBaseline Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<UniqueCorpusGateObservationalBaseline>(json, JsonOptions) ??
            throw new InvalidDataException($"Observational baseline was empty: {path}");
    }

    public static void WriteDiff(UniqueCorpusGateObservationalDiff diff, string path)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(diff, JsonOptions));
    }

    public static UniqueCorpusGateObservationalDiff SummarizeBaselineUpdate(
        UniqueCorpusGateObservationalBaseline? previous,
        UniqueCorpusGateObservationalBaseline next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (previous is null)
        {
            return new UniqueCorpusGateObservationalDiff
            {
                BaselineRowCount = 0,
                CurrentRowCount = next.ModifierRowCount,
                NewRowCount = next.ModifierRowCount,
                Changes =
                [
                    new UniqueCorpusGateRowChange
                    {
                        RowKey = "*",
                        Kind = UniqueCorpusGateChangeKind.New,
                        ReasonCode = "BASELINE_CREATED",
                        Message = $"Created observational baseline with {next.ModifierRowCount} rows.",
                    },
                ],
            };
        }

        return UniqueCorpusGateDifferential.Compare(previous, CreateRowsFromBaseline(next));
    }

    private static IReadOnlyList<UniqueCorpusGateObservationalRow> CreateRowsFromBaseline(
        UniqueCorpusGateObservationalBaseline baseline) =>
        baseline.Rows.Select(row => new UniqueCorpusGateObservationalRow
        {
            RowKey = row.RowKey,
            Fingerprint = row.Fingerprint,
            FingerprintParts = new UniqueCorpusGateStructuralFingerprint { Rendered = row.Fingerprint },
            Facts = new UniqueCorpusGateObservationalFacts
            {
                ItemIdentityKey = row.ItemIdentityKey,
                ItemName = row.ItemName,
                CoreStatus = row.CoreStatus,
                IsEquivalentSourceSet = row.IsEquivalentSourceSet,
                SourceDiagnosticCode = row.SourceDiagnosticFamily,
                AggregateDiagnosticCode = row.AggregateDiagnosticFamily,
                ProviderStatus = row.ProviderStatus,
                ModifierIdCount = row.ModifierIdCount,
                StatIdCount = row.StatIdCount,
                ComponentCount = row.ComponentCount,
                OmittedComponentCount = row.OmittedComponentCount,
                CompositionProjectionReason = row.CompositionKind,
                IsSearchable = row.IsSearchable,
                NotSearchableReason = row.NotSearchableReasonFamily,
                RoleSummary = row.RoleSummary,
                IsBaseImplicit = row.IsBaseImplicit,
                HasExactProvenance = row.HasExactProvenance,
            },
            ModifierIdSetHash = row.ModifierIdSetHash,
            StatIdSetHash = row.StatIdSetHash,
            ProviderIdSetHash = row.ProviderIdSetHash,
        }).ToArray();
}
