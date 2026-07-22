using System.Net.Http;
using System.Reflection;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class OfferCardSnapshotMapperTests
{
    [Fact]
    public void Create_MapsStructuredFetchFactsWithoutMergingModifierSections()
    {
        var response = ParseFixture();
        var source = Assert.Single(response.Result, offer => offer.Id == "result-rare-armour");

        var snapshot = OfferCardSnapshotMapper.Create(source);

        Assert.Equal("result-rare-armour", snapshot.OfferId);
        Assert.Equal("item-rare-armour", snapshot.ItemId);
        Assert.Equal("Dusk Shell", snapshot.Name);
        Assert.Equal("Titan Plate", snapshot.TypeLine);
        Assert.Equal("Titan Plate", snapshot.BaseType);
        Assert.Equal(OfferCardFrameKind.Rare, snapshot.Frame);
        Assert.Equal("Rare", snapshot.Rarity);
        Assert.Equal("https://img.test/titan.png", snapshot.IconReference);
        Assert.Equal(84, snapshot.ItemLevel);

        Assert.True(snapshot.Flags.Identified);
        Assert.False(snapshot.Flags.Corrupted);
        Assert.False(snapshot.Flags.Mirrored);
        Assert.True(snapshot.Flags.Split);
        Assert.True(snapshot.Flags.Synthesised);
        Assert.True(snapshot.Flags.Fractured);
        Assert.True(snapshot.Flags.Duplicated);
        Assert.True(snapshot.Flags.Replica);
        Assert.True(snapshot.Flags.Veiled);
        Assert.True(snapshot.Flags.IsRelic);
        Assert.True(snapshot.Flags.Ruthless);

        Assert.True(snapshot.Influences.Shaper);
        Assert.False(snapshot.Influences.Elder);
        Assert.True(snapshot.Influences.Crusader);
        Assert.True(snapshot.Influences.Hunter);
        Assert.True(snapshot.Influences.Redeemer);
        Assert.True(snapshot.Influences.Warlord);
        Assert.True(snapshot.Influences.Searing);
        Assert.True(snapshot.Influences.Tangled);

        Assert.Equal(["Armour", "Quality"], snapshot.Properties.Select(property => property.DisplayName));
        var armour = snapshot.Properties[0];
        Assert.Equal("1,234", Assert.Single(armour.Values).Text);
        Assert.Equal(1, Assert.Single(armour.Values).DisplayStyleCode);
        Assert.Equal(OfferCardPropertyDisplayMode.NameThenValues, armour.DisplayMode);
        Assert.Equal(0.75, armour.Progress);
        Assert.Equal(16, armour.TypeCode);
        Assert.Equal("%", armour.Suffix);
        Assert.Equal("https://img.test/armour-property.png", armour.IconReference);
        Assert.Equal(["Level", "Str"], snapshot.Requirements.Select(requirement => requirement.DisplayName));
        Assert.Equal(
            [OfferCardPropertyDisplayMode.NameThenValues, OfferCardPropertyDisplayMode.ValuesThenName],
            snapshot.Requirements.Select(requirement => requirement.DisplayMode));

        Assert.Equal([0, 1, 2, 3], snapshot.Sockets.Select(socket => socket.Index));
        Assert.Equal([0, 0, 1, 1], snapshot.Sockets.Select(socket => socket.Group));
        Assert.Equal(["S", "D", "I", "G"], snapshot.Sockets.Select(socket => socket.Attribute));
        Assert.Equal(["R", "G", "B", "W"], snapshot.Sockets.Select(socket => socket.Colour));

        Assert.Equal(
            [
                OfferCardModifierProvenance.Enchant,
                OfferCardModifierProvenance.Implicit,
                OfferCardModifierProvenance.Explicit,
                OfferCardModifierProvenance.Crafted,
                OfferCardModifierProvenance.Fractured,
                OfferCardModifierProvenance.Utility,
                OfferCardModifierProvenance.Cosmetic,
            ],
            snapshot.ModifierSections.Select(section => section.Provenance));
        Assert.Equal(
            ["Enchanted Test Mod 1", "Enchanted Test Mod 2"],
            snapshot.ModifierSections[0].Lines.ToArray());
        Assert.Equal(
            ["+55 to maximum Life", "+30% to Cold Resistance"],
            snapshot.ModifierSections[2].Lines.ToArray());

        Assert.Equal("A sanitised item description.", snapshot.Description);
        Assert.Equal("A sanitised secondary description.", snapshot.SecondaryDescription);
        Assert.Equal(["First flavour line.", "Second flavour line."], snapshot.FlavourText.ToArray());
        Assert.Equal("~price", snapshot.Price?.Type);
        Assert.Equal(1.25m, snapshot.Price?.Amount);
        Assert.Equal("divine", snapshot.Price?.Currency);
        Assert.Equal("SanitisedAccount", snapshot.Seller.AccountName);
        Assert.Equal("SanitisedCharacter", snapshot.Seller.LastCharacterName);
        Assert.Equal("Sanitised League", snapshot.Online?.League);
        Assert.Equal("online", snapshot.Online?.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-07-14T10:15:30Z"), snapshot.IndexedAt);
    }

    [Fact]
    public void Create_PreservesMultipleDisplayedWeaponValuesInOrder()
    {
        var response = ParseFixture();
        var source = Assert.Single(response.Result, offer => offer.Id == "result-weapon");

        var snapshot = OfferCardSnapshotMapper.Create(source);

        var elementalDamage = Assert.Single(
            snapshot.Properties,
            property => property.DisplayName == "Elemental Damage");
        Assert.Equal(["10-20", "30-40", "50-60"], elementalDamage.Values.Select(value => value.Text));
        Assert.Equal([4, 5, 6], elementalDamage.Values.Select(value => value.DisplayStyleCode));
    }

    [Fact]
    public void Create_AbsentOptionalFactsBecomeNullOrEmpty()
    {
        var snapshot = OfferCardSnapshotMapper.Create(new PathOfExileTradeFetchedOffer
        {
            Id = "minimal-offer",
            Item = new PathOfExileTradeFetchedItem(),
            Listing = new PathOfExileTradeListing(),
        });

        Assert.Equal("minimal-offer", snapshot.OfferId);
        Assert.Null(snapshot.ItemId);
        Assert.Null(snapshot.Frame);
        Assert.Null(snapshot.Rarity);
        Assert.Null(snapshot.Flags.Split);
        Assert.Null(snapshot.Influences.Shaper);
        Assert.Null(snapshot.Influences.Searing);
        Assert.Empty(snapshot.Properties);
        Assert.Empty(snapshot.Requirements);
        Assert.Empty(snapshot.Sockets);
        Assert.Empty(snapshot.ModifierSections);
        Assert.Empty(snapshot.FlavourText);
        Assert.Null(snapshot.Price);
        Assert.Null(snapshot.Online);
        Assert.Null(snapshot.IndexedAt);
    }

    [Fact]
    public void Create_ClonesAllCollectionsAwayFromMutableProviderSources()
    {
        var propertyValues = new List<PathOfExileTradeItemPropertyValue>
        {
            new()
            {
                Text = "100",
                ValueType = 1,
            },
        };
        var properties = new List<PathOfExileTradeItemProperty>
        {
            new()
            {
                Name = "Armour",
                Values = propertyValues,
            },
        };
        var sockets = new List<PathOfExileTradeItemSocket>
        {
            new()
            {
                Group = 0,
                Attribute = "S",
                Colour = "R",
            },
        };
        var explicitMods = new[] { "+50 to maximum Life" };
        var flavourText = new[] { "Original flavour" };
        var source = new PathOfExileTradeFetchedOffer
        {
            Id = "stable-offer",
            Item = new PathOfExileTradeFetchedItem
            {
                Properties = properties,
                Sockets = sockets,
                ExplicitMods = explicitMods,
                FlavourText = flavourText,
            },
            Listing = new PathOfExileTradeListing(),
        };

        var snapshot = OfferCardSnapshotMapper.Create(source);
        propertyValues[0] = new PathOfExileTradeItemPropertyValue
        {
            Text = "changed",
            ValueType = 9,
        };
        properties.Clear();
        sockets.Clear();
        explicitMods[0] = "changed";
        flavourText[0] = "changed";

        Assert.Equal("Armour", Assert.Single(snapshot.Properties).DisplayName);
        Assert.Equal("100", Assert.Single(snapshot.Properties[0].Values).Text);
        Assert.Equal("R", Assert.Single(snapshot.Sockets).Colour);
        Assert.Equal("+50 to maximum Life", Assert.Single(snapshot.ModifierSections).Lines[0]);
        Assert.Equal("Original flavour", Assert.Single(snapshot.FlavourText));
    }

    [Fact]
    public void Create_RemainsStableWhenSourceResultCollectionIsReplacedOrCleared()
    {
        var sourceResults = ParseFixture().Result.ToList();
        var snapshot = OfferCardSnapshotMapper.Create(sourceResults[0]);

        sourceResults[0] = new PathOfExileTradeFetchedOffer
        {
            Id = "replacement",
            Item = new PathOfExileTradeFetchedItem(),
            Listing = new PathOfExileTradeListing(),
        };
        sourceResults.Clear();

        Assert.Equal("result-rare-armour", snapshot.OfferId);
        Assert.Equal("Dusk Shell", snapshot.Name);
        Assert.Equal(["Armour", "Quality"], snapshot.Properties.Select(property => property.DisplayName));
    }

    [Fact]
    public void Create_IsASynchronousHttpFreeMappingBoundary()
    {
        var createMethod = typeof(OfferCardSnapshotMapper).GetMethod(
            nameof(OfferCardSnapshotMapper.Create),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(createMethod);
        Assert.Equal(typeof(OfferCardSnapshot), createMethod.ReturnType);
        Assert.Equal(
            [typeof(PathOfExileTradeFetchedOffer)],
            createMethod.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(typeof(OfferCardSnapshotMapper).GetFields(
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), field =>
                field.FieldType == typeof(HttpClient) ||
                field.FieldType == typeof(IPathOfExileTradeFetchClient));
    }

    [Fact]
    public void SnapshotModelDoesNotExposeProviderDtosOrRawJson()
    {
        var snapshotTypes = typeof(OfferCardSnapshot).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(OfferCardSnapshot).Namespace)
            .Where(type => type.Name.StartsWith("OfferCard", StringComparison.Ordinal))
            .ToArray();

        var exposedTypes = snapshotTypes
            .SelectMany(type => type.GetProperties())
            .Select(property => property.PropertyType)
            .ToArray();
        Assert.DoesNotContain(exposedTypes, type =>
            type.FullName?.Contains("PathOfExileTrade", StringComparison.Ordinal) == true ||
            type.FullName?.Contains("JsonElement", StringComparison.Ordinal) == true);
    }

    private static PathOfExileTradeFetchResponse ParseFixture()
    {
        var parseResult = new PathOfExileTradeFetchResponseParser()
            .ParseFetchResponse(PathOfExileTradeFetchFixtures.OfferCardResponse());

        Assert.True(parseResult.IsSuccess);
        Assert.Empty(parseResult.Diagnostics);
        return Assert.IsType<PathOfExileTradeFetchResponse>(parseResult.Response);
    }
}
