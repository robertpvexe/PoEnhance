using System.Reflection;
using System.Text;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeItemPropertyMappingResourceTests
{
    [Fact]
    public void DefaultResource_LoadsWeaponAndDefensiveMappingsAndExplicitUnsupportedChaos()
    {
        var catalog = new PathOfExileTradeItemPropertyMappingResourceLoader().LoadDefaultOrThrow();

        Assert.Equal("item-property-trade-mapping-audit-2026-07-17", catalog.ReviewReference);
        Assert.Equal(10, catalog.Mappings.Count(mapping => mapping.IsSupported));
        Assert.Equal(
            ["es", "ar", "ev", "ward", "block"],
            catalog.Mappings.Where(mapping => mapping.Kind is
                    TradeSearchItemPropertyKind.EnergyShield or TradeSearchItemPropertyKind.Armour or
                    TradeSearchItemPropertyKind.EvasionRating or TradeSearchItemPropertyKind.Ward or
                    TradeSearchItemPropertyKind.ChanceToBlock)
                .Select(mapping => mapping.ProviderFilterId));
        var evasion = Assert.Single(catalog.Mappings, mapping =>
            mapping.Kind == TradeSearchItemPropertyKind.EvasionRating);
        Assert.Equal("armour_filters", evasion.ProviderGroupId);
        Assert.Equal("ev", evasion.ProviderFilterId);
        Assert.Equal("Evasion", evasion.ExpectedOfficialText);
        Assert.Equal(
            "Includes base value, local modifiers, and maximum quality",
            evasion.ExpectedOfficialTip);
        Assert.False(evasion.RequiresExactOfficialTextMatch);
        Assert.True(evasion.RequiresNumericMinMax);
        var block = Assert.Single(catalog.Mappings, mapping =>
            mapping.Kind == TradeSearchItemPropertyKind.ChanceToBlock);
        Assert.Equal("armour_filters", block.ProviderGroupId);
        Assert.Equal("block", block.ProviderFilterId);
        Assert.Equal("Block", block.ExpectedOfficialText);
        Assert.Equal("Includes base value and local modifiers", block.ExpectedOfficialTip);
        Assert.False(block.RequiresExactOfficialTextMatch);
        Assert.True(block.RequiresNumericMinMax);
        Assert.True(Assert.Single(catalog.Mappings, mapping =>
            mapping.Kind == TradeSearchItemPropertyKind.Armour).RequiresExactOfficialTextMatch);
        Assert.Equal(catalog.Mappings.Count, catalog.Mappings.Select(mapping => mapping.Kind).Distinct().Count());
        Assert.Equal(
            catalog.Mappings.Count(mapping => mapping.IsSupported),
            catalog.Mappings.Where(mapping => mapping.IsSupported)
                .Select(mapping => $"{mapping.ProviderGroupId}/{mapping.ProviderFilterId}")
                .Distinct(StringComparer.Ordinal)
                .Count());
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
