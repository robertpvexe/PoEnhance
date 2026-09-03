using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Diagnostics;

internal static class ModifierPipelineDiagnosticRecorder
{
    public const string EnvironmentVariableName = "POENHANCE_MODIFIER_PIPELINE_DIAGNOSTIC_DIR";

    private static readonly AsyncLocal<ModifierPipelineDiagnosticSession?> ActiveSession = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly MethodInfo InteractionReadyMethod =
        typeof(PriceCheckerSearchController).GetMethod(
            "IsModifierInteractionReady",
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(nameof(PriceCheckerSearchController), "IsModifierInteractionReady");

    private static readonly MethodInfo StaticModifierLabelMethod =
        typeof(PriceCheckerSearchController).GetMethod(
            "StaticModifierLabel",
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(nameof(PriceCheckerSearchController), "StaticModifierLabel");

    private static readonly MethodInfo ModifierAvailabilityStatusMethod =
        typeof(PriceCheckerSearchController).GetMethod(
            "ModifierAvailabilityStatus",
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(nameof(PriceCheckerSearchController), "ModifierAvailabilityStatus");

    public static bool IsEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    public static void TryBeginCapture(
        ParsedItem parsedItem,
        ItemBaseResolutionResult? baseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions,
        TradeSearchDraft initialDraft)
    {
        if (!IsEnabled)
        {
            return;
        }

        var outputDirectory = Environment.GetEnvironmentVariable(EnvironmentVariableName)!;
        ActiveSession.Value = ModifierPipelineDiagnosticSession.Create(
            outputDirectory,
            parsedItem,
            baseResolution,
            modifierResolutions,
            initialDraft);
    }

    public static void RecordProviderResolution(
        TradeSearchDraft draft,
        ResolvedSearchComponent inputComponent,
        ResolvedSearchComponent outputComponent,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeItemIdentity? uniqueIdentity,
        PathOfExileTradeStatMatchResult? match,
        string resolutionPhase,
        string? skipReason,
        bool hasProviderOwnedUniqueProof,
        bool hasStructuredAdvancedExplicitProof,
        bool canResolveProviderComponent)
    {
        if (ActiveSession.Value is null)
        {
            return;
        }

        ActiveSession.Value.RecordProviderResolution(
            draft,
            inputComponent,
            outputComponent,
            catalog,
            uniqueIdentity,
            match,
            resolutionPhase,
            skipReason,
            hasProviderOwnedUniqueProof,
            hasStructuredAdvancedExplicitProof,
            canResolveProviderComponent);
    }

    public static void TryCompleteCapture(
        TradeSearchDraft finalDraft,
        TradeSearchValidationResult? validationResult,
        PathOfExileTradeItemIdentity? uniqueIdentity = null)
    {
        var session = ActiveSession.Value;
        if (session is null)
        {
            return;
        }

        ActiveSession.Value = null;
        session.Complete(finalDraft, validationResult, uniqueIdentity, JsonOptions);
    }

    internal static bool IsInteractionReady(ResolvedSearchComponent component) =>
        (bool)(InteractionReadyMethod.Invoke(null, [component]) ?? false);

    internal static string StaticModifierLabel(ResolvedSearchComponent component) =>
        (string)(StaticModifierLabelMethod.Invoke(null, [component]) ?? string.Empty);

    internal static string ModifierAvailabilityStatus(ResolvedSearchComponent component) =>
        (string)(ModifierAvailabilityStatusMethod.Invoke(null, [component]) ?? string.Empty);
}

internal sealed class ModifierPipelineDiagnosticSession
{
    private readonly string outputDirectory;
    private readonly ModifierPipelineDiagnosticCapture capture;
    private readonly Dictionary<string, List<ModifierPipelineProviderPassCapture>> providerPasses = new(StringComparer.Ordinal);
    private PathOfExileTradeStatCatalog? lastCatalog;
    private int providerPassSequence;

    private ModifierPipelineDiagnosticSession(
        string outputDirectory,
        ModifierPipelineDiagnosticCapture capture)
    {
        this.outputDirectory = outputDirectory;
        this.capture = capture;
    }

    public static ModifierPipelineDiagnosticSession Create(
        string outputDirectory,
        ParsedItem parsedItem,
        ItemBaseResolutionResult? baseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions,
        TradeSearchDraft initialDraft)
    {
        return new ModifierPipelineDiagnosticSession(
            outputDirectory,
            ModifierPipelineDiagnosticCapture.FromInputs(
                parsedItem,
                baseResolution,
                modifierResolutions,
                initialDraft));
    }

    public void RecordProviderResolution(
        TradeSearchDraft draft,
        ResolvedSearchComponent inputComponent,
        ResolvedSearchComponent outputComponent,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeItemIdentity? uniqueIdentity,
        PathOfExileTradeStatMatchResult? match,
        string resolutionPhase,
        string? skipReason,
        bool hasProviderOwnedUniqueProof,
        bool hasStructuredAdvancedExplicitProof,
        bool canResolveProviderComponent)
    {
        lastCatalog = catalog;
        providerPassSequence++;
        capture.UniqueIdentity ??= CaptureUniqueIdentity(uniqueIdentity);
        capture.Catalog = new ModifierPipelineCatalogCapture
        {
            StatEntryCount = catalog.Entries.Count,
            CandidateGroupCount = catalog.CandidateGroups.Count,
        };

        if (!providerPasses.TryGetValue(inputComponent.ComponentId, out var passes))
        {
            passes = [];
            providerPasses[inputComponent.ComponentId] = passes;
        }

        passes.Add(new ModifierPipelineProviderPassCapture
        {
            PassSequence = providerPassSequence,
            ResolutionPhase = resolutionPhase,
            SkipReason = skipReason,
            InputProviderResolutionStatus = inputComponent.ProviderResolutionStatus.ToString(),
            OutputProviderResolutionStatus = outputComponent.ProviderResolutionStatus.ToString(),
            HasProviderOwnedUniqueProof = hasProviderOwnedUniqueProof,
            HasStructuredAdvancedExplicitProof = hasStructuredAdvancedExplicitProof,
            CanResolveProviderComponent = canResolveProviderComponent,
            HasExactUniqueSourceProvenance = inputComponent.HasExactUniqueSourceProvenance,
            HasResolvedUniqueSourceSemantics = inputComponent.HasResolvedUniqueSourceSemantics,
            Match = match is null ? null : ModifierPipelineMatchCapture.FromMatch(match, inputComponent),
            Projection = CreateProjectionCapture(outputComponent, catalog, match),
        });
    }

    public void Complete(
        TradeSearchDraft finalDraft,
        TradeSearchValidationResult? validationResult,
        PathOfExileTradeItemIdentity? uniqueIdentity,
        JsonSerializerOptions jsonOptions)
    {
        capture.CompletedAtUtc = DateTimeOffset.UtcNow;
        capture.UniqueIdentity ??= CaptureUniqueIdentity(uniqueIdentity);
        capture.ValidationDiagnosticCount = validationResult?.Diagnostics.Count ?? 0;
        capture.Modifiers = finalDraft.ModifierFilters
            .Select(component => BuildModifierCapture(finalDraft, component))
            .ToArray();

        Directory.CreateDirectory(outputDirectory);
        var itemLabel = SanitizeFileNameSegment(finalDraft.DisplayName ?? finalDraft.ParsedBaseType ?? "item");
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{itemLabel}.json";
        var path = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(capture, jsonOptions));
        capture.OutputPath = path;
    }

    private ModifierPipelineModifierCapture BuildModifierCapture(
        TradeSearchDraft draft,
        ResolvedSearchComponent component)
    {
        providerPasses.TryGetValue(component.ComponentId, out var passes);
        var source = capture.InitialModifiers.FirstOrDefault(entry =>
            entry.SourceModifierIndex == component.SourceModifierIndex &&
            entry.SourceLineIndex == component.SourceLineIndex);
        var serialization = TryCaptureSerialization(draft, component);

        return new ModifierPipelineModifierCapture
        {
            ComponentId = component.ComponentId,
            SourceModifierIndex = component.SourceModifierIndex,
            SourceLineIndex = component.SourceLineIndex,
            SourceComponentIndex = component.SourceComponentIndex,
            Raw = source?.Raw,
            SourceResolution = ModifierPipelineSourceResolutionCapture.FromResolution(null, component)
                ?? source?.SourceResolution,
            ResolvedSemantics = ModifierPipelineResolvedSemanticsCapture.FromComponent(component),
            Signatures = ModifierPipelineSignatureCapture.FromComponent(component),
            Multiline = ModifierPipelineMultilineCapture.FromComponent(component),
            ProviderPasses = passes?.ToArray() ?? [],
            ProviderResolution = ModifierPipelineProviderOutcomeCapture.FromComponent(component),
            Consumer = ModifierPipelineConsumerCapture.FromComponent(component, serialization),
        };
    }

    private ModifierPipelineSerializationCapture TryCaptureSerialization(
        TradeSearchDraft draft,
        ResolvedSearchComponent component)
    {
        if (!component.IsSelected)
        {
            return new ModifierPipelineSerializationCapture
            {
                Attempted = false,
                BlockedReason = "Component is not selected.",
            };
        }

        var mapper = new PathOfExileTradeSelectedModifierMapper();
        var selectedDraft = draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(candidate => candidate with
                {
                    IsSelected = string.Equals(
                        candidate.ComponentId,
                        component.ComponentId,
                        StringComparison.Ordinal),
                })
                .ToArray(),
        };
        var catalog = lastCatalog;
        var mapping = catalog is null
            ? mapper.Map(selectedDraft)
            : mapper.Map(selectedDraft, catalog);

        if (!mapping.IsSuccess)
        {
            var diagnostic = mapping.Diagnostics.FirstOrDefault();
            return new ModifierPipelineSerializationCapture
            {
                Attempted = true,
                Success = false,
                BlockedReason = diagnostic?.Message ?? "Selected modifier mapping failed.",
                DiagnosticCode = diagnostic?.Code,
            };
        }

        var filter = mapping.Filters.FirstOrDefault();
        return new ModifierPipelineSerializationCapture
        {
            Attempted = true,
            Success = true,
            Filter = filter is null
                ? null
                : new ModifierPipelineSerializedFilterCapture
                {
                    StatId = filter.StatId,
                    Minimum = filter.Minimum,
                    Maximum = filter.Maximum,
                },
        };
    }

    private static ModifierPipelineProjectionCapture? CreateProjectionCapture(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchResult? match)
    {
        var candidate = match?.ExactCandidate ??
            match?.ExactEquivalentCandidates.FirstOrDefault() ??
            (component.ProviderStatId is not null &&
                catalog.TryGetById(component.ProviderStatId, out var entry)
                ? PathOfExileTradeStatCandidateClassifier.ToCandidate(entry)
                : null);
        if (candidate is null)
        {
            return null;
        }

        var projection = PathOfExileTradeModifierBoundProjector.ProjectBounds(component, candidate);
        return new ModifierPipelineProjectionCapture
        {
            ProjectionKind = projection.ProjectionKind,
            ValueBoundShape = component.ValueBoundShape.ToString(),
            IsFaithful = projection.IsFaithful,
            RequestedMinimum = component.RequestedMinimum,
            RequestedMaximum = component.RequestedMaximum,
            ProjectedMinimum = projection.Minimum,
            ProjectedMaximum = projection.Maximum,
            FixedQueryValue = component.FixedQueryValue,
            SupportsValueBounds = component.SupportsValueBounds,
            ProviderFallbackNumericValues = component.ProviderFallbackNumericValues.ToArray(),
            Representation =
                projection.ProjectionKind == "FixedNumericQueryConstraint"
                    ? "fixed-parametric"
                    : projection.ProjectionKind == "ExactFixedLiteralPresence"
                    ? "fixed-literal"
                    : component.ValueBoundShape == ModifierBoundShape.PresenceOnly
                        ? "presence-only"
                        : component.ValueBoundShape == ModifierBoundShape.Scalar
                            ? "scalar"
                            : component.ValueBoundShape.ToString(),
        };
    }

    private static ModifierPipelineUniqueIdentityCapture? CaptureUniqueIdentity(
        PathOfExileTradeItemIdentity? uniqueIdentity)
    {
        if (uniqueIdentity is null)
        {
            return null;
        }

        return new ModifierPipelineUniqueIdentityCapture
        {
            CanonicalName = uniqueIdentity.CanonicalName,
            CanonicalType = uniqueIdentity.CanonicalType,
            Foulborn = uniqueIdentity.Foulborn.ToString(),
        };
    }

    private static string SanitizeFileNameSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "item" : sanitized[..Math.Min(sanitized.Length, 48)];
    }
}

