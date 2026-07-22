namespace PoEnhance.App.Infrastructure.PathOfExile;

internal interface IPathOfExileForegroundWindowDetector
{
    bool IsPathOfExileForegroundWindow();

    bool IsPathOfExileOverlayContextActive() => IsPathOfExileForegroundWindow();
}
