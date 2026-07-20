namespace PoEnhance.App.Infrastructure.Input;

internal interface IQuickUseCommandSender
{
    bool TrySendQuickUseCommand(
        string command,
        bool pressEnter,
        out uint sentInputCount,
        out int errorCode);
}
