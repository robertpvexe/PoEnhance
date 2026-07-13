namespace PoEnhance.App.Features.PriceChecking;

internal interface IPathOfExileClientBoundsProvider
{
    bool TryGetClientBounds(out PathOfExileClientBounds bounds);
}
