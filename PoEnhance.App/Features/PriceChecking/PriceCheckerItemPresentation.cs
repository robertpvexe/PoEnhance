using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerItemPresentation
{
    public string? SocketText { get; init; }

    public string? LinkText { get; init; }

    // This is provider presentation metadata, retained only in the App layer.
    public string? CategoryDisplayLabel { get; init; }

    public static PriceCheckerItemPresentation FromParsedItem(ParsedItem parsedItem)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);

        var socketProperty = parsedItem.Properties.FirstOrDefault(property =>
            string.Equals(property.NormalizedName, "sockets", StringComparison.Ordinal));
        if (socketProperty is null || string.IsNullOrWhiteSpace(socketProperty.RawValueText))
        {
            return new PriceCheckerItemPresentation();
        }

        var socketValue = socketProperty.RawValueText;

        var largestLink = socketValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(group => group.Count(character => character == '-') + 1)
            .DefaultIfEmpty(0)
            .Max();

        return new PriceCheckerItemPresentation
        {
            SocketText = socketValue,
            LinkText = largestLink > 1 ? largestLink.ToString() : null,
        };
    }
}
