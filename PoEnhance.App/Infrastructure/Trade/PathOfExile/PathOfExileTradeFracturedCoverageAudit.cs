using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

/// <summary>Development-only coverage audit derived from the loaded GameData and Trade catalogs.</summary>
internal static class PathOfExileTradeFracturedCoverageAuditor
{
    public static PathOfExileTradeFracturedCoverageReport Audit(
        PathOfExileTradeStatCatalog tradeCatalog,
        PathOfExileTradeFilterCatalog filterCatalog,
        GameDataCatalog gameDataCatalog,
        string? packagedDataVersion = null)
    {
        ArgumentNullException.ThrowIfNull(tradeCatalog);
        ArgumentNullException.ThrowIfNull(filterCatalog);
        ArgumentNullException.ThrowIfNull(gameDataCatalog);

        var matcher = new PathOfExileTradeStatMatcher();
        var basesByTag = BuildBaseTagIndex(gameDataCatalog.ItemBases);
        var records = new List<PathOfExileTradeFracturedCoverageRecord>();
        var exclusions = new Dictionary<string, int>(StringComparer.Ordinal);
        var stateAvailable = new PathOfExileTradeItemStateFilterResolver().TryMap(
            TradeItemStateKind.Fractured,
            TradeTriState.Yes,
            filterCatalog,
            out _,
            out _);

        var packagedModifiers = gameDataCatalog.Modifiers
            .Where(IsItemPrefixOrSuffix)
            .OrderBy(modifier => modifier.Id, StringComparer.Ordinal)
            .ToArray();
        foreach (var modifier in packagedModifiers)
        {
            var eligibleBases = FindEligibleCurrentBases(modifier, basesByTag);
            if (eligibleBases.Count == 0)
            {
                Increment(exclusions, "NoPositiveCurrentBaseEligibility");
                continue;
            }

            var itemBase = eligibleBases.FirstOrDefault(itemBase =>
                CanonicalItemClassIdentityResolver.Resolve(itemBase.ItemClass).IsSupported);
            var families = BuildFamilies(modifier, gameDataCatalog);
            if (itemBase is null)
            {
                const string reason = "UnsupportedNonMvpItemClass";
                Increment(exclusions, reason);
                var representativeBase = eligibleBases[0];
                if (families.Count == 0)
                {
                    records.Add(ExcludedRecord(
                        modifier,
                        representativeBase,
                        reason,
                        "No supported one-line packaged GameData translation was available."));
                }
                else
                {
                    records.AddRange(families.Select(family => ExcludedRecord(
                        modifier,
                        representativeBase,
                        family,
                        reason)));
                }

                continue;
            }

            if (families.Count == 0)
            {
                records.Add(UnknownRecord(
                    modifier,
                    itemBase,
                    "MissingPackagedSingleLineTranslation",
                    "No supported one-line packaged GameData translation could define a canonical Trade effect."));
                continue;
            }

            foreach (var family in families)
            {
                var component = CreateComponent(modifier, family, itemBase, gameDataCatalog);
                var context = new PathOfExileTradeStatMatchContext
                {
                    ItemClass = itemBase.ItemClass,
                    ParsedBaseType = itemBase.Name,
                    ModifierLocality = component.Locality,
                    ResolvedModifierId = modifier.Id,
                    ResolvedModifierName = modifier.Name,
                    InternalStatIds = family.StatIds,
                };
                var direct = matcher.Match(component, tradeCatalog, context);
                var ordinary = matcher.Match(
                    component with { IsFractured = false },
                    tradeCatalog,
                    context);
                var outcome = ResolveOutcome(
                    component,
                    tradeCatalog,
                    direct,
                    ordinary,
                    stateAvailable,
                    out var reason,
                    out var providerIds);
                var beforeOutcome = ResolveBeforeOutcome(direct, ordinary, stateAvailable);

                records.Add(new PathOfExileTradeFracturedCoverageRecord
                {
                    ModifierId = modifier.Id!,
                    ModifierName = modifier.Name,
                    ItemBaseId = itemBase.Id,
                    ItemBaseName = itemBase.Name,
                    ItemClass = itemBase.ItemClass,
                    GenerationType = modifier.GenerationType,
                    CanonicalSignature = family.CanonicalSignature,
                    InternalStatIds = family.StatIds,
                    Locality = family.Locality,
                    ValueShape = family.ValueShape,
                    Outcome = outcome,
                    BeforeOutcome = beforeOutcome,
                    Reason = reason,
                    ProviderStatIds = providerIds,
                });
            }
        }

        var includedRecords = records
            .Where(record =>
                record.Outcome != PathOfExileTradeFracturedCoverageOutcome.Excluded)
            .ToArray();
        var familyRecords = includedRecords
            .GroupBy(FamilyKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(record => OutcomeOrder(record.Outcome))
                .ThenBy(record => record.ModifierId, StringComparer.Ordinal)
                .First())
            .ToArray();
        var knownGroups = includedRecords
            .GroupBy(record => record.ModifierId, StringComparer.Ordinal)
            .ToArray();
        var lockedKnownFamilies = familyRecords.Count(record => IsLocked(record.Outcome));
        var beforeLockedKnownFamilies = familyRecords.Count(record =>
            IsLocked(record.BeforeOutcome));
        var unresolved = familyRecords
            .Where(record => IsLocked(record.Outcome))
            .GroupBy(record => record.Reason, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PathOfExileTradeFracturedCoverageExample>)group
                    .Take(20)
                    .Select(record => new PathOfExileTradeFracturedCoverageExample
                    {
                        ModifierId = record.ModifierId,
                        ModifierName = record.ModifierName,
                        ItemClass = record.ItemClass,
                        CanonicalSignature = record.CanonicalSignature,
                        ProviderStatIds = record.ProviderStatIds,
                    })
                    .ToArray(),
                StringComparer.Ordinal);

