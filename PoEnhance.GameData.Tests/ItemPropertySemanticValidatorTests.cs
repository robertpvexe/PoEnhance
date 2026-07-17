using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class ItemPropertySemanticValidatorTests
{
    [Fact]
    public void Validate_ReviewedProviderNeutralDescriptors_Pass()
    {
        var result = GameDataPackageValidator.Validate(ItemPropertySemanticTestFixtures.CreatePackage());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_DuplicateDescriptorId_Fails()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var duplicate = package.ItemPropertySemantics[1] with
        {
            Id = package.ItemPropertySemantics[0].Id!.ToUpperInvariant(),
        };

        var result = Validate(package with
        {
            ItemPropertySemantics = [package.ItemPropertySemantics[0], duplicate],
        });

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticIdDuplicate);
    }

    [Fact]
    public void Validate_EmptyDescriptorIdAndStatVector_Fail()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var invalid = package.ItemPropertySemantics[0] with
        {
            Id = " ",
            OrderedStatIds = [],
        };

        var result = Validate(package with { ItemPropertySemantics = [invalid] });

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticIdRequired);
        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticStatIdsRequired);
    }

    [Fact]
    public void Validate_DuplicateExactOrderedVector_Fails()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var duplicate = package.ItemPropertySemantics[0] with { Id = "different.id" };

        var result = Validate(package with
        {
            ItemPropertySemantics = [package.ItemPropertySemantics[0], duplicate],
        });

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticStatVectorDuplicate);
    }

    [Fact]
    public void Validate_UnknownStatId_Fails()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var invalid = package.ItemPropertySemantics[0] with
        {
            OrderedStatIds = ["missing_stat"],
        };

        var result = Validate(package with { ItemPropertySemantics = [invalid] });

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticStatIdUnknown);
    }

    [Fact]
    public void Validate_MissingContributions_Fails()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var invalid = package.ItemPropertySemantics[0] with { Contributions = [] };

        var result = Validate(package with { ItemPropertySemantics = [invalid] });

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticContributionsRequired);
    }

    [Fact]
    public void Validate_MissingEvidence_Fails()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var invalid = package.ItemPropertySemantics[0] with { Evidence = [] };

        var result = Validate(package with { ItemPropertySemantics = [invalid] });

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticEvidenceRequired);
    }

    [Fact]
    public void Validate_UnconditionalDisplayedLocalWithNonLocalStat_Fails()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var nonLocalId = ItemPropertySemanticTestFixtures.IncreasedPhysicalVector[0];
        package = package with
        {
            Stats = package.Stats
                .Select(stat => string.Equals(stat.Id, nonLocalId, StringComparison.Ordinal)
                    ? stat with { IsLocal = false }
                    : stat)
                .ToArray(),
            ItemPropertySemantics = [package.ItemPropertySemantics[0]],
        };

        var result = Validate(package);

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticUnconditionalStatNotLocal);
    }

    [Fact]
    public void Validate_UnknownEnums_Fail()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var invalid = package.ItemPropertySemantics[0] with
        {
            Applicability = (ItemPropertyApplicability)999,
            Contributions =
            [
                new ItemPropertyContribution
                {
                    Operation = (ItemPropertyOperation)999,
                    Targets = [(ItemPropertyTarget)999],
                },
            ],
            Evidence =
            [
                ItemPropertySemanticTestFixtures.CreateEvidence() with
                {
                    Method = (ItemPropertySemanticEvidenceMethod)999,
                },
            ],
        };

        var result = Validate(package with { ItemPropertySemantics = [invalid] });

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticApplicabilityInvalid);
        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticContributionOperationInvalid);
        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticContributionTargetInvalid);
        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticEvidenceMethodInvalid);
    }

    [Fact]
    public void Validate_EmptyTargetSet_Fails()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var invalid = package.ItemPropertySemantics[0] with
        {
            Contributions =
            [
                new ItemPropertyContribution
                {
                    Operation = ItemPropertyOperation.IncreasedPercent,
                    Targets = [],
                },
            ],
        };

        var result = Validate(package with { ItemPropertySemantics = [invalid] });

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticContributionTargetsRequired);
    }

    [Fact]
    public void Validate_DuplicateStatsTargetsAndContributions_Fail()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var contribution = new ItemPropertyContribution
        {
            Operation = ItemPropertyOperation.Added,
            Targets = [ItemPropertyTarget.PhysicalDamage, ItemPropertyTarget.PhysicalDamage],
        };
        var invalid = package.ItemPropertySemantics[0] with
        {
            OrderedStatIds = [
                ItemPropertySemanticTestFixtures.IncreasedPhysicalVector[0],
                ItemPropertySemanticTestFixtures.IncreasedPhysicalVector[0],
            ],
            Contributions = [contribution, contribution],
        };

        var result = Validate(package with { ItemPropertySemantics = [invalid] });

        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticStatIdDuplicate);
        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticContributionTargetDuplicate);
        AssertHasError(result, GameDataValidationErrorCodes.ItemPropertySemanticContributionDuplicate);
    }

    private static GameDataValidationResult Validate(GameDataPackage package)
    {
        var result = GameDataPackageValidator.Validate(package);
        Assert.False(result.IsValid);
        return result;
    }

    private static void AssertHasError(GameDataValidationResult result, string code)
    {
        Assert.Contains(result.Errors, error => error.Code == code);
    }
}
