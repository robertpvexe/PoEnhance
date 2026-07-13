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

    public string? Name { get; init; }

    public string? TypeLine { get; init; }

    public string? BaseType { get; init; }

    public string? Icon { get; init; }

    public int? ItemLevel { get; init; }

    public bool? Identified { get; init; }

    public bool? Corrupted { get; init; }

    public bool? Mirrored { get; init; }

    public IReadOnlyList<string> ImplicitMods { get; init; } = [];

    public IReadOnlyList<string> ExplicitMods { get; init; } = [];

    public IReadOnlyList<string> CraftedMods { get; init; } = [];

    public IReadOnlyList<string> FracturedMods { get; init; } = [];

    public IReadOnlyList<string> EnchantMods { get; init; } = [];
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
