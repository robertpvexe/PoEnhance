using System.IO;
using System.Text.Json;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeItemPropertyMappingResourceLoader
{
    internal const string DefaultResourceName =
        "PoEnhance.App.Infrastructure.Trade.PathOfExile.Data.item-property-filter-mappings.json";
    private const int SupportedSchemaVersion = 1;
    private static readonly Lazy<PathOfExileTradeItemPropertyMappingResourceLoadResult> DefaultLoad =
        new(LoadEmbeddedDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    public PathOfExileTradeItemPropertyMappingCatalog LoadDefaultOrThrow()
    {
        var result = DefaultLoad.Value;
        if (result.IsSuccess && result.Catalog is not null)
        {
            return result.Catalog;
        }

        var message = result.Diagnostics.FirstOrDefault()?.Message ??
            "The reviewed Path of Exile item-property mapping resource could not be initialized.";
        throw new InvalidOperationException(message);
    }

    internal PathOfExileTradeItemPropertyMappingResourceLoadResult Load(
        Stream? stream,
        string sourceName)
    {
        if (stream is null)
        {
            return Failure(
                PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.MissingResource,
                $"The reviewed Path of Exile item-property mapping resource '{sourceName}' was not found.");
        }

        try
        {
            using var document = JsonDocument.Parse(stream);
            return Parse(document.RootElement, sourceName);
        }
        catch (JsonException)
        {
            return Failure(
                PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.MalformedResource,
                $"The reviewed Path of Exile item-property mapping resource '{sourceName}' is not valid JSON.");
        }
        catch (IOException)
        {
            return Failure(
                PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.ResourceReadFailed,
                $"The reviewed Path of Exile item-property mapping resource '{sourceName}' could not be read.");
        }
    }

    private static PathOfExileTradeItemPropertyMappingResourceLoadResult LoadEmbeddedDefault()
    {
        var assembly = typeof(PathOfExileTradeItemPropertyMappingResourceLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(DefaultResourceName);
        return new PathOfExileTradeItemPropertyMappingResourceLoader().Load(stream, DefaultResourceName);
    }

    private static PathOfExileTradeItemPropertyMappingResourceLoadResult Parse(
        JsonElement root,
        string sourceName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out var schemaVersionElement) ||
            !schemaVersionElement.TryGetInt32(out var schemaVersion) ||
            schemaVersion != SupportedSchemaVersion ||
            string.IsNullOrWhiteSpace(ReadString(root, "reviewReference")) ||
            !root.TryGetProperty("mappings", out var mappingsElement) ||
            mappingsElement.ValueKind != JsonValueKind.Array)
        {
            return Failure(
                PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.MalformedResource,
                $"The reviewed Path of Exile item-property mapping resource '{sourceName}' has an unsupported or incomplete root shape.");
        }

        var mappings = new List<PathOfExileTradeItemPropertyMapping>();
        var seenKinds = new HashSet<TradeSearchItemPropertyKind>();
        var seenProviderIdentities = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var element in mappingsElement.EnumerateArray())
        {
            if (!TryParseMapping(element, out var mapping, out var reason))
            {
                return Failure(
                    PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.MalformedMapping,
                    $"Mapping at index {index} in '{sourceName}' is invalid: {reason}");
            }

            if (!seenKinds.Add(mapping.Kind))
            {
                return Failure(
                    PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.DuplicateKind,
                    $"The reviewed mapping resource contains duplicate kind '{mapping.Kind}'.");
            }

            if (mapping.IsSupported)
            {
                var identity = $"{mapping.ProviderGroupId}\n{mapping.ProviderFilterId}";
                if (!seenProviderIdentities.Add(identity))
                {
                    return Failure(
                        PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.DuplicateProviderIdentity,
                        $"The reviewed mapping resource contains duplicate provider identity '{mapping.ProviderGroupId}/{mapping.ProviderFilterId}'.");
                }
            }

            mappings.Add(mapping);
            index++;
        }

        var expectedKinds = Enum.GetValues<TradeSearchItemPropertyKind>();
        if (mappings.Count != expectedKinds.Length || expectedKinds.Any(kind => !seenKinds.Contains(kind)))
        {
            return Failure(
                PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes.IncompleteKindSet,
                "The reviewed mapping resource must contain exactly one entry for every item-property kind.");
        }

        return PathOfExileTradeItemPropertyMappingResourceLoadResult.Success(
            new PathOfExileTradeItemPropertyMappingCatalog(
                ReadString(root, "reviewReference")!.Trim(),
                mappings));
    }

    private static bool TryParseMapping(
        JsonElement element,
        out PathOfExileTradeItemPropertyMapping mapping,
        out string reason)
    {
        mapping = null!;
        reason = string.Empty;
        var kindText = ReadString(element, "kind");
        if (element.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(kindText) ||
            !Enum.TryParse<TradeSearchItemPropertyKind>(kindText, ignoreCase: false, out var kind) ||
            !element.TryGetProperty("supported", out var supportedElement) ||
            supportedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            reason = "kind and boolean supported fields are required.";
            return false;
        }

        var isSupported = supportedElement.GetBoolean();
        var groupId = TrimToNull(ReadString(element, "providerGroupId"));
        var filterId = TrimToNull(ReadString(element, "providerFilterId"));
        var officialText = TrimToNull(ReadString(element, "expectedOfficialText"));
        var unsupportedReason = TrimToNull(ReadString(element, "unsupportedReason"));
        var hasMinMax = element.TryGetProperty("requiresNumericMinMax", out var minMaxElement) &&
            minMaxElement.ValueKind is JsonValueKind.True or JsonValueKind.False;
        var requiresMinMax = hasMinMax && minMaxElement.GetBoolean();

        if (isSupported &&
            (groupId is null || filterId is null || officialText is null || !requiresMinMax))
        {
            reason = "supported mappings require group, filter, official text, and numeric min/max capability.";
            return false;
        }

        if (!isSupported &&
            (groupId is not null || filterId is not null || officialText is not null || unsupportedReason is null))
        {
            reason = "unsupported mappings require a reason and may not contain provider identity fields.";
            return false;
        }

        mapping = new PathOfExileTradeItemPropertyMapping
        {
            Kind = kind,
            IsSupported = isSupported,
            ProviderGroupId = groupId,
            ProviderFilterId = filterId,
            ExpectedOfficialText = officialText,
            RequiresNumericMinMax = requiresMinMax,
            UnsupportedReason = unsupportedReason,
        };
        return true;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static PathOfExileTradeItemPropertyMappingResourceLoadResult Failure(
        string code,
        string message)
    {
        return PathOfExileTradeItemPropertyMappingResourceLoadResult.Failure(
            new PathOfExileTradeItemPropertyMappingResourceDiagnostic(code, message));
    }
}

