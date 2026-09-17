namespace PoEnhance.DataTool.UniqueCorpusGate;

/// <summary>
/// Flattened facts extracted from a modifier-pipeline diagnostic component.
/// </summary>
public sealed class UniqueCorpusGateObservationalFacts
{
    public required string ItemIdentityKey { get; init; }

    public required string ItemName { get; init; }

    public string ItemClass { get; init; } = "-";

    public string BaseType { get; init; } = "-";

    public string ComponentId { get; init; } = "-";

    public int SourceModifierIndex { get; init; }

    public int SourceLineIndex { get; init; }

    public string ParsedKind { get; init; } = "-";

    public string UniqueOrigin { get; init; } = "-";

    public string ImplicitOrigin { get; init; } = "-";

    public string OriginalText { get; init; } = "-";

    public string NormalizedSourceText { get; init; } = "-";

    public int SourceLineCount { get; init; }

    public int ObservedValueCount { get; init; }

    public bool IsMultiline { get; init; }

    public string CoreStatus { get; init; } = "-";

    public bool IsEquivalentSourceSet { get; init; }

    public string SourceDiagnosticCode { get; init; } = "-";

    public string AggregateDiagnosticCode { get; init; } = "-";

    public string CompositionProjectionReason { get; init; } = "-";

    public IReadOnlyList<string> ModifierIds { get; init; } = [];

    public IReadOnlyList<string> StatIds { get; init; } = [];

    public IReadOnlyList<string> BlockIds { get; init; } = [];

    public int ModifierIdCount { get; init; }

    public int StatIdCount { get; init; }

    public int BlockIdCount { get; init; }

    public int ComponentCount { get; init; }

    public int OmittedComponentCount { get; init; }

    public string RoleSummary { get; init; } = "-";

    public string ResolvedSourceKind { get; init; } = "-";

    public bool HasExactProvenance { get; init; }

    public bool HasResolvedUniqueSourceSemantics { get; init; }

    public bool IsBaseImplicit { get; init; }

    public string ProviderStatus { get; init; } = "-";

    public string ProviderDiagnosticCode { get; init; } = "-";

    public IReadOnlyList<string> ProviderStatIds { get; init; } = [];

    public int ProviderCandidateCount { get; init; }

    public bool IsSearchable { get; init; }

    public string NotSearchableReason { get; init; } = "-";

    public string AvailabilityStatus { get; init; } = "-";

    public string EarliestFailureLayer { get; init; } = "ok";

    public string Outcome { get; init; } = UniqueCorpusGateOutcomes.Other;

    public string Stage { get; init; } = UniqueCorpusGateStages.None;

    public string RootCauseKey { get; init; } = "none";
}

public sealed class UniqueCorpusGateObservationalRow
{
    public required string RowKey { get; init; }

    public required string Fingerprint { get; init; }

    public required UniqueCorpusGateStructuralFingerprint FingerprintParts { get; init; }

    public required UniqueCorpusGateObservationalFacts Facts { get; init; }

    public string ModifierIdSetHash { get; init; } = "-";

    public string StatIdSetHash { get; init; } = "-";

    public string ProviderIdSetHash { get; init; } = "-";

    public string BlockIdSetHash { get; init; } = "-";
}

public static class UniqueCorpusGateRowKey
{
    public static string Build(
        string itemIdentityKey,
        string parsedKind,
        string normalizedSourceText,
        int sourceModifierIndex,
        int sourceLineIndex,
        string componentId,
        int occurrenceOrdinal)
    {
        return string.Join(
            '|',
            Norm(itemIdentityKey),
            Norm(parsedKind),
            Norm(normalizedSourceText),
            sourceModifierIndex.ToString(),
            sourceLineIndex.ToString(),
            Norm(componentId),
            "#" + occurrenceOrdinal.ToString());
    }

    public static string NormalizeSourceText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "-";
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.Length == 0 ? "-" : normalized;
    }

    private static string Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}

