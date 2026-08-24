using System.Text.Json;

namespace PoEnhance.DataTool.UniqueCorpusGate;

public static class UniqueCorpusGateAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static UniqueCorpusGateReport AnalyzeDirectory(
        string inputDirectory,
        UniqueCorpusGateOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        options ??= new UniqueCorpusGateOptions();

        if (!Directory.Exists(inputDirectory))
        {
            throw new DirectoryNotFoundException($"Corpus directory was not found: {inputDirectory}");
        }

        var files = Directory.GetFiles(inputDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var parsed = new List<UniqueCorpusGateAnalyzedCapture>();
        var skipped = new List<UniqueCorpusGateSkippedFile>();
        foreach (var path in files)
        {
            if (TryReadCapture(path, out var capture, out var skipReason))
            {
                parsed.Add(capture);
            }
            else
            {
                skipped.Add(new UniqueCorpusGateSkippedFile
                {
                    FileName = Path.GetFileName(path),
                    Reason = skipReason,
                });
            }
        }

        var ordered = parsed
            .OrderByDescending(capture => capture.Timestamp)
            .ThenBy(capture => capture.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var analyzedCaptures = options.DeduplicateLatestCapturePerItem
            ? ordered
                .GroupBy(capture => capture.ItemIdentityKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray()
            : ordered;
        var deduplicatedAway = parsed.Count - analyzedCaptures.Length;

        var components = analyzedCaptures
            .SelectMany(ClassifyCaptureComponents)
            .ToArray();

        var outcomes = CountOutcomes(components.Select(component => component.Outcome).ToArray());
        var clusters = BuildClusters(components);
        var families = BuildSignatureFamilies(components);
        var ranked = clusters
            .OrderByDescending(cluster => cluster.DistinctItemCount)
            .ThenByDescending(cluster => cluster.ComponentCount)
            .ThenBy(cluster => cluster.RootCauseKey, StringComparer.Ordinal)
            .ToArray();

        return new UniqueCorpusGateReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            InputDirectory = Path.GetFullPath(inputDirectory),
            Identity = new UniqueCorpusGateIdentity
            {
                CaptureFileCount = files.Length,
                ParsedCaptureCount = parsed.Count,
                SkippedFileCount = skipped.Count,
                SkippedFiles = skipped
                    .OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                DeduplicatedCaptureCount = deduplicatedAway,
                AnalyzedCaptureCount = analyzedCaptures.Length,
                DistinctItemNameCount = analyzedCaptures
                    .Select(capture => capture.ItemName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                DistinctItemIdentityCount = analyzedCaptures
                    .Select(capture => capture.ItemIdentityKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                ModifierComponentCount = components.Length,
                DeduplicationPolicy = options.DeduplicateLatestCapturePerItem
                    ? "keep-latest-capture-per-item-identity"
                    : "analyze-all-parsed-captures",
            },
            Outcomes = outcomes,
            OutcomesByParsedKind = Breakdown(components, component => component.ParsedKind),
            OutcomesByResolvedSourceKind = Breakdown(components, component => component.ResolvedSourceKind),
            OutcomesBySourceFamily = Breakdown(components, component => component.SourceFamily),
            FailureStages = Breakdown(
                components.Where(component => component.Outcome != UniqueCorpusGateOutcomes.Supported),
                component => component.Stage),
            RootCauseClusters = clusters,
            SignatureFamilies = families,
            RankedBacklog = ranked,
        };
    }

    public static UniqueCorpusGateComparison Compare(
        UniqueCorpusGateReport current,
        UniqueCorpusGateReport baseline)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baseline);

        var currentClusters = current.RootCauseClusters.ToDictionary(
            cluster => cluster.RootCauseKey,
            StringComparer.Ordinal);
        var baselineClusters = baseline.RootCauseClusters.ToDictionary(
            cluster => cluster.RootCauseKey,
            StringComparer.Ordinal);
        var keys = currentClusters.Keys
            .Concat(baselineClusters.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var deltas = new List<UniqueCorpusGateClusterDelta>();
        var introduced = new List<string>();
        var resolved = new List<string>();
        foreach (var key in keys)
        {
            currentClusters.TryGetValue(key, out var currentCluster);
            baselineClusters.TryGetValue(key, out var baselineCluster);
            var currentCount = currentCluster?.ComponentCount ?? 0;
            var baselineCount = baselineCluster?.ComponentCount ?? 0;
            if (baselineCluster is null && currentCluster is not null)
            {
                introduced.Add(key);
            }

            if (currentCluster is null && baselineCluster is not null)
            {
                resolved.Add(key);
            }

            deltas.Add(new UniqueCorpusGateClusterDelta
            {
                RootCauseKey = key,
                BaselineComponentCount = baselineCount,
                CurrentComponentCount = currentCount,
                ComponentDelta = currentCount - baselineCount,
                BaselineDistinctItemCount = baselineCluster?.DistinctItemCount ?? 0,
                CurrentDistinctItemCount = currentCluster?.DistinctItemCount ?? 0,
                DistinctItemDelta = (currentCluster?.DistinctItemCount ?? 0) -
                    (baselineCluster?.DistinctItemCount ?? 0),
            });
        }

        var regressions = CompareSignatureFamilies(current.SignatureFamilies, baseline.SignatureFamilies);

        return new UniqueCorpusGateComparison
        {
            Outcomes = new UniqueCorpusGateOutcomeDelta
            {
                SupportedDelta = current.Outcomes.Supported - baseline.Outcomes.Supported,
                AmbiguousDelta = current.Outcomes.Ambiguous - baseline.Outcomes.Ambiguous,
                UnsupportedDelta = current.Outcomes.Unsupported - baseline.Outcomes.Unsupported,
                OtherDelta = current.Outcomes.Other - baseline.Outcomes.Other,
                SupportedPercentDelta = current.Outcomes.SupportedPercent - baseline.Outcomes.SupportedPercent,
            },
            ClusterDeltas = deltas
                .OrderByDescending(delta => Math.Abs(delta.DistinctItemDelta))
                .ThenByDescending(delta => Math.Abs(delta.ComponentDelta))
                .ThenBy(delta => delta.RootCauseKey, StringComparer.Ordinal)
                .ToArray(),
            IntroducedClusterKeys = introduced,
            ResolvedClusterKeys = resolved,
            SignatureFamilyRegressions = regressions,
        };
    }

    public static UniqueCorpusGateStrictResult EvaluateStrictGate(
        UniqueCorpusGateReport report,
        UniqueCorpusGateOptions options)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (options.MaxUnclassifiedClusterComponents is { } maxUnclassified)
        {
            var unclassified = report.RootCauseClusters
                .Where(cluster =>
                    string.Equals(cluster.Stage, UniqueCorpusGateStages.Unclassified, StringComparison.Ordinal))
                .Sum(cluster => cluster.ComponentCount);
            if (unclassified > maxUnclassified)
            {
                failures.Add(
                    $"Unclassified systemic failures ({unclassified}) exceed --max-unclassified-cluster-components {maxUnclassified}.");
            }
        }

        if (report.Comparison is { } comparison)
        {
            foreach (var delta in comparison.ClusterDeltas.Where(delta =>
                         delta.BaselineComponentCount > 0 &&
                         delta.ComponentDelta > 0))
            {
                failures.Add(
                    $"Baseline cluster '{delta.RootCauseKey}' regressed by {delta.ComponentDelta} component(s).");
            }

            if (options.MaxSupportedCoverageDropPercent is { } maxDrop &&
                comparison.Outcomes.SupportedPercentDelta < 0 &&
                Math.Abs(comparison.Outcomes.SupportedPercentDelta) > maxDrop)
            {
                failures.Add(
                    $"Supported coverage dropped {Math.Abs(comparison.Outcomes.SupportedPercentDelta):0.##} points, exceeding --max-supported-coverage-drop-percent {maxDrop}.");
            }

            foreach (var regression in comparison.SignatureFamilyRegressions)
            {
                failures.Add(
                    $"Signature family '{regression.NormalizedSignature}' ({regression.SourceFamily}) lost Supported coverage.");
            }
        }

        return new UniqueCorpusGateStrictResult
        {
            Passed = failures.Count == 0,
            Failures = failures,
        };
    }

    internal static bool TryReadCapture(
        string path,
        out UniqueCorpusGateAnalyzedCapture capture,
        out string skipReason)
    {
        capture = null!;
        skipReason = string.Empty;
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            skipReason = $"File could not be read: {exception.Message}";
            return false;
        }

        UniqueCorpusGateCaptureDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<UniqueCorpusGateCaptureDocument>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            skipReason = $"Invalid JSON: {exception.Message}";
            return false;
        }

        if (document is null)
        {
            skipReason = "JSON root was empty.";
            return false;
        }

        if (!IsPipelineCapture(document, out skipReason))
        {
            return false;
        }

        var itemName = FirstNonEmpty(
            document.UniqueIdentity?.CanonicalName,
            document.Item?.DisplayName,
            document.Item?.ResolvedBaseName,
            document.Item?.ParsedBaseType,
            Path.GetFileNameWithoutExtension(path)) ?? "unknown-item";
        capture = new UniqueCorpusGateAnalyzedCapture
        {
            FileName = Path.GetFileName(path),
            Timestamp = document.CompletedAtUtc ?? document.CapturedAtUtc ?? DateTimeOffset.MinValue,
            Document = document,
            ItemIdentityKey = BuildItemIdentityKey(document),
            ItemName = itemName,
        };
        skipReason = string.Empty;
        return true;
    }

    internal static IReadOnlyList<UniqueCorpusGateAnalyzedComponent> ClassifyCaptureComponents(
        UniqueCorpusGateAnalyzedCapture capture)
    {
        return (capture.Document.Modifiers ?? [])
            .Select(modifier => ClassifyComponent(capture, modifier))
            .ToArray();
    }

    internal static UniqueCorpusGateAnalyzedComponent ClassifyComponent(
        UniqueCorpusGateAnalyzedCapture capture,
        UniqueCorpusGateCaptureModifier modifier)
    {
        var parsedKind = FirstNonEmpty(
            modifier.ResolvedSemantics?.ParsedKind,
            modifier.Raw?.ParsedKind,
            "Unknown")!;
        var resolvedSourceKind = FirstNonEmpty(
            modifier.ResolvedSemantics?.ResolvedSourceKind,
            parsedKind,
            "Unknown")!;
        var sourceFamily = ClassifySourceFamily(modifier, parsedKind, resolvedSourceKind);
        var outcome = ClassifyOutcome(modifier);
        var diagnosticCodes = CollectDiagnosticCodes(modifier);
        var (stage, rootCauseKey) = ClassifyFailure(modifier, outcome, sourceFamily, diagnosticCodes);
        return new UniqueCorpusGateAnalyzedComponent
        {
            ItemIdentityKey = capture.ItemIdentityKey,
            ItemName = capture.ItemName,
            Outcome = outcome,
            ParsedKind = parsedKind,
            ResolvedSourceKind = resolvedSourceKind,
            SourceFamily = sourceFamily,
            Stage = outcome == UniqueCorpusGateOutcomes.Supported ? UniqueCorpusGateStages.None : stage,
            RootCauseKey = outcome == UniqueCorpusGateOutcomes.Supported ? "none" : rootCauseKey,
            NormalizedSignature = NormalizeSignature(modifier),
            DiagnosticCodes = diagnosticCodes,
        };
    }

    internal static string ClassifySourceFamily(
        UniqueCorpusGateCaptureModifier modifier,
        string parsedKind,
        string resolvedSourceKind)
    {
        var implicitOrigin = modifier.Raw?.ImplicitOrigin ?? string.Empty;
        if (EqualsOrdinal(implicitOrigin, "Corrupted") ||
            ContainsOrdinal(resolvedSourceKind, "Corrupted") ||
            ContainsOrdinal(parsedKind, "Corrupted"))
        {
            return UniqueCorpusGateSourceFamilies.CorruptedImplicit;
        }

        if (EqualsOrdinal(parsedKind, "Enchantment") ||
            EqualsOrdinal(resolvedSourceKind, "Enchantment"))
        {
            return UniqueCorpusGateSourceFamilies.Enchantment;
        }

        if (modifier.ResolvedSemantics?.HasResolvedUniqueSourceSemantics == true ||
            EqualsOrdinal(parsedKind, "Unique") ||
            EqualsOrdinal(resolvedSourceKind, "Unique") ||
            EqualsOrdinal(modifier.ResolvedSemantics?.UniqueOrigin, "Ordinary") ||
            EqualsOrdinal(modifier.ResolvedSemantics?.UniqueOrigin, "Foulborn") ||
            EqualsOrdinal(modifier.Raw?.UniqueOrigin, "Ordinary") ||
            EqualsOrdinal(modifier.Raw?.UniqueOrigin, "Foulborn"))
        {
            return UniqueCorpusGateSourceFamilies.Unique;
        }

        if (EqualsOrdinal(parsedKind, "Implicit") || EqualsOrdinal(resolvedSourceKind, "Implicit"))
        {
            return UniqueCorpusGateSourceFamilies.Implicit;
        }

        return UniqueCorpusGateSourceFamilies.Other;
    }

    internal static string ClassifyOutcome(UniqueCorpusGateCaptureModifier modifier)
    {
        var availability = modifier.Consumer?.AvailabilityStatus;
        if (EqualsOrdinal(availability, UniqueCorpusGateOutcomes.Supported) ||
            modifier.Consumer?.IsSearchable == true &&
            IsSuccessfulProviderStatus(modifier.ProviderResolution?.ProviderResolutionStatus))
        {
            return UniqueCorpusGateOutcomes.Supported;
        }

        if (EqualsOrdinal(availability, UniqueCorpusGateOutcomes.Ambiguous) ||
            EqualsOrdinal(modifier.ProviderResolution?.ProviderResolutionStatus, "Ambiguous") ||
            ContainsAmbiguityMarker(modifier.ProviderResolution?.ProviderDiagnosticCode) ||
            ContainsAmbiguityMarker(modifier.SourceResolution?.UniqueResolutionDiagnosticCode))
        {
            return UniqueCorpusGateOutcomes.Ambiguous;
        }

        if (EqualsOrdinal(availability, UniqueCorpusGateOutcomes.Unsupported) ||
            modifier.Consumer?.IsSearchable == false)
        {
            return UniqueCorpusGateOutcomes.Unsupported;
        }

        return UniqueCorpusGateOutcomes.Other;
    }

    internal static (string Stage, string RootCauseKey) ClassifyFailure(
        UniqueCorpusGateCaptureModifier modifier,
        string outcome,
        string sourceFamily,
        IReadOnlyList<string> diagnosticCodes)
    {
        if (outcome == UniqueCorpusGateOutcomes.Supported)
        {
            return (UniqueCorpusGateStages.None, "none");
        }

        var uniqueCode = modifier.SourceResolution?.UniqueResolutionDiagnosticCode;
        if (!string.IsNullOrWhiteSpace(uniqueCode))
        {
            if (ContainsOrdinal(uniqueCode, "VERSION_MISMATCH"))
            {
                return (UniqueCorpusGateStages.VersionBlockMatching, uniqueCode);
            }

            return (UniqueCorpusGateStages.UniqueSourceMechanics, uniqueCode);
        }

        var serialization = modifier.Consumer?.Serialization;
        if (serialization is { Attempted: true, Success: false })
        {
            return (
                UniqueCorpusGateStages.Serialization,
                FirstNonEmpty(serialization.DiagnosticCode, "SERIALIZATION_FAILED")!);
        }

        var projection = Last(modifier.ProviderPasses)?.Projection;
        if (projection is { IsFaithful: false } && Last(modifier.ProviderPasses)?.Match is not null)
        {
            return (
                UniqueCorpusGateStages.BoundProjection,
                FirstNonEmpty(projection.ProjectionKind, "BOUND_PROJECTION_UNFAITHFUL")!);
        }

        var match = Last(modifier.ProviderPasses)?.Match;
        var matchCode = match?.Diagnostics?.Select(diagnostic => diagnostic.Code)
            .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));
        var providerCode = modifier.ProviderResolution?.ProviderDiagnosticCode;
        if (ContainsOrdinal(matchCode, "AMBIGUOUS") ||
            ContainsOrdinal(providerCode, "AMBIGUOUS") ||
            EqualsOrdinal(match?.Status, "Ambiguous") ||
            EqualsOrdinal(modifier.ProviderResolution?.ProviderResolutionStatus, "Ambiguous"))
        {
            return (
                UniqueCorpusGateStages.ProviderAmbiguity,
                FirstNonEmpty(matchCode, providerCode, "POE_TRADE_STAT_MATCH_AMBIGUOUS_CANDIDATES")!);
        }

        if (EqualsOrdinal(match?.Status, "NotFound") ||
            EqualsOrdinal(modifier.ProviderResolution?.ProviderResolutionStatus, "NotFound") ||
            ContainsOrdinal(providerCode, "NO_CANDIDATE") ||
            ContainsOrdinal(providerCode, "NOT_FOUND") ||
            ContainsOrdinal(matchCode, "NO_CANDIDATE") ||
            ContainsOrdinal(matchCode, "NOT_FOUND"))
        {
            return (
                UniqueCorpusGateStages.ProviderDiscovery,
                FirstNonEmpty(matchCode, providerCode, "PROVIDER_NOT_FOUND")!);
        }

        if (ContainsOrdinal(providerCode, "MISSING_GAMEDATA_PROVENANCE") ||
            EqualsOrdinal(Last(modifier.ProviderPasses)?.ResolutionPhase, "unsupported-before-match") ||
            EqualsOrdinal(Last(modifier.ProviderPasses)?.SkipReason, "POE_TRADE_SELECTED_MODIFIER_MISSING_GAMEDATA_PROVENANCE") ||
            Last(modifier.ProviderPasses)?.CanResolveProviderComponent == false &&
            Last(modifier.ProviderPasses)?.Match is null)
        {
            return (
                UniqueCorpusGateStages.ProvenanceGate,
                FirstNonEmpty(
                    providerCode,
                    Last(modifier.ProviderPasses)?.SkipReason,
                    "MISSING_GAMEDATA_PROVENANCE")!);
        }

        if (EqualsOrdinal(modifier.SourceResolution?.Status, "Unknown") ||
            string.IsNullOrWhiteSpace(modifier.SourceResolution?.ResolvedModifierId) &&
            (modifier.SourceResolution?.ResolvedStatIds?.Count ?? 0) == 0 &&
            sourceFamily is UniqueCorpusGateSourceFamilies.Unique or UniqueCorpusGateSourceFamilies.Enchantment)
        {
            return (
                UniqueCorpusGateStages.UniqueSourceMechanics,
                sourceFamily == UniqueCorpusGateSourceFamilies.Enchantment
                    ? "ENCHANTMENT_SOURCE_UNRESOLVED"
                    : "UNIQUE_SOURCE_UNRESOLVED");
        }

        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            return (UniqueCorpusGateStages.Unclassified, providerCode);
        }

        if (diagnosticCodes.Count > 0)
        {
            return (UniqueCorpusGateStages.Unclassified, diagnosticCodes[0]);
        }

        return (
            UniqueCorpusGateStages.Unclassified,
            $"UNCLASSIFIED:{outcome}:{sourceFamily}");
    }

    internal static string NormalizeSignature(UniqueCorpusGateCaptureModifier modifier)
    {
        return FirstNonEmpty(
            modifier.Signatures?.CanonicalSignature,
            modifier.Signatures?.ProviderCanonicalSignature,
            modifier.Signatures?.ProviderSearchSignatures?.FirstOrDefault(value =>
                !string.IsNullOrWhiteSpace(value)),
            modifier.Signatures?.OriginalText,
            modifier.Raw?.OriginalText,
            modifier.ComponentId,
            "<missing-signature>")!;
    }

    private static bool IsPipelineCapture(UniqueCorpusGateCaptureDocument document, out string skipReason)
    {
        var version = document.DiagnosticVersion ?? string.Empty;
        if (version.Contains("trade-search", StringComparison.OrdinalIgnoreCase))
        {
            skipReason = "Unrelated diagnostic (Trade Search payload capture).";
            return false;
        }

        if (document.Modifiers is null)
        {
            skipReason = "Not a modifier-pipeline capture (missing modifiers).";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(version) &&
            !version.Contains("generic-live", StringComparison.OrdinalIgnoreCase) &&
            !version.Contains("modifier-pipeline", StringComparison.OrdinalIgnoreCase))
        {
            skipReason = $"Unrelated diagnostic version '{version}'.";
            return false;
        }

        skipReason = string.Empty;
        return true;
    }

    private static string BuildItemIdentityKey(UniqueCorpusGateCaptureDocument document)
    {
        var name = FirstNonEmpty(
            document.UniqueIdentity?.CanonicalName,
            document.Item?.DisplayName);
        var type = FirstNonEmpty(
            document.UniqueIdentity?.CanonicalType,
            document.Item?.ResolvedBaseName,
            document.Item?.ParsedBaseType);
        var itemClass = document.Item?.ItemClass;
        return string.Join('|', new[] { name, type, itemClass }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static IReadOnlyList<string> CollectDiagnosticCodes(UniqueCorpusGateCaptureModifier modifier)
    {
        var codes = new List<string>();
        Add(codes, modifier.SourceResolution?.UniqueResolutionDiagnosticCode);
        Add(codes, modifier.ProviderResolution?.ProviderDiagnosticCode);
        foreach (var pass in modifier.ProviderPasses ?? [])
        {
            foreach (var diagnostic in pass.Match?.Diagnostics ?? [])
            {
                Add(codes, diagnostic.Code);
            }

            Add(codes, pass.SkipReason is "POE_TRADE_SELECTED_MODIFIER_MISSING_GAMEDATA_PROVENANCE"
                ? pass.SkipReason
                : null);
        }

        Add(codes, modifier.Consumer?.Serialization?.DiagnosticCode);
        return codes;
    }

    private static UniqueCorpusGateOutcomeCounts CountOutcomes(IReadOnlyList<string> outcomes)
    {
        var supported = outcomes.Count(outcome => outcome == UniqueCorpusGateOutcomes.Supported);
        var ambiguous = outcomes.Count(outcome => outcome == UniqueCorpusGateOutcomes.Ambiguous);
        var unsupported = outcomes.Count(outcome => outcome == UniqueCorpusGateOutcomes.Unsupported);
        var other = outcomes.Count - supported - ambiguous - unsupported;
        var total = outcomes.Count;
        return new UniqueCorpusGateOutcomeCounts
        {
            Supported = supported,
            Ambiguous = ambiguous,
            Unsupported = unsupported,
            Other = other,
            Total = total,
            SupportedPercent = Percent(supported, total),
            AmbiguousPercent = Percent(ambiguous, total),
            UnsupportedPercent = Percent(unsupported, total),
            OtherPercent = Percent(other, total),
        };
    }

    private static IReadOnlyList<UniqueCorpusGateBreakdownCount> Breakdown(
        IEnumerable<UniqueCorpusGateAnalyzedComponent> components,
        Func<UniqueCorpusGateAnalyzedComponent, string> keySelector)
    {
        return components
            .GroupBy(keySelector, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var outcomes = CountOutcomes(group.Select(component => component.Outcome).ToArray());
                return new UniqueCorpusGateBreakdownCount
                {
                    Key = group.Key,
                    Supported = outcomes.Supported,
                    Ambiguous = outcomes.Ambiguous,
                    Unsupported = outcomes.Unsupported,
                    Other = outcomes.Other,
                    Total = outcomes.Total,
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<UniqueCorpusGateCluster> BuildClusters(
        IReadOnlyList<UniqueCorpusGateAnalyzedComponent> components)
    {
        return components
            .Where(component => component.Outcome != UniqueCorpusGateOutcomes.Supported)
            .GroupBy(component => component.RootCauseKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var representative = group.First();
                var codes = group
                    .SelectMany(component => component.DiagnosticCodes)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray();
                var signatures = group
                    .GroupBy(component => component.NormalizedSignature, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(signature => signature.Select(entry => entry.ItemIdentityKey).Distinct(StringComparer.OrdinalIgnoreCase).Count())
                    .ThenByDescending(signature => signature.Count())
                    .Select(signature => signature.Key)
                    .Take(8)
                    .ToArray();
                var items = group
                    .GroupBy(component => component.ItemName, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(item => item.Count())
                    .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.Key)
                    .Take(8)
                    .ToArray();
                var families = group
                    .GroupBy(component => component.SourceFamily, StringComparer.Ordinal)
                    .OrderByDescending(family => family.Count())
                    .Select(family => family.Key)
                    .ToArray();
                return new UniqueCorpusGateCluster
                {
                    RootCauseKey = group.Key,
                    Stage = representative.Stage,
                    SourceFamily = families.Length == 1 ? families[0] : string.Join("+", families),
                    DiagnosticCodes = codes.Length == 0 ? [group.Key] : codes,
                    ComponentCount = group.Count(),
                    DistinctItemCount = group
                        .Select(component => component.ItemIdentityKey)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    DistinctSignatureCount = group
                        .Select(component => component.NormalizedSignature)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    TopSignatures = signatures,
                    TopItemNames = items,
                };
            })
            .OrderByDescending(cluster => cluster.DistinctItemCount)
            .ThenByDescending(cluster => cluster.ComponentCount)
            .ThenBy(cluster => cluster.RootCauseKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<UniqueCorpusGateSignatureFamily> BuildSignatureFamilies(
        IReadOnlyList<UniqueCorpusGateAnalyzedComponent> components)
    {
        return components
            .GroupBy(
                component => $"{component.SourceFamily}|{component.NormalizedSignature}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sample = group.First();
                var outcomes = CountOutcomes(group.Select(component => component.Outcome).ToArray());
                return new UniqueCorpusGateSignatureFamily
                {
                    NormalizedSignature = sample.NormalizedSignature,
                    SourceFamily = sample.SourceFamily,
                    ComponentCount = group.Count(),
                    DistinctItemCount = group
                        .Select(component => component.ItemIdentityKey)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    Outcomes = outcomes,
                    TopItemNames = group
                        .Select(component => component.ItemName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .Take(8)
                        .ToArray(),
                    RootCauseKeys = group
                        .Where(component => component.Outcome != UniqueCorpusGateOutcomes.Supported)
                        .Select(component => component.RootCauseKey)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(key => key, StringComparer.Ordinal)
                        .ToArray(),
                };
            })
            .OrderByDescending(family => family.DistinctItemCount)
            .ThenByDescending(family => family.ComponentCount)
            .ThenBy(family => family.NormalizedSignature, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<UniqueCorpusGateRegression> CompareSignatureFamilies(
        IReadOnlyList<UniqueCorpusGateSignatureFamily> current,
        IReadOnlyList<UniqueCorpusGateSignatureFamily> baseline)
    {
        var currentMap = current.ToDictionary(
            family => FamilyKey(family),
            StringComparer.OrdinalIgnoreCase);
        var regressions = new List<UniqueCorpusGateRegression>();
        foreach (var baselineFamily in baseline)
        {
            if (!currentMap.TryGetValue(FamilyKey(baselineFamily), out var currentFamily))
            {
                continue;
            }

            var baselineFailed = baselineFamily.Outcomes.Ambiguous + baselineFamily.Outcomes.Unsupported +
                baselineFamily.Outcomes.Other;
            var currentFailed = currentFamily.Outcomes.Ambiguous + currentFamily.Outcomes.Unsupported +
                currentFamily.Outcomes.Other;
            if (baselineFamily.Outcomes.Supported > 0 &&
                currentFamily.Outcomes.Supported < baselineFamily.Outcomes.Supported &&
                currentFailed > baselineFailed)
            {
                regressions.Add(new UniqueCorpusGateRegression
                {
                    NormalizedSignature = baselineFamily.NormalizedSignature,
                    SourceFamily = baselineFamily.SourceFamily,
                    BaselineSupported = baselineFamily.Outcomes.Supported,
                    CurrentSupported = currentFamily.Outcomes.Supported,
                    BaselineFailed = baselineFailed,
                    CurrentFailed = currentFailed,
                });
            }
        }

        return regressions
            .OrderByDescending(regression => regression.BaselineSupported - regression.CurrentSupported)
            .ThenBy(regression => regression.NormalizedSignature, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FamilyKey(UniqueCorpusGateSignatureFamily family) =>
        $"{family.SourceFamily}|{family.NormalizedSignature}";

    private static UniqueCorpusGateCaptureProviderPass? Last(
        IReadOnlyList<UniqueCorpusGateCaptureProviderPass>? passes) =>
        passes is { Count: > 0 } ? passes[^1] : null;

    private static bool IsSuccessfulProviderStatus(string? status) =>
        EqualsOrdinal(status, "Exact") ||
        EqualsOrdinal(status, "ExactEquivalentSet") ||
        EqualsOrdinal(status, "Approximate") ||
        EqualsOrdinal(status, "BaseGuaranteed");

    private static bool ContainsAmbiguityMarker(string? value) =>
        ContainsOrdinal(value, "AMBIG") ||
        ContainsOrdinal(value, "VERSION") ||
        ContainsOrdinal(value, "CONFLICT") ||
        ContainsOrdinal(value, "INDEPENDENT_DIMENSIONS");

    private static bool EqualsOrdinal(string? left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsOrdinal(string? value, string token) =>
        value?.Contains(token, StringComparison.OrdinalIgnoreCase) == true;

    private static void Add(List<string> codes, string? code)
    {
        if (!string.IsNullOrWhiteSpace(code) &&
            !codes.Contains(code, StringComparer.Ordinal))
        {
            codes.Add(code);
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static decimal Percent(int count, int total) =>
        total == 0 ? 0m : Math.Round(100m * count / total, 2, MidpointRounding.AwayFromZero);
}
