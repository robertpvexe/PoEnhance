using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class GameDataCatalogTests
{
    [Fact]
    public void FromPackage_InvalidPackage_Throws()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            Manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest() with
            {
                DataVersion = "",
            },
        };

        var exception = Assert.Throws<ArgumentException>(() => GameDataCatalog.FromPackage(package));

        Assert.Contains("valid package", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FindItemBasesByIdAndName_ReturnsExpectedRecords()
    {
        var catalog = CreateCatalog();

        var byId = Assert.Single(catalog.FindItemBasesById(" ITEM-BASE.GOLD-RING "));
        var byExactName = Assert.Single(catalog.FindItemBasesByExactName("Gold Ring"));

        Assert.Equal("Gold Ring", byId.Name);
        Assert.Same(byId, byExactName);
        Assert.Empty(catalog.FindItemBasesByExactName("gold ring"));
    }

    [Fact]
    public void FindItemBasesByNormalizedName_UsesCaseInsensitiveTrimmedName()
    {
        var catalog = CreateCatalog();

        var itemBase = Assert.Single(catalog.FindItemBasesByNormalizedName(" gold ring "));

        Assert.Equal("item-base.gold-ring", itemBase.Id);
    }

    [Fact]
    public void FindItemBasesByName_DuplicateNamesReturnAllRecordsInPackageOrder()
    {
        var duplicate = GameDataPackageFixtures.CreateDevelopmentPackage().ItemBases[1] with
        {
            Id = "item-base.gold-ring.duplicate-name",
            Name = "Gold Ring",
        };
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            ItemBases =
            [
                GameDataPackageFixtures.CreateDevelopmentPackage().ItemBases[0],
                duplicate,
            ],
        };
        var catalog = GameDataCatalog.FromPackage(package);

        var records = catalog.FindItemBasesByNormalizedName("gold ring");

        Assert.Collection(
            records,
            first => Assert.Equal("item-base.gold-ring", first.Id),
            second => Assert.Equal("item-base.gold-ring.duplicate-name", second.Id));
    }

    [Fact]
    public void ItemBases_ReturnsReadOnlyPackageOrderSnapshot()
    {
        var mutableItemBases = GameDataPackageFixtures.CreateDevelopmentPackage().ItemBases.ToList();
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            ItemBases = mutableItemBases,
        };
        var catalog = GameDataCatalog.FromPackage(package);

        mutableItemBases.Clear();
        var mutableView = Assert.IsAssignableFrom<ICollection<ItemBaseRecord>>(catalog.ItemBases);

        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(new ItemBaseRecord
        {
            Id = "item-base.injected",
            Name = "Injected",
            ItemClass = "Test",
        }));
        Assert.Collection(
            catalog.ItemBases,
            first => Assert.Equal("item-base.gold-ring", first.Id),
            second => Assert.Equal("item-base.granite-flask", second.Id));
    }

    [Fact]
    public void FindModifiersByIdGroupGenerationTypeAndStatId_ReturnsExpectedRecords()
    {
        var catalog = CreateCatalog();

        var byId = Assert.Single(catalog.FindModifiersById(" MOD.PREFIX.MAXIMUM-LIFE.T5 "));
        var byGroup = Assert.Single(catalog.FindModifiersByGroupId("mod-group.maximum-life"));
        var byStat = Assert.Single(catalog.FindModifiersByStatId("base_maximum_life"));
        var prefixes = catalog.FindModifiersByGenerationType(ModifierGenerationType.Prefix);

        Assert.Equal("mod.prefix.maximum-life.t5", byId.Id);
        Assert.Same(byId, byGroup);
        Assert.Same(byId, byStat);
        Assert.Collection(
            prefixes,
            first => Assert.Equal("mod.prefix.maximum-life.t5", first.Id),
            second => Assert.Equal("mod.prefix.armour-requirements.hybrid.t3", second.Id));
    }

    [Fact]
    public void FindModifiersByName_ReturnsExactAndNormalizedMatches()
    {
        var catalog = CreateCatalog();

        var byExactName = Assert.Single(catalog.FindModifiersByExactName(" Hale "));
        var byNormalizedName = Assert.Single(catalog.FindModifiersByNormalizedName(" hale "));

        Assert.Equal("mod.prefix.maximum-life.t5", byExactName.Id);
        Assert.Same(byExactName, byNormalizedName);
        Assert.Empty(catalog.FindModifiersByExactName("hale"));
    }

    [Fact]
    public void FindModifiersByNameAndGenerationType_ReturnsOnlyMatchingGenerationType()
    {
        var suffixDuplicateName = GameDataPackageFixtures.CreateDevelopmentPackage().Modifiers[2] with
        {
            Id = "mod.suffix.maximum-life.test",
            Name = "Hale",
            GenerationType = ModifierGenerationType.Suffix,
        };
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            Modifiers =
            [
                GameDataPackageFixtures.CreateDevelopmentPackage().Modifiers[1],
                suffixDuplicateName,
            ],
        };
        var catalog = GameDataCatalog.FromPackage(package);

        var prefixMatches = catalog.FindModifiersByNameAndGenerationType(" hale ", ModifierGenerationType.Prefix);
        var suffixMatches = catalog.FindModifiersByNameAndGenerationType("Hale", ModifierGenerationType.Suffix);

        Assert.Equal(["mod.prefix.maximum-life.t5"], prefixMatches.Select(modifier => modifier.Id));
        Assert.Equal(["mod.suffix.maximum-life.test"], suffixMatches.Select(modifier => modifier.Id));
    }

    [Fact]
    public void FindModifiersByName_DuplicateNamesReturnAllRecordsInPackageOrder()
    {
        var duplicate = GameDataPackageFixtures.CreateDevelopmentPackage().Modifiers[1] with
        {
            Id = "mod.prefix.maximum-life.duplicate-name",
            Tier = 6,
        };
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            Modifiers =
            [
                GameDataPackageFixtures.CreateDevelopmentPackage().Modifiers[1],
                duplicate,
            ],
        };
        var catalog = GameDataCatalog.FromPackage(package);

        var records = catalog.FindModifiersByNameAndGenerationType("hale", ModifierGenerationType.Prefix);

        Assert.Collection(
            records,
            first => Assert.Equal("mod.prefix.maximum-life.t5", first.Id),
            second => Assert.Equal("mod.prefix.maximum-life.duplicate-name", second.Id));
    }

    [Fact]
    public void ModifierLookupsReturnReadOnlyPackageOrderSnapshots()
    {
        var mutableModifiers = GameDataPackageFixtures.CreateDevelopmentPackage().Modifiers.ToList();
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            Modifiers = mutableModifiers,
        };
        var catalog = GameDataCatalog.FromPackage(package);

        mutableModifiers.Clear();
        var records = catalog.FindModifiersByNameAndGenerationType("Hale", ModifierGenerationType.Prefix);
        var mutableView = Assert.IsAssignableFrom<ICollection<ModifierDefinition>>(records);

        Assert.Single(records);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(new ModifierDefinition
        {
            Id = "mod.injected",
            Name = "Injected",
            GenerationType = ModifierGenerationType.Prefix,
        }));
        Assert.Equal(
            ["mod.implicit.gold-ring.item-rarity", "mod.prefix.maximum-life.t5", "mod.suffix.fire-resistance.t4", "mod.prefix.armour-requirements.hybrid.t3"],
            catalog.Modifiers.Select(modifier => modifier.Id));
    }

    [Fact]
    public void FindStatsById_ReturnsExpectedRecord()
    {
        var catalog = CreateCatalog();

        var stat = Assert.Single(catalog.FindStatsById(" BASE_MAXIMUM_LIFE "));

        Assert.Equal("base_maximum_life", stat.Id);
    }

    [Fact]
    public void FindStatTranslationsByIdAndStatId_ReturnsExpectedRecord()
    {
        var catalog = CreateCatalog();

        var byId = Assert.Single(catalog.FindStatTranslationsById(" TRANSLATION.BASE-MAXIMUM-LIFE "));
        var byStatId = Assert.Single(catalog.FindStatTranslationsByStatId("BASE_MAXIMUM_LIFE"));

        Assert.Equal("translation.base-maximum-life", byId.Id);
        Assert.Same(byId, byStatId);
    }

    [Fact]
    public void FindStatTranslationsByStatId_MultiStatTranslationsRemainOrdered()
    {
        var package = CreatePackageWithMultiStatTranslation();
        var catalog = GameDataCatalog.FromPackage(package);

        var translation = Assert.Single(catalog.FindStatTranslationsByStatId("base_fire_damage_resistance_%"));

        Assert.Equal("translation.life-and-fire", translation.Id);
        Assert.Equal(["base_maximum_life", "base_fire_damage_resistance_%"], translation.StatIds);
        Assert.Collection(
            translation.Variants,
            variant =>
            {
                Assert.Equal(["#|#", "+{0} to maximum Life", "+{1}% to Fire Resistance"], variant.FormatLines);
                Assert.Collection(
                    variant.Conditions,
                    first => Assert.Equal(0, first.Index),
                    second => Assert.Equal(1, second.Index));
                Assert.Collection(
                    variant.IndexHandlers,
                    first =>
                    {
                        Assert.Equal(0, first.Index);
                        Assert.Equal(["negate"], first.Handlers);
                    },
                    second =>
                    {
                        Assert.Equal(1, second.Index);
                        Assert.Equal(["divide_by_one"], second.Handlers);
                    });
            });
    }

    [Fact]
    public void FindStatTranslationsByStatIdGroup_UsesExactOrderedGroup()
    {
        var package = CreatePackageWithMultiStatTranslation();
        var catalog = GameDataCatalog.FromPackage(package);

        var exact = Assert.Single(catalog.FindStatTranslationsByStatIdGroup(
            [" BASE_MAXIMUM_LIFE ", "base_fire_damage_resistance_%"]));
        var reversed = catalog.FindStatTranslationsByStatIdGroup(
            ["base_fire_damage_resistance_%", "base_maximum_life"]);
        var partial = catalog.FindStatTranslationsByStatIdGroup(["base_maximum_life"]);

        Assert.Equal("translation.life-and-fire", exact.Id);
        Assert.Empty(reversed);
        Assert.Empty(partial);
    }

    [Fact]
    public void ReturnedCollectionsCannotMutateCatalogState()
    {
        var catalog = CreateCatalog();
        var records = catalog.FindItemBasesByNormalizedName("Gold Ring");
        var mutableView = Assert.IsAssignableFrom<ICollection<ItemBaseRecord>>(records);

        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(new ItemBaseRecord
        {
            Id = "item-base.injected",
            Name = "Injected",
            ItemClass = "Test",
        }));

        var freshLookup = catalog.FindItemBasesByNormalizedName("Gold Ring");
        var itemBase = Assert.Single(freshLookup);
        Assert.Equal("item-base.gold-ring", itemBase.Id);
    }

    [Fact]
    public void LookupsWithMissingOrWhitespaceKeysReturnEmptyReadOnlyCollections()
    {
        var catalog = CreateCatalog();

        var records = catalog.FindModifiersByStatId(" ");
        var mutableView = Assert.IsAssignableFrom<ICollection<ModifierDefinition>>(records);

        Assert.Empty(records);
        Assert.True(mutableView.IsReadOnly);
    }

    private static GameDataCatalog CreateCatalog()
    {
        return GameDataCatalog.FromPackage(GameDataPackageFixtures.CreateDevelopmentPackage());
    }

    private static GameDataPackage CreatePackageWithMultiStatTranslation()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        return package with
        {
            StatTranslations =
            [
                new StatTranslationDefinition
                {
                    Id = "translation.life-and-fire",
                    StatIds = ["base_maximum_life", "base_fire_damage_resistance_%"],
                    Language = "English",
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition
                                {
                                    Index = 0,
                                    MinValue = 1m,
                                },
                                new StatTranslationCondition
                                {
                                    Index = 1,
                                    MinValue = 1m,
                                },
                            ],
                            ValueFormats = ["#", "#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = ["negate"],
                                },
                                new StatTranslationIndexHandler
                                {
                                    Index = 1,
                                    Handlers = ["divide_by_one"],
                                },
                            ],
                            FormatLines =
                            [
                                "#|#",
                                "+{0} to maximum Life",
                                "+{1}% to Fire Resistance",
                            ],
                        },
                    ],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "translation.life-and-fire",
                        },
                    ],
                },
            ],
        };
    }
}
