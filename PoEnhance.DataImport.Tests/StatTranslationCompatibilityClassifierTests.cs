using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class StatTranslationCompatibilityClassifierTests
{
    [Fact]
    public void TextOnlyChange_IsMechanicallyEquivalentAndKeepsOneMechanicalIdentity()
    {
        var current = Translation("{0}% increased Damage");
        var historical = Translation("{0}% increased Global Damage");

        var result = StatTranslationCompatibilityClassifier.Compare(current, historical, usageCount: 2, specialOnly: false);

        Assert.Equal(
            StatTranslationCompatibilityClassification.MechanicallyEquivalentRendering,
            result.Classification);
        Assert.Equal(result.CurrentMechanicalSignature, result.HistoricalMechanicalSignature);
        Assert.NotEqual(result.CurrentRenderingSignature, result.HistoricalRenderingSignature);
    }

    [Fact]
    public void SameRenderedTextWithDifferentHandlers_IsNotCollapsed()
    {
        var current = Translation("{0}% increased Damage", handlers: ["divide_by_ten_1dp"]);
        var historical = Translation("{0}% increased Damage", handlers: ["negate"]);

        var result = StatTranslationCompatibilityClassifier.Compare(current, historical, 1, false);

        Assert.Equal(StatTranslationCompatibilityClassification.MechanicsChanged, result.Classification);
        Assert.NotEqual(result.CurrentMechanicalSignature, result.HistoricalMechanicalSignature);
    }

    [Fact]
    public void NumericArityChange_IsDetected()
    {
        var current = Translation("Adds {0} to {1} Damage", ["#", "#"], ["stat", "stat_max"]);
        var historical = Translation("Adds {0} Damage", ["#"], ["stat"]);

        var result = StatTranslationCompatibilityClassifier.Compare(current, historical, 1, false);

        Assert.Equal(StatTranslationCompatibilityClassification.NumericShapeChanged, result.Classification);
        Assert.NotEqual(result.CurrentNumericShapeSignature, result.HistoricalNumericShapeSignature);
    }

    [Fact]
    public void PlaceholderOrderChange_IsDetectedAsMechanicsChange()
    {
        var current = Translation("Adds {0} to {1} Damage", ["#", "#"], ["stat", "stat_max"]);
        var historical = Translation("Adds {1} to {0} Damage", ["#", "#"], ["stat", "stat_max"]);

        var result = StatTranslationCompatibilityClassifier.Compare(current, historical, 1, false);

        Assert.Equal(StatTranslationCompatibilityClassification.MechanicsChanged, result.Classification);
    }

    [Fact]
    public void NegateDivideAndConditionalBranchChanges_AreMechanicsChanges()
    {
        var baseline = Translation("{0}% increased Damage", handlers: ["negate"]);
        var divide = Translation("{0}% increased Damage", handlers: ["divide_by_ten_1dp"]);
        var conditional = Translation("{0}% increased Damage", conditionMin: 1m);

        Assert.Equal(
            StatTranslationCompatibilityClassification.MechanicsChanged,
            StatTranslationCompatibilityClassifier.Compare(baseline, divide, 1, false).Classification);
        Assert.Equal(
            StatTranslationCompatibilityClassification.MechanicsChanged,
            StatTranslationCompatibilityClassifier.Compare(baseline, conditional, 1, false).Classification);
    }

    [Fact]
    public void ContributorVectorOrdering_IsDeterministic()
    {
        var first = Translation("Adds {0} to {1} Damage", ["#", "#"], ["minimum", "maximum"]);
        var reversed = Translation("Adds {0} to {1} Damage", ["#", "#"], ["maximum", "minimum"]);

        var result = StatTranslationCompatibilityClassifier.Compare(first, reversed, 1, false);

        Assert.Equal(StatTranslationCompatibilityClassification.MechanicsChanged, result.Classification);
    }

    [Fact]
    public void HistoricalOnlyModifier_DoesNotBecomeCurrentRuntimeEligible()
    {
        var current = Translation("Current {0}% Damage");
        var historical = Translation("Historical {0}% Damage");
        var historicalModifier = new ModifierDefinition
        {
            Id = "removed",
            GroupId = "group",
            Name = "Removed",
            GenerationType = ModifierGenerationType.Prefix,
            Domain = "item",
            Stats = [new ModifierStat { Index = 0, StatId = "stat", MinValue = 1, MaxValue = 2 }],
        };

        var history = StatTranslationHistoryBuilder.Build(
            "https://github.com/repoe-fork/repoe", new string('a', 40), "current",
            [], [new StatDefinition { Id = "stat" }], [current],
            "https://github.com/repoe-fork/repoe", new string('b', 40), "historical",
            [historicalModifier], [new StatDefinition { Id = "stat" }], [historical]);

        var change = Assert.Single(history.Changes);
        Assert.Equal(StatTranslationRuntimeRelevance.OrdinaryItemModifier, change.RuntimeRelevance);
        Assert.False(change.ChangesRuntimeBehaviorInT3A);
    }

    private static StatTranslationDefinition Translation(
        string line,
        IReadOnlyList<string>? formats = null,
        IReadOnlyList<string>? statIds = null,
        IReadOnlyList<string>? handlers = null,
        decimal? conditionMin = null)
    {
        formats ??= ["#"];
        statIds ??= ["stat"];
        return new StatTranslationDefinition
        {
            Id = "translation",
            StatIds = statIds,
            Language = "English",
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions = formats.Select((_, index) => new StatTranslationCondition
                    {
                        Index = index,
                        MinValue = index == 0 ? conditionMin : null,
                    }).ToArray(),
                    ValueFormats = formats,
                    IndexHandlers = formats.Select((_, index) => new StatTranslationIndexHandler
                    {
                        Index = index,
                        Handlers = index == 0 ? handlers ?? [] : [],
                    }).ToArray(),
                    FormatLines = [line],
                },
            ],
        };
    }
}
