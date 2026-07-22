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
            Item = ParseItem(itemElement, resultIndex, diagnostics),
            Listing = ParseListing(listingElement, resultIndex, diagnostics),
        };
        return true;
    }

    private static PathOfExileTradeFetchedItem ParseItem(
        JsonElement itemElement,
        int resultIndex,
        List<PathOfExileTradeHttpDiagnostic> diagnostics)
    {
        return new PathOfExileTradeFetchedItem
        {
            Id = ReadOptionalString(itemElement, "id"),
            FrameType = ReadOptionalInt(itemElement, "frameType"),
            Rarity = ReadOptionalString(itemElement, "rarity"),
            Name = ReadOptionalString(itemElement, "name"),
            TypeLine = ReadOptionalString(itemElement, "typeLine"),
            BaseType = ReadOptionalString(itemElement, "baseType"),
            Icon = ReadOptionalString(itemElement, "icon"),
            ItemLevel = ReadOptionalInt(itemElement, "ilvl"),
            Identified = ReadOptionalBool(itemElement, "identified"),
            Corrupted = ReadOptionalBool(itemElement, "corrupted"),
            Mirrored = ReadOptionalBool(itemElement, "mirrored"),
            Split = ReadOptionalBool(itemElement, "split"),
            Synthesised = ReadOptionalBool(itemElement, "synthesised"),
            Fractured = ReadOptionalBool(itemElement, "fractured"),
            Duplicated = ReadOptionalBool(itemElement, "duplicated"),
            Replica = ReadOptionalBool(itemElement, "replica"),
            Veiled = ReadOptionalBool(itemElement, "veiled"),
            IsRelic = ReadOptionalBool(itemElement, "isRelic"),
            Ruthless = ReadOptionalBool(itemElement, "ruthless"),
            Influences = ParseInfluences(itemElement, resultIndex, diagnostics),
            Searing = ReadOptionalBool(itemElement, "searing"),
            Tangled = ReadOptionalBool(itemElement, "tangled"),
            Properties = ParseItemProperties(
                itemElement,
                "properties",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemProperties,
                resultIndex,
                diagnostics),
            Requirements = ParseItemProperties(
                itemElement,
                "requirements",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemRequirements,
                resultIndex,
                diagnostics),
            Sockets = ParseSockets(itemElement, resultIndex, diagnostics),
            ImplicitMods = ReadOptionalStringArray(
                itemElement,
                "implicitMods",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemModifierSection,
                resultIndex,
                diagnostics),
            ExplicitMods = ReadOptionalStringArray(
                itemElement,
                "explicitMods",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemModifierSection,
                resultIndex,
                diagnostics),
            CraftedMods = ReadOptionalStringArray(
                itemElement,
                "craftedMods",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemModifierSection,
                resultIndex,
                diagnostics),
            FracturedMods = ReadOptionalStringArray(
                itemElement,
                "fracturedMods",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemModifierSection,
                resultIndex,
                diagnostics),
            EnchantMods = ReadOptionalStringArray(
                itemElement,
                "enchantMods",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemModifierSection,
                resultIndex,
                diagnostics),
            UtilityMods = ReadOptionalStringArray(
                itemElement,
                "utilityMods",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemModifierSection,
                resultIndex,
                diagnostics),
            CosmeticMods = ReadOptionalStringArray(
                itemElement,
                "cosmeticMods",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemModifierSection,
                resultIndex,
                diagnostics),
            Description = ReadOptionalString(itemElement, "descrText"),
            SecondaryDescription = ReadOptionalString(itemElement, "secDescrText"),
            FlavourText = ReadOptionalStringArray(
                itemElement,
                "flavourText",
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemFlavourText,
                resultIndex,
                diagnostics),
        };
    }

    private static IReadOnlyList<PathOfExileTradeItemProperty> ParseItemProperties(
        JsonElement itemElement,
        string propertyName,
        string diagnosticCode,
        int resultIndex,
        List<PathOfExileTradeHttpDiagnostic> diagnostics)
    {
        if (!itemElement.TryGetProperty(propertyName, out var propertiesElement) ||
            propertiesElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (propertiesElement.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(MalformedItemSection(
                diagnosticCode,
                resultIndex,
                $"Trade fetch item {propertyName} must be an array."));
            return [];
        }

        var properties = new List<PathOfExileTradeItemProperty>();
        var propertyIndex = 0;
        foreach (var propertyElement in propertiesElement.EnumerateArray())
        {
            if (TryParseItemProperty(propertyElement, out var property, out var malformedMetadata))
            {
                properties.Add(property);
                if (malformedMetadata)
                {
                    diagnostics.Add(MalformedItemSection(
                        diagnosticCode,
                        resultIndex,
                        $"Trade fetch item {propertyName} entry at index {propertyIndex} has malformed optional display metadata."));
                }
            }
            else
            {
                diagnostics.Add(MalformedItemSection(
                    diagnosticCode,
                    resultIndex,
                    $"Trade fetch item {propertyName} entry at index {propertyIndex} is malformed and was ignored."));
            }

            propertyIndex++;
        }

        return properties.ToArray();
    }

    private static bool TryParseItemProperty(
        JsonElement propertyElement,
        out PathOfExileTradeItemProperty property,
        out bool malformedMetadata)
    {
        property = null!;
        malformedMetadata = false;
        if (propertyElement.ValueKind != JsonValueKind.Object ||
            !propertyElement.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String ||
            !propertyElement.TryGetProperty("values", out var valuesElement) ||
            valuesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var values = new List<PathOfExileTradeItemPropertyValue>();
        foreach (var valueElement in valuesElement.EnumerateArray())
        {
            if (valueElement.ValueKind != JsonValueKind.Array || valueElement.GetArrayLength() != 2)
            {
                return false;
            }

            var textElement = valueElement[0];
            var valueTypeElement = valueElement[1];
            if (textElement.ValueKind != JsonValueKind.String ||
                valueTypeElement.ValueKind != JsonValueKind.Number ||
                !valueTypeElement.TryGetInt32(out var valueType) ||
                valueType < 0)
            {
                return false;
            }

            values.Add(new PathOfExileTradeItemPropertyValue
            {
                Text = textElement.GetString() ?? string.Empty,
                ValueType = valueType,
            });
        }

        var displayMode = ReadOptionalInt(propertyElement, "displayMode", out var malformedDisplayMode);
        var progress = ReadOptionalDouble(propertyElement, "progress", out var malformedProgress);
        var type = ReadOptionalInt(propertyElement, "type", out var malformedType);
        var suffix = ReadOptionalString(propertyElement, "suffix", out var malformedSuffix);
        var icon = ReadOptionalString(propertyElement, "icon", out var malformedIcon);
        malformedMetadata = malformedDisplayMode ||
            malformedProgress ||
            malformedType ||
            malformedSuffix ||
            malformedIcon;

        property = new PathOfExileTradeItemProperty
        {
            Name = nameElement.GetString() ?? string.Empty,
            Values = values.ToArray(),
            DisplayMode = displayMode,
            Progress = progress,
            Type = type,
            Suffix = suffix,
            Icon = icon,
        };
        return true;
    }

    private static IReadOnlyList<PathOfExileTradeItemSocket> ParseSockets(
        JsonElement itemElement,
        int resultIndex,
        List<PathOfExileTradeHttpDiagnostic> diagnostics)
    {
        if (!itemElement.TryGetProperty("sockets", out var socketsElement) ||
            socketsElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (socketsElement.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(MalformedItemSection(
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemSockets,
                resultIndex,
                "Trade fetch item sockets must be an array."));
            return [];
        }

        var sockets = new List<PathOfExileTradeItemSocket>();
        var socketIndex = 0;
        foreach (var socketElement in socketsElement.EnumerateArray())
        {
            if (socketElement.ValueKind != JsonValueKind.Object ||
                !socketElement.TryGetProperty("group", out var groupElement) ||
                groupElement.ValueKind != JsonValueKind.Number ||
                !groupElement.TryGetInt32(out var group) ||
                group < 0)
            {
                diagnostics.Add(MalformedItemSection(
                    PathOfExileTradeHttpDiagnosticCodes.MalformedItemSockets,
                    resultIndex,
                    $"Trade fetch item socket at index {socketIndex} is malformed and was ignored."));
                socketIndex++;
                continue;
            }

            var attribute = ReadOptionalString(socketElement, "attr", out var malformedAttribute);
            var colour = ReadOptionalString(socketElement, "sColour", out var malformedColour);
            if (malformedAttribute || malformedColour)
            {
                diagnostics.Add(MalformedItemSection(
                    PathOfExileTradeHttpDiagnosticCodes.MalformedItemSockets,
                    resultIndex,
                    $"Trade fetch item socket at index {socketIndex} has malformed optional display metadata."));
            }

            sockets.Add(new PathOfExileTradeItemSocket
            {
                Group = group,
                Attribute = attribute,
                Colour = colour,
            });
            socketIndex++;
        }

        return sockets.ToArray();
    }

    private static PathOfExileTradeItemInfluences? ParseInfluences(
        JsonElement itemElement,
        int resultIndex,
        List<PathOfExileTradeHttpDiagnostic> diagnostics)
    {
        if (!itemElement.TryGetProperty("influences", out var influencesElement) ||
            influencesElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (influencesElement.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(MalformedItemSection(
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemInfluences,
                resultIndex,
                "Trade fetch item influences must be an object."));
            return null;
        }

        var shaper = ReadOptionalBool(influencesElement, "shaper", out var malformedShaper);
        var elder = ReadOptionalBool(influencesElement, "elder", out var malformedElder);
        var crusader = ReadOptionalBool(influencesElement, "crusader", out var malformedCrusader);
        var hunter = ReadOptionalBool(influencesElement, "hunter", out var malformedHunter);
        var redeemer = ReadOptionalBool(influencesElement, "redeemer", out var malformedRedeemer);
        var warlord = ReadOptionalBool(influencesElement, "warlord", out var malformedWarlord);
        if (malformedShaper ||
            malformedElder ||
            malformedCrusader ||
            malformedHunter ||
            malformedRedeemer ||
            malformedWarlord)
        {
            diagnostics.Add(MalformedItemSection(
                PathOfExileTradeHttpDiagnosticCodes.MalformedItemInfluences,
                resultIndex,
                "Trade fetch item influences contain a non-boolean flag; that flag was ignored."));
        }

        return new PathOfExileTradeItemInfluences
        {
            Shaper = shaper,
            Elder = elder,
            Crusader = crusader,
            Hunter = hunter,
            Redeemer = redeemer,
            Warlord = warlord,
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
        string propertyName,
        string diagnosticCode,
        int resultIndex,
        List<PathOfExileTradeHttpDiagnostic> diagnostics)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(MalformedItemSection(
                diagnosticCode,
                resultIndex,
                $"Trade fetch item {propertyName} must be an array."));
            return [];
        }

        var values = new List<string>();
        var containsMalformedValue = false;
        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                values.Add(element.GetString() ?? string.Empty);
            }
            else
            {
                containsMalformedValue = true;
            }
        }

        if (containsMalformedValue)
        {
            diagnostics.Add(MalformedItemSection(
                diagnosticCode,
                resultIndex,
                $"Trade fetch item {propertyName} contains a non-string value; that value was ignored."));
        }

        return values.ToArray();
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

    private static string? ReadOptionalString(
        JsonElement parent,
        string propertyName,
        out bool malformed)
    {
        malformed = false;
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        malformed = true;
        return null;
    }

    private static int? ReadOptionalInt(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out var value) &&
            value >= 0
            ? value
            : null;
    }

    private static int? ReadOptionalInt(
        JsonElement parent,
        string propertyName,
        out bool malformed)
    {
        malformed = false;
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out var value) &&
            value >= 0)
        {
            return value;
        }

        malformed = true;
        return null;
    }

    private static double? ReadOptionalDouble(
        JsonElement parent,
        string propertyName,
        out bool malformed)
    {
        malformed = false;
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetDouble(out var value) &&
            double.IsFinite(value))
        {
            return value;
        }

        malformed = true;
        return null;
    }

    private static bool? ReadOptionalBool(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? element.GetBoolean()
            : null;
    }

    private static bool? ReadOptionalBool(
        JsonElement parent,
        string propertyName,
        out bool malformed)
    {
        malformed = false;
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return element.GetBoolean();
        }

        malformed = true;
        return null;
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

    private static PathOfExileTradeHttpDiagnostic MalformedItemSection(
        string diagnosticCode,
        int resultIndex,
        string message)
    {
        return new PathOfExileTradeHttpDiagnostic(
            diagnosticCode,
            $"{message} Index: {resultIndex}.",
            ResultIndex: resultIndex);
    }
}
