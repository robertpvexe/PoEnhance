namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerModifierFilterVariantViewModel
{
    public required string Identity { get; init; }

    public required string Label { get; init; }

    public required string Description { get; init; }

    public bool SupportsValueBounds { get; init; }

    /// <summary>
    /// Closed ComboBox / accessibility fallback. Never expose the CLR type name
    /// (<c>PriceChecker...</c>) or record dump as the visible Mod Type.
    /// </summary>
    public override string ToString() =>
        string.IsNullOrWhiteSpace(Label) ? "Unsupported" : Label.Trim();

    /// <summary>
    /// Short, user-facing hover text. Prefer the stable Mod Type label over long
    /// technical descriptions that can leak identities or debug detail.
    /// </summary>
    public string DisplayToolTip
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(Label) ? null : Label.Trim();
            if (label is null)
            {
                return "Unsupported";
            }

            var description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            if (description is null ||
                string.Equals(description, label, StringComparison.OrdinalIgnoreCase) ||
                description.Contains("PriceChecker", StringComparison.Ordinal) ||
                description.Contains("ViewModel", StringComparison.Ordinal) ||
                description.Contains("Identity =", StringComparison.Ordinal) ||
                description.Length > 80)
            {
                return label;
            }

            return description;
        }
    }
}
