using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeStatsResponseParserTests
{
    private readonly PathOfExileTradeStatsResponseParser parser = new();

    [Fact]
    public void ParseStatsResponse_SuccessfulSmallFixturePreservesGroupsEntriesMetadataAndOrder()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "explicit",
                  "label": "Explicit",
                  "entries": [
                    { "id": "explicit.stat1", "text": "+# to maximum Life", "type": "explicit", "ignored": true },
                    { "id": "explicit.stat2", "text": "Adds # to # Fire Damage", "type": "explicit" }
                  ]
                },
                {
                  "id": "implicit",
                  "label": "Implicit",
                  "entries": [
                    { "id": "implicit.stat1", "text": "+#% to Fire Resistance", "type": "implicit" }
                  ]
                }
              ]
            }
            """);

        Assert.Equal(["explicit.stat1", "explicit.stat2", "implicit.stat1"], result.Catalog?.Entries.Select(entry => entry.Id));
        var first = result.Catalog!.Entries[0];
        Assert.Equal("explicit", first.GroupId);
        Assert.Equal("Explicit", first.GroupLabel);
        Assert.Equal("+# to maximum Life", first.Text);
        Assert.Equal("explicit", first.Type);
        Assert.True(result.Catalog.TryGetById("explicit.stat2", out var second));
        Assert.Equal("Adds # to # Fire Damage", second.Text);
    }

    [Fact]
    public void ParseStatsResponse_UnknownFieldsAndOptionMetadataAreHandledSafely()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "explicit",
                  "label": "Explicit",
                  "unknown": { "shape": "ignored" },
                  "entries": [
                    {
                      "id": "explicit.stat",
                      "text": "#% increased Armour",
                      "type": "explicit",
                      "option": { "id": "armour", "label": "Armour", "number": 2, "flag": true }
                    }
                  ]
                }
              ]
            }
            """);

        var entry = Assert.Single(result.Catalog!.Entries);
        Assert.Equal("armour", entry.OptionMetadata["id"]);
        Assert.Equal("Armour", entry.OptionMetadata["label"]);
        Assert.Equal("2", entry.OptionMetadata["number"]);
        Assert.Equal(bool.TrueString, entry.OptionMetadata["flag"]);
    }

    [Fact]
    public void ParseStatsResponse_ObjectOptionWithNestedArraysDoesNotRejectValidEntry()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "pseudo",
                  "label": "Pseudo",
                  "entries": [
                    {
                      "id": "pseudo.stat",
                      "text": "+# to maximum Life",
                      "type": "pseudo",
                      "option": {
                        "options": [
                          { "id": "life", "text": "Life" }
                        ],
                        "label": "Life"
                      }
                    }
                  ]
                }
              ]
            }
            """);

        var entry = Assert.Single(result.Catalog!.Entries);
        Assert.Equal("pseudo.stat", entry.Id);
        Assert.Equal("Life", entry.OptionMetadata["label"]);
        Assert.False(entry.OptionMetadata.ContainsKey("options"));
    }

    [Fact]
    public void ParseStatsResponse_ArrayShapedOptionMetadataIsIgnoredWithoutDiscardingEntry()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "explicit",
                  "entries": [
                    {
                      "id": "explicit.stat",
                      "text": "#% increased Armour",
                      "type": "explicit",
                      "option": [
                        { "id": "armour" }
                      ]
                    }
                  ]
                }
              ]
            }
            """);

        var entry = Assert.Single(result.Catalog!.Entries);
        Assert.Equal("explicit.stat", entry.Id);
        Assert.Empty(entry.OptionMetadata);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ParseStatsResponse_MalformedOptionalMetadataDoesNotDiscardCatalog()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": 7,
                  "label": { "bad": true },
                  "entries": [
                    {
                      "id": "explicit.stat",
                      "text": "+# to maximum Life",
                      "type": 42,
                      "option": { "nested": { "ignored": true } }
                    }
                  ]
                }
              ],
              "unknown": { "ignored": true }
            }
            """);

        var entry = Assert.Single(result.Catalog!.Entries);
        Assert.Equal("explicit.stat", entry.Id);
        Assert.Null(entry.GroupId);
        Assert.Null(entry.GroupLabel);
        Assert.Null(entry.Type);
        Assert.Empty(entry.OptionMetadata);
    }

    [Fact]
    public void ParseStatsResponse_MissingTopLevelResultFailsStructurally()
    {
        var result = parser.ParseStatsResponse("""{"notResult":[]}""");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Catalog);
        AssertFailure(result, PathOfExileTradeStatsDiagnosticCodes.MissingResultCollection);
    }

    [Fact]
    public void ParseStatsResponse_MalformedJsonFailsWithoutThrowing()
    {
        var result = parser.ParseStatsResponse("""{"result":[""");

        Assert.False(result.IsSuccess);
        AssertFailure(result, PathOfExileTradeStatsDiagnosticCodes.MalformedJson);
    }

    [Fact]
    public void ParseStatsResponse_MalformedGroupAndEntriesPreserveValidEntries()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                42,
                { "id": "bad-group", "entries": "not-array" },
                {
                  "id": "explicit",
                  "label": "Explicit",
                  "entries": [
                    "bad-entry",
                    { "text": "+# to maximum Life" },
                    { "id": "missing-text" },
                    { "id": "good", "text": "+# to maximum Life", "type": "explicit" }
                  ]
                }
              ]
            }
            """);

        Assert.Equal("good", Assert.Single(result.Catalog!.Entries).Id);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeStatsDiagnosticCodes.MalformedGroup);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeStatsDiagnosticCodes.MalformedEntry);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeStatsDiagnosticCodes.MissingStatId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeStatsDiagnosticCodes.MissingTemplateText);
    }

    [Fact]
    public void ParseStatsResponse_DuplicateIdsAreDeterministicAndDiagnosed()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "explicit",
                  "entries": [
                    { "id": "same", "text": "+# to maximum Life", "type": "explicit" },
                    { "id": "same", "text": "+# to maximum Mana", "type": "explicit" }
                  ]
                }
              ]
            }
            """);

        Assert.True(result.Catalog!.TryGetById("same", out var entry));
        Assert.Equal("+# to maximum Life", entry.Text);
        Assert.Single(result.Catalog.Entries);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeStatsDiagnosticCodes.DuplicateStatId);
    }

    [Fact]
    public void ParseStatsResponse_LiveShapeSyntheticFixtureProducesUsableCatalog()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "pseudo",
                  "label": "Pseudo",
                  "entries": [
                    {
                      "id": "pseudo.pseudo_total_life",
                      "text": "+# to maximum Life",
                      "type": "pseudo",
                      "option": {
                        "options": [
                          { "id": "explicit.stat_life", "text": "Life" }
                        ]
                      }
                    },
                    {
                      "id": "duplicate",
                      "text": "+# to maximum Mana",
                      "type": "pseudo"
                    }
                  ]
                },
                {
                  "id": "explicit",
                  "label": "Explicit",
                  "entries": [
                    { "id": "duplicate", "text": "+# to maximum Mana", "type": "explicit" },
                    { "id": "explicit.stat_resist", "text": "+#% to Fire Resistance", "type": "explicit", "unknown": [1, 2] },
                    { "id": "bad-missing-text", "type": "explicit" }
                  ]
                }
              ]
            }
            """);

        Assert.Equal(
            ["pseudo.pseudo_total_life", "duplicate", "explicit.stat_resist"],
            result.Catalog!.Entries.Select(entry => entry.Id));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeStatsDiagnosticCodes.DuplicateStatId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeStatsDiagnosticCodes.MissingTemplateText);
    }

    [Fact]
    public void ParseStatsResponse_EmptyCatalogSucceedsButIsDiagnosedUnusable()
    {
        var result = parser.ParseStatsResponse("""{"result":[{"id":"empty","entries":[]}]}""");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Catalog!.Entries);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PathOfExileTradeStatsDiagnosticCodes.UnusableEmptyCatalog);
    }

    [Fact]
    public void Catalog_LooksUpMultipleEntriesBySameNormalizedTemplate()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "explicit",
                  "entries": [
                    { "id": "one", "text": "+# to maximum Life", "type": "explicit" },
                    { "id": "two", "text": "+# to maximum Life", "type": "explicit" }
                  ]
                }
              ]
            }
            """);

        var entries = result.Catalog!.FindByNormalizedTemplate("+# to maximum Life");

        Assert.Equal(["one", "two"], entries.Select(entry => entry.Id));
    }

    private PathOfExileTradeStatsResponseParseResult ParseSuccessful(string json)
    {
        var result = parser.ParseStatsResponse(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Catalog);
        return result;
    }

    private static void AssertFailure(
        PathOfExileTradeStatsResponseParseResult result,
        string code)
    {
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }
}
