using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using PoEnhance.Core.Items.GameData;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

/// <summary>
/// A session-local index over references in the loaded GameData catalog. It retains no
/// copied provider records and is released with the catalog that owns it.
/// </summary>
internal sealed class ModifierProviderDomainEligibilityIndex
{
    private const string ExplicitDomain = "Explicit";
    private const string CraftedDomain = "Crafted";
    private const string FracturedDomain = "Fractured";
    private const string ImplicitDomain = "Implicit";
    private const string EnchantDomain = "Enchant";
    private const string ScourgeDomain = "Scourge";

    private static readonly ConditionalWeakTable<GameDataCatalog, ModifierProviderDomainEligibilityIndex> Cache = new();

    private readonly GameDataCatalog catalog;
    private readonly HashSet<string> declaredBaseImplicitIds;
    private readonly IReadOnlyList<ItemContextMarker> itemContextMarkers;
    private readonly ConcurrentDictionary<string, IReadOnlyList<SemanticFamily>> semanticFamilyCache =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> translationIdentityCache =
        new(StringComparer.Ordinal);

    private ModifierProviderDomainEligibilityIndex(GameDataCatalog catalog)
    {
        this.catalog = catalog;
        declaredBaseImplicitIds = catalog.ItemBases
            .SelectMany(itemBase => itemBase.ImplicitModifierIds)
            .Select(Normalize)
            .Where(id => id is not null)
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        itemContextMarkers = BuildItemContextMarkers(catalog.ItemBases);
    }

    public static ModifierProviderDomainEligibilityIndex For(GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Cache.GetValue(catalog, static value => new ModifierProviderDomainEligibilityIndex(value));
    }

    public IReadOnlyList<ModifierProviderDomainEligibilityEvaluation> Evaluate(
        ResolvedSearchComponent component,
        ModifierDefinition sourceModifier,
        ItemModifierEligibilityContext itemContext)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(sourceModifier);
        ArgumentNullException.ThrowIfNull(itemContext);

        var sourceVector = NormalizeStatVector(component.ResolvedStatIds);
        if (sourceVector.Count == 0)
        {
            return [];
        }

        var sourceStats = SelectComponentStats(sourceModifier, sourceVector);
        var sourceTranslationIdentities = string.IsNullOrWhiteSpace(component.ValueBoundTranslationIdentity)
            ? ModifierBoundDefaults.FindTranslationIdentities(sourceModifier, sourceStats, catalog)
            : [component.ValueBoundTranslationIdentity!];
        var semanticKey = string.Join(
            '\u001f',
            component.Locality,
            string.Join('\u001e', sourceVector),
            string.Join('\u001e', sourceTranslationIdentities.OrderBy(value => value, StringComparer.Ordinal)));
        var semanticFamilies = semanticFamilyCache.GetOrAdd(semanticKey, _ =>
            catalog.FindModifiersByStatId(sourceVector[0])
                .DistinctBy(modifier => Normalize(modifier.Id), StringComparer.OrdinalIgnoreCase)
                .Select(modifier => new SemanticFamily(
                    modifier,
                    SelectComponentStats(modifier, sourceVector)))
                .Where(family => family.ComponentStats.Count == sourceVector.Count &&
                    NormalizeStatVector(family.ComponentStats.Select(stat => stat.StatId))
                        .SequenceEqual(sourceVector, StringComparer.OrdinalIgnoreCase) &&
                    DetermineLocality(family.ComponentStats) == component.Locality &&
                    HasCompatibleTranslations(
                        sourceTranslationIdentities,
                        family.Modifier,
                        family.ComponentStats))
                .ToArray());

        var direct = semanticFamilies
            .Select(family => EvaluateDirectFamily(family, itemContext))
            .ToArray();
        var applicableExplicit = direct
            .Where(evaluation => evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported &&
                string.Equals(evaluation.ProviderDomain, ExplicitDomain, StringComparison.Ordinal))
            .ToArray();
        var results = new List<ModifierProviderDomainEligibilityEvaluation>(direct.Length + applicableExplicit.Length);

