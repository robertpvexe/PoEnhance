using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

public sealed partial class TradeSearchDraftMapper
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
        var uniqueItemResolution = gameDataCatalog is null
            ? null
            : new ParsedUniqueItemResolver().Resolve(parsedItem, gameDataCatalog, itemBaseResolution);
        var aggregation = CanonicalModifierEffectAggregator.Aggregate(
            CreateSearchComponents(
                    parsedItem,
                    itemBaseResolution,
                    modifierResolutionByIndex,
                    gameDataCatalog,
                    uniqueItemResolution)
                .ToArray());
        var derivedPropertyCalculator = new DerivedWeaponPropertyCalculator();
        var derivedWeaponProperties = derivedPropertyCalculator.CalculateQ20(
            parsedItem,
            itemBaseResolution?.MatchedItemBase,
            CreateDerivedWeaponModifierEffects(aggregation.Components));
        var derivedDefensiveProperties = derivedPropertyCalculator.CalculateDefensiveQ20(
            parsedItem,
            itemBaseResolution?.MatchedItemBase,
            DerivedWeaponModifierEffectProjector.ProjectSourcesIndependently(
                aggregation.Components));
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
            ItemVariantCriteria = new TradeItemVariantCriteria
            {
                Foulborn = uniqueItemResolution?.Status == UniqueItemResolutionStatus.ExactIdentity
                    ? uniqueItemResolution.IsFoulborn ? TradeTriState.Yes : TradeTriState.No
                    : TradeTriState.Auto,
            },
            UniqueItemResolution = uniqueItemResolution,
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
        return DerivedWeaponModifierEffectProjector.Project(components);
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
        GameDataCatalog? catalog,
        UniqueItemResolutionResult? uniqueItemResolution)
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
                         catalog,
                         uniqueItemResolution?.ModifierBlocks.SingleOrDefault(block =>
                             block.ParsedModifierIndex == modifierIndex)))
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
        GameDataCatalog? catalog,
        UniqueModifierBlockResolution? uniqueBlockResolution)
    {
        var exactCandidate = resolution?.Status == ModifierCandidateResolutionStatus.Exact &&
            (resolution.Candidates.Count == 1 || resolution.IsEquivalentSourceSet)
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

        // A row recovered from identity-bound Unique source evidence carries the same mechanics as a
        // Unique-labelled row, so it takes the Unique source component path even though the client
        // labelled it with another domain.
        if (modifier.Kind == ParsedModifierKind.Unique ||
            uniqueBlockResolution?.HasRecoveredUniqueSourceSemantics == true)
        {
            if (TryExpandUniqueBlockIntoIndependentComponents(
                    modifierIndex,
                    modifier,
                    resolution,
                    exactCandidate,
                    valueLines,
                    itemBaseResolution,
                    traditionalInfluences,
                    catalog,
                    uniqueBlockResolution,
                    out var expandedComponents))
            {
                foreach (var component in expandedComponents)
                {
                    yield return component;
                }

                yield break;
            }

            yield return CreateComponent(
                modifierIndex,
                modifier,
                resolution,
                exactCandidate,
                stats: [],
                ModifierStatMappingProofStatus.WholeVector,
                sourceLineIndex: valueLines.Length == 1 ? 0 : -1,
                sourceComponentIndex: 0,
                componentLines: valueLines,
                itemBaseResolution,
                traditionalInfluences,
                catalog,
                uniqueBlockResolution: uniqueBlockResolution);
            yield break;
        }

        if (exactCandidate is null)
        {
            var baseImplicitProvenance = CreateBaseImplicitProvenance(
                resolution?.BaseImplicitRecognition);
            if (TryResolveRecognizedBaseImplicit(
                    modifier,
                    valueLines,
                    resolution?.BaseImplicitRecognition,
                    out var recognizedBaseImplicitCandidate,
                    out var recognizedLineStats,
                    out var recognizedEffectCatalog))
            {
                for (var index = 0; index < valueLines.Length; index++)
                {
                    yield return CreateComponent(
                        modifierIndex,
                        modifier,
                        resolution,
                        recognizedBaseImplicitCandidate,
                        recognizedLineStats[index],
                        ModifierStatMappingProofStatus.ProvenExact,
                        sourceLineIndex: index,
                        sourceComponentIndex: index,
                        componentLines: [valueLines[index]],
                        itemBaseResolution,
                        traditionalInfluences,
                        recognizedEffectCatalog,
                        isBaseImplicit: true,
                        baseImplicitProvenance: baseImplicitProvenance,
                        baseIdentityCatalog: catalog);
                }

                yield break;
            }

            var recognitionIsAmbiguous = resolution?.BaseImplicitRecognition?.Status ==
                BaseImplicitRecognitionStatus.Ambiguous;
            if (!recognitionIsAmbiguous && TryResolveParsedBaseImplicit(
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
                        matchedLineStats[index],
                        ModifierStatMappingProofStatus.ProvenExact,
                        sourceLineIndex: index,
                        sourceComponentIndex: index,
                        componentLines: [valueLines[index]],
                        itemBaseResolution,
                        traditionalInfluences,
                        catalog,
                        isBaseImplicit: true,
                        baseImplicitProvenance: baseImplicitProvenance);
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
                    catalog,
                    isBaseImplicit: baseImplicitProvenance is not null,
                    baseImplicitProvenance: baseImplicitProvenance);
            }

            yield break;
        }

        if (TryMatchStatsToParsedLines(
                exactCandidate,
                valueLines,
                catalog,
                preserveSingleLineProofSemantics: true,
                out var exactMatchedLineStatGroups))
        {
            for (var index = 0; index < valueLines.Length; index++)
            {
                yield return CreateComponent(
                    modifierIndex,
                    modifier,
                    resolution,
                    exactCandidate,
                    exactMatchedLineStatGroups[index],
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
        bool isBaseImplicit = false,
        SearchComponentBaseImplicitProvenance? baseImplicitProvenance = null,
        GameDataCatalog? baseIdentityCatalog = null,
        UniqueModifierBlockResolution? uniqueBlockResolution = null)
    {
        isBaseImplicit = isBaseImplicit ||
            modifier.Kind == ParsedModifierKind.Implicit &&
            modifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Unspecified;

        // True when this component's mechanics come from Unique source-block evidence rather than a
        // normal modifier candidate, whether the client labelled the row as a Unique modifier or the
        // row was recovered from the resolved Unique identity.
        var usesUniqueSourceMechanics = modifier.Kind == ParsedModifierKind.Unique ||
            uniqueBlockResolution?.HasRecoveredUniqueSourceSemantics == true;
        var uniqueModifierCandidates = ResolveUniqueModifierCandidates(uniqueBlockResolution, catalog);
        var uniqueBoundCandidate = ResolveUniqueBoundCandidate(uniqueModifierCandidates);
        var boundCandidate = exactCandidate ?? uniqueBoundCandidate;
        var boundStats = stats.Count > 0
            ? stats
            : uniqueBoundCandidate?.Stats
                .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
                .OrderBy(stat => stat.Index)
                .ToArray() ?? [];
        var statIds = exactCandidate is null && uniqueBlockResolution?.IsResolved == true
            ? uniqueBlockResolution.StatIds.ToArray()
            : StatIds(stats).ToArray();
        var statLocalities = uniqueBlockResolution?.IsResolved == true
            ? uniqueBlockResolution.StatLocalities.ToArray()
            : ResolveStatLocalities(statIds, catalog);
        var providerSearchEvidence = ResolveUniqueProviderSearchSignatures(
            uniqueBlockResolution,
            uniqueModifierCandidates,
            componentLines,
            catalog);
        var providerSearchSignatures = providerSearchEvidence.Signatures;
        var isSearchable = (exactCandidate is not null || uniqueBlockResolution?.IsResolved == true ||
                uniqueBlockResolution?.CatalogBlocks.Count > 0) &&
            (statIds.Length > 0 || uniqueBlockResolution?.CatalogBlocks.Count > 0);
        var translationRecognition = resolution?.TranslationRecognition;
        var boundDefault = ModifierBoundDefaults.Create(
            boundCandidate,
            boundStats,
            componentLines,
            catalog,
            exactCandidate is null ? null : translationRecognition);
        var hasUnscalableValue = sourceLineIndex >= 0 &&
            modifier.Effects.ElementAtOrDefault(sourceLineIndex)?.HasUnscalableValue == true;
        var hasProvenGeneratedTextualOptionRange = hasUnscalableValue &&
            uniqueBlockResolution is
            {
                IsResolved: true,
                SourceSemantics: UniqueModifierSourceSemantics.GeneratedCandidate,
                CandidatePoolMembershipIds.Count: > 0,
                TextualOptionRangeAnnotations.Count: > 0,
            } &&
            modifier.Effects.ElementAtOrDefault(sourceLineIndex)?.TextualOptionRange is not null;
        var treatAsUnscalablePresence = hasUnscalableValue &&
            !hasProvenGeneratedTextualOptionRange;
        var sourceFixedQueryValue = treatAsUnscalablePresence &&
            usesUniqueSourceMechanics &&
            uniqueBlockResolution is
            {
                IsResolved: true,
                SourceSemantics: UniqueModifierSourceSemantics.Fixed,
            } &&
            boundDefault.IsSupported &&
            boundDefault.Shape == ModifierBoundShape.Scalar &&
            boundDefault.ObservedValues.Count == 1 &&
            boundStats.Count > 0 &&
            boundStats.All(stat => stat.MinValue.HasValue &&
                stat.MaxValue.HasValue &&
                stat.MinValue == stat.MaxValue)
                ? boundDefault.ObservedCanonicalValue
                : (decimal?)null;
        // Identity-fixed only (e.g. Level 10 Spell Echo). Multi-line Unique seed evidence is
        // editable exact-initialized bounds, not FixedQueryValue.
        var fixedQueryValue = sourceFixedQueryValue;
        var exactInitializedEditableQueryValue = fixedQueryValue is null &&
            !treatAsUnscalablePresence &&
            !boundDefault.IsSupported
                ? providerSearchEvidence.ExactInitializedEditableQueryValue
                : null;
        var providerOnlyUniqueValues = usesUniqueSourceMechanics &&
            (boundCandidate is null || boundDefault.Shape == ModifierBoundShape.Unsupported) &&
            componentLines.Count == 1
                ? ModifierBoundDefaults.ExtractObservedValues(componentLines[0])
                : [];
        var hasProviderOnlyUniqueScalar = !treatAsUnscalablePresence &&
            providerOnlyUniqueValues.Count == 1;
        var supportsValueBounds = !treatAsUnscalablePresence &&
            (boundDefault.IsSupported ||
                hasProviderOnlyUniqueScalar ||
                exactInitializedEditableQueryValue.HasValue);
        var providerOnlyUniqueDirection = ModifierBoundDirection.Minimum;
        var valueBoundShape = fixedQueryValue.HasValue || exactInitializedEditableQueryValue.HasValue
            ? ModifierBoundShape.Scalar
            : treatAsUnscalablePresence
            ? ModifierBoundShape.PresenceOnly
            : hasProviderOnlyUniqueScalar
                ? ModifierBoundShape.Scalar
                : boundCandidate is null && usesUniqueSourceMechanics && providerOnlyUniqueValues.Count == 0
                    ? ModifierBoundShape.PresenceOnly
                : boundDefault.Shape == ModifierBoundShape.Unsupported &&
                    usesUniqueSourceMechanics &&
                    componentLines.Count == 1 &&
                    providerOnlyUniqueValues.Count == 0 &&
                    !componentLines[0].Any(char.IsDigit)
                    ? ModifierBoundShape.PresenceOnly
            : boundDefault.Shape;
        var observedNumericValues = fixedQueryValue.HasValue
            ? [fixedQueryValue.Value]
            : exactInitializedEditableQueryValue.HasValue
            ? [exactInitializedEditableQueryValue.Value]
            : hasProviderOnlyUniqueScalar ||
            boundCandidate is null && usesUniqueSourceMechanics
            ? providerOnlyUniqueValues
            : boundDefault.ObservedValues;
        var canonicalNumericValues = fixedQueryValue.HasValue
            ? [fixedQueryValue.Value]
            : exactInitializedEditableQueryValue.HasValue
            ? [exactInitializedEditableQueryValue.Value]
            : hasProviderOnlyUniqueScalar
            ? providerOnlyUniqueValues
            : valueBoundShape switch
            {
                ModifierBoundShape.Scalar => [boundDefault.ObservedCanonicalValue],
                ModifierBoundShape.ArithmeticMeanRange => boundDefault.ObservedValues,
                _ => [],
            };
        var providerFallbackNumericValues = valueBoundShape == ModifierBoundShape.PresenceOnly &&
            boundStats.Count > 0 &&
            boundStats.All(stat => stat.MinValue.HasValue &&
                stat.MaxValue.HasValue &&
                stat.MinValue == stat.MaxValue)
                ? boundStats.Select(stat => stat.MinValue!.Value).ToArray()
                : [];
        var defaultBoundDirection = hasProviderOnlyUniqueScalar
            ? providerOnlyUniqueDirection
            : boundDefault.Direction;
        var observedCanonicalValue = exactInitializedEditableQueryValue ??
            (hasProviderOnlyUniqueScalar
                ? providerOnlyUniqueValues[0]
                : boundDefault.ObservedCanonicalValue);

        var isEquivalentSourceSet = resolution?.IsEquivalentSourceSet == true ||
            uniqueBlockResolution?.IsEquivalentSourceSet == true;
        var component = new ResolvedSearchComponent
        {
            ComponentId = $"modifier:{modifierIndex}:{sourceComponentIndex}",
            SourceModifierIndex = modifierIndex,
            SourceLineIndex = sourceLineIndex,
            SourceComponentIndex = sourceComponentIndex,
            OriginalText = string.Join(Environment.NewLine, componentLines),
            RawCopiedText = sourceLineIndex >= 0
                ? modifier.Effects.ElementAtOrDefault(sourceLineIndex)?.RawText ??
                    string.Join(Environment.NewLine, componentLines)
                : string.Join(Environment.NewLine, modifier.Effects.Select(effect => effect.RawText)),
            PresentationText = uniqueBlockResolution?.PresentationLines.Count > 0
                ? string.Join(Environment.NewLine, uniqueBlockResolution.PresentationLines)
                : null,
            CanonicalSignature = translationRecognition?.CanonicalSignature.Lines.Count > 0
                ? string.Join("\n", translationRecognition.CanonicalSignature.Lines)
                : NormalizeComponentSignature(componentLines),
            ParsedKind = modifier.Kind,
            ImplicitOrigin = modifier.ImplicitOrigin,
            UniqueOrigin = modifier.UniqueOrigin,
            GenerationType = modifier.Kind == ParsedModifierKind.Enchantment
                ? ModifierGenerationType.Enchantment
                : exactCandidate?.GenerationType ??
                resolution?.GenerationType ??
                CommonGenerationType(uniqueModifierCandidates),
            Locality = uniqueBlockResolution?.IsResolved == true
                ? AggregateLocality(statLocalities)
                : exactCandidate is null
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
            IsVeiled = modifier.IsUnrevealedVeiledPlaceholder,
            IsUnveiled = modifier.IsNamedUnveiled || IsUnveiledDomain(exactCandidate?.Domain),
            IsBaseImplicit = isBaseImplicit,
            IsEquivalentSourceSet = isEquivalentSourceSet,
            BaseImplicitProvenance = baseImplicitProvenance,
            GuaranteedExactBaseName = isBaseImplicit &&
                baseImplicitProvenance?.RecognitionStatus !=
                    BaseImplicitRecognitionStatus.HistoricalExact &&
                !supportsValueBounds &&
                exactCandidate is not null &&
                (baseIdentityCatalog ?? catalog) is { } exactBaseCatalog
                ? GuaranteedExactBaseName(exactCandidate, itemBaseResolution, exactBaseCatalog)
                : null,
            ResolutionStatus = uniqueBlockResolution?.IsResolved == true
                ? ModifierCandidateResolutionStatus.Exact
                : exactCandidate is not null &&
                (isBaseImplicit ||
                    baseImplicitProvenance?.RecognitionStatus is (
                        BaseImplicitRecognitionStatus.CurrentExact or
                        BaseImplicitRecognitionStatus.HistoricalExact))
                ? ModifierCandidateResolutionStatus.Exact
                : resolution?.Status,
            ResolvedModifierId = uniqueBlockResolution?.ModifierIds.Count == 1
                ? uniqueBlockResolution.ModifierIds[0]
                : isEquivalentSourceSet ? null : TrimToNull(exactCandidate?.Id),
            ResolvedModifierName = TrimToNull(exactCandidate?.Name) ?? CommonModifierName(uniqueModifierCandidates),
            ResolvedStatIds = statIds,
            ResolvedStatLocalities = statLocalities,
            ProviderSearchSignatures = providerSearchSignatures,
            UniqueCatalogBlockIds = uniqueBlockResolution?.CatalogBlocks
                .Select(block => block.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToArray() ?? [],
            UniqueSourceSemantics = uniqueBlockResolution?.SourceSemantics ??
                UniqueModifierSourceSemantics.Fixed,
            UniqueCandidatePoolMembershipIds =
                uniqueBlockResolution?.CandidatePoolMembershipIds ?? [],
            UniqueOptionChoiceMemberships =
                uniqueBlockResolution?.OptionChoiceMemberships ?? [],
            UniqueTextualOptionRangeAnnotations =
                uniqueBlockResolution?.TextualOptionRangeAnnotations ?? [],
            UniqueFoulbornRelationshipIds = uniqueBlockResolution?.FoulbornRelationshipIds ?? [],
            UniqueNormalCounterpartModifierIds = uniqueBlockResolution?.NormalCounterpartModifierIds ?? [],
            UniqueSourceObservationIds = uniqueBlockResolution?.SourceObservationIds ?? [],
            UniqueResolutionDiagnosticCode = uniqueBlockResolution?.DiagnosticCode,
            UsesIdentityBoundUniqueRecovery =
                uniqueBlockResolution?.HasRecoveredUniqueSourceSemantics == true,
            RecoveredSourceKind = uniqueBlockResolution?.RecoveredSourceKind,
            RecoveredSourceUniqueOrigin = uniqueBlockResolution?.RecoveredSourceUniqueOrigin,
            IsSearchable = isSearchable,
            NotSearchableReason = isSearchable
                ? null
                : usesUniqueSourceMechanics && uniqueBlockResolution is { IsResolved: false }
                    ? uniqueBlockResolution.Diagnostic
                : exactCandidate is null
                    ? "The source modifier did not resolve to one exact GameData modifier."
                    : "The resolved component has no retained stat ids.",
            SupportsValueBounds = supportsValueBounds,
            ValueBoundsUnsupportedReason = fixedQueryValue.HasValue
                ? "The source proves a fixed numeric query value, but it is not user-editable."
                : exactInitializedEditableQueryValue.HasValue
                    ? null
                : treatAsUnscalablePresence
                    ? "The copied modifier is a presence-only value and has no numeric Trade bound."
                : hasProviderOnlyUniqueScalar
                    ? null
                : boundCandidate is null && usesUniqueSourceMechanics &&
                    !hasProviderOnlyUniqueScalar
                    ? providerOnlyUniqueValues.Count > 1
                        ? "The provider-owned Unique modifier has multiple numeric values without a proven scalar projection."
                        : "Official Trade must prove whether this Unique modifier is presence-only."
                : boundDefault.UnsupportedReason,
            ValueBoundShape = valueBoundShape,
            ObservedNumericValues = treatAsUnscalablePresence &&
                !fixedQueryValue.HasValue &&
                !exactInitializedEditableQueryValue.HasValue
                ? []
                : observedNumericValues,
            OriginalSourceRollRanges = treatAsUnscalablePresence
                ? []
                : ModifierBoundDefaults.ExtractOriginalSourceRollRanges(componentLines),
            CanonicalNumericValues = fixedQueryValue.HasValue
                ? [fixedQueryValue.Value]
                : exactInitializedEditableQueryValue.HasValue
                ? [exactInitializedEditableQueryValue.Value]
                : treatAsUnscalablePresence ? [] : canonicalNumericValues,
            FixedQueryValue = fixedQueryValue,
            ProviderFallbackNumericValues = treatAsUnscalablePresence ? [] : providerFallbackNumericValues,
            ProviderCanonicalSignature = boundDefault.ProviderCanonicalSignature,
            ValueBoundTranslationHandlers = boundDefault.TranslationHandlers,
            ValueBoundTranslationIdentity = boundDefault.TranslationIdentity,
            TranslationRecognition = translationRecognition,
            DefaultBoundDirection = defaultBoundDirection,
            RequestedMinimum = exactInitializedEditableQueryValue.HasValue
                ? exactInitializedEditableQueryValue
                : supportsValueBounds && defaultBoundDirection == ModifierBoundDirection.Minimum
                    ? observedCanonicalValue
                    : null,
            RequestedMaximum = exactInitializedEditableQueryValue.HasValue
                ? exactInitializedEditableQueryValue
                : supportsValueBounds && defaultBoundDirection == ModifierBoundDirection.Maximum
                    ? observedCanonicalValue
                    : null,
            IsSelected = false,
        };

        component = component with
        {
            ReviewedItemPropertySemantic = FindReviewedItemPropertySemantic(component, catalog),
        };

        var provenanceCandidates = uniqueModifierCandidates.Count > 0
            ? uniqueModifierCandidates
            : resolution?.IsEquivalentSourceSet == true
                ? resolution.Candidates
                : [];
        if (provenanceCandidates.Count > 0)
        {
            component = component with
            {
                Sources = provenanceCandidates
                    .Select(candidate => CanonicalModifierEffectAggregator.CreateSourceProvenance(
                        component with
                        {
                            ResolvedModifierId = TrimToNull(candidate.Id),
                            ResolvedModifierName = TrimToNull(candidate.Name),
                        }))
                    .ToArray(),
            };
        }

        var providerEvidenceCandidates = uniqueModifierCandidates.Count > 0
            ? uniqueModifierCandidates
            : exactCandidate is null
                ? []
                : isEquivalentSourceSet
                    ? resolution!.Candidates
                    : [exactCandidate];
        return providerEvidenceCandidates.Count == 0 || catalog is null
            ? component
            : component with
            {
                ProviderDomainEvidence = providerEvidenceCandidates
                    .SelectMany(candidate => ModifierProviderDomainEvidenceResolver.Resolve(
                        component,
                        candidate,
                        componentLines,
                        itemBaseResolution,
                        traditionalInfluences,
                        catalog))
                    .DistinctBy(
                        evidence => string.Join('\u001f', evidence.ProviderDomain, evidence.ModifierId),
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            };
    }

    private static bool IsUnveiledDomain(string? domain)
    {
        return string.Equals(domain?.Trim(), "unveiled", StringComparison.OrdinalIgnoreCase);
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
        out IReadOnlyList<IReadOnlyList<ModifierStat>> matchedLineStats)
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
                IsMatch = valueLines.Count == 1
                    ? TryMatchPartialStatsToSingleParsedLine(
                        modifierDefinition,
                        valueLines[0],
                        catalog,
                        out var stats)
                    : TryMatchIndividualStatsToParsedLinesUnordered(
                        modifierDefinition,
                        valueLines,
                        catalog,
                        out stats),
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

    private static bool TryResolveRecognizedBaseImplicit(
        ParsedModifier modifier,
        IReadOnlyList<string> valueLines,
        BaseImplicitRecognitionResult? recognition,
        out ModifierDefinition candidate,
        out IReadOnlyList<IReadOnlyList<ModifierStat>> matchedLineStats,
        out GameDataCatalog effectCatalog)
    {
        candidate = null!;
        matchedLineStats = [];
        effectCatalog = null!;
        if (modifier.Kind != ParsedModifierKind.Implicit ||
            recognition?.Status is not (
                BaseImplicitRecognitionStatus.CurrentExact or
                BaseImplicitRecognitionStatus.HistoricalExact))
        {
            return false;
        }

        var effects = recognition.Matches
            .Where(match => match.Effect.IsResolved &&
                match.Effect.Modifier is not null &&
                !string.IsNullOrWhiteSpace(match.Effect.MechanicalSignature))
            .GroupBy(match => match.Effect.MechanicalSignature!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().Effect)
            .ToArray();
        if (effects.Length != 1)
        {
            return false;
        }

        try
        {
            effectCatalog = BaseImplicitMechanicalEffectCatalogFactory.Create(effects[0]);
        }
        catch (ArgumentException)
        {
            return false;
        }

        candidate = effects[0].Modifier!;
        return TryMatchStatsToParsedLines(
            candidate,
            valueLines,
            effectCatalog,
            preserveSingleLineProofSemantics: false,
            out matchedLineStats);
    }

    private static SearchComponentBaseImplicitProvenance? CreateBaseImplicitProvenance(
        BaseImplicitRecognitionResult? recognition)
    {
        if (recognition?.Status is not (
                BaseImplicitRecognitionStatus.CurrentExact or
                BaseImplicitRecognitionStatus.HistoricalExact or
                BaseImplicitRecognitionStatus.Ambiguous))
        {
            return null;
        }

        if (recognition.Status is (
                BaseImplicitRecognitionStatus.CurrentExact or
                BaseImplicitRecognitionStatus.HistoricalExact) &&
            recognition.Matches.Count == 0)
        {
            return null;
        }

        return new SearchComponentBaseImplicitProvenance
        {
            RecognitionStatus = recognition.Status,
            MechanicalSignatures = recognition.Matches
                .Select(match => TrimToNull(match.Effect.MechanicalSignature))
                .Where(signature => signature is not null)
                .Select(signature => signature!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(signature => signature, StringComparer.Ordinal)
                .ToArray(),
            SourceSnapshots = recognition.Matches
                .Select(match => match.SourceSnapshot)
                .DistinctBy(snapshot => string.Join(
                    '\u001f',
                    snapshot.Id,
                    snapshot.Role,
                    snapshot.CommitSha,
                    snapshot.DataVersion), StringComparer.OrdinalIgnoreCase)
                .Select(snapshot => new SearchComponentBaseImplicitSourceSnapshot
                {
                    SnapshotId = TrimToNull(snapshot.Id),
                    Role = snapshot.Role,
                    CommitSha = TrimToNull(snapshot.CommitSha),
                    DataVersion = TrimToNull(snapshot.DataVersion),
                })
                .ToArray(),
            DiagnosticCode = TrimToNull(recognition.DiagnosticCode),
            Diagnostic = TrimToNull(recognition.Diagnostic),
        };
    }

    private static bool TryMatchIndividualStatsToParsedLinesUnordered(
        ModifierDefinition candidate,
        IReadOnlyList<string> valueLines,
        GameDataCatalog catalog,
        out IReadOnlyList<IReadOnlyList<ModifierStat>> matchedLineStats)
    {
        matchedLineStats = [];
        var stats = candidate.Stats.OrderBy(stat => stat.Index).ToArray();
        if (stats.Length < valueLines.Count)
        {
            return false;
        }

        var matcher = new ModifierTextSignatureMatcher();
        var matched = new List<ModifierStat>();
        var groups = new List<IReadOnlyList<ModifierStat>>();
        foreach (var valueLine in valueLines)
        {
            var lineMatches = stats
                .Where(stat => !matched.Contains(stat))
                .Where(stat => IsProvenSingleLineStatAssociation(
                    candidate,
                    stat,
                    valueLine,
                    catalog,
                    matcher,
                    allowContainingTranslationProof: true))
                .ToArray();
            if (lineMatches.Length != 1)
            {
                return false;
            }

            matched.Add(lineMatches[0]);
            groups.Add([lineMatches[0]]);
        }

        matchedLineStats = groups;
        return true;
    }

    private static bool TryMatchPartialStatsToSingleParsedLine(
        ModifierDefinition candidate,
        string valueLine,
        GameDataCatalog catalog,
        out IReadOnlyList<IReadOnlyList<ModifierStat>> matchedLineStats)
    {
        matchedLineStats = [];
        var stats = candidate.Stats.OrderBy(stat => stat.Index).ToArray();
        var groups = new List<IReadOnlyList<ModifierStat>>();
        for (var start = 0; start < stats.Length; start++)
        {
            for (var count = 1; start + count <= stats.Length; count++)
            {
                var group = stats.Skip(start).Take(count).ToArray();
                if (IsProvenLineStatAssociation(candidate, group, valueLine, catalog))
                {
                    groups.Add(group);
                }
            }
        }

        if (groups.Count == 0)
        {
            return false;
        }

        var smallestSize = groups.Min(group => group.Count);
        var smallest = groups.Where(group => group.Count == smallestSize).ToArray();
        if (smallest.Length != 1)
        {
            return false;
        }

        matchedLineStats = [smallest[0]];
        return true;
    }

    private static bool TryMatchStatsToParsedLines(
        ModifierDefinition candidate,
        IReadOnlyList<string> valueLines,
        GameDataCatalog? catalog,
        bool preserveSingleLineProofSemantics,
        out IReadOnlyList<IReadOnlyList<ModifierStat>> matchedLineStats)
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

        if (preserveSingleLineProofSemantics && valueLines.Count == 1)
        {
            var matcher = new ModifierTextSignatureMatcher();
            var allowContainingTranslationProof = stats.Length > 1;
            var lineMatches = stats
                .Where(stat => IsProvenSingleLineStatAssociation(
                    candidate,
                    stat,
                    valueLines[0],
                    catalog,
                    matcher,
                    allowContainingTranslationProof))
                .ToArray();
            if (lineMatches.Length != 1)
            {
                return false;
            }

            matchedLineStats = [[lineMatches[0]]];
            return true;
        }

        var partitions = new List<IReadOnlyList<IReadOnlyList<ModifierStat>>>();
        CreateStatPartitions(
            stats,
            remainingGroupCount: valueLines.Count,
            statIndex: 0,
            current: [],
            partitions);
        var matchedPartitions = partitions
            .Select(partition => TryMatchPartitionToParsedLines(
                candidate,
                partition,
                valueLines,
                catalog,
                out var matched)
                    ? matched
                    : null)
            .Where(matched => matched is not null)
            .Select(matched => matched!)
            .ToArray();
        if (matchedPartitions.Length != 1)
        {
            return false;
        }

        matchedLineStats = matchedPartitions[0];
        return true;
    }

    private static void CreateStatPartitions(
        IReadOnlyList<ModifierStat> stats,
        int remainingGroupCount,
        int statIndex,
        IReadOnlyList<IReadOnlyList<ModifierStat>> current,
        ICollection<IReadOnlyList<IReadOnlyList<ModifierStat>>> partitions)
    {
        if (remainingGroupCount == 0)
        {
            if (statIndex == stats.Count)
            {
                partitions.Add(current.ToArray());
            }

            return;
        }

        var maximumGroupSize = stats.Count - statIndex - (remainingGroupCount - 1);
        for (var groupSize = 1; groupSize <= maximumGroupSize; groupSize++)
        {
            var group = stats.Skip(statIndex).Take(groupSize).ToArray();
            CreateStatPartitions(
                stats,
                remainingGroupCount - 1,
                statIndex + groupSize,
                current.Append(group).ToArray(),
                partitions);
        }
    }

    private static bool TryMatchPartitionToParsedLines(
        ModifierDefinition candidate,
        IReadOnlyList<IReadOnlyList<ModifierStat>> partition,
        IReadOnlyList<string> valueLines,
        GameDataCatalog catalog,
        out IReadOnlyList<IReadOnlyList<ModifierStat>> matchedLineStats)
    {
        matchedLineStats = [];
        if (partition.Count != valueLines.Count)
        {
            return false;
        }

        var consumedGroups = new bool[partition.Count];
        var assignments = new IReadOnlyList<ModifierStat>[valueLines.Count];
        if (!TryAssignLine(0))
        {
            return false;
        }

        matchedLineStats = assignments;
        return true;

        bool TryAssignLine(int lineIndex)
        {
            if (lineIndex == valueLines.Count)
            {
                return consumedGroups.All(consumed => consumed);
            }

            for (var groupIndex = 0; groupIndex < partition.Count; groupIndex++)
            {
                if (consumedGroups[groupIndex] ||
                    !IsProvenLineStatAssociation(
                        candidate,
                        partition[groupIndex],
                        valueLines[lineIndex],
                        catalog))
                {
                    continue;
                }

                consumedGroups[groupIndex] = true;
                assignments[lineIndex] = partition[groupIndex];
                if (TryAssignLine(lineIndex + 1))
                {
                    return true;
                }

                assignments[lineIndex] = [];
                consumedGroups[groupIndex] = false;
            }

            return false;
        }
    }

    private static bool IsProvenLineStatAssociation(
        ModifierDefinition candidate,
        IReadOnlyList<ModifierStat> stats,
        string valueLine,
        GameDataCatalog catalog)
    {
        var matcher = new ModifierTextSignatureMatcher();
        var exactGroupMatch = matcher.Match(
            candidate with { Stats = stats },
            catalog,
            [valueLine]);
        if (exactGroupMatch.Outcome == ModifierTextSignatureMatchOutcome.Match)
        {
            return true;
        }

        var compatibleBranch = ModifierBoundDefaults.Create(
            candidate,
            stats,
            [valueLine],
            catalog);
        return compatibleBranch.Shape != ModifierBoundShape.Unsupported &&
            compatibleBranch.TranslationIdentity is not null &&
            compatibleBranch.ProviderCanonicalSignature is not null;
    }

    private static bool IsProvenSingleLineStatAssociation(
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
        return compatibleBranch.IsSupported && compatibleBranch.TranslationIdentity is not null;
    }

    private static IEnumerable<string> StatIds(IEnumerable<ModifierStat> stats)
    {
        return stats
            .Select(stat => TrimToNull(stat.StatId))
            .Where(statId => statId is not null)
            .Select(statId => statId!);
    }

    private static bool TryExpandUniqueBlockIntoIndependentComponents(
        int modifierIndex,
        ParsedModifier modifier,
        ModifierCandidateResolutionResult? resolution,
        ModifierDefinition? exactCandidate,
        IReadOnlyList<string> valueLines,
        ItemBaseResolutionResult? itemBaseResolution,
        IReadOnlyList<string> traditionalInfluences,
        GameDataCatalog? catalog,
        UniqueModifierBlockResolution? uniqueBlockResolution,
        out IReadOnlyList<ResolvedSearchComponent> components)
    {
        components = [];
        if (uniqueBlockResolution?.IsResolved != true ||
            catalog is null ||
            valueLines.Count < 2 ||
            uniqueBlockResolution.StatIds.Count < 2 ||
            uniqueBlockResolution.StatIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != uniqueBlockResolution.StatIds.Count)
        {
            return false;
        }

        var uniqueModifierCandidates = ResolveUniqueModifierCandidates(uniqueBlockResolution, catalog);
        var boundCandidate = ResolveUniqueBoundCandidate(uniqueModifierCandidates);
        if (boundCandidate is null ||
            !TryMatchIndividualStatsToParsedLinesUnordered(
                boundCandidate,
                valueLines,
                catalog,
                out var matchedLineStats) ||
            matchedLineStats.Count != valueLines.Count)
        {
            return false;
        }

        var expanded = new List<ResolvedSearchComponent>(valueLines.Count);
        for (var index = 0; index < valueLines.Count; index++)
        {
            var lineStats = matchedLineStats[index];
            var lineStatIds = StatIds(lineStats).ToArray();
            if (lineStatIds.Length != 1)
            {
                return false;
            }

            var lineLocalities = lineStatIds
                .Select(statId => ResolveStatLocality(statId, catalog))
                .ToArray();
            var lineResolution = uniqueBlockResolution with
            {
                StatIds = lineStatIds,
                StatLocalities = lineLocalities,
                CanonicalSignatures =
                [
                    uniqueBlockResolution.CanonicalSignatures.ElementAtOrDefault(index) ??
                    uniqueBlockResolution.CatalogBlocks
                        .Select(block => block.CanonicalSignatures.ElementAtOrDefault(index))
                        .FirstOrDefault(signature => !string.IsNullOrWhiteSpace(signature)) ??
                    NormalizeComponentSignature([valueLines[index]]),
                ],
                IsEquivalentSourceSet = false,
            };
            expanded.Add(CreateComponent(
                modifierIndex,
                modifier,
                resolution,
                exactCandidate,
                lineStats,
                ModifierStatMappingProofStatus.ProvenExact,
                sourceLineIndex: index,
                sourceComponentIndex: index,
                componentLines: [valueLines[index]],
                itemBaseResolution,
                traditionalInfluences,
                catalog,
                uniqueBlockResolution: lineResolution));
        }

        components = expanded;
        return true;
    }

    private static ModifierLocality ResolveStatLocality(string statId, GameDataCatalog catalog)
    {
        var matches = catalog.FindStatsById(statId);
        return matches.Count == 1 && matches[0].IsLocal
            ? ModifierLocality.Local
            : matches.Count == 1
                ? ModifierLocality.Global
                : ModifierLocality.Unknown;
    }

    private static IReadOnlyList<ModifierDefinition> ResolveUniqueModifierCandidates(
        UniqueModifierBlockResolution? resolution,
        GameDataCatalog? catalog)
    {
        if (resolution?.IsResolved != true || catalog is null)
        {
            return [];
        }

        return resolution.ModifierIds
            .SelectMany(catalog.FindModifiersById)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Id))
            .DistinctBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static UniqueProviderSearchEvidence ResolveUniqueProviderSearchSignatures(
        UniqueModifierBlockResolution? resolution,
        IReadOnlyList<ModifierDefinition> candidates,
        IReadOnlyList<string> componentLines,
        GameDataCatalog? catalog)
    {
        if (resolution?.IsResolved != true)
        {
            return UniqueProviderSearchEvidence.Empty;
        }

        var signatures = new List<string>(resolution.CanonicalSignatures);
        var fixedQueryCandidates = new List<UniqueProviderLineFixedQueryCandidate>();
        var componentLineSignatures = componentLines.Count == 1
            ? ModifierTextSignatureNormalizer.CreateParsedSignature(componentLines).Signature.Lines
            : [];
        if (HasExactUniqueProviderSearchProvenance(resolution) && componentLines.Count > 1)
        {
            var semanticLines = resolution.PresentationLines.Count == componentLines.Count
                ? resolution.PresentationLines
                : componentLines;
            foreach (var block in resolution.CatalogBlocks)
            {
                if (block.CanonicalSignatures.Count != block.Lines.Count ||
                    block.CanonicalSignatures.Count != semanticLines.Count ||
                    block.CanonicalSignatures.Any(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                for (var index = 0; index < block.CanonicalSignatures.Count; index++)
                {
                    var sourceSignature = block.CanonicalSignatures[index].Trim();
                    signatures.Add(sourceSignature);

                    var semanticSignature = ModifierTextSignatureNormalizer
                        .CreateParsedSignature([semanticLines[index]])
                        .Signature.Lines;
                    if (semanticSignature.Count != 1 ||
                        !string.Equals(
                            semanticSignature[0],
                            sourceSignature,
                            StringComparison.OrdinalIgnoreCase) ||
                        CountNumberPlaceholders(sourceSignature) != 1)
                    {
                        continue;
                    }

                    var observedValues = ModifierBoundDefaults.ExtractObservedValues(
                        semanticLines[index]);
                    if (observedValues.Count == 1)
                    {
                        fixedQueryCandidates.Add(new UniqueProviderLineFixedQueryCandidate(
                            sourceSignature,
                            observedValues[0]));
                    }
                }
            }
        }
        foreach (var block in resolution.CatalogBlocks)
        {
            foreach (var line in block.Lines)
            {
                if (!IsFixedLiteralUniqueCatalogLine(line))
                {
                    continue;
                }

                if (componentLineSignatures.Count == 1)
                {
                    var catalogSignature = ModifierTextSignatureNormalizer.CreateParsedSignature([line])
                        .Signature.Lines;
                    if (catalogSignature.Count != 1 ||
                        !string.Equals(
                            catalogSignature[0],
                            componentLineSignatures[0],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                signatures.Add(line);
            }
        }

        foreach (var line in resolution.PresentationLines)
        {
            if (!IsFixedLiteralUniqueCatalogLine(line))
            {
                continue;
            }

            if (componentLineSignatures.Count == 1)
            {
                var presentationSignature = ModifierTextSignatureNormalizer.CreateParsedSignature([line])
                    .Signature.Lines;
                if (presentationSignature.Count != 1 ||
                    !string.Equals(
                        presentationSignature[0],
                        componentLineSignatures[0],
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            signatures.Add(line);
        }

        if (catalog is not null)
        {
            var matcher = new ModifierTextSignatureMatcher();
            foreach (var candidate in candidates)
            {
                var stats = candidate.Stats
                    .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
                    .OrderBy(stat => stat.Index)
                    .ToArray();
                signatures.AddRange(ModifierBoundDefaults.FindProviderSearchSignatures(
                    candidate,
                    stats,
                    catalog));
                var match = matcher.Match(candidate, catalog, componentLines);
                if (match.CandidateSignatures.Count == 1)
                {
                    signatures.Add(string.Join("\n", match.CandidateSignatures[0].Lines));
                }
            }
        }

        var retainedSignatures = signatures
            .Select(TrimToNull)
            .Where(signature => signature is not null)
            .Select(signature => signature!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();
        var fixedQueries = fixedQueryCandidates
            .Distinct()
            .ToArray();
        return new UniqueProviderSearchEvidence(
            retainedSignatures,
            fixedQueries.Length == 1 ? fixedQueries[0].Value : null);
    }

    private static bool HasExactUniqueProviderSearchProvenance(
        UniqueModifierBlockResolution resolution) =>
        resolution.IsResolved &&
        resolution.CatalogBlocks.Count > 0 &&
        resolution.CatalogBlocks.All(block =>
            !string.IsNullOrWhiteSpace(block.Id) &&
            block.SourceObservationIds.Count > 0 &&
            block.MechanicalMapping.Status is
                UniqueModifierMechanicalMappingStatus.Exact or
                UniqueModifierMechanicalMappingStatus.EquivalentSourceSet) &&
        resolution.SourceObservationIds.Count > 0 &&
        resolution.StatIds.Count > 0 &&
        string.IsNullOrWhiteSpace(resolution.DiagnosticCode);

    private static int CountNumberPlaceholders(string signature)
    {
        const string placeholder = "<number>";
        var count = 0;
        for (var index = 0; (index = signature.IndexOf(
                 placeholder,
                 index,
                 StringComparison.Ordinal)) >= 0; index += placeholder.Length)
        {
            count++;
        }

        return count;
    }

    private static bool IsFixedLiteralUniqueCatalogLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.Any(char.IsDigit))
        {
            return false;
        }

        return !UniqueCatalogRangeTokenPattern().IsMatch(line);
    }

    [GeneratedRegex(
        @"(?<![A-Za-z<])\(?\s*[+-]?\d+(?:[\.,]\d+)?\s*-\s*[+-]?\d+(?:[\.,]\d+)?\s*\)?",
        RegexOptions.CultureInvariant)]
    private static partial Regex UniqueCatalogRangeTokenPattern();

    private sealed record UniqueProviderSearchEvidence(
        IReadOnlyList<string> Signatures,
        decimal? ExactInitializedEditableQueryValue)
    {
        public static UniqueProviderSearchEvidence Empty { get; } = new([], null);
    }

    private sealed record UniqueProviderLineFixedQueryCandidate(
        string Signature,
        decimal Value);

    private static ModifierDefinition? ResolveUniqueBoundCandidate(
        IReadOnlyList<ModifierDefinition> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var statVectors = candidates
            .Select(candidate => candidate.Stats
                .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
                .OrderBy(stat => stat.Index)
                .Select(stat => stat.StatId!.Trim())
                .ToArray())
            .ToArray();
        return statVectors.All(vector => vector.Length > 0 && vector.SequenceEqual(
                statVectors[0],
                StringComparer.Ordinal))
            ? candidates[0]
            : null;
    }

    private static ModifierGenerationType? CommonGenerationType(
        IReadOnlyList<ModifierDefinition> candidates)
    {
        var values = candidates.Select(candidate => candidate.GenerationType).Distinct().ToArray();
        return values.Length == 1 ? values[0] : null;
    }

    private static string? CommonModifierName(IReadOnlyList<ModifierDefinition> candidates)
    {
        var values = candidates
            .Select(candidate => TrimToNull(candidate.Name))
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 1 ? values[0] : null;
    }

    private static IReadOnlyList<ModifierLocality> ResolveStatLocalities(
        IReadOnlyList<string> statIds,
        GameDataCatalog? catalog)
    {
        if (catalog is null)
        {
            return statIds.Select(_ => ModifierLocality.Unknown).ToArray();
        }

        return statIds.Select(statId =>
        {
            var matches = catalog.FindStatsById(statId);
            return matches.Count == 1
                ? matches[0].IsLocal ? ModifierLocality.Local : ModifierLocality.Global
                : ModifierLocality.Unknown;
        }).ToArray();
    }

    private static ModifierLocality AggregateLocality(IReadOnlyList<ModifierLocality> localities)
    {
        var proven = localities.Distinct().ToArray();
        return proven.Length == 1 ? proven[0] : ModifierLocality.Unknown;
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
