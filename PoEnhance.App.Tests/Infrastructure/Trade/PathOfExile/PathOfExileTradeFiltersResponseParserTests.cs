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
            catalog.NumericFilterDefinitions
                .Where(definition => definition.GroupId == "weapon_filters")
                .Select(definition => definition.FilterId));
        Assert.All(catalog.NumericFilterDefinitions.Where(definition =>
            definition.GroupId == "weapon_filters"), definition =>
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

    [Theory]
    [InlineData(
        "ev",
        "Evasion",
        "Includes base value, local modifiers, and maximum quality")]
    [InlineData(
        "block",
        "Block",
        "Includes base value and local modifiers")]
    public void Parse_CurrentOfficialDefensiveEntryRetainsIdentityNumericCapabilityAndSemanticTip(
        string filterId,
        string expectedText,
        string expectedTip)
    {
        var result = new PathOfExileTradeFiltersResponseParser().ParseFiltersResponse(
            PathOfExileTradeItemPropertyTestFixtures.OfficialFiltersJson);

        Assert.True(result.IsSuccess);
        var definition = Assert.Single(result.Catalog!.NumericFilterDefinitions, definition =>
            definition.GroupId == "armour_filters" && definition.FilterId == filterId);
        Assert.Equal("Armour Filters", definition.GroupTitle);
        Assert.True(definition.GroupHidden);
        Assert.Equal(expectedText, definition.Text);
        Assert.Equal(expectedTip, definition.Tip);
        Assert.True(definition.SupportsMinMax);
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

    [Fact]
    public void Parse_OfficialItemStateOptionDefinitionsRetainExactYesAndNoIdentities()
    {
        var result = new PathOfExileTradeFiltersResponseParser().ParseFiltersResponse("""
{
  "result": [
    {
      "id": "type_filters",
      "title": "Type Filters",
      "filters": [
        {
          "id": "category",
          "text": "Item Category",
          "option": { "options": [{ "id": "accessory.ring", "text": "Ring" }] }
        }
      ]
    },
    {
      "id": "misc_filters",
      "title": "Miscellaneous",
      "filters": [
        { "id": "identified", "text": "Identified", "option": { "options": [
          { "id": null, "text": "Any" }, { "id": "true", "text": "Yes" }, { "id": "false", "text": "No" }
        ] } },
        { "id": "corrupted", "text": "Corrupted", "option": { "options": [
          { "id": null, "text": "Any" }, { "id": "true", "text": "Yes" }, { "id": "false", "text": "No" }
        ] } },
        { "id": "mirrored", "text": "Mirrored", "option": { "options": [
          { "id": null, "text": "Any" }, { "id": "true", "text": "Yes" }, { "id": "false", "text": "No" }
        ] } }
      ]
    }
  ]
}
""");

        Assert.True(result.IsSuccess);
        var definitions = result.Catalog!.OptionFilterDefinitions
            .Where(definition => definition.GroupId == "misc_filters")
            .ToArray();
        Assert.Equal(["identified", "corrupted", "mirrored"], definitions.Select(definition => definition.FilterId));
        Assert.All(definitions, definition => Assert.Equal(
            [(null, "Any"), ("true", "Yes"), ("false", "No")],
            definition.Options.Select(option => (option.Id, option.Text))));
    }
}
