using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeItemStateFilterResolver
{
    private readonly IReadOnlyDictionary<TradeItemStateKind, Mapping> mappings;

    public PathOfExileTradeItemStateFilterResolver()
    {
        mappings = LoadReviewedMappings();
    }

    public PathOfExileTradeItemStateFilterMappingResult MapSelected(
        TradeSearchDraft draft,
        PathOfExileTradeFilterCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(catalog);

        var filters = new List<PathOfExileTradeSelectedItemStateFilter>();
        var diagnostics = new List<string>();
        foreach (var kind in Enum.GetValues<TradeItemStateKind>())
        {
            var state = draft.ItemStateCriteria.Get(kind);
            if (state is TradeTriState.Any or TradeTriState.Auto)
            {
                continue;
            }

            if (!mappings.TryGetValue(kind, out var mapping))
            {
                diagnostics.Add($"No reviewed provider mapping exists for item state '{kind}'.");
                continue;
            }

            var definitions = catalog.FindOptionFilterDefinitions(
                mapping.ProviderGroupId,
                mapping.ProviderFilterId);
            var optionId = state == TradeTriState.Yes ? mapping.YesOptionId : mapping.NoOptionId;
            if (definitions.Count != 1 ||
                !string.Equals(definitions[0].Text, mapping.ExpectedOfficialText, StringComparison.Ordinal) ||
                !HasExactOption(definitions[0], mapping.YesOptionId, mapping.YesOptionText) ||
                !HasExactOption(definitions[0], mapping.NoOptionId, mapping.NoOptionText))
            {
                diagnostics.Add(
                    $"The official Trade catalog is incompatible with the reviewed '{kind}' state mapping.");
                continue;
            }

            filters.Add(new PathOfExileTradeSelectedItemStateFilter
            {
                SourceKind = kind,
                ProviderGroupId = mapping.ProviderGroupId,
                ProviderFilterId = mapping.ProviderFilterId,
                Option = optionId,
            });
        }

        return diagnostics.Count == 0
            ? PathOfExileTradeItemStateFilterMappingResult.Success(filters)
            : PathOfExileTradeItemStateFilterMappingResult.Failure(diagnostics);
    }

    private static bool HasExactOption(
        PathOfExileTradeOptionFilterDefinition definition,
        string optionId,
        string optionText)
    {
        var options = definition.Options
            .Where(option => string.Equals(option.Id, optionId, StringComparison.Ordinal))
            .ToArray();
        return options.Length == 1 && string.Equals(options[0].Text, optionText, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<TradeItemStateKind, Mapping> LoadReviewedMappings()
    {
        const string resourceName =
            "PoEnhance.App.Infrastructure.Trade.PathOfExile.Data.item-state-filter-mappings.json";
        var assembly = typeof(PathOfExileTradeItemStateFilterResolver).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException("The reviewed item-state filter mapping resource is missing.");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new JsonStringEnumConverter<TradeItemStateKind>());
        var resource = JsonSerializer.Deserialize<Resource>(stream, options) ??
            throw new InvalidOperationException("The reviewed item-state filter mapping resource is empty.");
        var kinds = Enum.GetValues<TradeItemStateKind>();
        if (resource.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(resource.ReviewReference) ||
            resource.Mappings is null ||
            resource.Mappings.Count != kinds.Length ||
            resource.Mappings.Select(mapping => mapping.Kind).Distinct().Count() != kinds.Length ||
            kinds.Any(kind => resource.Mappings.All(mapping => mapping.Kind != kind)) ||
            resource.Mappings.Any(mapping =>
                string.IsNullOrWhiteSpace(mapping.ProviderGroupId) ||
                string.IsNullOrWhiteSpace(mapping.ProviderFilterId) ||
                string.IsNullOrWhiteSpace(mapping.ExpectedOfficialText) ||
                string.IsNullOrWhiteSpace(mapping.YesOptionId) ||
                string.IsNullOrWhiteSpace(mapping.YesOptionText) ||
                string.IsNullOrWhiteSpace(mapping.NoOptionId) ||
                string.IsNullOrWhiteSpace(mapping.NoOptionText)))
        {
            throw new InvalidOperationException(
                "The reviewed item-state filter mapping resource has an unsupported or incomplete shape.");
        }

        return resource.Mappings.ToDictionary(mapping => mapping.Kind);
    }

    private sealed record Resource
    {
        public int SchemaVersion { get; init; }

        public string? ReviewReference { get; init; }

        public IReadOnlyList<Mapping>? Mappings { get; init; }
    }

    private sealed record Mapping
    {
        public TradeItemStateKind Kind { get; init; }

        public string ProviderGroupId { get; init; } = string.Empty;

        public string ProviderFilterId { get; init; } = string.Empty;

        public string ExpectedOfficialText { get; init; } = string.Empty;

        public string YesOptionId { get; init; } = string.Empty;

        public string YesOptionText { get; init; } = string.Empty;

        public string NoOptionId { get; init; } = string.Empty;

        public string NoOptionText { get; init; } = string.Empty;
    }
}

internal sealed record PathOfExileTradeSelectedItemStateFilter
{
    public required TradeItemStateKind SourceKind { get; init; }

    public required string ProviderGroupId { get; init; }

    public required string ProviderFilterId { get; init; }

    public required string Option { get; init; }
}

internal sealed record PathOfExileTradeItemStateFilterMappingResult
{
    public bool IsSuccess { get; init; }

    public IReadOnlyList<PathOfExileTradeSelectedItemStateFilter> Filters { get; init; } = [];

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public static PathOfExileTradeItemStateFilterMappingResult Success(
        IReadOnlyList<PathOfExileTradeSelectedItemStateFilter> filters) =>
        new() { IsSuccess = true, Filters = filters };

    public static PathOfExileTradeItemStateFilterMappingResult Failure(
        IReadOnlyList<string> diagnostics) =>
        new() { Diagnostics = diagnostics };
}
