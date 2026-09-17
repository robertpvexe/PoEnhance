using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PoEnhance.DataTool.UniqueCorpusGate;

/// <summary>
/// Canonical structural fingerprint for corpus analysis. Excludes item name, path, and timestamps.
/// </summary>
public sealed record UniqueCorpusGateStructuralFingerprint
{
    public string Kind { get; init; } = "-";

    public int SourceLineCount { get; init; }

    public int ObservedValueCount { get; init; }

    public string CoreStatus { get; init; } = "-";

    public string BlockKind { get; init; } = "-";

    public string CompositionKind { get; init; } = "-";

    public int ModifierIdCount { get; init; }

    public int StatIdCount { get; init; }

    public int BlockCount { get; init; }

    public int ComponentCount { get; init; }

    public string BaseImplicitState { get; init; } = "-";

    public string UniqueOrigin { get; init; } = "-";

    public string RoleSummary { get; init; } = "-";

    public string SourceDiagnosticFamily { get; init; } = "-";

    public string AggregateDiagnosticFamily { get; init; } = "-";

    public string ProviderStatusFamily { get; init; } = "-";

    public int ProviderCandidateCount { get; init; }

    public bool IsSearchable { get; init; }

    public string NotSearchableReasonFamily { get; init; } = "-";

    public bool IsMultiline { get; init; }

    public bool HasExactProvenance { get; init; }

    public string Rendered { get; init; } = string.Empty;

    public static UniqueCorpusGateStructuralFingerprint Build(UniqueCorpusGateObservationalFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var fingerprint = new UniqueCorpusGateStructuralFingerprint
        {
            Kind = Norm(facts.ParsedKind),
            SourceLineCount = facts.SourceLineCount,
            ObservedValueCount = facts.ObservedValueCount,
            CoreStatus = UniqueCorpusGateStatusFamilies.Core(facts.CoreStatus, facts.IsEquivalentSourceSet),
            BlockKind = Norm(facts.ResolvedSourceKind),
            CompositionKind = Norm(facts.CompositionProjectionReason),
            ModifierIdCount = facts.ModifierIdCount,
            StatIdCount = facts.StatIdCount,
            BlockCount = facts.BlockIdCount,
            ComponentCount = facts.ComponentCount,
            BaseImplicitState = Norm(facts.ImplicitOrigin),
            UniqueOrigin = Norm(facts.UniqueOrigin),
            RoleSummary = Norm(facts.RoleSummary),
            SourceDiagnosticFamily = UniqueCorpusGateStatusFamilies.DiagnosticFamily(facts.SourceDiagnosticCode),
            AggregateDiagnosticFamily = UniqueCorpusGateStatusFamilies.DiagnosticFamily(facts.AggregateDiagnosticCode),
            ProviderStatusFamily = UniqueCorpusGateStatusFamilies.Provider(facts.ProviderStatus),
            ProviderCandidateCount = facts.ProviderCandidateCount,
            IsSearchable = facts.IsSearchable,
            NotSearchableReasonFamily = UniqueCorpusGateStatusFamilies.NotSearchableFamily(facts.NotSearchableReason),
            IsMultiline = facts.IsMultiline,
            HasExactProvenance = facts.HasExactProvenance,
        };
        return fingerprint with { Rendered = Render(fingerprint) };
    }

    private static string Render(UniqueCorpusGateStructuralFingerprint fingerprint) =>
        string.Join('|',
            $"kind={fingerprint.Kind}",
            $"srcLines={fingerprint.SourceLineCount}",
            $"obsVals={fingerprint.ObservedValueCount}",
            $"core={fingerprint.CoreStatus}",
            $"blockKind={fingerprint.BlockKind}",
            $"comp={fingerprint.CompositionKind}",
            $"modIds={fingerprint.ModifierIdCount}",
            $"statIds={fingerprint.StatIdCount}",
            $"blocks={fingerprint.BlockCount}",
            $"comps={fingerprint.ComponentCount}",
            $"baseImpl={fingerprint.BaseImplicitState}",
            $"uniqOrigin={fingerprint.UniqueOrigin}",
            $"roles={fingerprint.RoleSummary}",
            $"srcDiag={fingerprint.SourceDiagnosticFamily}",
            $"aggDiag={fingerprint.AggregateDiagnosticFamily}",
            $"prov={fingerprint.ProviderStatusFamily}",
            $"cand={fingerprint.ProviderCandidateCount}",
            $"search={(fingerprint.IsSearchable ? "T" : "F")}",
            $"notReason={fingerprint.NotSearchableReasonFamily}",
            $"exactProv={(fingerprint.HasExactProvenance ? "T" : "F")}",
            $"multiline={(fingerprint.IsMultiline ? "T" : "F")}");

