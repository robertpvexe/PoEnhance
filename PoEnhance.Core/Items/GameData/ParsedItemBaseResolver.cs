using System.Collections.ObjectModel;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed class ParsedItemBaseResolver
{
    private const string MagicRarity = "Magic";
    private const string NormalRarity = "Normal";
    private const string CurrencyRarity = "Currency";
    private const string CurrencyBaseType = "Currency";
    private const string StackableCurrencyItemClass = "Stackable Currency";
    private const string SynthesisedItemState = "Synthesised Item";
    private const string SynthesisedPrefix = "Synthesised ";

    public ParsedItemGameDataEnrichment Enrich(ParsedItem parsedItem, GameDataCatalog catalog)
    {
        return new ParsedItemGameDataEnrichment(parsedItem, Resolve(parsedItem, catalog));
    }

    public ItemBaseResolutionResult Resolve(ParsedItem parsedItem, GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);
        ArgumentNullException.ThrowIfNull(catalog);

        var itemClassIdentity = CanonicalItemClassIdentityResolver.Resolve(parsedItem.ItemClass);
        ItemBaseResolutionResult result;
        if (!string.IsNullOrWhiteSpace(parsedItem.BaseType))
        {
            result = ResolveExactBaseType(parsedItem, catalog);
            if (result.Status is ItemBaseResolutionStatus.Exact or ItemBaseResolutionStatus.Probable)
            {
                return result with { ItemClassIdentity = itemClassIdentity };
            }

            if (IsMagic(parsedItem))
            {
                result = ResolveCatalogNameSpan(
                    parsedItem,
                    catalog,
                    ItemBaseResolutionDiagnosticCodes.BaseProbableMagicSuffixMatch,
                    "magic item display name");
            }
            else if (IsNormal(parsedItem))
            {
                result = ResolveCatalogNameSpan(
                    parsedItem,
                    catalog,
                    ItemBaseResolutionDiagnosticCodes.BaseProbableDecoratedNameMatch,
                    "decorated Normal item name");
            }
        }
        else if (IsMagic(parsedItem) && !string.IsNullOrWhiteSpace(parsedItem.DisplayName))
        {
            result = ResolveCatalogNameSpan(
                parsedItem,
                catalog,
                ItemBaseResolutionDiagnosticCodes.BaseProbableMagicSuffixMatch,
                "magic item display name");
        }
        else
        {
            result = Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseNotFound,
                "The parsed item does not contain a base type that can be resolved.");
        }

        return result with { ItemClassIdentity = itemClassIdentity };
    }

    private static ItemBaseResolutionResult ResolveExactBaseType(
        ParsedItem parsedItem,
        GameDataCatalog catalog)
    {
        if (TryResolveGenericCategoryDisplayName(parsedItem, catalog, out var genericCategoryResult))
        {
            return genericCategoryResult;
        }

        var candidates = catalog.FindItemBasesByNormalizedName(parsedItem.BaseType);
        if (TryResolveCandidates(
            parsedItem,
            candidates,
            catalog,
            ItemBaseResolutionStatus.Exact,
            ItemBaseResolutionDiagnosticCodes.BaseExactMatch,
            "The parsed base type exactly matched one catalog item base.",
            "Catalog candidates were found, but none matched the parsed item class.",
            "Multiple catalog item bases match the parsed base type.",
            out var directResult))
        {
            return directResult;
        }

        if (TryResolveStateDecoration(parsedItem, catalog, out var stateDecorationResult))
        {
            return stateDecorationResult;
        }

        return Unknown(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            "The parsed base type was not found in the game-data catalog.");
    }

    private static ItemBaseResolutionResult ResolveCatalogNameSpan(
        ParsedItem parsedItem,
        GameDataCatalog catalog,
        string successDiagnosticCode,
        string sourceDescription)
    {
        var displayName = parsedItem.DisplayName?.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseNotFound,
                $"The {sourceDescription} is empty.");
        }

        var normalizedDisplayName = NormalizeWhitespace(displayName);
        var nameMatches = catalog.ItemBases
            .Select(itemBase => new BaseNameCandidate(itemBase, NormalizeWhitespace(itemBase.Name ?? string.Empty)))
            .Where(candidate =>
                candidate.NormalizedName.Length > 0 &&
                ContainsCompleteTokenPhrase(normalizedDisplayName, candidate.NormalizedName))
            .ToArray();
        if (nameMatches.Length == 0)
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseNotFound,
                $"No catalog item base matched the {sourceDescription} as a complete token phrase.");
        }

        var compatibleMatches = nameMatches
            .Where(candidate => IsCompatibleWithParsedItemClass(candidate.ItemBase, parsedItem.ItemClass))
            .ToArray();
        if (compatibleMatches.Length == 0)
        {
            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseItemClassMismatch,
                $"Catalog-name candidates were found in the {sourceDescription}, but none matched the parsed item class.",
                nameMatches.Select(candidate => candidate.ItemBase).ToArray());
        }

        var longestCandidateNameLength = compatibleMatches
            .Select(candidate => candidate.NormalizedName.Length)
            .Max();
        var longestCandidates = compatibleMatches
            .Where(candidate => candidate.NormalizedName.Length == longestCandidateNameLength)
            .ToArray();

        if (longestCandidates.Length > 1)
        {
            if (TryDisambiguateByParsedImplicit(
                parsedItem,
                longestCandidates.Select(candidate => candidate.ItemBase).ToArray(),
                catalog,
                out var implicitMatch))
            {
                return Matched(
                    ItemBaseResolutionStatus.Probable,
                    implicitMatch,
                    ItemBaseResolutionDiagnosticCodes.BaseExactImplicitDisambiguationMatch,
                    $"The {sourceDescription} contained an equally named catalog base and the copied base implicit uniquely proved the selected variant.");
            }

            return Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseAmbiguous,
                $"Multiple equally specific catalog base-name candidates remain in the {sourceDescription}.",
                longestCandidates.Select(candidate => candidate.ItemBase).ToArray());
        }

        return Matched(
            ItemBaseResolutionStatus.Probable,
            longestCandidates[0].ItemBase,
            successDiagnosticCode,
            $"The {sourceDescription} contained the unique longest compatible catalog item base as a complete token phrase.");
    }

    private static bool TryResolveGenericCategoryDisplayName(
        ParsedItem parsedItem,
        GameDataCatalog catalog,
        out ItemBaseResolutionResult result)
    {
        result = default!;

        var displayName = parsedItem.DisplayName?.Trim();
        if (!CanUseDisplayNameForGenericCategory(parsedItem)
            || string.IsNullOrEmpty(displayName)
            || string.Equals(displayName, parsedItem.BaseType?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidates = catalog.FindItemBasesByNormalizedName(displayName);
        if (!TryResolveCandidates(
            parsedItem,
            candidates,
            catalog,
            ItemBaseResolutionStatus.Exact,
            ItemBaseResolutionDiagnosticCodes.BaseExactMatch,
            "The item display name exactly matched one catalog item base for an explicitly supported generic category.",
            "Generic-category display-name candidates were found, but none matched the parsed item class.",
            "Multiple catalog item bases match the generic-category display name.",
            out result))
        {
            result = Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseNotFound,
                "Neither the generic base type nor the display name was found in the game-data catalog.");
            return true;
        }

        return true;
    }

    private static bool TryResolveStateDecoration(
        ParsedItem parsedItem,
        GameDataCatalog catalog,
        out ItemBaseResolutionResult result)
    {
        result = default!;

        if (!TryGetStateDecorationBaseName(parsedItem, out var undecoratedBaseName))
        {
            return false;
        }

        var candidates = catalog.FindItemBasesByNormalizedName(undecoratedBaseName);
        if (!TryResolveCandidates(
            parsedItem,
            candidates,
            catalog,
            ItemBaseResolutionStatus.Probable,
            ItemBaseResolutionDiagnosticCodes.BaseProbableStateDecorationMatch,
            "A confirmed parsed item state allowed the decorated base type to match one catalog item base.",
            "State-decoration candidates were found, but none matched the parsed item class.",
            "Multiple catalog item bases match the state-decoration base type.",
            out result))
        {
            result = Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseNotFound,
                "The parsed base type was not found in the game-data catalog.");
            return true;
        }

        return true;
    }

    private static bool CanUseDisplayNameForGenericCategory(ParsedItem parsedItem)
    {
        return string.Equals(parsedItem.BaseType?.Trim(), CurrencyBaseType, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(parsedItem.Rarity?.Trim(), CurrencyRarity, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsedItem.ItemClass?.Trim(), StackableCurrencyItemClass, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetStateDecorationBaseName(ParsedItem parsedItem, out string baseName)
    {
        baseName = string.Empty;

        var parsedBaseType = parsedItem.BaseType?.Trim();
        if (string.IsNullOrEmpty(parsedBaseType)
            || !parsedBaseType.StartsWith(SynthesisedPrefix, StringComparison.OrdinalIgnoreCase)
            || !parsedItem.ItemStates.Any(state => string.Equals(
                state.Trim(),
                SynthesisedItemState,
                StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        baseName = parsedBaseType[SynthesisedPrefix.Length..].Trim();
        return !string.IsNullOrEmpty(baseName);
    }

    private static bool TryResolveCandidates(
        ParsedItem parsedItem,
        IReadOnlyList<ItemBaseRecord> candidates,
        GameDataCatalog catalog,
        ItemBaseResolutionStatus successStatus,
        string successDiagnosticCode,
        string successReason,
        string classMismatchReason,
        string ambiguousReason,
        out ItemBaseResolutionResult result)
    {
        result = default!;

        if (candidates.Count == 0)
        {
            return false;
        }

        var classMatches = NarrowByItemClass(candidates, parsedItem.ItemClass);
        if (classMatches.Count == 0)
        {
            result = Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseItemClassMismatch,
                classMismatchReason,
                candidates);
            return true;
        }

        if (classMatches.Count > 1)
        {
            if (TryDisambiguateByParsedImplicit(
                parsedItem,
                classMatches,
                catalog,
                out var implicitMatch))
            {
                result = Matched(
                    successStatus,
                    implicitMatch,
                    ItemBaseResolutionDiagnosticCodes.BaseExactImplicitDisambiguationMatch,
                    "The copied base implicit uniquely matched one same-name, class-compatible catalog base.");
                return true;
            }

            result = Unknown(
                ItemBaseResolutionDiagnosticCodes.BaseAmbiguous,
                ambiguousReason,
                classMatches);
            return true;
        }

        result = Matched(
            successStatus,
            classMatches[0],
            successDiagnosticCode,
            successReason);
        return true;
    }

    private static bool IsMagic(ParsedItem parsedItem)
    {
        return string.Equals(parsedItem.Rarity?.Trim(), MagicRarity, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNormal(ParsedItem parsedItem)
    {
        return string.Equals(parsedItem.Rarity?.Trim(), NormalRarity, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDisambiguateByParsedImplicit(
        ParsedItem parsedItem,
        IReadOnlyList<ItemBaseRecord> candidates,
        GameDataCatalog catalog,
        out ItemBaseRecord match)
    {
        match = null!;
        if (parsedItem.ImplicitModifiers.Count == 0)
        {
            return false;
        }

        var matcher = new ModifierTextSignatureMatcher();
        var matches = candidates
            .Where(candidate => candidate.ImplicitModifierIds.Any(implicitModifierId =>
                catalog.FindModifiersById(implicitModifierId).Any(modifier =>
                    parsedItem.ImplicitModifiers.Any(parsedImplicit =>
                        matcher.Match(modifier, catalog, parsedImplicit.ValueLines).Outcome ==
                        ModifierTextSignatureMatchOutcome.Match))))
            .ToArray();
        if (matches.Length != 1)
        {
            return false;
        }

        match = matches[0];
        return true;
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsCompleteTokenPhrase(string text, string phrase)
    {
        if (phrase.Length == 0 || text.Length < phrase.Length)
        {
            return false;
        }

        var searchStart = 0;
        while (searchStart <= text.Length - phrase.Length)
        {
            var matchStart = text.IndexOf(phrase, searchStart, StringComparison.OrdinalIgnoreCase);
            if (matchStart < 0)
            {
                return false;
            }

            var matchEnd = matchStart + phrase.Length;
            if (HasTokenBoundaryBefore(text, matchStart) && HasTokenBoundaryAfter(text, matchEnd))
            {
                return true;
            }

            searchStart = matchStart + 1;
        }

        return false;
    }

    private static bool HasTokenBoundaryBefore(string text, int matchStart)
    {
        return matchStart == 0 || !IsTokenCharacter(text[matchStart - 1]);
    }

    private static bool HasTokenBoundaryAfter(string text, int matchEnd)
    {
        return matchEnd == text.Length || !IsTokenCharacter(text[matchEnd]);
    }

    private static bool IsTokenCharacter(char character)
    {
        return char.IsLetterOrDigit(character);
    }

    private static bool IsCompatibleWithParsedItemClass(ItemBaseRecord candidate, string? itemClass)
    {
        return string.IsNullOrWhiteSpace(itemClass) ||
            ItemBaseClassCompatibility.AreCompatible(itemClass, candidate.ItemClass);
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
            ItemBaseClassCompatibility.AreCompatible(itemClass, candidate.ItemClass)));
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

    private sealed record BaseNameCandidate(ItemBaseRecord ItemBase, string NormalizedName);
}
