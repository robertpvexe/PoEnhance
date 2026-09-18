using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal static class ModifierPipelineReplayRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<ModifierPipelineReplayReport> ReplayDirectoryAsync(
        string inputDirectory,
        string gameDataPackagePath,
        string outputDirectory,
        PathOfExileTradeStatCatalog? tradeStatCatalog = null,
        PathOfExileTradeItemCatalog? tradeItemCatalog = null,
        PathOfExileTradeFilterCatalog? filterCatalog = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDataPackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!Directory.Exists(inputDirectory))
        {
            throw new DirectoryNotFoundException($"Replay corpus directory was not found: {inputDirectory}");
        }

        Directory.CreateDirectory(outputDirectory);
        var identity = await ModifierPipelineReplayGameDataGate
            .LoadIdentityAsync(gameDataPackagePath, cancellationToken)
            .ConfigureAwait(false);
        tradeStatCatalog ??= LoadPinnedOfficialTradeCatalog();
        tradeItemCatalog ??= new PathOfExileTradeItemCatalog([]);
        filterCatalog ??= PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();

        var files = Directory.GetFiles(inputDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var itemResults = new List<ModifierPipelineReplayItemResult>();
        var rawByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in files)
        {
            var result = ReplayFile(
                path,
                identity,
                tradeStatCatalog,
                tradeItemCatalog,
                filterCatalog);
            itemResults.Add(result);
            if (TryReadRawClipboard(path, out var raw) &&
                result.Classification != ModifierPipelineReplayDivergenceClass.AuditOnlyOrSchemaRefused)
            {
                rawByFile[result.FileName] = raw;
            }
        }

        // Retain duplicates: do not collapse by item name.
        var summary = BuildSummary(itemResults, identity, inputDirectory, files.Length);
        var fixtureGap = ModifierPipelineReplayFixtureGapAnalyzer.AnalyzeTimeless(itemResults);
        ModifierPipelineReplayFixtureGapAnalyzer.AttachCapturedRawTexts(fixtureGap, rawByFile);
        var report = new ModifierPipelineReplayReport
        {
            Schema = ModifierPipelineReplaySchemas.ReportSchema,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            InputDirectory = Path.GetFullPath(inputDirectory),
            OutputDirectory = Path.GetFullPath(outputDirectory),
            Summary = summary,
            Items = itemResults,
            TimelessAnalysis = BuildTimelessAnalysis(itemResults),
            FixtureGap = fixtureGap,
            WorkflowNote =
                "A.5.5 bridge: replay report remains separate; future stage can emit unique-corpus-gate-compatible diagnostic JSON from ReplayReady inputs after identity gate.",
        };

        await ModifierPipelineReplayReportWriter.WriteAsync(report, outputDirectory, cancellationToken)
            .ConfigureAwait(false);
        return report;
    }

    public static ModifierPipelineReplayItemResult ReplayFile(
        string capturePath,
        ModifierPipelineReplayGameDataIdentity identity,
        PathOfExileTradeStatCatalog tradeStatCatalog,
        PathOfExileTradeItemCatalog tradeItemCatalog,
        PathOfExileTradeFilterCatalog filterCatalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capturePath);
        ArgumentNullException.ThrowIfNull(identity);

        var fileName = Path.GetFileName(capturePath);
        ModifierPipelineReplayCaptureDocument? capture;
        try
        {
            var json = File.ReadAllText(capturePath);
            capture = JsonSerializer.Deserialize<ModifierPipelineReplayCaptureDocument>(json, JsonOptions);
            if (capture is null)
            {
                return Failed(fileName, null, ModifierPipelineReplayDivergenceClass.ReplayError, "Capture JSON deserialized to null.");
            }
        }
        catch (Exception exception)
        {
            return Failed(fileName, null, ModifierPipelineReplayDivergenceClass.ReplayError, exception.Message);
        }

        var gate = ModifierPipelineReplayGameDataGate.Evaluate(capture, identity);
        if (!gate.CanReplay)
        {
            return new ModifierPipelineReplayItemResult
            {
                FileName = fileName,
                ItemName = capture.Item?.DisplayName,
                Classification = gate.DivergenceClass,
                Reason = gate.Reason,
                CapturedGameDataVersion = capture.ReplayContext?.GameDataVersion,
                CapturedGameDataSha256 = capture.ReplayContext?.GameDataSha256,
                ReplayGameDataVersion = identity.DataVersion,
                ReplayGameDataSha256 = identity.Sha256,
                RawClipboardSha256 = capture.ReplayContext?.RawClipboardText is { } raw
                    ? ModifierPipelineReplayNormalizer.HashRawClipboard(raw)
                    : null,
            };
        }

        var rawClipboardText = capture.ReplayContext!.RawClipboardText!;
        var rawHash = ModifierPipelineReplayNormalizer.HashRawClipboard(rawClipboardText);
        try
        {
            var execution = ModifierPipelineReplayProductionPath.Execute(
                rawClipboardText,
                identity.Catalog,
                tradeStatCatalog,
                tradeItemCatalog,
                filterCatalog);
            // Prove the exact same string instance/content entered the parser path.
            if (!string.Equals(execution.RawClipboardText, rawClipboardText, StringComparison.Ordinal))
            {
                return Failed(
                    fileName,
                    capture.Item?.DisplayName,
                    ModifierPipelineReplayDivergenceClass.ReplayError,
                    "Replay host altered rawClipboardText before parse.");
            }

            var capturedNormalized = ModifierPipelineReplayNormalizer.FromCapture(capture);
            var replayNormalized = ModifierPipelineReplayNormalizer.FromReplay(
                execution.Parsed,
                execution.BaseResolution,
                execution.ProviderDraft,
                execution.UniqueResolution);
            var comparison = ModifierPipelineReplayComparer.Compare(capturedNormalized, replayNormalized);
            return new ModifierPipelineReplayItemResult
            {
                FileName = fileName,
                ItemName = capture.Item?.DisplayName ?? replayNormalized.DisplayName,
                Classification = comparison.Classification,
                Reason = comparison.Classification == ModifierPipelineReplayDivergenceClass.ExactMatch
                    ? "Normalized captured and replay semantics match."
                    : string.Join("; ", comparison.Deltas.Select(delta => $"{delta.Field}:{delta.Classification}")),
                CapturedGameDataVersion = capture.ReplayContext.GameDataVersion,
                CapturedGameDataSha256 = capture.ReplayContext.GameDataSha256,
                ReplayGameDataVersion = identity.DataVersion,
                ReplayGameDataSha256 = identity.Sha256,
                RawClipboardSha256 = rawHash,
                Captured = capturedNormalized,
                Replayed = replayNormalized,
                Deltas = comparison.Deltas,
            };
        }
        catch (Exception exception)
        {
            return new ModifierPipelineReplayItemResult
            {
                FileName = fileName,
                ItemName = capture.Item?.DisplayName,
                Classification = ModifierPipelineReplayDivergenceClass.ReplayError,
                Reason = exception.Message,
                CapturedGameDataVersion = capture.ReplayContext.GameDataVersion,
                CapturedGameDataSha256 = capture.ReplayContext.GameDataSha256,
                ReplayGameDataVersion = identity.DataVersion,
                ReplayGameDataSha256 = identity.Sha256,
                RawClipboardSha256 = rawHash,
            };
        }
    }

    private static ModifierPipelineReplayItemResult Failed(
        string fileName,
        string? itemName,
        string classification,
        string reason) =>
        new()
        {
            FileName = fileName,
            ItemName = itemName,
            Classification = classification,
            Reason = reason,
        };

    private static ModifierPipelineReplaySummary BuildSummary(
        IReadOnlyList<ModifierPipelineReplayItemResult> items,
        ModifierPipelineReplayGameDataIdentity identity,
        string inputDirectory,
        int fileCount)
    {
        var refused = items.Count(item =>
            item.Classification == ModifierPipelineReplayDivergenceClass.AuditOnlyOrSchemaRefused);
        var replayReadyCount = items.Count - refused;

        return new ModifierPipelineReplaySummary
        {
            InputDirectory = Path.GetFullPath(inputDirectory),
            CaptureFileCount = fileCount,
            ReplayReady = replayReadyCount,
            AuditOnlyOrSchemaRefused = refused,
            Replayed = items.Count(item => item.Captured is not null && item.Replayed is not null),
            ExactMatch = Count(items, ModifierPipelineReplayDivergenceClass.ExactMatch),
            InputEquivalentOutputDivergence = Count(items, ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence),
            CaptureFieldUnavailable = Count(items, ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable),
            ProviderContextUnverified = Count(items, ModifierPipelineReplayDivergenceClass.ProviderContextUnverified),
            GameDataMismatch = Count(items, ModifierPipelineReplayDivergenceClass.GameDataMismatch),
            ReplayError = Count(items, ModifierPipelineReplayDivergenceClass.ReplayError),
            ReplayGameDataVersion = identity.DataVersion,
            ReplayGameDataSha256 = identity.Sha256,
            DistinctCapturedGameDataVersions = items
                .Select(item => item.CapturedGameDataVersion)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            DistinctCapturedGameDataSha256Values = items
                .Select(item => item.CapturedGameDataSha256)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static int Count(IReadOnlyList<ModifierPipelineReplayItemResult> items, string classification) =>
        items.Count(item => item.Classification == classification);

    private static ModifierPipelineReplayTimelessAnalysis BuildTimelessAnalysis(
        IReadOnlyList<ModifierPipelineReplayItemResult> items)
    {
        var timelessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Lethal Pride",
            "Brutal Restraint",
            "Elegant Hubris",
            "Glorious Vanity",
            "Militant Faith",
        };
        var timeless = items
            .Where(item => item.ItemName is not null && timelessNames.Contains(item.ItemName))
            .Where(item => item.Classification != ModifierPipelineReplayDivergenceClass.AuditOnlyOrSchemaRefused)
            .ToArray();
        var reproducedFailure = timeless.Count(item =>
            (item.Classification is
                ModifierPipelineReplayDivergenceClass.ExactMatch or
                ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable or
                ModifierPipelineReplayDivergenceClass.ProviderContextUnverified) &&
            HasVersionMismatch(item));
        var divergedToExact = timeless.Count(item =>
            item.Classification == ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence &&
            CapturedHadVersionMismatch(item) &&
            !ReplayedHasVersionMismatch(item));
        var divergedOther = timeless.Count(item =>
            item.Classification == ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence);
        // T1: same raw+GameData reproduces Unique version/block failure (ExactMatch/capture-only noise allowed).
        var family =
            timeless.Length == 0 ? "NONE" :
            divergedToExact > 0 && reproducedFailure > 0 ? "T3" :
            divergedOther > 0 && reproducedFailure > 0 ? "T3" :
            divergedToExact == timeless.Length ? "T2" :
            reproducedFailure == timeless.Length ? "T1" :
            (reproducedFailure > 0 && divergedOther == 0 && divergedToExact == 0) ? "T1" :
            divergedOther > 0 ? "T3" :
            "T1";

        return new ModifierPipelineReplayTimelessAnalysis
        {
            FamilyOutcome = family,
            Items = timeless.Select(item => new ModifierPipelineReplayTimelessItem
            {
                FileName = item.FileName,
                ItemName = item.ItemName ?? "-",
                Classification = item.Classification,
                RawClipboardSha256 = item.RawClipboardSha256,
                CapturedGameDataVersion = item.CapturedGameDataVersion,
                CapturedGameDataSha256 = item.CapturedGameDataSha256,
                ReplayGameDataVersion = item.ReplayGameDataVersion,
                ReplayGameDataSha256 = item.ReplayGameDataSha256,
                CapturedSeedBlockDiagnostic = FirstBlockDiagnostic(item.Captured),
                ReplaySeedBlockDiagnostic = FirstBlockDiagnostic(item.Replayed),
                CapturedModifierIds = FirstIds(item.Captured, modifier => modifier.ModifierIds),
                ReplayModifierIds = FirstIds(item.Replayed, modifier => modifier.ModifierIds),
                CapturedStatIds = FirstIds(item.Captured, modifier => modifier.StatIds),
                ReplayStatIds = FirstIds(item.Replayed, modifier => modifier.StatIds),
                CapturedSearchable = item.Captured?.Modifiers.FirstOrDefault()?.IsSearchable,
                ReplaySearchable = item.Replayed?.Modifiers.FirstOrDefault()?.IsSearchable,
            }).ToArray(),
        };
    }

    private static bool HasVersionMismatch(ModifierPipelineReplayItemResult item) =>
        CapturedHadVersionMismatch(item) && ReplayedHasVersionMismatch(item);

    private static bool CapturedHadVersionMismatch(ModifierPipelineReplayItemResult item) =>
        IsVersionFailureDiagnostic(FirstBlockDiagnostic(item.Captured)) ||
        item.Captured?.Modifiers.Any(modifier =>
            IsVersionFailureDiagnostic(modifier.UniqueBlockDiagnosticCode)) == true;

    private static bool ReplayedHasVersionMismatch(ModifierPipelineReplayItemResult item) =>
        IsVersionFailureDiagnostic(FirstBlockDiagnostic(item.Replayed)) ||
        item.Replayed?.Modifiers.Any(modifier =>
            IsVersionFailureDiagnostic(modifier.UniqueBlockDiagnosticCode)) == true;

    private static bool IsVersionFailureDiagnostic(string? code) =>
        string.Equals(code, "UNIQUE_BLOCK_VERSION_MISMATCH", StringComparison.Ordinal) ||
        string.Equals(code, "UNIQUE_VERSION_NOT_FOUND", StringComparison.Ordinal);

    private static string? FirstBlockDiagnostic(ModifierPipelineNormalizedItem? item) =>
        item?.UniqueResolutionDiagnosticCode is { Length: > 0 } code
            ? code
            : item?.Modifiers.Select(modifier => modifier.UniqueBlockDiagnosticCode)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static IReadOnlyList<string> FirstIds(
        ModifierPipelineNormalizedItem? item,
        Func<ModifierPipelineNormalizedModifier, IReadOnlyList<string>> selector) =>
        item?.Modifiers.SelectMany(selector).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
        ?? [];

    private static bool TryReadRawClipboard(string capturePath, out string rawClipboardText)
    {
        rawClipboardText = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(capturePath));
            if (!document.RootElement.TryGetProperty("replayContext", out var replay) ||
                !replay.TryGetProperty("rawClipboardText", out var raw) ||
                raw.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            rawClipboardText = raw.GetString() ?? string.Empty;
            return rawClipboardText.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static PathOfExileTradeStatCatalog LoadPinnedOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var result = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        if (!result.IsSuccess || result.Catalog is null)
        {
            throw new InvalidOperationException(
                "Pinned official Trade stats catalog failed to load for replay provider layer.");
        }

        return result.Catalog;
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(relativeParts)}");
    }
}

