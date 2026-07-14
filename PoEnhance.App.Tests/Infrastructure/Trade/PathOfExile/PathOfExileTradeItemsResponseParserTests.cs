using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeItemsResponseParserTests
{
    private readonly PathOfExileTradeItemsResponseParser parser = new();

    [Fact]
    public void ParseItemsResponse_LiveShapeSyntheticFixturePreservesCanonicalUniqueIdentity()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "jewel",
                  "label": "Jewels",
                  "entries": [
                    { "name": "Voices", "type": "Large Cluster Jewel", "flags": { "unique": true } }
                  ]
                },
                {
                  "id": "weapon",
                  "label": "Weapons",
                  "entries": [
                    { "type": "Tomahawk" },
                    { "name": "Moonbender's Wing", "type": "Tomahawk", "flags": { "unique": true } }
                  ]
                }
              ]
            }
            """);

        Assert.Equal(["Voices", null, "Moonbender's Wing"], result.Catalog!.Entries.Select(entry => entry.Name));
        var voices = Assert.Single(result.Catalog.FindByExactDisplayText("Voices"));
        Assert.True(voices.IsUnique);
        Assert.Equal("Large Cluster Jewel", voices.Type);
        var moonbender = Assert.Single(result.Catalog.FindByExactDisplayText("Moonbender's Wing"));
        Assert.Equal("weapon", moonbender.GroupId);
        Assert.Equal("Tomahawk", moonbender.Type);
    }

    [Fact]
    public void ParseItemsResponse_TypeOnlyEntriesAreSearchableByExactDisplayText()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "currency",
                  "entries": [
                    { "type": "Foulborn Orb", "flags": { "unique": false } }
                  ]
                }
              ]
            }
            """);

        var entry = Assert.Single(result.Catalog!.FindByExactDisplayText("Foulborn Orb"));
        Assert.Equal("Foulborn Orb", entry.Type);
        Assert.False(entry.IsUnique);
    }

    [Fact]
    public void ParseItemsResponse_MalformedOptionalFieldsDoNotDiscardValidEntry()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": 7,
                  "label": { "bad": true },
                  "entries": [
                    {
                      "name": 42,
                      "type": "Tomahawk",
                      "flags": { "unique": "true", "ignored": [1, 2] },
                      "unknown": { "ignored": true }
                    }
                  ]
                }
              ]
            }
            """);

        var entry = Assert.Single(result.Catalog!.Entries);
        Assert.Null(entry.GroupId);
        Assert.Null(entry.GroupLabel);
        Assert.Null(entry.Name);
        Assert.Equal("Tomahawk", entry.Type);
        Assert.True(entry.IsUnique);
    }

    [Fact]
    public void ParseItemsResponse_MalformedGroupAndEntriesPreserveValidEntries()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                42,
                { "id": "bad-group", "entries": "not-array" },
                {
                  "id": "weapon",
                  "entries": [
                    "bad-entry",
                    { "name": "missing type" },
                    { "name": "Moonbender's Wing", "type": "Tomahawk", "flags": { "unique": true } }
                  ]
                }
              ]
            }
            """);

        Assert.Equal("Moonbender's Wing", Assert.Single(result.Catalog!.Entries).Name);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeItemsDiagnosticCodes.MalformedGroup);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeItemsDiagnosticCodes.MalformedEntry);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeItemsDiagnosticCodes.MissingItemType);
    }

    [Fact]
    public void ParseItemsResponse_MissingTopLevelResultFailsStructurally()
    {
        var result = parser.ParseItemsResponse("""{"notResult":[]}""");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeItemsDiagnosticCodes.MissingResultCollection);
    }

    [Fact]
    public void ParseItemsResponse_MalformedJsonFailsWithoutThrowing()
    {
        var result = parser.ParseItemsResponse("""{"result":[""");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeItemsDiagnosticCodes.MalformedJson);
    }

    private PathOfExileTradeItemsResponseParseResult ParseSuccessful(string json)
    {
        var result = parser.ParseItemsResponse(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Catalog);
        return result;
    }
}
