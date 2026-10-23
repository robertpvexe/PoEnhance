using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// Test-only U.2.1 Unique ambiguity scanner. Catalogue-level mechanical mapping preflight
/// plus genuine clipboard replay. Does not call live Trade HTTP and does not fabricate
/// catalogue rows as end-to-end captures.
/// </summary>
internal static class UniqueAmbiguityScanner
{
    public const string CheckA_CataloguePreflight = "A_CatalogueMechanicalMapping";
    public const string CheckB_GenuineClipboardReplay = "B_GenuineClipboardReplay";
    public const string CheckC_SerializedTradeLive = "C_SerializedTradeOrLiveSearchFetch";

    public const string StatusExactResolved = "ExactResolved";
    public const string StatusAmbiguous = "Ambiguous";
    public const string StatusUnsupported = "Unsupported";
    public const string StatusRequirementNonSearchable = "RequirementNonSearchable";
    public const string StatusInsufficientEvidence = "InsufficientEvidence";
    public const string StatusNotEvaluated = "NotEvaluated";

    public const string AmbiguityKindStatMapping = "StatMapping";
    public const string AmbiguityKindItemIdentity = "ItemIdentity";
    public const string AmbiguityKindNone = "None";

    public const string EvidenceCatalogue = "CurrentCatalogDerived";
    public const string EvidenceGenuineCapture = "GenuineClipboardCapture";
    public const string EvidenceSynthetic = "SyntheticTestFixture";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<ScanArtifacts> RunAsync(
        string packagePath,
        string outputDirectory,
        string? replayReadyCapturesDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath, cancellationToken);
        if (!load.IsSuccess || load.Package is null)
        {
            throw new InvalidOperationException(
                "GameData package load failed: " +
                string.Join("; ", load.Diagnostics.Select(d => d.Message)));
        }

        var package = load.Package;
        var catalog = GameDataCatalog.FromPackage(package);
        var uniqueCatalog = package.UniqueItems
            ?? throw new InvalidOperationException("Package has no UniqueItems catalog.");

        var findings = new List<Finding>();
        var currentIdentities = uniqueCatalog.Items
            .Where(item => item.Versions.Any(v => v.Role == UniqueItemVersionRole.Current))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ThenBy(item => item.CanonicalName, StringComparer.Ordinal)
            .ToArray();

        var assessableIdentities = 0;
        var modRowsAssessed = 0;
        var generatedCandidateBlocks = 0;
        var optionAxisVersions = 0;
        var identitiesWithoutUniqueBlocks = 0;

        foreach (var item in currentIdentities)
        {
            var currentVersions = item.Versions
                .Where(v => v.Role == UniqueItemVersionRole.Current)
                .OrderBy(v => v.Id, StringComparer.Ordinal)
                .ToArray();
            if (currentVersions.Length == 0)
            {
                continue;
            }

            assessableIdentities++;
            var uniqueBlocks = currentVersions
                .SelectMany(v => v.ModifierBlocks
                    .Where(b => b.Kind == UniqueModifierBlockKind.Unique)
                    .Select(b => (Version: v, Block: b)))
                .ToArray();

            if (uniqueBlocks.Length == 0)
            {
                identitiesWithoutUniqueBlocks++;
            }

            foreach (var version in currentVersions)
            {
                if (version.OptionAxes.Count > 0)
                {
                    optionAxisVersions++;
                }
            }

            foreach (var (version, block) in uniqueBlocks
                .OrderBy(entry => entry.Version.Id, StringComparer.Ordinal)
                .ThenBy(entry => entry.Block.Id, StringComparer.Ordinal))
            {
                if (block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate)
                {
                    generatedCandidateBlocks++;
                }

                modRowsAssessed++;
                findings.Add(ClassifyCatalogueBlock(package, item, version, block));
            }
        }

        var captureFindings = RunGenuineClipboardTier(
            catalog,
            package,
            replayReadyCapturesDirectory);
        findings.AddRange(captureFindings);

        // Check C is explicitly not executed for the mass scan.
        findings.Add(new Finding
        {
            DedupKey = $"{CheckC_SerializedTradeLive}|not-run",
            Check = CheckC_SerializedTradeLive,
            Status = StatusNotEvaluated,
            AmbiguityKind = AmbiguityKindNone,
            UniqueName = null,
            UniqueIdentityId = null,
            BaseType = null,
            ModText = null,
            ModBlockKind = null,
            SourceSemantics = null,
            Reason = "Mass scan does not serialize final Trade JSON or perform live Search/Fetch. " +
                "Frozen official Trade catalogue may not contain newly added live IDs. " +
                "Live HTTP is forbidden for U.2.1 mass diagnostics.",
            DiagnosticCode = null,
            CandidateStatIds = [],
            CandidateModifierIds = [],
            ConflictCandidateModifierIds = [],
            OptionAxisIds = [],
            PipelineStage = "NotRun",
            EvidenceTier = "NotApplicable",
            ProvenanceNotes = "reason unavailable for Trade provider IDs (check not executed)",
            CatalogDataVersion = package.Manifest.DataVersion,
            CatalogSchemaVersion = package.Manifest.SchemaVersion,
            CaptureFileName = null,
        });

