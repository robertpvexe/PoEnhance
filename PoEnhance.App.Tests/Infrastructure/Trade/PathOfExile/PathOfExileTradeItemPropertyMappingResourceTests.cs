using System.Reflection;
using System.Text;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeItemPropertyMappingResourceTests
{
    [Fact]
    public void DefaultResource_LoadsFiveSupportedMappingsAndExplicitUnsupportedChaos()
    {
        var catalog = new PathOfExileTradeItemPropertyMappingResourceLoader().LoadDefaultOrThrow();

        Assert.Equal("item-property-trade-mapping-audit-2026-07-17", catalog.ReviewReference);
        Assert.Equal(5, catalog.Mappings.Count(mapping => mapping.IsSupported));
        var chaos = Assert.Single(catalog.Mappings, mapping =>
            mapping.Kind == TradeSearchItemPropertyKind.ChaosDps);
        Assert.False(chaos.IsSupported);
        Assert.Null(chaos.ProviderGroupId);
        Assert.Null(chaos.ProviderFilterId);
        Assert.Contains("does not expose a Chaos DPS", chaos.UnsupportedReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_DuplicateKindsFailClearly()
    {
        var json = DefaultJson().Replace(
            "\"kind\": \"PhysicalDps\"",
            "\"kind\": \"TotalDps\"",
            StringComparison.Ordinal);

        var result = Load(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.DuplicateKind,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Load_DuplicateProviderIdentitiesFailClearly()
    {
        var json = DefaultJson().Replace(
            "\"providerFilterId\": \"pdps\"",
            "\"providerFilterId\": \"dps\"",
            StringComparison.Ordinal);

        var result = Load(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.DuplicateProviderIdentity,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Load_MalformedAndMissingResourcesFailClearly()
    {
        var loader = new PathOfExileTradeItemPropertyMappingResourceLoader();

        var malformed = Load("{");
        var missing = loader.Load(null, "missing-test-resource.json");

        Assert.Equal(
            PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.MalformedResource,
            Assert.Single(malformed.Diagnostics).Code);
        Assert.Equal(
            PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.MissingResource,
            Assert.Single(missing.Diagnostics).Code);
    }

    [Fact]
    public void ProviderMappingTypesAndIdentitiesRemainOutsideCore()
    {
        Assert.Same(
            typeof(PathOfExileTradeQueryBuilder).Assembly,
            typeof(PathOfExileTradeItemPropertyMappingCatalog).Assembly);
        Assert.NotSame(
            typeof(TradeSearchDraft).Assembly,
            typeof(PathOfExileTradeItemPropertyMappingCatalog).Assembly);
        Assert.DoesNotContain(typeof(TradeSearchItemProperty).GetProperties(), property =>
            property.Name.Contains("ProviderGroup", StringComparison.Ordinal) ||
            property.Name.Contains("ProviderFilter", StringComparison.Ordinal));
    }

    private static PathOfExileTradeItemPropertyMappingResourceLoadResult Load(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new PathOfExileTradeItemPropertyMappingResourceLoader().Load(stream, "test.json");
    }

    private static string DefaultJson()
    {
        var assembly = typeof(PathOfExileTradeItemPropertyMappingResourceLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            PathOfExileTradeItemPropertyMappingResourceLoader.DefaultResourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