        return new PathOfExileTradeFracturedCoverageReport
        {
            PackagedDataVersion = packagedDataVersion,
            TotalPackagedModifierRecordsConsidered = packagedModifiers.Length,
            ConfirmedCurrentFracturedRecords = knownGroups.Length,
            CanonicalFamilies = familyRecords.Length,
            ExactSingle = familyRecords.Count(record =>
                record.Outcome == PathOfExileTradeFracturedCoverageOutcome.ExactSingle),
            ExactEquivalentSet = familyRecords.Count(record =>
                record.Outcome == PathOfExileTradeFracturedCoverageOutcome.ExactEquivalentSet),
            GuardedApproximate = familyRecords.Count(record =>
                record.Outcome == PathOfExileTradeFracturedCoverageOutcome.GuardedApproximate),
            SafeAlternativeOnly = familyRecords.Count(record =>
                record.Outcome == PathOfExileTradeFracturedCoverageOutcome.SafeAlternativeOnly),
            UnknownOrNew = familyRecords.Count(record =>
                record.Outcome == PathOfExileTradeFracturedCoverageOutcome.Unknown),
            CompletelyLockedKnown = lockedKnownFamilies,
            BeforeExactSingle = familyRecords.Count(record =>
                record.BeforeOutcome == PathOfExileTradeFracturedCoverageOutcome.ExactSingle),
            BeforeExactEquivalentSet = familyRecords.Count(record =>
                record.BeforeOutcome == PathOfExileTradeFracturedCoverageOutcome.ExactEquivalentSet),
            BeforeGuardedApproximate = familyRecords.Count(record =>
                record.BeforeOutcome == PathOfExileTradeFracturedCoverageOutcome.GuardedApproximate),
            BeforeSafeAlternativeOnly = familyRecords.Count(record =>
                record.BeforeOutcome == PathOfExileTradeFracturedCoverageOutcome.SafeAlternativeOnly),
            BeforeUnknownOrNew = familyRecords.Count(record =>
                record.BeforeOutcome == PathOfExileTradeFracturedCoverageOutcome.Unknown),
            BeforeCompletelyLockedKnown = beforeLockedKnownFamilies,
            ExcludedHistoricalOrNonFracturable = exclusions,
            AmbiguousOrUnresolvedByReason = familyRecords
                .Where(record => IsLocked(record.Outcome))
                .GroupBy(record => record.Reason, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            ProviderAbsentCurrentBlockers = familyRecords
                .Where(record =>
                    IsLocked(record.Outcome) &&
                    record.ProviderStatIds.Count == 0)
                .Select(record => new PathOfExileTradeFracturedCoverageExample
                {
                    ModifierId = record.ModifierId,
                    ModifierName = record.ModifierName,
                    ItemClass = record.ItemClass,
                    CanonicalSignature = record.CanonicalSignature,
                    ProviderStatIds = record.ProviderStatIds,
                })
                .ToArray(),
            RepresentativeUnresolvedExamples = unresolved,
            Records = records,
            ExcludedRecords = records
                .Where(record =>
                    record.Outcome == PathOfExileTradeFracturedCoverageOutcome.Excluded)
                .ToArray(),
        };
    }

