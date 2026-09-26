using PoEnhance.Core.Items.GameData;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatMatchContext
{
    public string? ItemClass { get; init; }

    public string? ParsedBaseType { get; init; }

    public ModifierLocality ModifierLocality { get; init; } = ModifierLocality.Unknown;

    public string? ResolvedModifierId { get; init; }

    public bool HasExactGameDataSourceProof { get; init; }

    public string? ResolvedModifierName { get; init; }

    public IReadOnlyList<string> InternalStatIds { get; init; } = [];

    public IReadOnlyList<ModifierLocality> InternalStatLocalities { get; init; } = [];

    /// <summary>
    /// Optional packaged GameData for translation-family provider discovery (TRADE.4c).
    /// </summary>
    public GameDataCatalog? GameDataCatalog { get; init; }
}