internal sealed class ModifierPipelineDiagnosticCapture
{
    public string DiagnosticVersion { get; init; } = "E6b-generic-live-1";

    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? OutputPath { get; set; }

    public ModifierPipelineItemCapture Item { get; init; } = new();

    public ModifierPipelineUniqueIdentityCapture? UniqueIdentity { get; set; }

    public ModifierPipelineUniqueMechanicalResolutionCapture? UniqueMechanicalResolution { get; init; }

    public ModifierPipelineCatalogCapture? Catalog { get; set; }

    public int ValidationDiagnosticCount { get; set; }

    public IReadOnlyList<ModifierPipelineInitialModifierCapture> InitialModifiers { get; init; } = [];

    public IReadOnlyList<ModifierPipelineModifierCapture> Modifiers { get; set; } = [];

    public static ModifierPipelineDiagnosticCapture FromInputs(
        ParsedItem parsedItem,
        ItemBaseResolutionResult? baseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions,
        TradeSearchDraft initialDraft)
    {
        var parsedByIndex = parsedItem.Modifiers
            .Select((modifier, index) => (index, modifier))
            .ToDictionary(entry => entry.index, entry => entry.modifier);

        var initialModifiers = initialDraft.ModifierFilters
            .Select(component =>
            {
                parsedByIndex.TryGetValue(component.SourceModifierIndex, out var parsedModifier);
                var resolution = modifierResolutions.FirstOrDefault(candidate =>
                    candidate.ParsedModifierIndex == component.SourceModifierIndex);
                return new ModifierPipelineInitialModifierCapture
                {
                    ComponentId = component.ComponentId,
                    SourceModifierIndex = component.SourceModifierIndex,
                    SourceLineIndex = component.SourceLineIndex,
                    Raw = ModifierPipelineRawCapture.FromParsedModifier(parsedModifier, component),
                    SourceResolution = ModifierPipelineSourceResolutionCapture.FromResolution(resolution, component),
                };
            })
            .ToArray();

        return new ModifierPipelineDiagnosticCapture
        {
            Item = new ModifierPipelineItemCapture
            {
                ItemClass = parsedItem.ItemClass,
                Rarity = parsedItem.Rarity,
                DisplayName = parsedItem.DisplayName,
                ParsedBaseType = parsedItem.BaseType,
                BaseResolutionStatus = baseResolution?.Status.ToString(),
                ResolvedBaseName = baseResolution?.ResolvedBaseName,
            },
            UniqueMechanicalResolution = CaptureUniqueMechanicalResolution(initialDraft.UniqueItemResolution),
            InitialModifiers = initialModifiers,
        };
    }

