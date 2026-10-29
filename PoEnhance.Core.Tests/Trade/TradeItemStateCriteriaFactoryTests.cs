using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeItemStateCriteriaFactoryTests
{
    private readonly ItemTextParser parser = new();

    [Fact]
    public void CreateRequestedDefaults_OnlyPositiveMirroredBecomesYes()
    {
        var mirrored = parser.Parse("""
Item Class: Rings
Rarity: Rare
Mirror Band
Iron Ring
--------
Item Level: 84
--------
Mirrored
""");
        var criteria = TradeItemStateCriteriaFactory.CreateRequestedDefaults(mirrored);

        Assert.Equal(TradeTriState.Yes, criteria.Mirrored);
        Assert.Equal(TradeTriState.Any, criteria.Corrupted);
        Assert.Equal(TradeTriState.Any, criteria.Identified);
        Assert.Equal(TradeTriState.Any, criteria.Fractured);
        Assert.True(mirrored.IsMirrored);
    }

    [Theory]
    [InlineData("Corrupted")]
    [InlineData("Unidentified")]
    [InlineData("Fractured Item")]
    public void CreateRequestedDefaults_ObservedNonMirroredStatesStayRequestedAny(string state)
    {
        var item = parser.Parse($"""
Item Class: Rings
Rarity: Magic
Hale Iron Ring
--------
Item Level: 20
--------
{state}
""");

        var criteria = TradeItemStateCriteriaFactory.CreateRequestedDefaults(item);

        Assert.Equal(TradeTriState.Any, criteria.Mirrored);
        Assert.Equal(TradeTriState.Any, criteria.Corrupted);
        Assert.Equal(TradeTriState.Any, criteria.Identified);
        Assert.Equal(TradeTriState.Any, criteria.Fractured);
        Assert.Contains(state, item.ItemStates);
    }
}
