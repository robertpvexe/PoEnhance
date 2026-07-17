using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PoEnhance.App.Features.PriceChecking;

public static class PriceCheckerDecimalInputBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(PriceCheckerDecimalInputBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

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

    internal static bool IsProspectiveTextAllowed(
        string? currentText,
        int selectionStart,
        int selectionLength,
        string? insertedText) =>
        IsTextAllowed(ProspectiveText(currentText, selectionStart, selectionLength, insertedText));

    internal static bool IsTextAllowed(string? text)
    {
        if (string.IsNullOrEmpty(text) || text is "." or ",")
        {
            return true;
        }

        var separator = '\0';
        foreach (var character in text)
        {
            if (char.IsAsciiDigit(character))
            {
                continue;
            }

            if (character is not ('.' or ',') || separator != '\0')
            {
                return false;
            }

            separator = character;
        }

        return true;
    }

    internal static bool IsPasteTextAllowed(string? text)
    {
        if (string.IsNullOrEmpty(text) || !IsTextAllowed(text))
        {
            return false;
        }

        return char.IsAsciiDigit(text[0]) && char.IsAsciiDigit(text[^1]);
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
        if (sender is TextBox textBox && !IsProspectiveTextAllowed(
                textBox.Text,
                textBox.SelectionStart,
                textBox.SelectionLength,
                e.Text))
        {
            e.Handled = true;
        }
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox ||
            !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, autoConvert: true) ||
            e.SourceDataObject.GetData(DataFormats.UnicodeText, autoConvert: true) is not string pastedText ||
            !IsPasteTextAllowed(pastedText) ||
            !IsProspectiveTextAllowed(
                textBox.Text,
                textBox.SelectionStart,
                textBox.SelectionLength,
                pastedText))
        {
            e.CancelCommand();
        }
    }
}
