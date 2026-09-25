using System.Globalization;
using System.Text.RegularExpressions;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.2 Track B — structural signature of official Trade / source provider templates that
/// preserves FilterArity (<c>#</c> slots) and embedded literal numeric constants. Used only for
/// decisive Ambiguous-candidate disambiguation; never invents synonyms or picks "closest".
/// </summary>
internal static partial class PathOfExileTradeStatEmbeddedConstantSignature
{
    public readonly record struct Signature(
        int FilterArity,
        IReadOnlyList<decimal> EmbeddedLiterals);

    public static Signature Derive(string? providerTemplateText)
    {
        var text = PathOfExileTradeStatTemplateNormalizer.NormalizeComparableProviderText(
            providerTemplateText);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Signature(0, []);
        }

        var arity = PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(text);
        var literals = new List<decimal>();
        foreach (Match match in EmbeddedLiteralNumberRegex().Matches(text))
        {
            if (!decimal.TryParse(
                    match.Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                continue;
            }

            literals.Add(value);
        }

        return new Signature(arity, literals);
    }

    public static bool IsCompatible(Signature source, Signature candidate) =>
        source.FilterArity == candidate.FilterArity &&
        source.EmbeddedLiterals.Count == candidate.EmbeddedLiterals.Count &&
        source.EmbeddedLiterals.SequenceEqual(candidate.EmbeddedLiterals);

    public static bool SourceProvesEmbeddedLiterals(Signature source) =>
        source.EmbeddedLiterals.Count > 0;

    public static bool ArityDistinguishesCandidates(
        Signature source,
        IReadOnlyList<Signature> candidateSignatures)
    {
        if (candidateSignatures.Count == 0)
        {
            return false;
        }

        var distinctArities = candidateSignatures
            .Select(signature => signature.FilterArity)
            .Distinct()
            .ToArray();
        return distinctArities.Length > 1 &&
            distinctArities.Contains(source.FilterArity);
    }

    [GeneratedRegex(@"(?<![\w#])[\+\-]?\d+(?:\.\d+)?(?![\w#])", RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddedLiteralNumberRegex();
}