    private static ModifierPipelineUniqueMechanicalResolutionCapture? CaptureUniqueMechanicalResolution(
        UniqueItemResolutionResult? uniqueItemResolution)
    {
        if (uniqueItemResolution is null)
        {
            return new ModifierPipelineUniqueMechanicalResolutionCapture
            {
                CatalogPassedToCreateDraft = false,
                Status = "null",
                Diagnostic =
                    "TradeSearchDraft.UniqueItemResolution is null — CreateDraft ran without GameDataCatalog (or Unique resolver was not invoked).",
            };
        }

        return new ModifierPipelineUniqueMechanicalResolutionCapture
        {
            CatalogPassedToCreateDraft = true,
            Status = uniqueItemResolution.Status.ToString(),
            DiagnosticCode = uniqueItemResolution.DiagnosticCode,
            Diagnostic = uniqueItemResolution.Diagnostic,
            IdentityCanonicalName = uniqueItemResolution.Identity?.CanonicalName,
            CompatibleVersionRoles = uniqueItemResolution.CompatibleVersions
                .Select(version => version.Role.ToString())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(role => role, StringComparer.Ordinal)
                .ToArray(),
            ModifierBlocks = uniqueItemResolution.ModifierBlocks
                .Select(block => new ModifierPipelineUniqueMechanicalBlockCapture
                {
                    ParsedModifierIndex = block.ParsedModifierIndex,
                    IsResolved = block.IsResolved,
                    IsEquivalentSourceSet = block.IsEquivalentSourceSet,
                    DiagnosticCode = block.DiagnosticCode,
                    AggregationDiagnosticCode = block.AggregationDiagnosticCode,
                    StatIds = block.StatIds.ToArray(),
                    ModifierIds = block.ModifierIds.ToArray(),
                    SourceObservationIds = block.SourceObservationIds.ToArray(),
                    CatalogBlockIds = block.CatalogBlocks.Select(catalogBlock => catalogBlock.Id!).ToArray(),
                    ConflictKind = block.ConflictEvidence?.Kind.ToString(),
                    NonBlockingHistoricalConflictKind =
                        block.NonBlockingHistoricalConflictEvidence?.Kind.ToString(),
                })
                .ToArray(),
        };
    }
}

