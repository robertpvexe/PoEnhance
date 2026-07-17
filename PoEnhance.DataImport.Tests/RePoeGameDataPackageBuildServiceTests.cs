using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoeGameDataPackageBuildServiceTests
{
    private static readonly DateTimeOffset FixedCreatedAtUtc = new(2026, 7, 12, 10, 30, 0, TimeSpan.Zero);
    private static readonly Lazy<TestSource> SharedTestSource = new(CreateSharedTestSource);

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
        Assert.Equal(4, result.FinalCounts.Modifiers);
        Assert.Equal(55, result.FinalCounts.Stats);
        Assert.Equal(6, result.FinalCounts.StatTranslations);
        Assert.Equal(25, result.FinalCounts.ItemPropertySemantics);
        Assert.Equal(25, result.Package.ItemPropertySemantics.Count);
        Assert.Equal("weapon.physical-damage.increased-percent.local", result.Package.ItemPropertySemantics[0].Id);
        Assert.Equal("item.evasion-energy-shield.added.local", result.Package.ItemPropertySemantics[^1].Id);
        Assert.Contains(result.Package.ItemPropertySemantics, descriptor =>
            descriptor.Id == "weapon.attack-speed.increased-percent.local");
        Assert.Contains(result.Package.ItemPropertySemantics, descriptor =>
            descriptor.Id == "weapon.critical-strike-chance.added.local");
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
        Assert.Equal("master", source.SourceBranch);
        Assert.Equal(SharedTestSource.Value.SourceVersion, source.SourceVersion);
        Assert.Equal(4, source.InputFiles.Count);
        Assert.All(source.InputFiles, input =>
        {
            Assert.False(string.IsNullOrWhiteSpace(input.RelativePath));
            Assert.False(string.IsNullOrWhiteSpace(input.Sha256));
            Assert.True(input.SizeBytes > 0);
        });
        Assert.DoesNotContain(source.InputFiles, input => input.Label == "item-property-semantics.json");
        var semanticInput = Assert.IsType<GameDataPackageReviewedItemPropertySemanticInput>(
            package.Manifest.ReviewedItemPropertySemantics);
        Assert.Equal("poenhance.item-property-semantics", semanticInput.SourceId);
        Assert.Equal("item-property-semantics.json", semanticInput.Label);
        Assert.Equal("item-property-semantics.json", semanticInput.DisplayPath);
        Assert.Equal(new FileInfo(RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath).Length, semanticInput.SizeBytes);
        Assert.Equal(ComputeSha256(RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath), semanticInput.Sha256);
        Assert.Equal(1, semanticInput.SchemaVersion);
        Assert.Equal("aps-crit-defence-v1", semanticInput.ReviewVersion);
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
        Assert.Equal(
            first.Package!.ItemPropertySemantics.Select(descriptor => descriptor.Id),
            second.Package!.ItemPropertySemantics.Select(descriptor => descriptor.Id));
        Assert.Equal(
            first.Package.ItemPropertySemantics.Select(descriptor => string.Join("\0", descriptor.OrderedStatIds)),
            second.Package.ItemPropertySemantics.Select(descriptor => string.Join("\0", descriptor.OrderedStatIds)));
        Assert.Equal(
            first.Package.Manifest.ReviewedItemPropertySemantics,
            second.Package.Manifest.ReviewedItemPropertySemantics);
    }

    [Fact]
    public void Build_SemanticInputOutsideRePoeDataRoot_IsAcceptedAsSeparateReviewedInput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var request = CreateWorkspaceRequest(workspace, workspace.PathFor("package.json"));
        var semanticPath = workspace.WriteText(
            Path.Combine("reviewed", "item-property-semantics.json"),
            File.ReadAllText(RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath));
        request = request with { ItemPropertySemanticsPath = semanticPath };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.Success, result.ExitCode);
        Assert.NotNull(result.Package);
        Assert.Equal(25, result.Package.ItemPropertySemantics.Count);
        Assert.DoesNotContain(
            Assert.Single(result.Package.Manifest.Sources).InputFiles,
            input => input.Label == "item-property-semantics.json");
        Assert.Equal(
            "poenhance.item-property-semantics",
            result.Package.Manifest.ReviewedItemPropertySemantics?.SourceId);
    }

    [Fact]
    public void Build_MissingInputFile_ReturnsMissingInputAndDoesNotWriteOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.PathFor("package.json");
        var request = CreateWorkspaceRequest(workspace, outputPath) with
        {
            ModsPath = workspace.PathFor("source-data", "missing-mods.json"),
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.MissingInputFile, result.ExitCode);
        Assert.False(File.Exists(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.BuildInputFileMissing &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Build_MissingSemanticArgument_ReturnsInvalidArgumentsAndPreservesOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.WriteText("package.json", "existing valid package");
        var request = CreateWorkspaceRequest(workspace, outputPath) with
        {
            ItemPropertySemanticsPath = null,
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal("existing valid package", File.ReadAllText(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.BuildArgumentInvalid &&
            diagnostic.SourceRecordId == "--item-property-semantics");
    }

    [Fact]
    public void Build_NonexistentSemanticFile_ReturnsMissingInputAndPreservesOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.WriteText("package.json", "existing valid package");
        var request = CreateWorkspaceRequest(workspace, outputPath) with
        {
            ItemPropertySemanticsPath = workspace.PathFor("reviewed", "missing.json"),
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.MissingInputFile, result.ExitCode);
        Assert.Equal("existing valid package", File.ReadAllText(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.BuildInputFileMissing &&
            diagnostic.SourceRecordId == "--item-property-semantics");
    }

    [Theory]
    [InlineData("malformed", ItemPropertySemanticImportDiagnosticCodes.JsonMalformed)]
    [InlineData("review-version-mismatch", ItemPropertySemanticImportDiagnosticCodes.ReviewVersionMismatch)]
    [InlineData("unknown-stat", ItemPropertySemanticImportDiagnosticCodes.ValidationFailed)]
    [InlineData("duplicate-vector", ItemPropertySemanticImportDiagnosticCodes.ValidationFailed)]
    public void Build_InvalidSemanticInput_PreservesExistingOutput(
        string invalidInput,
        string expectedDiagnosticCode)
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.WriteText("package.json", "existing valid package");
        var semanticJson = CreateInvalidSemanticJson(invalidInput);
        var semanticPath = workspace.WriteText(
            Path.Combine("reviewed", "item-property-semantics.json"),
            semanticJson);
        var request = CreateWorkspaceRequest(workspace, outputPath) with
        {
            ItemPropertySemanticsPath = semanticPath,
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.SourceImportFailure, result.ExitCode);
        Assert.Equal("existing valid package", File.ReadAllText(outputPath));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp", SearchOption.AllDirectories));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedDiagnosticCode);
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
    public void Build_SourceVersionMismatch_ReturnsInvalidArgumentsAndDoesNotWriteOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.PathFor("package.json");
        var request = CreateWorkspaceRequest(workspace, outputPath) with
        {
            SourceVersion = "8023a1d696dbddc836c05ac3fcedd072da1767d2",
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.InvalidArguments, result.ExitCode);
        Assert.False(File.Exists(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.BuildArgumentInvalid &&
            diagnostic.SourceRecordId == "--source-version" &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Build_InputOutsideDeclaredSourceDataRoot_ReturnsInvalidArguments()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.PathFor("package.json");
        var outsideModsPath = workspace.WriteText(
            "outside-mods.json",
            File.ReadAllText(RePoeImportTestFixtures.ReducedModsPath));
        var request = CreateWorkspaceRequest(workspace, outputPath) with
        {
            ModsPath = outsideModsPath,
        };

        var result = _service.Build(request);

        Assert.Equal(GameDataPackageBuildExitCode.InvalidArguments, result.ExitCode);
        Assert.False(File.Exists(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.BuildArgumentInvalid &&
            diagnostic.SourceRecordId == "mods.json" &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Build_MalformedSourceFile_ReturnsSourceImportFailureAndDoesNotWriteOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var outputPath = workspace.PathFor("package.json");
        var request = CreateWorkspaceRequest(workspace, outputPath);
        var malformedBaseItemsPath = workspace.WriteText(Path.Combine("source-data", "base_items.bad.json"), "[]");
        request = request with
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
        var outputPath = workspace.PathFor("package.json");
        var request = CreateWorkspaceRequest(workspace, outputPath);
        var malformedStatsPath = workspace.WriteText(Path.Combine("source-data", "stats.bad.json"), "[]");
        request = request with
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
        var outputPath = workspace.WriteText("package.json", "existing valid package");
        var request = CreateWorkspaceRequest(workspace, outputPath);
        var badModsPath = workspace.WriteText(
            Path.Combine("source-data", "mods.unknown-stat.json"),
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
        request = request with
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
            modifiers => AssertSummary(modifiers, "Modifiers", 4, 4, 0),
            stats => AssertSummary(stats, "Stats", 55, 55, 0),
            translations => AssertSummary(translations, "StatTranslations", 6, 6, 0),
            semantics => AssertSummary(semantics, "ItemPropertySemantics", 25, 25, 0));
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
        var testSource = SharedTestSource.Value;
        return new GameDataPackageBuildRequest
        {
            BaseItemsPath = RePoeImportTestFixtures.ReducedBaseItemsPath,
            ModsPath = RePoeImportTestFixtures.ReducedModsPath,
            StatsPath = RePoeImportTestFixtures.ReducedStatsPath,
            TranslationsPath = RePoeImportTestFixtures.ReducedStatTranslationsPath,
            ItemPropertySemanticsPath = RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath,
            OutputPath = outputPath,
            SourceRootPath = testSource.SourceRoot,
            SourceDataRootPath = testSource.DataRoot,
            SourceUri = testSource.SourceUri,
            SourceBranch = testSource.SourceBranch,
            DataVersion = "dev-test",
            League = "Mercenaries",
            Patch = "3.26.0",
            SourceVersion = testSource.SourceVersion,
            CreatedAtUtc = FixedCreatedAtUtc,
        };
    }

    private static GameDataPackageBuildRequest CreateWorkspaceRequest(
        TemporaryWorkspace workspace,
        string outputPath)
    {
        var dataRoot = workspace.PathFor("source-data");
        Directory.CreateDirectory(dataRoot);

        var baseItemsPath = CopyFixture(RePoeImportTestFixtures.ReducedBaseItemsPath, dataRoot, "base_items.json");
        var modsPath = CopyFixture(RePoeImportTestFixtures.ReducedModsPath, dataRoot, "mods.json");
        var statsPath = CopyFixture(RePoeImportTestFixtures.ReducedStatsPath, dataRoot, "stats.json");
        var translationsPath = CopyFixture(
            RePoeImportTestFixtures.ReducedStatTranslationsPath,
            dataRoot,
            "stat_translations.json");

        return CreateRequest(outputPath) with
        {
            BaseItemsPath = baseItemsPath,
            ModsPath = modsPath,
            StatsPath = statsPath,
            TranslationsPath = translationsPath,
            SourceDataRootPath = dataRoot,
        };
    }

    private static string CopyFixture(string sourcePath, string dataRoot, string fileName)
    {
        var destinationPath = Path.Combine(dataRoot, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        return destinationPath;
    }

    private static string CreateInvalidSemanticJson(string invalidInput)
    {
        if (invalidInput == "malformed")
        {
            return "{ not valid json";
        }

        var root = JsonNode.Parse(File.ReadAllText(RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath))!;
        switch (invalidInput)
        {
            case "review-version-mismatch":
                root["reviewVersion"] = "other-review";
                break;
            case "unknown-stat":
                root["descriptors"]![0]!["orderedStatIds"]![0] = "unknown_package_stat";
                break;
            case "duplicate-vector":
                root["descriptors"]![1]!["orderedStatIds"] = new JsonArray("local_physical_damage_+%");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidInput));
        }

        return root.ToJsonString();
    }

    private static TestSource CreateSharedTestSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "PoEnhance.DataImport.Tests", "source-" + Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "repoe");
        Directory.CreateDirectory(sourceRoot);

        File.WriteAllText(Path.Combine(sourceRoot, "README.md"), "PoEnhance test source provenance repository.");
        RunGit(sourceRoot, "init");
        RunGit(sourceRoot, "checkout -B master");
        RunGit(sourceRoot, "remote add origin https://github.com/repoe-fork/repoe");
        RunGit(sourceRoot, "add README.md");
        RunGit(sourceRoot, "-c user.name=PoEnhance -c user.email=poenhance@example.invalid commit -m init");
        var sourceVersion = RunGit(sourceRoot, "rev-parse HEAD");

        return new TestSource(
            sourceRoot,
            Path.GetDirectoryName(RePoeImportTestFixtures.ReducedBaseItemsPath)!,
            "https://github.com/repoe-fork/repoe",
            "master",
            sourceVersion);
    }

    private static string RunGit(string workingDirectory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{workingDirectory}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        Assert.NotNull(process);

        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {arguments} failed: {error}");
        return output;
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

    private sealed record TestSource(
        string SourceRoot,
        string DataRoot,
        string SourceUri,
        string SourceBranch,
        string SourceVersion);

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
