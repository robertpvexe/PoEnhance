using System.Collections.ObjectModel;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed class ParsedItemBaseResolver
{
    private const string MagicRarity = "Magic";

    public ParsedItemGameDataEnrichment Enrich(ParsedItem parsedItem, GameDataCatalog catalog)
    {
        return new ParsedItemGameDataEnrichment(parsedItem, Resolve(parsedItem, catalog));
    }

    public ItemBaseResolutionResult Resolve(ParsedItem parsedItem, GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);
        ArgumentNullException.ThrowIfNull(catalog);

        if (!string.IsNullOrWhiteSpace(parsedItem.BaseType))
        {
            return ResolveExactBaseType(parsedItem, catalog);
        }

        if (IsMagic(parsedItem) && !string.IsNullOrWhiteSpace(parsedItem.DisplayName))
        {
            return ResolveMagicDisplayName(parsedItem, catalog);
        }

        return Unknown(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            "The parsed item does not contain a base type that can be resolved.");
    }

    private static ItemBaseResolutionResult ResolveExactBaseType(
        ParsedItem parsedItem,
        GameDataCatalog catalog)
    {
        var candidates = catalog.FindItemBasesByNormalizedName(parsedItem.BaseType);
        if (candidates.Count == 0)
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseNotFound,
                "The parsed base type was not found in the game-data catalog.");
        }

        var classMatches = NarrowByItemClass(candidates, parsedItem.ItemClass);
        if (classMatches.Count == 0)
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseItemClassMismatch,
                "Catalog candidates were found, but none matched the parsed item class.",
                candidates);
        }

        if (classMatches.Count > 1)
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseAmbiguous,
                "Multiple catalog item bases match the parsed base type.",
                classMatches);
        }

        return Matched(
            ItemBaseResolutionStatus.Exact,
            classMatches[0],
            ItemBaseResolutionDiagnosticCodes.BaseExactMatch,
            "The parsed base type exactly matched one catalog item base.");
    }

    private static ItemBaseResolutionResult ResolveMagicDisplayName(
        ParsedItem parsedItem,
        GameDataCatalog catalog)
    {
        var displayName = parsedItem.DisplayName?.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseNotFound,
                "The magic item display name is empty.");
        }

        var candidates = catalog.ItemBases
            .Where(itemBase => IsMagicBaseNameCandidate(displayName, itemBase.Name))
            .ToArray();
        if (candidates.Length == 0)
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseNotFound,
                "No catalog item base matched the magic item display name.");
        }

        var longestCandidateNameLength = candidates
            .Select(candidate => candidate.Name?.Trim().Length ?? 0)
            .Max();
        var longestCandidates = candidates
            .Where(candidate => (candidate.Name?.Trim().Length ?? 0) == longestCandidateNameLength)
            .ToArray();

        var classMatches = NarrowByItemClass(longestCandidates, parsedItem.ItemClass);
        if (classMatches.Count == 0)
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseItemClassMismatch,
                "Magic-name item base candidates were found, but none matched the parsed item class.",
                longestCandidates);
        }

        if (classMatches.Count > 1)
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseAmbiguous,
                "Multiple equally specific magic-name item base candidates remain.",
                classMatches);
        }

        return Matched(
            ItemBaseResolutionStatus.Probable,
            classMatches[0],
            ItemBaseResolutionDiagnosticCodes.BaseProbableMagicSuffixMatch,
            "The magic item display name matched one catalog item base by token-boundary suffix.");
    }

    private static bool IsMagicBaseNameCandidate(string displayName, string? baseName)
    {
        baseName = baseName?.Trim();
        if (string.IsNullOrEmpty(baseName))
        {
            return false;
        }

        if (EndsWithTokenBoundary(displayName, baseName))
        {
            return true;
        }

        var suffixStart = displayName.LastIndexOf(" of ", StringComparison.OrdinalIgnoreCase);
        return suffixStart > 0 && EndsWithTokenBoundary(displayName[..suffixStart], baseName);
    }

    private static bool EndsWithTokenBoundary(string text, string suffix)
    {
        text = text.Trim();
        suffix = suffix.Trim();

        if (text.Length < suffix.Length)
        {
            return false;
        }

        if (!text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var startIndex = text.Length - suffix.Length;
        return startIndex == 0 || char.IsWhiteSpace(text[startIndex - 1]);
    }

    private static bool IsMagic(ParsedItem parsedItem)
    {
        return string.Equals(parsedItem.Rarity?.Trim(), MagicRarity, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ItemBaseRecord> NarrowByItemClass(
        IReadOnlyList<ItemBaseRecord> candidates,
        string? itemClass)
    {
        if (string.IsNullOrWhiteSpace(itemClass))
        {
            return ToReadOnly(candidates);
        }

        return ToReadOnly(candidates.Where(candidate =>
            string.Equals(
                candidate.ItemClass?.Trim(),
                itemClass.Trim(),
                StringComparison.OrdinalIgnoreCase)));
    }

    private static ItemBaseResolutionResult Matched(
        ItemBaseResolutionStatus status,
        ItemBaseRecord itemBase,
        string diagnosticCode,
        string reason)
    {
        return new ItemBaseResolutionResult
        {
            Status = status,
            MatchedItemBase = itemBase,
            ResolvedBaseId = itemBase.Id,
            ResolvedBaseName = itemBase.Name,
            Candidates = ToReadOnly([itemBase]),
            Diagnostics = Diagnostics(diagnosticCode, reason),
        };
    }

    private static ItemBaseResolutionResult Unknown(
        string diagnosticCode,
        string reason,
        IReadOnlyList<ItemBaseRecord>? candidates = null)
    {
        return new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Unknown,
            Candidates = candidates is null ? [] : ToReadOnly(candidates),
            Diagnostics = Diagnostics(diagnosticCode, reason),
        };
    }

    private static IReadOnlyList<ItemBaseResolutionDiagnostic> Diagnostics(string code, string reason)
    {
        return ToReadOnly([new ItemBaseResolutionDiagnostic(code, reason)]);
    }

    private static IReadOnlyList<T> ToReadOnly<T>(IEnumerable<T> values)
    {
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}
