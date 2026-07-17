using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;
using Serilog;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeQueryBuilder : IPathOfExileTradeQueryBuilder
{
    private const string RarityUnique = "Unique";
    private const string RarityRare = "Rare";
    private const string RarityMagic = "Magic";
    private const string RarityNormal = "Normal";
    private const string StatusInstantBuyout = "securable";
    private const string StatusInPerson = "onlineleague";
    private const string TypeFiltersKey = "type_filters";
    private const string ProviderRarityFilterKey = "rarity";
    private const string ProviderCategoryFilterKey = "category";
    private const string ProviderRarityNormal = "normal";
    private const string ProviderRarityMagic = "magic";
    private const string ProviderRarityRare = "rare";
    private const string MiscFiltersKey = "misc_filters";
    private const string ProviderFoulbornFilterKey = "mutated";
    private const string WeaponFiltersKey = "weapon_filters";
    private const string ProviderPerHitDamageFilterKey = "damage";
    private readonly PathOfExileTradeItemPropertyResolver itemPropertyResolver;
    private readonly PathOfExileTradeRequestedItemFilterResolver requestedItemFilterResolver;

    public PathOfExileTradeQueryBuilder(
        PathOfExileTradeItemPropertyResolver? itemPropertyResolver = null,
        PathOfExileTradeRequestedItemFilterResolver? requestedItemFilterResolver = null)
    {
        this.itemPropertyResolver = itemPropertyResolver ?? new PathOfExileTradeItemPropertyResolver();
        this.requestedItemFilterResolver = requestedItemFilterResolver ??
            new PathOfExileTradeRequestedItemFilterResolver();
    }

    public PathOfExileTradeQueryBuildResult Build(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifierFilters = null,
        PathOfExileTradeItemIdentity? providerItemIdentity = null,
        PathOfExileTradeFilterCatalog? providerFilterCatalog = null,
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter>? selectedItemPropertyFilters = null)
    {
        return Build(
            draft,
            validationResult,
            leagueIdentifier,
            selectedModifierFilters,
            providerItemIdentity,
            providerFilterCatalog,
            selectedItemPropertyFilters,
            selectedRequestedItemFilters: null);
    }

    public PathOfExileTradeQueryBuildResult Build(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifierFilters,
        PathOfExileTradeItemIdentity? providerItemIdentity,
        PathOfExileTradeFilterCatalog? providerFilterCatalog,
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter>? selectedItemPropertyFilters,
        IReadOnlyList<PathOfExileTradeSelectedRequestedItemFilter>? selectedRequestedItemFilters)
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

        var selectedSourceIndexes = draft.ModifierFilters
            .Select((modifier, index) => new { Modifier = modifier, Index = index })
            .Where(indexed =>
                indexed.Modifier.IsSelected &&
                indexed.Modifier.ProviderResolutionStatus != SearchComponentProviderResolutionStatus.BaseGuaranteed)
            .Select(indexed => indexed.Index)
            .ToHashSet();
        var providerFilters = selectedModifierFilters ?? [];
        if (selectedSourceIndexes.Count > 0 && providerFilters.Count == 0)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.SelectedModifiersMissingProviderMapping,
                "Selected modifiers require provider Trade stat mappings before query serialization.");
        }

        var mappedSourceIndexes = providerFilters
            .SelectMany(filter => filter.SourceIndexes.Count > 0
                ? filter.SourceIndexes
                : [filter.SourceIndex])
            .ToHashSet();
        if (!selectedSourceIndexes.SetEquals(mappedSourceIndexes))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.SelectedModifierMappingMismatch,
                "Selected modifier provider mappings must cover exactly the selected modifiers.");
        }

        if (providerFilters.Any(filter => TrimToNull(filter.StatId) is null))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedModifierMapping,
                "Selected modifier provider mappings need non-empty Trade stat identifiers.");
        }

        var providerPropertyFilters = selectedItemPropertyFilters ?? [];
        var selectedPropertyIndexes = draft.ItemProperties
            .Select((property, index) => new { Property = property, Index = index })
            .Where(indexed => indexed.Property.IsSelected)
            .Select(indexed => indexed.Index)
            .ToHashSet();
        if (selectedPropertyIndexes.Count > 0 && providerPropertyFilters.Count == 0)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.SelectedItemPropertiesMissingProviderMapping,
                "Selected item properties require verified provider Trade filter mappings before query serialization.");
        }

        var mappedPropertyIndexes = providerPropertyFilters
            .Select(filter => filter.SourceItemPropertyIndex)
            .ToArray();
        if (mappedPropertyIndexes.Distinct().Count() != mappedPropertyIndexes.Length)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.DuplicateSelectedItemPropertySourceIndex,
                "A selected item property may have only one provider mapping.");
        }

        if (!selectedPropertyIndexes.SetEquals(mappedPropertyIndexes))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.SelectedItemPropertyMappingMismatch,
                "Item-property provider mappings must cover exactly the selected item properties.");
        }

        if (providerPropertyFilters.Any(filter =>
                TrimToNull(filter.ProviderGroupId) is null ||
                TrimToNull(filter.ProviderFilterId) is null ||
                !string.Equals(filter.ProviderGroupId.Trim(), WeaponFiltersKey, StringComparison.Ordinal) ||
                string.Equals(filter.ProviderFilterId.Trim(), ProviderPerHitDamageFilterKey, StringComparison.Ordinal)))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping,
                "Selected item-property mappings require non-empty reviewed weapon filter identities and may not use the per-hit damage filter.");
        }

        if (providerPropertyFilters
            .GroupBy(
                filter => $"{filter.ProviderGroupId.Trim()}\n{filter.ProviderFilterId.Trim()}",
                StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.DuplicateSelectedItemPropertyProviderIdentity,
                "Selected item properties may not emit duplicate provider group/filter identities.");
        }

        IReadOnlyDictionary<int, PathOfExileTradeSelectedItemPropertyFilter> verifiedPropertyFilters =
            new Dictionary<int, PathOfExileTradeSelectedItemPropertyFilter>();
        if (providerPropertyFilters.Count > 0)
        {
            if (providerFilterCatalog is null)
            {
                return Failure(
                    PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping,
                    "Selected item properties require the session-verified Trade filter catalog.");
            }

            var verifiedMapping = itemPropertyResolver.MapSelected(draft, providerFilterCatalog);
            if (!verifiedMapping.IsSuccess ||
                verifiedMapping.Filters.Count != providerPropertyFilters.Count)
            {
                return Failure(
                    PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping,
                    "Selected item-property mappings no longer match the reviewed mapping, official catalog, or weapon derivation evidence.");
            }

            verifiedPropertyFilters = verifiedMapping.Filters.ToDictionary(
                filter => filter.SourceItemPropertyIndex);
        }

        foreach (var filter in providerPropertyFilters)
        {
            var property = draft.ItemProperties[filter.SourceItemPropertyIndex];
            if (property.Kind == TradeSearchItemPropertyKind.ChaosDps ||
                property.ProviderResolutionStatus != TradeSearchItemPropertyProviderResolutionStatus.Exact ||
                !property.IsSearchable ||
                !verifiedPropertyFilters.TryGetValue(filter.SourceItemPropertyIndex, out var verifiedFilter) ||
                !string.Equals(
                    filter.ProviderGroupId.Trim(),
                    verifiedFilter.ProviderGroupId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    filter.ProviderFilterId.Trim(),
                    verifiedFilter.ProviderFilterId,
                    StringComparison.Ordinal) ||
                filter.RequestedMinimum != property.RequestedMinimum ||
                filter.RequestedMaximum != property.RequestedMaximum)
            {
                return Failure(
                    PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedItemPropertyMapping,
                    "Selected item-property mappings must retain exact searchable source state and canonical bounds; Chaos DPS is unsupported.");
            }
        }

        var providerRequestedFilters = selectedRequestedItemFilters ?? [];
        var activeRequestedKinds = draft.RequestedItemFilters
            .Where(filter => filter.IsActive)
            .Select(filter => filter.Kind)
            .ToHashSet();
        var mappedRequestedKinds = providerRequestedFilters
            .Select(filter => filter.SourceKind)
            .ToArray();
        if (mappedRequestedKinds.Distinct().Count() != mappedRequestedKinds.Length ||
            !activeRequestedKinds.SetEquals(mappedRequestedKinds))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.SelectedRequestedItemFilterMappingMismatch,
                "Requested item-filter provider mappings must cover exactly the active requested filters once each.");
        }

        if (providerRequestedFilters.Count > 0)
        {
            if (providerFilterCatalog is null)
            {
                return Failure(
                    PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedRequestedItemFilterMapping,
                    "Active requested item filters require the session-verified Trade filter catalog.");
            }

            var verifiedRequested = requestedItemFilterResolver.MapSelected(draft, providerFilterCatalog);
            if (!verifiedRequested.IsSuccess ||
                verifiedRequested.Filters.Count != providerRequestedFilters.Count ||
                providerRequestedFilters.Any(filter => !verifiedRequested.Filters.Any(verified =>
                    verified == filter)))
            {
                return Failure(
                    PathOfExileTradeQueryDiagnosticCodes.InvalidSelectedRequestedItemFilterMapping,
                    "Active requested item filters no longer match the reviewed mapping, requested minimum, or official catalog.");
            }
        }

        if (!IsSupportedBaseOnlyIndividualItemPath(draft))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.UnsupportedRarityOrItemPath,
                "This item cannot be represented safely by the base-only individual-item Trade query builder.");
        }

        if (IsRarity(draft, RarityUnique) &&
            (TrimToNull(providerItemIdentity?.CanonicalName) is null ||
                TrimToNull(providerItemIdentity?.CanonicalType) is null))
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.MissingProviderUniqueIdentity,
                "A Unique item needs a resolved provider item identity before query serialization.");
        }

        var selectedBaseType = SelectBaseType(draft, providerItemIdentity);
        var categoryOptionResult = SelectProviderCategoryOption(draft, providerFilterCatalog);
        if (!categoryOptionResult.IsSuccess)
        {
            return Failure(
                categoryOptionResult.DiagnosticCode,
                categoryOptionResult.DiagnosticMessage);
        }

        if (selectedBaseType is null && categoryOptionResult.Option is null)
        {
            return Failure(
                PathOfExileTradeQueryDiagnosticCodes.MissingBaseIdentity,
                "An active category or exact base identity is required for a Path of Exile Trade query.");
        }

        var itemNameResult = SelectItemName(draft, providerItemIdentity);
        if (!itemNameResult.IsSuccess)
        {
            return Failure(
                itemNameResult.DiagnosticCode,
                itemNameResult.DiagnosticMessage);
        }

        if (IsRarity(draft, RarityUnique))
        {
            Log.Debug(
                "Path of Exile Trade unique name decision. Decision={Decision}; BaseType={BaseType}; CanonicalNamePresent={CanonicalNamePresent}",
                itemNameResult.Decision,
                selectedBaseType,
                itemNameResult.Name is not null);
        }

        var statFilters = providerFilters
            .Select(filter => new PathOfExileTradeSearchStatFilter
            {
                Id = filter.StatId.Trim(),
                Value = filter.Minimum.HasValue || filter.Maximum.HasValue
                    ? new PathOfExileTradeSearchStatValue
                    {
                        Min = filter.Minimum,
                        Max = filter.Maximum,
                    }
                    : null,
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
                Name = itemNameResult.Name,
                Type = selectedBaseType,
                Stats =
                [
                    new PathOfExileTradeSearchStatsGroup
                    {
                        Filters = statFilters,
                    },
                ],
                Filters = BuildProviderFilters(
                    draft,
                    providerItemIdentity,
                    categoryOptionResult.Option,
                    providerPropertyFilters,
                    providerRequestedFilters),
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
        var isUnique = IsRarity(draft, RarityUnique);
        if (EqualsOrdinalIgnoreCase(itemClass, "Currency") ||
            EqualsOrdinalIgnoreCase(itemClass, "Stackable Currency") ||
            EqualsOrdinalIgnoreCase(itemClass, "Gems") ||
            EqualsOrdinalIgnoreCase(itemClass, "Maps") ||
            EqualsOrdinalIgnoreCase(itemClass, "Map Fragments") ||
            EqualsOrdinalIgnoreCase(itemClass, "Divination Cards") ||
            (!isUnique && EqualsOrdinalIgnoreCase(itemClass, "Cluster Jewels")))
        {
            return true;
        }

        return (!isUnique && ContainsOrdinalIgnoreCase(draft.ParsedBaseType, "Cluster Jewel")) ||
            ContainsOrdinalIgnoreCase(draft.ParsedBaseType, "Timeless Jewel");
    }

    private static string? SelectBaseType(
        TradeSearchDraft draft,
        PathOfExileTradeItemIdentity? providerItemIdentity)
    {
        if (IsRarity(draft, RarityUnique))
        {
            return TrimToNull(providerItemIdentity?.CanonicalType);
        }

        if (draft.Base.ActiveCriterion?.Mode == BaseSearchMode.Category)
        {
            return null;
        }

        if (draft.Base.ActiveCriterion?.Mode == BaseSearchMode.ExactBase)
        {
            return TrimToNull(draft.Base.ActiveCriterion.ExactBaseName) ??
                TrimToNull(draft.Base.ResolvedBaseName) ??
                TrimToNull(draft.ParsedBaseType);
        }

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

    private static ProviderCategoryOptionSelectionResult SelectProviderCategoryOption(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog? providerFilterCatalog)
    {
        if (IsRarity(draft, RarityUnique) ||
            draft.Base.ActiveCriterion?.Mode != BaseSearchMode.Category)
        {
            return ProviderCategoryOptionSelectionResult.Success(null);
        }

        var category = TrimToNull(draft.Base.ActiveCriterion.Category) ??
            TrimToNull(draft.Base.Category);
        if (category is null)
        {
            return ProviderCategoryOptionSelectionResult.Failure(
                PathOfExileTradeQueryDiagnosticCodes.MissingBaseIdentity,
                "Category mode requires a resolved ordinary item category.");
        }

        if (providerFilterCatalog is null)
        {
            return ProviderCategoryOptionSelectionResult.Failure(
                PathOfExileTradeQueryDiagnosticCodes.MissingProviderCategoryCatalog,
                "Category mode requires the current Path of Exile Trade filter catalog.");
        }

        if (!providerFilterCatalog.TryFindCategoryOption(category, out var option))
        {
            return ProviderCategoryOptionSelectionResult.Failure(
                PathOfExileTradeQueryDiagnosticCodes.UnsupportedProviderCategory,
                $"The current Path of Exile Trade filters do not contain an exact category option for '{category}'.");
        }

        return ProviderCategoryOptionSelectionResult.Success(option);
    }

    private static UniqueNameSelectionResult SelectItemName(
        TradeSearchDraft draft,
        PathOfExileTradeItemIdentity? providerItemIdentity)
    {
        if (!IsRarity(draft, RarityUnique))
        {
            return UniqueNameSelectionResult.Success(null, "NonUnique");
        }

        var canonicalName = TrimToNull(providerItemIdentity?.CanonicalName);
        if (canonicalName is null)
        {
            return UniqueNameSelectionResult.Failure(
                PathOfExileTradeQueryDiagnosticCodes.MissingProviderUniqueIdentity,
                "A Unique item needs a resolved provider item identity before query serialization.",
                "MissingProviderIdentity");
        }

        return UniqueNameSelectionResult.Success(canonicalName, "ProviderItemIdentity");
    }

    private static IReadOnlyDictionary<string, object> BuildProviderFilters(
        TradeSearchDraft draft,
        PathOfExileTradeItemIdentity? providerItemIdentity,
        PathOfExileTradeFilterOption? categoryOption,
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter> itemPropertyFilters,
        IReadOnlyList<PathOfExileTradeSelectedRequestedItemFilter> requestedItemFilters)
    {
        var groups = new Dictionary<string, object>(StringComparer.Ordinal);
        var typeFilters = new Dictionary<string, object>(StringComparer.Ordinal);
        var rarityOption = MapNonUniqueRarityOption(draft);
        if (rarityOption is not null)
        {
            typeFilters[ProviderRarityFilterKey] = new PathOfExileTradeSearchOptionFilter
            {
                Option = rarityOption,
            };
        }

        if (categoryOption is not null)
        {
            typeFilters[ProviderCategoryFilterKey] = new PathOfExileTradeSearchOptionFilter
            {
                Option = categoryOption.Id,
            };
        }

        if (typeFilters.Count > 0)
        {
            groups[TypeFiltersKey] = new PathOfExileTradeSearchFilterGroup
            {
                Filters = typeFilters,
            };
        }

        if (itemPropertyFilters.Count > 0)
        {
            AddFilterGroup(
                groups,
                WeaponFiltersKey,
                itemPropertyFilters.ToDictionary(
                    filter => filter.ProviderFilterId.Trim(),
                    filter => (object)new PathOfExileTradeSearchStatValue
                    {
                        Min = filter.RequestedMinimum,
                        Max = filter.RequestedMaximum,
                    },
                    StringComparer.Ordinal));
        }

        foreach (var requestedGroup in requestedItemFilters.GroupBy(
                     filter => filter.ProviderGroupId.Trim(),
                     StringComparer.Ordinal))
        {
            AddFilterGroup(
                groups,
                requestedGroup.Key,
                requestedGroup.ToDictionary(
                    filter => filter.ProviderFilterId.Trim(),
                    filter => (object)new PathOfExileTradeSearchStatValue
                    {
                        Min = filter.MinimumValue,
                    },
                    StringComparer.Ordinal));
        }

        if (providerItemIdentity is null ||
            providerItemIdentity.Foulborn is TradeTriState.Auto or TradeTriState.Any)
        {
            return groups;
        }

        var option = providerItemIdentity.Foulborn == TradeTriState.Yes
            ? "true"
            : "false";

        AddFilterGroup(
            groups,
            MiscFiltersKey,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [ProviderFoulbornFilterKey] = new PathOfExileTradeSearchOptionFilter
                {
                    Option = option,
                },
            });
        return groups;
    }

    private static void AddFilterGroup(
        IDictionary<string, object> groups,
        string groupId,
        IReadOnlyDictionary<string, object> filters)
    {
        var merged = groups.TryGetValue(groupId, out var existing) &&
            existing is PathOfExileTradeSearchFilterGroup existingGroup
                ? existingGroup.Filters.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (filterId, value) in filters)
        {
            merged.Add(filterId, value);
        }

        groups[groupId] = new PathOfExileTradeSearchFilterGroup { Filters = merged };
    }

    private static string MapListingStatus(TradeListingMode listingMode)
    {
        return listingMode switch
        {
            TradeListingMode.InstantBuyout => StatusInstantBuyout,
            TradeListingMode.InPerson => StatusInPerson,
            _ => StatusInstantBuyout,
        };
    }

    private static string? MapNonUniqueRarityOption(TradeSearchDraft draft)
    {
        if (IsRarity(draft, RarityNormal))
        {
            return ProviderRarityNormal;
        }

        if (IsRarity(draft, RarityMagic))
        {
            return ProviderRarityMagic;
        }

        if (IsRarity(draft, RarityRare))
        {
            return ProviderRarityRare;
        }

        return null;
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

    private sealed record UniqueNameSelectionResult
    {
        public required bool IsSuccess { get; init; }

        public string? Name { get; init; }

        public string Decision { get; init; } = string.Empty;

        public string DiagnosticCode { get; init; } = string.Empty;

        public string DiagnosticMessage { get; init; } = string.Empty;

        public static UniqueNameSelectionResult Success(
            string? name,
            string decision)
        {
            return new UniqueNameSelectionResult
            {
                IsSuccess = true,
                Name = name,
                Decision = decision,
            };
        }

        public static UniqueNameSelectionResult Failure(
            string code,
            string message,
            string decision)
        {
            return new UniqueNameSelectionResult
            {
                IsSuccess = false,
                DiagnosticCode = code,
                DiagnosticMessage = message,
                Decision = decision,
            };
        }
    }

    private sealed record ProviderCategoryOptionSelectionResult
    {
        public required bool IsSuccess { get; init; }

        public PathOfExileTradeFilterOption? Option { get; init; }

        public string DiagnosticCode { get; init; } = string.Empty;

        public string DiagnosticMessage { get; init; } = string.Empty;

        public static ProviderCategoryOptionSelectionResult Success(
            PathOfExileTradeFilterOption? option)
        {
            return new ProviderCategoryOptionSelectionResult
            {
                IsSuccess = true,
                Option = option,
            };
        }

        public static ProviderCategoryOptionSelectionResult Failure(
            string code,
            string message)
        {
            return new ProviderCategoryOptionSelectionResult
            {
                IsSuccess = false,
                DiagnosticCode = code,
                DiagnosticMessage = message,
            };
        }
    }
}