internal sealed class ModifierPipelineItemCapture
{
    public string? ItemClass { get; init; }

    public string? Rarity { get; init; }

    public string? DisplayName { get; init; }

    public string? ParsedBaseType { get; init; }

    public string? BaseResolutionStatus { get; init; }

    public string? ResolvedBaseName { get; init; }
}

internal sealed class ModifierPipelineCatalogCapture
{
    public int StatEntryCount { get; init; }

    public int CandidateGroupCount { get; init; }
}

internal sealed class ModifierPipelineUniqueIdentityCapture
{
    public string? CanonicalName { get; init; }

    public string? CanonicalType { get; init; }

    public string? Foulborn { get; init; }
}

internal sealed class ModifierPipelineUniqueMechanicalResolutionCapture
{
    public bool CatalogPassedToCreateDraft { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }

    public string? IdentityCanonicalName { get; init; }

    public IReadOnlyList<string> CompatibleVersionRoles { get; init; } = [];

    public IReadOnlyList<ModifierPipelineUniqueMechanicalBlockCapture> ModifierBlocks { get; init; } = [];
}

internal sealed class ModifierPipelineUniqueMechanicalBlockCapture
{
    public int ParsedModifierIndex { get; init; }

    public bool IsResolved { get; init; }

    public bool IsEquivalentSourceSet { get; init; }

    public string? DiagnosticCode { get; init; }

    public string? AggregationDiagnosticCode { get; init; }

    public IReadOnlyList<string> StatIds { get; init; } = [];

    public IReadOnlyList<string> ModifierIds { get; init; } = [];

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];

    public IReadOnlyList<string> CatalogBlockIds { get; init; } = [];

    public string? ConflictKind { get; init; }

    public string? NonBlockingHistoricalConflictKind { get; init; }
}

