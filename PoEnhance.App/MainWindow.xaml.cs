using System.Windows;
using System.Windows.Threading;
using PoEnhance.App.Infrastructure.PathOfExile;
using Serilog;

namespace PoEnhance.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly PathOfExileProcessDetector pathOfExileProcessDetector = new();
    private readonly DispatcherTimer pathOfExileStatusTimer;
    private bool? lastPathOfExileRunning;

    public MainWindow()
    {
        InitializeComponent();

        pathOfExileStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        pathOfExileStatusTimer.Tick += (_, _) => RefreshPathOfExileStatus();

        Loaded += (_, _) =>
        {
            RefreshPathOfExileStatus();
            pathOfExileStatusTimer.Start();
        };

        Closed += (_, _) => pathOfExileStatusTimer.Stop();
    }

    private void RefreshPathOfExileStatus()
    {
        bool isRunning = pathOfExileProcessDetector.IsPathOfExileRunning();

        PathOfExileStatusText.Text = isRunning
            ? "Path of Exile: Running"
            : "Path of Exile: Not running";

        if (lastPathOfExileRunning == isRunning)
        {
            return;
        }

        if (isRunning)
        {
            Log.Information("Path of Exile detected");
        }
        else if (lastPathOfExileRunning is true)
        {
            Log.Information("Path of Exile no longer detected");
        }

        lastPathOfExileRunning = isRunning;
    }
}