internal static class UniqueCorpusGateObservationalExtractor
{
    public static IReadOnlyList<UniqueCorpusGateObservationalRow> ExtractRows(
        IReadOnlyList<UniqueCorpusGateAnalyzedCapture> captures)
    {
        ArgumentNullException.ThrowIfNull(captures);
        var rows = new List<UniqueCorpusGateObservationalRow>();
        var occurrenceCounters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var capture in captures)
        {
            var roleSummary = BuildRoleSummary(capture.Document);
            foreach (var modifier in capture.Document.Modifiers ?? [])
            {
                var facts = BuildFacts(capture, modifier, roleSummary);
                var occurrenceKey = string.Join(
                    '|',
                    facts.ItemIdentityKey,
                    facts.ParsedKind,
                    facts.NormalizedSourceText,
                    facts.SourceModifierIndex,
                    facts.SourceLineIndex,
                    facts.ComponentId);
                occurrenceCounters.TryGetValue(occurrenceKey, out var ordinal);
                occurrenceCounters[occurrenceKey] = ordinal + 1;

                var fingerprintParts = UniqueCorpusGateStructuralFingerprint.Build(facts);
                var analyzed = UniqueCorpusGateAnalyzer.ClassifyComponent(capture, modifier);
                facts = CloneWithClassification(facts, analyzed);

                rows.Add(new UniqueCorpusGateObservationalRow
                {
                    RowKey = UniqueCorpusGateRowKey.Build(
                        facts.ItemIdentityKey,
                        facts.ParsedKind,
                        facts.NormalizedSourceText,
                        facts.SourceModifierIndex,
                        facts.SourceLineIndex,
                        facts.ComponentId,
                        ordinal),
                    Fingerprint = fingerprintParts.Rendered,
                    FingerprintParts = fingerprintParts,
                    Facts = facts,
                    ModifierIdSetHash = UniqueCorpusGateStatusFamilies.StableSetHash(facts.ModifierIds),
                    StatIdSetHash = UniqueCorpusGateStatusFamilies.StableSetHash(facts.StatIds),
                    ProviderIdSetHash = UniqueCorpusGateStatusFamilies.StableSetHash(facts.ProviderStatIds),
                    BlockIdSetHash = UniqueCorpusGateStatusFamilies.StableSetHash(facts.BlockIds),
                });
            }
        }