internal sealed class ModifierPipelineInitialModifierCapture
{
    public required string ComponentId { get; init; }

    public int SourceModifierIndex { get; init; }

    public int SourceLineIndex { get; init; }

    public ModifierPipelineRawCapture? Raw { get; init; }

    public ModifierPipelineSourceResolutionCapture? SourceResolution { get; init; }
}

internal sealed class ModifierPipelineModifierCapture
{
    public required string ComponentId { get; init; }

    public int SourceModifierIndex { get; init; }

    public int SourceLineIndex { get; init; }

    public int SourceComponentIndex { get; init; }

    public ModifierPipelineRawCapture? Raw { get; init; }

    public ModifierPipelineSourceResolutionCapture? SourceResolution { get; init; }

    public ModifierPipelineResolvedSemanticsCapture? ResolvedSemantics { get; init; }

    public ModifierPipelineSignatureCapture? Signatures { get; init; }

    public ModifierPipelineMultilineCapture? Multiline { get; init; }

    public IReadOnlyList<ModifierPipelineProviderPassCapture> ProviderPasses { get; init; } = [];

    public ModifierPipelineProviderOutcomeCapture? ProviderResolution { get; init; }

    public ModifierPipelineConsumerCapture? Consumer { get; init; }
}

internal sealed class ModifierPipelineRawCapture
{
    public int? ParsedModifierIndex { get; init; }

    public IReadOnlyList<string> ValueLines { get; init; } = [];

    public string? RawMetadataLine { get; init; }

    public string? ParsedKind { get; init; }

    public string? UniqueOrigin { get; init; }

    public string? ImplicitOrigin { get; init; }

    public string? CategoryText { get; init; }

    public string? OriginalText { get; init; }

    public static ModifierPipelineRawCapture? FromParsedModifier(
        ParsedModifier? parsedModifier,
        ResolvedSearchComponent component)
    {
        if (parsedModifier is null && string.IsNullOrWhiteSpace(component.OriginalText))
        {
            return null;
        }

        return new ModifierPipelineRawCapture
        {
            ParsedModifierIndex = component.SourceModifierIndex,
            ValueLines = parsedModifier?.ValueLines.ToArray() ?? [component.OriginalText],
            RawMetadataLine = parsedModifier?.RawMetadataLine ?? component.CategoryText,
            ParsedKind = (parsedModifier?.Kind ?? component.ParsedKind).ToString(),
            UniqueOrigin = (parsedModifier?.UniqueOrigin ?? component.UniqueOrigin).ToString(),
            ImplicitOrigin = parsedModifier?.ImplicitOrigin.ToString(),
            CategoryText = parsedModifier?.CategoryText ?? component.CategoryText,
            OriginalText = component.OriginalText,
        };
    }
}

internal sealed class ModifierPipelineSourceResolutionCapture
{
    public string? Status { get; init; }

    public string? ResolvedModifierId { get; init; }

    public IReadOnlyList<string> ResolvedStatIds { get; init; } = [];

    public IReadOnlyList<string> UniqueCatalogBlockIds { get; init; } = [];

    public IReadOnlyList<ModifierPipelineOptionChoiceMembershipCapture> UniqueOptionChoiceMemberships { get; init; } = [];

    public IReadOnlyList<string> UniqueSourceObservationIds { get; init; } = [];

    public string? UniqueResolutionDiagnosticCode { get; init; }

    public string? UniqueAggregationDiagnosticCode { get; init; }

    public string? UniqueAggregationDiagnostic { get; init; }

    public bool IsEquivalentSourceSet { get; init; }

    public int SourceCandidateCount { get; init; }

    public string? UniqueConflictKind { get; init; }

    public int UniqueConflictCandidateCount { get; init; }

    public IReadOnlyList<string> UniqueConflictCandidateModifierIds { get; init; } = [];

    public IReadOnlyList<string> UniqueConflictCandidateStatVectors { get; init; } = [];

    public IReadOnlyList<string> UniqueConflictCandidateHandlers { get; init; } = [];

    public IReadOnlyList<string> UniqueConflictCandidateEncodingMarkers { get; init; } = [];

    public IReadOnlyList<string> UniqueConflictCandidateSourceAvailability { get; init; } = [];

    public string? UniqueNonBlockingHistoricalConflictKind { get; init; }

    public int UniqueNonBlockingHistoricalConflictCandidateCount { get; init; }

