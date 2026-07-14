namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerModifierViewModel
{
    public required int SourceIndex { get; init; }

    public required string Text { get; init; }

    public string SectionLabel { get; init; } = string.Empty;

    public bool IsSelected { get; init; }
}
