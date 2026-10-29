using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeInfluenceFilterResolver
{
    private readonly IReadOnlyDictionary<TradeSearchInfluenceKind, Mapping> mappings;

    public PathOfExileTradeInfluenceFilterResolver()
    {
        mappings = LoadReviewedMappings();
    }

    public TradeSearchDraft ResolveDraftStatuses(
        TradeSearchDraft draft,
        PathOfExileTradeStatCatalog? statCatalog,
        PathOfExileTradeFilterCatalog? filterCatalog)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.InfluenceCriteria.IsDefaultOrEmpty)
        {
            return draft;
        }

        var resolved = draft.InfluenceCriteria
            .Select(criterion => ResolveCriterionStatus(criterion, statCatalog, filterCatalog))
            .ToImmutableArray();
        return draft with { InfluenceCriteria = resolved };
    }

    public PathOfExileTradeInfluenceFilterMappingResult MapSelected(
        TradeSearchDraft draft,
        PathOfExileTradeStatCatalog? statCatalog,
        PathOfExileTradeFilterCatalog? filterCatalog)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var filters = new List<PathOfExileTradeSelectedInfluenceFilter>();
        var diagnostics = new List<string>();
        var resolvedCriteria = new List<TradeSearchInfluenceCriterion>();

        foreach (var criterion in draft.InfluenceCriteria)
        {
            var resolved = ResolveCriterionStatus(criterion, statCatalog, filterCatalog);
            resolvedCriteria.Add(resolved);
            if (!resolved.IsEnabled)
            {
                continue;
            }

            if (!TryMapEnabled(resolved, statCatalog, filterCatalog, out var filter, out var diagnostic))
            {
                diagnostics.Add(diagnostic);
                continue;
            }

            filters.Add(filter);
        }

        var updatedDraft = draft with { InfluenceCriteria = resolvedCriteria.ToImmutableArray() };
        return diagnostics.Count == 0
            ? PathOfExileTradeInfluenceFilterMappingResult.Success(filters, updatedDraft)
            : PathOfExileTradeInfluenceFilterMappingResult.Failure(diagnostics, updatedDraft);
    }

    private TradeSearchInfluenceCriterion ResolveCriterionStatus(
        TradeSearchInfluenceCriterion criterion,
        PathOfExileTradeStatCatalog? statCatalog,
        PathOfExileTradeFilterCatalog? filterCatalog)
    {
        if (criterion.Kind == TradeSearchInfluenceKind.Unknown)
        {
            return criterion with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                DiagnosticReason = criterion.DiagnosticReason ??
                    "This influence line was observed but has no recognized Trade influence identity.",
            };
        }

        if (!mappings.TryGetValue(criterion.Kind, out var mapping) ||
            mapping.Family != criterion.Family)
        {
            return criterion with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                DiagnosticReason =
                    $"No reviewed provider mapping exists for influence '{criterion.DisplayLabel}'.",
            };
        }

        if (mapping.Encoding == InfluenceEncoding.PseudoStat)
        {
            if (statCatalog is null)
            {
                return criterion with
                {
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
                    DiagnosticReason = "Official Trade stats catalog is required to verify this influence.",
                };
            }

            if (!TryResolvePseudoStat(mapping, statCatalog, out _, out var diagnostic))
            {
                return criterion with
                {
                    ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                    DiagnosticReason = diagnostic,
                };
            }

            return criterion with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
                DiagnosticReason = null,
            };
        }

        if (filterCatalog is null)
        {
            return criterion with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
                DiagnosticReason = "Official Trade filter catalog is required to verify this influence.",
            };
        }

        if (!TryResolveMiscOption(mapping, filterCatalog, out _, out var miscDiagnostic))
        {
            return criterion with
            {
                ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                DiagnosticReason = miscDiagnostic,
            };
        }

        return criterion with
        {
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
            DiagnosticReason = null,
        };
    }

    private bool TryMapEnabled(
        TradeSearchInfluenceCriterion criterion,
        PathOfExileTradeStatCatalog? statCatalog,
        PathOfExileTradeFilterCatalog? filterCatalog,
        out PathOfExileTradeSelectedInfluenceFilter filter,
        out string diagnostic)
    {
        filter = null!;
        if (criterion.ProviderResolutionStatus != TradeSearchItemPropertyProviderResolutionStatus.Exact)
        {
            diagnostic = criterion.DiagnosticReason ??
                $"Enabled influence '{criterion.DisplayLabel}' has no exact official Trade representation.";
            return false;
        }

        if (!mappings.TryGetValue(criterion.Kind, out var mapping))
        {
            diagnostic = $"No reviewed provider mapping exists for influence '{criterion.DisplayLabel}'.";
            return false;
        }

        if (mapping.Encoding == InfluenceEncoding.PseudoStat)
        {
            if (!TryResolvePseudoStat(mapping, statCatalog!, out var statId, out diagnostic))
            {
                return false;
            }

            filter = new PathOfExileTradeSelectedInfluenceFilter
            {
                SourceKind = criterion.Kind,
                Family = criterion.Family,
                StatId = statId,
            };
            return true;
        }

        if (!TryResolveMiscOption(mapping, filterCatalog!, out var option, out diagnostic))
        {
            return false;
        }

        filter = new PathOfExileTradeSelectedInfluenceFilter
        {
            SourceKind = criterion.Kind,
            Family = criterion.Family,
            ProviderGroupId = mapping.ProviderGroupId,
            ProviderFilterId = mapping.ProviderFilterId,
            Option = option,
        };
        return true;
    }

    private static bool TryResolvePseudoStat(
        Mapping mapping,
        PathOfExileTradeStatCatalog catalog,
        out string statId,
        out string diagnostic)
    {
        statId = string.Empty;
        if (!catalog.TryGetById(mapping.ExpectedStatId, out var entry) ||
            !string.Equals(entry.Text, mapping.ExpectedOfficialText, StringComparison.Ordinal) ||
            !string.Equals(entry.Type, mapping.ExpectedProviderKind, StringComparison.OrdinalIgnoreCase))
        {
            diagnostic =
                $"The official Trade stats catalog is incompatible with the reviewed '{mapping.Kind}' influence mapping.";
            return false;
        }

        // Prefer exact text identity over trusting the reviewed ID alone when duplicates exist.
        var exactTextMatches = catalog.Entries
            .Where(candidate =>
                string.Equals(candidate.Text, mapping.ExpectedOfficialText, StringComparison.Ordinal) &&
                string.Equals(candidate.Type, mapping.ExpectedProviderKind, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (exactTextMatches.Length != 1 ||
            !string.Equals(exactTextMatches[0].Id, mapping.ExpectedStatId, StringComparison.Ordinal))
        {
            diagnostic =
                $"The official Trade stats catalog does not expose a unique verified entry for '{mapping.ExpectedOfficialText}'.";
            return false;
        }

        statId = exactTextMatches[0].Id;
        diagnostic = string.Empty;
        return true;
    }

    private static bool TryResolveMiscOption(
        Mapping mapping,
        PathOfExileTradeFilterCatalog catalog,
        out string option,
        out string diagnostic)
    {
        option = string.Empty;
        var definitions = catalog.FindOptionFilterDefinitions(
            mapping.ProviderGroupId!,
            mapping.ProviderFilterId!);
        if (definitions.Count != 1 ||
            !string.Equals(definitions[0].Text, mapping.ExpectedOfficialText, StringComparison.Ordinal) ||
            !HasExactOption(definitions[0], mapping.YesOptionId!, mapping.YesOptionText!) ||
            !HasExactOption(definitions[0], mapping.NoOptionId!, mapping.NoOptionText!))
        {
            diagnostic =
                $"The official Trade filter catalog is incompatible with the reviewed '{mapping.Kind}' influence mapping.";
            return false;
        }

        option = mapping.YesOptionId!;
        diagnostic = string.Empty;
        return true;
    }

    private static bool HasExactOption(
        PathOfExileTradeOptionFilterDefinition definition,
        string optionId,
        string optionText)
    {
        var options = definition.Options
            .Where(candidate => string.Equals(candidate.Id, optionId, StringComparison.Ordinal))
            .ToArray();
        return options.Length == 1 && string.Equals(options[0].Text, optionText, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<TradeSearchInfluenceKind, Mapping> LoadReviewedMappings()
    {
        const string resourceName =
            "PoEnhance.App.Infrastructure.Trade.PathOfExile.Data.influence-filter-mappings.json";
        var assembly = typeof(PathOfExileTradeInfluenceFilterResolver).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException("The reviewed influence filter mapping resource is missing.");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new JsonStringEnumConverter<TradeSearchInfluenceKind>());
        options.Converters.Add(new JsonStringEnumConverter<TradeSearchInfluenceFamily>());
        options.Converters.Add(new JsonStringEnumConverter<InfluenceEncoding>());
        var resource = JsonSerializer.Deserialize<Resource>(stream, options) ??
            throw new InvalidOperationException("The reviewed influence filter mapping resource is empty.");

        var expectedKinds = Enum.GetValues<TradeSearchInfluenceKind>()
            .Where(kind => kind != TradeSearchInfluenceKind.Unknown)
            .ToArray();
        if (resource.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(resource.ReviewReference) ||
            resource.Mappings is null ||
            resource.Mappings.Count != expectedKinds.Length ||
            resource.Mappings.Select(mapping => mapping.Kind).Distinct().Count() != expectedKinds.Length ||
            expectedKinds.Any(kind => resource.Mappings.All(mapping => mapping.Kind != kind)) ||
            resource.Mappings.Any(IsIncomplete))
        {
            throw new InvalidOperationException(
                "The reviewed influence filter mapping resource has an unsupported or incomplete shape.");
        }

        return resource.Mappings.ToDictionary(mapping => mapping.Kind);
    }

    private static bool IsIncomplete(Mapping mapping)
    {
        if (mapping.Encoding == InfluenceEncoding.PseudoStat)
        {
            return string.IsNullOrWhiteSpace(mapping.ExpectedOfficialText) ||
                string.IsNullOrWhiteSpace(mapping.ExpectedStatId) ||
                string.IsNullOrWhiteSpace(mapping.ExpectedProviderKind) ||
                mapping.Family != TradeSearchInfluenceFamily.Traditional;
        }

        return string.IsNullOrWhiteSpace(mapping.ProviderGroupId) ||
            string.IsNullOrWhiteSpace(mapping.ProviderFilterId) ||
            string.IsNullOrWhiteSpace(mapping.ExpectedOfficialText) ||
            string.IsNullOrWhiteSpace(mapping.YesOptionId) ||
            string.IsNullOrWhiteSpace(mapping.YesOptionText) ||
            string.IsNullOrWhiteSpace(mapping.NoOptionId) ||
            string.IsNullOrWhiteSpace(mapping.NoOptionText) ||
            mapping.Family != TradeSearchInfluenceFamily.Eldritch;
    }

    private enum InfluenceEncoding
    {
        PseudoStat,
        MiscOptionFilter,
    }

    private sealed record Resource
    {
        public int SchemaVersion { get; init; }

        public string? ReviewReference { get; init; }

        public IReadOnlyList<Mapping>? Mappings { get; init; }
    }

    private sealed record Mapping
    {
        public TradeSearchInfluenceKind Kind { get; init; }

        public TradeSearchInfluenceFamily Family { get; init; }

        public InfluenceEncoding Encoding { get; init; }

        public string? ExpectedOfficialText { get; init; }

        public string? ExpectedStatId { get; init; }

        public string? ExpectedProviderKind { get; init; }

        public string? ProviderGroupId { get; init; }

        public string? ProviderFilterId { get; init; }

        public string? YesOptionId { get; init; }

        public string? YesOptionText { get; init; }

        public string? NoOptionId { get; init; }

        public string? NoOptionText { get; init; }
    }
}

internal sealed record PathOfExileTradeInfluenceFilterMappingResult
{
    public bool IsSuccess { get; init; }

    public IReadOnlyList<PathOfExileTradeSelectedInfluenceFilter> Filters { get; init; } = [];

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public TradeSearchDraft? Draft { get; init; }

    public static PathOfExileTradeInfluenceFilterMappingResult Success(
        IReadOnlyList<PathOfExileTradeSelectedInfluenceFilter> filters,
        TradeSearchDraft draft) =>
        new() { IsSuccess = true, Filters = filters, Draft = draft };

    public static PathOfExileTradeInfluenceFilterMappingResult Failure(
        IReadOnlyList<string> diagnostics,
        TradeSearchDraft draft) =>
        new() { Diagnostics = diagnostics, Draft = draft };
}
