using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Trade;

public sealed class TradeSearchDraftMapper
{
    public TradeSearchDraftResult CreateDraft(
        ParsedItem? parsedItem,
        ItemBaseResolutionResult? itemBaseResolution = null,
        IReadOnlyList<ModifierCandidateResolutionResult>? modifierResolutions = null,
        TradeListingMode listingMode = TradeListingMode.MerchantOnly)
    {
        if (parsedItem is null)
        {
            return Unsupported("A parsed item is required to create a Trade search draft.");
        }

        if (!HasEnoughParsedIdentity(parsedItem))
        {
            return Unsupported("The parsed item does not contain enough identity fields for an individual-item Trade search draft.");
        }

        var modifierResolutionByIndex = BuildModifierResolutionIndex(parsedItem, modifierResolutions ?? []);
        var draft = new TradeSearchDraft
        {
            ItemClass = TrimToNull(parsedItem.ItemClass),
            Rarity = TrimToNull(parsedItem.Rarity),
            DisplayName = TrimToNull(parsedItem.DisplayName),
            ParsedBaseType = TrimToNull(parsedItem.BaseType),
            ItemStates = parsedItem.ItemStates.ToArray(),
            IsCorrupted = parsedItem.IsCorrupted,
            Base = CreateBaseDraft(itemBaseResolution),
            ItemLevel = parsedItem.ItemLevel,
            TraditionalInfluences = parsedItem.TraditionalInfluences.ToArray(),
            EldritchInfluences = parsedItem.EldritchInfluences.ToArray(),
            ModifierFilters = parsedItem.Modifiers
                .Select((modifier, index) => CreateModifierFilterDraft(
                    modifier,
                    modifierResolutionByIndex.GetValueOrDefault(index)))
                .Where(filter => !string.IsNullOrWhiteSpace(filter.OriginalText))
                .ToArray(),
            ListingMode = listingMode,
        };

        return TradeSearchDraftResult.Success(draft);
    }

    private static TradeSearchDraftResult Unsupported(string message)
    {
        return TradeSearchDraftResult.Failure(
            new TradeSearchDraftDiagnostic(
                TradeSearchDraftDiagnosticCodes.UnsupportedInput,
                message));
    }

    private static bool HasEnoughParsedIdentity(ParsedItem parsedItem)
    {
        return !string.IsNullOrWhiteSpace(parsedItem.ItemClass)
            || !string.IsNullOrWhiteSpace(parsedItem.Rarity)
            || !string.IsNullOrWhiteSpace(parsedItem.DisplayName)
            || !string.IsNullOrWhiteSpace(parsedItem.BaseType);
    }

    private static Dictionary<int, ModifierCandidateResolutionResult> BuildModifierResolutionIndex(
        ParsedItem parsedItem,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions)
    {
        var results = new Dictionary<int, ModifierCandidateResolutionResult>();
        foreach (var resolution in modifierResolutions)
        {
            if (resolution.ParsedModifierIndex < 0 ||
                resolution.ParsedModifierIndex >= parsedItem.Modifiers.Count)
            {
                continue;
            }

            var parsedModifier = parsedItem.Modifiers[resolution.ParsedModifierIndex];
            if (ReferenceEquals(parsedModifier, resolution.ParsedModifier) ||
                parsedModifier == resolution.ParsedModifier)
            {
                results[resolution.ParsedModifierIndex] = resolution;
            }
        }

        return results;
    }

    private static TradeSearchBaseDraft CreateBaseDraft(ItemBaseResolutionResult? itemBaseResolution)
    {
        if (itemBaseResolution is null)
        {
            return new TradeSearchBaseDraft();
        }

        return new TradeSearchBaseDraft
        {
            Status = itemBaseResolution.Status,
            ResolvedBaseId = itemBaseResolution.Status == ItemBaseResolutionStatus.Unknown
                ? null
                : TrimToNull(itemBaseResolution.ResolvedBaseId),
            ResolvedBaseName = itemBaseResolution.Status == ItemBaseResolutionStatus.Unknown
                ? null
                : TrimToNull(itemBaseResolution.ResolvedBaseName),
        };
    }

    private static TradeModifierFilterDraft CreateModifierFilterDraft(
        ParsedModifier modifier,
        ModifierCandidateResolutionResult? resolution)
    {
        var exactCandidate = resolution?.Status == ModifierCandidateResolutionStatus.Exact &&
            resolution.Candidates.Count == 1
            ? resolution.Candidates[0]
            : null;

        return new TradeModifierFilterDraft
        {
            OriginalText = modifier.Text,
            ParsedKind = modifier.Kind,
            GenerationType = resolution?.GenerationType,
            Locality = exactCandidate is null
                ? ModifierLocality.Unknown
                : resolution?.Locality ?? ModifierLocality.Unknown,
            ParsedModifierName = TrimToNull(modifier.Name ?? resolution?.ParsedModifierName),
            CategoryText = TrimToNull(modifier.CategoryText),
            IsCrafted = modifier.IsCrafted,
            IsFractured = modifier.IsFractured,
            IsVeiled = modifier.IsVeiled,
            ResolutionStatus = resolution?.Status,
            ResolvedModifierId = TrimToNull(exactCandidate?.Id),
            ResolvedModifierName = TrimToNull(exactCandidate?.Name),
            ResolvedStatIds = exactCandidate is null
                ? []
                : exactCandidate.Stats
                    .Select(stat => TrimToNull(stat.StatId))
                    .Where(statId => statId is not null)
                    .Select(statId => statId!)
                    .ToArray(),
            IsSelected = false,
        };
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
