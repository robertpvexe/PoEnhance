using System.Text.Json.Serialization;

namespace PoEnhance.DataTool.UniqueCorpusGate;

internal sealed class UniqueCorpusGateCaptureDocument
{
    public string? DiagnosticVersion { get; init; }

    public DateTimeOffset? CapturedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public UniqueCorpusGateCaptureItem? Item { get; init; }

    public UniqueCorpusGateCaptureUniqueIdentity? UniqueIdentity { get; init; }

    public IReadOnlyList<UniqueCorpusGateCaptureModifier>? Modifiers { get; init; }
}

internal sealed class UniqueCorpusGateCaptureItem
{
    public string? ItemClass { get; init; }

    public string? Rarity { get; init; }

    public string? DisplayName { get; init; }

    public string? ParsedBaseType { get; init; }

    public string? BaseResolutionStatus { get; init; }

    public string? ResolvedBaseName { get; init; }
}

internal sealed class UniqueCorpusGateCaptureUniqueIdentity
{
    public string? CanonicalName { get; init; }

    public string? CanonicalType { get; init; }

    public string? Foulborn { get; init; }
}

internal sealed class UniqueCorpusGateCaptureModifier
{
    public string? ComponentId { get; init; }

    public UniqueCorpusGateCaptureRaw? Raw { get; init; }

    public UniqueCorpusGateCaptureSourceResolution? SourceResolution { get; init; }

    public UniqueCorpusGateCaptureSemantics? ResolvedSemantics { get; init; }

    public UniqueCorpusGateCaptureSignatures? Signatures { get; init; }

    public IReadOnlyList<UniqueCorpusGateCaptureProviderPass>? ProviderPasses { get; init; }

    public UniqueCorpusGateCaptureProviderOutcome? ProviderResolution { get; init; }

    public UniqueCorpusGateCaptureConsumer? Consumer { get; init; }
}

internal sealed class UniqueCorpusGateCaptureRaw
{
    public string? ParsedKind { get; init; }

    public string? UniqueOrigin { get; init; }

    public string? ImplicitOrigin { get; init; }

    public string? OriginalText { get; init; }
}

internal sealed class UniqueCorpusGateCaptureSourceResolution
{
    public string? Status { get; init; }

    public string? ResolvedModifierId { get; init; }

    public IReadOnlyList<string>? ResolvedStatIds { get; init; }

    public string? UniqueResolutionDiagnosticCode { get; init; }

    public int SourceCandidateCount { get; init; }
}

internal sealed class UniqueCorpusGateCaptureSemantics
{
    public string? ParsedKind { get; init; }

    public string? UniqueOrigin { get; init; }

    public string? ResolvedSourceKind { get; init; }

    public string? ResolvedSourceUniqueOrigin { get; init; }

    public bool HasResolvedUniqueSourceSemantics { get; init; }

    public bool HasExactUniqueSourceProvenance { get; init; }
}

internal sealed class UniqueCorpusGateCaptureSignatures
{
    public string? OriginalText { get; init; }

    public string? CanonicalSignature { get; init; }

    public string? ProviderCanonicalSignature { get; init; }

    public IReadOnlyList<string>? ProviderSearchSignatures { get; init; }
}

internal sealed class UniqueCorpusGateCaptureProviderPass
{
    public string? ResolutionPhase { get; init; }

    public string? SkipReason { get; init; }

    public bool CanResolveProviderComponent { get; init; }

    public UniqueCorpusGateCaptureMatch? Match { get; init; }

    public UniqueCorpusGateCaptureProjection? Projection { get; init; }
}

internal sealed class UniqueCorpusGateCaptureMatch
{
    public string? Status { get; init; }

    public IReadOnlyList<UniqueCorpusGateCaptureDiagnostic>? Diagnostics { get; init; }
}

internal sealed class UniqueCorpusGateCaptureProjection
{
    public bool IsFaithful { get; init; }

    public string? ProjectionKind { get; init; }
}

internal sealed class UniqueCorpusGateCaptureDiagnostic
{
    public string? Code { get; init; }

    public string? Message { get; init; }
}

internal sealed class UniqueCorpusGateCaptureProviderOutcome
{
    public string? ProviderResolutionStatus { get; init; }

    public string? ProviderDiagnosticCode { get; init; }

    public string? ProviderDiagnosticMessage { get; init; }
}

internal sealed class UniqueCorpusGateCaptureConsumer
{
    public bool IsSearchable { get; init; }

    public string? NotSearchableReason { get; init; }

    public string? AvailabilityStatus { get; init; }

    public UniqueCorpusGateCaptureSerialization? Serialization { get; init; }
}

internal sealed class UniqueCorpusGateCaptureSerialization
{
    public bool Attempted { get; init; }

    public bool Success { get; init; }

    public string? DiagnosticCode { get; init; }

    public string? BlockedReason { get; init; }
}

internal sealed class UniqueCorpusGateAnalyzedCapture
{
    public required string FileName { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public required UniqueCorpusGateCaptureDocument Document { get; init; }

    public required string ItemIdentityKey { get; init; }

    public required string ItemName { get; init; }
}

internal sealed class UniqueCorpusGateAnalyzedComponent
{
    public required string ItemIdentityKey { get; init; }

    public required string ItemName { get; init; }

    public required string Outcome { get; init; }

    public required string ParsedKind { get; init; }

    public required string ResolvedSourceKind { get; init; }

    public required string SourceFamily { get; init; }

    public required string Stage { get; init; }

    public required string RootCauseKey { get; init; }

    public required string NormalizedSignature { get; init; }

    public IReadOnlyList<string> DiagnosticCodes { get; init; } = [];
}