        foreach (var evaluation in direct)
        {
            if (string.Equals(evaluation.ProviderDomain, CraftedDomain, StringComparison.Ordinal) &&
                evaluation.Status != ModifierProviderDomainEligibilityStatus.Supported)
            {
                var explicitProof = StrongestExplicitProof(applicableExplicit);
                results.Add(explicitProof is null
                    ? evaluation with
                    {
                        ReasonCode = ModifierProviderDomainEligibilityReasonCodes.CraftedTargetFamilyMissing,
                        Reason = "A compatible crafted family exists, but no normal explicit family proves this effect on the target item context.",
                    }
                    : evaluation with
                    {
                        Status = ModifierProviderDomainEligibilityStatus.Supported,
                        MatchedTag = explicitProof.MatchedTag,
                        EvidenceStrength = 80,
                        ReasonCode = ModifierProviderDomainEligibilityReasonCodes.CraftedFamilySupported,
                        Reason = $"Compatible crafted GameData family; target applicability is proven by explicit family '{explicitProof.Modifier.Id}'.",
                    });
                continue;
            }

            if (string.Equals(evaluation.ProviderDomain, ImplicitDomain, StringComparison.Ordinal) &&
                evaluation.Status != ModifierProviderDomainEligibilityStatus.Supported &&
                IsSpecialImplicitFamily(evaluation.Modifier))
            {
                results.Add(EvaluateSpecialImplicit(
                    evaluation,
                    itemContext.ItemBase,
                    applicableExplicit));
                continue;
            }

            results.Add(evaluation);
        }

        foreach (var explicitProof in applicableExplicit.Where(IsFaithfullyFracturable))
        {
            results.Add(explicitProof with
            {
                ProviderDomain = FracturedDomain,
                IsProjectedDomain = true,
                EvidenceStrength = 75,
                ReasonCode = ModifierProviderDomainEligibilityReasonCodes.FracturableExplicitFamilySupported,
                Reason = "A compatible normal explicit GameData family is applicable to the target without influence-only provenance and can faithfully back the Fractured provider domain.",
            });
        }

