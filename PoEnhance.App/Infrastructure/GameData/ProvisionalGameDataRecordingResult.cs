namespace PoEnhance.App.Infrastructure.GameData;

internal sealed record ProvisionalGameDataRecordingResult
{
    public bool IsSuccess { get; init; }

    public int DiscoveredRecordCount { get; init; }

    public ProvisionalGameDataStoreStatus StoreStatus { get; init; } = new();

    public string Diagnostic { get; init; } = string.Empty;
}
