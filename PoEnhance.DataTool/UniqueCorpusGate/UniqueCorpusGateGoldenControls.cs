namespace PoEnhance.DataTool.UniqueCorpusGate;

public static class UniqueCorpusGateGoldenControls
{
    public static readonly IReadOnlyList<string> TimelessFamilyNames =
    [
        "Lethal Pride",
        "Brutal Restraint",
        "Elegant Hubris",
        "Glorious Vanity",
        "Militant Faith",
    ];

    public static readonly IReadOnlyList<string> IntentionalVersionMismatchNames =
    [
        "Replica Bated Breath",
        "Augyre",
    ];

    public static IReadOnlyList<UniqueCorpusGateGoldenControlResult> Evaluate(
        IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return
        [
            EvaluateCategory(
                "native-base-implicit-parity",
                "native_base_implicit_parity",
                "Native base implicit parity",
                rows.Where(IsNativeBaseImplicit).ToArray(),
                row => UniqueCorpusGateStatusFamilies.IsExactProviderFamily(row.Facts.ProviderStatus) &&
                       row.Facts.IsSearchable),
            EvaluateCategory(
                "corrupted-implicit-value-mapping",
                "corrupted_implicit_value_mapping",
                "Corrupted implicit value mapping",
                rows.Where(IsCorruptedImplicit).ToArray(),
                row => UniqueCorpusGateStatusFamilies.IsExactCoreFamily(
                           row.Facts.CoreStatus,
                           row.Facts.IsEquivalentSourceSet) &&
                       UniqueCorpusGateStatusFamilies.IsExactProviderFamily(row.Facts.ProviderStatus) &&
                       row.Facts.IsSearchable),
            EvaluateCategory(
                "multi-stat-exact-implicit-expansion",
                "multi_stat_exact_implicit_expansion",
                "Multi-stat Exact Implicit expansion",
                rows.Where(row =>
                    IsImplicitFamily(row) &&
                    row.Facts.StatIdCount >= 2 &&
                    UniqueCorpusGateStatusFamilies.IsExactCoreFamily(
                        row.Facts.CoreStatus,
                        row.Facts.IsEquivalentSourceSet)).ToArray(),
                row => UniqueCorpusGateStatusFamilies.IsExactProviderFamily(row.Facts.ProviderStatus) &&
                       row.Facts.IsSearchable &&
                       row.Facts.StatIdCount >= 2),
            EvaluateCategory(
                "q20-equivalent-source-collapse",
                "q20_equivalent_source_collapse",
                "Q20 equivalent-source collapse",
                rows.Where(row => row.Facts.IsEquivalentSourceSet).ToArray(),
                row => UniqueCorpusGateStatusFamilies.Core(
                           row.Facts.CoreStatus,
                           row.Facts.IsEquivalentSourceSet) == "EquivalentSourceSet" &&
                       (row.Facts.IsSearchable ||
                        !string.IsNullOrWhiteSpace(row.Facts.SourceDiagnosticCode) &&
                        row.Facts.SourceDiagnosticCode != "-")),
            EvaluateCategory(
                "partial-composition",
                "partial_composition",
                "Partial composition",
                rows.Where(row =>
                    row.Facts.OmittedComponentCount > 0 ||
                    row.Facts.CompositionProjectionReason.Contains("partial", StringComparison.OrdinalIgnoreCase))
                    .ToArray(),
                row => row.Facts.OmittedComponentCount > 0 &&
                       (!string.IsNullOrWhiteSpace(row.Facts.CompositionProjectionReason) &&
                        row.Facts.CompositionProjectionReason != "-" ||
                        !string.IsNullOrWhiteSpace(row.Facts.SourceDiagnosticCode) &&
                        row.Facts.SourceDiagnosticCode != "-")),
            EvaluateCategory(
                "exact-base-refinement-by-modifierid",
                "exact_base_refinement_by_modifierid",
                "Exact base refinement by ModifierId",
                rows.Where(row =>
                    UniqueCorpusGateStatusFamilies.IsExactCoreFamily(
                        row.Facts.CoreStatus,
                        row.Facts.IsEquivalentSourceSet) &&
                    row.Facts.ModifierIdCount >= 1 &&
                    IsImplicitFamily(row)).ToArray(),
                row => row.Facts.ModifierIdCount >= 1 &&
                       UniqueCorpusGateStatusFamilies.IsExactProviderFamily(row.Facts.ProviderStatus)),
            EvaluateCategory(
                "passage-textual-option-range",
                "passage_textual_option_range",
                "Passage / TextualOptionRange",
                rows.Where(IsPassageLike).ToArray(),
                row => UniqueCorpusGateStatusFamilies.IsExactCoreFamily(
                           row.Facts.CoreStatus,
                           row.Facts.IsEquivalentSourceSet) &&
                       row.Facts.HasExactProvenance &&
                       row.Facts.IsSearchable),
            EvaluateCategory(
                "provider-conjunctive-and",
                "provider_conjunctive_and",
                "Provider conjunctive AND",
                rows.Where(row =>
                    string.Equals(
                        row.Facts.ProviderStatus,
                        "ExactConjunctiveSet",
                        StringComparison.OrdinalIgnoreCase)).ToArray(),
                row => string.Equals(
                           row.Facts.ProviderStatus,
                           "ExactConjunctiveSet",
                           StringComparison.OrdinalIgnoreCase) &&
                       row.Facts.IsSearchable),
            EvaluateCategory(
                "provider-equivalent-source-or",
                "provider_equivalent_source_or",
                "Provider equivalent-source OR",
                rows.Where(row =>
                    string.Equals(
                        row.Facts.ProviderStatus,
                        "ExactEquivalentSet",
                        StringComparison.OrdinalIgnoreCase) ||
                    row.Facts.IsEquivalentSourceSet &&
                    UniqueCorpusGateStatusFamilies.IsExactProviderFamily(row.Facts.ProviderStatus)).ToArray(),
                row => UniqueCorpusGateStatusFamilies.IsExactProviderFamily(row.Facts.ProviderStatus) &&
                       row.Facts.IsSearchable),
            EvaluateTimeless(rows),
            EvaluateIntentionalVersionMismatch(rows),
        ];
    }

