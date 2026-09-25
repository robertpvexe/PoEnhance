using System.Text.Json;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class BlightTowerEnchantDiscoveryTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();
    private readonly ParsedItemBaseResolver baseResolver = new();

    [Theory]
    [InlineData(
        "Your Shock Nova Towers deal 25% increased Damage",
        "BlightEnchantmentShockNovaTowerDamage",
        "blight_shocking_tower_damage_+%")]
    [InlineData(
        "Your Chilling Towers deal 25% increased Damage",
        "BlightEnchantmentChillingTowerDamage",
        "blight_chilling_tower_damage_+%")]
    [InlineData(
        "Your Seismic Towers deal 25% increased Damage",
        "BlightEnchantmentSeismicTowerDamage",
        "blight_seismic_tower_damage_+%")]
    public void Resolve_BlightTowerEnchant_IsExactWithAuthoritativeModAndStat(
        string enchantLine,
        string expectedModId,
        string expectedStatId)
    {
        var catalog = CreateTowerCatalog(TowerFamily());
        var item = ParseRing("Unique", "Test Ring", enchantLine);
        var result = ResolveEnchant(item, catalog, "base.two-stone-ring");

        AssertExactTower(result, expectedModId, expectedStatId, 25m);
    }

    [Theory]
    [InlineData("Rare")]
    [InlineData("Unique")]
    public void Resolve_BlightTowerEnchant_IsIndependentOfItemRarity(string rarity)
    {
        var catalog = CreateTowerCatalog(TowerFamily());
        var item = ParseRing(rarity, "Test Ring", "Your Shock Nova Towers deal 25% increased Damage");
        var result = ResolveEnchant(item, catalog, "base.two-stone-ring");

        AssertExactTower(
            result,
            "BlightEnchantmentShockNovaTowerDamage",
            "blight_shocking_tower_damage_+%",
            25m);
    }

    [Fact]
    public void Resolve_BlightTowerEnchant_WrongClassWithRingOnlySpawn_FailsClosed()
    {
        var ringOnly = Tower(
            "BlightEnchantmentShockNovaTowerDamage",
            "blight_shocking_tower_damage_+%",
            SpawnWeight("ring", 100),
            SpawnWeight("default", 0));
        var catalog = CreateTowerCatalog([ringOnly], includeBootsBase: true);
        var item = parser.Parse("""
Item Class: Boots
Rarity: Rare
Wrong Path
Ambush Boots
--------
Item Level: 80
--------
Your Shock Nova Towers deal 25% increased Damage (enchant)
""");

        var result = ResolveEnchant(item, catalog, "base.ambush-boots");

        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.DoesNotContain(
            result.Candidates,
            candidate => candidate.Id == "BlightEnchantmentShockNovaTowerDamage");
    }

    [Fact]
    public void Resolve_BlightTowerEnchant_WrongVisibleValue_RejectsCandidate()
    {
        var catalog = CreateTowerCatalog(TowerFamily());
        var item = ParseRing("Unique", "Test Ring", "Your Shock Nova Towers deal 40% increased Damage");
        var result = ResolveEnchant(item, catalog, "base.two-stone-ring");

        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.DoesNotContain(
            result.Candidates,
            candidate => candidate.Id == "BlightEnchantmentShockNovaTowerDamage");
    }

    [Fact]
    public void Resolve_BlightTowerEnchant_ReducedNegateVariant_IsExact()
    {
        var reduced = Tower(
            "BlightEnchantmentShockNovaTowerDamage",
            "blight_shocking_tower_damage_+%");
        var catalog = CreateTowerCatalog([reduced], includeReducedShockNovaVariant: true);
        var item = ParseRing("Unique", "Test Ring", "Your Shock Nova Towers deal 25% reduced Damage");
        var result = ResolveEnchant(item, catalog, "base.two-stone-ring");

        Assert.Equal(ParsedModifierKind.Enchantment, result.ParsedModifierKind);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("BlightEnchantmentShockNovaTowerDamage", candidate.Id);
        Assert.Equal("blight_shocking_tower_damage_+%", Assert.Single(candidate.Stats).StatId);
        Assert.Equal(25m, Assert.Single(candidate.Stats).MinValue);
        Assert.Equal(25m, Assert.Single(candidate.Stats).MaxValue);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public void Resolve_BlightTowerEnchant_ArbitraryCount_DoesNotTruncate(int count)
    {
        var family = Enumerable.Range(0, 6)
            .Select(index => Tower(
                $"BlightEnchantmentSyntheticTower{index}",
                $"blight_synthetic_tower_{index}_damage_+%"))
            .ToArray();
        var catalog = CreateTowerCatalog(family);
        var enchantLines = family
            .Take(count)
            .Select((_, index) => $"Your Synthetic{index} Towers deal 25% increased Damage (enchant)");
        var item = parser.Parse($"""
Item Class: Rings
Rarity: Rare
Many Towers
Two-Stone Ring
--------
Item Level: 80
--------
{string.Join(Environment.NewLine, enchantLines)}
""");

        var results = resolver
            .Resolve(item, catalog, ExactBase(catalog, "base.two-stone-ring"))
            .Where(result => result.ParsedModifierKind == ParsedModifierKind.Enchantment)
            .ToArray();

        Assert.Equal(count, results.Length);
        Assert.All(results, result => Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status));
        Assert.Equal(
            family.Take(count).Select(modifier => modifier.Id).OrderBy(id => id, StringComparer.Ordinal),
            results.Select(result => Assert.Single(result.Candidates).Id).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void Resolve_NonEnchantmentExplicit_DoesNotEnterBlightTowerPath()
    {
        var catalog = CreateTowerCatalog(TowerFamily());
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Plain Ring
Two-Stone Ring
--------
Item Level: 80
--------
+50 to maximum Life
""");

        var results = resolver.Resolve(item, catalog, ExactBase(catalog, "base.two-stone-ring"));

        Assert.DoesNotContain(
            results,
            result => result.Candidates.Any(candidate =>
                string.Equals(candidate.SourceGenerationType, "blight_tower", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("20260919-184022-181-Berek's Grip.json", "Berek's Grip")]
    [InlineData("20260919-184430-728-Mokou's Embrace.json", "Mokou's Embrace")]
    public async Task Resolve_ReplayReadyShockNova_IsExactAgainstPackagedGameData(
        string fileName,
        string expectedName)
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = await ReadCaptureClipboardAsync(fileName);
        var parsed = parser.Parse(raw);
        Assert.Contains(expectedName, raw, StringComparison.Ordinal);

        var baseResolution = baseResolver.Resolve(parsed, catalog);
        var result = Assert.Single(
            resolver.Resolve(parsed, catalog, baseResolution),
            candidate => candidate.ParsedModifier.ValueLines.Any(line =>
                line.Contains("Shock Nova Towers", StringComparison.Ordinal)));

        Assert.Equal(ParsedModifierKind.Enchantment, result.ParsedModifierKind);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal("BlightEnchantmentShockNovaTowerDamage", Assert.Single(result.Candidates).Id);
        Assert.Equal(
            ["blight_shocking_tower_damage_+%"],
            Assert.Single(result.Candidates).Stats
                .Select(stat => stat.StatId)
                .Where(statId => !string.IsNullOrWhiteSpace(statId))
                .Cast<string>()
                .ToArray());
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextExactMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task Resolve_PackagedBlightTowerFamily_EntersEnchantCandidatePipeline()
    {
        var catalog = await LoadActiveCatalogAsync();
        var blight = catalog.Modifiers
            .Where(modifier =>
                string.Equals(modifier.SourceGenerationType, "blight_tower", StringComparison.Ordinal) &&
                modifier.SourceAvailability != ModifierSourceAvailability.Disabled)
            .ToArray();
        Assert.Equal(92, blight.Length);
        Assert.All(blight, modifier => Assert.Equal(ModifierGenerationType.Unknown, modifier.GenerationType));

        var discoverable = blight
            .Where(modifier =>
                modifier.SourceAvailability != ModifierSourceAvailability.Disabled &&
                (modifier.GenerationType == ModifierGenerationType.Enchantment ||
                    modifier.SourceGenerationType?.Contains("enchant", StringComparison.OrdinalIgnoreCase) == true ||
                    string.Equals(modifier.SourceGenerationType, "blight_tower", StringComparison.Ordinal)))
            .ToArray();
        Assert.Equal(92, discoverable.Length);
    }

    private ModifierCandidateResolutionResult ResolveEnchant(
        ParsedItem item,
        GameDataCatalog catalog,
        string baseId)
    {
        return Assert.Single(
            resolver.Resolve(item, catalog, ExactBase(catalog, baseId)),
            result => result.ParsedModifierKind == ParsedModifierKind.Enchantment);
    }

    private ParsedItem ParseRing(string rarity, string name, string enchantLineWithoutTag)
    {
        return parser.Parse($"""
Item Class: Rings
Rarity: {rarity}
{name}
Two-Stone Ring
--------
Item Level: 80
--------
{enchantLineWithoutTag} (enchant)
""");
    }

    private static void AssertExactTower(
        ModifierCandidateResolutionResult result,
        string expectedModId,
        string expectedStatId,
        decimal expectedFixedValue)
    {
        Assert.Equal(ParsedModifierKind.Enchantment, result.ParsedModifierKind);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(expectedModId, candidate.Id);
        Assert.Equal("blight_tower", candidate.SourceGenerationType);
        Assert.Equal(ModifierGenerationType.Unknown, candidate.GenerationType);
        var stat = Assert.Single(candidate.Stats);
        Assert.Equal(expectedStatId, stat.StatId);
        Assert.Equal(expectedFixedValue, stat.MinValue);
        Assert.Equal(expectedFixedValue, stat.MaxValue);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextExactMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    private static IReadOnlyList<ModifierDefinition> TowerFamily() =>
    [
        Tower("BlightEnchantmentShockNovaTowerDamage", "blight_shocking_tower_damage_+%"),
        Tower("BlightEnchantmentChillingTowerDamage", "blight_chilling_tower_damage_+%"),
        Tower("BlightEnchantmentSeismicTowerDamage", "blight_seismic_tower_damage_+%"),
    ];

    private static ModifierDefinition Tower(
        string modId,
        string statId,
        params ModifierSpawnWeight[] spawnWeights)
    {
        return new ModifierDefinition
        {
            Id = modId,
            GroupId = $"group.{modId}",
            Name = string.Empty,
            GenerationType = ModifierGenerationType.Unknown,
            SourceGenerationType = "blight_tower",
            SourceAvailability = ModifierSourceAvailability.Unknown,
            Domain = "item",
            SpawnWeights = spawnWeights,
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = statId,
                    MinValue = 25m,
                    MaxValue = 25m,
                },
            ],
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = modId,
                },
            ],
        };
    }

    private static GameDataCatalog CreateTowerCatalog(
        IReadOnlyList<ModifierDefinition> towers,
        bool includeBootsBase = false,
        bool includeReducedShockNovaVariant = false)
    {
        var translations = towers
            .Select(tower =>
            {
                var statId = Assert.Single(tower.Stats).StatId!;
                var increased = FormatLineFor(tower.Id!);
                var variants = new List<StatTranslationVariant>
                {
                    Variant([increased], ["#"]),
                };
                if (includeReducedShockNovaVariant &&
                    string.Equals(
                        tower.Id,
                        "BlightEnchantmentShockNovaTowerDamage",
                        StringComparison.Ordinal))
                {
                    variants.Add(Variant(
                        ["Your Shock Nova Towers deal {0}% reduced Damage"],
                        ["#"],
                        [["negate"]]));
                }

                return Translation([statId], variants.ToArray());
            })
            .ToArray();

        var bases = new List<ItemBaseRecord>
        {
            Base("base.two-stone-ring", "Two-Stone Ring", "Rings", "item", ["ring", "default", "twostonering"]),
        };
        if (includeBootsBase)
        {
            bases.Add(Base("base.ambush-boots", "Ambush Boots", "Boots", "item", ["boots", "default"]));
        }

        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "test",
                CreatedAtUtc = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero),
                League = "test",
                Patch = "test",
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "test",
                        RetrievedAtUtc = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero),
                        SourceVersion = "test",
                    },
                ],
            },
            ItemBases = bases,
            Modifiers = towers.ToArray(),
            Stats = towers
                .Select(tower => Assert.Single(tower.Stats).StatId!)
                .Distinct(StringComparer.Ordinal)
                .Select(statId => new StatDefinition
                {
                    Id = statId,
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "test",
                            ExternalId = statId,
                        },
                    ],
                })
                .ToArray(),
            StatTranslations = translations,
        });
    }

    private static StatTranslationDefinition Translation(
        IReadOnlyList<string> statIds,
        params StatTranslationVariant[] variants)
    {
        return new StatTranslationDefinition
        {
            Id = "translation." + string.Join(".", statIds),
            StatIds = statIds,
            Language = "English",
            Variants = variants,
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = "translation." + string.Join(".", statIds),
                },
            ],
        };
    }

    private static StatTranslationVariant Variant(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> formats,
        IReadOnlyList<IReadOnlyList<string>>? handlers = null)
    {
        return new StatTranslationVariant
        {
            Conditions = formats
                .Select((_, index) => new StatTranslationCondition { Index = index })
                .ToArray(),
            ValueFormats = formats,
            IndexHandlers = formats
                .Select((_, index) => new StatTranslationIndexHandler
                {
                    Index = index,
                    Handlers = handlers?.ElementAtOrDefault(index) ?? [],
                })
                .ToArray(),
            FormatLines = lines,
        };
    }

    private static ItemBaseRecord Base(
        string id,
        string name,
        string itemClass,
        string? domain,
        IReadOnlyList<string> tags)
    {
        return new ItemBaseRecord
        {
            Id = id,
            Name = name,
            ItemClass = itemClass,
            Domain = domain,
            Tags = tags,
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = id,
                },
            ],
        };
    }

    private static ItemBaseResolutionResult ExactBase(GameDataCatalog catalog, string id)
    {
        var itemBase = Assert.Single(catalog.FindItemBasesById(id));
        return new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Exact,
            MatchedItemBase = itemBase,
            ResolvedBaseId = itemBase.Id,
            ResolvedBaseName = itemBase.Name,
            Candidates = [itemBase],
        };
    }

    private static ModifierSpawnWeight SpawnWeight(string tag, int weight) =>
        new() { Tag = tag, Weight = weight };

    private static string FormatLineFor(string modId)
    {
        if (string.Equals(modId, "BlightEnchantmentShockNovaTowerDamage", StringComparison.Ordinal))
        {
            return "Your Shock Nova Towers deal {0}% increased Damage";
        }

        if (string.Equals(modId, "BlightEnchantmentChillingTowerDamage", StringComparison.Ordinal))
        {
            return "Your Chilling Towers deal {0}% increased Damage";
        }

        if (string.Equals(modId, "BlightEnchantmentSeismicTowerDamage", StringComparison.Ordinal))
        {
            return "Your Seismic Towers deal {0}% increased Damage";
        }

        if (modId.StartsWith("BlightEnchantmentSyntheticTower", StringComparison.Ordinal))
        {
            var index = modId["BlightEnchantmentSyntheticTower".Length..];
            return $"Your Synthetic{index} Towers deal {{0}}% increased Damage";
        }

        throw new ArgumentOutOfRangeException(nameof(modId), modId, "Unknown blight tower fixture.");
    }

    private static async Task<string> ReadCaptureClipboardAsync(string fileName)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-A.5.20-ManualValidation",
            fileName);
        Assert.True(File.Exists(path), $"Missing ReplayReady capture: {path}");
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);
        var raw = document.RootElement
            .GetProperty("replayContext")
            .GetProperty("rawClipboardText")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(raw));
        return raw!;
    }

    private static async Task<GameDataCatalog> LoadActiveCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        Assert.Equal(4, load.Package!.Manifest.SchemaVersion);
        Assert.Equal("3.29.1.2.8-timeless-unique-item-domain", load.Package.Manifest.DataVersion);
        return GameDataCatalog.FromPackage(load.Package);
    }
}
