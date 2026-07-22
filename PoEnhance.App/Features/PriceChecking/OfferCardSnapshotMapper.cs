using System.Collections.Immutable;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Features.PriceChecking;

internal static class OfferCardSnapshotMapper
{
    public static OfferCardSnapshot Create(PathOfExileTradeFetchedOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var item = offer.Item;
        var listing = offer.Listing;
        return new OfferCardSnapshot
        {
            OfferId = offer.Id.Trim(),
            ItemId = item.Id,
            Name = item.Name,
            TypeLine = item.TypeLine,
            BaseType = item.BaseType,
            Frame = MapFrame(item.FrameType),
            Rarity = item.Rarity,
            IconReference = item.Icon,
            ItemLevel = item.ItemLevel,
            Flags = new OfferCardItemFlags
            {
                Identified = item.Identified,
                Corrupted = item.Corrupted,
                Mirrored = item.Mirrored,
                Split = item.Split,
                Synthesised = item.Synthesised,
                Fractured = item.Fractured,
                Duplicated = item.Duplicated,
                Replica = item.Replica,
                Veiled = item.Veiled,
                IsRelic = item.IsRelic,
                Ruthless = item.Ruthless,
            },
            Influences = new OfferCardInfluenceFacts
            {
                Shaper = item.Influences?.Shaper,
                Elder = item.Influences?.Elder,
                Crusader = item.Influences?.Crusader,
                Hunter = item.Influences?.Hunter,
                Redeemer = item.Influences?.Redeemer,
                Warlord = item.Influences?.Warlord,
                Searing = item.Searing,
                Tangled = item.Tangled,
            },
            Properties = item.Properties.Select(MapProperty).ToImmutableArray(),
            Requirements = item.Requirements.Select(MapProperty).ToImmutableArray(),
            Sockets = item.Sockets
                .Select((socket, index) => new OfferCardSocket
                {
                    Index = index,
                    Group = socket.Group,
                    Attribute = socket.Attribute,
                    Colour = socket.Colour,
                })
                .ToImmutableArray(),
            ModifierSections = MapModifierSections(item),
            Description = item.Description,
            SecondaryDescription = item.SecondaryDescription,
            FlavourText = item.FlavourText.ToImmutableArray(),
            Price = listing.Price is null
                ? null
                : new OfferCardPrice
                {
                    Type = listing.Price.Type,
                    Amount = listing.Price.Amount,
                    Currency = listing.Price.Currency,
                },
            Seller = new OfferCardSeller
            {
                AccountName = listing.Account?.Name,
                LastCharacterName = listing.Account?.LastCharacterName,
            },
            Online = listing.Account?.Online is null
                ? null
                : new OfferCardOnlineState
                {
                    League = listing.Account.Online.League,
                    Status = listing.Account.Online.Status,
                },
            IndexedAt = listing.Indexed,
            ModifierPipelineSource = new OfferCardModifierPipelineSource
            {
                RawFetchOfferPresent = item.ModifierDiagnostics.RawFetchOfferPresent,
                RawJson = MapModifierCounts(item.ModifierDiagnostics.RawJsonCounts),
                ParsedDto = new OfferCardModifierCounts
                {
                    Enchant = item.EnchantMods.Count,
                    Implicit = item.ImplicitMods.Count,
                    Explicit = item.ExplicitMods.Count,
                    Crafted = item.CraftedMods.Count,
                    Fractured = item.FracturedMods.Count,
                    Utility = item.UtilityMods.Count,
                    Cosmetic = item.CosmeticMods.Count,
                },
            },
        };
    }

    private static OfferCardModifierCounts MapModifierCounts(
        PathOfExileTradeFetchedModifierCounts counts)
    {
        return new OfferCardModifierCounts
        {
            Enchant = counts.Enchant,
            Implicit = counts.Implicit,
            Explicit = counts.Explicit,
            Crafted = counts.Crafted,
            Fractured = counts.Fractured,
            Utility = counts.Utility,
            Cosmetic = counts.Cosmetic,
        };
    }

    private static OfferCardProperty MapProperty(PathOfExileTradeItemProperty property)
    {
        return new OfferCardProperty
        {
            DisplayName = property.Name,
            Values = property.Values
                .Select(value => new OfferCardPropertyValue
                {
                    Text = value.Text,
                    DisplayStyleCode = value.ValueType,
                })
                .ToImmutableArray(),
            DisplayMode = MapDisplayMode(property.DisplayMode),
            Progress = property.Progress,
            TypeCode = property.Type,
            Suffix = property.Suffix,
            IconReference = property.Icon,
        };
    }

    private static ImmutableArray<OfferCardModifierSection> MapModifierSections(
        PathOfExileTradeFetchedItem item)
    {
        var sections = ImmutableArray.CreateBuilder<OfferCardModifierSection>();
        AddSection(sections, OfferCardModifierProvenance.Enchant, item.EnchantMods);
        AddSection(sections, OfferCardModifierProvenance.Implicit, item.ImplicitMods);
        AddSection(sections, OfferCardModifierProvenance.Explicit, item.ExplicitMods);
        AddSection(sections, OfferCardModifierProvenance.Crafted, item.CraftedMods);
        AddSection(sections, OfferCardModifierProvenance.Fractured, item.FracturedMods);
        AddSection(sections, OfferCardModifierProvenance.Utility, item.UtilityMods);
        AddSection(sections, OfferCardModifierProvenance.Cosmetic, item.CosmeticMods);
        return sections.ToImmutable();
    }

    private static void AddSection(
        ImmutableArray<OfferCardModifierSection>.Builder sections,
        OfferCardModifierProvenance provenance,
        IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        sections.Add(new OfferCardModifierSection
        {
            Provenance = provenance,
            Lines = lines.ToImmutableArray(),
        });
    }

    private static OfferCardFrameKind? MapFrame(int? frameType)
    {
        return frameType switch
        {
            0 => OfferCardFrameKind.Normal,
            1 => OfferCardFrameKind.Magic,
            2 => OfferCardFrameKind.Rare,
            3 => OfferCardFrameKind.Unique,
            4 => OfferCardFrameKind.Gem,
            5 => OfferCardFrameKind.Currency,
            6 => OfferCardFrameKind.DivinationCard,
            7 => OfferCardFrameKind.Quest,
            8 => OfferCardFrameKind.Prophecy,
            9 => OfferCardFrameKind.Foil,
            10 => OfferCardFrameKind.SupporterFoil,
            11 => OfferCardFrameKind.Necropolis,
            12 => OfferCardFrameKind.Gold,
            13 => OfferCardFrameKind.BreachSkill,
            _ => null,
        };
    }

    private static OfferCardPropertyDisplayMode? MapDisplayMode(int? displayMode)
    {
        return displayMode switch
        {
            0 => OfferCardPropertyDisplayMode.NameThenValues,
            1 => OfferCardPropertyDisplayMode.ValuesThenName,
            2 => OfferCardPropertyDisplayMode.Progress,
            3 => OfferCardPropertyDisplayMode.ValuesInsertedIntoName,
            4 => OfferCardPropertyDisplayMode.Separator,
            _ => null,
        };
    }
}
