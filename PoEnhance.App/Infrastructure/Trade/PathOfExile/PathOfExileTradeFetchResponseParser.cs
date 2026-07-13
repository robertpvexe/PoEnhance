using System.Globalization;
using System.Text.Json;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeFetchResponseParser
{
    public PathOfExileTradeFetchResponseParseResult ParseFetchResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return TopLevelFailure("The Trade fetch response body is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var errorElement))
            {
                return ConvertProviderError(PathOfExileTradeProviderErrorParser.Parse(errorElement));
            }

            if (!root.TryGetProperty("result", out var resultElement))
            {
                return TopLevelFailure("A Trade fetch response requires a result collection.");
            }

            if (resultElement.ValueKind != JsonValueKind.Array)
            {
                return TopLevelFailure("The Trade fetch response result field must be an array.");
            }

            var diagnostics = new List<PathOfExileTradeHttpDiagnostic>();
            var offers = new List<PathOfExileTradeFetchedOffer>();
            var index = 0;
            foreach (var offerElement in resultElement.EnumerateArray())
            {
                if (TryParseOffer(offerElement, index, diagnostics, out var offer))
                {
                    offers.Add(offer);
                }

                index++;
            }

            return PathOfExileTradeFetchResponseParseResult.Success(
                new PathOfExileTradeFetchResponse
                {
                    Result = offers,
                },
                diagnostics);
        }
        catch (JsonException)
        {
            return TopLevelFailure("The Trade fetch response body is not valid JSON.");
        }
    }

    private static bool TryParseOffer(
        JsonElement offerElement,
        int resultIndex,
        List<PathOfExileTradeHttpDiagnostic> diagnostics,
        out PathOfExileTradeFetchedOffer offer)
    {
        offer = null!;
        if (offerElement.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(MalformedOffer(
                resultIndex,
                "A Trade fetch result entry must be an object."));
            return false;
        }

        if (!TryReadString(offerElement, "id", out var resultId))
        {
            diagnostics.Add(MalformedOffer(
                resultIndex,
                "A Trade fetch result entry requires a result id."));
            return false;
        }

        if (!offerElement.TryGetProperty("item", out var itemElement) ||
            itemElement.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(MalformedOffer(
                resultIndex,
                "A Trade fetch result entry requires an item object."));
            return false;
        }

        if (!offerElement.TryGetProperty("listing", out var listingElement) ||
            listingElement.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(MalformedOffer(
                resultIndex,
                "A Trade fetch result entry requires a listing object."));
            return false;
        }

        offer = new PathOfExileTradeFetchedOffer
        {
            Id = resultId,
            Item = ParseItem(itemElement),
            Listing = ParseListing(listingElement, resultIndex, diagnostics),
        };
        return true;
    }

    private static PathOfExileTradeFetchedItem ParseItem(JsonElement itemElement)
    {
        return new PathOfExileTradeFetchedItem
        {
            Id = ReadOptionalString(itemElement, "id"),
            Name = ReadOptionalString(itemElement, "name"),
            TypeLine = ReadOptionalString(itemElement, "typeLine"),
            BaseType = ReadOptionalString(itemElement, "baseType"),
            Icon = ReadOptionalString(itemElement, "icon"),
            ItemLevel = ReadOptionalInt(itemElement, "ilvl"),
            Identified = ReadOptionalBool(itemElement, "identified"),
            Corrupted = ReadOptionalBool(itemElement, "corrupted"),
            Mirrored = ReadOptionalBool(itemElement, "mirrored"),
            ImplicitMods = ReadOptionalStringArray(itemElement, "implicitMods"),
            ExplicitMods = ReadOptionalStringArray(itemElement, "explicitMods"),
            CraftedMods = ReadOptionalStringArray(itemElement, "craftedMods"),
            FracturedMods = ReadOptionalStringArray(itemElement, "fracturedMods"),
            EnchantMods = ReadOptionalStringArray(itemElement, "enchantMods"),
        };
    }

    private static PathOfExileTradeListing ParseListing(
        JsonElement listingElement,
        int resultIndex,
        List<PathOfExileTradeHttpDiagnostic> diagnostics)
    {
        var rawIndexed = ReadOptionalString(listingElement, "indexed");
        DateTimeOffset? indexed = null;
        if (rawIndexed is not null)
        {
            if (DateTimeOffset.TryParse(
                rawIndexed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedIndexed))
            {
                indexed = parsedIndexed;
            }
            else
            {
                diagnostics.Add(new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.MalformedIndexedTimestamp,
                    $"Trade fetch result entry at index {resultIndex} has a malformed indexed timestamp.",
                    ResultIndex: resultIndex));
            }
        }

        return new PathOfExileTradeListing
        {
            Method = ReadOptionalString(listingElement, "method"),
            Indexed = indexed,
            RawIndexed = rawIndexed,
            Whisper = ReadOptionalString(listingElement, "whisper"),
            Account = ParseAccount(listingElement),
            Price = ParsePrice(listingElement, resultIndex, diagnostics),
        };
    }

    private static PathOfExileTradeListingAccount? ParseAccount(JsonElement listingElement)
    {
        if (!listingElement.TryGetProperty("account", out var accountElement) ||
            accountElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new PathOfExileTradeListingAccount
        {
            Name = ReadOptionalString(accountElement, "name"),
            LastCharacterName = ReadOptionalString(accountElement, "lastCharacterName"),
            Online = ParseOnlineState(accountElement),
        };
    }

    private static PathOfExileTradeListingOnlineState? ParseOnlineState(JsonElement accountElement)
    {
        if (!accountElement.TryGetProperty("online", out var onlineElement) ||
            onlineElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new PathOfExileTradeListingOnlineState
        {
            League = ReadOptionalString(onlineElement, "league"),
            Status = ReadOptionalString(onlineElement, "status"),
        };
    }

    private static PathOfExileTradeListingPrice? ParsePrice(
        JsonElement listingElement,
        int resultIndex,
        List<PathOfExileTradeHttpDiagnostic> diagnostics)
    {
        if (!listingElement.TryGetProperty("price", out var priceElement) ||
            priceElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (priceElement.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(MalformedPrice(
                resultIndex,
                "A Trade fetch listing price must be an object."));
            return null;
        }

        decimal? amount = null;
        if (priceElement.TryGetProperty("amount", out var amountElement))
        {
            if (amountElement.ValueKind == JsonValueKind.Number &&
                amountElement.TryGetDecimal(out var parsedAmount))
            {
                amount = parsedAmount;
            }
            else
            {
                diagnostics.Add(MalformedPrice(
                    resultIndex,
                    "A Trade fetch listing price amount must be a JSON number."));
                return null;
            }
        }

        return new PathOfExileTradeListingPrice
        {
            Type = ReadOptionalString(priceElement, "type"),
            Amount = amount,
            Currency = ReadOptionalString(priceElement, "currency"),
        };
    }

    private static IReadOnlyList<string> ReadOptionalStringArray(
        JsonElement parent,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value
            .EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();
    }

    private static bool TryReadString(
        JsonElement parent,
        string propertyName,
        out string value)
    {
        if (parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(element.GetString()))
        {
            value = element.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? ReadOptionalString(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static int? ReadOptionalInt(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool? ReadOptionalBool(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? element.GetBoolean()
            : null;
    }

    private static PathOfExileTradeFetchResponseParseResult ConvertProviderError(
        PathOfExileTradeResponseParseResult parseResult)
    {
        if (parseResult.ProviderError is not null)
        {
            return PathOfExileTradeFetchResponseParseResult.ProviderFailure(parseResult.ProviderError);
        }

        return PathOfExileTradeFetchResponseParseResult.Failure(
            parseResult.Diagnostics
                .Select(diagnostic => new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.MalformedProviderError,
                    diagnostic.Message))
                .ToArray());
    }

    private static PathOfExileTradeFetchResponseParseResult TopLevelFailure(string message)
    {
        return PathOfExileTradeFetchResponseParseResult.Failure(
            new PathOfExileTradeHttpDiagnostic(
                PathOfExileTradeHttpDiagnosticCodes.MalformedResponse,
                message));
    }

    private static PathOfExileTradeHttpDiagnostic MalformedOffer(
        int resultIndex,
        string message)
    {
        return new PathOfExileTradeHttpDiagnostic(
            PathOfExileTradeHttpDiagnosticCodes.MalformedOffer,
            $"{message} Index: {resultIndex}.",
            ResultIndex: resultIndex);
    }

    private static PathOfExileTradeHttpDiagnostic MalformedPrice(
        int resultIndex,
        string message)
    {
        return new PathOfExileTradeHttpDiagnostic(
            PathOfExileTradeHttpDiagnosticCodes.MalformedPrice,
            $"{message} Index: {resultIndex}.",
            ResultIndex: resultIndex);
    }
}
