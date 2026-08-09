namespace PoEnhance.App.Infrastructure.Input;

internal interface IPriceCheckerCopyChordSender
{
    bool TrySendAdvancedItemDescriptionCopyChord(out uint sentInputCount, out int errorCode);
}