        var deduped = Deduplicate(findings);
        var sorted = StableSort(deduped);

        var statusCounts = CountByStatus(sorted);
        var checkCounts = sorted
            .GroupBy(f => f.Check, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var summary = new ScanSummary
        {
            GeneratedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            PackagePath = Path.GetFullPath(packagePath),
            CatalogDataVersion = package.Manifest.DataVersion,
            CatalogSchemaVersion = package.Manifest.SchemaVersion,
            OutputDirectory = Path.GetFullPath(outputDirectory),
            Checks =
            [
                new CheckCoverage
                {
                    Check = CheckA_CataloguePreflight,
                    Executed = true,
                    Description =
                        "GameData/catalogue-level preflight across current Unique definitions " +
                        "using packaged UniqueModifierMechanicalMapping (import-time production resolution).",
                },
                new CheckCoverage
                {
                    Check = CheckB_GenuineClipboardReplay,
                    Executed = captureFindings.Count > 0,
                    Description =
                        "Existing genuine clipboard captures replayed through ItemTextParser + " +
                        "ParsedUniqueItemResolver only. Synthetic/catalogue rows are NOT treated as captures.",
                },
                new CheckCoverage
                {
                    Check = CheckC_SerializedTradeLive,
                    Executed = false,
                    Description =
                        "Serialized final Trade JSON / live Search-Fetch - NOT tested in U.2.1 mass scan. " +
                        "No live HTTP. Frozen Aug-19-2026 official stats snapshot may omit newer live IDs.",
                },
            ],
            Denominator = new Denominator
            {
                UniqueIdentitiesEnumerated = currentIdentities.Length,
                UniqueIdentitiesAssessable = assessableIdentities,
                ModRowsAssessed = modRowsAssessed,
                GenuineClipboardCapturesReplayed = captureFindings
                    .Select(f => f.CaptureFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                GenuineClipboardFindings = captureFindings.Count,
                FindingsTotalAfterDedup = sorted.Count,
                FindingsByStatus = statusCounts,
                FindingsByCheck = checkCounts,
            },
            CoverageLimits =
            [
                "Check A evaluates packaged mechanical mapping status per Current Unique modifier block; " +
                "it is not an end-to-end clipboard->Trade replay and must not be equated with % of playable Unique items.",
                "Historical Unique versions are excluded (Role != Current).",
                "Unique-owned Implicit blocks are excluded from mod-row assessment (Kind != Unique).",
                $"GeneratedCandidate blocks observed on Current Unique rows: {generatedCandidateBlocks} " +
                "(catalogue mapping is reported; live option selection is not simulated).",
                $"Current versions with OptionAxes: {optionAxisVersions} " +
                "(orthogonal option axes are not expanded into synthetic whole-item combinations).",
                $"Current identities with zero Unique-kind blocks: {identitiesWithoutUniqueBlocks}.",
                "Trade provider / official stat ID matching and serialized query confidence are out of scope for the mass scan (Check C NotEvaluated).",
                "Ambiguous item identity is only observable on Check B when a genuine capture matches multiple catalog identities.",
                "Dedup key = Check|UniqueIdentityId|VersionId|BlockId|ModText|CaptureFileName|ParsedModifierIndex (empty segments allowed).",
            ],
            TopProblematicExamples = sorted
                .Where(f => f.Status is StatusAmbiguous or StatusUnsupported or StatusInsufficientEvidence)
                .Where(f => f.Check != CheckC_SerializedTradeLive)
                .Take(20)
                .Select(ToExample)
                .ToArray(),
        };

        var findingsPath = Path.Combine(outputDirectory, "unique-ambiguity-findings.json");
        var csvPath = Path.Combine(outputDirectory, "unique-ambiguity-findings.csv");
        var summaryPath = Path.Combine(outputDirectory, "unique-ambiguity-summary.md");
        var summaryJsonPath = Path.Combine(outputDirectory, "unique-ambiguity-summary.json");

        var payload = new FindingsDocument
        {
            Summary = summary,
            Findings = sorted,
        };

        await File.WriteAllTextAsync(
            findingsPath,
            JsonSerializer.Serialize(payload, JsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            summaryJsonPath,
            JsonSerializer.Serialize(summary, JsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(csvPath, ToCsv(sorted), cancellationToken);
        await File.WriteAllTextAsync(summaryPath, ToMarkdown(summary, sorted), cancellationToken);

        return new ScanArtifacts(
            summary,
            sorted,
            findingsPath,
            csvPath,
            summaryPath,
            summaryJsonPath);
    }

    private static Finding ClassifyCatalogueBlock(
        GameDataPackage package,
        UniqueItemIdentity item,
        UniqueItemVersionObservation version,
        UniqueModifierBlock block)
    {
        var mapping = block.MechanicalMapping;
        var (status, ambiguityKind, reason) = MapMechanicalStatus(mapping);
        var conflictMods = mapping.ConflictEvidence?.Candidates
            .Select(c => c.ModifierId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToArray() ?? [];
        var conflictStats = mapping.ConflictEvidence?.Candidates
            .SelectMany(c => c.StatIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        var optionAxisIds = block.OptionChoiceMemberships
            .Select(m => m.OptionAxisId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToArray();

        var modText = string.Join('\n', block.Lines);
        var provenance = BuildCatalogueProvenance(mapping);

        return new Finding
        {
            DedupKey = BuildDedupKey(
                CheckA_CataloguePreflight,
                item.Id,
                version.Id,
                block.Id,
                modText,
                captureFileName: null,
                parsedModifierIndex: null),
            Check = CheckA_CataloguePreflight,
            Status = status,
            AmbiguityKind = ambiguityKind,
            UniqueName = item.CanonicalName,
            UniqueIdentityId = item.Id,
            UniqueVersionId = version.Id,
            UniqueBlockId = block.Id,
            BaseType = version.BaseType,
            ModText = modText,
            ModBlockKind = block.Kind.ToString(),
            SourceSemantics = block.SourceSemantics.ToString(),
            Reason = reason,
            DiagnosticCode = mapping.DiagnosticCode,
            CandidateStatIds = mapping.StatIds.Count > 0
                ? mapping.StatIds.ToArray()
                : conflictStats,
            CandidateModifierIds = mapping.ModifierIds.Count > 0
                ? mapping.ModifierIds.ToArray()
                : conflictMods,
            ConflictCandidateModifierIds = conflictMods,
            ConflictKind = mapping.ConflictEvidence?.Kind.ToString(),
            OptionAxisIds = optionAxisIds,
            PipelineStage = "GameData.UniqueModifierMechanicalMapping",
            EvidenceTier = EvidenceCatalogue,
            ProvenanceNotes = provenance,
            CatalogDataVersion = package.Manifest.DataVersion,
            CatalogSchemaVersion = package.Manifest.SchemaVersion,
            CaptureFileName = null,
        };
    }

    private static (string Status, string AmbiguityKind, string Reason) MapMechanicalStatus(
        UniqueModifierMechanicalMapping mapping)
    {
        switch (mapping.Status)
        {
            case UniqueModifierMechanicalMappingStatus.Exact:
                return (
                    StatusExactResolved,
                    AmbiguityKindNone,
                    string.IsNullOrWhiteSpace(mapping.Diagnostic)
                        ? "Packaged mechanical mapping status Exact."
                        : mapping.Diagnostic!);
            case UniqueModifierMechanicalMappingStatus.EquivalentSourceSet:
                return (
                    StatusExactResolved,
                    AmbiguityKindNone,
                    string.IsNullOrWhiteSpace(mapping.Diagnostic)
                        ? "Packaged mechanical mapping status EquivalentSourceSet (exact-equivalent)."
                        : mapping.Diagnostic!);
            case UniqueModifierMechanicalMappingStatus.Ambiguous:
                return (
                    StatusAmbiguous,
                    AmbiguityKindStatMapping,
                    BuildAmbiguousReason(mapping));
            case UniqueModifierMechanicalMappingStatus.Unsupported:
                return (
                    StatusUnsupported,
                    AmbiguityKindNone,
                    string.IsNullOrWhiteSpace(mapping.Diagnostic)
                        ? (string.IsNullOrWhiteSpace(mapping.DiagnosticCode)
                            ? "Packaged mechanical mapping status Unsupported; reason unavailable."
                            : $"Unsupported ({mapping.DiagnosticCode}).")
                        : mapping.Diagnostic!);
            case UniqueModifierMechanicalMappingStatus.NonSearchable:
                return (
                    StatusRequirementNonSearchable,
                    AmbiguityKindNone,
                    string.IsNullOrWhiteSpace(mapping.Diagnostic)
                        ? (string.IsNullOrWhiteSpace(mapping.DiagnosticCode)
                            ? "Packaged mechanical mapping status NonSearchable (requirement/display-only)."
                            : $"Requirement/NonSearchable ({mapping.DiagnosticCode}).")
                        : mapping.Diagnostic!);
            case UniqueModifierMechanicalMappingStatus.Unknown:
                return (
                    StatusInsufficientEvidence,
                    AmbiguityKindNone,
                    "Packaged mechanical mapping status Unknown; diagnostic blocked / insufficient evidence.");
            default:
                return (
                    StatusNotEvaluated,
                    AmbiguityKindNone,
                    $"Unrecognized mechanical mapping status '{mapping.Status}'; diagnostic blocked.");
        }
    }

    private static string BuildAmbiguousReason(UniqueModifierMechanicalMapping mapping)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(mapping.DiagnosticCode))
        {
            parts.Add(mapping.DiagnosticCode!);
        }

        if (!string.IsNullOrWhiteSpace(mapping.Diagnostic))
        {
            parts.Add(mapping.Diagnostic!);
        }
        else if (parts.Count == 0)
        {
            parts.Add("reason unavailable");
        }

        if (mapping.ConflictEvidence is { } evidence)
        {
            parts.Add($"conflictKind={evidence.Kind}");
            var candidateIds = evidence.Candidates
                .Select(c => c.ModifierId)
                .Where(id => !string.IsNullOrWhiteSpace(id));
            parts.Add("conflictCandidates=[" + string.Join(',', candidateIds) + "]");
        }

        return string.Join("; ", parts);
    }

    private static string BuildCatalogueProvenance(UniqueModifierMechanicalMapping mapping)
    {
        var notes = new List<string>();
        if (mapping.Provenance?.ResolutionReasons is { Count: > 0 } reasons)
        {
            notes.Add("resolutionReasons=[" + string.Join(',', reasons) + "]");
        }

        if (mapping.ConflictEvidence is { } evidence)
        {
            notes.Add($"conflictKind={evidence.Kind}; candidateCount={evidence.CandidateCount}");
        }

        if (notes.Count == 0)
        {
            return mapping.Status is UniqueModifierMechanicalMappingStatus.Exact
                or UniqueModifierMechanicalMappingStatus.EquivalentSourceSet
                ? "ordinary exact-vector / equivalent packaged provenance"
                : "provenance notes unavailable";
        }

        return string.Join("; ", notes);
    }

    private static List<Finding> RunGenuineClipboardTier(
        GameDataCatalog catalog,
        GameDataPackage package,
        string? capturesDirectory)
    {
        var findings = new List<Finding>();
        if (string.IsNullOrWhiteSpace(capturesDirectory) || !Directory.Exists(capturesDirectory))
        {
            return findings;
        }

        var files = Directory.GetFiles(capturesDirectory, "*.clipboard.txt")
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var parser = new ItemTextParser();
        var resolver = new ParsedUniqueItemResolver();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var raw = File.ReadAllText(file);
            var parsed = parser.Parse(raw);
            var resolution = resolver.Resolve(parsed, catalog);

            if (resolution.Status == UniqueItemResolutionStatus.AmbiguousIdentity)
            {
                findings.Add(new Finding
                {
                    DedupKey = BuildDedupKey(
                        CheckB_GenuineClipboardReplay,
                        string.Join(',', resolution.IdentityCandidates.Select(i => i.Id)),
                        versionId: null,
                        blockId: null,
                        modText: parsed.DisplayName,
                        captureFileName: fileName,
                        parsedModifierIndex: null),
                    Check = CheckB_GenuineClipboardReplay,
                    Status = StatusAmbiguous,
                    AmbiguityKind = AmbiguityKindItemIdentity,
                    UniqueName = parsed.DisplayName,
                    UniqueIdentityId = null,
                    BaseType = parsed.BaseType,
                    ModText = null,
                    Reason = resolution.Diagnostic ?? "UNIQUE_IDENTITY_AMBIGUOUS",
                    DiagnosticCode = resolution.DiagnosticCode,
                    CandidateStatIds = [],
                    CandidateModifierIds = [],
                    ConflictCandidateModifierIds = resolution.IdentityCandidates
                        .Select(i => i.Id)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Cast<string>()
                        .ToArray(),
                    OptionAxisIds = [],
                    PipelineStage = "ParsedUniqueItemResolver.Identity",
                    EvidenceTier = EvidenceGenuineCapture,
                    ProvenanceNotes = "identityCandidates=[" +
                        string.Join(',', resolution.IdentityCandidates.Select(i => i.CanonicalName)) + "]",
                    CatalogDataVersion = package.Manifest.DataVersion,
                    CatalogSchemaVersion = package.Manifest.SchemaVersion,
                    CaptureFileName = fileName,
                });
                continue;
            }

            if (resolution.Status is UniqueItemResolutionStatus.Unsupported
                or UniqueItemResolutionStatus.NotApplicable)
            {
                findings.Add(new Finding
                {
                    DedupKey = BuildDedupKey(
                        CheckB_GenuineClipboardReplay,
                        resolution.Identity?.Id,
                        versionId: null,
                        blockId: null,
                        modText: parsed.DisplayName,
                        captureFileName: fileName,
                        parsedModifierIndex: -1),
                    Check = CheckB_GenuineClipboardReplay,
                    Status = resolution.Status == UniqueItemResolutionStatus.NotApplicable
                        ? StatusNotEvaluated
                        : StatusUnsupported,
                    AmbiguityKind = AmbiguityKindItemIdentity,
                    UniqueName = parsed.DisplayName,
                    UniqueIdentityId = resolution.Identity?.Id,
                    BaseType = parsed.BaseType,
                    ModText = null,
                    Reason = resolution.Diagnostic
                        ?? (string.IsNullOrWhiteSpace(resolution.DiagnosticCode)
                            ? "reason unavailable"
                            : resolution.DiagnosticCode),
                    DiagnosticCode = resolution.DiagnosticCode,
                    CandidateStatIds = [],
                    CandidateModifierIds = [],
                    ConflictCandidateModifierIds = [],
                    OptionAxisIds = [],
                    PipelineStage = "ParsedUniqueItemResolver.Identity",
                    EvidenceTier = EvidenceGenuineCapture,
                    ProvenanceNotes = "genuine clipboard identity resolution",
                    CatalogDataVersion = package.Manifest.DataVersion,
                    CatalogSchemaVersion = package.Manifest.SchemaVersion,
                    CaptureFileName = fileName,
                });
                continue;
            }

            // ExactIdentity path: emit per resolved unique modifier block.
            if (resolution.ModifierBlocks.Count == 0)
            {
                findings.Add(new Finding
                {
                    DedupKey = BuildDedupKey(
                        CheckB_GenuineClipboardReplay,
                        resolution.Identity?.Id,
                        versionId: null,
                        blockId: null,
                        modText: "(no unique modifier blocks on capture)",
                        captureFileName: fileName,
                        parsedModifierIndex: null),
                    Check = CheckB_GenuineClipboardReplay,
                    Status = StatusInsufficientEvidence,
                    AmbiguityKind = AmbiguityKindNone,
                    UniqueName = resolution.Identity?.CanonicalName ?? parsed.DisplayName,
                    UniqueIdentityId = resolution.Identity?.Id,
                    BaseType = parsed.BaseType,
                    ModText = null,
                    Reason = "Capture resolved ExactIdentity but exposed no Unique modifier block resolutions.",
                    DiagnosticCode = resolution.DiagnosticCode,
                    CandidateStatIds = [],
                    CandidateModifierIds = [],
                    ConflictCandidateModifierIds = [],
                    OptionAxisIds = [],
                    PipelineStage = "ParsedUniqueItemResolver.ModifierBlocks",
                    EvidenceTier = EvidenceGenuineCapture,
                    ProvenanceNotes = "identity exact; modifier rows absent on capture",
                    CatalogDataVersion = package.Manifest.DataVersion,
                    CatalogSchemaVersion = package.Manifest.SchemaVersion,
                    CaptureFileName = fileName,
                });
                continue;
            }

            foreach (var block in resolution.ModifierBlocks.OrderBy(b => b.ParsedModifierIndex))
            {
                findings.Add(ClassifyCaptureBlock(package, resolution, parsed, block, fileName));
            }
        }

        return findings;
    }

    private static Finding ClassifyCaptureBlock(
        GameDataPackage package,
        UniqueItemResolutionResult resolution,
        ParsedItem parsed,
        UniqueModifierBlockResolution block,
        string fileName)
    {
        string status;
        string ambiguityKind;
        string reason;

        if (block.IsResolved)
        {
            status = StatusExactResolved;
            ambiguityKind = AmbiguityKindNone;
            reason = block.IsEquivalentSourceSet
                ? "Capture block resolved as equivalent source set."
                : "Capture block resolved exactly.";
        }
        else if (block.ConflictEvidence is not null ||
                 string.Equals(block.DiagnosticCode, "UNIQUE_MECHANICS_EXACT_CONFLICT", StringComparison.Ordinal) ||
                 string.Equals(block.DiagnosticCode, "UNIQUE_MECHANICS_CONFLICT", StringComparison.Ordinal))
        {
            status = StatusAmbiguous;
            ambiguityKind = AmbiguityKindStatMapping;
            reason = BuildCaptureAmbiguousReason(block);
        }
        else if (!string.IsNullOrWhiteSpace(block.DiagnosticCode) &&
                 block.DiagnosticCode.Contains("UNSUPPORTED", StringComparison.OrdinalIgnoreCase))
        {
            status = StatusUnsupported;
            ambiguityKind = AmbiguityKindNone;
            reason = block.Diagnostic ?? block.DiagnosticCode!;
        }
        else if (string.IsNullOrWhiteSpace(block.DiagnosticCode) && string.IsNullOrWhiteSpace(block.Diagnostic))
        {
            status = StatusInsufficientEvidence;
            ambiguityKind = AmbiguityKindNone;
            reason = "Unresolved capture block without diagnostic code; reason unavailable.";
        }
        else
        {
            // Fail-closed unresolved rows with a diagnostic are treated as Unsupported
            // unless conflict evidence already classified them as Ambiguous above.
            status = StatusUnsupported;
            ambiguityKind = AmbiguityKindNone;
            reason = block.Diagnostic
                ?? (string.IsNullOrWhiteSpace(block.DiagnosticCode)
                    ? "reason unavailable"
                    : block.DiagnosticCode!);
        }

        var catalogBlockId = block.CatalogBlocks.FirstOrDefault()?.Id;
        var modText = block.PresentationLines.Count > 0
            ? string.Join('\n', block.PresentationLines)
            : (parsed.UniqueModifiers.ElementAtOrDefault(block.ParsedModifierIndex) is { } pm
                ? string.Join('\n', pm.Effects.Select(e => e.Text))
                : null);

        var conflictMods = block.ConflictEvidence?.Candidates
            .Select(c => c.ModifierId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToArray() ?? [];

        return new Finding
        {
            DedupKey = BuildDedupKey(
                CheckB_GenuineClipboardReplay,
                resolution.Identity?.Id,
                resolution.CompatibleVersions.FirstOrDefault()?.Id,
                catalogBlockId,
                modText,
                fileName,
                block.ParsedModifierIndex),
            Check = CheckB_GenuineClipboardReplay,
            Status = status,
            AmbiguityKind = ambiguityKind,
            UniqueName = resolution.Identity?.CanonicalName ?? parsed.DisplayName,
            UniqueIdentityId = resolution.Identity?.Id,
            UniqueVersionId = resolution.CompatibleVersions.FirstOrDefault()?.Id,
            UniqueBlockId = catalogBlockId,
            BaseType = parsed.BaseType,
            ModText = modText,
            ModBlockKind = "Unique",
            SourceSemantics = block.SourceSemantics.ToString(),
            Reason = reason,
            DiagnosticCode = block.DiagnosticCode,
            CandidateStatIds = block.StatIds.ToArray(),
            CandidateModifierIds = block.ModifierIds.Count > 0
                ? block.ModifierIds.ToArray()
                : conflictMods,
            ConflictCandidateModifierIds = conflictMods,
            ConflictKind = block.ConflictEvidence?.Kind.ToString(),
            OptionAxisIds = block.OptionChoiceMemberships
                .Select(m => m.OptionAxisId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Cast<string>()
                .ToArray(),
            PipelineStage = "ParsedUniqueItemResolver.ModifierBlocks",
            EvidenceTier = EvidenceGenuineCapture,
            ProvenanceNotes = block.ConflictEvidence is not null
                ? $"conflictKind={block.ConflictEvidence.Kind}; candidateCount={block.ConflictEvidence.CandidateCount}"
                : "genuine clipboard modifier resolution",
            CatalogDataVersion = package.Manifest.DataVersion,
            CatalogSchemaVersion = package.Manifest.SchemaVersion,
            CaptureFileName = fileName,
            ParsedModifierIndex = block.ParsedModifierIndex,
        };
    }

    private static string BuildCaptureAmbiguousReason(UniqueModifierBlockResolution block)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(block.DiagnosticCode))
        {
            parts.Add(block.DiagnosticCode!);
        }

        if (!string.IsNullOrWhiteSpace(block.Diagnostic))
        {
            parts.Add(block.Diagnostic!);
        }
        else if (parts.Count == 0)
        {
            parts.Add("reason unavailable");
        }

        if (block.ConflictEvidence is { } evidence)
        {
            parts.Add($"conflictKind={evidence.Kind}");
            parts.Add(
                "conflictCandidates=[" +
                string.Join(',', evidence.Candidates.Select(c => c.ModifierId)) +
                "]");
        }

        return string.Join("; ", parts);
    }

    public static string BuildDedupKey(
        string check,
        string? uniqueIdentityId,
        string? versionId,
        string? blockId,
        string? modText,
        string? captureFileName,
        int? parsedModifierIndex) =>
        string.Join(
            '|',
            check,
            uniqueIdentityId ?? string.Empty,
            versionId ?? string.Empty,
            blockId ?? string.Empty,
            modText ?? string.Empty,
            captureFileName ?? string.Empty,
            parsedModifierIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private static List<Finding> Deduplicate(IEnumerable<Finding> findings) =>
        findings
            .GroupBy(f => f.DedupKey, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

    private static List<Finding> StableSort(IEnumerable<Finding> findings) =>
        findings
            .OrderBy(f => f.Check, StringComparer.Ordinal)
            .ThenBy(f => StatusSortOrder(f.Status))
            .ThenBy(f => f.UniqueName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.UniqueIdentityId, StringComparer.Ordinal)
            .ThenBy(f => f.UniqueVersionId, StringComparer.Ordinal)
            .ThenBy(f => f.UniqueBlockId, StringComparer.Ordinal)
            .ThenBy(f => f.ModText, StringComparer.Ordinal)
            .ThenBy(f => f.CaptureFileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.ParsedModifierIndex ?? int.MinValue)
            .ThenBy(f => f.DedupKey, StringComparer.Ordinal)
            .ToList();

    private static int StatusSortOrder(string status) => status switch
    {
        StatusAmbiguous => 0,
        StatusUnsupported => 1,
        StatusRequirementNonSearchable => 2,
        StatusInsufficientEvidence => 3,
        StatusNotEvaluated => 4,
        StatusExactResolved => 5,
        _ => 6,
    };

    private static Dictionary<string, int> CountByStatus(IReadOnlyList<Finding> findings) =>
        new[]
        {
            StatusExactResolved,
            StatusAmbiguous,
            StatusUnsupported,
            StatusRequirementNonSearchable,
            StatusInsufficientEvidence,
            StatusNotEvaluated,
        }.ToDictionary(
            status => status,
            status => findings.Count(f => f.Status == status),
            StringComparer.Ordinal);

    private static ProblematicExample ToExample(Finding finding) => new()
    {
        Check = finding.Check,
        Status = finding.Status,
        AmbiguityKind = finding.AmbiguityKind,
        UniqueName = finding.UniqueName,
        BaseType = finding.BaseType,
        ModText = finding.ModText,
        Reason = finding.Reason,
        DiagnosticCode = finding.DiagnosticCode,
        CandidateModifierIds = finding.CandidateModifierIds,
        ConflictKind = finding.ConflictKind,
        EvidenceTier = finding.EvidenceTier,
        CaptureFileName = finding.CaptureFileName,
    };

    private static string ToCsv(IReadOnlyList<Finding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
        [
            "DedupKey",
            "Check",
            "Status",
            "AmbiguityKind",
            "UniqueName",
            "UniqueIdentityId",
            "UniqueVersionId",
            "UniqueBlockId",
            "BaseType",
            "ModText",
            "ModBlockKind",
            "SourceSemantics",
            "Reason",
            "DiagnosticCode",
            "CandidateStatIds",
            "CandidateModifierIds",
            "ConflictCandidateModifierIds",
            "ConflictKind",
            "OptionAxisIds",
            "PipelineStage",
            "EvidenceTier",
            "ProvenanceNotes",
            "CatalogDataVersion",
            "CatalogSchemaVersion",
            "CaptureFileName",
            "ParsedModifierIndex",
        ]));

        foreach (var f in findings)
        {
            sb.AppendLine(string.Join(',',
            [
                Csv(f.DedupKey),
                Csv(f.Check),
                Csv(f.Status),
                Csv(f.AmbiguityKind),
                Csv(f.UniqueName),
                Csv(f.UniqueIdentityId),
                Csv(f.UniqueVersionId),
                Csv(f.UniqueBlockId),
                Csv(f.BaseType),
                Csv(f.ModText),
                Csv(f.ModBlockKind),
                Csv(f.SourceSemantics),
                Csv(f.Reason),
                Csv(f.DiagnosticCode),
                Csv(string.Join(';', f.CandidateStatIds)),
                Csv(string.Join(';', f.CandidateModifierIds)),
                Csv(string.Join(';', f.ConflictCandidateModifierIds)),
                Csv(f.ConflictKind),
                Csv(string.Join(';', f.OptionAxisIds)),
                Csv(f.PipelineStage),
                Csv(f.EvidenceTier),
                Csv(f.ProvenanceNotes),
                Csv(f.CatalogDataVersion),
                Csv(f.CatalogSchemaVersion?.ToString(CultureInfo.InvariantCulture)),
                Csv(f.CaptureFileName),
                Csv(f.ParsedModifierIndex?.ToString(CultureInfo.InvariantCulture)),
            ]));
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }

    private static string ToMarkdown(ScanSummary summary, IReadOnlyList<Finding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# PoEnhance U.2.1 - Unique Ambiguity Scanner Summary");
        sb.AppendLine();
        sb.AppendLine($"- GeneratedAtUtc: `{summary.GeneratedAtUtc}`");
        sb.AppendLine($"- CatalogDataVersion: `{summary.CatalogDataVersion}` (schema {summary.CatalogSchemaVersion})");
        sb.AppendLine($"- Package: `{summary.PackagePath}`");
        sb.AppendLine($"- Output: `{summary.OutputDirectory}`");
        sb.AppendLine();
        sb.AppendLine("## Checks");
        foreach (var check in summary.Checks)
        {
            sb.AppendLine(
                $"- **{check.Check}** - executed={check.Executed}: {check.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("## Denominator");
        sb.AppendLine($"- Unique identities enumerated (Current): **{summary.Denominator.UniqueIdentitiesEnumerated}**");
        sb.AppendLine($"- Unique identities assessable: **{summary.Denominator.UniqueIdentitiesAssessable}**");
        sb.AppendLine($"- Mod rows assessed (catalogue Unique-kind blocks): **{summary.Denominator.ModRowsAssessed}**");
        sb.AppendLine($"- Genuine clipboard captures replayed: **{summary.Denominator.GenuineClipboardCapturesReplayed}**");
        sb.AppendLine($"- Genuine clipboard findings: **{summary.Denominator.GenuineClipboardFindings}**");
        sb.AppendLine($"- Findings after dedup: **{summary.Denominator.FindingsTotalAfterDedup}**");
        sb.AppendLine();
        sb.AppendLine("### Findings by status");
        foreach (var (status, count) in summary.Denominator.FindingsByStatus)
        {
            sb.AppendLine($"- {status}: **{count}**");
        }

        sb.AppendLine();
        sb.AppendLine("### Findings by check");
        foreach (var (check, count) in summary.Denominator.FindingsByCheck)
        {
            sb.AppendLine($"- {check}: **{count}**");
        }

        sb.AppendLine();
        sb.AppendLine("## Coverage limits");
        foreach (var limit in summary.CoverageLimits)
        {
            sb.AppendLine($"- {limit}");
        }

        sb.AppendLine();
        sb.AppendLine("## Top problematic examples (up to 20)");
        if (summary.TopProblematicExamples.Count == 0)
        {
            sb.AppendLine("_No Ambiguous / Unsupported / InsufficientEvidence findings outside Check C._");
        }
        else
        {
            var index = 1;
            foreach (var example in summary.TopProblematicExamples)
            {
                sb.AppendLine(
                    $"{index}. `{example.Status}` / {example.AmbiguityKind} - " +
                    $"**{example.UniqueName}** ({example.BaseType}) - `{Truncate(example.ModText, 120)}`");
                sb.AppendLine($"   - check: {example.Check}; evidence: {example.EvidenceTier}");
                sb.AppendLine($"   - reason: {example.Reason}");
                if (!string.IsNullOrWhiteSpace(example.DiagnosticCode))
                {
                    sb.AppendLine($"   - diagnosticCode: `{example.DiagnosticCode}`");
                }

                if (example.CandidateModifierIds.Count > 0)
                {
                    sb.AppendLine(
                        $"   - candidates: `{string.Join(", ", example.CandidateModifierIds)}`");
                }

                if (!string.IsNullOrWhiteSpace(example.CaptureFileName))
                {
                    sb.AppendLine($"   - capture: `{example.CaptureFileName}`");
                }

                index++;
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Accounting note");
        sb.AppendLine(
            "Catalogue mapping-row percentages are **not** percentages of playable Unique items. " +
            "Check C was not executed.");
        sb.AppendLine();
        sb.AppendLine($"Full machine-readable findings: `{findings.Count}` rows (see JSON/CSV beside this file).");
        return sb.ToString();
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(no mod text)";
        }

        var flat = text.Replace('\n', ' ').Replace('\r', ' ');
        return flat.Length <= max ? flat : flat[..max] + "…";
    }

    internal sealed record ScanArtifacts(
        ScanSummary Summary,
        IReadOnlyList<Finding> Findings,
        string FindingsJsonPath,
        string FindingsCsvPath,
        string SummaryMarkdownPath,
        string SummaryJsonPath);

    internal sealed class FindingsDocument
    {
        public required ScanSummary Summary { get; init; }

        public required IReadOnlyList<Finding> Findings { get; init; }
    }

    internal sealed class ScanSummary
    {
        public required string GeneratedAtUtc { get; init; }

        public required string PackagePath { get; init; }

        public string? CatalogDataVersion { get; init; }

        public int? CatalogSchemaVersion { get; init; }

        public required string OutputDirectory { get; init; }

        public required IReadOnlyList<CheckCoverage> Checks { get; init; }

        public required Denominator Denominator { get; init; }

        public required IReadOnlyList<string> CoverageLimits { get; init; }

        public required IReadOnlyList<ProblematicExample> TopProblematicExamples { get; init; }
    }

    internal sealed class CheckCoverage
    {
        public required string Check { get; init; }

        public required bool Executed { get; init; }

        public required string Description { get; init; }
    }

    internal sealed class Denominator
    {
        public required int UniqueIdentitiesEnumerated { get; init; }

        public required int UniqueIdentitiesAssessable { get; init; }

        public required int ModRowsAssessed { get; init; }

        public required int GenuineClipboardCapturesReplayed { get; init; }

        public required int GenuineClipboardFindings { get; init; }

        public required int FindingsTotalAfterDedup { get; init; }

        public required Dictionary<string, int> FindingsByStatus { get; init; }

        public required Dictionary<string, int> FindingsByCheck { get; init; }
    }

    internal sealed class ProblematicExample
    {
        public required string Check { get; init; }

        public required string Status { get; init; }

        public required string AmbiguityKind { get; init; }

        public string? UniqueName { get; init; }

        public string? BaseType { get; init; }

        public string? ModText { get; init; }

        public string? Reason { get; init; }

        public string? DiagnosticCode { get; init; }

        public IReadOnlyList<string> CandidateModifierIds { get; init; } = [];

        public string? ConflictKind { get; init; }

        public string? EvidenceTier { get; init; }

        public string? CaptureFileName { get; init; }
    }

    internal sealed class Finding
    {
        public required string DedupKey { get; init; }

        public required string Check { get; init; }

        public required string Status { get; init; }

        public required string AmbiguityKind { get; init; }

        public string? UniqueName { get; init; }

        public string? UniqueIdentityId { get; init; }

        public string? UniqueVersionId { get; init; }

        public string? UniqueBlockId { get; init; }

        public string? BaseType { get; init; }

        public string? ModText { get; init; }

        public string? ModBlockKind { get; init; }

        public string? SourceSemantics { get; init; }

        public string? Reason { get; init; }

        public string? DiagnosticCode { get; init; }

        public IReadOnlyList<string> CandidateStatIds { get; init; } = [];

        public IReadOnlyList<string> CandidateModifierIds { get; init; } = [];

        public IReadOnlyList<string> ConflictCandidateModifierIds { get; init; } = [];

        public string? ConflictKind { get; init; }

        public IReadOnlyList<string> OptionAxisIds { get; init; } = [];

        public required string PipelineStage { get; init; }

        public required string EvidenceTier { get; init; }

        public string? ProvenanceNotes { get; init; }

        public string? CatalogDataVersion { get; init; }

        public int? CatalogSchemaVersion { get; init; }

        public string? CaptureFileName { get; init; }

        public int? ParsedModifierIndex { get; init; }
    }
}
