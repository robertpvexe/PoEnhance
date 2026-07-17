using System.Collections.Immutable;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

internal static class TradeSearchItemPropertyContributionGroupBuilder
{
    public static ImmutableArray<TradeSearchItemPropertyContributionGroup> Create(
        ImmutableArray<TradeSearchItemProperty> itemProperties,
        IReadOnlyList<ResolvedSearchComponent> modifierFilters)
    {
        ArgumentNullException.ThrowIfNull(modifierFilters);

        var availableParents = itemProperties
            .Select(property => property.Kind)
            .ToHashSet();
        var contributionsByParent = new Dictionary<
            TradeSearchItemPropertyKind,
            List<TradeSearchItemPropertyContribution>>();
        var retainedLinks = new HashSet<ContributionLink>();

        for (var modifierFilterIndex = 0; modifierFilterIndex < modifierFilters.Count; modifierFilterIndex++)
        {
            var semantic = modifierFilters[modifierFilterIndex].ReviewedItemPropertySemantic;
            if (semantic is null ||
                semantic.Applicability != ItemPropertyApplicability.UnconditionalDisplayedLocal)
            {
                continue;
            }

            foreach (var semanticContribution in semantic.Contributions)
            {
                if (semanticContribution.Operation is not (
                    ItemPropertyOperation.Added or
                    ItemPropertyOperation.IncreasedPercent))
                {
                    continue;
                }

                foreach (var target in semanticContribution.Targets)
                {
                    if (!TryMapParent(target, out var parentKind) ||
                        !availableParents.Contains(parentKind) ||
                        !retainedLinks.Add(new ContributionLink(
                            modifierFilterIndex,
                            target,
                            semanticContribution.Operation)))
                    {
                        continue;
                    }

                    if (!contributionsByParent.TryGetValue(parentKind, out var contributions))
                    {
                        contributions = [];
                        contributionsByParent.Add(parentKind, contributions);
                    }

                    contributions.Add(new TradeSearchItemPropertyContribution
                    {
                        ModifierFilterIndex = modifierFilterIndex,
                        Target = target,
                        Operation = semanticContribution.Operation,
                        ReviewedSemanticDescriptorId = semantic.Id,
                    });
                }
            }
        }

        var groups = ImmutableArray.CreateBuilder<TradeSearchItemPropertyContributionGroup>();
        var emittedParents = new HashSet<TradeSearchItemPropertyKind>();
        foreach (var itemProperty in itemProperties)
        {
            if (!emittedParents.Add(itemProperty.Kind) ||
                !contributionsByParent.TryGetValue(itemProperty.Kind, out var contributions) ||
                contributions.Count == 0)
            {
                continue;
            }

            groups.Add(new TradeSearchItemPropertyContributionGroup
            {
                ParentKind = itemProperty.Kind,
                Contributions = contributions.ToImmutableArray(),
            });
        }

        return groups.ToImmutable();
    }

    private static bool TryMapParent(
        ItemPropertyTarget target,
        out TradeSearchItemPropertyKind parentKind)
    {
        parentKind = target switch
        {
            ItemPropertyTarget.PhysicalDamage => TradeSearchItemPropertyKind.PhysicalDps,
            ItemPropertyTarget.FireDamage or
                ItemPropertyTarget.ColdDamage or
                ItemPropertyTarget.LightningDamage => TradeSearchItemPropertyKind.ElementalDps,
            ItemPropertyTarget.ChaosDamage => TradeSearchItemPropertyKind.ChaosDps,
            ItemPropertyTarget.AttacksPerSecond => TradeSearchItemPropertyKind.AttacksPerSecond,
            ItemPropertyTarget.CriticalStrikeChance => TradeSearchItemPropertyKind.CriticalStrikeChance,
            ItemPropertyTarget.EnergyShield => TradeSearchItemPropertyKind.EnergyShield,
            ItemPropertyTarget.Armour => TradeSearchItemPropertyKind.Armour,
            ItemPropertyTarget.Evasion => TradeSearchItemPropertyKind.EvasionRating,
            ItemPropertyTarget.Ward => TradeSearchItemPropertyKind.Ward,
            ItemPropertyTarget.Block => TradeSearchItemPropertyKind.ChanceToBlock,
            _ => default,
        };
        return target is
            ItemPropertyTarget.PhysicalDamage or
            ItemPropertyTarget.FireDamage or
            ItemPropertyTarget.ColdDamage or
            ItemPropertyTarget.LightningDamage or
            ItemPropertyTarget.ChaosDamage or
            ItemPropertyTarget.AttacksPerSecond or
            ItemPropertyTarget.CriticalStrikeChance or
            ItemPropertyTarget.EnergyShield or
            ItemPropertyTarget.Armour or
            ItemPropertyTarget.Evasion or
            ItemPropertyTarget.Ward or
            ItemPropertyTarget.Block;
    }

    private readonly record struct ContributionLink(
        int ModifierFilterIndex,
        ItemPropertyTarget Target,
        ItemPropertyOperation Operation);
}