    private static PathOfExileTradeFracturedCoverageOutcome ResolveOutcome(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchResult direct,
        PathOfExileTradeStatMatchResult ordinary,
        bool stateAvailable,
        out string reason,
        out IReadOnlyList<string> providerIds)
    {
        var directCandidates = ExactCandidates(direct);
        if (directCandidates.Count > 0)
        {
            var exact = PathOfExileTradeModifierVariantResolver.ApplyFracturedExact(
                component,
                catalog,
                directCandidates);
            if (exact.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact)
            {
                reason = "DirectFracturedExactSingle";
                providerIds = directCandidates.Select(candidate => candidate.StatId).ToArray();
                return PathOfExileTradeFracturedCoverageOutcome.ExactSingle;
            }

            if (exact.ProviderResolutionStatus ==
                SearchComponentProviderResolutionStatus.ExactEquivalentSet)
            {
                reason = "DirectFracturedExactEquivalentSet";
                providerIds = directCandidates.Select(candidate => candidate.StatId).ToArray();
                return PathOfExileTradeFracturedCoverageOutcome.ExactEquivalentSet;
            }
        }

        var ordinaryCandidates = ExactCandidates(ordinary);
        if (stateAvailable &&
            ordinaryCandidates.Count > 0 &&
            ordinaryCandidates.All(candidate => string.Equals(
                PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
                "explicit",
                StringComparison.Ordinal)))
        {
            var approximate = PathOfExileTradeModifierVariantResolver.ApplyFracturedApproximate(
                component,
                catalog,
                ordinaryCandidates);
            if (approximate.ProviderResolutionStatus ==
                SearchComponentProviderResolutionStatus.Approximate)
            {
                reason = "GuardedExplicitRepresentation";
                providerIds = ordinaryCandidates.Select(candidate => candidate.StatId).ToArray();
                return PathOfExileTradeFracturedCoverageOutcome.GuardedApproximate;
            }
        }

        providerIds = direct.Candidates
            .Concat(ordinary.Candidates)
            .Select(candidate => candidate.StatId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        reason = !stateAvailable
            ? "OfficialFracturedItemStateFilterUnavailable"
            : direct.Status == PathOfExileTradeStatMatchStatus.Ambiguous
                ? "IncompatibleDirectFracturedCandidates"
                : ordinary.Status == PathOfExileTradeStatMatchStatus.Ambiguous
                    ? "IncompatibleOrdinaryCandidates"
                    : direct.Diagnostics.FirstOrDefault()?.Code ??
                        ordinary.Diagnostics.FirstOrDefault()?.Code ??
                        "NoSafeProviderRepresentation";
        return PathOfExileTradeFracturedCoverageOutcome.Unknown;
    }

    private static PathOfExileTradeFracturedCoverageOutcome ResolveBeforeOutcome(
        PathOfExileTradeStatMatchResult direct,
        PathOfExileTradeStatMatchResult ordinary,
        bool stateAvailable)
    {
        if (direct.Status == PathOfExileTradeStatMatchStatus.Exact)
        {
            return PathOfExileTradeFracturedCoverageOutcome.ExactSingle;
        }

        if (stateAvailable && ordinary.Status == PathOfExileTradeStatMatchStatus.Exact)
        {
            return PathOfExileTradeFracturedCoverageOutcome.GuardedApproximate;
        }

        return PathOfExileTradeFracturedCoverageOutcome.Unknown;
    }

    private static IReadOnlyList<PathOfExileTradeStatMatchCandidate> ExactCandidates(
        PathOfExileTradeStatMatchResult match) =>
        match.Status switch
        {
            PathOfExileTradeStatMatchStatus.Exact when match.ExactCandidate is not null =>
                [match.ExactCandidate],
            PathOfExileTradeStatMatchStatus.ExactEquivalentSet =>
                match.ExactEquivalentCandidates,
            _ => [],
        };

    private static ResolvedSearchComponent CreateComponent(
        ModifierDefinition modifier,
        TranslationFamily family,
        ItemBaseRecord itemBase,
        GameDataCatalog catalog)
    {
        var scalar = family.ValueShape == ModifierBoundShape.Scalar ? 1m : (decimal?)null;
        var component = new ResolvedSearchComponent
        {
            ComponentId = $"fractured-audit:{modifier.Id}:{family.CanonicalSignature}",
            OriginalText = family.CanonicalSignature,
            CanonicalSignature = family.CanonicalSignature,
            ProviderCanonicalSignature = family.CanonicalSignature,
            ParsedKind = modifier.GenerationType == ModifierGenerationType.Prefix
                ? ParsedModifierKind.Prefix
                : ParsedModifierKind.Suffix,
            GenerationType = modifier.GenerationType,
            Locality = family.Locality,
            IsFractured = true,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = modifier.Id,
            ResolvedModifierName = modifier.Name,
            ResolvedStatIds = family.StatIds,
            IsSearchable = true,
            SupportsValueBounds = family.ValueShape is
                ModifierBoundShape.Scalar or ModifierBoundShape.ArithmeticMeanRange,
            ValueBoundShape = family.ValueShape,
            ObservedNumericValues = family.ValueShape switch
            {
                ModifierBoundShape.Scalar => [1m],
                ModifierBoundShape.ArithmeticMeanRange => [1m, 2m],
                _ => [],
            },
            CanonicalNumericValues = family.ValueShape switch
            {
                ModifierBoundShape.Scalar => [1m],
                ModifierBoundShape.ArithmeticMeanRange => [1m, 2m],
                _ => [],
            },
            ProviderFallbackNumericValues = family.ValueShape == ModifierBoundShape.PresenceOnly
                ? modifier.Stats
                    .Where(stat => family.StatIds.Contains(
                        stat.StatId ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase))
                    .Where(stat => stat.MinValue.HasValue &&
                        stat.MinValue == stat.MaxValue)
                    .Select(stat => stat.MinValue!.Value)
                    .ToArray()
                : [],
            ValueBoundTranslationHandlers = family.TranslationHandlers,
            DefaultBoundDirection = ModifierBoundDirection.Minimum,
            RequestedMinimum = family.ValueShape == ModifierBoundShape.ArithmeticMeanRange
                ? 1.5m
                : scalar,
        };
        var evaluations = ModifierProviderDomainEligibilityIndex.For(catalog).Evaluate(
            component,
            modifier,
            ItemModifierEligibilityContext.ForItemBase(itemBase));
        return component with
        {
            ProviderDomainEvidence =
            [
                new SearchComponentProviderDomainEvidence
                {
                    ProviderDomain = "Fractured",
                    ModifierId = modifier.Id!,
                    GenerationType = modifier.GenerationType,
                    Locality = family.Locality,
                    IsSourceExact = true,
                    EvidenceStrength = 1000,
                    ItemBaseId = itemBase.Id,
                    ItemClass = itemBase.ItemClass,
                    ApplicabilityReasonCode = "SOURCE_EXACT",
                    ApplicabilityReason = "Confirmed-current Fractured audit source.",
                },
                .. evaluations
                    .Where(evaluation =>
                        evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported)
                    .Select(evaluation => new SearchComponentProviderDomainEvidence
                    {
                        ProviderDomain = evaluation.ProviderDomain,
                        ModifierId = evaluation.Modifier.Id!,
                        GenerationType = evaluation.Modifier.GenerationType,
                        SourceGenerationType = evaluation.Modifier.SourceGenerationType,
                        Locality = family.Locality,
                        IsProjectedDomain = evaluation.IsProjectedDomain,
                        EvidenceStrength = evaluation.EvidenceStrength,
                        ItemBaseId = itemBase.Id,
                        ItemClass = itemBase.ItemClass,
                        MatchedTag = evaluation.MatchedTag,
                        ApplicabilityReasonCode = evaluation.ReasonCode,
                        ApplicabilityReason = evaluation.Reason,
                    }),
            ],
        };
    }

    private static IReadOnlyList<TranslationFamily> BuildFamilies(
        ModifierDefinition modifier,
        GameDataCatalog catalog)
    {
        var modifierStatIds = modifier.Stats
            .Select(stat => Normalize(stat.StatId))
            .Where(statId => statId is not null)
            .Select(statId => statId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var translations = modifierStatIds
            .SelectMany(catalog.FindStatTranslationsByStatId)
            .DistinctBy(translation => translation.Id, StringComparer.Ordinal)
            .Where(translation =>
                translation.StatIds.Count > 0 &&
                translation.StatIds.Any(modifierStatIds.Contains));
        var families = new List<TranslationFamily>();
        foreach (var translation in translations)
        {
            var includedIndexes = translation.StatIds
                .Select((statId, index) => new { StatId = statId, Index = index })
                .Where(entry => modifierStatIds.Contains(entry.StatId))
                .Select(entry => entry.Index)
                .ToHashSet();
            var includedStatIds = translation.StatIds
                .Where((_, index) => includedIndexes.Contains(index))
                .ToArray();
            var locality = LocalityFor(includedStatIds, catalog);
            if (locality == ModifierLocality.Unknown)
            {
                continue;
            }

            foreach (var variant in translation.Variants)
            {
                if (!VariantCanApply(translation, variant, modifier))
                {
                    continue;
                }

                if (!TryCreateCanonicalSignature(
                        variant,
                        includedIndexes,
                        out var signature,
                        out var numericIndexes))
                {
                    continue;
                }

                families.Add(new TranslationFamily(
                    signature,
                    includedStatIds,
                    locality,
                    numericIndexes.Count switch
                    {
                        0 => ModifierBoundShape.PresenceOnly,
                        1 => ModifierBoundShape.Scalar,
                        2 => ModifierBoundShape.ArithmeticMeanRange,
                        _ => ModifierBoundShape.Unsupported,
                    },
                    numericIndexes.Select(index =>
                        (IReadOnlyList<string>)(variant.IndexHandlers
                            .SingleOrDefault(handler => handler.Index == index)?.Handlers
                            .ToArray() ?? [])).ToArray()));
            }
        }

        return families
            .Where(family => family.ValueShape != ModifierBoundShape.Unsupported)
            .DistinctBy(family => string.Join(
                '\u001f',
                family.CanonicalSignature,
                family.Locality,
                family.ValueShape,
                string.Join('\u001e', family.StatIds)), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool VariantCanApply(
        StatTranslationDefinition translation,
        StatTranslationVariant variant,
        ModifierDefinition modifier)
    {
        var modifierStats = modifier.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
            .GroupBy(stat => stat.StatId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return variant.Conditions.All(condition =>
        {
            if (condition.Index < 0 || condition.Index >= translation.StatIds.Count)
            {
                return false;
            }

            var statId = translation.StatIds[condition.Index];
            var minimum = modifierStats.TryGetValue(statId, out var stat)
                ? stat.MinValue ?? 0m
                : 0m;
            var maximum = stat?.MaxValue ?? minimum;
            var conditionMinimum = condition.MinValue ?? decimal.MinValue;
            var conditionMaximum = condition.MaxValue ?? decimal.MaxValue;
            var intersects = maximum >= conditionMinimum && minimum <= conditionMaximum;
            if (!condition.IsNegated)
            {
                return intersects;
            }

            return minimum < conditionMinimum || maximum > conditionMaximum;
        });
    }

    private static bool TryCreateCanonicalSignature(
        StatTranslationVariant variant,
        IReadOnlySet<int> includedIndexes,
        out string signature,
        out IReadOnlyList<int> numericIndexes)
    {
        signature = string.Empty;
        numericIndexes = [];
        if (variant.FormatLines.Count != 1 || variant.ValueFormats.Count == 0)
        {
            return false;
        }

        var indexes = new List<int>();
        var value = variant.FormatLines[0];
        for (var index = 0; index < variant.ValueFormats.Count; index++)
        {
            if (!includedIndexes.Contains(index) &&
                !string.Equals(variant.ValueFormats[index], "ignore", StringComparison.Ordinal))
            {
                return false;
            }

            var replacement = variant.ValueFormats[index] switch
            {
                "#" => "<number>",
                "+#" => "+<number>",
                "ignore" => string.Empty,
                _ => null,
            };
            if (replacement is null)
            {
                return false;
            }

            if (includedIndexes.Contains(index) &&
                variant.ValueFormats[index] is "#" or "+#")
            {
                indexes.Add(index);
            }

            value = value.Replace($"{{{index}}}", replacement, StringComparison.Ordinal);
        }

        signature = value.Trim();
        numericIndexes = indexes;
        return signature.Length > 0 && !signature.Contains('{', StringComparison.Ordinal);
    }

    private static ModifierLocality LocalityFor(
        IReadOnlyList<string> statIds,
        GameDataCatalog catalog)
    {
        var definitions = statIds
            .Select(statId => catalog.FindStatsById(statId).SingleOrDefault())
            .ToArray();
        return definitions.Length == 0 || definitions.Any(definition => definition is null)
            ? ModifierLocality.Unknown
            : definitions.All(definition => definition!.IsLocal)
                ? ModifierLocality.Local
                : definitions.All(definition => !definition!.IsLocal)
                    ? ModifierLocality.Global
                    : ModifierLocality.Unknown;
    }

    private static IReadOnlyList<ItemBaseRecord> FindEligibleCurrentBases(
        ModifierDefinition modifier,
        IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> basesByTag)
    {
        var evaluator = new ModifierEligibilityEvaluator();
        var eligibleBases = new List<ItemBaseRecord>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in modifier.SpawnWeights
                     .Where(weight => weight.Weight > 0)
                     .Select(weight => Normalize(weight.Tag))
                     .Where(tag => tag is not null)
                     .Select(tag => tag!)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!basesByTag.TryGetValue(tag, out var itemBases))
            {
                continue;
            }

            foreach (var itemBase in itemBases.Where(itemBase =>
                         evaluator.Evaluate(modifier, itemBase).Outcome ==
                         ModifierEligibilityOutcome.Eligible))
            {
                if (!string.IsNullOrWhiteSpace(itemBase.Id) &&
                    seenIds.Add(itemBase.Id))
                {
                    eligibleBases.Add(itemBase);
                }
            }
        }

        return eligibleBases;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> BuildBaseTagIndex(
        IReadOnlyList<ItemBaseRecord> itemBases)
    {
        return itemBases
            .Where(itemBase =>
                string.Equals(Normalize(itemBase.Domain), "item", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(itemBase.Name))
            .SelectMany(itemBase => itemBase.Tags
                .Select(Normalize)
                .Where(tag => tag is not null)
                .Select(tag => new { Tag = tag!, ItemBase = itemBase }))
            .GroupBy(entry => entry.Tag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ItemBaseRecord>)group
                    .Select(entry => entry.ItemBase)
                    .DistinctBy(itemBase => itemBase.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static PathOfExileTradeFracturedCoverageRecord UnknownRecord(
        ModifierDefinition modifier,
        ItemBaseRecord itemBase,
        string reason,
        string message) =>
        new()
        {
            ModifierId = modifier.Id!,
            ModifierName = modifier.Name,
            ItemBaseId = itemBase.Id,
            ItemBaseName = itemBase.Name,
            ItemClass = itemBase.ItemClass,
            GenerationType = modifier.GenerationType,
            CanonicalSignature = message,
            InternalStatIds = modifier.Stats
                .Select(stat => stat.StatId)
                .Where(statId => !string.IsNullOrWhiteSpace(statId))
                .Select(statId => statId!)
                .ToArray(),
            Outcome = PathOfExileTradeFracturedCoverageOutcome.Unknown,
            BeforeOutcome = PathOfExileTradeFracturedCoverageOutcome.Unknown,
            Reason = reason,
        };

    private static PathOfExileTradeFracturedCoverageRecord ExcludedRecord(
        ModifierDefinition modifier,
        ItemBaseRecord itemBase,
        string reason,
        string message) =>
        new()
        {
            ModifierId = modifier.Id!,
            ModifierName = modifier.Name,
            ItemBaseId = itemBase.Id,
            ItemBaseName = itemBase.Name,
            ItemClass = itemBase.ItemClass,
            GenerationType = modifier.GenerationType,
            CanonicalSignature = message,
            InternalStatIds = modifier.Stats
                .Select(stat => stat.StatId)
                .Where(statId => !string.IsNullOrWhiteSpace(statId))
                .Select(statId => statId!)
                .ToArray(),
            Outcome = PathOfExileTradeFracturedCoverageOutcome.Excluded,
            BeforeOutcome = PathOfExileTradeFracturedCoverageOutcome.Excluded,
            Reason = reason,
        };

    private static PathOfExileTradeFracturedCoverageRecord ExcludedRecord(
        ModifierDefinition modifier,
        ItemBaseRecord itemBase,
        TranslationFamily family,
        string reason) =>
        new()
        {
            ModifierId = modifier.Id!,
            ModifierName = modifier.Name,
            ItemBaseId = itemBase.Id,
            ItemBaseName = itemBase.Name,
            ItemClass = itemBase.ItemClass,
            GenerationType = modifier.GenerationType,
            CanonicalSignature = family.CanonicalSignature,
            InternalStatIds = family.StatIds,
            Locality = family.Locality,
            ValueShape = family.ValueShape,
            Outcome = PathOfExileTradeFracturedCoverageOutcome.Excluded,
            BeforeOutcome = PathOfExileTradeFracturedCoverageOutcome.Excluded,
            Reason = reason,
        };

    private static bool IsItemPrefixOrSuffix(ModifierDefinition modifier) =>
        !string.IsNullOrWhiteSpace(modifier.Id) &&
        string.Equals(Normalize(modifier.Domain), "item", StringComparison.Ordinal) &&
        modifier.GenerationType is ModifierGenerationType.Prefix or ModifierGenerationType.Suffix;

    private static bool IsLocked(PathOfExileTradeFracturedCoverageOutcome outcome) =>
        outcome == PathOfExileTradeFracturedCoverageOutcome.Unknown;

    private static int OutcomeOrder(PathOfExileTradeFracturedCoverageOutcome outcome) =>
        outcome switch
        {
            PathOfExileTradeFracturedCoverageOutcome.ExactSingle => 0,
            PathOfExileTradeFracturedCoverageOutcome.ExactEquivalentSet => 1,
            PathOfExileTradeFracturedCoverageOutcome.GuardedApproximate => 2,
            PathOfExileTradeFracturedCoverageOutcome.SafeAlternativeOnly => 3,
            PathOfExileTradeFracturedCoverageOutcome.Unknown => 4,
            _ => 5,
        };

    private static string FamilyKey(PathOfExileTradeFracturedCoverageRecord record) =>
        string.Join(
            '\u001f',
            record.CanonicalSignature,
            record.Locality,
            record.ValueShape,
            string.Join('\u001e', record.InternalStatIds));

    private static void Increment(IDictionary<string, int> values, string key) =>
        values[key] = values.TryGetValue(key, out var count) ? count + 1 : 1;

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record TranslationFamily(
        string CanonicalSignature,
        IReadOnlyList<string> StatIds,
        ModifierLocality Locality,
        ModifierBoundShape ValueShape,
        IReadOnlyList<IReadOnlyList<string>> TranslationHandlers);
}

internal enum PathOfExileTradeFracturedCoverageOutcome
{
    ExactSingle,
    ExactEquivalentSet,
    GuardedApproximate,
    SafeAlternativeOnly,
    Unknown,
    Excluded,
}

internal sealed record PathOfExileTradeFracturedCoverageReport
{
    public string? PackagedDataVersion { get; init; }

    public int TotalPackagedModifierRecordsConsidered { get; init; }

    public int ConfirmedCurrentFracturedRecords { get; init; }

    public int CanonicalFamilies { get; init; }

    public int ExactSingle { get; init; }

    public int ExactEquivalentSet { get; init; }

    public int GuardedApproximate { get; init; }

    public int SafeAlternativeOnly { get; init; }

    public int UnknownOrNew { get; init; }

    public int CompletelyLockedKnown { get; init; }

    public int BeforeExactSingle { get; init; }

    public int BeforeExactEquivalentSet { get; init; }

    public int BeforeGuardedApproximate { get; init; }

    public int BeforeSafeAlternativeOnly { get; init; }

    public int BeforeUnknownOrNew { get; init; }

    public int BeforeCompletelyLockedKnown { get; init; }

    public IReadOnlyDictionary<string, int> ExcludedHistoricalOrNonFracturable { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AmbiguousOrUnresolvedByReason { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<PathOfExileTradeFracturedCoverageExample>>
        RepresentativeUnresolvedExamples { get; init; } =
            new Dictionary<string, IReadOnlyList<PathOfExileTradeFracturedCoverageExample>>(
                StringComparer.Ordinal);

    public IReadOnlyList<PathOfExileTradeFracturedCoverageExample> ProviderAbsentCurrentBlockers
        { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeFracturedCoverageRecord> Records { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeFracturedCoverageRecord> ExcludedRecords { get; init; } = [];
}

internal sealed record PathOfExileTradeFracturedCoverageRecord
{
    public required string ModifierId { get; init; }

    public string? ModifierName { get; init; }

    public string? ItemBaseId { get; init; }

    public string? ItemBaseName { get; init; }

    public string? ItemClass { get; init; }

    public ModifierGenerationType GenerationType { get; init; }

    public required string CanonicalSignature { get; init; }

    public IReadOnlyList<string> InternalStatIds { get; init; } = [];

    public ModifierLocality Locality { get; init; }

    public ModifierBoundShape ValueShape { get; init; }

    public PathOfExileTradeFracturedCoverageOutcome Outcome { get; init; }

    public PathOfExileTradeFracturedCoverageOutcome BeforeOutcome { get; init; }

    public required string Reason { get; init; }

    public IReadOnlyList<string> ProviderStatIds { get; init; } = [];
}

internal sealed record PathOfExileTradeFracturedCoverageExample
{
    public required string ModifierId { get; init; }

    public string? ModifierName { get; init; }

    public string? ItemClass { get; init; }

    public required string CanonicalSignature { get; init; }

    public IReadOnlyList<string> ProviderStatIds { get; init; } = [];
}
