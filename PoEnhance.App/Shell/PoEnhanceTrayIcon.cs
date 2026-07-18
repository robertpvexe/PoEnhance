using System.Drawing;
using System.Windows.Forms;

namespace PoEnhance.App.Shell;

internal sealed class PoEnhanceTrayIcon : IPoEnhanceTrayIcon
{
    private const string OpenMenuText = "Open developer window";
    private const string ExitMenuText = "Exit PoEnhance";

    private readonly Icon icon;
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip contextMenu;
    private bool isDisposed;

    public PoEnhanceTrayIcon()
        : this(PoEnhanceIconLoader.LoadDrawingIcon())
    {
    }

    internal PoEnhanceTrayIcon(Icon icon)
    {
        this.icon = icon;
        contextMenu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem(OpenMenuText);
        var exitItem = new ToolStripMenuItem(ExitMenuText);
        openItem.Click += (_, _) => OpenDeveloperWindowRequested?.Invoke(this, EventArgs.Empty);
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = icon,
            Text = CreateToolTipText(isRunning: false),
            Visible = false,
        };
        notifyIcon.MouseDoubleClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                OpenDeveloperWindowRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler? OpenDeveloperWindowRequested;

    public event EventHandler? ExitRequested;

    public bool IsVisible => !isDisposed && notifyIcon.Visible;

    public string ToolTipText => notifyIcon.Text;

    internal static IReadOnlyList<string> MenuItemTexts => [OpenMenuText, ExitMenuText];

    public static string CreateToolTipText(bool isRunning)
    {
        return isRunning
            ? "PoEnhance — Path of Exile: Running"
            : "PoEnhance — Path of Exile: Not running";
    }

    public void Show()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        notifyIcon.Visible = true;
    }

    public void UpdatePathOfExileState(bool isRunning)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        notifyIcon.Text = CreateToolTipText(isRunning);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        contextMenu.Dispose();
        icon.Dispose();
        isDisposed = true;
    }
}
