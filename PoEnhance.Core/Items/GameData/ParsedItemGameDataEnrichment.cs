using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Items.GameData;

public sealed record ParsedItemGameDataEnrichment(
    ParsedItem ParsedItem,
    ItemBaseResolutionResult BaseResolution)
{
    public string? EffectiveBaseName => BaseResolution.ResolvedBaseName ?? ParsedItem.BaseType;
}
