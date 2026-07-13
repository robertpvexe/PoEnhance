namespace PoEnhance.GameData;

public sealed record ProvisionalGameDataSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public IReadOnlyList<ProvisionalGameDataRecord> Records { get; init; } = [];
}
