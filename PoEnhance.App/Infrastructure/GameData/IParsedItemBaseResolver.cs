using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

internal interface IParsedItemBaseResolver
{
    ItemBaseResolutionResult Resolve(ParsedItem parsedItem, GameDataCatalog catalog);
}
