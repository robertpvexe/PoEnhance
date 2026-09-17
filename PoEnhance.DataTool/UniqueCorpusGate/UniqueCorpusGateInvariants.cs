namespace PoEnhance.DataTool.UniqueCorpusGate;

public static class UniqueCorpusGateInvariants
{
    public static IReadOnlyList<UniqueCorpusGateInvariantResult> Evaluate(
        IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return
        [
            EvaluateA(rows),
            EvaluateB(rows),
            EvaluateC(rows),
            EvaluateD(rows),
            EvaluateE(rows),
            EvaluateF(rows),
            EvaluateG(rows),
        ];
    }

    private static UniqueCorpusGateInvariantResult EvaluateA(IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        var evaluable = new List<UniqueCorpusGateObservationalRow>();
        var violations = new List<UniqueCorpusGateObservationalRow>();
        foreach (var row in rows)
        {
            var hadExact = UniqueCorpusGateStatusFamilies.IsExactCoreFamily(
                               row.Facts.CoreStatus,
                               row.Facts.IsEquivalentSourceSet) &&
                           (row.Facts.StatIdCount > 0 || row.Facts.HasExactProvenance);
            if (!hadExact && !row.Facts.HasExactProvenance)
            {
                continue;
            }

            evaluable.Add(row);
            var lostLater = (row.Facts.HasExactProvenance || hadExact) &&
                            (UniqueCorpusGateStatusFamilies.IsFailedProviderFamily(row.Facts.ProviderStatus) ||
                             !row.Facts.IsSearchable);
            var silent = lostLater &&
                         row.Facts.ProviderDiagnosticCode is "-" or "" &&
                         row.Facts.SourceDiagnosticCode is "-" or "";
            if (silent)
            {
                violations.Add(row);
            }
        }

        return Build("A", "exact_provenance_must_not_disappear_silently", evaluable, violations);
    }

    private static UniqueCorpusGateInvariantResult EvaluateB(IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        var evaluable = new List<UniqueCorpusGateObservationalRow>();
        var violations = new List<UniqueCorpusGateObservationalRow>();
        foreach (var row in rows)
        {
            if (!UniqueCorpusGateStatusFamilies.IsExactProviderFamily(row.Facts.ProviderStatus))
            {
                continue;
            }

            evaluable.Add(row);
            var backed = row.Facts.HasExactProvenance || row.Facts.StatIdCount > 0 || row.Facts.BlockIdCount > 0;
            if (!backed)
            {
                violations.Add(row);
            }
        }

        return Build("B", "provider_exact_must_be_provenance_backed", evaluable, violations);
    }

    private static UniqueCorpusGateInvariantResult EvaluateC(IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        var evaluable = new List<UniqueCorpusGateObservationalRow>();
        var violations = new List<UniqueCorpusGateObservationalRow>();
        foreach (var row in rows)
        {
            var unresolved =
                UniqueCorpusGateStatusFamilies.IsFailedCoreFamily(
                    row.Facts.CoreStatus,
                    row.Facts.IsEquivalentSourceSet) ||
                UniqueCorpusGateStatusFamilies.IsFailedProviderFamily(row.Facts.ProviderStatus);
            if (!unresolved)
            {
                continue;
            }

            evaluable.Add(row);
            var emittedBroad = row.Facts.IsSearchable &&
                               !UniqueCorpusGateStatusFamilies.IsExactProviderFamily(row.Facts.ProviderStatus);
            if (emittedBroad)
            {
                violations.Add(row);
            }
        }

        return Build("C", "unresolved_evidence_remains_fail_closed", evaluable, violations);
    }

