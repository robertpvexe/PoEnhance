using PoEnhance.Core.Items.GameData;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ModifierEligibilityEvaluatorTests
{
    private readonly ModifierEligibilityEvaluator evaluator = new();

    [Fact]
    public void Evaluate_PositiveMatchingSpawnWeight_ReturnsEligible()
    {
        var result = evaluator.Evaluate(
            Modifier(SpawnWeight("ring", 1000)),
            Base(tags: ["ring"]));

        Assert.True(result.Evaluated);
        Assert.Equal(ModifierEligibilityOutcome.Eligible, result.Outcome);
        Assert.Equal(ModifierEligibilityDiagnosticCodes.ModifierEligibleForBase, result.ReasonCode);
        Assert.Equal("ring", result.MatchedTag);
    }

    [Fact]
    public void Evaluate_ZeroMatchingSpawnWeight_ReturnsIneligible()
    {
        var result = evaluator.Evaluate(
            Modifier(SpawnWeight("ring", 0)),
            Base(tags: ["ring"]));

        Assert.True(result.Evaluated);
        Assert.Equal(ModifierEligibilityOutcome.Ineligible, result.Outcome);
        Assert.Equal(ModifierEligibilityDiagnosticCodes.ModifierSpawnWeightZero, result.ReasonCode);
        Assert.Equal("ring", result.MatchedTag);
    }

    [Fact]
    public void Evaluate_NoMatchingItemBaseTag_ReturnsIneligible()
    {
        var result = evaluator.Evaluate(
            Modifier(SpawnWeight("amulet", 1000)),
            Base(tags: ["ring"]));

        Assert.True(result.Evaluated);
        Assert.Equal(ModifierEligibilityOutcome.Ineligible, result.Outcome);
        Assert.Equal(ModifierEligibilityDiagnosticCodes.ModifierNoMatchingBaseTag, result.ReasonCode);
    }

    [Fact]
    public void Evaluate_DomainMismatch_ReturnsIneligible()
    {
        var result = evaluator.Evaluate(
            Modifier("flask", SpawnWeight("default", 1000)),
            Base(domain: "item", tags: ["default"]));

        Assert.True(result.Evaluated);
        Assert.Equal(ModifierEligibilityOutcome.Ineligible, result.Outcome);
        Assert.Equal(ModifierEligibilityDiagnosticCodes.ModifierDomainMismatch, result.ReasonCode);
        Assert.Equal("flask", result.ModifierDomain);
        Assert.Equal("item", result.ItemBaseDomain);
    }

    [Fact]
    public void Evaluate_MissingRequiredData_ReturnsUnknown()
    {
        var missingBaseDomain = evaluator.Evaluate(
            Modifier(SpawnWeight("default", 1000)),
            Base(domain: null, tags: ["default"]));
        var missingSpawnWeights = evaluator.Evaluate(
            Modifier(),
            Base(tags: ["default"]));
        var missingBaseTags = evaluator.Evaluate(
            Modifier(SpawnWeight("default", 1000)),
            Base(tags: []));

        Assert.All(
            [missingBaseDomain, missingSpawnWeights, missingBaseTags],
            result =>
            {
                Assert.False(result.Evaluated);
                Assert.Equal(ModifierEligibilityOutcome.Unknown, result.Outcome);
                Assert.Equal(ModifierEligibilityDiagnosticCodes.ModifierEligibilityUnknown, result.ReasonCode);
            });
    }

    [Fact]
    public void Evaluate_UsesFirstMatchingSpawnWeightInSourceOrder()
    {
        var zeroFirst = evaluator.Evaluate(
            Modifier(
                SpawnWeight("ring", 0),
                SpawnWeight("default", 1000)),
            Base(tags: ["default", "ring"]));
        var positiveFirst = evaluator.Evaluate(
            Modifier(
                SpawnWeight("default", 1000),
                SpawnWeight("ring", 0)),
            Base(tags: ["default", "ring"]));

        Assert.Equal(ModifierEligibilityOutcome.Ineligible, zeroFirst.Outcome);
        Assert.Equal("ring", zeroFirst.MatchedTag);
        Assert.Equal(ModifierEligibilityOutcome.Eligible, positiveFirst.Outcome);
        Assert.Equal("default", positiveFirst.MatchedTag);
    }

    [Fact]
    public void Evaluate_DoesNotMutateInputs()
    {
        var modifier = Modifier(SpawnWeight("ring", 1000), SpawnWeight("default", 0));
        var itemBase = Base(tags: ["ring", "default"]);
        var originalWeights = modifier.SpawnWeights.ToArray();
        var originalTags = itemBase.Tags.ToArray();

        _ = evaluator.Evaluate(modifier, itemBase);

        Assert.Equal(originalWeights, modifier.SpawnWeights);
        Assert.Equal(originalTags, itemBase.Tags);
    }

    private static ModifierDefinition Modifier(
        params ModifierSpawnWeight[] spawnWeights)
    {
        return Modifier("item", spawnWeights);
    }

    private static ModifierDefinition Modifier(
        string? domain,
        params ModifierSpawnWeight[] spawnWeights)
    {
        return new ModifierDefinition
        {
            Id = "mod.test",
            GroupId = "group.test",
            Name = "Test",
            GenerationType = ModifierGenerationType.Prefix,
            Domain = domain,
            SpawnWeights = spawnWeights,
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "test_stat",
                    MinValue = 1m,
                    MaxValue = 2m,
                },
            ],
        };
    }

    private static ItemBaseRecord Base(
        string? domain = "item",
        IReadOnlyList<string>? tags = null)
    {
        return new ItemBaseRecord
        {
            Id = "base.test",
            Name = "Test Base",
            ItemClass = "Rings",
            Domain = domain,
            Tags = tags ?? ["default"],
        };
    }

    private static ModifierSpawnWeight SpawnWeight(string tag, int weight)
    {
        return new ModifierSpawnWeight
        {
            Tag = tag,
            Weight = weight,
        };
    }
}