    public static ModifierPipelineSourceResolutionCapture? FromResolution(
        ModifierCandidateResolutionResult? resolution,
        ResolvedSearchComponent component)
    {
        if (resolution is null &&
            component.ResolutionStatus is null &&
            component.ResolvedStatIds.Count == 0 &&
            component.UniqueConflictEvidence is null &&
            component.UniqueNonBlockingHistoricalConflictEvidence is null &&
            string.IsNullOrWhiteSpace(component.UniqueResolutionDiagnosticCode) &&
            string.IsNullOrWhiteSpace(component.UniqueAggregationDiagnosticCode) &&
            component.UniqueCatalogBlockIds.Count == 0)
        {
            return null;
        }

        var conflict = component.UniqueConflictEvidence ??
            component.UniqueNonBlockingHistoricalConflictEvidence;
        return new ModifierPipelineSourceResolutionCapture
        {
            Status = (resolution?.Status ?? component.ResolutionStatus)?.ToString(),
            ResolvedModifierId = resolution?.Candidates.FirstOrDefault()?.Id ?? component.ResolvedModifierId,
            ResolvedStatIds = resolution?.Candidates.FirstOrDefault()?.Stats
                .Select(stat => stat.StatId)
                .Where(statId => !string.IsNullOrWhiteSpace(statId))
                .Select(statId => statId!)
                .ToArray() ?? component.ResolvedStatIds.ToArray(),
            UniqueCatalogBlockIds = component.UniqueCatalogBlockIds.ToArray(),
            UniqueOptionChoiceMemberships = component.UniqueOptionChoiceMemberships
                .Select(membership => new ModifierPipelineOptionChoiceMembershipCapture
                {
                    OptionAxisId = membership.OptionAxisId,
                    OptionChoiceId = membership.OptionChoiceId,
                    SourceObservationIds = membership.SourceObservationIds.ToArray(),
                })
                .ToArray(),
            UniqueSourceObservationIds = component.UniqueSourceObservationIds.ToArray(),
            UniqueResolutionDiagnosticCode = component.UniqueResolutionDiagnosticCode,
            UniqueAggregationDiagnosticCode = component.UniqueAggregationDiagnosticCode,
            UniqueAggregationDiagnostic = component.UniqueAggregationDiagnostic,
            IsEquivalentSourceSet = resolution?.IsEquivalentSourceSet == true || component.IsEquivalentSourceSet,
            SourceCandidateCount = resolution?.CandidateCount ?? 0,
            UniqueConflictKind = conflict?.Kind.ToString(),
            UniqueConflictCandidateCount = conflict?.Candidates.Count ?? 0,
            UniqueConflictCandidateModifierIds = conflict?.Candidates
                .Select(candidate => candidate.ModifierId)
                .ToArray() ?? [],
            UniqueConflictCandidateStatVectors = conflict?.Candidates
                .Select(candidate => string.Join(',', candidate.StatIds))
                .ToArray() ?? [],
            UniqueConflictCandidateHandlers = conflict?.Candidates
                .SelectMany(candidate => candidate.Handlers)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(handler => handler, StringComparer.Ordinal)
                .ToArray() ?? [],
            UniqueConflictCandidateEncodingMarkers = conflict?.Candidates
                .SelectMany(candidate => candidate.EncodingMarkers)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(marker => marker, StringComparer.Ordinal)
                .ToArray() ?? [],
            UniqueConflictCandidateSourceAvailability = conflict?.Candidates
                .Select(candidate => candidate.SourceAvailability.ToString())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray() ?? [],
            UniqueNonBlockingHistoricalConflictKind =
                component.UniqueNonBlockingHistoricalConflictEvidence?.Kind.ToString(),
            UniqueNonBlockingHistoricalConflictCandidateCount =
                component.UniqueNonBlockingHistoricalConflictEvidence?.Candidates.Count ?? 0,
        };
    }
}

internal sealed class ModifierPipelineOptionChoiceMembershipCapture
{
    public string? OptionAxisId { get; init; }

    public string? OptionChoiceId { get; init; }

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}

internal sealed class ModifierPipelineResolvedSemanticsCapture
{
    public string ParsedKind { get; init; } = string.Empty;

    public string UniqueOrigin { get; init; } = string.Empty;

    public string? RecoveredSourceKind { get; init; }

    public string? RecoveredSourceUniqueOrigin { get; init; }

    public bool UsesIdentityBoundUniqueRecovery { get; init; }

    public string ResolvedSourceKind { get; init; } = string.Empty;

    public string ResolvedSourceUniqueOrigin { get; init; } = string.Empty;

    public bool HasResolvedUniqueSourceSemantics { get; init; }

    public bool HasExactUniqueSourceProvenance { get; init; }

    public static ModifierPipelineResolvedSemanticsCapture FromComponent(ResolvedSearchComponent component)
    {
        return new ModifierPipelineResolvedSemanticsCapture
        {
            ParsedKind = component.ParsedKind.ToString(),
            UniqueOrigin = component.UniqueOrigin.ToString(),
            RecoveredSourceKind = component.RecoveredSourceKind?.ToString(),
            RecoveredSourceUniqueOrigin = component.RecoveredSourceUniqueOrigin?.ToString(),
            UsesIdentityBoundUniqueRecovery = component.UsesIdentityBoundUniqueRecovery,
            ResolvedSourceKind = component.ResolvedSourceKind.ToString(),
            ResolvedSourceUniqueOrigin = component.ResolvedSourceUniqueOrigin.ToString(),
            HasResolvedUniqueSourceSemantics = component.HasResolvedUniqueSourceSemantics,
            HasExactUniqueSourceProvenance = component.HasExactUniqueSourceProvenance,
        };
    }
}

