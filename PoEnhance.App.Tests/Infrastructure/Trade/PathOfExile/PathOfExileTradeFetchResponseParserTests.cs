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
    public void ParseFetchResponse_OfferCardFixturePreservesStructuredItemDataAndOrder()
    {
        var result = ParseSuccessful(PathOfExileTradeFetchFixtures.OfferCardResponse());

        Assert.Empty(result.Diagnostics);
        var armour = Assert.Single(result.Response?.Result ?? [], offer => offer.Id == "result-rare-armour");
        Assert.Equal(2, armour.Item.FrameType);
        Assert.Equal("Rare", armour.Item.Rarity);
        Assert.True(armour.Item.Split);
        Assert.True(armour.Item.Synthesised);
        Assert.True(armour.Item.Fractured);
        Assert.True(armour.Item.Duplicated);
        Assert.True(armour.Item.Replica);
        Assert.True(armour.Item.Veiled);
        Assert.True(armour.Item.IsRelic);
        Assert.True(armour.Item.Ruthless);
        Assert.True(armour.Item.Influences?.Shaper);
        Assert.False(armour.Item.Influences?.Elder);
        Assert.True(armour.Item.Influences?.Crusader);
        Assert.True(armour.Item.Influences?.Hunter);
        Assert.True(armour.Item.Influences?.Redeemer);
        Assert.True(armour.Item.Influences?.Warlord);
        Assert.True(armour.Item.Searing);
        Assert.True(armour.Item.Tangled);

        Assert.Equal(["Armour", "Quality"], armour.Item.Properties.Select(property => property.Name));
        var armourProperty = armour.Item.Properties[0];
        Assert.Equal("1,234", Assert.Single(armourProperty.Values).Text);
        Assert.Equal(1, Assert.Single(armourProperty.Values).ValueType);
        Assert.Equal(0, armourProperty.DisplayMode);
        Assert.Equal(0.75, armourProperty.Progress);
        Assert.Equal(16, armourProperty.Type);
        Assert.Equal("%", armourProperty.Suffix);
        Assert.Equal("https://img.test/armour-property.png", armourProperty.Icon);
        Assert.Equal(["Level", "Str"], armour.Item.Requirements.Select(requirement => requirement.Name));

        Assert.Equal([0, 0, 1, 1], armour.Item.Sockets.Select(socket => socket.Group));
        Assert.Equal(["S", "D", "I", "G"], armour.Item.Sockets.Select(socket => socket.Attribute));
        Assert.Equal(["R", "G", "B", "W"], armour.Item.Sockets.Select(socket => socket.Colour));
        Assert.Equal(["Utility display line"], armour.Item.UtilityMods);
        Assert.Equal(["Cosmetic display line"], armour.Item.CosmeticMods);
        Assert.Equal("A sanitised item description.", armour.Item.Description);
        Assert.Equal("A sanitised secondary description.", armour.Item.SecondaryDescription);
        Assert.Equal(["First flavour line.", "Second flavour line."], armour.Item.FlavourText);

        var weapon = Assert.Single(result.Response?.Result ?? [], offer => offer.Id == "result-weapon");
        var elementalDamage = Assert.Single(
            weapon.Item.Properties,
            property => property.Name == "Elemental Damage");
        Assert.Equal(["10-20", "30-40", "50-60"], elementalDamage.Values.Select(value => value.Text));
        Assert.Equal([4, 5, 6], elementalDamage.Values.Select(value => value.ValueType));
    }

    [Fact]
    public void ParseFetchResponse_LiveModifierObjectShapeUsesDescriptionsAndPreservesCountsAndOrder()
    {
        var result = ParseSuccessful(PathOfExileTradeFetchFixtures.LiveModifierObjectResponse());

        Assert.Empty(result.Diagnostics);
        var offer = Assert.Single(result.Response?.Result ?? []);
        Assert.Equal("result-live-rare-armour", offer.Id);
        Assert.Equal(["Sanitised enchant line"], offer.Item.EnchantMods);
        Assert.Equal(["+12% to Fire Resistance"], offer.Item.ImplicitMods);
        Assert.Equal(
            ["+196 to Evasion Rating", "+43% to Fire Resistance"],
            offer.Item.ExplicitMods);
        Assert.Equal(["+30% to Lightning Resistance"], offer.Item.CraftedMods);
        Assert.Equal(["+30 to Strength"], offer.Item.FracturedMods);
        Assert.Equal(["Sanitised utility line"], offer.Item.UtilityMods);
        Assert.Equal(["Sanitised cosmetic line"], offer.Item.CosmeticMods);

        Assert.True(offer.Item.ModifierDiagnostics.RawFetchOfferPresent);
        AssertModifierCounts(
            offer.Item.ModifierDiagnostics.RawJsonCounts,
            enchant: 1,
            implicitCount: 1,
            explicitCount: 2,
            crafted: 1,
            fractured: 1,
            utility: 1,
            cosmetic: 1);
        AssertModifierCounts(
            offer.Item.ModifierDiagnostics.ParsedDtoCounts,
            enchant: 1,
            implicitCount: 1,
            explicitCount: 2,
            crafted: 1,
            fractured: 1,
            utility: 1,
            cosmetic: 1);
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
        Assert.Empty(offer.Item.UtilityMods);
        Assert.Empty(offer.Item.CosmeticMods);
        Assert.Empty(offer.Item.Properties);
        Assert.Empty(offer.Item.Requirements);
        Assert.Empty(offer.Item.Sockets);
        Assert.Empty(offer.Item.FlavourText);
        Assert.Null(offer.Item.FrameType);
        Assert.Null(offer.Item.Rarity);
        Assert.Null(offer.Item.Split);
        Assert.Null(offer.Item.Synthesised);
        Assert.Null(offer.Item.Fractured);
        Assert.Null(offer.Item.Duplicated);
        Assert.Null(offer.Item.Replica);
        Assert.Null(offer.Item.Influences);
        Assert.Null(offer.Item.Searing);
        Assert.Null(offer.Item.Tangled);
        Assert.Null(offer.Listing.Price);
    }

    [Fact]
    public void ParseFetchResponse_MalformedOptionalStructuredSectionsKeepUsableOfferAndValidEntries()
    {
        var result = ParseSuccessful("""
            {
              "result": [
                {
                  "id": "result-1",
                  "item": {
                    "typeLine": "Titan Plate",
                    "properties": {},
                    "requirements": [
                      { "name": "Bad", "values": [["68", "default"]] },
                      { "name": "Level", "values": [["68", 0]] }
                    ],
                    "sockets": [
                      { "group": "zero", "attr": "S", "sColour": "R" },
                      { "group": 1, "attr": "D", "sColour": "G" }
                    ],
                    "influences": [],
                    "explicitMods": ["valid explicit line", 42],
                    "flavourText": "not-an-array"
                  },
                  "listing": {}
                }
              ]
            }
            """);

        var offer = Assert.Single(result.Response?.Result ?? []);
        Assert.Equal("Titan Plate", offer.Item.TypeLine);
        Assert.Empty(offer.Item.Properties);
        Assert.Equal("Level", Assert.Single(offer.Item.Requirements).Name);
        Assert.Equal(1, Assert.Single(offer.Item.Sockets).Group);
        Assert.Null(offer.Item.Influences);
        Assert.Equal(["valid explicit line"], offer.Item.ExplicitMods);
        Assert.Empty(offer.Item.FlavourText);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeHttpDiagnosticCodes.MalformedItemProperties);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeHttpDiagnosticCodes.MalformedItemRequirements);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeHttpDiagnosticCodes.MalformedItemSockets);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeHttpDiagnosticCodes.MalformedItemInfluences);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeHttpDiagnosticCodes.MalformedItemModifierSection);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeHttpDiagnosticCodes.MalformedItemFlavourText);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(0, diagnostic.ResultIndex));
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

    private static void AssertModifierCounts(
        PathOfExileTradeFetchedModifierCounts counts,
        int enchant,
        int implicitCount,
        int explicitCount,
        int crafted,
        int fractured,
        int utility,
        int cosmetic)
    {
        Assert.Equal(enchant, counts.Enchant);
        Assert.Equal(implicitCount, counts.Implicit);
        Assert.Equal(explicitCount, counts.Explicit);
        Assert.Equal(crafted, counts.Crafted);
        Assert.Equal(fractured, counts.Fractured);
        Assert.Equal(utility, counts.Utility);
        Assert.Equal(cosmetic, counts.Cosmetic);
        Assert.Equal(
            enchant + implicitCount + explicitCount + crafted + fractured + utility + cosmetic,
            counts.Total);
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
