using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed class ParsedItemGameDataDisplayService
{
    private const int CandidateDisplayLimit = 5;
    private readonly IParsedItemBaseResolver itemBaseResolver;
    private readonly IParsedItemModifierCandidateResolver modifierCandidateResolver;

    public ParsedItemGameDataDisplayService()
        : this(
            new CoreParsedItemBaseResolverAdapter(),
            new CoreParsedItemModifierCandidateResolverAdapter())
    {
    }

    public ParsedItemGameDataDisplayService(IParsedItemBaseResolver itemBaseResolver)
        : this(itemBaseResolver, new CoreParsedItemModifierCandidateResolverAdapter())
    {
    }

    public ParsedItemGameDataDisplayService(
        IParsedItemBaseResolver itemBaseResolver,
        IParsedItemModifierCandidateResolver modifierCandidateResolver)
    {
        this.itemBaseResolver = itemBaseResolver;
        this.modifierCandidateResolver = modifierCandidateResolver;
    }

    public ItemBaseResolutionDisplay ResolveItemBase(
        ParsedItem parsedItem,
        GameDataCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);

        if (catalog is null)
        {
            return new ItemBaseResolutionDisplay
            {
                IsAvailable = false,
                Status = "Unavailable",
                Diagnostic = "Game data not loaded",
            };
        }

        var result = itemBaseResolver.Resolve(parsedItem, catalog);
        var diagnostic = result.Diagnostics.FirstOrDefault();

        return new ItemBaseResolutionDisplay
        {
            IsAvailable = true,
            Status = result.Status.ToString(),
            ResolvedBaseName = DisplayValue(result.ResolvedBaseName),
            ResolvedBaseId = DisplayValue(result.ResolvedBaseId),
            Diagnostic = diagnostic is null
                ? "Not detected"
                : $"{diagnostic.Code}: {diagnostic.Reason}",
            CandidateCount = result.Candidates.Count,
            CandidateNames = result.Candidates
                .Select(candidate => candidate.Name)
                .Where(candidateName => !string.IsNullOrWhiteSpace(candidateName))
                .Take(CandidateDisplayLimit)
                .Cast<string>()
                .ToArray(),
            Result = result,
        };
    }

    public ModifierCandidateResolutionsDisplay ResolveModifierCandidates(
        ParsedItem parsedItem,
        GameDataCatalog? catalog,
        ItemBaseResolutionResult? baseResolution = null)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);

        if (catalog is null)
        {
            return new ModifierCandidateResolutionsDisplay
            {
                IsAvailable = false,
                Diagnostic = "Game data not loaded",
            };
        }

        baseResolution ??= itemBaseResolver.Resolve(parsedItem, catalog);
        var results = modifierCandidateResolver.Resolve(parsedItem, catalog, baseResolution);
        return new ModifierCandidateResolutionsDisplay
        {
            IsAvailable = true,
            Diagnostic = "Available",
            Results = results
                .Select(result =>
                {
                    var diagnostic = result.Diagnostics.FirstOrDefault();
                    return new ModifierCandidateResolutionItemDisplay
                    {
                        ParsedModifierIndex = result.ParsedModifierIndex,
                        ParsedModifier = result.ParsedModifier,
                        Status = result.Status.ToString(),
                        Diagnostic = diagnostic is null
                            ? "Not detected"
                            : $"{diagnostic.Code}: {diagnostic.Reason}",
                        CandidateCount = result.Candidates.Count,
                        CountSummary =
                            $"{result.NameCandidateCount} name -> {result.GenerationKindCandidateCount} kind -> {result.EligibilityCandidateCount} eligible",
                        CandidateLabels = result.Candidates
                            .Take(CandidateDisplayLimit)
                            .Select(FormatModifierCandidate)
                            .ToArray(),
                        ShowInRegularDisplay = diagnostic?.Code
                            != ModifierCandidateResolutionDiagnosticCodes.ModifierKindUnsupported,
                    };
                })
                .ToArray(),
        };
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not detected" : value;
    }

    private static string FormatModifierCandidate(ModifierDefinition candidate)
    {
        var name = DisplayValue(candidate.Name);
        return $"{DisplayValue(candidate.Id)} ({name})";
    }
}
