using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed record StatTranslationRecognitionEvidence
{
    public StatTranslationRecognitionRole Role { get; init; }

    public string? SourceSnapshotId { get; init; }

    public string? SourceRepositoryUri { get; init; }

    public string? SourceCommitSha { get; init; }

    public string? SourceDataVersion { get; init; }

    public string? ObservationId { get; init; }

    public string? CanonicalObservationId { get; init; }

    public string? CanonicalMechanicalSignature { get; init; }

    public ModifierTextSignature CanonicalSignature { get; init; } = ModifierTextSignature.Create([]);

    public StatTranslationDefinition? RecognizedTranslation { get; init; }

    public StatTranslationDefinition? CanonicalTranslation { get; init; }
}
