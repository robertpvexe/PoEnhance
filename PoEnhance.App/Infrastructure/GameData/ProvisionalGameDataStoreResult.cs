using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed record ProvisionalGameDataStoreResult
{
    public bool IsSuccess { get; init; }

    public ProvisionalGameDataSnapshot Snapshot { get; init; } = new();

    public string Diagnostic { get; init; } = string.Empty;

    public static ProvisionalGameDataStoreResult Success(
        ProvisionalGameDataSnapshot snapshot,
        string diagnostic)
    {
        return new ProvisionalGameDataStoreResult
        {
            IsSuccess = true,
            Snapshot = snapshot,
            Diagnostic = diagnostic,
        };
    }

    public static ProvisionalGameDataStoreResult Failure(
        ProvisionalGameDataSnapshot snapshot,
        string diagnostic)
    {
        return new ProvisionalGameDataStoreResult
        {
            IsSuccess = false,
            Snapshot = snapshot,
            Diagnostic = diagnostic,
        };
    }
}
