namespace PoEnhance.DataTool.UniqueCorpusGate;

public static class UniqueCorpusGateDifferential
{
    public static UniqueCorpusGateObservationalDiff Compare(
        UniqueCorpusGateObservationalBaseline baseline,
        IReadOnlyList<UniqueCorpusGateObservationalRow> currentRows)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(currentRows);

        var baselineMap = baseline.Rows
            .GroupBy(row => row.RowKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var duplicateCurrentKeys = currentRows
            .GroupBy(row => row.RowKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Take(5)
            .ToArray();
        if (duplicateCurrentKeys.Length > 0)
        {
            throw new InvalidDataException(
                "Observational row-key collision(s): " + string.Join(" || ", duplicateCurrentKeys));
        }

        var currentMap = currentRows.ToDictionary(row => row.RowKey, StringComparer.Ordinal);
        var changes = new List<UniqueCorpusGateRowChange>();

        foreach (var (rowKey, current) in currentMap)
        {
            if (!baselineMap.TryGetValue(rowKey, out var previous))
            {
                changes.Add(new UniqueCorpusGateRowChange
                {
                    RowKey = rowKey,
                    Kind = UniqueCorpusGateChangeKind.New,
                    ReasonCode = "NEW_ROW",
                    Message = "New observational row appeared in corpus.",
                    ItemName = current.Facts.ItemName,
                    FingerprintAfter = current.Fingerprint,
                });
                continue;
            }

            changes.AddRange(ClassifyPair(previous, current));
        }

        foreach (var (rowKey, previous) in baselineMap)
        {
            if (currentMap.ContainsKey(rowKey))
            {
                continue;
            }

            changes.Add(new UniqueCorpusGateRowChange
            {
                RowKey = rowKey,
                Kind = UniqueCorpusGateChangeKind.ReviewRequired,
                ReasonCode = "ROW_MISSING",
                Message = "Previously observed row disappeared from corpus.",
                ItemName = previous.ItemName,
                FingerprintBefore = previous.Fingerprint,
            });
        }

        var ordered = changes
            .OrderBy(change => ChangeSortOrder(change.Kind))
            .ThenBy(change => change.ReasonCode, StringComparer.Ordinal)
            .ThenBy(change => change.RowKey, StringComparer.Ordinal)
            .ToArray();

        var touchedKeys = ordered
            .Select(change => change.RowKey)
            .ToHashSet(StringComparer.Ordinal);
        var stableShared = baselineMap.Keys.Count(key => currentMap.ContainsKey(key) && !touchedKeys.Contains(key));

        return new UniqueCorpusGateObservationalDiff
        {
            BaselineRowCount = baseline.Rows.Count,
            CurrentRowCount = currentRows.Count,
            HardRegressionCount = ordered.Count(change => change.Kind == UniqueCorpusGateChangeKind.HardRegression),
            ReviewRequiredCount = ordered.Count(change => change.Kind == UniqueCorpusGateChangeKind.ReviewRequired),
            ImprovementCount = ordered.Count(change => change.Kind == UniqueCorpusGateChangeKind.Improvement),
            NewRowCount = ordered.Count(change => change.Kind == UniqueCorpusGateChangeKind.New),
            NeutralCount = stableShared,
            Changes = ordered,
        };
    }

    private static IEnumerable<UniqueCorpusGateRowChange> ClassifyPair(
        UniqueCorpusGateObservationalBaselineRow previous,
        UniqueCorpusGateObservationalRow current)
    {
        var changes = new List<UniqueCorpusGateRowChange>();

        void Add(UniqueCorpusGateChangeKind kind, string code, string message) =>
            changes.Add(new UniqueCorpusGateRowChange
            {
                RowKey = current.RowKey,
                Kind = kind,
                ReasonCode = code,
                Message = message,
                ItemName = current.Facts.ItemName,
                FingerprintBefore = previous.Fingerprint,
                FingerprintAfter = current.Fingerprint,
            });

        var currentCore = UniqueCorpusGateStatusFamilies.Core(
            current.Facts.CoreStatus,
            current.Facts.IsEquivalentSourceSet);
        var currentProvider = UniqueCorpusGateStatusFamilies.Provider(current.Facts.ProviderStatus);

        if (previous.IsSearchable && !current.Facts.IsSearchable)
        {
            Add(UniqueCorpusGateChangeKind.HardRegression, "SEARCHABLE_TRUE_TO_FALSE",
                "Searchable changed from true to false.");
        }
        else if (!previous.IsSearchable && current.Facts.IsSearchable)
        {
            Add(UniqueCorpusGateChangeKind.Improvement, "SEARCHABLE_FALSE_TO_TRUE",
                "Searchable changed from false to true.");
        }

        if (UniqueCorpusGateStatusFamilies.IsExactProviderFamily(previous.ProviderStatus) &&
            UniqueCorpusGateStatusFamilies.IsFailedProviderFamily(currentProvider))
        {
            Add(UniqueCorpusGateChangeKind.HardRegression, "PROVIDER_EXACT_TO_FAILED",
                $"Provider status degraded {previous.ProviderStatus} → {currentProvider}.");
        }
        else if (UniqueCorpusGateStatusFamilies.IsFailedProviderFamily(previous.ProviderStatus) &&
                 UniqueCorpusGateStatusFamilies.IsExactProviderFamily(currentProvider))
        {
            Add(UniqueCorpusGateChangeKind.Improvement, "PROVIDER_FAILED_TO_EXACT",
                $"Provider status improved {previous.ProviderStatus} → {currentProvider}.");
        }

        if (UniqueCorpusGateStatusFamilies.IsExactCoreFamily(previous.CoreStatus, previous.IsEquivalentSourceSet) &&
            UniqueCorpusGateStatusFamilies.IsFailedCoreFamily(current.Facts.CoreStatus, current.Facts.IsEquivalentSourceSet))
        {
            Add(UniqueCorpusGateChangeKind.HardRegression, "CORE_EXACT_TO_FAILED",
                $"Core/mechanical status degraded {previous.CoreStatus} → {currentCore}.");
        }
        else if (UniqueCorpusGateStatusFamilies.IsFailedCoreFamily(previous.CoreStatus, previous.IsEquivalentSourceSet) &&
                 UniqueCorpusGateStatusFamilies.IsExactCoreFamily(current.Facts.CoreStatus, current.Facts.IsEquivalentSourceSet))
        {
            Add(UniqueCorpusGateChangeKind.Improvement, "CORE_FAILED_TO_EXACT",
                $"Core/mechanical status improved {previous.CoreStatus} → {currentCore}.");
        }

        if (previous.StatIdCount > 0 && current.Facts.StatIdCount == 0)
        {
            Add(UniqueCorpusGateChangeKind.HardRegression, "TRUSTED_STATIDS_LOST",
                "Trusted StatIds became empty.");
        }

        if (previous.ModifierIdCount > 0 && current.Facts.ModifierIdCount == 0)
        {
            Add(UniqueCorpusGateChangeKind.HardRegression, "TRUSTED_MODIFIERIDS_LOST",
                "Trusted ModifierIds became empty.");
        }

        if (previous.IsBaseImplicit && !current.Facts.IsBaseImplicit)
        {
            Add(UniqueCorpusGateChangeKind.HardRegression, "BASE_IMPLICIT_LOST",
                "IsBaseImplicit changed from true to false.");
        }

        if (previous.RoleSummary.Contains("Current", StringComparison.OrdinalIgnoreCase) &&
            !current.Facts.RoleSummary.Contains("Current", StringComparison.OrdinalIgnoreCase) &&
            current.Facts.RoleSummary.Contains("Historical", StringComparison.OrdinalIgnoreCase))
        {
            Add(UniqueCorpusGateChangeKind.HardRegression, "CURRENT_ROLE_LOST",
                "Current role/provenance disappeared or became Historical-only.");
        }

        if (previous.ComponentCount > current.Facts.ComponentCount &&
            string.IsNullOrWhiteSpace(current.Facts.SourceDiagnosticCode.Replace("-", string.Empty)))
        {
            Add(UniqueCorpusGateChangeKind.HardRegression, "COMPONENT_LOSS_WITHOUT_DIAGNOSTIC",
                "Component structure lost proven components without an explicit diagnostic reason.");
        }

        if (previous.HasExactProvenance && !current.Facts.HasExactProvenance &&
            current.Facts.IsSearchable)
        {
            // provenance loss while remaining searchable is covered by invariants; still review
            Add(UniqueCorpusGateChangeKind.ReviewRequired, "EXACT_PROVENANCE_LOST",
                "Exact provenance flag was lost.");
        }
        else if (!previous.HasExactProvenance && current.Facts.HasExactProvenance)
        {
            Add(UniqueCorpusGateChangeKind.Improvement, "PROVENANCE_GAINED",
                "Exact provenance was gained.");
        }

        if (UniqueCorpusGateStatusFamilies.IsExactProviderFamily(previous.ProviderStatus) &&
            UniqueCorpusGateStatusFamilies.IsExactProviderFamily(currentProvider) &&
            !string.Equals(previous.ProviderIdSetHash, current.ProviderIdSetHash, StringComparison.Ordinal) &&
            previous.ProviderIdSetHash != "-" &&
            current.ProviderIdSetHash != "-")
        {
            Add(UniqueCorpusGateChangeKind.ReviewRequired, "PROVIDER_ID_SET_CHANGED",
                "Provider id set changed while still Exact-family.");
        }

        if (UniqueCorpusGateStatusFamilies.IsExactCoreFamily(previous.CoreStatus, previous.IsEquivalentSourceSet) &&
            UniqueCorpusGateStatusFamilies.IsExactCoreFamily(current.Facts.CoreStatus, current.Facts.IsEquivalentSourceSet))
        {
            if (!string.Equals(previous.ModifierIdSetHash, current.ModifierIdSetHash, StringComparison.Ordinal) &&
                previous.ModifierIdSetHash != "-" &&
                current.ModifierIdSetHash != "-")
            {
                Add(UniqueCorpusGateChangeKind.ReviewRequired, "MODIFIERID_SET_CHANGED",
                    "ModifierId set changed while still Exact-family.");
            }

            if (!string.Equals(previous.StatIdSetHash, current.StatIdSetHash, StringComparison.Ordinal) &&
                previous.StatIdSetHash != "-" &&
                current.StatIdSetHash != "-")
            {
                Add(UniqueCorpusGateChangeKind.ReviewRequired, "STATID_SET_CHANGED",
                    "StatId set changed while still Exact-family.");
            }
        }

        if (previous.ComponentCount != current.Facts.ComponentCount)
        {
            Add(UniqueCorpusGateChangeKind.ReviewRequired, "COMPONENT_COUNT_CHANGED",
                $"Component count changed {previous.ComponentCount} → {current.Facts.ComponentCount}.");
        }

        if (!string.Equals(previous.CompositionKind, current.FingerprintParts.CompositionKind, StringComparison.Ordinal) &&
            previous.CompositionKind != "-" &&
            current.FingerprintParts.CompositionKind != "-")
        {
            Add(UniqueCorpusGateChangeKind.ReviewRequired, "COMPOSITION_KIND_CHANGED",
                $"Composition kind changed {previous.CompositionKind} → {current.FingerprintParts.CompositionKind}.");
        }

        if (!string.Equals(previous.Fingerprint, current.Fingerprint, StringComparison.Ordinal))
        {
            Add(UniqueCorpusGateChangeKind.ReviewRequired, "FINGERPRINT_CHANGED",
                "Structural fingerprint changed for a previously-known row.");
        }

        return DeduplicateByPriority(changes);
    }

    private static IReadOnlyList<UniqueCorpusGateRowChange> DeduplicateByPriority(
        IReadOnlyList<UniqueCorpusGateRowChange> changes) =>
        changes
            .GroupBy(change => change.ReasonCode, StringComparer.Ordinal)
            .Select(group => group.OrderBy(change => ChangeSortOrder(change.Kind)).First())
            .ToArray();

    private static int ChangeSortOrder(UniqueCorpusGateChangeKind kind) => kind switch
    {
        UniqueCorpusGateChangeKind.HardRegression => 0,
        UniqueCorpusGateChangeKind.ReviewRequired => 1,
        UniqueCorpusGateChangeKind.Improvement => 2,
        UniqueCorpusGateChangeKind.New => 3,
        _ => 4,
    };
}
