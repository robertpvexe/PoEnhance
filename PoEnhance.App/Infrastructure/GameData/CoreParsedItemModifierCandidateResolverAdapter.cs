using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed class CoreParsedItemModifierCandidateResolverAdapter : IParsedItemModifierCandidateResolver
{
    private readonly ParsedItemModifierCandidateResolver resolver = new();

    public IReadOnlyList<ModifierCandidateResolutionResult> Resolve(
        ParsedItem parsedItem,
        GameDataCatalog catalog,
        ItemBaseResolutionResult baseResolution)
    {
        return resolver.Resolve(parsedItem, catalog, baseResolution);
    }
}
