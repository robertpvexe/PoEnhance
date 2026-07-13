using System.Security.Cryptography;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoeGameDataPackageBuildServiceTests
{
    private static readonly DateTimeOffset FixedCreatedAtUtc = new(2026, 7, 12, 10, 30, 0, TimeSpan.Zero);

    private readonly RePoeGameDataPackageBuildService _service = new();

    [Fact]
    public void Build_WithReducedFixtures_WritesCompleteValidPackage()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.PathFor("out", "poenhance-game-data.json");

        var result = _service.Build(CreateRequest(outputPath));

        Assert.Equal(GameDataPackageBuildExitCode.Success, result.ExitCode);
        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(outputPath));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error);
        Assert.NotNull(result.Package);
        Assert.True(GameDataPackageValidator.Validate(result.Package).IsValid);
        Assert.Equal(6, result.FinalCounts.ItemBases);
        Assert.Equal(3, result.FinalCounts.Modifiers);
        Assert.Equal(19, result.FinalCounts.Stats);
        Assert.Equal(6, result.FinalCounts.StatTranslations);
        Assert.Equal(new FileInfo(outputPath).Length, result.OutputFileSizeBytes);
        Assert.Equal(ComputeSha256(outputPath), result.Sha256);
    }

    [Fact]
    public void Build_OutputJson_RoundTripsAndPreservesManifest()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.PathFor("package.json");

        var result = _service.Build(CreateRequest(outputPath));
        var package = GameDataPackageJson.Deserialize(File.ReadAllText(outputPath));

        Assert.Equal(GameDataPackageBuildExitCode.Success, result.ExitCode);
        Assert.NotNull(package);
        Assert.Equal("dev-test", package.Manifest.DataVersion);
        Assert.Equal("Mercenaries", package.Manifest.League);
        Assert.Equal("3.26.0", package.Manifest.Patch);
        Assert.Equal(FixedCreatedAtUtc, package.Manifest.CreatedAtUtc);
        var source = Assert.Single(package.Manifest.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal("https://github.com/repoe-fork/repoe", source.SourceUri);
        Assert.Equal("c50acab2ed660a70511e7f91ee09db4e632089e4", source.SourceVersion);
        Assert.True(GameDataPackageValidator.Validate(package).IsValid);
    }

    [Fact]
    public void Build_WithFixedCreatedAtUtc_ProducesDeterministicOutputAndHash()
    {
        using var workspace = TemporaryWorkspace.Create();
        var firstPath = workspace.PathFor("first.json");
        var secondPath = workspace.PathFor("second.json");

        var first = _service.Build(CreateRequest(firstPath));
        var second = _service.Build(CreateRequest(secondPath));

        Assert.Equal(GameDataPackageBuildExitCode.Success, first.ExitCode);
        Assert.Equal(GameDataPackageBuildExitCode.Success, second.ExitCode);
        Assert.Equal(File.ReadAllText(firstPath), File.ReadAllText(secondPath));
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(ComputeSha256(firstPath), first.Sha256);
    }

    [Fact]
    public void Build_MissingInputFile_ReturnsMissingInputAndDoesNotWriteOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.PathFor("package.json");
        var request = CreateRequest(outputPath) with
        {
            ModsPath = workspace.PathFor("missing-mods.json"),
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.MissingInputFile, result.ExitCode);
        Assert.False(File.Exists(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.BuildInputFileMissing &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Build_InvalidArguments_ReturnsInvalidArguments()
    {
        var result = _service.Build(new GameDataPackageBuildRequest());

        Assert.Equal(GameDataPackageBuildExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.BuildArgumentInvalid &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Build_MalformedSourceFile_ReturnsSourceImportFailureAndDoesNotWriteOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var malformedBaseItemsPath = workspace.WriteText("base_items.bad.json", "[]");
        var outputPath = workspace.PathFor("package.json");
        var request = CreateRequest(outputPath) with
        {
            BaseItemsPath = malformedBaseItemsPath,
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.SourceImportFailure, result.ExitCode);
        Assert.False(File.Exists(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.SchemaUnsupported &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Build_ImportWithErrorDiagnostics_DoesNotWriteOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var malformedStatsPath = workspace.WriteText("stats.bad.json", "[]");
        var outputPath = workspace.PathFor("package.json");
        var request = CreateRequest(outputPath) with
        {
            StatsPath = malformedStatsPath,
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.SourceImportFailure, result.ExitCode);
        Assert.False(File.Exists(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.SchemaUnsupported &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Build_ValidationFailure_DoesNotOverwriteExistingOutputOrLeaveTempFile()
    {
        using var workspace = TemporaryWorkspace.Create();
        var badModsPath = workspace.WriteText(
            "mods.unknown-stat.json",
            """
            {
              "BadUnknownStatMod": {
                "domain": "item",
                "generation_type": "prefix",
                "groups": ["BadUnknownStatGroup"],
                "stats": [
                  {
                    "id": "missing_stat_id",
                    "min": 1,
                    "max": 1
                  }
                ]
              }
            }
            """);
        var outputPath = workspace.WriteText("package.json", "existing valid package");
        var request = CreateRequest(outputPath) with
        {
            ModsPath = badModsPath,
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.PackageValidationFailure, result.ExitCode);
        Assert.Equal("existing valid package", File.ReadAllText(outputPath));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp", SearchOption.AllDirectories));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.PackageModifierStatReferenceMissing &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Build_CreatesOutputDirectory()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.PathFor("nested", "out", "package.json");

        var result = _service.Build(CreateRequest(outputPath));

        Assert.Equal(GameDataPackageBuildExitCode.Success, result.ExitCode);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void Build_SourceSummaries_ReportExpectedCounts()
    {
        using var workspace = TemporaryWorkspace.Create();

        var result = _service.Build(CreateRequest(workspace.PathFor("package.json")));

        Assert.Equal(GameDataPackageBuildExitCode.Success, result.ExitCode);
        Assert.Collection(
            result.SourceSummaries,
            itemBases => AssertSummary(itemBases, "ItemBases", 6, 6, 0),
            modifiers => AssertSummary(modifiers, "Modifiers", 3, 3, 0),
            stats => AssertSummary(stats, "Stats", 19, 19, 0),
            translations => AssertSummary(translations, "StatTranslations", 6, 6, 0));
    }

    [Fact]
    public void Build_AllSourceReferencesPointToManifestRePoeSource()
    {
        using var workspace = TemporaryWorkspace.Create();

        var result = _service.Build(CreateRequest(workspace.PathFor("package.json")));

        Assert.Equal(GameDataPackageBuildExitCode.Success, result.ExitCode);
        Assert.NotNull(result.Package);
        var manifestSource = Assert.Single(result.Package.Manifest.Sources);
        Assert.Equal("repoe", manifestSource.SourceId);
        Assert.Equal("https://github.com/repoe-fork/repoe", manifestSource.SourceUri);

        Assert.All(result.Package.ItemBases.SelectMany(record => record.Sources), AssertRePoeSource);
        Assert.All(result.Package.Modifiers.SelectMany(record => record.Sources), AssertRePoeSource);
        Assert.All(result.Package.Stats.SelectMany(record => record.Sources), AssertRePoeSource);
        Assert.All(result.Package.StatTranslations.SelectMany(record => record.Sources), AssertRePoeSource);
    }

    private static GameDataPackageBuildRequest CreateRequest(string outputPath)
    {
        return new GameDataPackageBuildRequest
        {
            BaseItemsPath = RePoeImportTestFixtures.ReducedBaseItemsPath,
            ModsPath = RePoeImportTestFixtures.ReducedModsPath,
            StatsPath = RePoeImportTestFixtures.ReducedStatsPath,
            TranslationsPath = RePoeImportTestFixtures.ReducedStatTranslationsPath,
            OutputPath = outputPath,
            DataVersion = "dev-test",
            League = "Mercenaries",
            Patch = "3.26.0",
            SourceVersion = "c50acab2ed660a70511e7f91ee09db4e632089e4",
            CreatedAtUtc = FixedCreatedAtUtc,
        };
    }

    private static void AssertSummary(
        GameDataPackageBuildSourceSummary summary,
        string sourceName,
        int sourceRecordsRead,
        int recordsImported,
        int recordsSkipped)
    {
        Assert.Equal(sourceName, summary.SourceName);
        Assert.Equal(sourceRecordsRead, summary.SourceRecordsRead);
        Assert.Equal(recordsImported, summary.RecordsImported);
        Assert.Equal(recordsSkipped, summary.RecordsSkipped);
    }

    private static void AssertRePoeSource(GameDataSourceReference source)
    {
        Assert.Equal("repoe", source.SourceId);
        Assert.False(string.IsNullOrWhiteSpace(source.ExternalId));
        Assert.Null(source.ExternalUri);
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TemporaryWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "PoEnhance.DataImport.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TemporaryWorkspace(root);
        }

        public string PathFor(params string[] paths)
        {
            return Path.Combine([Root, .. paths]);
        }

        public string WriteText(string relativePath, string contents)
        {
            var path = PathFor(relativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup for test artifacts.
            }
        }
    }
}
