using System.Text.Json.Nodes;
using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class ItemPropertySemanticJsonTests
{
    [Fact]
    public void SerializeAndDeserialize_SemanticsRoundTripWithOrderedVectorsAndEvidence()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();

        var json = GameDataPackageJson.Serialize(package);
        var roundTripped = GameDataPackageJson.Deserialize(json);

        Assert.NotNull(roundTripped);
        Assert.Contains("\"applicability\": \"unconditionalDisplayedLocal\"", json);
        Assert.Contains("\"method\": \"reviewedOverride\"", json);
        Assert.Contains("\"operation\": \"increasedPercent\"", json);
        Assert.DoesNotContain("\"applicability\": 1", json);
        Assert.Equal(6, roundTripped.ItemPropertySemantics.Count);

        var addedPhysical = roundTripped.ItemPropertySemantics[1];
        Assert.Equal(ItemPropertySemanticTestFixtures.AddedPhysicalVector, addedPhysical.OrderedStatIds);
        Assert.Equal(ItemPropertyApplicability.UnconditionalDisplayedLocal, addedPhysical.Applicability);
        var evidence = Assert.Single(addedPhysical.Evidence);
        Assert.Equal(ItemPropertySemanticEvidenceMethod.ReviewedOverride, evidence.Method);
        Assert.Equal(ItemPropertySemanticTestFixtures.ReviewVersion, evidence.ReviewVersion);
        Assert.Equal("repoe-item-property-semantic-source-audit:2026-07-16#initial-weapon-vectors", evidence.ReviewReference);
        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
    }

    [Fact]
    public void Deserialize_LegacyPackageWithoutSemantics_UsesEmptyCollection()
    {
        var jsonObject = JsonNode.Parse(GameDataPackageJson.Serialize(
            GameDataPackageFixtures.CreateDevelopmentPackage()))!.AsObject();
        Assert.True(jsonObject.Remove("itemPropertySemantics"));

        var package = GameDataPackageJson.Deserialize(jsonObject.ToJsonString());

        Assert.NotNull(package);
        Assert.NotNull(package.ItemPropertySemantics);
        Assert.Empty(package.ItemPropertySemantics);
        Assert.True(GameDataPackageValidator.Validate(package).IsValid);
    }

    [Fact]
    public void SerializeAndDeserialize_MultipleTargetsRemainOneContribution()
    {
        var package = ItemPropertySemanticTestFixtures.CreatePackage();
        var hybrid = package.ItemPropertySemantics[0] with
        {
            Id = "defence.armour-evasion.increased-percent.local",
            Contributions =
            [
                new ItemPropertyContribution
                {
                    Targets = [ItemPropertyTarget.Armour, ItemPropertyTarget.Evasion],
                    Operation = ItemPropertyOperation.IncreasedPercent,
                },
            ],
        };
        package = package with { ItemPropertySemantics = [hybrid] };

        var roundTripped = GameDataPackageJson.Deserialize(GameDataPackageJson.Serialize(package));

        Assert.NotNull(roundTripped);
        var contribution = Assert.Single(Assert.Single(roundTripped.ItemPropertySemantics).Contributions);
        Assert.Equal([ItemPropertyTarget.Armour, ItemPropertyTarget.Evasion], contribution.Targets);
        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
    }
}