internal sealed class ModifierPipelineSignatureCapture
{
    public string OriginalText { get; init; } = string.Empty;

    public string CanonicalSignature { get; init; } = string.Empty;

    public string? ProviderCanonicalSignature { get; init; }

    public IReadOnlyList<string> ProviderSearchSignatures { get; init; } = [];

    public static ModifierPipelineSignatureCapture FromComponent(ResolvedSearchComponent component)
    {
        return new ModifierPipelineSignatureCapture
        {
            OriginalText = component.OriginalText,
            CanonicalSignature = component.CanonicalSignature,
            ProviderCanonicalSignature = component.ProviderCanonicalSignature,
            ProviderSearchSignatures = component.ProviderSearchSignatures.ToArray(),
        };
    }
}

internal sealed class ModifierPipelineMultilineCapture
{
    public bool OriginalTextContainsNewLine { get; init; }

    public bool IsEquivalentSourceSet { get; init; }

    public int SourceCount { get; init; }

    public int UniqueSourceObservationCount { get; init; }

    public static ModifierPipelineMultilineCapture FromComponent(ResolvedSearchComponent component)
    {
        return new ModifierPipelineMultilineCapture
        {
            OriginalTextContainsNewLine = component.OriginalText.Contains('\n') ||
                component.OriginalText.Contains('\r'),
            IsEquivalentSourceSet = component.IsEquivalentSourceSet,
            SourceCount = component.Sources.Count,
            UniqueSourceObservationCount = component.UniqueSourceObservationIds.Count,
        };
    }
}

internal sealed class ModifierPipelineProviderPassCapture
{
    public int PassSequence { get; init; }

    public string ResolutionPhase { get; init; } = string.Empty;

    public string? SkipReason { get; init; }

    public string? InputProviderResolutionStatus { get; init; }

    public string? OutputProviderResolutionStatus { get; init; }

    public bool HasProviderOwnedUniqueProof { get; init; }

    public bool HasStructuredAdvancedExplicitProof { get; init; }

    public bool CanResolveProviderComponent { get; init; }

    public bool HasExactUniqueSourceProvenance { get; init; }

    public bool HasResolvedUniqueSourceSemantics { get; init; }

    public ModifierPipelineMatchCapture? Match { get; init; }

    public ModifierPipelineProjectionCapture? Projection { get; init; }
}

internal sealed class ModifierPipelineMatchCapture
{
    public string Status { get; init; } = string.Empty;

    public string NormalizedItemTemplate { get; init; } = string.Empty;

    public IReadOnlyList<string> InitialCandidateStatIds { get; init; } = [];

    public IReadOnlyList<string> CandidateStatIds { get; init; } = [];

    public IReadOnlyList<ModifierPipelineCandidateCapture> Candidates { get; init; } = [];

    public IReadOnlyList<ModifierPipelineRejectionCapture> Rejections { get; init; } = [];

    public IReadOnlyList<ModifierPipelineDiagnosticMessageCapture> Diagnostics { get; init; } = [];

    public string? SelectedProviderStatId { get; init; }

    public bool ExactSourceEvidenceExpandedDiscovery { get; init; }

    public static ModifierPipelineMatchCapture FromMatch(
        PathOfExileTradeStatMatchResult match,
        ResolvedSearchComponent component)
    {
        return new ModifierPipelineMatchCapture
        {
            Status = match.Status.ToString(),
            NormalizedItemTemplate = match.NormalizedItemTemplate,
            InitialCandidateStatIds = match.InitialCandidates.Select(candidate => candidate.StatId).ToArray(),
            CandidateStatIds = (match.Candidates.Count > 0
                    ? match.Candidates
                    : match.InitialCandidates)
                .Select(candidate => candidate.StatId)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Candidates = (match.Candidates.Count > 0 ? match.Candidates : match.InitialCandidates)
                .Select(ModifierPipelineCandidateCapture.FromCandidate)
                .ToArray(),
            Rejections = (match.Trace?.Rejections ?? [])
                .Select(rejection => new ModifierPipelineRejectionCapture
                {
                    StatId = rejection.Candidate.StatId,
                    Reason = rejection.Reason,
                })
                .ToArray(),
            Diagnostics = match.Diagnostics
                .Select(diagnostic => new ModifierPipelineDiagnosticMessageCapture
                {
                    Code = diagnostic.Code,
                    Message = diagnostic.Message,
                })
                .ToArray(),
            SelectedProviderStatId = match.ExactCandidate?.StatId ??
                match.ExactEquivalentCandidates.FirstOrDefault()?.StatId,
            ExactSourceEvidenceExpandedDiscovery =
                component.HasExactUniqueSourceProvenance &&
                component.ProviderSearchSignatures.Count > 0,
        };
    }
}

