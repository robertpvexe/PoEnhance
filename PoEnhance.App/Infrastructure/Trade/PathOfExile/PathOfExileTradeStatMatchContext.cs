using PoEnhance.Core.Items.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatMatchContext
{
    public string? ItemClass { get; init; }

    public string? ParsedBaseType { get; init; }

    public ModifierLocality ModifierLocality { get; init; } = ModifierLocality.Unknown;

    public string? ResolvedModifierId { get; init; }

    public string? ResolvedModifierName { get; init; }

    public IReadOnlyList<string> InternalStatIds { get; init; } = [];
}
