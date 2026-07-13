namespace PoEnhance.App.Infrastructure.GameData;

internal sealed record ProvisionalGameDataStoreStatus
{
    public string FilePath { get; init; } = string.Empty;

    public int RecordCount { get; init; }

    public string LastDiagnostic { get; init; } = "Not loaded";
}
