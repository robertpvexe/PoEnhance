using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

internal interface IProvisionalGameDataStore
{
    ProvisionalGameDataStoreStatus Status { get; }

    Task<ProvisionalGameDataStoreResult> LoadSnapshotAsync(
        CancellationToken cancellationToken = default);

    Task<ProvisionalGameDataStoreResult> UpsertAsync(
        IReadOnlyList<ProvisionalGameDataRecord> records,
        CancellationToken cancellationToken = default);
}