        return rows;
    }

    private static UniqueCorpusGateObservationalFacts CloneWithClassification(
        UniqueCorpusGateObservationalFacts facts,
        UniqueCorpusGateAnalyzedComponent analyzed) =>
        new()
        {
            ItemIdentityKey = facts.ItemIdentityKey,
            ItemName = facts.ItemName,
            ItemClass = facts.ItemClass,
            BaseType = facts.BaseType,
            ComponentId = facts.ComponentId,
            SourceModifierIndex = facts.SourceModifierIndex,
            SourceLineIndex = facts.SourceLineIndex,
            ParsedKind = facts.ParsedKind,
            UniqueOrigin = facts.UniqueOrigin,
            ImplicitOrigin = facts.ImplicitOrigin,
            OriginalText = facts.OriginalText,
            NormalizedSourceText = facts.NormalizedSourceText,
            SourceLineCount = facts.SourceLineCount,
            ObservedValueCount = facts.ObservedValueCount,
            IsMultiline = facts.IsMultiline,
            CoreStatus = facts.CoreStatus,
            IsEquivalentSourceSet = facts.IsEquivalentSourceSet,
            SourceDiagnosticCode = facts.SourceDiagnosticCode,
            AggregateDiagnosticCode = facts.AggregateDiagnosticCode,
            CompositionProjectionReason = facts.CompositionProjectionReason,
            ModifierIds = facts.ModifierIds,
            StatIds = facts.StatIds,
            BlockIds = facts.BlockIds,
            ModifierIdCount = facts.ModifierIdCount,
            StatIdCount = facts.StatIdCount,
            BlockIdCount = facts.BlockIdCount,
            ComponentCount = facts.ComponentCount,
            OmittedComponentCount = facts.OmittedComponentCount,
            RoleSummary = facts.RoleSummary,
            ResolvedSourceKind = facts.ResolvedSourceKind,
            HasExactProvenance = facts.HasExactProvenance,
            HasResolvedUniqueSourceSemantics = facts.HasResolvedUniqueSourceSemantics,
            IsBaseImplicit = facts.IsBaseImplicit,
            ProviderStatus = facts.ProviderStatus,
            ProviderDiagnosticCode = facts.ProviderDiagnosticCode,
            ProviderStatIds = facts.ProviderStatIds,
            ProviderCandidateCount = facts.ProviderCandidateCount,
            IsSearchable = facts.IsSearchable,
            NotSearchableReason = facts.NotSearchableReason,
            AvailabilityStatus = facts.AvailabilityStatus,
            EarliestFailureLayer = InferEarliestLayer(facts, analyzed),
            Outcome = analyzed.Outcome,
            Stage = analyzed.Stage,
            RootCauseKey = analyzed.RootCauseKey,
        };

    private static string InferEarliestLayer(
        UniqueCorpusGateObservationalFacts facts,
        UniqueCorpusGateAnalyzedComponent analyzed)
    {
        if (analyzed.Outcome == UniqueCorpusGateOutcomes.Supported && facts.IsSearchable)
        {
            return "ok";
        }

        if (!string.IsNullOrWhiteSpace(facts.SourceDiagnosticCode) &&
            facts.SourceDiagnosticCode != "-" ||
            UniqueCorpusGateStatusFamilies.IsFailedCoreFamily(facts.CoreStatus, facts.IsEquivalentSourceSet))
        {
            return analyzed.Stage == UniqueCorpusGateStages.VersionBlockMatching
                ? "Parser→GameData/Core"
                : "GameData→Core";
        }

        if (UniqueCorpusGateStatusFamilies.IsFailedProviderFamily(facts.ProviderStatus))
        {
            return "Core→provider";
        }

        if (!facts.IsSearchable)
        {
            return "provider→final query";
        }

        return analyzed.Stage;
    }

    private static string BuildRoleSummary(UniqueCorpusGateCaptureDocument document)
    {
        var roles = document.UniqueMechanicalResolution?.CompatibleVersionRoles ?? [];
        if (roles.Count == 0)
        {
            return "-";
        }

        return string.Join(
            "+",
            roles.Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(role => role, StringComparer.Ordinal));
    }

    private static UniqueCorpusGateObservationalFacts BuildFacts(
        UniqueCorpusGateAnalyzedCapture capture,
        UniqueCorpusGateCaptureModifier modifier,
        string roleSummary)
    {
        var originalText = FirstNonEmpty(
            modifier.Raw?.OriginalText,
            modifier.Signatures?.OriginalText,
            modifier.ComponentId,
            "-")!;
        var valueLines = modifier.Raw?.ValueLines ?? [];
        if (valueLines.Count == 0 && !string.IsNullOrWhiteSpace(originalText) && originalText != "-")
        {
            valueLines = originalText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        }

        var sourceLineCount = modifier.Multiline?.SourceCount > 0
            ? modifier.Multiline.SourceCount
            : Math.Max(1, valueLines.Count);
        var isMultiline = modifier.Multiline?.OriginalTextContainsNewLine == true || sourceLineCount > 1;
        var modifierIds = BuildModifierIds(modifier);
        var statIds = modifier.SourceResolution?.ResolvedStatIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToArray() ?? [];
        var blockIds = modifier.SourceResolution?.UniqueCatalogBlockIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToArray() ?? [];
        var omitted = modifier.SourceResolution?.UniqueOmittedCompositionComponentIds?.Count ?? 0;
        var providerIds = BuildProviderIds(modifier);
        var providerCandidates = Math.Max(
            providerIds.Count,
            modifier.ProviderResolution?.ProviderCandidateStatIds?.Count ?? 0);
        foreach (var pass in modifier.ProviderPasses ?? [])
        {
            providerCandidates = Math.Max(providerCandidates, pass.Match?.Candidates?.Count ?? 0);
        }

        var implicitOrigin = FirstNonEmpty(modifier.Raw?.ImplicitOrigin, "-")!;
        var isBaseImplicit = modifier.ResolvedSemantics?.IsBaseImplicit == true ||
            string.Equals(implicitOrigin, "Base", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(implicitOrigin, "Native", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(implicitOrigin, "Item", StringComparison.OrdinalIgnoreCase);

        return new UniqueCorpusGateObservationalFacts
        {
            ItemIdentityKey = capture.ItemIdentityKey,
            ItemName = capture.ItemName,
            ItemClass = FirstNonEmpty(capture.Document.Item?.ItemClass, "-")!,
            BaseType = FirstNonEmpty(
                capture.Document.UniqueIdentity?.CanonicalType,
                capture.Document.Item?.ResolvedBaseName,
                capture.Document.Item?.ParsedBaseType,
                "-")!,
            ComponentId = FirstNonEmpty(modifier.ComponentId, "-")!,
            SourceModifierIndex = modifier.SourceModifierIndex,
            SourceLineIndex = modifier.SourceLineIndex,
            ParsedKind = FirstNonEmpty(
                modifier.ResolvedSemantics?.ParsedKind,
                modifier.Raw?.ParsedKind,
                "Unknown")!,
            UniqueOrigin = FirstNonEmpty(
                modifier.ResolvedSemantics?.UniqueOrigin,
                modifier.Raw?.UniqueOrigin,
                "-")!,
            ImplicitOrigin = implicitOrigin,
            OriginalText = originalText,
            NormalizedSourceText = UniqueCorpusGateRowKey.NormalizeSourceText(originalText),
            SourceLineCount = sourceLineCount,
            ObservedValueCount = UniqueCorpusGateStatusFamilies.CountObservedValues(valueLines),
            IsMultiline = isMultiline,
            CoreStatus = FirstNonEmpty(modifier.SourceResolution?.Status, "-")!,
            IsEquivalentSourceSet = modifier.SourceResolution?.IsEquivalentSourceSet == true ||
                modifier.Multiline?.IsEquivalentSourceSet == true,
            SourceDiagnosticCode = FirstNonEmpty(modifier.SourceResolution?.UniqueResolutionDiagnosticCode, "-")!,
            AggregateDiagnosticCode = FirstNonEmpty(
                modifier.SourceResolution?.UniqueAggregationDiagnosticCode,
                "-")!,
            CompositionProjectionReason = FirstNonEmpty(
                modifier.SourceResolution?.UniqueCompositionProjectionReason,
                "-")!,
            ModifierIds = modifierIds,
            StatIds = statIds,
            BlockIds = blockIds,
            ModifierIdCount = modifierIds.Count,
            StatIdCount = statIds.Length,
            BlockIdCount = blockIds.Length,
            ComponentCount = Math.Max(1, omitted + 1),
            OmittedComponentCount = omitted,
            RoleSummary = roleSummary,
            ResolvedSourceKind = FirstNonEmpty(
                modifier.ResolvedSemantics?.ResolvedSourceKind,
                modifier.Raw?.ParsedKind,
                "-")!,
            HasExactProvenance = modifier.ResolvedSemantics?.HasExactUniqueSourceProvenance == true,
            HasResolvedUniqueSourceSemantics =
                modifier.ResolvedSemantics?.HasResolvedUniqueSourceSemantics == true,
            IsBaseImplicit = isBaseImplicit,
            ProviderStatus = FirstNonEmpty(modifier.ProviderResolution?.ProviderResolutionStatus, "-")!,
            ProviderDiagnosticCode = FirstNonEmpty(modifier.ProviderResolution?.ProviderDiagnosticCode, "-")!,
            ProviderStatIds = providerIds,
            ProviderCandidateCount = providerCandidates,
            IsSearchable = modifier.Consumer?.IsSearchable == true,
            NotSearchableReason = FirstNonEmpty(modifier.Consumer?.NotSearchableReason, "-")!,
            AvailabilityStatus = FirstNonEmpty(modifier.Consumer?.AvailabilityStatus, "-")!,
        };
    }

    private static IReadOnlyList<string> BuildModifierIds(UniqueCorpusGateCaptureModifier modifier)
    {
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(modifier.SourceResolution?.ResolvedModifierId))
        {
            ids.Add(modifier.SourceResolution.ResolvedModifierId.Trim());
        }

        foreach (var id in modifier.SourceResolution?.UniqueConflictCandidateModifierIds ?? [])
        {
            if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id, StringComparer.Ordinal))
            {
                ids.Add(id.Trim());
            }
        }

        return ids;
    }

    private static IReadOnlyList<string> BuildProviderIds(UniqueCorpusGateCaptureModifier modifier)
    {
        var ids = new List<string>();
        Add(ids, modifier.ProviderResolution?.ProviderStatId);
        foreach (var id in modifier.ProviderResolution?.ProviderCandidateStatIds ?? [])
        {
            Add(ids, id);
        }

        foreach (var id in modifier.ProviderResolution?.ProviderStatAlternativeIds ?? [])
        {
            Add(ids, id);
        }

        return ids;
    }

    private static void Add(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value, StringComparer.Ordinal))
        {
            values.Add(value.Trim());
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
