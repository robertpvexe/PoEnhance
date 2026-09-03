using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

/// <summary>
/// Shared Price Checker preparation after clipboard text is already captured:
/// wait for Runtime GameData if needed, then parse/resolve with a non-null catalog.
/// </summary>
internal static class PriceCheckerCapturedTextPreparation
{
    public static async Task<PriceCheckerCapturedTextPreparationResult> PrepareAsync(
        string rawText,
        RuntimeGameDataService runtimeGameDataService,
        ItemTextParser itemTextParser,
        ParsedItemGameDataDisplayService itemGameDataDisplayService,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawText);
        ArgumentNullException.ThrowIfNull(runtimeGameDataService);
        ArgumentNullException.ThrowIfNull(itemTextParser);
        ArgumentNullException.ThrowIfNull(itemGameDataDisplayService);

        var readiness = await PriceCheckerRuntimeGameDataGate
            .EnsureCatalogReadyAsync(runtimeGameDataService, cancellationToken)
            .ConfigureAwait(false);
        if (!readiness.IsReady || readiness.Catalog is null)
        {
            return PriceCheckerCapturedTextPreparationResult.Unavailable(readiness);
        }

        var catalog = readiness.Catalog;
        var parsedItem = itemTextParser.Parse(rawText);
        var itemBaseResolution = itemGameDataDisplayService.ResolveItemBase(parsedItem, catalog);
        var modifierCandidateResolutions = itemGameDataDisplayService.ResolveModifierCandidates(
            parsedItem,
            catalog,
            itemBaseResolution.Result);

        return PriceCheckerCapturedTextPreparationResult.Ready(
            readiness,
            parsedItem,
            itemBaseResolution,
            modifierCandidateResolutions,
            catalog);
    }
}

internal sealed record PriceCheckerCapturedTextPreparationResult
{
    public bool IsReady { get; private init; }

    public PriceCheckerRuntimeGameDataGateResult Readiness { get; private init; } =
        PriceCheckerRuntimeGameDataGateResult.Unavailable(new RuntimeGameDataStatus());

    public ParsedItem? ParsedItem { get; private init; }

    public ItemBaseResolutionDisplay? ItemBaseResolution { get; private init; }

    public ModifierCandidateResolutionsDisplay? ModifierCandidateResolutions { get; private init; }

    public GameDataCatalog? Catalog { get; private init; }

    public string UserFacingStatus => Readiness.UserFacingStatus;

    public static PriceCheckerCapturedTextPreparationResult Ready(
        PriceCheckerRuntimeGameDataGateResult readiness,
        ParsedItem parsedItem,
        ItemBaseResolutionDisplay itemBaseResolution,
        ModifierCandidateResolutionsDisplay modifierCandidateResolutions,
        GameDataCatalog catalog)
    {
        return new PriceCheckerCapturedTextPreparationResult
        {
            IsReady = true,
            Readiness = readiness,
            ParsedItem = parsedItem,
            ItemBaseResolution = itemBaseResolution,
            ModifierCandidateResolutions = modifierCandidateResolutions,
            Catalog = catalog,
        };
    }

    public static PriceCheckerCapturedTextPreparationResult Unavailable(
        PriceCheckerRuntimeGameDataGateResult readiness)
    {
        return new PriceCheckerCapturedTextPreparationResult
        {
            IsReady = false,
            Readiness = readiness,
        };
    }
}
