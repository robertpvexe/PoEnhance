namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeFetchResponse
{
    public required IReadOnlyList<PathOfExileTradeFetchedOffer> Result { get; init; }
}

internal sealed record PathOfExileTradeFetchedOffer
{
    public required string Id { get; init; }

    public required PathOfExileTradeFetchedItem Item { get; init; }

    public required PathOfExileTradeListing Listing { get; init; }
}

internal sealed record PathOfExileTradeFetchedItem
{
    public string? Id { get; init; }

    public int? FrameType { get; init; }

    public string? Rarity { get; init; }

    public string? Name { get; init; }

    public string? TypeLine { get; init; }

    public string? BaseType { get; init; }

    public string? Icon { get; init; }

    public int? ItemLevel { get; init; }

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

    public PathOfExileTradeItemInfluences? Influences { get; init; }

    public bool? Searing { get; init; }

    public bool? Tangled { get; init; }

    public IReadOnlyList<PathOfExileTradeItemProperty> Properties { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeItemProperty> Requirements { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeItemSocket> Sockets { get; init; } = [];

    public IReadOnlyList<string> ImplicitMods { get; init; } = [];

    public IReadOnlyList<string> ExplicitMods { get; init; } = [];

    public IReadOnlyList<string> CraftedMods { get; init; } = [];

    public IReadOnlyList<string> FracturedMods { get; init; } = [];

    public IReadOnlyList<string> EnchantMods { get; init; } = [];

    public IReadOnlyList<string> UtilityMods { get; init; } = [];

    public IReadOnlyList<string> CosmeticMods { get; init; } = [];

    public PathOfExileTradeFetchedModifierDiagnostics ModifierDiagnostics { get; init; } = new();

    public string? Description { get; init; }

    public string? SecondaryDescription { get; init; }

    public IReadOnlyList<string> FlavourText { get; init; } = [];
}

internal sealed record PathOfExileTradeFetchedModifierDiagnostics
{
    public bool RawFetchOfferPresent { get; init; }

    public PathOfExileTradeFetchedModifierCounts RawJsonCounts { get; init; } = new();

    public PathOfExileTradeFetchedModifierCounts ParsedDtoCounts { get; init; } = new();
}

internal sealed record PathOfExileTradeFetchedModifierCounts
{
    public int Enchant { get; init; }

    public int Implicit { get; init; }

    public int Explicit { get; init; }

    public int Crafted { get; init; }

    public int Fractured { get; init; }

    public int Utility { get; init; }

    public int Cosmetic { get; init; }

    public int Total => Enchant + Implicit + Explicit + Crafted + Fractured + Utility + Cosmetic;
}

internal sealed record PathOfExileTradeItemProperty
{
    public required string Name { get; init; }

    public required IReadOnlyList<PathOfExileTradeItemPropertyValue> Values { get; init; }

    public int? DisplayMode { get; init; }

    public double? Progress { get; init; }

    public int? Type { get; init; }

    public string? Suffix { get; init; }

    public string? Icon { get; init; }
}

internal sealed record PathOfExileTradeItemPropertyValue
{
    public required string Text { get; init; }

    public required int ValueType { get; init; }
}

internal sealed record PathOfExileTradeItemSocket
{
    public required int Group { get; init; }

    public string? Attribute { get; init; }

    public string? Colour { get; init; }
}

internal sealed record PathOfExileTradeItemInfluences
{
    public bool? Shaper { get; init; }

    public bool? Elder { get; init; }

    public bool? Crusader { get; init; }

    public bool? Hunter { get; init; }

    public bool? Redeemer { get; init; }

    public bool? Warlord { get; init; }
}

internal sealed record PathOfExileTradeListing
{
    public string? Method { get; init; }

    public DateTimeOffset? Indexed { get; init; }

    public string? RawIndexed { get; init; }

    public string? Whisper { get; init; }

    public PathOfExileTradeListingAccount? Account { get; init; }

    public PathOfExileTradeListingPrice? Price { get; init; }
}

internal sealed record PathOfExileTradeListingAccount
{
    public string? Name { get; init; }

    public string? LastCharacterName { get; init; }

    public PathOfExileTradeListingOnlineState? Online { get; init; }
}

internal sealed record PathOfExileTradeListingOnlineState
{
    public string? League { get; init; }

    public string? Status { get; init; }
}

internal sealed record PathOfExileTradeListingPrice
{
    public string? Type { get; init; }

    public decimal? Amount { get; init; }

    public string? Currency { get; init; }
}
