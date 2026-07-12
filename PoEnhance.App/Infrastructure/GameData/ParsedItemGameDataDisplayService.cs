using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed class ParsedItemGameDataDisplayService
{
    private const int CandidateDisplayLimit = 5;
    private readonly IParsedItemBaseResolver resolver;

    public ParsedItemGameDataDisplayService()
        : this(new CoreParsedItemBaseResolverAdapter())
    {
    }

    public ParsedItemGameDataDisplayService(IParsedItemBaseResolver resolver)
    {
        this.resolver = resolver;
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

        var result = resolver.Resolve(parsedItem, catalog);
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
        };
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not detected" : value;
    }
}
