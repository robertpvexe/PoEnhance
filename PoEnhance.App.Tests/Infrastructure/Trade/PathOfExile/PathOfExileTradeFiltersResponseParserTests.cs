using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeFiltersResponseParserTests
{
    [Fact]
    public void Parse_OfficialWeaponFiltersRetainsGenericNumericDefinitionsInProviderOrder()
    {
        var result = new PathOfExileTradeFiltersResponseParser().ParseFiltersResponse(
            PathOfExileTradeItemPropertyTestFixtures.OfficialFiltersJson);

        Assert.True(result.IsSuccess);
        var catalog = Assert.IsType<PathOfExileTradeFilterCatalog>(result.Catalog);
        Assert.Equal(
            ["damage", "aps", "crit", "dps", "pdps", "edps"],
            catalog.NumericFilterDefinitions.Select(definition => definition.FilterId));
        Assert.All(catalog.NumericFilterDefinitions, definition =>
        {
            Assert.Equal("weapon_filters", definition.GroupId);
            Assert.Equal("Weapon Filters", definition.GroupTitle);
            Assert.True(definition.GroupHidden);
            Assert.True(definition.SupportsMinMax);
        });
        Assert.DoesNotContain(catalog.NumericFilterDefinitions, definition =>
            definition.FilterId.Contains("chaos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_WeaponFiltersDoesNotChangeExistingCategoryParsing()
    {
        var result = new PathOfExileTradeFiltersResponseParser().ParseFiltersResponse(
            PathOfExileTradeItemPropertyTestFixtures.OfficialFiltersJson);

        var catalog = Assert.IsType<PathOfExileTradeFilterCatalog>(result.Catalog);
        Assert.True(catalog.TryFindCategoryOption("One Hand Axes", out var axe));
        Assert.Equal("weapon.oneaxe", axe.Id);
        Assert.True(catalog.TryFindCategoryOption("Bow", out var bow));
        Assert.Equal("weapon.bow", bow.Id);
    }

    [Fact]
    public void Parse_MalformedNumericEntryIsDiagnosedAndIgnoredConservatively()
    {
        var json = PathOfExileTradeItemPropertyTestFixtures.OfficialFiltersJson.Replace(
            "{ \"id\": \"crit\", \"text\": \"Critical Chance\", \"minMax\": true }",
            "{ \"id\": \"crit\", \"text\": \"Critical Chance\", \"minMax\": \"yes\" }",
            StringComparison.Ordinal);

        var result = new PathOfExileTradeFiltersResponseParser().ParseFiltersResponse(json);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Catalog!.NumericFilterDefinitions, definition =>
            definition.FilterId == "crit");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeFiltersDiagnosticCodes.MalformedNumericFilter);
    }
}
