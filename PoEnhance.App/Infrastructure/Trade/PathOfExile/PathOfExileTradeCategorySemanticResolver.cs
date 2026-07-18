using System.Text.Json;
using PoEnhance.Core.Items.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeCategorySemanticResolver
{
    private static readonly IReadOnlyDictionary<string, Mapping> Mappings = LoadReviewedMappings();

    public bool TryResolveExpectedOfficialText(
        string? itemClass,
        out string expectedOfficialText)
    {
        var identity = CanonicalItemClassIdentityResolver.Resolve(itemClass);
        if (identity.IsSupported &&
            identity.CanonicalItemClass is not null &&
            Mappings.TryGetValue(identity.CanonicalItemClass, out var mapping))
        {
            expectedOfficialText = mapping.ExpectedOfficialText;
            return true;
        }

        expectedOfficialText = null!;
        return false;
    }

    private static IReadOnlyDictionary<string, Mapping> LoadReviewedMappings()
    {
        const string resourceName =
            "PoEnhance.App.Infrastructure.Trade.PathOfExile.Data.item-class-category-mappings.json";
        var assembly = typeof(PathOfExileTradeCategorySemanticResolver).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException("The reviewed item-class category mapping resource is missing.");
        var resource = JsonSerializer.Deserialize<Resource>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }) ?? throw new InvalidOperationException(
                "The reviewed item-class category mapping resource is empty.");
        if (resource.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(resource.ReviewReference) ||
            resource.Mappings is null ||
            resource.Mappings.Count == 0 ||
            resource.Mappings.Any(mapping =>
                string.IsNullOrWhiteSpace(mapping.CanonicalItemClass) ||
                string.IsNullOrWhiteSpace(mapping.ExpectedOfficialText)) ||
            resource.Mappings
                .Select(mapping => mapping.CanonicalItemClass)
                .Distinct(StringComparer.Ordinal)
                .Count() != resource.Mappings.Count ||
            resource.Mappings.Any(mapping =>
            {
                var identity = CanonicalItemClassIdentityResolver.Resolve(mapping.CanonicalItemClass);
                return !identity.IsSupported ||
                    !string.Equals(
                        identity.CanonicalItemClass,
                        mapping.CanonicalItemClass,
                        StringComparison.Ordinal);
            }))
        {
            throw new InvalidOperationException(
                "The reviewed item-class category mapping resource has an unsupported or invalid shape.");
        }

        return resource.Mappings.ToDictionary(
            mapping => mapping.CanonicalItemClass,
            StringComparer.Ordinal);
    }

    private sealed record Resource
    {
        public int SchemaVersion { get; init; }

        public string? ReviewReference { get; init; }

        public IReadOnlyList<Mapping>? Mappings { get; init; }
    }

    private sealed record Mapping
    {
        public string CanonicalItemClass { get; init; } = string.Empty;

        public string ExpectedOfficialText { get; init; } = string.Empty;
    }
}
