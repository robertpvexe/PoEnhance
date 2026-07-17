using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeRequestedItemFilterMappingResourceLoader
{
    internal const string DefaultResourceName =
        "PoEnhance.App.Infrastructure.Trade.PathOfExile.Data.requested-item-filter-mappings.json";

    public PathOfExileTradeRequestedItemFilterMappingCatalog LoadDefaultOrThrow()
    {
        var assembly = typeof(PathOfExileTradeRequestedItemFilterMappingResourceLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(DefaultResourceName) ??
            throw new InvalidOperationException("The reviewed requested-item-filter mapping resource is missing.");
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            options.Converters.Add(new JsonStringEnumConverter<TradeSearchRequestedItemFilterKind>());
            var resource = JsonSerializer.Deserialize<Resource>(stream, options) ??
                throw new InvalidOperationException("The reviewed requested-item-filter mapping resource is empty.");
            Validate(resource);
            return new PathOfExileTradeRequestedItemFilterMappingCatalog(
                resource.ReviewReference!,
                resource.Mappings!);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The reviewed requested-item-filter mapping resource is malformed.",
                exception);
        }
    }

    private static void Validate(Resource resource)
    {
        var expectedKinds = Enum.GetValues<TradeSearchRequestedItemFilterKind>();
        if (resource.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(resource.ReviewReference) ||
            resource.Mappings is null ||
            resource.Mappings.Count != expectedKinds.Length ||
            resource.Mappings.Select(mapping => mapping.Kind).Distinct().Count() != expectedKinds.Length ||
            expectedKinds.Any(kind => resource.Mappings.All(mapping => mapping.Kind != kind)) ||
            resource.Mappings.Any(mapping =>
                string.IsNullOrWhiteSpace(mapping.ProviderGroupId) ||
                string.IsNullOrWhiteSpace(mapping.ProviderFilterId) ||
                string.IsNullOrWhiteSpace(mapping.ExpectedOfficialText) ||
                !mapping.RequiresNumericMinMax ||
                mapping.MinimumSupportedValue < 0 ||
                mapping.MaximumSupportedValue < mapping.MinimumSupportedValue))
        {
            throw new InvalidOperationException(
                "The reviewed requested-item-filter mapping resource has an unsupported or incomplete shape.");
        }
    }

    private sealed record Resource
    {
        public int SchemaVersion { get; init; }

        public string? ReviewReference { get; init; }

        public IReadOnlyList<PathOfExileTradeRequestedItemFilterMapping>? Mappings { get; init; }
    }
}