        return results
            .OrderByDescending(evaluation => evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported)
            .ThenByDescending(evaluation => evaluation.EvidenceStrength)
            .ThenBy(evaluation => evaluation.ProviderDomain, StringComparer.Ordinal)
            .ThenBy(evaluation => evaluation.Modifier.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private ModifierProviderDomainEligibilityEvaluation EvaluateDirectFamily(
        SemanticFamily family,
        ItemModifierEligibilityContext itemContext)
    {
        var providerDomain = ProviderDomainFor(family.Modifier);
        if (providerDomain is null)
        {
            return Rejected(
                family.Modifier,
                "Unknown",
                ModifierProviderDomainEligibilityReasonCodes.SourceFamilyDomainUnknown,
                "The GameData source generation/domain does not map to an official Trade modifier domain.");
        }

        if (string.Equals(providerDomain, CraftedDomain, StringComparison.Ordinal))
        {
            return Rejected(
                family.Modifier,
                providerDomain,
                ModifierProviderDomainEligibilityReasonCodes.CraftedTargetFamilyMissing,
                "Crafted families require a compatible target-applicable explicit family because RePoE crafted records do not carry bench item-class applicability.");
        }

        var modifierId = Normalize(family.Modifier.Id);
        if (string.Equals(providerDomain, ImplicitDomain, StringComparison.Ordinal) &&
            modifierId is not null &&
            itemContext.ItemBase.ImplicitModifierIds.Any(id => string.Equals(
                Normalize(id),
                modifierId,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Supported(
                family.Modifier,
                providerDomain,
                matchedTag: null,
                evidenceStrength: 100,
                ModifierProviderDomainEligibilityReasonCodes.BaseImplicitDeclared,
                "The resolved item base directly declares this implicit GameData source family.");
        }

        var eligibility = new ModifierEligibilityEvaluator().Evaluate(family.Modifier, itemContext);
        if (eligibility.Outcome == ModifierEligibilityOutcome.Eligible)
        {
            return Supported(
                family.Modifier,
                providerDomain,
                eligibility.MatchedTag,
                evidenceStrength: eligibility.MatchedDynamicTag ? 85 : 90,
                eligibility.ReasonCode,
                $"Applicable GameData source family ({eligibility.ReasonCode}, tag {eligibility.MatchedTag ?? "<none>"}).",
                eligibility.MatchedDynamicTag);
        }

        if (IsStructurallyApplicableDefaultFamily(family.Modifier, itemContext.ItemBase))
        {
            return Supported(
                family.Modifier,
                providerDomain,
                "default",
                evidenceStrength: 60,
                ModifierProviderDomainEligibilityReasonCodes.StructuralDefaultFamilySupported,
                "The source family has the target domain and only a zero default spawn marker; existing GameData resolution treats it as structurally applicable.");
        }

        return Rejected(
            family.Modifier,
            providerDomain,
            eligibility.ReasonCode,
            eligibility.Reason,
            eligibility.MatchedTag,
            eligibility.MatchedDynamicTag);
    }

    private ModifierProviderDomainEligibilityEvaluation EvaluateSpecialImplicit(
        ModifierProviderDomainEligibilityEvaluation evaluation,
        ItemBaseRecord itemBase,
        IReadOnlyList<ModifierProviderDomainEligibilityEvaluation> applicableExplicit)
    {
        var sourceMarkers = SourceContextMarkers(evaluation.Modifier);
        if (sourceMarkers.Count > 0)
        {
            var missingMarkers = sourceMarkers
                .Where(marker => !marker.AppliesTo(itemBase))
                .ToArray();
            var markerDisplay = string.Join(", ", sourceMarkers.Select(marker => marker.Display));
            return missingMarkers.Length > 0
                ? evaluation with
                {
                    ReasonCode = ModifierProviderDomainEligibilityReasonCodes.ImplicitSourceContextMismatch,
                    Reason = $"The implicit source-family context [{markerDisplay}] does not match all target item class/tag markers; missing [{string.Join(", ", missingMarkers.Select(marker => marker.Display))}].",
                }
                : evaluation with
                {
                    Status = ModifierProviderDomainEligibilityStatus.Supported,
                    MatchedTag = markerDisplay,
                    EvidenceStrength = 70,
                    ReasonCode = ModifierProviderDomainEligibilityReasonCodes.ImplicitSourceContextSupported,
                    Reason = $"The implicit source-family identity matches target GameData context markers [{markerDisplay}].",
                };
        }

        var explicitProof = StrongestExplicitProof(applicableExplicit);
        return explicitProof is null
            ? evaluation with
            {
                ReasonCode = ModifierProviderDomainEligibilityReasonCodes.ImplicitTargetFamilyMissing,
                Reason = "The implicit source family has no spawn/tag context and no compatible explicit family proves this logical effect on the target item context.",
            }
            : evaluation with
            {
                Status = ModifierProviderDomainEligibilityStatus.Supported,
                MatchedTag = explicitProof.MatchedTag,
                EvidenceStrength = 65,
                ReasonCode = ModifierProviderDomainEligibilityReasonCodes.ImplicitTargetFamilySupported,
                Reason = $"Compatible implicit source family; target effect applicability is proven by GameData family '{explicitProof.Modifier.Id}'.",
            };
    }

    private bool IsSpecialImplicitFamily(ModifierDefinition modifier)
    {
        var sourceGeneration = NormalizeKey(modifier.SourceGenerationType);
        var id = NormalizeKey(modifier.Id);
        return sourceGeneration.Contains("implicit", StringComparison.Ordinal) ||
            sourceGeneration.Contains("corrupted", StringComparison.Ordinal) ||
            sourceGeneration.Contains("talisman", StringComparison.Ordinal) ||
            sourceGeneration == "unique" && id.Contains("implicit", StringComparison.Ordinal) ||
            modifier.GenerationType == ModifierGenerationType.Implicit &&
                (id.Contains("implicit", StringComparison.Ordinal) ||
                    declaredBaseImplicitIds.Contains(Normalize(modifier.Id) ?? string.Empty));
    }

    private IReadOnlyList<ItemContextMarker> SourceContextMarkers(ModifierDefinition modifier)
    {
        var identity = NormalizeKey(modifier.Id);
        return itemContextMarkers
            .Where(marker => identity.Contains(marker.Key, StringComparison.Ordinal))
            .ToArray();
    }

    private bool HasCompatibleTranslations(
        IReadOnlyList<string> sourceIdentities,
        ModifierDefinition modifier,
        IReadOnlyList<ModifierStat> stats)
    {
        var cacheKey = string.Join(
            '\u001f',
            Normalize(modifier.Id),
            string.Join('\u001e', stats.Select(stat => stat.StatId)));
        var candidateIdentities = translationIdentityCache.GetOrAdd(cacheKey, _ =>
            ModifierBoundDefaults.FindTranslationIdentities(modifier, stats, catalog));
        return sourceIdentities.Count > 0 && candidateIdentities.Count > 0 &&
            sourceIdentities.Intersect(candidateIdentities, StringComparer.Ordinal).Any();
    }

    private static ModifierProviderDomainEligibilityEvaluation? StrongestExplicitProof(
        IReadOnlyList<ModifierProviderDomainEligibilityEvaluation> applicableExplicit)
    {
        return applicableExplicit
            .OrderByDescending(evaluation => evaluation.EvidenceStrength)
            .ThenBy(evaluation => evaluation.Modifier.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsFaithfullyFracturable(ModifierProviderDomainEligibilityEvaluation evaluation)
    {
        return !evaluation.MatchedDynamicTag &&
            evaluation.Modifier.GenerationType is ModifierGenerationType.Prefix or ModifierGenerationType.Suffix &&
            !string.Equals(Normalize(evaluation.Modifier.Domain), "crafted", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStructurallyApplicableDefaultFamily(
        ModifierDefinition modifier,
        ItemBaseRecord itemBase)
    {
        return string.Equals(
                Normalize(modifier.Domain),
                Normalize(itemBase.Domain),
                StringComparison.OrdinalIgnoreCase) &&
            modifier.SpawnWeights.Count > 0 &&
            modifier.SpawnWeights.All(spawnWeight =>
                string.Equals(Normalize(spawnWeight.Tag), "default", StringComparison.OrdinalIgnoreCase) &&
                spawnWeight.Weight == 0);
    }

    private static string? ProviderDomainFor(ModifierDefinition modifier)
    {
        var sourceGeneration = NormalizeKey(modifier.SourceGenerationType);
        var domain = NormalizeKey(modifier.Domain);
        var id = NormalizeKey(modifier.Id);

        if (domain is "crafted")
        {
            return CraftedDomain;
        }

        if (domain is "unveiled" or "veiled")
        {
            return "Veiled";
        }

        if (sourceGeneration.Contains("enchant", StringComparison.Ordinal) ||
            modifier.GenerationType == ModifierGenerationType.Enchantment)
        {
            return EnchantDomain;
        }

        if (sourceGeneration.Contains("scourge", StringComparison.Ordinal))
        {
            return ScourgeDomain;
        }

        if (sourceGeneration.Contains("implicit", StringComparison.Ordinal) ||
            sourceGeneration.Contains("corrupted", StringComparison.Ordinal) ||
            sourceGeneration.Contains("talisman", StringComparison.Ordinal) ||
            sourceGeneration == "unique" && id.Contains("implicit", StringComparison.Ordinal) ||
            modifier.GenerationType == ModifierGenerationType.Implicit && id.Contains("implicit", StringComparison.Ordinal))
        {
            return ImplicitDomain;
        }

        if (modifier.GenerationType is ModifierGenerationType.Prefix or ModifierGenerationType.Suffix &&
            domain is not "crafted")
        {
            return ExplicitDomain;
        }

        return null;
    }

    private static IReadOnlyList<ModifierStat> SelectComponentStats(
        ModifierDefinition modifier,
        IReadOnlyList<string> statVector)
    {
        var remaining = statVector.ToList();
        var selected = new List<ModifierStat>(remaining.Count);
        foreach (var stat in modifier.Stats.OrderBy(stat => stat.Index))
        {
            var index = remaining.FindIndex(statId => string.Equals(
                statId,
                Normalize(stat.StatId),
                StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                continue;
            }

            selected.Add(stat);
            remaining.RemoveAt(index);
        }

        return remaining.Count == 0 ? selected : [];
    }

    private ModifierLocality DetermineLocality(IReadOnlyList<ModifierStat> stats)
    {
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

    private static IReadOnlyList<ItemContextMarker> BuildItemContextMarkers(
        IReadOnlyList<ItemBaseRecord> itemBases)
    {
        var baseCount = Math.Max(1, itemBases.Count);
        return itemBases
            .SelectMany(itemBase => new[] { itemBase.ItemClass }.Concat(itemBase.Tags)
                .Select(value => new { ItemBase = itemBase, Value = NormalizeKey(value) }))
            .Where(entry => entry.Value.Length >= 4 &&
                entry.Value is not "default" and not "notforsale")
            .GroupBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(group => new ItemContextMarker(
                group.Key,
                group.First().Value,
                group.Select(entry => Normalize(entry.ItemBase.Id))
                    .Where(id => id is not null)
                    .Select(id => id!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .Where(marker => marker.ItemBaseIds.Count <= baseCount / 2)
            .OrderByDescending(marker => marker.Key.Length)
            .ThenBy(marker => marker.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static ModifierProviderDomainEligibilityEvaluation Supported(
        ModifierDefinition modifier,
        string providerDomain,
        string? matchedTag,
        int evidenceStrength,
        string reasonCode,
        string reason,
        bool matchedDynamicTag = false)
    {
        return new ModifierProviderDomainEligibilityEvaluation
        {
            Modifier = modifier,
            ProviderDomain = providerDomain,
            Status = ModifierProviderDomainEligibilityStatus.Supported,
            MatchedTag = matchedTag,
            MatchedDynamicTag = matchedDynamicTag,
            EvidenceStrength = evidenceStrength,
            ReasonCode = reasonCode,
            Reason = reason,
        };
    }

    private static ModifierProviderDomainEligibilityEvaluation Rejected(
        ModifierDefinition modifier,
        string providerDomain,
        string reasonCode,
        string reason,
        string? matchedTag = null,
        bool matchedDynamicTag = false)
    {
        return new ModifierProviderDomainEligibilityEvaluation
        {
            Modifier = modifier,
            ProviderDomain = providerDomain,
            Status = ModifierProviderDomainEligibilityStatus.Rejected,
            MatchedTag = matchedTag,
            MatchedDynamicTag = matchedDynamicTag,
            ReasonCode = reasonCode,
            Reason = reason,
        };
    }

    private static IReadOnlyList<string> NormalizeStatVector(IEnumerable<string?> statIds)
    {
        return statIds
            .Select(Normalize)
            .Where(statId => statId is not null)
            .Select(statId => statId!)
            .ToArray();
    }

    private static string NormalizeKey(string? value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record SemanticFamily(
        ModifierDefinition Modifier,
        IReadOnlyList<ModifierStat> ComponentStats);

    private sealed record ItemContextMarker(
        string Key,
        string Display,
        IReadOnlySet<string> ItemBaseIds)
    {
        public bool AppliesTo(ItemBaseRecord itemBase)
        {
            var itemBaseId = Normalize(itemBase.Id);
            return itemBaseId is not null && ItemBaseIds.Contains(itemBaseId);
        }
    }
}

internal enum ModifierProviderDomainEligibilityStatus
{
    Supported,
    Rejected,
}

internal sealed record ModifierProviderDomainEligibilityEvaluation
{
    public required ModifierDefinition Modifier { get; init; }

    public required string ProviderDomain { get; init; }

    public ModifierProviderDomainEligibilityStatus Status { get; init; }

    public string? MatchedTag { get; init; }

    public bool MatchedDynamicTag { get; init; }

    public bool IsProjectedDomain { get; init; }

    public int EvidenceStrength { get; init; }

    public required string ReasonCode { get; init; }

    public required string Reason { get; init; }
}

internal static class ModifierProviderDomainEligibilityReasonCodes
{
    public const string SourceFamilyDomainUnknown = "SOURCE_FAMILY_DOMAIN_UNKNOWN";
    public const string BaseImplicitDeclared = "BASE_IMPLICIT_DECLARED";
    public const string StructuralDefaultFamilySupported = "STRUCTURAL_DEFAULT_FAMILY_SUPPORTED";
    public const string CraftedFamilySupported = "CRAFTED_FAMILY_SUPPORTED";
    public const string CraftedTargetFamilyMissing = "CRAFTED_TARGET_FAMILY_MISSING";
    public const string FracturableExplicitFamilySupported = "FRACTURABLE_EXPLICIT_FAMILY_SUPPORTED";
    public const string ImplicitSourceContextSupported = "IMPLICIT_SOURCE_CONTEXT_SUPPORTED";
    public const string ImplicitSourceContextMismatch = "IMPLICIT_SOURCE_CONTEXT_MISMATCH";
    public const string ImplicitTargetFamilySupported = "IMPLICIT_TARGET_FAMILY_SUPPORTED";
    public const string ImplicitTargetFamilyMissing = "IMPLICIT_TARGET_FAMILY_MISSING";
}
