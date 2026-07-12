using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

internal interface IParsedItemModifierCandidateResolver
{
    IReadOnlyList<ModifierCandidateResolutionResult> Resolve(ParsedItem parsedItem, GameDataCatalog catalog);
}