    private static string Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}

public static class UniqueCorpusGateStatusFamilies
{
    public static string Core(string? status, bool isEquivalentSourceSet)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "-" : status.Trim();
        if (string.Equals(normalized, "Exact", StringComparison.OrdinalIgnoreCase))
        {
            return isEquivalentSourceSet ? "EquivalentSourceSet" : "Exact";
        }

        return string.IsNullOrWhiteSpace(normalized) ? "Missing" : normalized;
    }

    public static string Provider(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Missing";
        }

        return status.Trim() switch
        {
            "Exact" => "Exact",
            "ExactEquivalentSet" => "ExactEquivalentSet",
            "ExactConjunctiveSet" => "ExactConjunctiveSet",
            "BaseGuaranteed" => "BaseGuaranteed",
            "Ambiguous" => "Ambiguous",
            "NotFound" => "NotFound",
            "Unsupported" => "Unsupported",
            "Skipped" => "Skipped",
            _ => status.Trim(),
        };
    }

    public static bool IsExactProviderFamily(string? status)
    {
        var family = Provider(status);
        return family is "Exact" or "ExactEquivalentSet" or "ExactConjunctiveSet" or "BaseGuaranteed";
    }

    public static bool IsFailedProviderFamily(string? status)
    {
        var family = Provider(status);
        return family is "Unsupported" or "NotFound" or "Ambiguous" or "Missing";
    }

    public static bool IsExactCoreFamily(string? status, bool isEquivalent)
    {
        var family = Core(status, isEquivalent);
        return family is "Exact" or "EquivalentSourceSet";
    }

    public static bool IsFailedCoreFamily(string? status, bool isEquivalent)
    {
        var family = Core(status, isEquivalent);
        return family is "Unknown" or "Ambiguous" or "Unsupported" or "Missing";
    }

    public static string DiagnosticFamily(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "-";
        }

        var trimmed = code.Trim();
        if (trimmed.Contains("VERSION_MISMATCH", StringComparison.OrdinalIgnoreCase))
        {
            return "VERSION_MISMATCH";
        }

        if (trimmed.Contains("MISSING_GAMEDATA_PROVENANCE", StringComparison.OrdinalIgnoreCase))
        {
            return "MISSING_PROVENANCE";
        }

        if (trimmed.Contains("AMBIGUOUS", StringComparison.OrdinalIgnoreCase))
        {
            return "AMBIGUOUS";
        }

        if (trimmed.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
        {
            return "CONFLICT";
        }

        if (trimmed.Contains("NO_CANDIDATE", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            return "NOT_FOUND";
        }

        if (trimmed.Contains("FOULBORN", StringComparison.OrdinalIgnoreCase))
        {
            return "FOULBORN";
        }

        return trimmed;
    }

    public static string NotSearchableFamily(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "-";
        }

        if (reason.Contains("version", StringComparison.OrdinalIgnoreCase))
        {
            return "version-mismatch";
        }

        if (reason.Contains("provenance", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("GameData", StringComparison.OrdinalIgnoreCase))
        {
            return "missing-provenance";
        }

        if (reason.Contains("ambiguous", StringComparison.OrdinalIgnoreCase))
        {
            return "ambiguous";
        }

        if (reason.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            return "unsupported";
        }

        if (reason.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "not-found";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reason)))[..8];
        return "other:" + hash;
    }

    public static string StableSetHash(IEnumerable<string>? values)
    {
        var ordered = (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0)
        {
            return "-";
        }

        var payload = string.Join('\u001f', ordered);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..16];
    }

    public static int CountObservedValues(IEnumerable<string>? lines)
    {
        var count = 0;
        foreach (var line in lines ?? [])
        {
            count += Regex.Matches(line ?? string.Empty, @"\d+(?:\.\d+)?").Count;
        }

        return count;
    }
}