internal sealed class ModifierPipelineReplayReport
{
    public string Schema { get; init; } = ModifierPipelineReplaySchemas.ReportSchema;

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public required string InputDirectory { get; init; }

    public required string OutputDirectory { get; init; }

    public required ModifierPipelineReplaySummary Summary { get; init; }

    public IReadOnlyList<ModifierPipelineReplayItemResult> Items { get; init; } = [];

    public ModifierPipelineReplayTimelessAnalysis? TimelessAnalysis { get; init; }

    public ModifierPipelineReplayFixtureGapReport? FixtureGap { get; init; }

    public string? WorkflowNote { get; init; }
}

internal sealed class ModifierPipelineReplaySummary
{
    public required string InputDirectory { get; init; }

    public int CaptureFileCount { get; init; }

    public int ReplayReady { get; init; }

    public int AuditOnlyOrSchemaRefused { get; init; }

    public int Replayed { get; init; }

    public int ExactMatch { get; init; }

    public int InputEquivalentOutputDivergence { get; init; }

    public int CaptureFieldUnavailable { get; init; }

    public int ProviderContextUnverified { get; init; }

    public int GameDataMismatch { get; init; }

    public int ReplayError { get; init; }

    public string? ReplayGameDataVersion { get; init; }

    public string? ReplayGameDataSha256 { get; init; }

