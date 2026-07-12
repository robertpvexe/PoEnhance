using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class GameDataPackageLoaderTests
{
    [Fact]
    public async Task LoadFromStreamAsync_ValidPackage_ReturnsSuccessfulResult()
    {
        using var stream = CreatePackageStream(GameDataPackageFixtures.CreateDevelopmentPackage());

        var result = await GameDataPackageLoader.LoadFromStreamAsync(stream);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Package);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.ValidationErrors);
        Assert.Null(result.SourcePath);
    }

    [Fact]
    public async Task LoadFromFileAsync_ValidPackage_ReturnsSuccessfulResultWithSourcePath()
    {
        var path = WriteTempPackage(GameDataPackageFixtures.CreateDevelopmentPackage());
        try
        {
            var result = await GameDataPackageLoader.LoadFromFileAsync(path);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Package);
            Assert.Equal(Path.GetFullPath(path), result.SourcePath);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task LoadFromFileAsync_MissingFile_ReturnsFileNotFoundDiagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-missing-{Guid.NewGuid():N}.json");

        var result = await GameDataPackageLoader.LoadFromFileAsync(path);

        Assert.False(result.IsSuccess);
        AssertDiagnostic(result, GameDataPackageLoadDiagnosticCodes.FileNotFound);
        Assert.Equal(Path.GetFullPath(path), result.SourcePath);
    }

    [Fact]
    public async Task LoadFromFileAsync_EmptyFile_ReturnsFileEmptyDiagnostic()
    {
        var path = WriteTempText("   ");
        try
        {
            var result = await GameDataPackageLoader.LoadFromFileAsync(path);

            Assert.False(result.IsSuccess);
            AssertDiagnostic(result, GameDataPackageLoadDiagnosticCodes.FileEmpty);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task LoadFromStreamAsync_MalformedJson_ReturnsJsonInvalidDiagnostic()
    {
        using var stream = CreateTextStream("{");

        var result = await GameDataPackageLoader.LoadFromStreamAsync(stream);

        Assert.False(result.IsSuccess);
        AssertDiagnostic(result, GameDataPackageLoadDiagnosticCodes.JsonInvalid);
    }

    [Fact]
    public async Task LoadFromStreamAsync_UnsupportedRootSchema_ReturnsSchemaUnsupportedDiagnostic()
    {
        using var stream = CreateTextStream("[]");

        var result = await GameDataPackageLoader.LoadFromStreamAsync(stream);

        Assert.False(result.IsSuccess);
        AssertDiagnostic(result, GameDataPackageLoadDiagnosticCodes.SchemaUnsupported);
    }

    [Fact]
    public async Task LoadFromStreamAsync_UnsupportedSchemaVersion_ReturnsSchemaUnsupportedDiagnostic()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            Manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest() with
            {
                SchemaVersion = 2,
            },
        };
        using var stream = CreatePackageStream(package);

        var result = await GameDataPackageLoader.LoadFromStreamAsync(stream);

        Assert.False(result.IsSuccess);
        AssertDiagnostic(result, GameDataPackageLoadDiagnosticCodes.SchemaUnsupported);
    }

    [Fact]
    public async Task LoadFromStreamAsync_InvalidPackage_ReturnsValidationErrors()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            Manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest() with
            {
                DataVersion = "",
            },
        };
        using var stream = CreatePackageStream(package);

        var result = await GameDataPackageLoader.LoadFromStreamAsync(stream);

        Assert.False(result.IsSuccess);
        AssertDiagnostic(result, GameDataPackageLoadDiagnosticCodes.PackageInvalid);
        Assert.Contains(result.ValidationErrors, error =>
            error.Code == GameDataValidationErrorCodes.ManifestDataVersionRequired);
    }

    [Fact]
    public async Task LoadFromStreamAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var stream = CreatePackageStream(GameDataPackageFixtures.CreateDevelopmentPackage());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => GameDataPackageLoader.LoadFromStreamAsync(stream, cancellation.Token));
    }

    [Fact]
    public async Task LoadFromStreamAsync_ReadFailure_ReturnsReadFailedDiagnostic()
    {
        using var stream = new ThrowingReadStream();

        var result = await GameDataPackageLoader.LoadFromStreamAsync(stream);

        Assert.False(result.IsSuccess);
        AssertDiagnostic(result, GameDataPackageLoadDiagnosticCodes.ReadFailed);
    }

    [Fact]
    public async Task LoadFromStreamAsync_DiagnosticCodesAreStable()
    {
        using var stream = CreateTextStream("");

        var result = await GameDataPackageLoader.LoadFromStreamAsync(stream);

        Assert.Equal("GAMEDATA_FILE_NOT_FOUND", GameDataPackageLoadDiagnosticCodes.FileNotFound);
        Assert.Equal("GAMEDATA_FILE_EMPTY", GameDataPackageLoadDiagnosticCodes.FileEmpty);
        Assert.Equal("GAMEDATA_JSON_INVALID", GameDataPackageLoadDiagnosticCodes.JsonInvalid);
        Assert.Equal("GAMEDATA_SCHEMA_UNSUPPORTED", GameDataPackageLoadDiagnosticCodes.SchemaUnsupported);
        Assert.Equal("GAMEDATA_PACKAGE_INVALID", GameDataPackageLoadDiagnosticCodes.PackageInvalid);
        Assert.Equal("GAMEDATA_READ_FAILED", GameDataPackageLoadDiagnosticCodes.ReadFailed);
        AssertDiagnostic(result, GameDataPackageLoadDiagnosticCodes.FileEmpty);
    }

    private static MemoryStream CreatePackageStream(GameDataPackage package)
    {
        return CreateTextStream(GameDataPackageJson.Serialize(package));
    }

    private static MemoryStream CreateTextStream(string text)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(text));
    }

    private static string WriteTempPackage(GameDataPackage package)
    {
        return WriteTempText(GameDataPackageJson.Serialize(package));
    }

    private static string WriteTempText(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-package-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, text, Encoding.UTF8);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Test cleanup only.
        }
    }

    private static void AssertDiagnostic(GameDataPackageLoadResult result, string code)
    {
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.DoesNotContain(" at ", diagnostic.Message, StringComparison.Ordinal);
    }

    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new IOException("Synthetic read failure.");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
