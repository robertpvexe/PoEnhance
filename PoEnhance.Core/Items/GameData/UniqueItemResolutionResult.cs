using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed record UniqueItemResolutionResult
{
    public UniqueItemResolutionStatus Status { get; init; }

    public UniqueItemIdentity? Identity { get; init; }

    public IReadOnlyList<UniqueItemIdentity> IdentityCandidates { get; init; } = [];

    public IReadOnlyList<UniqueItemVersionObservation> CompatibleVersions { get; init; } = [];

    public IReadOnlyList<UniqueModifierBlockResolution> ModifierBlocks { get; init; } = [];

    public bool IsFoulborn { get; init; }

    public bool IsLegacy => CompatibleVersions.Count > 0 &&
        CompatibleVersions.All(version => version.Role == UniqueItemVersionRole.Historical);

    public string? DiagnosticCode { get; init; }

    public string? Diagnostic { get; init; }
}
