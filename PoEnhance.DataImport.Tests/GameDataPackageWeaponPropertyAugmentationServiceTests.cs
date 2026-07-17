using System.Security.Cryptography;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class GameDataPackageWeaponPropertyAugmentationServiceTests
{
    private readonly GameDataPackageWeaponPropertyAugmentationService service = new();

    [Fact]
    public async Task Augment_ImportsExactWeaponFactsPreservesIdentitiesOrderAndUnrelatedPackageData()
    {
        using var workspace = TemporaryWorkspace.Create();
        var input = InputPackage();
        var inputPath = workspace.WritePackage("active.json", input);
        var baseItemsPath = workspace.WriteText("base_items.json", BaseItemsJson);
        var outputPath = workspace.PathFor("candidate.json");
        var inputBytes = File.ReadAllBytes(inputPath);

        var result = service.Augment(new GameDataPackageWeaponPropertyAugmentationRequest
        {
            InputPackagePath = inputPath,
            BaseItemsPath = baseItemsPath,
            OutputPath = outputPath,
            DataVersion = "q20-candidate-test",
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("q20-candidate-test", result.Package!.Manifest.DataVersion);
        Assert.Equivalent(
            input.Manifest with { DataVersion = "q20-candidate-test" },
            result.Package.Manifest,
            strict: true);
        Assert.Equal(input.ItemBases.Select(itemBase => itemBase.Id),
            result.Package.ItemBases.Select(itemBase => itemBase.Id));
        Assert.Equal(input.ItemBases.Select(itemBase => itemBase.Name),
            result.Package.ItemBases.Select(itemBase => itemBase.Name));
        Assert.Equivalent(
            input.ItemBases.Select(itemBase => itemBase.Tags),
            result.Package.ItemBases.Select(itemBase => itemBase.Tags),
            strict: true);
        Assert.Equivalent(input.Modifiers, result.Package.Modifiers, strict: true);
        Assert.Equivalent(input.Stats, result.Package.Stats, strict: true);
        Assert.Equivalent(input.StatTranslations, result.Package.StatTranslations, strict: true);
        Assert.Equivalent(input.ItemPropertySemantics, result.Package.ItemPropertySemantics, strict: true);
        Assert.Equal(inputBytes, File.ReadAllBytes(inputPath));

        var reaver = Assert.Single(result.Package.ItemBases, itemBase => itemBase.Name == "Reaver Axe");
        var properties = Assert.IsType<ItemBaseWeaponProperties>(reaver.WeaponProperties);
        Assert.Equal(38, properties.PhysicalDamageMinimum);
        Assert.Equal(114, properties.PhysicalDamageMaximum);
        Assert.Equal(833, properties.AttackTimeMilliseconds);
        Assert.Equal(5m, properties.CriticalStrikeChancePercent);
        var source = Assert.Single(properties.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal(reaver.Id, source.ExternalId);

        Assert.Equal(2, result.ItemBaseCount);
        Assert.Equal(1, result.ItemBasesWithWeaponProperties);
        Assert.Equal(1, result.ItemBasesWithCompletePhysicalRange);
        Assert.Equal(1, result.ItemBasesWithAttackTime);
        Assert.Equal(1, result.ItemBasesWithCriticalStrikeChance);
        Assert.Empty(result.MissingCompleteWeaponPropertiesByClass);
        Assert.Equal(inputBytes.LongLength, result.InputPackageSizeBytes);
        Assert.Equal(Sha256(inputBytes), result.InputPackageSha256);
        Assert.Equal(Sha256(File.ReadAllBytes(baseItemsPath)), result.BaseItemsSha256);
        Assert.Equal(Sha256(File.ReadAllBytes(outputPath)), result.OutputSha256);
        Assert.Equal(new FileInfo(outputPath).Length, result.OutputSizeBytes);

        var loaded = await GameDataPackageLoader.LoadFromFileAsync(outputPath);
        Assert.True(loaded.IsSuccess);
        Assert.Empty(loaded.ValidationErrors);
    }

    [Fact]
    public void Augment_IdenticalInputsProduceDeterministicCandidateBytesAndHash()
    {
        using var workspace = TemporaryWorkspace.Create();
        var inputPath = workspace.WritePackage("active.json", InputPackage());
        var baseItemsPath = workspace.WriteText("base_items.json", BaseItemsJson);

        var first = service.Augment(Request(inputPath, baseItemsPath, workspace.PathFor("first.json")));
        var second = service.Augment(Request(inputPath, baseItemsPath, workspace.PathFor("second.json")));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(File.ReadAllBytes(workspace.PathFor("first.json")),
            File.ReadAllBytes(workspace.PathFor("second.json")));
        Assert.Equal(first.OutputSha256, second.OutputSha256);
    }

    [Fact]
    public void Augment_DoesNotSynthesizeMissingWeaponFactsFromNames()
    {
        using var workspace = TemporaryWorkspace.Create();
        var input = InputPackage() with
        {
            ItemBases =
            [
                InputPackage().ItemBases[0] with { Id = "Metadata/Items/Test/UnknownReaverName" },
            ],
        };
        var inputPath = workspace.WritePackage("active.json", input);
        var baseItemsPath = workspace.WriteText("base_items.json", BaseItemsJson);

        var result = service.Augment(Request(inputPath, baseItemsPath, workspace.PathFor("candidate.json")));

        Assert.True(result.IsSuccess);
        Assert.Null(Assert.Single(result.Package!.ItemBases).WeaponProperties);
        Assert.Equal(new Dictionary<string, int> { ["One Hand Axe"] = 1 },
            result.MissingCompleteWeaponPropertiesByClass);
    }

    private static GameDataPackageWeaponPropertyAugmentationRequest Request(
        string inputPath,
        string baseItemsPath,
        string outputPath) => new()
    {
        InputPackagePath = inputPath,
        BaseItemsPath = baseItemsPath,
        OutputPath = outputPath,
        DataVersion = "q20-candidate-test",
    };

    private static GameDataPackage InputPackage()
    {
        var source = new GameDataSourceReference
        {
            SourceId = "repoe",
            ExternalId = "source-record",
        };
        return new GameDataPackage
        {
            Manifest = RePoeImportTestFixtures.CreateManifestWithRePoeSource() with
            {
                DataVersion = "active-test",
            },
            ItemBases =
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Weapons/OneHandWeapons/OneHandAxes/OneHandAxe18",
                    Name = "Reaver Axe",
                    ItemClass = "One Hand Axe",
                    Domain = "item",
                    Tags = ["default", "weapon", "one_hand_weapon"],
                    Sources = [source],
                },
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Rings/Ring4",
                    Name = "Gold Ring",
                    ItemClass = "Ring",
                    Domain = "item",
                    Tags = ["default", "ring"],
                    Sources = [source with { ExternalId = "Metadata/Items/Rings/Ring4" }],
                },
            ],
        };
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private const string BaseItemsJson = """
        {
          "Metadata/Items/Weapons/OneHandWeapons/OneHandAxes/OneHandAxe18": {
            "name": "Reaver Axe",
            "item_class": "One Hand Axe",
            "domain": "item",
            "requirements": { "level": 61 },
            "tags": ["default", "weapon", "one_hand_weapon"],
            "properties": {
              "physical_damage_min": 38,
              "physical_damage_max": 114,
              "attack_time": 833,
              "critical_strike_chance": 500
            }
          },
          "Metadata/Items/Rings/Ring4": {
            "name": "Gold Ring",
            "item_class": "Ring",
            "domain": "item",
            "requirements": null,
            "tags": ["default", "ring"]
          },
          "Metadata/Items/Weapons/Test/NotInPackage": {
            "name": "Not In Package",
            "item_class": "One Hand Axe",
            "domain": "item",
            "requirements": null,
            "tags": ["weapon"],
            "properties": {
              "physical_damage_min": 1,
              "physical_damage_max": 2,
              "attack_time": 1000,
              "critical_strike_chance": 500
            }
          }
        }
        """;

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string root) => Root = root;

        public string Root { get; }

        public static TemporaryWorkspace Create() => new(Directory.CreateTempSubdirectory(
            "poenhance-q20-package-tests-").FullName);

        public string PathFor(string fileName) => Path.Combine(Root, fileName);

        public string WriteText(string fileName, string text)
        {
            var path = PathFor(fileName);
            File.WriteAllText(path, text);
            return path;
        }

        public string WritePackage(string fileName, GameDataPackage package) =>
            WriteText(fileName, GameDataPackageJson.Serialize(package));

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
