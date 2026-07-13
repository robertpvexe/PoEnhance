using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.GameData;

public sealed class JsonProvisionalGameDataStoreTests
{
    [Fact]
    public async Task UpsertAsync_CreatesNewFilePersistsFirstRecordAndReloads()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "provisional-game-data.json");
        var store = new JsonProvisionalGameDataStore(path);
        var record = Record("item-base|rings|new ring");

        var upsert = await store.UpsertAsync([record]);
        var reloaded = await new JsonProvisionalGameDataStore(path).LoadSnapshotAsync();

        Assert.True(upsert.IsSuccess);
        Assert.True(File.Exists(path));
        Assert.Single(upsert.Snapshot.Records);
        Assert.Equal(record.StableKey, Assert.Single(reloaded.Snapshot.Records).StableKey);
    }

    [Fact]
    public async Task UpsertAsync_RepeatedDiscoveryUpdatesExistingRecord()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "store.json");
        var store = new JsonProvisionalGameDataStore(path);
        var firstSeen = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        var lastSeen = firstSeen.AddMinutes(5);

        await store.UpsertAsync([Record("modifier|prefix|new mod", firstSeen)]);
        var result = await store.UpsertAsync([Record("modifier|prefix|new mod", lastSeen)]);

        var record = Assert.Single(result.Snapshot.Records);
        Assert.Equal(2, record.SeenCount);
        Assert.Equal(firstSeen, record.FirstSeenUtc);
        Assert.Equal(lastSeen, record.LastSeenUtc);
    }

    [Fact]
    public async Task UpsertAsync_DifferentStableKeysRemainSeparateAndJsonIsDeterministic()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "store.json");
        var store = new JsonProvisionalGameDataStore(path);

        await store.UpsertAsync(
        [
            Record("modifier|suffix|zeta"),
            Record("item-base|rings|alpha"),
        ]);
        var firstJson = await File.ReadAllTextAsync(path);

        await store.UpsertAsync([]);
        var secondJson = await File.ReadAllTextAsync(path);

        Assert.Equal(firstJson, secondJson);
        Assert.Contains("item-base|rings|alpha", firstJson);
        Assert.Contains("modifier|suffix|zeta", firstJson);
        Assert.True(firstJson.IndexOf("item-base|rings|alpha", StringComparison.Ordinal) <
            firstJson.IndexOf("modifier|suffix|zeta", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpsertAsync_WritesThroughTemporaryFileWithoutLeavingTempFiles()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "store.json");
        var store = new JsonProvisionalGameDataStore(path);

        var result = await store.UpsertAsync([Record("item-base|rings|new ring")]);

        Assert.True(result.IsSuccess);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
        Assert.True((await new JsonProvisionalGameDataStore(path).LoadSnapshotAsync()).IsSuccess);
    }

    [Fact]
    public async Task UpsertAsync_ConcurrentRapidUpsertsDoNotCorruptFile()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "store.json");
        var store = new JsonProvisionalGameDataStore(path);

        var tasks = Enumerable
            .Range(0, 20)
            .Select(_ => store.UpsertAsync([Record("modifier|prefix|new mod")]))
            .ToArray();
        await Task.WhenAll(tasks);

        var reloaded = await new JsonProvisionalGameDataStore(path).LoadSnapshotAsync();
        var record = Assert.Single(reloaded.Snapshot.Records);
        Assert.Equal(20, record.SeenCount);
    }

    [Fact]
    public async Task UpsertAsync_MalformedExistingFileIsNotOverwritten()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "store.json");
        await File.WriteAllTextAsync(path, "{ malformed");
        var store = new JsonProvisionalGameDataStore(path);

        var result = await store.UpsertAsync([Record("item-base|rings|new ring")]);

        Assert.False(result.IsSuccess);
        Assert.Contains("malformed", await File.ReadAllTextAsync(path));
        Assert.Contains("malformed", store.Status.LastDiagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertAsync_WriteFailureIsReportedWithoutThrowing()
    {
        using var temp = TempDirectory.Create();
        var directoryAsFilePath = Path.Combine(temp.Path, "store-directory");
        Directory.CreateDirectory(directoryAsFilePath);
        var store = new JsonProvisionalGameDataStore(directoryAsFilePath);

        var result = await store.UpsertAsync([Record("item-base|rings|new ring")]);

        Assert.False(result.IsSuccess);
        Assert.Contains("failed", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveDefaultPath_UsesUserLocalApplicationData()
    {
        var resolver = new ProvisionalGameDataStorePathResolver(
            folder => folder == Environment.SpecialFolder.LocalApplicationData
                ? @"C:\Users\Test\AppData\Local"
                : throw new InvalidOperationException());

        var path = resolver.ResolveDefaultPath();

        Assert.Equal(
            Path.Combine(@"C:\Users\Test\AppData\Local", "PoEnhance", "provisional-game-data.json"),
            path);
    }

    private static ProvisionalGameDataRecord Record(
        string stableKey,
        DateTimeOffset? seenAt = null)
    {
        var timestamp = seenAt ?? new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        return new ProvisionalGameDataRecord
        {
            StableKey = stableKey,
            Kind = stableKey.StartsWith("modifier", StringComparison.Ordinal)
                ? ProvisionalGameDataRecordKind.Modifier
                : ProvisionalGameDataRecordKind.ItemBase,
            NormalizedIdentity = stableKey.Split('|')[^1],
            OriginalIdentity = stableKey.Split('|')[^1],
            FirstSeenUtc = timestamp,
            LastSeenUtc = timestamp,
            SeenCount = 1,
            Source = "local-parser",
            Confidence = "unknown-missing-catalog",
            DiscoveryContext = "test",
        };
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"poenhance-provisional-store-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
