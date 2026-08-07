using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class GameDataPackageEligibilitySourceValidatorTests
{
    [Fact]
    public void Validate_OldPackageWithoutNewCatalogs_RemainsValid()
    {
        var result = GameDataPackageValidator.Validate(GameDataPackageFixtures.CreateDevelopmentPackage());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownClassTagAndEvidenceReferences_AreReported()
    {
        var package = CreateValidPackage();
        package = package with
        {
            ItemBases =
            [
                package.ItemBases[0] with { ItemClass = "Missing Class", Tags = ["missing_tag"] },
                package.ItemBases[1],
            ],
            BaseModifierEvidence = package.BaseModifierEvidence! with
            {
                Groups =
                [
                    package.BaseModifierEvidence.Groups[0] with
                    {
                        BaseItemIds = ["missing.base"],
                        Modifiers =
                        [
                            package.BaseModifierEvidence.Groups[0].Modifiers[0] with { ModifierId = "missing.modifier" },
                        ],
                    },
                ],
            },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.False(result.IsValid);
        AssertHas(result, GameDataValidationErrorCodes.ItemBaseItemClassUnknown);
        AssertHas(result, GameDataValidationErrorCodes.ItemBaseTagUnknown);
        AssertHas(result, GameDataValidationErrorCodes.BaseModifierEvidenceBaseIdUnknown);
        AssertHas(result, GameDataValidationErrorCodes.BaseModifierEvidenceModifierIdUnknown);
    }

    [Fact]
    public void Validate_DuplicateCatalogIdentitiesRelationshipsAndContradictoryCounts_AreReported()
    {
        var package = CreateValidPackage();
        package = package with
        {
            ItemClasses = [package.ItemClasses![0], package.ItemClasses[0]],
            Tags = [package.Tags![0], package.Tags[0]],
            BaseModifierEvidence = package.BaseModifierEvidence! with
            {
                RelationshipsRepresented = 99,
                Groups = [package.BaseModifierEvidence.Groups[0], package.BaseModifierEvidence.Groups[0]],
            },
        };

        var result = GameDataPackageValidator.Validate(package);

        AssertHas(result, GameDataValidationErrorCodes.ItemClassIdDuplicate);
        AssertHas(result, GameDataValidationErrorCodes.TagIdDuplicate);
        AssertHas(result, GameDataValidationErrorCodes.BaseModifierEvidenceBaseIdDuplicate);
        AssertHas(result, GameDataValidationErrorCodes.BaseModifierEvidenceRelationshipDuplicate);
        AssertHas(result, GameDataValidationErrorCodes.BaseModifierEvidenceCountContradiction);
    }

    private static GameDataPackage CreateValidPackage()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var source = new GameDataSourceReference { SourceId = "repoe", ExternalId = "fixture" };
        var classes = package.ItemBases.Select(item => item.ItemClass!).Distinct(StringComparer.Ordinal)
            .Select(value => new ItemClassDefinition { Id = value, Name = value, Sources = [source with { ExternalId = value }] }).ToArray();
        var tags = package.ItemBases.SelectMany(item => item.Tags)
            .Concat(package.Modifiers.SelectMany(item => item.Tags))
            .Concat(package.Modifiers.SelectMany(item => item.SpawnWeights.Select(weight => weight.Tag!)))
            .Distinct(StringComparer.Ordinal)
            .Select(value => new TagDefinition { Id = value, Sources = [source with { ExternalId = value }] }).ToArray();
        return package with
        {
            ItemClasses = classes,
            Tags = tags,
            BaseModifierEvidence = new BaseModifierSourceEvidence
            {
                Semantics = BaseModifierEvidenceSemantics.PositiveAndContextualOnly,
                Coverage = BaseModifierEvidenceCoverage.Partial,
                SourceBaseEntriesRead = 1,
                BaseEntriesRepresented = 1,
                SourceRelationshipsRead = 1,
                RelationshipsRepresented = 1,
                Groups =
                [
                    new BaseModifierSourceEvidenceGroup
                    {
                        BaseItemIds = [package.ItemBases[0].Id!],
                        Modifiers =
                        [
                            new BaseModifierSourceEvidenceEntry
                            {
                                ModifierId = package.Modifiers[1].Id,
                                ReportedWeight = 100,
                                SourceGenerationBucket = "prefix",
                            },
                        ],
                        Sources = [source with { ExternalId = "mods_by_base.json#/fixture" }],
                    },
                ],
                Sources = [source with { ExternalId = "mods_by_base.json" }],
            },
        };
    }

    private static void AssertHas(GameDataValidationResult result, string code) =>
        Assert.Contains(result.Errors, error => error.Code == code);
}
