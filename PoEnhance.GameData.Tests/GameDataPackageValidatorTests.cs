using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class GameDataPackageValidatorTests
{
    [Fact]
    public void Validate_DevelopmentPackage_ReturnsValidResult()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();

        var result = GameDataPackageValidator.Validate(package);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_NullTopLevelCollections_ReturnsRequiredErrors()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            ItemBases = null!,
            Modifiers = null!,
            Stats = null!,
            StatTranslations = null!,
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.PackageItemBasesRequired);
        AssertHasError(result, GameDataValidationErrorCodes.PackageModifiersRequired);
        AssertHasError(result, GameDataValidationErrorCodes.PackageStatsRequired);
        AssertHasError(result, GameDataValidationErrorCodes.PackageStatTranslationsRequired);
    }

    [Fact]
    public void Validate_DuplicateItemBaseIds_ReturnsDuplicateError()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var duplicate = package.ItemBases[1] with
        {
            Id = "ITEM-BASE.GOLD-RING",
        };

        var invalidPackage = package with
        {
            ItemBases = [package.ItemBases[0], duplicate],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.ItemBaseIdDuplicate);
    }

    [Fact]
    public void Validate_DuplicateModifierIds_ReturnsDuplicateError()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var duplicate = package.Modifiers[1] with
        {
            Id = "MOD.IMPLICIT.GOLD-RING.ITEM-RARITY",
        };

        var invalidPackage = package with
        {
            Modifiers = [package.Modifiers[0], duplicate],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierIdDuplicate);
    }

    [Fact]
    public void Validate_ItemBaseImplicitModifierIdUnknown_ReturnsUnknownError()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidItemBase = package.ItemBases[0] with
        {
            ImplicitModifierIds = ["missing.implicit.modifier"],
        };

        var invalidPackage = package with
        {
            ItemBases = [invalidItemBase],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.ItemBaseImplicitModifierIdUnknown);
    }

    [Fact]
    public void Validate_UnknownSourceReferenceId_ReturnsUnknownSourceError()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidItemBase = package.ItemBases[0] with
        {
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "wiki",
                    ExternalId = "Gold Ring",
                },
            ],
        };

        var invalidPackage = package with
        {
            ItemBases = [invalidItemBase],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.SourceReferenceSourceIdUnknown);
    }

    [Fact]
    public void Validate_MissingSourceReferenceId_ReturnsSourceIdRequiredError()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidItemBase = package.ItemBases[0] with
        {
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = " ",
                    ExternalId = "Gold Ring",
                },
            ],
        };

        var invalidPackage = package with
        {
            ItemBases = [invalidItemBase],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.SourceReferenceSourceIdRequired);
    }

    [Fact]
    public void Validate_DuplicateSourceReferencesOnOneRecord_ReturnsDuplicateError()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidItemBase = package.ItemBases[0] with
        {
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "repoe",
                    ExternalId = "same-record",
                    ExternalUri = "https://example.test/source",
                },
                new GameDataSourceReference
                {
                    SourceId = "RePoE",
                    ExternalId = "same-record",
                    ExternalUri = "https://example.test/source",
                },
            ],
        };

        var invalidPackage = package with
        {
            ItemBases = [invalidItemBase],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.SourceReferenceDuplicate);
    }

    [Fact]
    public void Validate_InvalidItemBaseFields_ReturnsExpectedErrors()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidItemBase = package.ItemBases[0] with
        {
            Id = "",
            Name = " ",
            ItemClass = null,
            RequiredLevel = -1,
            Tags = ["ring", "Ring", ""],
        };

        var invalidPackage = package with
        {
            ItemBases = [invalidItemBase],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.ItemBaseIdRequired);
        AssertHasError(result, GameDataValidationErrorCodes.ItemBaseNameRequired);
        AssertHasError(result, GameDataValidationErrorCodes.ItemBaseItemClassRequired);
        AssertHasError(result, GameDataValidationErrorCodes.ItemBaseRequiredLevelNegative);
        AssertHasError(result, GameDataValidationErrorCodes.ItemBaseTagDuplicate);
        AssertHasError(result, GameDataValidationErrorCodes.ItemBaseTagRequired);
    }

    [Fact]
    public void Validate_InvalidModifierFields_ReturnsExpectedErrors()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidModifier = package.Modifiers[1] with
        {
            Id = "",
            GroupId = " ",
            Tier = 0,
            RequiredLevel = -1,
            Tags = ["life", "Life", ""],
        };

        var invalidPackage = package with
        {
            Modifiers = [invalidModifier],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierIdRequired);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierGroupIdRequired);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierTierInvalid);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierRequiredLevelNegative);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierTagDuplicate);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierTagRequired);
    }

    [Fact]
    public void Validate_EmptyModifierStats_ReturnsStatsRequiredError()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidModifier = package.Modifiers[1] with
        {
            Stats = [],
        };

        var invalidPackage = package with
        {
            Modifiers = [invalidModifier],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierStatsRequired);
    }

    [Fact]
    public void Validate_InvalidModifierStatsAndSpawnWeights_ReturnsExpectedErrors()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidModifier = package.Modifiers[1] with
        {
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "",
                    MinValue = 10m,
                    MaxValue = 5m,
                },
                new ModifierStat
                {
                    Index = 0,
                    StatId = "base_maximum_life",
                    MinValue = 1m,
                    MaxValue = 2m,
                },
                new ModifierStat
                {
                    Index = -1,
                    StatId = "local_missing_index_test",
                },
            ],
            SpawnWeights =
            [
                new ModifierSpawnWeight
                {
                    Tag = "",
                    Weight = -1,
                },
                new ModifierSpawnWeight
                {
                    Tag = "ring",
                    Weight = 1,
                },
                new ModifierSpawnWeight
                {
                    Tag = "RING",
                    Weight = 2,
                },
            ],
        };

        var invalidPackage = package with
        {
            Modifiers = [invalidModifier],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierStatIdRequired);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierStatRangeInvalid);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierStatIndexDuplicate);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierStatIndexNegative);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierSpawnWeightTagRequired);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierSpawnWeightWeightNegative);
        AssertHasError(result, GameDataValidationErrorCodes.ModifierSpawnWeightTagDuplicate);
    }

    [Fact]
    public void Validate_InvalidStats_ReturnsExpectedErrors()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidPackage = package with
        {
            Stats =
            [
                new StatDefinition
                {
                    Id = "",
                    MainHandAliasId = " ",
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "missing-id",
                        },
                    ],
                },
                new StatDefinition
                {
                    Id = "base_maximum_life",
                    MainHandAliasId = "base_maximum_life",
                },
                new StatDefinition
                {
                    Id = "BASE_MAXIMUM_LIFE",
                    OffHandAliasId = "missing_alias_target",
                },
            ],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.StatIdRequired);
        AssertHasError(result, GameDataValidationErrorCodes.StatMainHandAliasIdRequired);
        AssertHasError(result, GameDataValidationErrorCodes.StatIdDuplicate);
        AssertHasError(result, GameDataValidationErrorCodes.StatAliasIdSelfReference);
        AssertHasError(result, GameDataValidationErrorCodes.StatAliasIdUnknown);
    }

    [Fact]
    public void Validate_InvalidStatTranslations_ReturnsExpectedErrors()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var invalidPackage = package with
        {
            StatTranslations =
            [
                new StatTranslationDefinition
                {
                    Id = "",
                    StatIds = ["base_maximum_life", "BASE_MAXIMUM_LIFE", "missing_stat", ""],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition
                                {
                                    Index = -1,
                                },
                                new StatTranslationCondition
                                {
                                    Index = 0,
                                    MinValue = 2m,
                                    MaxValue = 1m,
                                },
                            ],
                            ValueFormats = ["#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 99,
                                    Handlers = [""],
                                },
                            ],
                            FormatLines = [""],
                        },
                    ],
                },
            ],
        };

        var result = GameDataPackageValidator.Validate(invalidPackage);

        Assert.False(result.IsValid);
        AssertHasError(result, GameDataValidationErrorCodes.StatTranslationIdRequired);
        AssertHasError(result, GameDataValidationErrorCodes.StatTranslationStatIdDuplicate);
        AssertHasError(result, GameDataValidationErrorCodes.StatTranslationStatIdUnknown);
        AssertHasError(result, GameDataValidationErrorCodes.StatTranslationStatIdRequired);
        AssertHasError(result, GameDataValidationErrorCodes.StatTranslationFormatLineRequired);
        AssertHasError(result, GameDataValidationErrorCodes.StatTranslationConditionIndexInvalid);
        AssertHasError(result, GameDataValidationErrorCodes.StatTranslationConditionRangeInvalid);
        AssertHasError(result, GameDataValidationErrorCodes.StatTranslationIndexHandlerIndexInvalid);
        AssertHasError(result, GameDataValidationErrorCodes.StatTranslationIndexHandlerValueRequired);
    }

    private static void AssertHasError(GameDataValidationResult result, string code)
    {
        Assert.Contains(result.Errors, error => error.Code == code);
    }
}
