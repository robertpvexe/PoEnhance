using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class GameDataPackageSemanticAugmentationServiceTests
{
    private static readonly Lazy<GameDataPackage> ValidInputPackage = new(CreateValidInputPackage);

    private readonly GameDataPackageSemanticAugmentationService _service = new();

    [Fact]
    public async Task Augment_ValidOlderPackage_CreatesSixSemanticsAndPreservesOriginalData()
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPackage = ValidInputPackage.Value;
        var inputPath = workspace.WritePackage("active.json", inputPackage);
        var outputPath = workspace.PathFor("candidate.json");
        var originalInputBytes = File.ReadAllBytes(inputPath);

        var result = _service.Augment(CreateRequest(inputPath, outputPath));

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.Success, result.ExitCode);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Package);
        Assert.Equal("candidate-version", result.Package.Manifest.DataVersion);
        Assert.Equal(6, result.Package.ItemPropertySemantics.Count);
        Assert.Equal(
            [
                "weapon.physical-damage.increased-percent.local",
                "weapon.physical-damage.added.local",
                "weapon.fire-damage.added.local",
                "weapon.cold-damage.added.local",
                "weapon.lightning-damage.added.local",
                "weapon.chaos-damage.added.local",
            ],
            result.Package.ItemPropertySemantics.Select(descriptor => descriptor.Id));
        AssertPreservedPackageData(inputPackage, result.Package);
        Assert.Equal(originalInputBytes, File.ReadAllBytes(inputPath));

        var loadResult = await GameDataPackageLoader.LoadFromFileAsync(outputPath);
        Assert.True(loadResult.IsSuccess);
        Assert.Empty(loadResult.ValidationErrors);
        Assert.NotNull(loadResult.Package);
    }

    [Fact]
    public void Augment_RecordsReviewedSemanticAndSourcePackageProvenance()
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPath = workspace.WritePackage("active.json", ValidInputPackage.Value);
        var outputPath = workspace.PathFor("candidate.json");
        var inputBytes = File.ReadAllBytes(inputPath);
        var semanticBytes = File.ReadAllBytes(RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath);

        var result = _service.Augment(CreateRequest(inputPath, outputPath));

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.Success, result.ExitCode);
        var manifest = result.Package!.Manifest;
        var semantic = Assert.IsType<GameDataPackageReviewedItemPropertySemanticInput>(
            manifest.ReviewedItemPropertySemantics);
        Assert.Equal("poenhance.item-property-semantics", semantic.SourceId);
        Assert.Equal("item-property-semantics.json", semantic.Label);
        Assert.Equal("item-property-semantics.json", semantic.DisplayPath);
        Assert.Equal(semanticBytes.LongLength, semantic.SizeBytes);
        Assert.Equal(ComputeSha256(semanticBytes), semantic.Sha256);
        Assert.Equal(1, semantic.SchemaVersion);
        Assert.Equal("weapon-dps-v1", semantic.ReviewVersion);

        var augmentation = Assert.IsType<GameDataPackageItemPropertySemanticAugmentation>(
            manifest.ItemPropertySemanticAugmentation);
        Assert.Equal("augment-package-semantics", augmentation.OperationId);
        Assert.Equal("input-package", augmentation.InputPackageLabel);
        Assert.Equal("active.json", augmentation.InputPackageDisplayPath);
        Assert.Equal(inputBytes.LongLength, augmentation.InputPackageSizeBytes);
        Assert.Equal(ComputeSha256(inputBytes), augmentation.InputPackageSha256);
        Assert.Equal("dev-repoe-base-items", augmentation.InputPackageDataVersion);
        Assert.Equal(augmentation.InputPackageSha256, result.InputPackageSha256);
    }

    [Fact]
    public void Augment_PackageWithExistingSemantics_ReplacesInsteadOfMerging()
    {
        using var workspace = TemporaryWorkspace.Create();
        var tracked = ImportTrackedSemantics();
        var inputPackage = ValidInputPackage.Value with
        {
            ItemPropertySemantics =
            [
                tracked[0] with { Id = "legacy.semantic.to.replace" },
            ],
        };
        Assert.True(GameDataPackageValidator.Validate(inputPackage).IsValid);
        var inputPath = workspace.WritePackage("already-augmented.json", inputPackage);

        var result = _service.Augment(CreateRequest(inputPath, workspace.PathFor("candidate.json")));

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.Success, result.ExitCode);
        Assert.Equal(6, result.Package!.ItemPropertySemantics.Count);
        Assert.DoesNotContain(result.Package.ItemPropertySemantics, descriptor =>
            descriptor.Id == "legacy.semantic.to.replace");
        Assert.Equal(
            tracked.Select(descriptor => descriptor.Id),
            result.Package.ItemPropertySemantics.Select(descriptor => descriptor.Id));
    }

    [Fact]
    public void Augment_IdenticalInputs_ProducesDeterministicOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPath = workspace.WritePackage("active.json", ValidInputPackage.Value);
        var firstPath = workspace.PathFor("first.json");
        var secondPath = workspace.PathFor("second.json");

        var first = _service.Augment(CreateRequest(inputPath, firstPath));
        var second = _service.Augment(CreateRequest(inputPath, secondPath));

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.Success, first.ExitCode);
        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.Success, second.ExitCode);
        Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
        Assert.Equal(first.Sha256, second.Sha256);
    }

    [Theory]
    [InlineData(nameof(GameDataPackageSemanticAugmentationRequest.InputPackagePath), "--input-package")]
    [InlineData(nameof(GameDataPackageSemanticAugmentationRequest.ItemPropertySemanticsPath), "--item-property-semantics")]
    [InlineData(nameof(GameDataPackageSemanticAugmentationRequest.OutputPath), "--output")]
    [InlineData(nameof(GameDataPackageSemanticAugmentationRequest.DataVersion), "--data-version")]
    public void Augment_MissingOrBlankRequiredArgument_PreservesExistingOutput(
        string propertyName,
        string expectedSourceRecordId)
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPath = workspace.WritePackage("active.json", ValidInputPackage.Value);
        var outputPath = workspace.WriteText("candidate.json", "existing candidate");
        var request = CreateRequest(inputPath, outputPath);
        request = propertyName switch
        {
            nameof(GameDataPackageSemanticAugmentationRequest.InputPackagePath) => request with { InputPackagePath = " " },
            nameof(GameDataPackageSemanticAugmentationRequest.ItemPropertySemanticsPath) => request with { ItemPropertySemanticsPath = null },
            nameof(GameDataPackageSemanticAugmentationRequest.OutputPath) => request with { OutputPath = " " },
            nameof(GameDataPackageSemanticAugmentationRequest.DataVersion) => request with { DataVersion = null },
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName)),
        };

        var result = _service.Augment(request);

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal("existing candidate", File.ReadAllText(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.SourceRecordId == expectedSourceRecordId);
    }

    [Fact]
    public void Augment_SameResolvedInputAndOutputPath_RejectsWithoutChangingInput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPath = workspace.WritePackage("active.json", ValidInputPackage.Value);
        var inputBytes = File.ReadAllBytes(inputPath);

        var result = _service.Augment(CreateRequest(inputPath, Path.Combine(workspace.Root, ".", "active.json")));

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal(inputBytes, File.ReadAllBytes(inputPath));
    }

    [Theory]
    [InlineData("input-package")]
    [InlineData("semantic-input")]
    public void Augment_MissingInputFile_DoesNotCreateOutputOrUseFallback(string missingInput)
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPath = missingInput == "input-package"
            ? workspace.PathFor("missing-package.json")
            : workspace.WritePackage("active.json", ValidInputPackage.Value);
        var semanticPath = missingInput == "semantic-input"
            ? workspace.PathFor("missing-semantics.json")
            : RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath;
        var outputPath = workspace.PathFor("candidate.json");
        var request = CreateRequest(inputPath, outputPath) with
        {
            ItemPropertySemanticsPath = semanticPath,
        };

        var result = _service.Augment(request);

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.MissingInputFile, result.ExitCode);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void Augment_MalformedInputPackage_PreservesExistingOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPath = workspace.WriteText("active.json", "{ invalid json");

        var result = AugmentWithExistingOutput(workspace, inputPath);

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.InputPackageInvalid, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == GameDataPackageLoadDiagnosticCodes.JsonInvalid);
    }

    [Fact]
    public void Augment_InvalidInputPackage_PreservesExistingOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var root = JsonNode.Parse(GameDataPackageJson.Serialize(ValidInputPackage.Value))!;
        root["manifest"]!["dataVersion"] = " ";
        var inputPath = workspace.WriteText("active.json", root.ToJsonString());

        var result = AugmentWithExistingOutput(workspace, inputPath);

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.InputPackageInvalid, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GameDataPackageSemanticAugmentationDiagnosticCodes.InputPackageValidationFailed);
    }

    [Theory]
    [InlineData("malformed", ItemPropertySemanticImportDiagnosticCodes.JsonMalformed, null)]
    [InlineData("review-version-mismatch", ItemPropertySemanticImportDiagnosticCodes.ReviewVersionMismatch, null)]
    [InlineData("unknown-stat", ItemPropertySemanticImportDiagnosticCodes.ValidationFailed, GameDataValidationErrorCodes.ItemPropertySemanticStatIdUnknown)]
    [InlineData("duplicate-id", ItemPropertySemanticImportDiagnosticCodes.ValidationFailed, GameDataValidationErrorCodes.ItemPropertySemanticIdDuplicate)]
    [InlineData("duplicate-vector", ItemPropertySemanticImportDiagnosticCodes.ValidationFailed, GameDataValidationErrorCodes.ItemPropertySemanticStatVectorDuplicate)]
    public void Augment_InvalidSemanticInput_PreservesExistingOutput(
        string invalidInput,
        string expectedDiagnosticCode,
        string? expectedValidationCode)
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPath = workspace.WritePackage("active.json", ValidInputPackage.Value);
        var semanticPath = workspace.WriteText("semantics.json", CreateInvalidSemanticJson(invalidInput));
        var outputPath = workspace.WriteText("candidate.json", "existing candidate");
        var request = CreateRequest(inputPath, outputPath) with
        {
            ItemPropertySemanticsPath = semanticPath,
        };

        var result = _service.Augment(request);

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.SemanticImportFailure, result.ExitCode);
        Assert.Equal("existing candidate", File.ReadAllText(outputPath));
        var diagnostic = result.Diagnostics.First(diagnostic => diagnostic.Code == expectedDiagnosticCode);
        if (expectedValidationCode is not null)
        {
            Assert.Contains(expectedValidationCode, diagnostic.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Augment_DisplayedLocalSemanticReferencingNonLocalStat_PreservesExistingOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPackage = ValidInputPackage.Value with
        {
            Stats = ValidInputPackage.Value.Stats
                .Select(stat => stat.Id == "local_physical_damage_+%"
                    ? stat with { IsLocal = false }
                    : stat)
                .ToArray(),
        };
        Assert.True(GameDataPackageValidator.Validate(inputPackage).IsValid);
        var inputPath = workspace.WritePackage("active.json", inputPackage);

        var result = AugmentWithExistingOutput(workspace, inputPath);

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.SemanticImportFailure, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == ItemPropertySemanticImportDiagnosticCodes.ValidationFailed &&
            diagnostic.Message.Contains(
                GameDataValidationErrorCodes.ItemPropertySemanticUnconditionalStatNotLocal,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Augment_FinalValidationFailure_PreservesExistingOutput()
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPath = workspace.WritePackage("active.json", ValidInputPackage.Value);
        var outputPath = workspace.WriteText("candidate.json", "existing candidate");
        var service = new GameDataPackageSemanticAugmentationService(
            new ReviewedItemPropertySemanticImporter(),
            _ => new GameDataValidationResult(
            [
                new GameDataValidationError("test.final.invalid", "package", "Forced final validation failure."),
            ]));

        var result = service.Augment(CreateRequest(inputPath, outputPath));

        Assert.Equal(GameDataPackageSemanticAugmentationExitCode.FinalPackageValidationFailure, result.ExitCode);
        Assert.Equal("existing candidate", File.ReadAllText(outputPath));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GameDataPackageSemanticAugmentationDiagnosticCodes.FinalPackageValidationFailed);
    }

    private GameDataPackageSemanticAugmentationResult AugmentWithExistingOutput(
        TemporaryWorkspace workspace,
        string inputPath)
    {
        var outputPath = workspace.WriteText("candidate.json", "existing candidate");
        var result = _service.Augment(CreateRequest(inputPath, outputPath));
        Assert.Equal("existing candidate", File.ReadAllText(outputPath));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp", SearchOption.AllDirectories));
        return result;
    }

    private static GameDataPackageSemanticAugmentationRequest CreateRequest(
        string inputPath,
        string outputPath)
    {
        return new GameDataPackageSemanticAugmentationRequest
        {
            InputPackagePath = inputPath,
            ItemPropertySemanticsPath = RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath,
            OutputPath = outputPath,
            DataVersion = "candidate-version",
        };
    }

    private static GameDataPackage CreateValidInputPackage()
    {
        var baseItems = new RePoeBaseItemImporter()
            .Import(RePoeImportTestFixtures.ReducedBaseItemsPath)
            .ImportedRecords;
        var modifiers = new RePoeModifierImporter()
            .Import(RePoeImportTestFixtures.ReducedModsPath)
            .ImportedRecords;
        var stats = new RePoeStatsImporter()
            .Import(RePoeImportTestFixtures.ReducedStatsPath)
            .ImportedRecords;
        var translations = new RePoeStatTranslationsImporter()
            .Import(RePoeImportTestFixtures.ReducedStatTranslationsPath, stats)
            .ImportedRecords;
        var result = new GameDataPackageBuilder().CreatePackage(
            RePoeImportTestFixtures.CreateManifestWithRePoeSource(),
            baseItems,
            modifiers,
            stats,
            translations);
        return result.Package ?? throw new InvalidOperationException("Reduced package fixture must be valid.");
    }

    private static IReadOnlyList<ItemPropertySemanticDescriptor> ImportTrackedSemantics()
    {
        var result = new ReviewedItemPropertySemanticImporter().Import(
            RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath,
            ValidInputPackage.Value.Stats);
        return result.ImportedRecords;
    }

    private static void AssertPreservedPackageData(GameDataPackage input, GameDataPackage output)
    {
        AssertJsonEqual(input.ItemBases, output.ItemBases);
        AssertJsonEqual(input.Modifiers, output.Modifiers);
        AssertJsonEqual(input.Stats, output.Stats);
        AssertJsonEqual(input.StatTranslations, output.StatTranslations);
        AssertJsonEqual(input.Manifest.Sources, output.Manifest.Sources);

        var inputManifest = JsonSerializer.SerializeToNode(
            input.Manifest,
            GameDataPackageJson.CreateSerializerOptions())!.AsObject();
        var outputManifest = JsonSerializer.SerializeToNode(
            output.Manifest,
            GameDataPackageJson.CreateSerializerOptions())!.AsObject();
        foreach (var propertyName in new[]
                 {
                     "dataVersion",
                     "reviewedItemPropertySemantics",
                     "itemPropertySemanticAugmentation",
                 })
        {
            inputManifest.Remove(propertyName);
            outputManifest.Remove(propertyName);
        }

        Assert.True(JsonNode.DeepEquals(inputManifest, outputManifest));
    }

    private static void AssertJsonEqual<T>(T expected, T actual)
    {
        var options = GameDataPackageJson.CreateSerializerOptions();
        var expectedNode = JsonSerializer.SerializeToNode(expected, options);
        var actualNode = JsonSerializer.SerializeToNode(actual, options);
        Assert.True(JsonNode.DeepEquals(expectedNode, actualNode));
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
            case "duplicate-id":
                root["descriptors"]![1]!["id"] = root["descriptors"]![0]!["id"]!.GetValue<string>();
                break;
            case "duplicate-vector":
                root["descriptors"]![1]!["orderedStatIds"] = new JsonArray("local_physical_damage_+%");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidInput));
        }

        return root.ToJsonString();
    }

    private static string ComputeSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
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
            var root = Path.Combine(
                Path.GetTempPath(),
                "PoEnhance.DataImport.Tests",
                "augment-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TemporaryWorkspace(root);
        }

        public string PathFor(params string[] paths)
        {
            return Path.Combine([Root, .. paths]);
        }

        public string WritePackage(string relativePath, GameDataPackage package)
        {
            return WriteText(relativePath, GameDataPackageJson.Serialize(package));
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