    private static UniqueCorpusGateInvariantResult EvaluateD(IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        var evaluable = new List<UniqueCorpusGateObservationalRow>();
        var violations = new List<UniqueCorpusGateObservationalRow>();
        foreach (var group in rows.GroupBy(row => row.Fingerprint, StringComparer.Ordinal))
        {
            var itemCount = group.Select(row => row.Facts.ItemIdentityKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (itemCount < 2)
            {
                continue;
            }

            evaluable.AddRange(group);
            var searchableStates = group.Select(row => row.Facts.IsSearchable).Distinct().Count();
            var providerStates = group.Select(row => UniqueCorpusGateStatusFamilies.Provider(row.Facts.ProviderStatus))
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (searchableStates > 1 || providerStates > 1)
            {
                violations.AddRange(group);
            }
        }

        return Build("D", "identical_fingerprint_consistent_across_items", evaluable, violations);
    }

    private static UniqueCorpusGateInvariantResult EvaluateE(IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        var evaluable = new List<UniqueCorpusGateObservationalRow>();
        var violations = new List<UniqueCorpusGateObservationalRow>();
        foreach (var row in rows)
        {
            if (!row.Facts.IsMultiline && row.Facts.SourceLineCount <= 1 && row.Facts.OmittedComponentCount == 0)
            {
                continue;
            }

            evaluable.Add(row);
            var collapsedSilent = row.Facts.SourceLineCount > 1 &&
                                  row.Facts.StatIdCount == 0 &&
                                  row.Facts.BlockIdCount == 0 &&
                                  row.Facts.SourceDiagnosticCode is "-" or "";
            if (collapsedSilent)
            {
                violations.Add(row);
            }
        }

        return Build("E", "composition_cardinality_requires_proof", evaluable, violations);
    }

    private static UniqueCorpusGateInvariantResult EvaluateF(IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        var evaluable = new List<UniqueCorpusGateObservationalRow>();
        var violations = new List<UniqueCorpusGateObservationalRow>();
        foreach (var row in rows)
        {
            if (!row.Facts.RoleSummary.Contains("Historical", StringComparison.OrdinalIgnoreCase) &&
                row.Facts.AggregateDiagnosticCode is "-" or "")
            {
                continue;
            }

            evaluable.Add(row);
            var histOnly = string.Equals(row.Facts.RoleSummary, "Historical", StringComparison.OrdinalIgnoreCase);
            if (histOnly && row.Facts.IsSearchable && row.Facts.AggregateDiagnosticCode is "-" or "")
            {
                violations.Add(row);
            }
        }

        return Build("F", "current_historical_provenance_survives", evaluable, violations);
    }

    private static UniqueCorpusGateInvariantResult EvaluateG(IReadOnlyList<UniqueCorpusGateObservationalRow> rows)
    {
        var evaluable = new List<UniqueCorpusGateObservationalRow>();
        var violations = new List<UniqueCorpusGateObservationalRow>();
        foreach (var row in rows)
        {
            if (row.Facts.ImplicitOrigin is "-" or "Unspecified")
            {
                continue;
            }

            evaluable.Add(row);
            if (row.Facts.IsBaseImplicit)
            {
                var ok = row.Facts.ResolvedSourceKind.Contains("Implicit", StringComparison.OrdinalIgnoreCase) ||
                         row.Facts.ParsedKind.Contains("Implicit", StringComparison.OrdinalIgnoreCase);
                if (!ok)
                {
                    violations.Add(row);
                }
            }
        }

        return Build("G", "native_base_ownership_not_invented", evaluable, violations);
    }

    private static UniqueCorpusGateInvariantResult Build(
        string id,
        string name,
        IReadOnlyList<UniqueCorpusGateObservationalRow> evaluable,
        IReadOnlyList<UniqueCorpusGateObservationalRow> violations) =>
        new()
        {
            Id = id,
            Name = name,
            Evaluable = evaluable.Count,
            Pass = Math.Max(0, evaluable.Count - violations.Count),
            Violations = violations.Count,
            TopViolatingFingerprints = violations
                .GroupBy(row => row.Fingerprint, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .Take(5)
                .Select(group => new UniqueCorpusGateInvariantViolationSample
                {
                    Fingerprint = group.Key,
                    Count = group.Count(),
                    ExampleItems = group.Select(row => row.Facts.ItemName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(5)
                        .ToArray(),
                })
                .ToArray(),
        };
}
