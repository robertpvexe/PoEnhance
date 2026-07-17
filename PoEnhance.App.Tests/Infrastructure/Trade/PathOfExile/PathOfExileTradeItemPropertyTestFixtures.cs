using System.Collections.Immutable;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeItemPropertyTestFixtures
{
    public const string OfficialFiltersJson = """
        {
          "result": [
            {
              "id": "type_filters",
              "title": "Type Filters",
              "filters": [
                {
                  "id": "category",
                  "text": "Item Category",
                  "option": {
                    "options": [
                      { "id": "weapon.oneaxe", "text": "One-Handed Axe" },
                      { "id": "weapon.bow", "text": "Bow" },
                      { "id": "armour.chest", "text": "Body Armour" }
                    ]
                  }
                }
              ]
            },
            {
              "id": "weapon_filters",
              "title": "Weapon Filters",
              "hidden": true,
              "filters": [
                { "id": "damage", "text": "Damage", "minMax": true },
                { "id": "aps", "text": "Attacks per Second", "minMax": true },
                { "id": "crit", "text": "Critical Chance", "minMax": true },
                { "id": "dps", "text": "Damage per Second", "minMax": true },
                { "id": "pdps", "text": "Physical DPS", "minMax": true },
                { "id": "edps", "text": "Elemental DPS", "minMax": true }
              ]
            }
          ]
        }
        """;

    public static PathOfExileTradeFilterCatalog OfficialCatalog()
    {
        var result = new PathOfExileTradeFiltersResponseParser().ParseFiltersResponse(OfficialFiltersJson);
        if (!result.IsSuccess || result.Catalog is null)
        {
            throw new InvalidOperationException("The official-shaped item-property test catalog did not parse.");
        }

        return result.Catalog;
    }

    public static TradeSearchDraft WeaponDraft(
        IEnumerable<TradeSearchItemProperty>? properties = null,
        bool categoryMode = false)
    {
        const string category = "One Hand Axes";
        var categoryCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = category,
        };
        var exactCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = category,
            ExactBaseName = "Reaver Axe",
        };
        return new TradeSearchDraft
        {
            ItemClass = "One Hand Axes",
            Rarity = "Rare",
            DisplayName = "Test Reaver Axe",
            ParsedBaseType = "Reaver Axe",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.reaver-axe",
                ResolvedBaseName = "Reaver Axe",
                Category = category,
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = "base.reaver-axe",
                    ExactBaseName = "Reaver Axe",
                    Category = category,
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = categoryCriterion,
                    ExactBase = exactCriterion,
                },
                ActiveCriterion = categoryMode ? categoryCriterion : exactCriterion,
            },
            ItemLevel = 84,
            ItemProperties = (properties ?? AllProperties()).ToImmutableArray(),
            ListingMode = TradeListingMode.InstantBuyout,
        };
    }

    public static TradeSearchDraft NonWeaponDraft(TradeSearchItemProperty property)
    {
        const string category = "Body Armour";
        return WeaponDraft([property]) with
        {
            ItemClass = "Body Armours",
            ParsedBaseType = "Titan Plate",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.titan-plate",
                ResolvedBaseName = "Titan Plate",
                Category = category,
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.Category,
                        Category = category,
                    },
                    ExactBase = new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.ExactBase,
                        Category = category,
                        ExactBaseName = "Titan Plate",
                    },
                },
                ActiveCriterion = new BaseSearchCriterion
                {
                    Mode = BaseSearchMode.ExactBase,
                    Category = category,
                    ExactBaseName = "Titan Plate",
                },
            },
        };
    }

    public static IReadOnlyList<TradeSearchItemProperty> AllProperties()
    {
        return
        [
            Property(TradeSearchItemPropertyKind.TotalDps, 437.45m),
            Property(TradeSearchItemPropertyKind.PhysicalDps, 169.065m),
            Property(TradeSearchItemPropertyKind.ElementalDps, 325m),
            Property(TradeSearchItemPropertyKind.ChaosDps, 42m),
            Property(TradeSearchItemPropertyKind.AttacksPerSecond, 1.20m),
            Property(TradeSearchItemPropertyKind.CriticalStrikeChance, 5.00m),
        ];
    }

    public static TradeSearchItemProperty Property(
        TradeSearchItemPropertyKind kind,
        decimal value,
        bool selected = false,
        decimal? maximum = null)
    {
        return new TradeSearchItemProperty
        {
            Kind = kind,
            Label = Label(kind),
            ObservedValue = value,
            RequestedMinimum = value,
            RequestedMaximum = maximum,
            IsSelected = selected,
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
            IsSearchable = false,
            NotSearchableReason = "Not resolved for test yet.",
            SourceProperties = Sources(kind),
        };
    }

    public static PathOfExileTradeNumericFilterDefinition NumericDefinition(
        string id,
        string text,
        bool supportsMinMax = true,
        int order = 0)
    {
        return new PathOfExileTradeNumericFilterDefinition
        {
            GroupProviderOrder = 1,
            ProviderOrder = order,
            GroupId = "weapon_filters",
            GroupTitle = "Weapon Filters",
            GroupHidden = true,
            FilterId = id,
            Text = text,
            SupportsMinMax = supportsMinMax,
        };
    }

    private static ImmutableArray<ParsedItemProperty> Sources(TradeSearchItemPropertyKind kind)
    {
        return kind switch
        {
            TradeSearchItemPropertyKind.TotalDps =>
                [Source("Physical Damage"), Source("Attacks per Second")],
            TradeSearchItemPropertyKind.PhysicalDps =>
                [Source("Physical Damage"), Source("Attacks per Second")],
            TradeSearchItemPropertyKind.ElementalDps =>
                [Source("Elemental Damage"), Source("Attacks per Second")],
            TradeSearchItemPropertyKind.ChaosDps =>
                [Source("Chaos Damage"), Source("Attacks per Second")],
            TradeSearchItemPropertyKind.AttacksPerSecond => [Source("Attacks per Second")],
            TradeSearchItemPropertyKind.CriticalStrikeChance => [Source("Critical Strike Chance")],
            _ => [],
        };
    }

    private static ParsedItemProperty Source(string name)
    {
        return new ParsedItemProperty(
            $"{name}: 1",
            name,
            "1",
            name.ToLowerInvariant(),
            0,
            []);
    }

    private static string Label(TradeSearchItemPropertyKind kind)
    {
        return kind switch
        {
            TradeSearchItemPropertyKind.TotalDps => "Total DPS",
            TradeSearchItemPropertyKind.PhysicalDps => "Physical DPS",
            TradeSearchItemPropertyKind.ElementalDps => "Elemental DPS",
            TradeSearchItemPropertyKind.ChaosDps => "Chaos DPS",
            TradeSearchItemPropertyKind.AttacksPerSecond => "Attacks per Second",
            TradeSearchItemPropertyKind.CriticalStrikeChance => "Critical Strike Chance",
            _ => kind.ToString(),
        };
    }
}
