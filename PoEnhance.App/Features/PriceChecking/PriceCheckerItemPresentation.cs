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

        var socketText = parsedItem.PropertyLines
            .Select(line => line?.Trim())
            .FirstOrDefault(line => line?.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase) == true);
        if (string.IsNullOrWhiteSpace(socketText))
        {
            return new PriceCheckerItemPresentation();
        }

        var socketValue = socketText["Sockets:".Length..].Trim();
        if (socketValue.Length == 0)
        {
            return new PriceCheckerItemPresentation();
        }

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
