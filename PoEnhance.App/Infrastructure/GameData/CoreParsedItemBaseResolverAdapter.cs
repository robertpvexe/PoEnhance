using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed class CoreParsedItemBaseResolverAdapter : IParsedItemBaseResolver
{
    private readonly ParsedItemBaseResolver resolver = new();

    public ItemBaseResolutionResult Resolve(ParsedItem parsedItem, GameDataCatalog catalog)
    {
        return resolver.Resolve(parsedItem, catalog);
    }
}