internal static class PathOfExileTradeItemPropertyMappingResourceDiagnosticCodes
{
    public const string MissingResource = "POE_TRADE_ITEM_PROPERTY_MAPPING_RESOURCE_MISSING";
    public const string ResourceReadFailed = "POE_TRADE_ITEM_PROPERTY_MAPPING_RESOURCE_READ_FAILED";
    public const string MalformedResource = "POE_TRADE_ITEM_PROPERTY_MAPPING_RESOURCE_MALFORMED";
    public const string MalformedMapping = "POE_TRADE_ITEM_PROPERTY_MAPPING_MALFORMED";
    public const string DuplicateKind = "POE_TRADE_ITEM_PROPERTY_MAPPING_DUPLICATE_KIND";
    public const string DuplicateProviderIdentity = "POE_TRADE_ITEM_PROPERTY_MAPPING_DUPLICATE_PROVIDER_IDENTITY";
    public const string IncompleteKindSet = "POE_TRADE_ITEM_PROPERTY_MAPPING_INCOMPLETE_KIND_SET";
}

internal sealed record PathOfExileTradeItemPropertyMappingResourceDiagnostic(
    string Code,
    string Message);

internal sealed record PathOfExileTradeItemPropertyMappingResourceLoadResult
{
    public bool IsSuccess => Catalog is not null;

    public PathOfExileTradeItemPropertyMappingCatalog? Catalog { get; init; }

    public IReadOnlyList<PathOfExileTradeItemPropertyMappingResourceDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeItemPropertyMappingResourceLoadResult Success(
        PathOfExileTradeItemPropertyMappingCatalog catalog)
    {
        return new PathOfExileTradeItemPropertyMappingResourceLoadResult { Catalog = catalog };
    }

    public static PathOfExileTradeItemPropertyMappingResourceLoadResult Failure(
        params PathOfExileTradeItemPropertyMappingResourceDiagnostic[] diagnostics)
    {
        return new PathOfExileTradeItemPropertyMappingResourceLoadResult { Diagnostics = diagnostics };
    }
}
