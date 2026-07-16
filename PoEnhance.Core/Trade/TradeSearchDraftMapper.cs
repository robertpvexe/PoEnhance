using System.Collections.Immutable;
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

        var derivedWeaponProperties = new DerivedWeaponPropertyCalculator().Calculate(parsedItem);
        var modifierResolutionByIndex = BuildModifierResolutionIndex(parsedItem, modifierResolutions ?? []);
        var aggregation = CanonicalModifierEffectAggregator.Aggregate(
            CreateSearchComponents(
                    parsedItem,
                    itemBaseResolution,
                    modifierResolutionByIndex,
                    gameDataCatalog)
                .ToArray());
        var draft = new TradeSearchDraft
        {
            ItemClass = TrimToNull(parsedItem.ItemClass),
            Rarity = TrimToNull(parsedItem.Rarity),
            DisplayName = TrimToNull(parsedItem.DisplayName),
            ParsedBaseType = TrimToNull(parsedItem.BaseType),
            ItemStates = parsedItem.ItemStates.ToArray(),
            IsCorrupted = parsedItem.IsCorrupted,
            Base = CreateBaseDraft(parsedItem, itemBaseResolution),
            ItemLevel = parsedItem.ItemLevel,
            TraditionalInfluences = parsedItem.TraditionalInfluences.ToArray(),
            EldritchInfluences = parsedItem.EldritchInfluences.ToArray(),
            ItemProperties = CreateItemProperties(derivedWeaponProperties),
            ItemPropertyDiagnostics = derivedWeaponProperties.Diagnostics
                .Select(diagnostic => new TradeSearchItemPropertyDiagnostic(
                    diagnostic.Code,
                    diagnostic.Reason,
                    diagnostic.SourceProperty))
                .ToImmutableArray(),
            ModifierFilters = aggregation.Components,
            ModifierAggregationDiagnostics = aggregation.Diagnostics,
            ListingMode = listingMode,
        };

        return TradeSearchDraftResult.Success(draft);
    }

    private static ImmutableArray<TradeSearchItemProperty> CreateItemProperties(
        DerivedWeaponProperties derived)
    {
        if (derived.Status != DerivedWeaponPropertyStatus.Success)
        {
            return [];
        }

        var properties = ImmutableArray.CreateBuilder<TradeSearchItemProperty>();
        if (derived.TotalDps.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.TotalDps,
                "Total DPS",
                derived.TotalDps.Value,
                derived.PhysicalDamage?.SourceProperty,
                derived.ElementalDamage?.SourceProperty,
                derived.ChaosDamage?.SourceProperty,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.PhysicalDps.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.PhysicalDps,
                "Physical DPS",
                derived.PhysicalDps.Value,
                derived.PhysicalDamage?.SourceProperty,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.ElementalDps.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.ElementalDps,
                "Elemental DPS",
                derived.ElementalDps.Value,
                derived.ElementalDamage?.SourceProperty,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.ChaosDps.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.ChaosDps,
                "Chaos DPS",
                derived.ChaosDps.Value,
                derived.ChaosDamage?.SourceProperty,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.AttacksPerSecond.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.AttacksPerSecond,
                "Attacks per Second",
                derived.AttacksPerSecond.Value,
                derived.AttacksPerSecondSourceProperty));
        }

        if (derived.CriticalStrikeChance.HasValue)
        {
            properties.Add(CreateItemProperty(
                TradeSearchItemPropertyKind.CriticalStrikeChance,
                "Critical Strike Chance",
                derived.CriticalStrikeChance.Value,
                derived.CriticalStrikeChanceSourceProperty));
        }

        return properties.ToImmutable();
    }

    private static TradeSearchItemProperty CreateItemProperty(
        TradeSearchItemPropertyKind kind,
        string label,
        decimal value,
        params ParsedItemProperty?[] sourceProperties)
    {
        return new TradeSearchItemProperty
        {
            Kind = kind,
            Label = label,
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
        var category = CanonicalizeOrdinaryCategory(
            parsedItem.ItemClass ?? itemBaseResolution?.MatchedItemBase?.ItemClass);

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

    private static string? CanonicalizeOrdinaryCategory(string? itemClass)
    {
        var normalized = TrimToNull(itemClass);
        if (normalized is null)
        {
            return null;
        }

        return normalized switch
        {
            "Bows" => "Bow",
            "Wands" => "Wand",
            "Body Armours" => "Body Armour",
            "Helmets" => "Helmet",
            "Gloves" => "Gloves",
            "Boots" => "Boots",
            "Rings" => "Ring",
            "Sceptres" => "Sceptre",
            "Amulets" => "Amulet",
            "Belts" => "Belt",
            "Shields" => "Shield",
            "Quivers" => "Quiver",
            "Jewels" => "Jewel",
            _ => normalized,
        };
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
