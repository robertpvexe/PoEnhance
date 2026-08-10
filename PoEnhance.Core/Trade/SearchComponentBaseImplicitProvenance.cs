using PoEnhance.Core.Items.GameData;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

public sealed record SearchComponentBaseImplicitProvenance
{
    public required BaseImplicitRecognitionStatus RecognitionStatus { get; init; }

    public IReadOnlyList<string> MechanicalSignatures { get; init; } = [];

    public IReadOnlyList<SearchComponentBaseImplicitSourceSnapshot> SourceSnapshots { get; init; } = [];

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }
}

public sealed record SearchComponentBaseImplicitSourceSnapshot
{
    public string? SnapshotId { get; init; }

    public BaseImplicitSnapshotRole Role { get; init; }

    public string? CommitSha { get; init; }

    public string? DataVersion { get; init; }
}