    public IReadOnlyList<string> DistinctCapturedGameDataVersions { get; init; } = [];

    public IReadOnlyList<string> DistinctCapturedGameDataSha256Values { get; init; } = [];
}

internal sealed class ModifierPipelineReplayItemResult
{
    public required string FileName { get; init; }

    public string? ItemName { get; init; }

    public required string Classification { get; init; }

    public string? Reason { get; init; }

    public string? CapturedGameDataVersion { get; init; }

    public string? CapturedGameDataSha256 { get; init; }

    public string? ReplayGameDataVersion { get; init; }

    public string? ReplayGameDataSha256 { get; init; }

    public string? RawClipboardSha256 { get; init; }

    public ModifierPipelineNormalizedItem? Captured { get; init; }

    public ModifierPipelineNormalizedItem? Replayed { get; init; }

    public IReadOnlyList<ModifierPipelineReplayFieldDelta> Deltas { get; init; } = [];
}

internal sealed class ModifierPipelineReplayTimelessAnalysis
{
    public required string FamilyOutcome { get; init; }

    public IReadOnlyList<ModifierPipelineReplayTimelessItem> Items { get; init; } = [];
}

internal sealed class ModifierPipelineReplayTimelessItem
{
    public required string FileName { get; init; }

    public required string ItemName { get; init; }

    public required string Classification { get; init; }

    public string? RawClipboardSha256 { get; init; }

    public string? CapturedGameDataVersion { get; init; }

    public string? CapturedGameDataSha256 { get; init; }

    public string? ReplayGameDataVersion { get; init; }

    public string? ReplayGameDataSha256 { get; init; }

    public string? CapturedSeedBlockDiagnostic { get; init; }

    public string? ReplaySeedBlockDiagnostic { get; init; }

    public IReadOnlyList<string> CapturedModifierIds { get; init; } = [];

    public IReadOnlyList<string> ReplayModifierIds { get; init; } = [];

    public IReadOnlyList<string> CapturedStatIds { get; init; } = [];

    public IReadOnlyList<string> ReplayStatIds { get; init; } = [];

    public bool? CapturedSearchable { get; init; }

    public bool? ReplaySearchable { get; init; }
}
