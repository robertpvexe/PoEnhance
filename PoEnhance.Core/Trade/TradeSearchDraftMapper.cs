using System.Collections.Immutable;
using System.Globalization;
using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

public sealed class TradeSearchDraftMapper
{
    public TradeSearchDraftResult CreateDraft(
        ParsedItem? parsedItem,
        ItemBaseResolutionResult? itemBaseResolution = null,
        IReadOnlyList<ModifierCandidateResolutionResult>? modifierResolutions = null,
        GameDataCatalog? gameDataCatalog = null,
        TradeListingMode listingMode = TradeListingMode.InstantBuyout)
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
        var aggregation = CanonicalModifierEffectAggregator.Aggregate(
            CreateSearchComponents(
                    parsedItem,
                    itemBaseResolution,
                    modifierResolutionByIndex,
                    gameDataCatalog)
                .ToArray());
        var derivedPropertyCalculator = new DerivedWeaponPropertyCalculator();
        var derivedWeaponProperties = derivedPropertyCalculator.CalculateQ20(
            parsedItem,
            itemBaseResolution?.MatchedItemBase,
            CreateDerivedWeaponModifierEffects(aggregation.Components));
        var derivedDefensiveProperties = derivedPropertyCalculator.CalculateDefensiveQ20(
            parsedItem,
            itemBaseResolution?.MatchedItemBase,
            CreateDerivedWeaponModifierEffects(aggregation.Components));
        var itemProperties = CreateItemProperties(derivedWeaponProperties, derivedDefensiveProperties);
        var itemPropertyContributionGroups = TradeSearchItemPropertyContributionGroupBuilder.Create(
            itemProperties,
            aggregation.Components);
        var draft = new TradeSearchDraft
        {
            ItemClass = TrimToNull(parsedItem.ItemClass),
            CanonicalItemClass = ResolveCanonicalItemClass(parsedItem, itemBaseResolution),
            Rarity = TrimToNull(parsedItem.Rarity),
            DisplayName = TrimToNull(parsedItem.DisplayName),
            ParsedBaseType = TrimToNull(parsedItem.BaseType),
            ItemStates = parsedItem.ItemStates.ToArray(),
            IsCorrupted = parsedItem.IsCorrupted,
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Mirrored = parsedItem.IsMirrored ? TradeTriState.Yes : TradeTriState.No,
                Corrupted = parsedItem.IsCorrupted ? TradeTriState.Yes : TradeTriState.No,
                Identified = parsedItem.IsIdentified ? TradeTriState.Yes : TradeTriState.No,
            },
            Base = CreateBaseDraft(parsedItem, itemBaseResolution),
            ItemLevel = parsedItem.ItemLevel,
            SocketText = ReadSocketText(parsedItem),
            BaseRollPercentile = DerivedBaseRollPercentileCalculator.Calculate(derivedDefensiveProperties),
            RequestedItemFilters = CreateRequestedItemFilters(parsedItem),
            TraditionalInfluences = parsedItem.TraditionalInfluences.ToArray(),
            EldritchInfluences = parsedItem.EldritchInfluences.ToArray(),
            ItemProperties = itemProperties,
            ItemPropertyDiagnostics = derivedWeaponProperties.Diagnostics
                .Select(diagnostic => new TradeSearchItemPropertyDiagnostic(
                    diagnostic.Code,
                    diagnostic.Reason,
                    diagnostic.SourceProperty))
                .ToImmutableArray(),
            ModifierFilters = aggregation.Components,
            ItemPropertyContributionGroups = itemPropertyContributionGroups,
            ModifierAggregationDiagnostics = aggregation.Diagnostics,
            ListingMode = listingMode,
        };

        return TradeSearchDraftResult.Success(draft);
    }

    private static ImmutableArray<TradeSearchRequestedItemFilter> CreateRequestedItemFilters(
        ParsedItem parsedItem)
    {
        var quality = ReadObservedQualityFilter(parsedItem);
        var links = ReadObservedLinksFilter(parsedItem);
        var filters = ImmutableArray.CreateBuilder<TradeSearchRequestedItemFilter>();
        filters.Add(CreateRequestedFilter(
            TradeSearchRequestedItemFilterKind.ItemLevel,
            "Item Level",
            parsedItem.ItemLevel,
            parsedItem.ItemLevel?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            parsedItem.ItemLevel.HasValue ? null : "The copied item has no valid Item Level."));
        filters.Add(quality);
        filters.Add(links);
        if (ReadObservedSocketCountFilter(parsedItem) is { } sockets)
        {
            filters.Add(sockets);
        }

        return filters.ToImmutable();
    }

    public static TradeSearchRequestedItemFilter ParseRequestedItemFilterText(
        TradeSearchRequestedItemFilter source,
        string? currentText,
        bool? isActive = null)
    {
        currentText ??= string.Empty;
        var status = string.IsNullOrWhiteSpace(currentText)
            ? TradeSearchRequestedItemFilterValidationStatus.Empty
            : currentText.All(char.IsAsciiDigit) &&
                int.TryParse(currentText, NumberStyles.None, CultureInfo.InvariantCulture, out _)
                ? TradeSearchRequestedItemFilterValidationStatus.Valid
                : TradeSearchRequestedItemFilterValidationStatus.Invalid;
        var requestedValue = status == TradeSearchRequestedItemFilterValidationStatus.Valid
            ? int.Parse(currentText, NumberStyles.None, CultureInfo.InvariantCulture)
            : (int?)null;
        return source with
        {
            CurrentText = currentText,
            RequestedMinimum = requestedValue,
            IsActive = isActive ?? source.IsActive,
            LocalValidationStatus = status,
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
            DiagnosticReason = status switch
            {
                TradeSearchRequestedItemFilterValidationStatus.Invalid =>
                    $"{source.Label} must be an unsigned integer.",
                _ => null,
            },
        };
    }

    private static TradeSearchRequestedItemFilter ReadObservedQualityFilter(ParsedItem parsedItem)
    {
        var properties = parsedItem.Properties
            .Where(property => string.Equals(property.NormalizedName, "quality", StringComparison.Ordinal))
            .ToArray();
        if (properties.Length == 0)
        {
            return CreateRequestedFilter(
                TradeSearchRequestedItemFilterKind.Quality,
                "Quality",
                0,
                "0");
        }

        if (properties.Length != 1)
        {
            return CreateRequestedFilter(
                TradeSearchRequestedItemFilterKind.Quality,
                "Quality",
                null,
                properties[0].RawValueText,
                "More than one Quality property was parsed; observed Quality is ambiguous.");
        }

        var property = properties[0];
        if (property.NumericGroups.Count != 1 ||
            !property.NumericGroups[0].IsScalar ||
            !property.NumericGroups[0].IsPercentage ||
            property.NumericGroups[0].ScalarValue is not { } value ||
            value < 0m ||
            value != decimal.Truncate(value) ||
            value > int.MaxValue)
        {
            return CreateRequestedFilter(
                TradeSearchRequestedItemFilterKind.Quality,
                "Quality",
                null,
                property.RawValueText,
                "Observed Quality is malformed or unsupported and was not replaced with zero.");
        }

        var observed = (int)value;
        return CreateRequestedFilter(
            TradeSearchRequestedItemFilterKind.Quality,
            "Quality",
            observed,
            observed.ToString(CultureInfo.InvariantCulture));
    }

    private static TradeSearchRequestedItemFilter ReadObservedLinksFilter(ParsedItem parsedItem)
    {
        var properties = parsedItem.Properties
            .Where(property => string.Equals(property.NormalizedName, "sockets", StringComparison.Ordinal))
            .ToArray();
        if (properties.Length == 0)
        {
            return CreateRequestedFilter(
                TradeSearchRequestedItemFilterKind.Links,
                "Links",
                0,
                "0");
        }

        if (properties.Length != 1 || !TryReadMaximumLinkedGroup(properties[0].RawValueText, out var links))
        {
            return CreateRequestedFilter(
                TradeSearchRequestedItemFilterKind.Links,
                "Links",
                null,
                string.Empty,
                "The copied socket/link representation is malformed or ambiguous.");
        }

        return CreateRequestedFilter(
            TradeSearchRequestedItemFilterKind.Links,
            "Links",
            links,
            links.ToString(CultureInfo.InvariantCulture));
    }

    private static TradeSearchRequestedItemFilter? ReadObservedSocketCountFilter(ParsedItem parsedItem)
    {
        var properties = parsedItem.Properties
            .Where(property => string.Equals(property.NormalizedName, "sockets", StringComparison.Ordinal))
            .ToArray();
        if (properties.Length != 1 ||
            !TryReadSocketSummary(properties[0].RawValueText, out _, out var socketCount))
        {
            return null;
        }

        return CreateRequestedFilter(
            TradeSearchRequestedItemFilterKind.Sockets,
            "Sockets",
            socketCount,
            socketCount.ToString(CultureInfo.InvariantCulture));
    }

    private static TradeSearchRequestedItemFilter CreateRequestedFilter(
        TradeSearchRequestedItemFilterKind kind,
        string label,
        int? observedValue,
        string currentText,
        string? diagnosticReason = null)
    {
        var source = new TradeSearchRequestedItemFilter
        {
            Kind = kind,
            Label = label,
            ObservedValue = observedValue,
            CurrentText = currentText,
            RequestedMinimum = observedValue,
            IsActive = false,
            LocalValidationStatus = observedValue.HasValue
                ? TradeSearchRequestedItemFilterValidationStatus.Valid
                : TradeSearchRequestedItemFilterValidationStatus.Invalid,
            DiagnosticReason = diagnosticReason,
        };
        return observedValue.HasValue
            ? source
            : ParseRequestedItemFilterText(source, currentText, isActive: false) with
            {
                DiagnosticReason = diagnosticReason,
            };
    }

    private static string? ReadSocketText(ParsedItem parsedItem)
    {
        var properties = parsedItem.Properties
            .Where(property => string.Equals(property.NormalizedName, "sockets", StringComparison.Ordinal))
            .ToArray();
        return properties.Length == 1 && !string.IsNullOrWhiteSpace(properties[0].RawValueText)
            ? properties[0].RawValueText
            : null;
    }

    private static bool TryReadMaximumLinkedGroup(string? socketText, out int maximumLinks) =>
        TryReadSocketSummary(socketText, out maximumLinks, out _);

    private static bool TryReadSocketSummary(
        string? socketText,
        out int maximumLinks,
        out int socketCount)
    {
        maximumLinks = 0;
        socketCount = 0;
        if (string.IsNullOrWhiteSpace(socketText))
        {
            return false;
        }

        foreach (var group in socketText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var sockets = group.Split('-', StringSplitOptions.None);
            if (sockets.Length == 0 || sockets.Any(socket =>
                    socket.Length != 1 || !char.IsAsciiLetterOrDigit(socket[0])))
            {
                return false;
            }

            maximumLinks = Math.Max(maximumLinks, sockets.Length);
            socketCount += sockets.Length;
        }

        return maximumLinks > 0 && socketCount > 0;
    }

    private static ImmutableArray<TradeSearchItemProperty> CreateItemProperties(
        DerivedWeaponProperties derived,
        DerivedDefensiveProperties defensive)
    {
        var properties = ImmutableArray.CreateBuilder<TradeSearchItemProperty>();
        if (derived.Status == DerivedWeaponPropertyStatus.Success && derived.TotalDps.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.TotalDps,
                "Total DPS",
                derived.TotalDps.Value,
                derived.Q20Status == DerivedWeaponQ20Status.Success ? "Q20" : null,
                derived.PhysicalDamage?.SourceProperty,
                derived.ElementalDamage?.SourceProperty,
                derived.ChaosDamage?.SourceProperty,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.Status == DerivedWeaponPropertyStatus.Success && derived.PhysicalDps.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.PhysicalDps,
                "Physical DPS",
                derived.PhysicalDps.Value,
                derived.Q20Status == DerivedWeaponQ20Status.Success ? "Q20" : null,
                derived.PhysicalDamage?.SourceProperty,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.Status == DerivedWeaponPropertyStatus.Success && derived.ElementalDps.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.ElementalDps,
                "Elemental DPS",
                derived.ElementalDps.Value,
                calculationBasisLabel: null,
                derived.ElementalDamage?.SourceProperty,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.Status == DerivedWeaponPropertyStatus.Success && derived.ChaosDps.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.ChaosDps,
                "Chaos DPS",
                derived.ChaosDps.Value,
                calculationBasisLabel: null,
                derived.ChaosDamage?.SourceProperty,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.Status == DerivedWeaponPropertyStatus.Success && derived.AttacksPerSecond.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.AttacksPerSecond,
                "Attacks per Second",
                derived.AttacksPerSecond.Value,
                calculationBasisLabel: null,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.Status == DerivedWeaponPropertyStatus.Success && derived.CriticalStrikeChance.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.CriticalStrikeChance,
                "Critical Strike Chance",
                derived.CriticalStrikeChance.Value,
                calculationBasisLabel: null,
                derived.CriticalStrikeChanceSourceProperty));
        }

        foreach (var property in defensive.Properties)
        {
            properties.Add(CreateItemProperty(
                    DefensiveKind(property.Target),
                    DefensiveLabel(property.Target),
                    property.Value,
                    property.IsQ20 ? "Q20" : null,
                    property.SourceProperty) with
            {
                DerivationUnsupportedReason = property.UnsupportedReason,
                NotSearchableReason = property.UnsupportedReason ??
                    "Provider mapping for derived item properties is not available.",
            });
        }

        return properties.ToImmutable();
    }

    private static TradeSearchItemPropertyKind DefensiveKind(ItemPropertyTarget target) => target switch
    {
        ItemPropertyTarget.EnergyShield => TradeSearchItemPropertyKind.EnergyShield,
        ItemPropertyTarget.Armour => TradeSearchItemPropertyKind.Armour,
        ItemPropertyTarget.Evasion => TradeSearchItemPropertyKind.EvasionRating,
        ItemPropertyTarget.Ward => TradeSearchItemPropertyKind.Ward,
        ItemPropertyTarget.Block => TradeSearchItemPropertyKind.ChanceToBlock,
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    private static string DefensiveLabel(ItemPropertyTarget target) => target switch
    {
        ItemPropertyTarget.EnergyShield => "Energy Shield",
        ItemPropertyTarget.Armour => "Armour",
        ItemPropertyTarget.Evasion => "Evasion Rating",
        ItemPropertyTarget.Ward => "Ward",
        ItemPropertyTarget.Block => "Chance to Block",
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    private static TradeSearchItemProperty CreateItemProperty(
        TradeSearchItemPropertyKind kind,
        string label,
        decimal value,
        string? calculationBasisLabel,
        params ParsedItemProperty?[] sourceProperties)
    {
        return new TradeSearchItemProperty
        {
            Kind = kind,
            Label = label,
            CalculationBasisLabel = calculationBasisLabel,
            ObservedValue = value,
            RequestedMinimum = value,
            RequestedMaximum = null,
            IsSelected = false,
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
            IsSearchable = false,
            NotSearchableReason = "Provider mapping for derived item properties is not available.",
            SourceProperties = sourceProperties
                .Where(property => property is not null)
                .Cast<ParsedItemProperty>()
                .ToImmutableArray(),
        };
    }

    private static IReadOnlyList<DerivedWeaponModifierEffect> CreateDerivedWeaponModifierEffects(
        IReadOnlyList<ResolvedSearchComponent> components)
    {
        return components
            .SelectMany(component => component.Sources.Count > 0
                ? component.Sources.Select(source => new DerivedWeaponModifierEffect
                {
                    ComponentId = source.ComponentId,
                    SourceModifierIndex = source.SourceModifierIndex,
                    ResolvedModifierId = source.ResolvedModifierId,
                    IsExactlyResolved = !string.IsNullOrWhiteSpace(source.ResolvedModifierId),
                    IsLocal = source.Locality == ModifierLocality.Local,
                    HasProvenStatAssociation = source.StatMappingProof is
                        ModifierStatMappingProofStatus.ProvenExact or
                        ModifierStatMappingProofStatus.WholeVector,
                    UsesPositionalFallback = source.StatMappingProof ==
                        ModifierStatMappingProofStatus.PositionalFallback,
                    ResolvedStatIds = source.ResolvedStatIds,
                    CanonicalNumericValues = source.CanonicalNumericValues.Count > 0
                        ? source.CanonicalNumericValues
                        : source.ObservedNumericValues,
                    ReviewedItemPropertySemantic = source.ReviewedItemPropertySemantic,
                })
                :
                [
                    new DerivedWeaponModifierEffect
                    {
                        ComponentId = component.ComponentId,
                        SourceModifierIndex = component.SourceModifierIndex,
                        ResolvedModifierId = component.ResolvedModifierId,
                        IsExactlyResolved = component.ResolutionStatus ==
                            ModifierCandidateResolutionStatus.Exact,
                        IsLocal = component.Locality == ModifierLocality.Local,
                        HasProvenStatAssociation = component.StatMappingProof is
                            ModifierStatMappingProofStatus.ProvenExact or
                            ModifierStatMappingProofStatus.WholeVector,
                        UsesPositionalFallback = component.StatMappingProof ==
                            ModifierStatMappingProofStatus.PositionalFallback,
                        ResolvedStatIds = component.ResolvedStatIds,
                        CanonicalNumericValues = component.CanonicalNumericValues.Count > 0
                            ? component.CanonicalNumericValues
                            : component.ObservedNumericValues,
                        ReviewedItemPropertySemantic = component.ReviewedItemPropertySemantic,
                    },
                ])
            .ToArray();
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

    private static TradeSearchBaseDraft CreateBaseDraft(
        ParsedItem parsedItem,
        ItemBaseResolutionResult? itemBaseResolution)
    {
        var parsedBaseName = TrimToNull(parsedItem.BaseType);
        var exactBaseId = itemBaseResolution?.Status == ItemBaseResolutionStatus.Unknown
            ? null
            : TrimToNull(itemBaseResolution?.ResolvedBaseId);
        var exactBaseName = itemBaseResolution?.Status == ItemBaseResolutionStatus.Unknown
            ? null
            : TrimToNull(itemBaseResolution?.ResolvedBaseName);
        var observedExactBaseName = exactBaseName ?? parsedBaseName;
        var category = ResolveCanonicalItemClass(parsedItem, itemBaseResolution);

        var observed = new ObservedBaseIdentity
        {
            Status = itemBaseResolution?.Status,
            ExactBaseId = exactBaseId,
            ExactBaseName = observedExactBaseName,
            Category = category,
        };
        var categoryCriterion = category is null
            ? null
            : new BaseSearchCriterion
            {
                Mode = BaseSearchMode.Category,
                Category = category,
            };
        var exactBaseCriterion = exactBaseName is null
            ? null
            : new BaseSearchCriterion
            {
                Mode = BaseSearchMode.ExactBase,
                Category = category,
                ExactBaseName = exactBaseName,
            };

        if (itemBaseResolution is null)
        {
            return new TradeSearchBaseDraft
            {
                Category = category,
                Observed = observed,
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = categoryCriterion,
                },
                ActiveCriterion = categoryCriterion,
            };
        }

        return new TradeSearchBaseDraft
        {
            Status = itemBaseResolution.Status,
            ResolvedBaseId = exactBaseId,
            ResolvedBaseName = exactBaseName,
            Category = category,
            Observed = observed,
            AvailableCriteria = new AvailableBaseSearchCriteria
            {
                Category = categoryCriterion,
                ExactBase = exactBaseCriterion,
            },
            ActiveCriterion = categoryCriterion ?? exactBaseCriterion,
        };
    }

    private static IEnumerable<ResolvedSearchComponent> CreateSearchComponents(
        ParsedItem parsedItem,
        ItemBaseResolutionResult? itemBaseResolution,
        IReadOnlyDictionary<int, ModifierCandidateResolutionResult> modifierResolutionByIndex,
        GameDataCatalog? catalog)
    {
        for (var modifierIndex = 0; modifierIndex < parsedItem.Modifiers.Count; modifierIndex++)
        {
            var modifier = parsedItem.Modifiers[modifierIndex];
            foreach (var component in CreateModifierComponents(
                         modifierIndex,
                         modifier,
                         modifierResolutionByIndex.GetValueOrDefault(modifierIndex),
                         itemBaseResolution,
                         parsedItem.TraditionalInfluences,
                         catalog))
            {
                yield return component;
            }
        }

        if (parsedItem.ImplicitModifiers.Count > 0 ||
            catalog is null ||
            itemBaseResolution?.Status is not (ItemBaseResolutionStatus.Exact or ItemBaseResolutionStatus.Probable) ||
            itemBaseResolution.MatchedItemBase?.ImplicitModifierIds.Count is not > 0)
        {
            yield break;
        }

        var implicitIndex = 0;
        foreach (var implicitModifierId in itemBaseResolution.MatchedItemBase.ImplicitModifierIds)
        {
            var implicitModifier = catalog.FindModifiersById(implicitModifierId).SingleOrDefault();
            if (implicitModifier is null ||
                !TryRenderModifierText(implicitModifier, catalog, out var text))
            {
                continue;
            }

            var statIds = StatIds(implicitModifier.Stats).ToArray();
            yield return new ResolvedSearchComponent
            {
                ComponentId = $"base-implicit:{implicitIndex}:{implicitModifier.Id}",
                SourceModifierIndex = -1,
                SourceLineIndex = 0,
                SourceComponentIndex = implicitIndex,
                OriginalText = text,
                CanonicalSignature = NormalizeComponentSignature([text]),
                ParsedKind = ParsedModifierKind.Implicit,
                GenerationType = implicitModifier.GenerationType,
                Locality = DetermineLocality(implicitModifier.Stats, catalog),
                IsBaseImplicit = true,
                GuaranteedExactBaseName = GuaranteedExactBaseName(
                    implicitModifier,
                    itemBaseResolution,
                    catalog),
                ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                ResolvedModifierId = TrimToNull(implicitModifier.Id),
                ResolvedModifierName = TrimToNull(implicitModifier.Name),
                ResolvedStatIds = statIds,
                StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
                IsSearchable = statIds.Length > 0,
                NotSearchableReason = statIds.Length == 0
                    ? "The base implicit modifier has no retained stat ids."
                    : null,
            };
            implicitIndex++;
        }
    }

    private static IEnumerable<ResolvedSearchComponent> CreateModifierComponents(
        int modifierIndex,
        ParsedModifier modifier,
        ModifierCandidateResolutionResult? resolution,
        ItemBaseResolutionResult? itemBaseResolution,
        IReadOnlyList<string> traditionalInfluences,
        GameDataCatalog? catalog)
    {
        var exactCandidate = resolution?.Status == ModifierCandidateResolutionStatus.Exact &&
            resolution.Candidates.Count == 1
            ? resolution.Candidates[0]
            : null;
        var valueLines = modifier.ValueLines
            .Select(TrimToNull)
            .Where(line => line is not null)
            .Select(line => line!)
            .ToArray();
        if (valueLines.Length == 0)
        {
            yield break;
        }

        if (exactCandidate is null)
        {
            if (TryResolveParsedBaseImplicit(
                    modifier,
                    valueLines,
                    itemBaseResolution,
                    catalog,
                    out var baseImplicitCandidate,
                    out var matchedLineStats))
            {
                for (var index = 0; index < valueLines.Length; index++)
                {
                    yield return CreateComponent(
                        modifierIndex,
                        modifier,
                        resolution,
                        baseImplicitCandidate,
                        [matchedLineStats[index]],
                        ModifierStatMappingProofStatus.ProvenExact,
                        sourceLineIndex: index,
                        sourceComponentIndex: index,
                        componentLines: [valueLines[index]],
                        itemBaseResolution,
                        traditionalInfluences,
                        catalog,
                        isBaseImplicit: true);
                }

                yield break;
            }

            for (var index = 0; index < valueLines.Length; index++)
            {
                yield return CreateComponent(
                    modifierIndex,
                    modifier,
                    resolution,
                    exactCandidate: null,
                    stats: [],
                    ModifierStatMappingProofStatus.Unknown,
                    sourceLineIndex: index,
                    sourceComponentIndex: index,
                    componentLines: [valueLines[index]],
                    itemBaseResolution,
                    traditionalInfluences,
                    catalog);
            }

            yield break;
        }

        if (TryMatchStatsToParsedLines(
                exactCandidate,
                valueLines,
                catalog,
                out var exactMatchedLineStats))
        {
            for (var index = 0; index < valueLines.Length; index++)
            {
                yield return CreateComponent(
                    modifierIndex,
                    modifier,
                    resolution,
                    exactCandidate,
                    [exactMatchedLineStats[index]],
                    ModifierStatMappingProofStatus.ProvenExact,
                    sourceLineIndex: index,
                    sourceComponentIndex: index,
                    componentLines: [valueLines[index]],
                    itemBaseResolution,
                    traditionalInfluences,
                    catalog);
            }

            yield break;
        }

        var orderedStats = exactCandidate.Stats
            .OrderBy(stat => stat.Index)
            .ToArray();
        if (valueLines.Length > 1 && orderedStats.Length >= valueLines.Length)
        {
            for (var index = 0; index < valueLines.Length; index++)
            {
                yield return CreateComponent(
                    modifierIndex,
                    modifier,
                    resolution,
                    exactCandidate,
                    [orderedStats[index]],
                    ModifierStatMappingProofStatus.PositionalFallback,
                    sourceLineIndex: index,
                    sourceComponentIndex: index,
                    componentLines: [valueLines[index]],
                    itemBaseResolution,
                    traditionalInfluences,
                    catalog);
            }

            yield break;
        }

        yield return CreateComponent(
            modifierIndex,
            modifier,
            resolution,
            exactCandidate,
            orderedStats,
            ModifierStatMappingProofStatus.WholeVector,
            sourceLineIndex: valueLines.Length == 1 ? 0 : -1,
            sourceComponentIndex: 0,
            componentLines: valueLines,
            itemBaseResolution,
            traditionalInfluences,
            catalog);
    }

    private static ResolvedSearchComponent CreateComponent(
        int modifierIndex,
        ParsedModifier modifier,
        ModifierCandidateResolutionResult? resolution,
        ModifierDefinition? exactCandidate,
        IReadOnlyList<ModifierStat> stats,
        ModifierStatMappingProofStatus statMappingProof,
        int sourceLineIndex,
        int sourceComponentIndex,
        IReadOnlyList<string> componentLines,
        ItemBaseResolutionResult? itemBaseResolution,
        IReadOnlyList<string> traditionalInfluences,
        GameDataCatalog? catalog,
        bool isBaseImplicit = false)
    {
        var statIds = StatIds(stats).ToArray();
        var isSearchable = exactCandidate is not null && statIds.Length > 0;
        var boundDefault = ModifierBoundDefaults.Create(exactCandidate, stats, componentLines, catalog);
        var hasUnscalableValue = sourceLineIndex >= 0 &&
            modifier.Effects.ElementAtOrDefault(sourceLineIndex)?.HasUnscalableValue == true;
        var supportsValueBounds = !hasUnscalableValue && boundDefault.IsSupported;
        var valueBoundShape = hasUnscalableValue
            ? ModifierBoundShape.PresenceOnly
            : boundDefault.Shape;

        var component = new ResolvedSearchComponent
        {
            ComponentId = $"modifier:{modifierIndex}:{sourceComponentIndex}",
            SourceModifierIndex = modifierIndex,
            SourceLineIndex = sourceLineIndex,
            SourceComponentIndex = sourceComponentIndex,
            OriginalText = string.Join(Environment.NewLine, componentLines),
            CanonicalSignature = NormalizeComponentSignature(componentLines),
            ParsedKind = modifier.Kind,
            ImplicitOrigin = modifier.ImplicitOrigin,
            GenerationType = resolution?.GenerationType,
            Locality = exactCandidate is null
                ? ModifierLocality.Unknown
                : DetermineLocality(stats, catalog) is ModifierLocality.Unknown
                    ? resolution?.Locality ?? ModifierLocality.Unknown
                    : DetermineLocality(stats, catalog),
            StatMappingProof = statMappingProof,
            ParsedModifierName = TrimToNull(modifier.Name ?? resolution?.ParsedModifierName),
            CategoryText = TrimToNull(modifier.CategoryText),
            Tier = modifier.Tier,
            Rank = modifier.Rank,
            IsCrafted = modifier.IsCrafted,
            IsFractured = modifier.IsFractured,
            IsVeiled = modifier.IsVeiled,
            IsBaseImplicit = isBaseImplicit,
            GuaranteedExactBaseName = isBaseImplicit &&
                !supportsValueBounds &&
                exactCandidate is not null &&
                catalog is not null
                ? GuaranteedExactBaseName(exactCandidate, itemBaseResolution, catalog)
                : null,
            ResolutionStatus = resolution?.Status,
            ResolvedModifierId = TrimToNull(exactCandidate?.Id),
            ResolvedModifierName = TrimToNull(exactCandidate?.Name),
            ResolvedStatIds = statIds,
            IsSearchable = isSearchable,
            NotSearchableReason = isSearchable
                ? null
                : exactCandidate is null
                    ? "The source modifier did not resolve to one exact GameData modifier."
                    : "The resolved component has no retained stat ids.",
            SupportsValueBounds = supportsValueBounds,
            ValueBoundsUnsupportedReason = hasUnscalableValue
                ? "The copied modifier is a presence-only value and has no numeric Trade bound."
                : boundDefault.UnsupportedReason,
            ValueBoundShape = valueBoundShape,
            ObservedNumericValues = hasUnscalableValue ? [] : boundDefault.ObservedValues,
            CanonicalNumericValues = valueBoundShape switch
            {
                ModifierBoundShape.Scalar => [boundDefault.ObservedCanonicalValue],
                ModifierBoundShape.ArithmeticMeanRange => boundDefault.ObservedValues,
                _ => [],
            },
            ValueBoundTranslationHandlers = boundDefault.TranslationHandlers,
            ValueBoundTranslationIdentity = boundDefault.TranslationIdentity,
            DefaultBoundDirection = boundDefault.Direction,
            RequestedMinimum = supportsValueBounds && boundDefault.Direction == ModifierBoundDirection.Minimum
                ? boundDefault.ObservedCanonicalValue
                : null,
            RequestedMaximum = supportsValueBounds && boundDefault.Direction == ModifierBoundDirection.Maximum
                ? boundDefault.ObservedCanonicalValue
                : null,
            IsSelected = false,
        };

        component = component with
        {
            ReviewedItemPropertySemantic = FindReviewedItemPropertySemantic(component, catalog),
        };

        return exactCandidate is null || catalog is null
            ? component
            : component with
            {
                ProviderDomainEvidence = ModifierProviderDomainEvidenceResolver.Resolve(
                    component,
                    exactCandidate,
                    componentLines,
                    itemBaseResolution,
                    traditionalInfluences,
                    catalog),
            };
    }

    private static ItemPropertySemanticDescriptor? FindReviewedItemPropertySemantic(
        ResolvedSearchComponent component,
        GameDataCatalog? catalog)
    {
        if (catalog is null ||
            component.ResolutionStatus != ModifierCandidateResolutionStatus.Exact ||
            component.Locality != ModifierLocality.Local ||
            component.StatMappingProof is not (
                ModifierStatMappingProofStatus.ProvenExact or
                ModifierStatMappingProofStatus.WholeVector) ||
            component.ResolvedStatIds.Count == 0)
        {
            return null;
        }

        var descriptor = catalog.FindItemPropertySemanticByOrderedStatVector(component.ResolvedStatIds);
        return descriptor?.Applicability == ItemPropertyApplicability.UnconditionalDisplayedLocal
            ? descriptor
            : null;
    }

    private static string? GuaranteedExactBaseName(
        ModifierDefinition modifier,
        ItemBaseResolutionResult? itemBaseResolution,
        GameDataCatalog catalog)
    {
        var modifierId = TrimToNull(modifier.Id);
        if (modifierId is null)
        {
            return null;
        }

        var currentItemClass = itemBaseResolution?.MatchedItemBase?.ItemClass;
        var compatibleBases = catalog.ItemBases
            .Where(itemBase => itemBase.ImplicitModifierIds.Any(implicitModifierId =>
                string.Equals(implicitModifierId?.Trim(), modifierId, StringComparison.OrdinalIgnoreCase)))
            .Where(itemBase => string.IsNullOrWhiteSpace(currentItemClass) ||
                ItemBaseClassCompatibility.AreCompatible(currentItemClass, itemBase.ItemClass))
            .Select(itemBase => itemBase.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return compatibleBases.Length == 1 ? compatibleBases[0] : null;
    }

    private static bool TryResolveParsedBaseImplicit(
        ParsedModifier modifier,
        IReadOnlyList<string> valueLines,
        ItemBaseResolutionResult? itemBaseResolution,
        GameDataCatalog? catalog,
        out ModifierDefinition candidate,
        out IReadOnlyList<ModifierStat> matchedLineStats)
    {
        candidate = null!;
        matchedLineStats = [];
        if (modifier.Kind != ParsedModifierKind.Implicit ||
            catalog is null ||
            valueLines.Count == 0 ||
            itemBaseResolution?.Status is not (ItemBaseResolutionStatus.Exact or ItemBaseResolutionStatus.Probable) ||
            itemBaseResolution.MatchedItemBase?.ImplicitModifierIds.Count is not > 0)
        {
            return false;
        }

        var matches = itemBaseResolution.MatchedItemBase.ImplicitModifierIds
            .Select(id => catalog.FindModifiersById(id).SingleOrDefault())
            .Where(modifierDefinition => modifierDefinition is not null)
            .Select(modifierDefinition => modifierDefinition!)
            .Select(modifierDefinition => new
            {
                Candidate = modifierDefinition,
                IsMatch = TryMatchStatsToParsedLines(
                    modifierDefinition,
                    valueLines,
                    catalog,
                    out var stats),
                Stats = stats,
            })
            .Where(match => match.IsMatch)
            .ToArray();

        if (matches.Length != 1)
        {
            return false;
        }

        candidate = matches[0].Candidate;
        matchedLineStats = matches[0].Stats;
        return true;
    }

    private static bool TryMatchStatsToParsedLines(
        ModifierDefinition candidate,
        IReadOnlyList<string> valueLines,
        GameDataCatalog? catalog,
        out IReadOnlyList<ModifierStat> matchedLineStats)
    {
        matchedLineStats = [];
        if (catalog is null || valueLines.Count == 0)
        {
            return false;
        }

        var stats = candidate.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
            .OrderBy(stat => stat.Index)
            .ToArray();
        if (stats.Length < valueLines.Count)
        {
            return false;
        }

        var matcher = new ModifierTextSignatureMatcher();
        var allowContainingTranslationProof = stats.Length > 1;
        var matchedStats = new List<ModifierStat>();
        foreach (var valueLine in valueLines)
        {
            var lineMatches = stats
                .Where(stat => !matchedStats.Any(matched => EqualsStat(matched, stat)))
                .Where(stat => IsProvenLineStatAssociation(
                    candidate,
                    stat,
                    valueLine,
                    catalog,
                    matcher,
                    allowContainingTranslationProof))
                .ToArray();
            if (lineMatches.Length != 1)
            {
                return false;
            }

            matchedStats.Add(lineMatches[0]);
        }

        matchedLineStats = matchedStats;
        return true;
    }

    private static bool IsProvenLineStatAssociation(
        ModifierDefinition candidate,
        ModifierStat stat,
        string valueLine,
        GameDataCatalog catalog,
        ModifierTextSignatureMatcher matcher,
        bool allowContainingTranslationProof)
    {
        var exactGroupMatch = matcher.Match(
            candidate with { Stats = [stat] },
            catalog,
            [valueLine]);
        if (exactGroupMatch.Outcome == ModifierTextSignatureMatchOutcome.Match)
        {
            return true;
        }

        if (!allowContainingTranslationProof)
        {
            return false;
        }

        var compatibleBranch = ModifierBoundDefaults.Create(
            candidate,
            [stat],
            [valueLine],
            catalog);
        return compatibleBranch.IsSupported &&
            compatibleBranch.TranslationIdentity is not null;
    }

    private static bool EqualsStat(ModifierStat left, ModifierStat right)
    {
        return left.Index == right.Index &&
            string.Equals(left.StatId, right.StatId, StringComparison.Ordinal);
    }

    private static IEnumerable<string> StatIds(IEnumerable<ModifierStat> stats)
    {
        return stats
            .Select(stat => TrimToNull(stat.StatId))
            .Where(statId => statId is not null)
            .Select(statId => statId!);
    }

    private static ModifierLocality DetermineLocality(
        IReadOnlyList<ModifierStat> stats,
        GameDataCatalog? catalog)
    {
        if (catalog is null || stats.Count == 0)
        {
            return ModifierLocality.Unknown;
        }

        var localCount = 0;
        var globalCount = 0;
        foreach (var modifierStat in stats)
        {
            var stat = catalog.FindStatsById(modifierStat.StatId).SingleOrDefault();
            if (stat is null)
            {
                return ModifierLocality.Unknown;
            }

            if (stat.IsLocal)
            {
                localCount++;
            }
            else
            {
                globalCount++;
            }
        }

        return (localCount, globalCount) switch
        {
            (> 0, 0) => ModifierLocality.Local,
            (0, > 0) => ModifierLocality.Global,
            _ => ModifierLocality.Unknown,
        };
    }

    private static string NormalizeComponentSignature(IReadOnlyList<string> lines)
    {
        return string.Join(
            "\n",
            lines.Select(ModifierTextSignatureNormalizer.NormalizeLine));
    }

    private static bool TryRenderModifierText(
        ModifierDefinition modifier,
        GameDataCatalog catalog,
        out string text)
    {
        text = string.Empty;
        var statIds = StatIds(modifier.Stats.OrderBy(stat => stat.Index)).ToArray();
        if (statIds.Length == 0)
        {
            return false;
        }

        var translation = catalog.FindStatTranslationsByStatIdGroup(statIds).SingleOrDefault();
        var variant = translation?.Variants.FirstOrDefault();
        if (variant is null)
        {
            return false;
        }

        var lines = variant.FormatLines
            .Select(line => RenderFormatLine(line, variant.ValueFormats))
            .Select(TrimToNull)
            .Where(line => line is not null)
            .Select(line => line!)
            .ToArray();
        if (lines.Length == 0)
        {
            return false;
        }

        text = string.Join(Environment.NewLine, lines);
        return true;
    }

    private static string RenderFormatLine(
        string line,
        IReadOnlyList<string> valueFormats)
    {
        var rendered = line;
        for (var index = 0; index < valueFormats.Count; index++)
        {
            var replacement = valueFormats[index] switch
            {
                "+#" => "+#",
                "#" => "#",
                "ignore" => string.Empty,
                _ => "#",
            };
            rendered = rendered.Replace($"{{{index}}}", replacement, StringComparison.Ordinal);
        }

        return rendered;
    }

    private static string? ResolveCanonicalItemClass(
        ParsedItem parsedItem,
        ItemBaseResolutionResult? itemBaseResolution)
    {
        var catalogIdentity = CanonicalItemClassIdentityResolver.Resolve(
            itemBaseResolution?.MatchedItemBase?.ItemClass);
        if (catalogIdentity.IsSupported)
        {
            return catalogIdentity.CanonicalItemClass;
        }

        var parsedIdentity = itemBaseResolution?.ItemClassIdentity ??
            CanonicalItemClassIdentityResolver.Resolve(parsedItem.ItemClass);
        return parsedIdentity.IsSupported ? parsedIdentity.CanonicalItemClass : null;
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