    private static UniqueCorpusGateGoldenControlResult EvaluateTimeless(
        IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        var matched = rows.Where(IsTimelessSeedRow).ToArray();
        var passing = matched.Where(row =>
            UniqueCorpusGateStatusFamilies.IsExactCoreFamily(
                row.Facts.CoreStatus,
                row.Facts.IsEquivalentSourceSet) &&
            row.Facts.HasExactProvenance &&
            UniqueCorpusGateStatusFamilies.IsExactProviderFamily(row.Facts.ProviderStatus) &&
            row.Facts.IsSearchable).ToArray();
        var failing = matched.Except(passing).ToArray();
        var missingFamily = TimelessFamilyNames
            .Where(name => matched.All(row =>
                !string.Equals(row.Facts.ItemName, name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var detail = matched.Length == 0
            ? "No Timeless multiline seed rows were present in the analyzed corpus; control skipped as pass."
            : failing.Length == 0 && missingFamily.Length == 0
                ? "All available Timeless multiline seed rows are Exact/searchable."
                : $"Desired Exact/searchable Timeless seed behavior failed for {failing.Length} row(s)" +
                  (missingFamily.Length == 0
                      ? "."
                      : $"; family members absent from this corpus: {string.Join(", ", missingFamily)}.");

        return new UniqueCorpusGateGoldenControlResult
        {
            Id = "timeless-multiline-seed-exact-searchable",
            Category = "timeless_multiline_seed_exact_searchable",
            Title = "Timeless multiline seed Exact/searchable behavior",
            Passed = failing.Length == 0,
            MatchedRowCount = matched.Length,
            PassingRowCount = passing.Length,
            FailingRowCount = failing.Length,
            Detail = detail,
            ExampleItems = failing.Select(row => row.Facts.ItemName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray(),
        };
    }

    private static UniqueCorpusGateGoldenControlResult EvaluateIntentionalVersionMismatch(
        IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        var matched = rows.Where(row =>
            IntentionalVersionMismatchNames.Contains(row.Facts.ItemName, StringComparer.OrdinalIgnoreCase) &&
            (string.Equals(
                 row.Facts.SourceDiagnosticCode,
                 "UNIQUE_BLOCK_VERSION_MISMATCH",
                 StringComparison.OrdinalIgnoreCase) ||
             UniqueCorpusGateStatusFamilies.DiagnosticFamily(row.Facts.SourceDiagnosticCode) ==
             "VERSION_MISMATCH")).ToArray();

        // If named fixtures are absent, fall back to zero-match skip (not auto-pass on unrelated mismatches).
        if (matched.Length == 0)
        {
            return new UniqueCorpusGateGoldenControlResult
            {
                Id = "intentional-version-mismatch-fail-closed",
                Category = "intentional_version_mismatch_fail_closed",
                Title = "Intentional version-mismatch fail-closed behavior",
                Passed = true,
                MatchedRowCount = 0,
                PassingRowCount = 0,
                FailingRowCount = 0,
                Detail =
                    "No intentional version-mismatch fixtures (Replica Bated Breath / Augyre mismatch rows) were present; control skipped as pass.",
            };
        }

        var passing = matched.Where(row =>
            !row.Facts.IsSearchable &&
            UniqueCorpusGateStatusFamilies.IsFailedProviderFamily(row.Facts.ProviderStatus)).ToArray();
        var failing = matched.Except(passing).ToArray();
        return new UniqueCorpusGateGoldenControlResult
        {
            Id = "intentional-version-mismatch-fail-closed",
            Category = "intentional_version_mismatch_fail_closed",
            Title = "Intentional version-mismatch fail-closed behavior",
            Passed = failing.Length == 0,
            MatchedRowCount = matched.Length,
            PassingRowCount = passing.Length,
            FailingRowCount = failing.Length,
            Detail = failing.Length == 0
                ? "Intentional version-mismatch rows remained fail-closed."
                : "Intentional version-mismatch rows unexpectedly became searchable or provider Exact.",
            ExampleItems = failing.Select(row => row.Facts.ItemName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray(),
        };
    }

    private static UniqueCorpusGateGoldenControlResult EvaluateCategory(
        string id,
        string category,
        string title,
        IReadOnlyList<UniqueCorpusGateObservationalRow> matched,
        Func<UniqueCorpusGateObservationalRow, bool> expectation)
    {
        if (matched.Count == 0)
        {
            return new UniqueCorpusGateGoldenControlResult
            {
                Id = id,
                Category = category,
                Title = title,
                Passed = true,
                MatchedRowCount = 0,
                PassingRowCount = 0,
                FailingRowCount = 0,
                Detail = "No matching rows in corpus; control skipped as pass (not auto-promoted from broken states).",
            };
        }

        var passing = matched.Where(expectation).ToArray();
        var failing = matched.Except(passing).ToArray();
        return new UniqueCorpusGateGoldenControlResult
        {
            Id = id,
            Category = category,
            Title = title,
            Passed = failing.Length == 0,
            MatchedRowCount = matched.Count,
            PassingRowCount = passing.Length,
            FailingRowCount = failing.Length,
            Detail = failing.Length == 0
                ? $"All {matched.Count} matched row(s) satisfied the structural expectation."
                : $"{failing.Length}/{matched.Count} matched row(s) failed the structural expectation.",
            ExampleItems = failing.Select(row => row.Facts.ItemName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray(),
        };
    }

    private static bool IsNativeBaseImplicit(UniqueCorpusGateObservationalRow row) =>
        row.Facts.IsBaseImplicit ||
        string.Equals(row.Facts.ImplicitOrigin, "Base", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(row.Facts.ImplicitOrigin, "Native", StringComparison.OrdinalIgnoreCase);

    private static bool IsCorruptedImplicit(UniqueCorpusGateObservationalRow row) =>
        string.Equals(row.Facts.ImplicitOrigin, "Corrupted", StringComparison.OrdinalIgnoreCase) ||
        row.Facts.ResolvedSourceKind.Contains("Corrupted", StringComparison.OrdinalIgnoreCase);

    private static bool IsImplicitFamily(UniqueCorpusGateObservationalRow row) =>
        row.Facts.ParsedKind.Contains("Implicit", StringComparison.OrdinalIgnoreCase) ||
        row.Facts.ResolvedSourceKind.Contains("Implicit", StringComparison.OrdinalIgnoreCase);

    private static bool IsPassageLike(UniqueCorpusGateObservationalRow row) =>
        row.Facts.NormalizedSourceText.Contains("Passage", StringComparison.OrdinalIgnoreCase) ||
        row.Facts.ItemName.Contains("Passage", StringComparison.OrdinalIgnoreCase) ||
        row.Facts.OriginalText.Contains("Passage", StringComparison.OrdinalIgnoreCase);

    private static bool IsTimelessSeedRow(UniqueCorpusGateObservationalRow row)
    {
        if (!TimelessFamilyNames.Contains(row.Facts.ItemName, StringComparer.OrdinalIgnoreCase) &&
            !string.Equals(row.Facts.BaseType, "Timeless Jewel", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return row.Facts.IsMultiline &&
               row.Facts.ParsedKind.Contains("Unique", StringComparison.OrdinalIgnoreCase) &&
               (row.Facts.NormalizedSourceText.Contains("Historic", StringComparison.OrdinalIgnoreCase) ||
                row.Facts.NormalizedSourceText.Contains("Conquered", StringComparison.OrdinalIgnoreCase) ||
                row.Facts.NormalizedSourceText.Contains("Passives in radius", StringComparison.OrdinalIgnoreCase));
    }
}

public static class UniqueCorpusGateFailureClustering
{
    public static IReadOnlyList<UniqueCorpusGateStructuralFailureClass> Build(
        IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows
            .Where(row => !row.Facts.IsSearchable)
            .GroupBy(row => row.Fingerprint, StringComparer.Ordinal)
            .Select(group =>
            {
                var sample = group.First();
                return new UniqueCorpusGateStructuralFailureClass
                {
                    Fingerprint = group.Key,
                    DiagnosticFamily = UniqueCorpusGateStatusFamilies.DiagnosticFamily(
                        sample.Facts.SourceDiagnosticCode) + "/" +
                        UniqueCorpusGateStatusFamilies.DiagnosticFamily(sample.Facts.ProviderDiagnosticCode),
                    ProviderFamily = UniqueCorpusGateStatusFamilies.Provider(sample.Facts.ProviderStatus),
                    EarliestLayer = sample.Facts.EarliestFailureLayer,
                    AffectedItemCount = group.Select(row => row.Facts.ItemIdentityKey)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    AffectedModifierCount = group.Count(),
                    ExampleItems = group.Select(row => row.Facts.ItemName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(5)
                        .ToArray(),
                };
            })
            .OrderByDescending(entry => entry.AffectedItemCount)
            .ThenByDescending(entry => entry.AffectedModifierCount)
            .ThenBy(entry => entry.Fingerprint, StringComparer.Ordinal)
            .ToArray();
    }
}
