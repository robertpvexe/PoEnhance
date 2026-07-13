namespace PoEnhance.GameData;

public sealed record ProvisionalGameDataRecord
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string StableKey { get; init; } = string.Empty;

    public ProvisionalGameDataRecordKind Kind { get; init; }

    public string NormalizedIdentity { get; init; } = string.Empty;

    public string OriginalIdentity { get; init; } = string.Empty;

    public string? ItemClass { get; init; }

    public string? ModifierKind { get; init; }

    public ModifierGenerationType? ModifierGenerationType { get; init; }

    public DateTimeOffset FirstSeenUtc { get; init; }

    public DateTimeOffset LastSeenUtc { get; init; }

    public int SeenCount { get; init; }

    public string Source { get; init; } = string.Empty;

    public string? League { get; init; }

    public string? Patch { get; init; }

    public string Confidence { get; init; } = string.Empty;

    public string DiscoveryContext { get; init; } = string.Empty;
}
