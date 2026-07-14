using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeQueryBuilder : IPathOfExileTradeQueryBuilder
{
    private const string RarityUnique = "Unique";
    private const string RarityRare = "Rare";
    private const string RarityMagic = "Magic";
    private const string RarityNormal = "Normal";
    private const string StatusMerchantOnly = "securable";
    private const string StatusInPerson = "available";

    public PathOfExileTradeQueryBuildResult Build(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifierFilters = null)
    {
        if (draft is null)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.NullDraft,
                "A Trade search draft is required.");
        }

        if (validationResult is null)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.NullValidationResult,
                "A local Trade search validation result is required.");
        }

        if (validationResult.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == TradeSearchValidationSeverity.Error))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.LocallyInvalidDraft,
                "The local Trade search draft has validation errors.");
        }

        var trimmedLeague = TrimToNull(leagueIdentifier);
        if (trimmedLeague is null)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.MissingLeague,
                "A league identifier is required before building a Path of Exile Trade query.");
        }

        var selectedCount = draft.ModifierFilters.Count(modifier => modifier.IsSelected);
        var providerFilters = selectedModifierFilters ?? [];
        if (selectedCount > 0 && providerFilters.Count == 0)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.SelectedModifiersMissingProviderMapping,
                "Selected modifiers require provider Trade stat mappings before query serialization.");
        }

        if (selectedCount != providerFilters.Count)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.SelectedModifierMappingMismatch,
                "Selected modifier provider mappings must match the selected modifier count.");
        }

        if (providerFilters.Any(filter => TrimToNull(filter.StatId) is null))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedModifierMapping,
                "Selected modifier provider mappings need non-empty Trade stat identifiers.");
        }

        if (!IsSupportedBaseOnlyIndividualItemPath(draft))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.UnsupportedRarityOrItemPath,
                "This item cannot be represented safely by the base-only individual-item Trade query builder.");
        }

        var selectedBaseType = SelectBaseType(draft);
        if (selectedBaseType is null)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.MissingBaseIdentity,
                "A resolved or parsed base identity is required for a base-only Path of Exile Trade query.");
        }

        var itemName = SelectItemName(draft);
        if (IsRarity(draft, RarityUnique) && itemName is null)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.MissingUniqueName,
                "A Unique item needs its unique display name for this Trade query shape.");
        }

        var statFilters = providerFilters
            .Select(filter => new PathOfExileTradeSearchStatFilter
            {
                Id = filter.StatId.Trim(),
            })
            .ToArray();

        var request = new PathOfExileTradeSearchRequest
        {
            Query = new PathOfExileTradeSearchQuery
            {
                Status = new PathOfExileTradeSearchStatus
                {
                    Option = MapListingStatus(draft.ListingMode),
                },
                Name = itemName,
                Type = selectedBaseType,
                Stats =
                [
                    new PathOfExileTradeSearchStatsGroup
                    {
                        Filters = statFilters,
                    },
                ],
            },
            Sort = new PathOfExileTradeSearchSort(),
        };

        var serializedJson = PathOfExileTradeJson.SerializeSearchRequest(request);
        return PathOfExileTradeQueryBuildResult.Success(
            trimmedLeague,
            request,
            serializedJson,
            selectedBaseType,
            draft.Base.Status);
    }

    private static bool IsSupportedBaseOnlyIndividualItemPath(TradeSearchDraft draft)
    {
        if (!IsRarity(draft, RarityRare) &&
            !IsRarity(draft, RarityMagic) &&
            !IsRarity(draft, RarityNormal) &&
            !IsRarity(draft, RarityUnique))
        {
            return false;
        }

        return !HasUnsupportedSpecialItemSignal(draft);
    }

    private static bool HasUnsupportedSpecialItemSignal(TradeSearchDraft draft)
    {
        var itemClass = draft.ItemClass?.Trim();
        if (EqualsOrdinalIgnoreCase(itemClass, "Currency") ||
            EqualsOrdinalIgnoreCase(itemClass, "Stackable Currency") ||
            EqualsOrdinalIgnoreCase(itemClass, "Gems") ||
            EqualsOrdinalIgnoreCase(itemClass, "Maps") ||
            EqualsOrdinalIgnoreCase(itemClass, "Map Fragments") ||
            EqualsOrdinalIgnoreCase(itemClass, "Divination Cards") ||
            EqualsOrdinalIgnoreCase(itemClass, "Cluster Jewels"))
        {
            return true;
        }

        return ContainsOrdinalIgnoreCase(draft.ParsedBaseType, "Cluster Jewel") ||
            ContainsOrdinalIgnoreCase(draft.ParsedBaseType, "Timeless Jewel");
    }

    private static string? SelectBaseType(TradeSearchDraft draft)
    {
        if (draft.Base.Status is ItemBaseResolutionStatus.Exact or ItemBaseResolutionStatus.Probable)
        {
            var resolvedBaseName = TrimToNull(draft.Base.ResolvedBaseName);
            if (resolvedBaseName is not null)
            {
                return resolvedBaseName;
            }
        }

        return TrimToNull(draft.ParsedBaseType);
    }

    private static string? SelectItemName(TradeSearchDraft draft)
    {
        return IsRarity(draft, RarityUnique)
            ? TrimToNull(draft.DisplayName)
            : null;
    }

    private static string MapListingStatus(TradeListingMode listingMode)
    {
        return listingMode switch
        {
            TradeListingMode.MerchantOnly => StatusMerchantOnly,
            TradeListingMode.InPerson => StatusInPerson,
            _ => StatusMerchantOnly,
        };
    }

    private static bool IsRarity(TradeSearchDraft draft, string rarity)
    {
        return EqualsOrdinalIgnoreCase(draft.Rarity?.Trim(), rarity);
    }

    private static bool EqualsOrdinalIgnoreCase(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsOrdinalIgnoreCase(string? value, string expected)
    {
        return value?.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static PathOfExileTradeQueryBuildResult Failure(
        string code,
        string message)
    {
        return PathOfExileTradeQueryBuildResult.Failure(
            new PathOfExileTradeQueryDiagnostic(code, message));
    }
}
