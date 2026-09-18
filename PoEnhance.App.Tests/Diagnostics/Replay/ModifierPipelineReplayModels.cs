using System.Text.Json.Serialization;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal sealed class ModifierPipelineReplayCaptureDocument
{
    public string? DiagnosticVersion { get; init; }

    public DateTimeOffset? CapturedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public ModifierPipelineReplayCaptureReplayContext? ReplayContext { get; init; }

    public ModifierPipelineReplayCaptureItem? Item { get; init; }

    public ModifierPipelineReplayCaptureUniqueIdentity? UniqueIdentity { get; init; }

    public ModifierPipelineReplayCaptureUniqueMechanicalResolution? UniqueMechanicalResolution { get; init; }

    public IReadOnlyList<ModifierPipelineReplayCaptureModifier>? Modifiers { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureReplayContext
{
    public string? CaptureSchemaVersion { get; init; }

    public string? RawClipboardText { get; init; }

    public string? InputKind { get; init; }

    public string? GameDataVersion { get; init; }

    public string? GameDataSha256 { get; init; }

    public string? GameDataPathSource { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureItem
{
    public string? ItemClass { get; init; }

    public string? Rarity { get; init; }

    public string? DisplayName { get; init; }

    public string? ParsedBaseType { get; init; }

    public string? BaseResolutionStatus { get; init; }

    public string? ResolvedBaseName { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureUniqueIdentity
{
    public string? CanonicalName { get; init; }

    public string? CanonicalType { get; init; }

    public string? Foulborn { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureUniqueMechanicalResolution
{
    public bool CatalogPassedToCreateDraft { get; init; }

    public string? Status { get; init; }

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }

    public string? IdentityCanonicalName { get; init; }

    public IReadOnlyList<string>? CompatibleVersionRoles { get; init; }

    public IReadOnlyList<ModifierPipelineReplayCaptureUniqueMechanicalBlock>? ModifierBlocks { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureUniqueMechanicalBlock
{
    public int ParsedModifierIndex { get; init; }

    public bool IsResolved { get; init; }

    public bool IsEquivalentSourceSet { get; init; }

    public string? DiagnosticCode { get; init; }

    public IReadOnlyList<string>? OmittedCompositionComponentIds { get; init; }

    public IReadOnlyList<string>? StatIds { get; init; }

    public IReadOnlyList<string>? ModifierIds { get; init; }

    public IReadOnlyList<string>? SourceObservationIds { get; init; }

    public IReadOnlyList<string>? CatalogBlockIds { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureModifier
{
    public string? ComponentId { get; init; }

    public int SourceModifierIndex { get; init; }

    public int SourceLineIndex { get; init; }

    public ModifierPipelineReplayCaptureRaw? Raw { get; init; }

    public ModifierPipelineReplayCaptureSourceResolution? SourceResolution { get; init; }

    public ModifierPipelineReplayCaptureSemantics? ResolvedSemantics { get; init; }

    public ModifierPipelineReplayCaptureProviderOutcome? ProviderResolution { get; init; }

    public ModifierPipelineReplayCaptureConsumer? Consumer { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureRaw
{
    public string? ParsedKind { get; init; }

    public string? UniqueOrigin { get; init; }

    public string? ImplicitOrigin { get; init; }

    public string? OriginalText { get; init; }

    public IReadOnlyList<string>? ValueLines { get; init; }

    public IReadOnlyList<decimal>? ObservedNumericValues { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureSourceResolution
{
    public string? Status { get; init; }

    public string? DiagnosticCode { get; init; }

    public string? AggregateDiagnosticCode { get; init; }

    public string? ResolvedModifierId { get; init; }

    public IReadOnlyList<string>? ResolvedModifierIds { get; init; }

    public IReadOnlyList<string>? ResolvedStatIds { get; init; }

    public bool IsEquivalentSourceSet { get; init; }

    public IReadOnlyList<string>? UniqueCatalogBlockIds { get; init; }

    public string? UniqueResolutionDiagnosticCode { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureSemantics
{
    public string? ParsedKind { get; init; }

    public string? ResolvedSourceKind { get; init; }

    public bool HasExactUniqueSourceProvenance { get; init; }

    public bool IsBaseImplicit { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureProviderOutcome
{
    public string? ProviderResolutionStatus { get; init; }

    public string? ProviderStatId { get; init; }

    public IReadOnlyList<string>? ProviderStatAlternativeIds { get; init; }

    public string? ProviderDiagnosticCode { get; init; }
}

internal sealed class ModifierPipelineReplayCaptureConsumer
{
    public bool? IsSearchable { get; init; }

    public string? AvailabilityStatus { get; init; }

    public string? NotSearchableReason { get; init; }
}

internal sealed class ModifierPipelineNormalizedItem
{
    public required string ItemClass { get; init; }

    public required string Rarity { get; init; }

    public required string DisplayName { get; init; }

    public required string BaseType { get; init; }

    public string? BaseResolutionStatus { get; init; }

    public string? ResolvedBaseName { get; init; }

    public string? UniqueCanonicalName { get; init; }

    public string? UniqueCanonicalType { get; init; }

    public string? UniqueResolutionStatus { get; init; }

    public string? UniqueResolutionDiagnosticCode { get; init; }

    public IReadOnlyList<string> CompatibleVersionRoles { get; init; } = [];

    public IReadOnlyList<ModifierPipelineNormalizedModifier> Modifiers { get; init; } = [];

    public string FinalSerializedRequestAvailability { get; init; } =
        "unavailable-in-capture-and-replay-compare-uses-draft-filter-shape";

    public IReadOnlyList<ModifierPipelineNormalizedDraftFilter> DraftFilters { get; init; } = [];
}

internal sealed class ModifierPipelineNormalizedModifier
{
    public required string RowKey { get; init; }

    public string? ComponentId { get; init; }

    public int SourceModifierIndex { get; init; }

    public int SourceLineIndex { get; init; }

    public string? ParsedKind { get; init; }

    public string? OriginalText { get; init; }

    public IReadOnlyList<string> ValueLines { get; init; } = [];

    public IReadOnlyList<string> ObservedNumericValues { get; init; } = [];

    public string? CoreStatus { get; init; }

    public string? SourceDiagnosticCode { get; init; }

    public string? AggregateDiagnosticCode { get; init; }

    public string? UniqueBlockDiagnosticCode { get; init; }

    public IReadOnlyList<string> ModifierIds { get; init; } = [];

    public IReadOnlyList<string> StatIds { get; init; } = [];

    public IReadOnlyList<string> BlockIds { get; init; } = [];

    public string? ResolvedSourceKind { get; init; }

    public bool? HasExactProvenance { get; init; }

    public bool? IsBaseImplicit { get; init; }

    public string? ProviderStatus { get; init; }

    public IReadOnlyList<string> ProviderStatIds { get; init; } = [];

    public string? ProviderDiagnosticCode { get; init; }

    public bool? IsSearchable { get; init; }

    public string? NotSearchableReasonFamily { get; init; }
}

internal sealed class ModifierPipelineNormalizedDraftFilter
{
    public required string ComponentId { get; init; }

    public string? ProviderStatId { get; init; }

    public string? BoundShape { get; init; }

    public string? RequestedMinimum { get; init; }

    public string? RequestedMaximum { get; init; }

    public bool IsSearchable { get; init; }
}

internal sealed class ModifierPipelineReplayFieldDelta
{
    public required string Field { get; init; }

    public string? CapturedValue { get; init; }

    public string? ReplayValue { get; init; }

    public required string Classification { get; init; }

    public string Layer { get; init; } = "core";
}
