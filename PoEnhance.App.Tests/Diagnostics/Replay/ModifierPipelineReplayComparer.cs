namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal static class ModifierPipelineReplayComparer
{
    public static ModifierPipelineReplayComparison Compare(
        ModifierPipelineNormalizedItem captured,
        ModifierPipelineNormalizedItem replayed)
    {
        ArgumentNullException.ThrowIfNull(captured);
        ArgumentNullException.ThrowIfNull(replayed);

        var deltas = new List<ModifierPipelineReplayFieldDelta>();
        CompareScalar(deltas, "item.itemClass", captured.ItemClass, replayed.ItemClass, "core");
        CompareScalar(deltas, "item.rarity", captured.Rarity, replayed.Rarity, "core");
        CompareScalar(deltas, "item.displayName", captured.DisplayName, replayed.DisplayName, "core");
        CompareScalar(deltas, "item.baseType", captured.BaseType, replayed.BaseType, "core");
        CompareOptional(deltas, "item.baseResolutionStatus", captured.BaseResolutionStatus, replayed.BaseResolutionStatus, "core");
        CompareOptional(deltas, "item.resolvedBaseName", captured.ResolvedBaseName, replayed.ResolvedBaseName, "core");
        CompareOptional(deltas, "item.uniqueCanonicalName", captured.UniqueCanonicalName, replayed.UniqueCanonicalName, "core");
        CompareOptional(deltas, "item.uniqueCanonicalType", captured.UniqueCanonicalType, replayed.UniqueCanonicalType, "core");
        CompareOptional(deltas, "item.uniqueResolutionStatus", captured.UniqueResolutionStatus, replayed.UniqueResolutionStatus, "core");
        CompareOptional(
            deltas,
            "item.uniqueResolutionDiagnosticCode",
            captured.UniqueResolutionDiagnosticCode,
            replayed.UniqueResolutionDiagnosticCode,
            "core");
        CompareSet(deltas, "item.compatibleVersionRoles", captured.CompatibleVersionRoles, replayed.CompatibleVersionRoles, "core");

        var capturedByKey = captured.Modifiers.ToDictionary(modifier => modifier.RowKey, StringComparer.Ordinal);
        var replayedByKey = replayed.Modifiers.ToDictionary(modifier => modifier.RowKey, StringComparer.Ordinal);
        foreach (var key in capturedByKey.Keys.Union(replayedByKey.Keys, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            capturedByKey.TryGetValue(key, out var left);
            replayedByKey.TryGetValue(key, out var right);
            if (left is null || right is null)
            {
                deltas.Add(new ModifierPipelineReplayFieldDelta
                {
                    Field = $"modifier[{key}].presence",
                    CapturedValue = left is null ? null : "present",
                    ReplayValue = right is null ? null : "present",
                    Classification = ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence,
                    Layer = "core",
                });
                continue;
            }

            CompareModifier(deltas, left, right);
        }

        // Draft filters exist on replay only in A.5.3 captures → CAPTURE_FIELD_UNAVAILABLE, not semantic divergence.
        if (captured.DraftFilters.Count == 0 && replayed.DraftFilters.Count > 0)
        {
            deltas.Add(new ModifierPipelineReplayFieldDelta
            {
                Field = "draftFilters",
                CapturedValue = null,
                ReplayValue = $"{replayed.DraftFilters.Count} filters",
                Classification = ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable,
                Layer = "draft",
            });
        }
        else if (captured.DraftFilters.Count > 0)
        {
            CompareSet(
                deltas,
                "draftFilters.providerStatIds",
                captured.DraftFilters.Select(filter => filter.ProviderStatId ?? "-"),
                replayed.DraftFilters.Select(filter => filter.ProviderStatId ?? "-"),
                "provider");
        }

        var classification = Classify(deltas);
        return new ModifierPipelineReplayComparison(classification, deltas);
    }

    private static void CompareModifier(
        List<ModifierPipelineReplayFieldDelta> deltas,
        ModifierPipelineNormalizedModifier captured,
        ModifierPipelineNormalizedModifier replayed)
    {
        var prefix = $"modifier[{captured.RowKey}]";
        CompareOptional(deltas, $"{prefix}.parsedKind", captured.ParsedKind, replayed.ParsedKind, "core");
        CompareOptional(deltas, $"{prefix}.originalText", captured.OriginalText, replayed.OriginalText, "core");
        CompareSet(deltas, $"{prefix}.valueLines", captured.ValueLines, replayed.ValueLines, "core");
        CompareSet(
            deltas,
            $"{prefix}.observedNumericValues",
            captured.ObservedNumericValues,
            replayed.ObservedNumericValues,
            "core");
        CompareOptional(deltas, $"{prefix}.coreStatus", captured.CoreStatus, replayed.CoreStatus, "core");
        CompareOptionalCaptureAware(
            deltas,
            $"{prefix}.sourceDiagnosticCode",
            captured.SourceDiagnosticCode,
            replayed.SourceDiagnosticCode,
            "core");
        CompareOptionalCaptureAware(
            deltas,
            $"{prefix}.aggregateDiagnosticCode",
            captured.AggregateDiagnosticCode,
            replayed.AggregateDiagnosticCode,
            "core");
        CompareOptional(
            deltas,
            $"{prefix}.uniqueBlockDiagnosticCode",
            captured.UniqueBlockDiagnosticCode,
            replayed.UniqueBlockDiagnosticCode,
            "core");
        CompareSet(deltas, $"{prefix}.modifierIds", captured.ModifierIds, replayed.ModifierIds, "core");
        CompareSet(deltas, $"{prefix}.statIds", captured.StatIds, replayed.StatIds, "core");
        CompareSet(deltas, $"{prefix}.blockIds", captured.BlockIds, replayed.BlockIds, "core");
        CompareOptional(deltas, $"{prefix}.resolvedSourceKind", captured.ResolvedSourceKind, replayed.ResolvedSourceKind, "core");
        CompareBool(deltas, $"{prefix}.hasExactProvenance", captured.HasExactProvenance, replayed.HasExactProvenance, "core");
        CompareBool(deltas, $"{prefix}.isBaseImplicit", captured.IsBaseImplicit, replayed.IsBaseImplicit, "core");
        CompareOptional(deltas, $"{prefix}.providerStatus", captured.ProviderStatus, replayed.ProviderStatus, "provider");
        CompareSet(deltas, $"{prefix}.providerStatIds", captured.ProviderStatIds, replayed.ProviderStatIds, "provider");
        CompareOptionalCaptureAware(
            deltas,
            $"{prefix}.providerDiagnosticCode",
            captured.ProviderDiagnosticCode,
            replayed.ProviderDiagnosticCode,
            "provider");
        CompareBool(deltas, $"{prefix}.isSearchable", captured.IsSearchable, replayed.IsSearchable, "provider");
        CompareOptionalCaptureAware(
            deltas,
            $"{prefix}.notSearchableReasonFamily",
            captured.NotSearchableReasonFamily,
            replayed.NotSearchableReasonFamily,
            "provider");
    }

    private static string Classify(IReadOnlyList<ModifierPipelineReplayFieldDelta> deltas)
    {
        if (deltas.Any(delta =>
                delta.Classification == ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence &&
                delta.Layer == "core"))
        {
            return ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence;
        }

        if (deltas.Any(delta =>
                delta.Classification == ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence &&
                delta.Layer == "provider"))
        {
            // Provider-only semantic difference without catalog identity → unverified, not hard Core regression.
            return ModifierPipelineReplayDivergenceClass.ProviderContextUnverified;
        }

        if (deltas.Any(delta =>
                delta.Classification == ModifierPipelineReplayDivergenceClass.ProviderContextUnverified))
        {
            return ModifierPipelineReplayDivergenceClass.ProviderContextUnverified;
        }

        if (deltas.Any(delta =>
                delta.Classification == ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable))
        {
            return ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable;
        }

        return ModifierPipelineReplayDivergenceClass.ExactMatch;
    }

    private static void CompareScalar(
        List<ModifierPipelineReplayFieldDelta> deltas,
        string field,
        string? left,
        string? right,
        string layer)
    {
        if (string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal))
        {
            return;
        }

        deltas.Add(new ModifierPipelineReplayFieldDelta
        {
            Field = field,
            CapturedValue = left,
            ReplayValue = right,
            Classification = layer == "provider"
                ? ModifierPipelineReplayDivergenceClass.ProviderContextUnverified
                : ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence,
            Layer = layer,
        });
    }

    private static void CompareOptional(
        List<ModifierPipelineReplayFieldDelta> deltas,
        string field,
        string? left,
        string? right,
        string layer)
    {
        if (left is null && right is null)
        {
            return;
        }

        if (left is null && right is not null)
        {
            deltas.Add(new ModifierPipelineReplayFieldDelta
            {
                Field = field,
                CapturedValue = null,
                ReplayValue = right,
                Classification = ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable,
                Layer = layer,
            });
            return;
        }

        CompareScalar(deltas, field, left, right, layer);
    }

    private static void CompareOptionalCaptureAware(
        List<ModifierPipelineReplayFieldDelta> deltas,
        string field,
        string? left,
        string? right,
        string layer)
    {
        if (string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
        {
            deltas.Add(new ModifierPipelineReplayFieldDelta
            {
                Field = field,
                CapturedValue = left,
                ReplayValue = right,
                Classification = ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable,
                Layer = layer,
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return;
        }

        var classification = layer == "provider"
            ? ModifierPipelineReplayDivergenceClass.ProviderContextUnverified
            : ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence;
        if (!string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal))
        {
            deltas.Add(new ModifierPipelineReplayFieldDelta
            {
                Field = field,
                CapturedValue = left,
                ReplayValue = right,
                Classification = classification,
                Layer = layer,
            });
        }
    }

    private static void CompareBool(
        List<ModifierPipelineReplayFieldDelta> deltas,
        string field,
        bool? left,
        bool? right,
        string layer)
    {
        if (left is null && right is not null)
        {
            deltas.Add(new ModifierPipelineReplayFieldDelta
            {
                Field = field,
                CapturedValue = null,
                ReplayValue = right.Value.ToString(),
                Classification = ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable,
                Layer = layer,
            });
            return;
        }

        if (left is null && right is null)
        {
            return;
        }

        if (left != right)
        {
            deltas.Add(new ModifierPipelineReplayFieldDelta
            {
                Field = field,
                CapturedValue = left?.ToString(),
                ReplayValue = right?.ToString(),
                Classification = layer == "provider"
                    ? ModifierPipelineReplayDivergenceClass.ProviderContextUnverified
                    : ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence,
                Layer = layer,
            });
        }
    }

    private static void CompareSet(
        List<ModifierPipelineReplayFieldDelta> deltas,
        string field,
        IEnumerable<string> left,
        IEnumerable<string> right,
        string layer)
    {
        var leftSet = left.Select(Normalize).Where(value => value.Length > 0).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var rightSet = right.Select(Normalize).Where(value => value.Length > 0).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (leftSet.SequenceEqual(rightSet, StringComparer.Ordinal))
        {
            return;
        }

        // Capture omitted optional detail that replay still has → not a semantic regression.
        if (leftSet.Length == 0 && rightSet.Length > 0)
        {
            deltas.Add(new ModifierPipelineReplayFieldDelta
            {
                Field = field,
                CapturedValue = string.Empty,
                ReplayValue = string.Join("|", rightSet),
                Classification = ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable,
                Layer = layer,
            });
            return;
        }

        // Capture stores multiline aggregate valueLines on each component; replay stores per-line.
        if (field.Contains(".valueLines", StringComparison.Ordinal) &&
            rightSet.Length > 0 &&
            rightSet.All(line => leftSet.Contains(line, StringComparer.Ordinal)))
        {
            deltas.Add(new ModifierPipelineReplayFieldDelta
            {
                Field = field,
                CapturedValue = string.Join("|", leftSet),
                ReplayValue = string.Join("|", rightSet),
                Classification = ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable,
                Layer = layer,
            });
            return;
        }

        deltas.Add(new ModifierPipelineReplayFieldDelta
        {
            Field = field,
            CapturedValue = string.Join("|", leftSet),
            ReplayValue = string.Join("|", rightSet),
            Classification = layer == "provider"
                ? ModifierPipelineReplayDivergenceClass.ProviderContextUnverified
                : ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence,
            Layer = layer,
        });
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

internal sealed record ModifierPipelineReplayComparison(
    string Classification,
    IReadOnlyList<ModifierPipelineReplayFieldDelta> Deltas);
