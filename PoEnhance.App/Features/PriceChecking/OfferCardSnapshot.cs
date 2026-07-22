using System.Collections.Immutable;

namespace PoEnhance.App.Features.PriceChecking;

public sealed record OfferCardSnapshot
{
    public required string OfferId { get; init; }

    public string? ItemId { get; init; }

    public string? Name { get; init; }

    public string? TypeLine { get; init; }

    public string? BaseType { get; init; }

    public OfferCardFrameKind? Frame { get; init; }

    public string? Rarity { get; init; }

    public string? IconReference { get; init; }

    public int? ItemLevel { get; init; }

    public OfferCardItemFlags Flags { get; init; } = new();

    public OfferCardInfluenceFacts Influences { get; init; } = new();

    public ImmutableArray<OfferCardProperty> Properties { get; init; } = [];

    public ImmutableArray<OfferCardProperty> Requirements { get; init; } = [];

    public ImmutableArray<OfferCardSocket> Sockets { get; init; } = [];

    public ImmutableArray<OfferCardModifierSection> ModifierSections { get; init; } = [];

    public string? Description { get; init; }

    public string? SecondaryDescription { get; init; }

    public ImmutableArray<string> FlavourText { get; init; } = [];

    public OfferCardPrice? Price { get; init; }

    public OfferCardSeller Seller { get; init; } = new();

    public OfferCardOnlineState? Online { get; init; }

    public DateTimeOffset? IndexedAt { get; init; }
}

public enum OfferCardFrameKind
{
    Normal,
    Magic,
    Rare,
    Unique,
    Gem,
    Currency,
    DivinationCard,
    Quest,
    Prophecy,
    Foil,
    SupporterFoil,
    Necropolis,
    Gold,
    BreachSkill,
}

public sealed record OfferCardItemFlags
{
    public bool? Identified { get; init; }

    public bool? Corrupted { get; init; }

    public bool? Mirrored { get; init; }

    public bool? Split { get; init; }

    public bool? Synthesised { get; init; }

    public bool? Fractured { get; init; }

    public bool? Duplicated { get; init; }

    public bool? Replica { get; init; }

    public bool? Veiled { get; init; }

    public bool? IsRelic { get; init; }

    public bool? Ruthless { get; init; }
}

public sealed record OfferCardInfluenceFacts
{
    public bool? Shaper { get; init; }

    public bool? Elder { get; init; }

    public bool? Crusader { get; init; }

    public bool? Hunter { get; init; }

    public bool? Redeemer { get; init; }

    public bool? Warlord { get; init; }

    public bool? Searing { get; init; }

    public bool? Tangled { get; init; }
}

public sealed record OfferCardProperty
{
    public required string DisplayName { get; init; }

    public ImmutableArray<OfferCardPropertyValue> Values { get; init; } = [];

    public OfferCardPropertyDisplayMode? DisplayMode { get; init; }

    public double? Progress { get; init; }

    public int? TypeCode { get; init; }

    public string? Suffix { get; init; }

    public string? IconReference { get; init; }
}

public sealed record OfferCardPropertyValue
{
    public required string Text { get; init; }

    public required int DisplayStyleCode { get; init; }
}

public enum OfferCardPropertyDisplayMode
{
    NameThenValues,
    ValuesThenName,
    Progress,
    ValuesInsertedIntoName,
    Separator,
}

public sealed record OfferCardSocket
{
    public required int Index { get; init; }

    public required int Group { get; init; }

    public string? Attribute { get; init; }

    public string? Colour { get; init; }
}

public sealed record OfferCardModifierSection
{
    public required OfferCardModifierProvenance Provenance { get; init; }

    public ImmutableArray<string> Lines { get; init; } = [];
}

public enum OfferCardModifierProvenance
{
    Enchant,
    Implicit,
    Explicit,
    Crafted,
    Fractured,
    Utility,
    Cosmetic,
}

public sealed record OfferCardPrice
{
    public string? Type { get; init; }

    public decimal? Amount { get; init; }

    public string? Currency { get; init; }
}

public sealed record OfferCardSeller
{
    public string? AccountName { get; init; }

    public string? LastCharacterName { get; init; }
}

public sealed record OfferCardOnlineState
{
    public string? League { get; init; }

    public string? Status { get; init; }
}
