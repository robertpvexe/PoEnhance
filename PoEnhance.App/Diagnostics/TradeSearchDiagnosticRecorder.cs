using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Diagnostics;

/// <summary>
/// Opt-in capture of the final Price Checker Trade Search request/response.
/// Enabled when <see cref="EnvironmentVariableName"/> is set, or when the existing
/// E6b modifier-pipeline diagnostic directory is set.
/// </summary>
internal static class TradeSearchDiagnosticRecorder
{
    public const string EnvironmentVariableName = "POENHANCE_TRADE_SEARCH_DIAGNOSTIC_DIR";
    public const string DiagnosticVersion = "E6b-trade-search-1";

    private static readonly object SequenceGate = new();
    private static int sequence;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static bool IsEnabled => !string.IsNullOrWhiteSpace(ResolveOutputDirectory());

    public static void TryCapture(TradeSearchDiagnosticInput input)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            WriteCapture(input, ResolveOutputDirectory()!);
        }
        catch
        {
            // Diagnostics must never affect Search semantics.
        }
    }

    internal static TradeSearchDiagnosticCapture BuildCapture(TradeSearchDiagnosticInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var draft = input.Draft;
        var request = input.BuildResult?.Request;
        var serializedJson = input.BuildResult?.SerializedJson;
        if (serializedJson is null && request is not null)
        {
            serializedJson = PathOfExileTradeJson.SerializeSearchRequest(request);
        }

        var selectedModifierIndexes = draft is null
            ? []
            : draft.ModifierFilters
                .Select((component, index) => (component, index))
                .Where(entry => entry.component.IsSelected)
                .ToArray();

        var provenance = selectedModifierIndexes
            .Select(entry => BuildProvenance(
                entry.component,
                entry.index,
                input.MappedModifierFilters,
                input.MappingDiagnostics,
                request))
            .ToArray();

        var league = Trim(input.LeagueIdentifier) ?? Trim(input.BuildResult?.LeagueIdentifier);
        var endpointPath = league is null
            ? null
            : $"/api/trade/search/{Uri.EscapeDataString(league)}";

        return new TradeSearchDiagnosticCapture
        {
            DiagnosticVersion = DiagnosticVersion,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Sequence = input.Sequence,
            Context = new TradeSearchDiagnosticContextCapture
            {
                League = league,
                EndpointPath = endpointPath,
                ItemName = request?.Query.Name ?? Trim(draft?.DisplayName),
                ItemType = request?.Query.Type ??
                    Trim(input.ProviderItemIdentity?.CanonicalType) ??
                    Trim(draft?.ParsedBaseType) ??
                    Trim(draft?.Base.ResolvedBaseName),
                ItemClass = Trim(draft?.ItemClass),
                Rarity = Trim(draft?.Rarity),
                ListingStatus = request?.Query.Status.Option ?? MapListingStatus(draft?.ListingMode),
                ProviderCanonicalName = Trim(input.ProviderItemIdentity?.CanonicalName),
                ProviderCanonicalType = Trim(input.ProviderItemIdentity?.CanonicalType),
                Stage = input.Stage,
            },
            SelectedInputs = new TradeSearchDiagnosticSelectedInputsCapture
            {
                Modifiers = (draft?.ModifierFilters ?? [])
                    .Where(component => component.IsSelected)
                    .Select(BuildSelectedModifier)
                    .ToArray(),
                Properties = (draft?.ItemProperties ?? [])
                    .Where(property => property.IsSelected)
                    .Select(BuildSelectedProperty)
                    .ToArray(),
                RequestedItemFilters = (draft?.RequestedItemFilters ?? [])
                    .Where(filter => filter.IsActive)
                    .Select(filter => new TradeSearchDiagnosticRequestedItemFilterCapture
                    {
                        Kind = filter.Kind.ToString(),
                        Label = filter.Label,
                        IsActive = filter.IsActive,
                        Minimum = filter.RequestedMinimum,
                    })
                    .ToArray(),
            },
            Provenance = provenance,
            FinalTradeFilters = BuildFinalTradeFilters(request),
            FinalRequestJson = serializedJson,
            FinalRequest = ParseRequestNode(serializedJson),
            Response = BuildResponse(input),
        };
    }

    private static void WriteCapture(TradeSearchDiagnosticInput input, string outputDirectory)
    {
        var sequence = input.Sequence > 0 ? input.Sequence : NextSequence();
        var capture = BuildCapture(input with { Sequence = sequence });

        Directory.CreateDirectory(outputDirectory);
        var itemLabel = SanitizeFileNameSegment(
            capture.Context.ItemName ??
            capture.Context.ItemType ??
            capture.Context.ItemClass ??
            "item");
        var stamp = capture.CapturedAtUtc.ToString("yyyyMMdd-HHmmss-fff");
        var baseName = $"{stamp}-{itemLabel}-search-{sequence:000}";
        var artifactPath = Path.Combine(outputDirectory, $"{baseName}.json");
        File.WriteAllText(artifactPath, JsonSerializer.Serialize(capture, JsonOptions));
        capture.OutputPath = artifactPath;

        if (!string.IsNullOrWhiteSpace(capture.FinalRequestJson))
        {
            var requestPath = Path.Combine(outputDirectory, $"{baseName}-request.json");
            File.WriteAllText(requestPath, PrettyPrintJson(capture.FinalRequestJson));
            capture.RequestPayloadPath = requestPath;
            // Rewrite artifact so paths are present.
            File.WriteAllText(artifactPath, JsonSerializer.Serialize(capture, JsonOptions));
        }
    }

    private static TradeSearchDiagnosticSelectedModifierCapture BuildSelectedModifier(
        ResolvedSearchComponent component)
    {
        return new TradeSearchDiagnosticSelectedModifierCapture
        {
            ComponentId = component.ComponentId,
            DisplayedLabel = string.IsNullOrWhiteSpace(component.OriginalText)
                ? component.CanonicalSignature
                : component.OriginalText,
            SourceModifierIndex = component.SourceModifierIndex,
            SourceLineIndex = component.SourceLineIndex,
            SourceComponentIndex = component.SourceComponentIndex,
            SemanticKind = component.ParsedKind.ToString(),
            ModTypeLabel = ModifierPipelineDiagnosticRecorder.StaticModifierLabel(component),
            ProviderResolutionStatus = component.ProviderResolutionStatus.ToString(),
            ProviderStatId = component.ProviderStatId,
            ProviderStatText = component.ProviderStatText,
            ProviderStatAlternativeIds = component.ProviderStatAlternativeIds.ToArray(),
            ValueBoundShape = component.ValueBoundShape.ToString(),
            IsPresenceOnly = component.ValueBoundShape == ModifierBoundShape.PresenceOnly,
            SelectedMinimum = component.RequestedMinimum,
            SelectedMaximum = component.RequestedMaximum,
            FixedQueryValue = component.FixedQueryValue,
            IsSearchable = component.IsSearchable,
            SupportsEditableBounds = component.SupportsValueBounds,
            IsInteractionReady = ModifierPipelineDiagnosticRecorder.IsInteractionReady(component),
            AvailabilityStatus = component.IsSearchable
                ? "Supported"
                : ModifierPipelineDiagnosticRecorder.ModifierAvailabilityStatus(component),
            NotSearchableReason = component.NotSearchableReason,
        };
    }

    private static TradeSearchDiagnosticSelectedPropertyCapture BuildSelectedProperty(
        TradeSearchItemProperty property)
    {
        return new TradeSearchDiagnosticSelectedPropertyCapture
        {
            PropertyId = property.Kind.ToString(),
            DisplayedLabel = property.Label,
            Kind = property.Kind.ToString(),
            SelectedMinimum = property.RequestedMinimum,
            SelectedMaximum = property.RequestedMaximum,
            IsSearchable = property.IsSearchable,
            SupportsEditableBounds = property.IsSearchable,
            NotSearchableReason = property.NotSearchableReason,
        };
    }

    private static TradeSearchDiagnosticProvenanceCapture BuildProvenance(
        ResolvedSearchComponent component,
        int draftIndex,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter> mappedFilters,
        IReadOnlyList<PathOfExileTradeSelectedModifierMappingDiagnostic> mappingDiagnostics,
        PathOfExileTradeSearchRequest? request)
    {
        var produced = mappedFilters
            .Where(filter => SourceIndexes(filter).Contains(draftIndex))
            .Select(filter => ToProducedFilter(filter, request))
            .ToArray();

        var mappingDiagnostic = mappingDiagnostics.FirstOrDefault(diagnostic =>
            diagnostic.SourceIndex == draftIndex);

        return new TradeSearchDiagnosticProvenanceCapture
        {
            ComponentId = component.ComponentId,
            DraftModifierIndex = draftIndex,
            DisplayedLabel = string.IsNullOrWhiteSpace(component.OriginalText)
                ? component.CanonicalSignature
                : component.OriginalText,
            ProviderStatId = component.ProviderStatId,
            ProviderStatText = component.ProviderStatText,
            ProducedFilterCount = produced.Length,
            ProducedFilters = produced,
            MappingDiagnosticCode = mappingDiagnostic?.Code,
            MappingDiagnosticMessage = mappingDiagnostic?.Message,
            ZeroFilterReason = produced.Length == 0
                ? mappingDiagnostic?.Message ??
                    (component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.BaseGuaranteed
                        ? "Provider resolution is BaseGuaranteed; no Trade stat filter is emitted."
                        : "Selected row produced zero Trade stat filters.")
                : null,
        };
    }

    private static TradeSearchDiagnosticProducedFilterCapture ToProducedFilter(
        PathOfExileTradeSelectedModifierFilter filter,
        PathOfExileTradeSearchRequest? request)
    {
        var serialized = FindSerializedFilters(request, filter);
        return new TradeSearchDiagnosticProducedFilterCapture
        {
            StatId = filter.StatId,
            Minimum = filter.Minimum,
            Maximum = filter.Maximum,
            AlternativeStatIds = filter.Alternatives.Select(alternative => alternative.StatId).ToArray(),
            GroupType = filter.Alternatives.Count > 0 ? "count" : "and",
            SerializedFilters = serialized,
        };
    }

    private static IReadOnlyList<TradeSearchDiagnosticSerializedStatFilterCapture> FindSerializedFilters(
        PathOfExileTradeSearchRequest? request,
        PathOfExileTradeSelectedModifierFilter filter)
    {
        if (request is null)
        {
            return [];
        }

        var wantedIds = filter.Alternatives.Count > 0
            ? filter.Alternatives.Select(alternative => alternative.StatId).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal) { filter.StatId };

        return request.Query.Stats
            .SelectMany(group => group.Filters.Select(statFilter => new
            {
                GroupType = group.Type,
                Filter = statFilter,
            }))
            .Where(entry => wantedIds.Contains(entry.Filter.Id))
            .Select(entry => new TradeSearchDiagnosticSerializedStatFilterCapture
            {
                GroupType = entry.GroupType,
                Id = entry.Filter.Id,
                Minimum = entry.Filter.Value?.Min,
                Maximum = entry.Filter.Value?.Max,
            })
            .ToArray();
    }

    private static TradeSearchDiagnosticFinalFiltersCapture? BuildFinalTradeFilters(
        PathOfExileTradeSearchRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        return new TradeSearchDiagnosticFinalFiltersCapture
        {
            Name = request.Query.Name,
            Type = request.Query.Type,
            ListingStatus = request.Query.Status.Option,
            SortPrice = request.Sort.Price,
            StatsGroups = request.Query.Stats
                .Select(group => new TradeSearchDiagnosticStatsGroupCapture
                {
                    Type = group.Type,
                    GroupMinimum = group.Value?.Min,
                    GroupMaximum = group.Value?.Max,
                    Filters = group.Filters
                        .Select(filter => new TradeSearchDiagnosticSerializedStatFilterCapture
                        {
                            GroupType = group.Type,
                            Id = filter.Id,
                            Minimum = filter.Value?.Min,
                            Maximum = filter.Value?.Max,
                        })
                        .ToArray(),
                })
                .ToArray(),
            QueryFilters = ToJsonNode(request.Query.Filters),
        };
    }

    private static TradeSearchDiagnosticResponseCapture BuildResponse(TradeSearchDiagnosticInput input)
    {
        var search = input.SearchResult;
        var providerError = search?.ProviderError;
        var httpDiagnostic = search?.Diagnostics.FirstOrDefault();
        var priceCheckDiagnostic = input.PriceCheckDiagnostics.FirstOrDefault();

        return new TradeSearchDiagnosticResponseCapture
        {
            HttpSuccess = search?.IsSuccess,
            HttpStatusCode = search?.HttpStatusCode is { } status
                ? (int)status
                : priceCheckDiagnostic?.HttpStatusCode is { } priceCheckStatus
                    ? (int)priceCheckStatus
                    : null,
            IsCancelled = search?.IsCancelled == true || input.IsCancelled,
            IsTimeout = search?.IsTimeout == true,
            SearchId = search?.Response?.Id,
            Total = search?.Response?.Total,
            ResultIdCount = search?.Response?.Result?.Count,
            Inexact = search?.Response?.Inexact,
            ProviderErrorCode = providerError?.Code ??
                httpDiagnostic?.ProviderCode ??
                httpDiagnostic?.Code ??
                priceCheckDiagnostic?.ProviderCode ??
                priceCheckDiagnostic?.SourceCode ??
                priceCheckDiagnostic?.Code,
            ProviderErrorMessage = providerError?.Message ??
                httpDiagnostic?.Message ??
                priceCheckDiagnostic?.Message,
            Stage = input.Stage,
        };
    }

    private static IEnumerable<int> SourceIndexes(PathOfExileTradeSelectedModifierFilter filter)
    {
        if (filter.SourceIndexes.Count > 0)
        {
            return filter.SourceIndexes;
        }

        return [filter.SourceIndex];
    }

    private static JsonNode? ParseRequestNode(string? serializedJson)
    {
        if (string.IsNullOrWhiteSpace(serializedJson))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(serializedJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonNode? ToJsonNode(IReadOnlyDictionary<string, object> filters)
    {
        try
        {
            return JsonSerializer.SerializeToNode(
                filters,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                });
        }
        catch
        {
            return null;
        }
    }

    private static string PrettyPrintJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, JsonOptions);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string? ResolveOutputDirectory()
    {
        var dedicated = Trim(Environment.GetEnvironmentVariable(EnvironmentVariableName));
        if (dedicated is not null)
        {
            return dedicated;
        }

        return Trim(Environment.GetEnvironmentVariable(
            ModifierPipelineDiagnosticRecorder.EnvironmentVariableName));
    }

    private static int NextSequence()
    {
        lock (SequenceGate)
        {
            sequence++;
            return sequence;
        }
    }

    private static string? MapListingStatus(TradeListingMode? listingMode) =>
        listingMode switch
        {
            TradeListingMode.InstantBuyout => "securable",
            TradeListingMode.InPerson => "onlineleague",
            _ => null,
        };

    private static string SanitizeFileNameSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "item" : sanitized[..Math.Min(sanitized.Length, 48)];
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed record TradeSearchDiagnosticInput
{
    public TradeSearchDraft? Draft { get; init; }

    public string? LeagueIdentifier { get; init; }

    public PathOfExileTradeItemIdentity? ProviderItemIdentity { get; init; }

    public IReadOnlyList<PathOfExileTradeSelectedModifierFilter> MappedModifierFilters { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeSelectedModifierMappingDiagnostic> MappingDiagnostics { get; init; } =
        [];

    public PathOfExileTradeQueryBuildResult? BuildResult { get; init; }

    public PathOfExileTradeSearchExecutionResult? SearchResult { get; init; }

    public IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> PriceCheckDiagnostics { get; init; } = [];

    public string Stage { get; init; } = "Search";

    public bool IsCancelled { get; init; }

    public int Sequence { get; init; }
}

internal sealed class TradeSearchDiagnosticCapture
{
    public string DiagnosticVersion { get; init; } = TradeSearchDiagnosticRecorder.DiagnosticVersion;

    public DateTimeOffset CapturedAtUtc { get; init; }

    public int Sequence { get; init; }

    public string? OutputPath { get; set; }

    public string? RequestPayloadPath { get; set; }

    public TradeSearchDiagnosticContextCapture Context { get; init; } = new();

    public TradeSearchDiagnosticSelectedInputsCapture SelectedInputs { get; init; } = new();

    public IReadOnlyList<TradeSearchDiagnosticProvenanceCapture> Provenance { get; init; } = [];

    public TradeSearchDiagnosticFinalFiltersCapture? FinalTradeFilters { get; init; }

    public string? FinalRequestJson { get; init; }

    public JsonNode? FinalRequest { get; init; }

    public TradeSearchDiagnosticResponseCapture Response { get; init; } = new();
}

internal sealed class TradeSearchDiagnosticContextCapture
{
    public string? League { get; init; }

    public string? EndpointPath { get; init; }

    public string? ItemName { get; init; }

    public string? ItemType { get; init; }

    public string? ItemClass { get; init; }

    public string? Rarity { get; init; }

    public string? ListingStatus { get; init; }

    public string? ProviderCanonicalName { get; init; }

    public string? ProviderCanonicalType { get; init; }

    public string? Stage { get; init; }
}

internal sealed class TradeSearchDiagnosticSelectedInputsCapture
{
    public IReadOnlyList<TradeSearchDiagnosticSelectedModifierCapture> Modifiers { get; init; } = [];

    public IReadOnlyList<TradeSearchDiagnosticSelectedPropertyCapture> Properties { get; init; } = [];

    public IReadOnlyList<TradeSearchDiagnosticRequestedItemFilterCapture> RequestedItemFilters { get; init; } =
        [];
}

internal sealed class TradeSearchDiagnosticSelectedModifierCapture
{
    public string ComponentId { get; init; } = string.Empty;

    public string? DisplayedLabel { get; init; }

    public int SourceModifierIndex { get; init; }

    public int SourceLineIndex { get; init; }

    public int SourceComponentIndex { get; init; }

    public string? SemanticKind { get; init; }

    public string? ModTypeLabel { get; init; }

    public string? ProviderResolutionStatus { get; init; }

    public string? ProviderStatId { get; init; }

    public string? ProviderStatText { get; init; }

    public IReadOnlyList<string> ProviderStatAlternativeIds { get; init; } = [];

    public string? ValueBoundShape { get; init; }

    public bool IsPresenceOnly { get; init; }

    public decimal? SelectedMinimum { get; init; }

    public decimal? SelectedMaximum { get; init; }

    public decimal? FixedQueryValue { get; init; }

    public bool IsSearchable { get; init; }

    public bool SupportsEditableBounds { get; init; }

    public bool IsInteractionReady { get; init; }

    public string? AvailabilityStatus { get; init; }

    public string? NotSearchableReason { get; init; }
}

internal sealed class TradeSearchDiagnosticSelectedPropertyCapture
{
    public string? PropertyId { get; init; }

    public string? DisplayedLabel { get; init; }

    public string? Kind { get; init; }

    public decimal? SelectedMinimum { get; init; }

    public decimal? SelectedMaximum { get; init; }

    public bool IsSearchable { get; init; }

    public bool SupportsEditableBounds { get; init; }

    public string? NotSearchableReason { get; init; }
}

internal sealed class TradeSearchDiagnosticRequestedItemFilterCapture
{
    public string? Kind { get; init; }

    public string? Label { get; init; }

    public bool IsActive { get; init; }

    public int? Minimum { get; init; }
}

internal sealed class TradeSearchDiagnosticProvenanceCapture
{
    public string ComponentId { get; init; } = string.Empty;

    public int DraftModifierIndex { get; init; }

    public string? DisplayedLabel { get; init; }

    public string? ProviderStatId { get; init; }

    public string? ProviderStatText { get; init; }

    public int ProducedFilterCount { get; init; }

    public IReadOnlyList<TradeSearchDiagnosticProducedFilterCapture> ProducedFilters { get; init; } = [];

    public string? MappingDiagnosticCode { get; init; }

    public string? MappingDiagnosticMessage { get; init; }

    public string? ZeroFilterReason { get; init; }
}

internal sealed class TradeSearchDiagnosticProducedFilterCapture
{
    public string StatId { get; init; } = string.Empty;

    public decimal? Minimum { get; init; }

    public decimal? Maximum { get; init; }

    public IReadOnlyList<string> AlternativeStatIds { get; init; } = [];

    public string GroupType { get; init; } = "and";

    public IReadOnlyList<TradeSearchDiagnosticSerializedStatFilterCapture> SerializedFilters { get; init; } =
        [];
}

internal sealed class TradeSearchDiagnosticSerializedStatFilterCapture
{
    public string? GroupType { get; init; }

    public string Id { get; init; } = string.Empty;

    public decimal? Minimum { get; init; }

    public decimal? Maximum { get; init; }
}

internal sealed class TradeSearchDiagnosticFinalFiltersCapture
{
    public string? Name { get; init; }

    public string? Type { get; init; }

    public string? ListingStatus { get; init; }

    public string? SortPrice { get; init; }

    public IReadOnlyList<TradeSearchDiagnosticStatsGroupCapture> StatsGroups { get; init; } = [];

    public JsonNode? QueryFilters { get; init; }
}

internal sealed class TradeSearchDiagnosticStatsGroupCapture
{
    public string Type { get; init; } = "and";

    public decimal? GroupMinimum { get; init; }

    public decimal? GroupMaximum { get; init; }

    public IReadOnlyList<TradeSearchDiagnosticSerializedStatFilterCapture> Filters { get; init; } = [];
}

internal sealed class TradeSearchDiagnosticResponseCapture
{
    public bool? HttpSuccess { get; init; }

    public int? HttpStatusCode { get; init; }

    public bool IsCancelled { get; init; }

    public bool IsTimeout { get; init; }

    public string? SearchId { get; init; }

    public int? Total { get; init; }

    public int? ResultIdCount { get; init; }

    public bool? Inexact { get; init; }

    public string? ProviderErrorCode { get; init; }

    public string? ProviderErrorMessage { get; init; }

    public string? Stage { get; init; }
}