internal sealed class ModifierPipelineCandidateCapture
{
    public string StatId { get; init; } = string.Empty;

    public string ProviderKind { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string LookupTemplate { get; init; } = string.Empty;

    public string ProviderLocality { get; init; } = string.Empty;

    public static ModifierPipelineCandidateCapture FromCandidate(PathOfExileTradeStatMatchCandidate candidate)
    {
        return new ModifierPipelineCandidateCapture
        {
            StatId = candidate.StatId,
            ProviderKind = candidate.ProviderKind,
            Text = candidate.Text,
            LookupTemplate = candidate.LookupTemplate,
            ProviderLocality = candidate.ProviderLocality.ToString(),
        };
    }
}

internal sealed class ModifierPipelineRejectionCapture
{
    public string StatId { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

internal sealed class ModifierPipelineDiagnosticMessageCapture
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

internal sealed class ModifierPipelineProjectionCapture
{
    public string? ProjectionKind { get; init; }

    public string? ValueBoundShape { get; init; }

    public bool IsFaithful { get; init; }

    public decimal? RequestedMinimum { get; init; }

    public decimal? RequestedMaximum { get; init; }

    public decimal? ProjectedMinimum { get; init; }

    public decimal? ProjectedMaximum { get; init; }

    public decimal? FixedQueryValue { get; init; }

    public bool SupportsValueBounds { get; init; }

    public IReadOnlyList<decimal> ProviderFallbackNumericValues { get; init; } = [];

    public string? Representation { get; init; }
}

internal sealed class ModifierPipelineProviderOutcomeCapture
{
    public string ProviderResolutionStatus { get; init; } = string.Empty;

    public string? ProviderStatId { get; init; }

    public string? ProviderStatText { get; init; }

    public IReadOnlyList<string> ProviderCandidateStatIds { get; init; } = [];

    public IReadOnlyList<string> ProviderStatAlternativeIds { get; init; } = [];

    public string? ProviderDiagnosticCode { get; init; }

    public string? ProviderDiagnosticMessage { get; init; }

    public IReadOnlyList<string> FilterVariantIdentities { get; init; } = [];

    public static ModifierPipelineProviderOutcomeCapture FromComponent(ResolvedSearchComponent component)
    {
        return new ModifierPipelineProviderOutcomeCapture
        {
            ProviderResolutionStatus = component.ProviderResolutionStatus.ToString(),
            ProviderStatId = component.ProviderStatId,
            ProviderStatText = component.ProviderStatText,
            ProviderCandidateStatIds = component.ProviderCandidateStatIds.ToArray(),
            ProviderStatAlternativeIds = component.ProviderStatAlternativeIds.ToArray(),
            ProviderDiagnosticCode = component.ProviderDiagnosticCode,
            ProviderDiagnosticMessage = component.ProviderDiagnosticMessage,
            FilterVariantIdentities = component.FilterVariants
                .Select(variant => $"{variant.ProviderKind}:{variant.Identity}")
                .ToArray(),
        };
    }
}

internal sealed class ModifierPipelineConsumerCapture
{
    public bool IsSearchable { get; init; }

    public string? NotSearchableReason { get; init; }

    public string UiModTypeLabel { get; init; } = string.Empty;

    public string AvailabilityStatus { get; init; } = string.Empty;

    public bool IsInteractionReady { get; init; }

    public bool IsSelected { get; init; }

    public ModifierPipelineSerializationCapture? Serialization { get; init; }

    public static ModifierPipelineConsumerCapture FromComponent(
        ResolvedSearchComponent component,
        ModifierPipelineSerializationCapture serialization)
    {
        return new ModifierPipelineConsumerCapture
        {
            IsSearchable = component.IsSearchable,
            NotSearchableReason = component.NotSearchableReason,
            UiModTypeLabel = ModifierPipelineDiagnosticRecorder.StaticModifierLabel(component),
            AvailabilityStatus = component.IsSearchable
                ? "Supported"
                : ModifierPipelineDiagnosticRecorder.ModifierAvailabilityStatus(component),
            IsInteractionReady = ModifierPipelineDiagnosticRecorder.IsInteractionReady(component),
            IsSelected = component.IsSelected,
            Serialization = serialization,
        };
    }
}

internal sealed class ModifierPipelineSerializationCapture
{
    public bool Attempted { get; init; }

    public bool Success { get; init; }

    public string? BlockedReason { get; init; }

    public string? DiagnosticCode { get; init; }

    public ModifierPipelineSerializedFilterCapture? Filter { get; init; }
}

internal sealed class ModifierPipelineSerializedFilterCapture
{
    public string? StatId { get; init; }

    public decimal? Minimum { get; init; }

    public decimal? Maximum { get; init; }
}
