using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeStatMatcher : IPathOfExileTradeStatMatcher
{
    public PathOfExileTradeStatMatchResult Match(
        ParsedModifier? modifier,
        PathOfExileTradeStatCatalog? catalog)
    {
        if (catalog is null)
        {
            return InvalidInput(
                PathOfExileTradeStatMatchDiagnosticCodes.NullCatalog,
                "A Trade stats catalog is required.");
        }

        var modifierText = modifier?.Text;
        if (string.IsNullOrWhiteSpace(modifierText))
        {
            return InvalidInput(
                PathOfExileTradeStatMatchDiagnosticCodes.BlankModifierText,
                "Modifier text is required.");
        }

        var normalization = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(modifierText);
        if (normalization.Diagnostic is not null)
        {
            return new PathOfExileTradeStatMatchResult
            {
                Status = PathOfExileTradeStatMatchStatus.InvalidInput,
                NormalizedItemTemplate = normalization.NormalizedTemplate,
                Diagnostics = [normalization.Diagnostic],
            };
        }

        var candidates = catalog
            .FindByNormalizedTemplate(normalization.NormalizedTemplate)
            .Select(ToCandidate)
            .ToArray();
        if (candidates.Length == 0)
        {
            return new PathOfExileTradeStatMatchResult
            {
                Status = PathOfExileTradeStatMatchStatus.NotFound,
                NormalizedItemTemplate = normalization.NormalizedTemplate,
                Diagnostics =
                [
                    new PathOfExileTradeStatMatchDiagnostic(
                        PathOfExileTradeStatMatchDiagnosticCodes.NoCandidate,
                        "No Trade stat template matched the modifier text."),
                ],
            };
        }

        var constrained = ApplyKindConstraints(modifier!, candidates, out var mismatchWasCertain);
        if (constrained.Length == 0)
        {
            return new PathOfExileTradeStatMatchResult
            {
                Status = PathOfExileTradeStatMatchStatus.NotFound,
                NormalizedItemTemplate = normalization.NormalizedTemplate,
                Diagnostics =
                [
                    new PathOfExileTradeStatMatchDiagnostic(
                        mismatchWasCertain
                            ? PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch
                            : PathOfExileTradeStatMatchDiagnosticCodes.NoCandidate,
                        "Trade stat template candidates were incompatible with the parsed modifier kind."),
                ],
            };
        }

        if (constrained.Length == 1)
        {
            return new PathOfExileTradeStatMatchResult
            {
                Status = PathOfExileTradeStatMatchStatus.Exact,
                NormalizedItemTemplate = normalization.NormalizedTemplate,
                ExtractedNumericValues = normalization.ExtractedNumericValues,
                ExactCandidate = constrained[0],
                Candidates = constrained,
            };
        }

        return new PathOfExileTradeStatMatchResult
        {
            Status = PathOfExileTradeStatMatchStatus.Ambiguous,
            NormalizedItemTemplate = normalization.NormalizedTemplate,
            Candidates = constrained,
            Diagnostics =
            [
                new PathOfExileTradeStatMatchDiagnostic(
                    PathOfExileTradeStatMatchDiagnosticCodes.AmbiguousCandidates,
                    "Multiple Trade stat templates matched the modifier text."),
            ],
        };
    }

    private static PathOfExileTradeStatMatchCandidate[] ApplyKindConstraints(
        ParsedModifier modifier,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates,
        out bool mismatchWasCertain)
    {
        mismatchWasCertain = false;
        var requiredKind = RequiredKind(modifier);
        if (requiredKind is null)
        {
            return candidates.ToArray();
        }

        var knownCandidates = candidates
            .Where(candidate => CandidateKind(candidate) is not null)
            .ToArray();
        if (knownCandidates.Length == 0)
        {
            return candidates.ToArray();
        }

        var compatible = candidates
            .Where(candidate => CandidateKind(candidate) == requiredKind)
            .ToArray();
        mismatchWasCertain = compatible.Length == 0;
        return compatible;
    }

    private static string? RequiredKind(ParsedModifier modifier)
    {
        if (modifier.IsCrafted)
        {
            return "crafted";
        }

        if (modifier.IsFractured)
        {
            return "fractured";
        }

        if (modifier.IsVeiled)
        {
            return "veiled";
        }

        return modifier.Kind switch
        {
            ParsedModifierKind.Implicit => "implicit",
            ParsedModifierKind.Prefix or ParsedModifierKind.Suffix => "explicit",
            _ => null,
        };
    }

    private static string? CandidateKind(PathOfExileTradeStatMatchCandidate candidate)
    {
        var metadata = string.Join(
            " ",
            candidate.GroupId,
            candidate.GroupLabel,
            candidate.Type);

        if (Contains(metadata, "crafted"))
        {
            return "crafted";
        }

        if (Contains(metadata, "fractured"))
        {
            return "fractured";
        }

        if (Contains(metadata, "veiled"))
        {
            return "veiled";
        }

        if (Contains(metadata, "implicit"))
        {
            return "implicit";
        }

        if (Contains(metadata, "explicit"))
        {
            return "explicit";
        }

        return null;
    }

    private static PathOfExileTradeStatMatchCandidate ToCandidate(
        PathOfExileTradeStatEntry entry)
    {
        return new PathOfExileTradeStatMatchCandidate
        {
            StatId = entry.Id,
            Text = entry.Text,
            NormalizedTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeTemplate(entry.Text),
            GroupId = entry.GroupId,
            GroupLabel = entry.GroupLabel,
            Type = entry.Type,
            OptionMetadata = entry.OptionMetadata,
        };
    }

    private static PathOfExileTradeStatMatchResult InvalidInput(
        string code,
        string message)
    {
        return new PathOfExileTradeStatMatchResult
        {
            Status = PathOfExileTradeStatMatchStatus.InvalidInput,
            Diagnostics = [new PathOfExileTradeStatMatchDiagnostic(code, message)],
        };
    }

    private static bool Contains(string value, string expected)
    {
        return value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
