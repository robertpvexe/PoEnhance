namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerRarityChangedEventArgs(string rarity) : EventArgs
{
    public string Rarity { get; } = rarity;
}
