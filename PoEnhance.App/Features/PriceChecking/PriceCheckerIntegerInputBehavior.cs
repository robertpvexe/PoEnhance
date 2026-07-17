using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PoEnhance.App.Features.PriceChecking;

public static class PriceCheckerIntegerInputBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(PriceCheckerIntegerInputBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    internal static bool IsTextAllowed(string? text) =>
        string.IsNullOrEmpty(text) || text.All(char.IsAsciiDigit);

    internal static bool IsPasteTextAllowed(string? text) =>
        !string.IsNullOrEmpty(text) && text.All(char.IsAsciiDigit);

    internal static string ProspectiveText(
        string? currentText,
        int selectionStart,
        int selectionLength,
        string? insertedText)
    {
        currentText ??= string.Empty;
        insertedText ??= string.Empty;
        selectionStart = Math.Clamp(selectionStart, 0, currentText.Length);
        selectionLength = Math.Clamp(selectionLength, 0, currentText.Length - selectionStart);
        return currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, insertedText);
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        if (e.OldValue is true)
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }

        if (e.NewValue is true)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(textBox, OnPasting);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox && !IsTextAllowed(ProspectiveText(
                textBox.Text,
                textBox.SelectionStart,
                textBox.SelectionLength,
                e.Text)))
        {
            e.Handled = true;
        }
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox ||
            !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, autoConvert: true) ||
            e.SourceDataObject.GetData(DataFormats.UnicodeText, autoConvert: true) is not string text ||
            !IsPasteTextAllowed(text) ||
            !IsTextAllowed(ProspectiveText(
                textBox.Text,
                textBox.SelectionStart,
                textBox.SelectionLength,
                text)))
        {
            e.CancelCommand();
        }
    }
}
