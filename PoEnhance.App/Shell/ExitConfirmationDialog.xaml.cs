using System.Windows;

namespace PoEnhance.App.Shell;

internal partial class ExitConfirmationDialog : Window
{
    private ExitConfirmationDialog(Window owner)
    {
        InitializeComponent();
        Owner = owner;
    }

    public static bool Confirm(Window owner)
    {
        return new ExitConfirmationDialog(owner).ShowDialog() == true;
    }

    private void ExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
