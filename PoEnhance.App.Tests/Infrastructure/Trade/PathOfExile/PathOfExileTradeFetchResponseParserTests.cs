using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeFetchResponseParserTests
{
    private readonly PathOfExileTradeFetchResponseParser parser = new();

    [Fact]
    public void ParseFetchResponse_StandardTwoOfferResponseParsesMinimalFieldsAndOrder()
    {
        var result = ParseSuccessful(StandardTwoOfferResponse());

        Assert.Equal(["result-1", "result-2"], result.Response?.Result.Select(offer => offer.Id));

        var first = result.Response?.Result[0];
        Assert.Equal("item-1", first?.Item.Id);
        Assert.Equal("Dusk Shell", first?.Item.Name);
        Assert.Equal("Titan Plate", first?.Item.TypeLine);
        Assert.Equal("Titan Plate", first?.Item.BaseType);
        Assert.Equal("https://img.test/titan.png", first?.Item.Icon);
        Assert.Equal(84, first?.Item.ItemLevel);
        Assert.True(first?.Item.Identified);
        Assert.False(first?.Item.Corrupted);
        Assert.False(first?.Item.Mirrored);
        Assert.Equal(["+12% to Fire Resistance"], first?.Item.ImplicitMods);
        Assert.Equal(["+55 to maximum Life"], first?.Item.ExplicitMods);
        Assert.Equal(["Can have up to 3 Crafted Modifiers"], first?.Item.CraftedMods);
        Assert.Equal(["Fractured +30 to Strength"], first?.Item.FracturedMods);
        Assert.Equal(["Enchanted Test Mod"], first?.Item.EnchantMods);

        Assert.Equal("psapi", first?.Listing.Method);
        Assert.Equal("2026-07-14T10:15:30Z", first?.Listing.RawIndexed);
        Assert.Equal(TimeSpan.Zero, first?.Listing.Indexed?.Offset);
        Assert.Equal("@fake whisper", first?.Listing.Whisper);
        Assert.Equal("FakeAccount", first?.Listing.Account?.Name);
        Assert.Equal("FakeCharacter", first?.Listing.Account?.LastCharacterName);
        Assert.Equal("Mercenaries", first?.Listing.Account?.Online?.League);
        Assert.Equal("online", first?.Listing.Account?.Online?.Status);
        Assert.Equal("~price", first?.Listing.Price?.Type);
        Assert.Equal(1.25m, first?.Listing.Price?.Amount);
        Assert.Equal("divine", first?.Listing.Price?.Currency);
    }

    [Fact]
    public void ParseFetchResponse_MissingOptionalModifierArraysBecomeEmptyAndMissingPriceRemainsNull()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "result-1",
                  "item": { "typeLine": "Gold Ring" },
                  "listing": { "method": "psapi" }
                }
              ]
            }
            """);

        var offer = Assert.Single(result.Response?.Result ?? []);
        Assert.Empty(offer.Item.ImplicitMods);
        Assert.Empty(offer.Item.ExplicitMods);
        Assert.Empty(offer.Item.CraftedMods);
        Assert.Empty(offer.Item.FracturedMods);
        Assert.Empty(offer.Item.EnchantMods);
        Assert.Null(offer.Listing.Price);
    }

    [Fact]
    public void ParseFetchResponse_ZeroResultArrayIsSuccessful()
    {
        var result = ParseSuccessful("""{"result":[]}""");

        Assert.Empty(result.Response?.Result ?? [new PathOfExileTradeFetchedOffer
        {
            Id = "unexpected",
            Item = new PathOfExileTradeFetchedItem(),
            Listing = new PathOfExileTradeListing(),
        }]);
    }

    [Fact]
    public void ParseFetchResponse_MissingTopLevelResultFails()
    {
        AssertFailure("""{"id":"not-fetch"}""", PathOfExileTradeHttpDiagnosticCodes.MalformedResponse);
    }

    [Fact]
    public void ParseFetchResponse_NonArrayTopLevelResultFails()
    {
        AssertFailure("""{"result":{}}""", PathOfExileTradeHttpDiagnosticCodes.MalformedResponse);
    }

    [Fact]
    public void ParseFetchResponse_MalformedJsonFailsWithoutThrowing()
    {
        var exception = Record.Exception(() => parser.ParseFetchResponse("{not json"));

        Assert.Null(exception);
        AssertFailure("{not json", PathOfExileTradeHttpDiagnosticCodes.MalformedResponse);
    }

    [Fact]
    public void ParseFetchResponse_ProviderErrorParsesCodeAndMessage()
    {
        var result = parser.ParseFetchResponse(
            """{"error":{"code":"BadQuery","message":"Bad query."}}""");

        Assert.False(result.IsSuccess);
        Assert.Equal("BadQuery", result.ProviderError?.Code);
        Assert.Equal("Bad query.", result.ProviderError?.Message);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ParseFetchResponse_MalformedOneEntryPreservesOtherValidEntriesWithIndexDiagnostic()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                { "id": "valid-1", "item": {}, "listing": {} },
                "bad-entry",
                { "id": "valid-2", "item": {}, "listing": {} }
              ]
            }
            """);

        Assert.Equal(["valid-1", "valid-2"], result.Response?.Result.Select(offer => offer.Id));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedOffer, diagnostic.Code);
        Assert.Equal(1, diagnostic.ResultIndex);
    }

    [Theory]
    [InlineData("""{ "item": {}, "listing": {} }""")]
    [InlineData("""{ "id": "missing-item", "listing": {} }""")]
    [InlineData("""{ "id": "missing-listing", "item": {} }""")]
    public void ParseFetchResponse_MissingRequiredOfferFieldsInvalidateOnlyThatEntry(string malformedOffer)
    {
        var result = ParseSuccessful($$"""
            {
              "result": [
                { "id": "valid-1", "item": {}, "listing": {} },
                {{malformedOffer}},
                { "id": "valid-2", "item": {}, "listing": {} }
              ]
            }
            """);

        Assert.Equal(["valid-1", "valid-2"], result.Response?.Result.Select(offer => offer.Id));
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedOffer, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ParseFetchResponse_MalformedPriceAddsDiagnosticWithoutInventingAmount()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "result-1",
                  "item": {},
                  "listing": {
                    "price": { "type": "~price", "amount": "1.5", "currency": "divine" }
                  }
                }
              ]
            }
            """);

        var offer = Assert.Single(result.Response?.Result ?? []);
        Assert.Null(offer.Listing.Price);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedPrice, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ParseFetchResponse_MalformedIndexedTimestampPreservesRawTextAndAddsDiagnostic()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "result-1",
                  "item": {},
                  "listing": { "indexed": "not-a-date" }
                }
              ]
            }
            """);

        var offer = Assert.Single(result.Response?.Result ?? []);
        Assert.Equal("not-a-date", offer.Listing.RawIndexed);
        Assert.Null(offer.Listing.Indexed);
        Assert.Equal(
            PathOfExileTradeHttpDiagnosticCodes.MalformedIndexedTimestamp,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ParseFetchResponse_RepeatedParsingIsEquivalent()
    {
        var first = ParseSuccessful(StandardTwoOfferResponse());
        var second = ParseSuccessful(StandardTwoOfferResponse());

        Assert.Equal(
            first.Response?.Result.Select(offer => offer.Id),
            second.Response?.Result.Select(offer => offer.Id));
        Assert.Equal(first.Diagnostics.Select(diagnostic => diagnostic.Code), second.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    private PathOfExileTradeFetchResponseParseResult ParseSuccessful(string json)
    {
        var result = parser.ParseFetchResponse(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Null(result.ProviderError);
        return result;
    }

    private void AssertFailure(string json, string expectedCode)
    {
        var result = parser.ParseFetchResponse(json);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Response);
        Assert.Null(result.ProviderError);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
    }

    private static string StandardTwoOfferResponse()
    {
        return """
            {
              "result": [
                {
                  "id": "result-1",
                  "item": {
                    "id": "item-1",
                    "name": "Dusk Shell",
                    "typeLine": "Titan Plate",
                    "baseType": "Titan Plate",
                    "icon": "https://img.test/titan.png",
                    "ilvl": 84,
                    "identified": true,
                    "corrupted": false,
                    "mirrored": false,
                    "implicitMods": ["+12% to Fire Resistance"],
                    "explicitMods": ["+55 to maximum Life"],
                    "craftedMods": ["Can have up to 3 Crafted Modifiers"],
                    "fracturedMods": ["Fractured +30 to Strength"],
                    "enchantMods": ["Enchanted Test Mod"]
                  },
                  "listing": {
                    "method": "psapi",
                    "indexed": "2026-07-14T10:15:30Z",
                    "whisper": "@fake whisper",
                    "account": {
                      "name": "FakeAccount",
                      "lastCharacterName": "FakeCharacter",
                      "online": {
                        "league": "Mercenaries",
                        "status": "online"
                      }
                    },
                    "price": {
                      "type": "~price",
                      "amount": 1.25,
                      "currency": "divine"
                    }
                  }
                },
                {
                  "id": "result-2",
                  "item": { "typeLine": "Gold Ring" },
                  "listing": { "method": "psapi" }
                }
              ]
            }
            """;
    }
}
