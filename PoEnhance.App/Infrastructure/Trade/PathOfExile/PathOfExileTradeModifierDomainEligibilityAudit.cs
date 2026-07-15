using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

/// <summary>Development-only exhaustive audit derived from the two loaded catalogs.</summary>
internal static class PathOfExileTradeModifierDomainEligibilityAuditor
{
    public static PathOfExileTradeModifierDomainEligibilityAuditReport Audit(
        PathOfExileTradeStatCatalog tradeCatalog,
        GameDataCatalog gameDataCatalog)
    {
        ArgumentNullException.ThrowIfNull(tradeCatalog);
        ArgumentNullException.ThrowIfNull(gameDataCatalog);

        var prototypes = BuildEffectPrototypes(tradeCatalog, gameDataCatalog);
        var baseIndex = BuildBaseIndex(gameDataCatalog.ItemBases);
        var combinations = new List<PathOfExileTradeModifierDomainEligibilityAuditCombination>();
        var contextCount = 0;

        foreach (var effect in prototypes)
        {
            var contexts = FindTargetContexts(effect, baseIndex)
                .GroupBy(context => string.Join(
                    '\u001f',
                    Normalize(context.ItemBase.Domain),
                    Normalize(context.ItemBase.ItemClass),
                    context.MatchedTag), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(context => context.ItemBase.ItemClass, StringComparer.Ordinal)
                .ThenBy(context => context.MatchedTag, StringComparer.Ordinal)
                .ToArray();
            if (contexts.Length == 0)
            {
                continue;
            }

            foreach (var targetContext in contexts)
            {
                contextCount++;
                var source = SelectSource(effect, targetContext, tradeCatalog);
                if (source is null)
                {
                    combinations.Add(new PathOfExileTradeModifierDomainEligibilityAuditCombination
                    {
                        CanonicalEffect = effect.CanonicalEffect,
                        CanonicalSignature = effect.CanonicalSignature,
                        InternalStatIds = effect.StatIds,
                        Locality = effect.Locality,
                        ItemClass = targetContext.ItemBase.ItemClass ?? string.Empty,
                        ItemBaseId = targetContext.ItemBase.Id ?? string.Empty,
                        MatchedContextTag = targetContext.MatchedTag,
                        ProviderKind = "unknown",
                        ProviderStatId = string.Empty,
                        SourceFamilyEvidence = [],
                        Status = PathOfExileTradeModifierDomainEligibilityAuditStatus.Rejected,
                        Reason = "No unique official source provider identity matched the canonical GameData translation.",
                        LocalityCompatible = false,
                        UnitCompatible = false,
                        ArityCompatible = false,
                        TransformCompatible = false,
                        DuplicateResolution = "NotEvaluated",
                    });
                    continue;
                }

                var component = CreateComponent(effect, source.Value, targetContext.ItemBase, gameDataCatalog);
                var discovery = PathOfExileTradeModifierVariantDiscovery.Discover(
                    component,
                    tradeCatalog,
                    source.Value.Candidate);
                foreach (var trace in discovery.Trace)
                {
                    var ambiguity = trace.RejectionReason == PathOfExileTradeModifierVariantDiscovery.SameKindAmbiguous;
                    combinations.Add(new PathOfExileTradeModifierDomainEligibilityAuditCombination
                    {
                        CanonicalEffect = effect.CanonicalEffect,
                        CanonicalSignature = effect.CanonicalSignature,
                        InternalStatIds = effect.StatIds,
                        Locality = effect.Locality,
                        ItemClass = targetContext.ItemBase.ItemClass ?? string.Empty,
                        ItemBaseId = targetContext.ItemBase.Id ?? string.Empty,
                        MatchedContextTag = targetContext.MatchedTag,
                        ProviderKind = trace.ProviderKind,
                        ProviderStatId = trace.ProviderStatId,
                        SourceFamilyEvidence = component.ProviderDomainEvidence
                            .Where(evidence => string.Equals(
                                evidence.ProviderDomain,
                                trace.ProviderKind,
                                StringComparison.OrdinalIgnoreCase))
                            .Select(evidence => $"{evidence.ModifierId}:{evidence.ApplicabilityReasonCode}:{evidence.MatchedTag}")
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray(),
                        Status = trace.IsAccepted
                            ? PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported
                            : ambiguity
                                ? PathOfExileTradeModifierDomainEligibilityAuditStatus.Ambiguous
                                : PathOfExileTradeModifierDomainEligibilityAuditStatus.Rejected,
                        Reason = trace.IsAccepted ? PathOfExileTradeModifierVariantDiscovery.Accepted : trace.RejectionReason,
                        LocalityCompatible = !IsLocalityRejection(trace.RejectionReason),
                        UnitCompatible = !trace.RejectionReason.EndsWith(
                            PathOfExileTradePseudoVariantCompatibility.IncompatibleNumericUnit,
                            StringComparison.Ordinal),
                        ArityCompatible = !trace.RejectionReason.EndsWith(
                            PathOfExileTradePseudoVariantCompatibility.IncompatibleNumericArity,
                            StringComparison.Ordinal),
                        TransformCompatible = !trace.RejectionReason.EndsWith(
                            PathOfExileTradePseudoVariantCompatibility.IncompatibleTranslationProjection,
                            StringComparison.Ordinal),
                        DuplicateResolution = DuplicateResolution(trace),
                    });
                }
            }
        }

        var supported = combinations
            .Where(combination => combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported)
            .ToArray();
        var ambiguous = combinations
            .Count(combination => combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Ambiguous);
        return new PathOfExileTradeModifierDomainEligibilityAuditReport
        {
            CanonicalEffectsInspected = prototypes.Count,
            ItemContextCombinationsInspected = contextCount,
            ProviderCandidatesInspected = combinations.Count,
            SupportedVariantsByProviderKind = supported
                .GroupBy(combination => combination.ProviderKind, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            RejectedVariantsByReason = combinations
                .Where(combination => combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Rejected)
                .GroupBy(combination => combination.Reason, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            AmbiguousVariants = ambiguous,
            DuplicateIdentitiesRemoved = combinations.Count(combination =>
                combination.DuplicateResolution is
                    PathOfExileTradeModifierVariantDiscovery.DuplicateProviderStatId or
                    PathOfExileTradeModifierVariantDiscovery.DuplicateCanonicalIdentity or
                    PathOfExileTradeModifierVariantDiscovery.WeakerSemanticProvenance),
            EffectsWithNoValidProviderVariant = prototypes.Count(effect =>
                !supported.Any(combination => string.Equals(
                    combination.CanonicalSignature,
                    effect.CanonicalSignature,
                    StringComparison.Ordinal) &&
                    combination.Locality == effect.Locality)),
            Combinations = combinations,
        };
    }

    private static IReadOnlyList<EffectPrototype> BuildEffectPrototypes(
        PathOfExileTradeStatCatalog tradeCatalog,
        GameDataCatalog gameDataCatalog)
    {
        var prototypes = new List<EffectPrototype>();
        var providerLookups = tradeCatalog.CandidateGroups
            .Select(group => group.Key.NormalizedTemplate)
            .ToHashSet(StringComparer.Ordinal);
        var translations = gameDataCatalog.Modifiers
            .SelectMany(modifier => modifier.Stats)
            .SelectMany(stat => gameDataCatalog.FindStatTranslationsByStatId(stat.StatId))
            .DistinctBy(translation => translation.Id, StringComparer.Ordinal);
        foreach (var translation in translations)
        {
            var statDefinitions = translation.StatIds
                .Select(statId => gameDataCatalog.FindStatsById(statId).SingleOrDefault())
                .ToArray();
            if (statDefinitions.Any(stat => stat is null))
            {
                continue;
            }

            var locality = statDefinitions.All(stat => stat!.IsLocal)
                ? ModifierLocality.Local
                : statDefinitions.All(stat => !stat!.IsLocal)
                    ? ModifierLocality.Global
                    : ModifierLocality.Unknown;
            if (locality == ModifierLocality.Unknown || translation.StatIds.Count == 0)
            {
                continue;
            }

            foreach (var variant in translation.Variants)
            {
                if (!TryCreateCanonicalSignature(variant, out var canonicalSignature, out var numericIndexes))
                {
                    continue;
                }

                var providerTemplate = canonicalSignature
                    .Replace("+<number>", "+#", StringComparison.Ordinal)
                    .Replace("-<number>", "-#", StringComparison.Ordinal)
                    .Replace("<number>", "#", StringComparison.Ordinal);
                var lookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(providerTemplate);
                if (!providerLookups.Contains(lookup))
                {
                    continue;
                }

                var modifiers = gameDataCatalog.FindModifiersByStatId(translation.StatIds[0])
                    .Where(modifier => SelectTranslationStats(
                        modifier.Stats.OrderBy(stat => stat.Index).ToArray(),
                        translation.StatIds).Count == translation.StatIds.Count)
                    .DistinctBy(modifier => modifier.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (modifiers.Length == 0)
                {
                    continue;
                }

                prototypes.Add(new EffectPrototype(
                    PathOfExileTradePseudoVariantCompatibility.LogicalEffectIdentity(providerTemplate),
                    canonicalSignature,
                    translation.StatIds.ToArray(),
                    locality,
                    numericIndexes.Count switch
                    {
                        0 => ModifierBoundShape.PresenceOnly,
                        1 => ModifierBoundShape.Scalar,
                        2 => ModifierBoundShape.ArithmeticMeanRange,
                        _ => ModifierBoundShape.Unsupported,
                    },
                    TranslationIdentity(translation, variant, numericIndexes),
                    numericIndexes.Select(index => (IReadOnlyList<string>)(variant.IndexHandlers
                        .SingleOrDefault(handler => handler.Index == index)?.Handlers
                        .ToArray() ?? [])).ToArray(),
                    modifiers));
            }
        }

        return prototypes
            .GroupBy(prototype => string.Join(
                '\u001f',
                prototype.CanonicalSignature,
                prototype.Locality,
                prototype.ValueShape,
                prototype.TranslationIdentity), StringComparer.Ordinal)
            .Select(group => group.First() with
            {
                Modifiers = group.SelectMany(prototype => prototype.Modifiers)
                    .DistinctBy(modifier => modifier.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            })
            .OrderBy(prototype => prototype.CanonicalSignature, StringComparer.Ordinal)
            .ThenBy(prototype => prototype.Locality)
            .ToArray();
    }

    private static IReadOnlyList<TargetContext> FindTargetContexts(
        EffectPrototype effect,
        IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> baseIndex)
    {
        var contexts = new List<TargetContext>();
        foreach (var modifier in effect.Modifiers)
        {
            var modifierDomain = Normalize(modifier.Domain);
            if (modifierDomain is null || modifierDomain is "crafted" or "unveiled" or "veiled")
            {
                continue;
            }

            var modifierId = Normalize(modifier.Id);
            if (modifierId is not null &&
                baseIndex.TryGetValue(BaseIndexKey("implicit", modifierId), out var implicitBases))
            {
                contexts.AddRange(implicitBases.Select(itemBase => new TargetContext(itemBase, "base-implicit", modifier)));
            }

            foreach (var spawnWeight in modifier.SpawnWeights.Where(weight => weight.Weight > 0))
            {
                var tag = Normalize(spawnWeight.Tag);
                if (tag is null)
                {
                    continue;
                }

                var key = BaseIndexKey(modifierDomain, tag);
                if (!baseIndex.TryGetValue(key, out var candidates))
                {
                    continue;
                }

                foreach (var itemBase in candidates
                             .GroupBy(itemBase => itemBase.ItemClass, StringComparer.OrdinalIgnoreCase)
                             .Select(group => group.First()))
                {
                    var eligibility = new ModifierEligibilityEvaluator().Evaluate(modifier, itemBase);
                    if (eligibility.Outcome == ModifierEligibilityOutcome.Eligible)
                    {
                        contexts.Add(new TargetContext(itemBase, eligibility.MatchedTag ?? tag, modifier));
                    }
                }
            }

            if (modifier.SpawnWeights.Count > 0 && modifier.SpawnWeights.All(weight =>
                    string.Equals(Normalize(weight.Tag), "default", StringComparison.OrdinalIgnoreCase) &&
                    weight.Weight == 0) &&
                baseIndex.TryGetValue(BaseIndexKey(modifierDomain, "default"), out var structuralBases))
            {
                contexts.AddRange(structuralBases
                    .GroupBy(itemBase => itemBase.ItemClass, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new TargetContext(group.First(), "default", modifier)));
            }
        }

        return contexts;
    }

    private static (ModifierDefinition Modifier, PathOfExileTradeStatMatchCandidate Candidate)? SelectSource(
        EffectPrototype effect,
        TargetContext targetContext,
        PathOfExileTradeStatCatalog tradeCatalog)
    {
        var sourceCandidates = new[] { targetContext.SourceModifier }
            .Concat(effect.Modifiers.OrderBy(SourceOrder).ThenBy(modifier => modifier.Id, StringComparer.Ordinal))
            .DistinctBy(modifier => modifier.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in sourceCandidates)
        {
            var providerKind = ProviderKindFor(modifier);
            if (providerKind is null)
            {
                continue;
            }

            if (!ReferenceEquals(modifier, targetContext.SourceModifier))
            {
                continue;
            }

            var lookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                effect.CanonicalSignature
                    .Replace("+<number>", "+#", StringComparison.Ordinal)
                    .Replace("-<number>", "-#", StringComparison.Ordinal)
                    .Replace("<number>", "#", StringComparison.Ordinal));
            var candidates = tradeCatalog.FindCandidateGroupsByNormalizedTemplate(lookup)
                .SelectMany(group => group.Candidates)
                .Where(candidate => string.Equals(
                    PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
                    providerKind.ToLowerInvariant(),
                    StringComparison.Ordinal) &&
                    PathOfExileTradeProviderLocalityCompatibility.EvaluateExactGameDataMatch(
                        effect.Locality,
                        hasExactGameDataProvenance: true,
                        candidate).IsCompatible)
                .DistinctBy(candidate => candidate.StatId, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 1)
            {
                return (modifier, candidates[0]);
            }
        }

        return null;
    }

    private static ResolvedSearchComponent CreateComponent(
        EffectPrototype effect,
        (ModifierDefinition Modifier, PathOfExileTradeStatMatchCandidate Candidate) source,
        ItemBaseRecord itemBase,
        GameDataCatalog gameDataCatalog)
    {
        var component = PrototypeComponent(effect, source.Modifier);
        var evaluations = ModifierProviderDomainEligibilityIndex.For(gameDataCatalog).Evaluate(
            component,
            source.Modifier,
            ItemModifierEligibilityContext.ForItemBase(itemBase));
        var sourceDomain = ProviderKindFor(source.Modifier) ?? "Unknown";
        return component with
        {
            ProviderDomainEvidence =
            [
                new SearchComponentProviderDomainEvidence
                {
                    ProviderDomain = sourceDomain,
                    ModifierId = source.Modifier.Id!,
                    GenerationType = source.Modifier.GenerationType,
                    SourceGenerationType = source.Modifier.SourceGenerationType,
                    IsSourceExact = true,
                    EvidenceStrength = 1000,
                    ItemBaseId = itemBase.Id,
                    ItemClass = itemBase.ItemClass,
                    ApplicabilityReasonCode = "SOURCE_EXACT",
                    ApplicabilityReason = "Audit source identity.",
                },
                .. evaluations
                    .Where(evaluation => evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported)
                    .Select(evaluation => new SearchComponentProviderDomainEvidence
                    {
                        ProviderDomain = evaluation.ProviderDomain,
                        ModifierId = evaluation.Modifier.Id!,
                        GenerationType = evaluation.Modifier.GenerationType,
                        SourceGenerationType = evaluation.Modifier.SourceGenerationType,
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

    private static ResolvedSearchComponent PrototypeComponent(
        EffectPrototype effect,
        ModifierDefinition modifier)
    {
        var providerKind = ProviderKindFor(modifier);
        return new ResolvedSearchComponent
        {
            ComponentId = $"audit:{modifier.Id}:{effect.TranslationIdentity.GetHashCode(StringComparison.Ordinal)}",
            OriginalText = effect.CanonicalSignature,
            CanonicalSignature = effect.CanonicalSignature,
            ParsedKind = providerKind == "Implicit"
                ? ParsedModifierKind.Implicit
                : modifier.GenerationType == ModifierGenerationType.Prefix
                    ? ParsedModifierKind.Prefix
                    : ParsedModifierKind.Suffix,
            GenerationType = modifier.GenerationType,
            Locality = effect.Locality,
            IsCrafted = providerKind == "Crafted",
            IsVeiled = providerKind == "Veiled",
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = modifier.Id,
            ResolvedModifierName = modifier.Name,
            ResolvedStatIds = effect.StatIds,
            IsSearchable = true,
            SupportsValueBounds = effect.ValueShape == ModifierBoundShape.Scalar,
            ValueBoundShape = effect.ValueShape,
            ValueBoundTranslationIdentity = effect.TranslationIdentity,
            ValueBoundTranslationHandlers = effect.TranslationHandlers,
            DefaultBoundDirection = ModifierBoundDirection.Minimum,
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> BuildBaseIndex(
        IReadOnlyList<ItemBaseRecord> itemBases)
    {
        var entries = new Dictionary<string, List<ItemBaseRecord>>(StringComparer.OrdinalIgnoreCase);
        foreach (var itemBase in itemBases)
        {
            var domain = Normalize(itemBase.Domain);
            if (domain is null)
            {
                continue;
            }

            foreach (var tag in itemBase.Tags.Append("default").Select(Normalize).Where(tag => tag is not null))
            {
                var key = BaseIndexKey(domain, tag!);
                if (!entries.TryGetValue(key, out var values))
                {
                    values = [];
                    entries.Add(key, values);
                }

                values.Add(itemBase);
            }

            foreach (var implicitModifierId in itemBase.ImplicitModifierIds.Select(Normalize).Where(id => id is not null))
            {
                var key = BaseIndexKey("implicit", implicitModifierId!);
                if (!entries.TryGetValue(key, out var values))
                {
                    values = [];
                    entries.Add(key, values);
                }

                values.Add(itemBase);
            }
        }

        return entries.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<ItemBaseRecord>)entry.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ModifierStat> SelectTranslationStats(
        IReadOnlyList<ModifierStat> modifierStats,
        IReadOnlyList<string> statIds)
    {
        var unused = modifierStats.ToList();
        var selected = new List<ModifierStat>(statIds.Count);
        foreach (var statId in statIds)
        {
            var index = unused.FindIndex(stat => string.Equals(
                Normalize(stat.StatId),
                Normalize(statId),
                StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return [];
            }

            selected.Add(unused[index]);
            unused.RemoveAt(index);
        }

        return selected;
    }

    private static bool TryCreateCanonicalSignature(
        StatTranslationVariant variant,
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

            if (variant.ValueFormats[index] is "#" or "+#")
            {
                indexes.Add(index);
            }

            value = value.Replace($"{{{index}}}", replacement, StringComparison.Ordinal);
        }

        signature = value.Trim();
        numericIndexes = indexes;
        return !string.IsNullOrWhiteSpace(signature);
    }

    private static string TranslationIdentity(
        StatTranslationDefinition translation,
        StatTranslationVariant variant,
        IReadOnlyList<int> numericIndexes)
    {
        var numeric = numericIndexes.Select(index => string.Join(
            "\u001f",
            translation.StatIds[index],
            variant.ValueFormats[index],
            string.Join("\u001d", variant.IndexHandlers
                .SingleOrDefault(handler => handler.Index == index)?.Handlers ?? ["<missing>"])));
        return string.Join("\u001e", variant.FormatLines.Concat(numeric));
    }

    private static string? ProviderKindFor(ModifierDefinition modifier)
    {
        var domain = Normalize(modifier.Domain);
        var sourceGeneration = Normalize(modifier.SourceGenerationType);
        var id = Normalize(modifier.Id);
        if (domain == "crafted") return "Crafted";
        if (domain is "unveiled" or "veiled") return "Veiled";
        if (sourceGeneration?.Contains("enchant", StringComparison.OrdinalIgnoreCase) == true ||
            modifier.GenerationType == ModifierGenerationType.Enchantment) return "Enchant";
        if (sourceGeneration?.Contains("scourge", StringComparison.OrdinalIgnoreCase) == true) return "Scourge";
        if (sourceGeneration?.Contains("implicit", StringComparison.OrdinalIgnoreCase) == true ||
            sourceGeneration?.Contains("corrupted", StringComparison.OrdinalIgnoreCase) == true ||
            sourceGeneration?.Contains("talisman", StringComparison.OrdinalIgnoreCase) == true ||
            sourceGeneration == "unique" && id?.Contains("implicit", StringComparison.OrdinalIgnoreCase) == true ||
            modifier.GenerationType == ModifierGenerationType.Implicit &&
                id?.Contains("implicit", StringComparison.OrdinalIgnoreCase) == true) return "Implicit";
        if (modifier.GenerationType is ModifierGenerationType.Prefix or ModifierGenerationType.Suffix) return "Explicit";
        return null;
    }

    private static bool IsLocalityRejection(string reason)
    {
        return reason.EndsWith(
                PathOfExileTradeProviderLocalityCompatibility.ExplicitLocalityConflict,
                StringComparison.Ordinal) ||
            reason.EndsWith(
                PathOfExileTradeProviderLocalityCompatibility.AmbiguousLocalityEvidence,
                StringComparison.Ordinal) ||
            reason.EndsWith(
                PathOfExileTradeProviderLocalityCompatibility.InsufficientLocalityEvidence,
                StringComparison.Ordinal);
    }

    private static int SourceOrder(ModifierDefinition modifier)
    {
        return ProviderKindFor(modifier) switch
        {
            "Explicit" => 0,
            "Crafted" => 1,
            "Implicit" => 2,
            "Enchant" => 3,
            _ => 4,
        };
    }

    private static string DuplicateResolution(PathOfExileTradeModifierVariantCandidateTrace trace)
    {
        return trace.RejectionReason is
            PathOfExileTradeModifierVariantDiscovery.DuplicateProviderStatId or
            PathOfExileTradeModifierVariantDiscovery.DuplicateCanonicalIdentity or
            PathOfExileTradeModifierVariantDiscovery.WeakerSemanticProvenance
            ? trace.RejectionReason
            : trace.IsAccepted ? "Retained" : "NotApplicable";
    }

    private static string BaseIndexKey(string domain, string tag) => $"{domain}\u001f{tag}";

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record EffectPrototype(
        string CanonicalEffect,
        string CanonicalSignature,
        IReadOnlyList<string> StatIds,
        ModifierLocality Locality,
        ModifierBoundShape ValueShape,
        string TranslationIdentity,
        IReadOnlyList<IReadOnlyList<string>> TranslationHandlers,
        IReadOnlyList<ModifierDefinition> Modifiers);

    private sealed record TargetContext(
        ItemBaseRecord ItemBase,
        string MatchedTag,
        ModifierDefinition SourceModifier);
}

internal enum PathOfExileTradeModifierDomainEligibilityAuditStatus
{
    Supported,
    Rejected,
    Ambiguous,
}

internal sealed record PathOfExileTradeModifierDomainEligibilityAuditReport
{
    public int CanonicalEffectsInspected { get; init; }

    public int ItemContextCombinationsInspected { get; init; }

    public int ProviderCandidatesInspected { get; init; }

    public IReadOnlyDictionary<string, int> SupportedVariantsByProviderKind { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> RejectedVariantsByReason { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public int AmbiguousVariants { get; init; }

    public int DuplicateIdentitiesRemoved { get; init; }

    public int EffectsWithNoValidProviderVariant { get; init; }

    public IReadOnlyList<PathOfExileTradeModifierDomainEligibilityAuditCombination> Combinations { get; init; } = [];
}

internal sealed record PathOfExileTradeModifierDomainEligibilityAuditCombination
{
    public required string CanonicalEffect { get; init; }

    public required string CanonicalSignature { get; init; }

    public IReadOnlyList<string> InternalStatIds { get; init; } = [];

    public ModifierLocality Locality { get; init; }

    public required string ItemClass { get; init; }

    public required string ItemBaseId { get; init; }

    public required string MatchedContextTag { get; init; }

    public required string ProviderKind { get; init; }

    public required string ProviderStatId { get; init; }

    public IReadOnlyList<string> SourceFamilyEvidence { get; init; } = [];

    public PathOfExileTradeModifierDomainEligibilityAuditStatus Status { get; init; }

    public required string Reason { get; init; }

    public bool LocalityCompatible { get; init; }

    public bool UnitCompatible { get; init; }

    public bool ArityCompatible { get; init; }

    public bool TransformCompatible { get; init; }

    public required string DuplicateResolution { get; init; }
}
