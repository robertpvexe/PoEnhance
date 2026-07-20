namespace PoEnhance.App.Shell;

internal interface IPoEnhanceTrayIcon : IDisposable
{
    event EventHandler? OpenDeveloperWindowRequested;

    event EventHandler? OpenMultitoolMenuRequested;

    event EventHandler? ExitRequested;

    bool IsVisible { get; }

    string ToolTipText { get; }

    void Show();

    void UpdatePathOfExileState(bool isRunning);
}
