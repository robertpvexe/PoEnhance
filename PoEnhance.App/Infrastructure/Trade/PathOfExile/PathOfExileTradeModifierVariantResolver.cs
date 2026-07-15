using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static partial class PathOfExileTradeModifierVariantResolver
{
    private const string UnsupportedBoundsMessage =
        "This Trade filter has incompatible numeric semantics; retained Min/Max text is not sent.";

    public static ResolvedSearchComponent Apply(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sourceExactCandidate);

        var candidates = DiscoverCandidates(catalog, sourceExactCandidate)
            .DistinctBy(candidate => candidate.StatId, StringComparer.Ordinal)
            .ToArray();
        if (!candidates.Any(candidate => string.Equals(
                candidate.StatId,
                sourceExactCandidate.StatId,
                StringComparison.Ordinal)))
        {
            candidates = [sourceExactCandidate, .. candidates];
        }

        var options = candidates
            .Select(candidate => CreateOption(sourceExactCandidate, candidate))
            .ToArray();
        var requestedIdentity = component.SelectedFilterVariantIdentity?.Trim();
        var selectedIndex = string.IsNullOrWhiteSpace(requestedIdentity)
            ? -1
            : Array.FindIndex(options, option => string.Equals(
                option.Identity,
                requestedIdentity,
                StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(requestedIdentity) && selectedIndex < 0)
        {
            return component with
            {
                FilterVariants = options,
                SelectedFilterVariantIdentity = requestedIdentity,
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.NotFound,
                ProviderStatId = null,
                ProviderStatText = null,
                ProviderDiagnosticCode =
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
            };
        }

        if (selectedIndex < 0)
        {
            selectedIndex = Array.FindIndex(candidates, candidate => string.Equals(
                candidate.StatId,
                sourceExactCandidate.StatId,
                StringComparison.Ordinal));
        }

        selectedIndex = Math.Max(0, selectedIndex);
        var selectedCandidate = candidates[selectedIndex];
        var selectedOption = options[selectedIndex];
        var resolved = component with
        {
            FilterVariants = options,
            SelectedFilterVariantIdentity = selectedOption.Identity,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = selectedCandidate.StatId,
            ProviderStatText = selectedCandidate.Text,
            ProviderDiagnosticCode = null,
        };

        return ApplyBounds(resolved, selectedOption, selectedCandidate);
    }

    internal static string IdentityFor(string providerStatId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerStatId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(providerStatId.Trim()));
        return $"variant-{Convert.ToHexString(hash.AsSpan(0, 10)).ToLowerInvariant()}";
    }

    private static IEnumerable<PathOfExileTradeStatMatchCandidate> DiscoverCandidates(
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate source)
    {
        var sourceKind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(source);
        var sourceEffect = EffectIdentity(
            source.Text,
            removePseudoBreadth: string.Equals(sourceKind, "pseudo", StringComparison.Ordinal));
        foreach (var entry in catalog.Entries)
        {
            var candidate = PathOfExileTradeStatCandidateClassifier.ToCandidate(entry);
            var kind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
            if (string.Equals(kind, PathOfExileTradeStatCandidateClassifier.UnknownProviderKind, StringComparison.Ordinal) ||
                !string.Equals(
                    sourceEffect,
                    EffectIdentity(candidate.Text, removePseudoBreadth: string.Equals(kind, "pseudo", StringComparison.Ordinal)),
                    StringComparison.Ordinal) ||
                !HasCompatibleLocality(source, candidate, kind))
            {
                continue;
            }

            yield return candidate;
        }
    }

    private static bool HasCompatibleLocality(
        PathOfExileTradeStatMatchCandidate source,
        PathOfExileTradeStatMatchCandidate candidate,
        string candidateKind)
    {
        return string.Equals(candidateKind, "pseudo", StringComparison.Ordinal) ||
            candidate.ProviderLocality == source.ProviderLocality;
    }

    private static SearchFilterVariant CreateOption(
        PathOfExileTradeStatMatchCandidate source,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        var kind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        var supportsBounds = HasCompatibleNumericSemantics(source, candidate);
        var label = ConciseLabel(candidate, kind);
        return new SearchFilterVariant
        {
            Identity = IdentityFor(candidate.StatId),
            Label = label,
            Description = candidate.Text,
            SupportsValueBounds = supportsBounds,
            ValueBoundsUnsupportedReason = supportsBounds ? null : UnsupportedBoundsMessage,
        };
    }

    private static ResolvedSearchComponent ApplyBounds(
        ResolvedSearchComponent component,
        SearchFilterVariant option,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        if (!option.SupportsValueBounds || component.ValueBoundShape is ModifierBoundShape.PresenceOnly or ModifierBoundShape.Unsupported)
        {
            return component with
            {
                SupportsValueBounds = false,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason = option.ValueBoundsUnsupportedReason ?? component.ValueBoundsUnsupportedReason,
            };
        }

        var restored = component with
        {
            SupportsValueBounds = true,
            ValueBoundsUnsupportedReason = null,
        };
        return PathOfExileTradeModifierBoundProjector.Project(restored, candidate);
    }

    private static bool HasCompatibleNumericSemantics(
        PathOfExileTradeStatMatchCandidate source,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        var sourceTokens = NumericTokens(source.Text);
        var candidateTokens = NumericTokens(candidate.Text);
        return sourceTokens.Count > 0 && sourceTokens.SequenceEqual(candidateTokens, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> NumericTokens(string? text)
    {
        var normalized = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(text);
        return NumericTokenRegex().Matches(normalized)
            .Select(match => $"{match.Groups["sign"].Value}{(match.Groups["percent"].Success ? "%" : "flat")}")
            .ToArray();
    }

    private static string EffectIdentity(string? text, bool removePseudoBreadth)
    {
        var normalized = PathOfExileTradeStatTemplateNormalizer
            .NormalizeLookupTemplate(text)
            .ToLowerInvariant();
        normalized = NumericAndUnitRegex().Replace(normalized, " ");
        if (removePseudoBreadth)
        {
            normalized = PseudoBreadthRegex().Replace(normalized, " ");
        }

        return string.Join(' ', normalized.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string ConciseLabel(PathOfExileTradeStatMatchCandidate candidate, string kind)
    {
        var metadataLabel = string.IsNullOrWhiteSpace(candidate.GroupLabel)
            ? candidate.Type
            : candidate.GroupLabel;
        var value = string.Equals(kind, PathOfExileTradeStatCandidateClassifier.UnknownProviderKind, StringComparison.Ordinal)
            ? metadataLabel
            : kind;
        value = string.IsNullOrWhiteSpace(value) ? "Filter" : value.Trim();
        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }

    [GeneratedRegex(@"(?<sign>[+-]?)#(?<percent>%?)", RegexOptions.CultureInvariant)]
    private static partial Regex NumericTokenRegex();

    [GeneratedRegex(@"[+-]?#%?", RegexOptions.CultureInvariant)]
    private static partial Regex NumericAndUnitRegex();

    [GeneratedRegex(@"\b(?:total|combined)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PseudoBreadthRegex();
}
