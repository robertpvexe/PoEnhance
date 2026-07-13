using PoEnhance.Core.Items.GameData;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ModifierTextSignatureMatcherTests
{
    private readonly ModifierTextSignatureMatcher matcher = new();

    [Fact]
    public void Match_FlatAddsRange_UsesNumberPlaceholders()
    {
        var modifier = Modifier(
            Stat("cold_min", 46, 46),
            Stat("cold_max", 81, 81));
        var catalog = CreateCatalog(Translation(
            ["cold_min", "cold_max"],
            Variant(["Adds {0} to {1} Cold Damage"], ["#", "#"])));

        var result = matcher.Match(modifier, catalog, ["Adds 1 to 999 Cold Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
        Assert.Equal(["Adds <number> to <number> Cold Damage"], Assert.Single(result.CandidateSignatures).Lines);
        Assert.Equal(["Adds <number> to <number> Cold Damage"], Assert.Single(result.ParsedSignatures).Lines);
    }

    [Fact]
    public void Match_FlatAddsRangeWithRealAdvancedAnnotations_UsesNumberPlaceholders()
    {
        var modifier = Modifier(
            Stat("cold_min", 41, 55),
            Stat("cold_max", 81, 95));
        var catalog = CreateCatalog(Translation(
            ["cold_min", "cold_max"],
            Variant(["Adds {0} to {1} Cold Damage"], ["#", "#"])));

        var result = matcher.Match(modifier, catalog, ["Adds 46(41-55) to 81(81-95) Cold Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
        Assert.Equal(["Adds <number> to <number> Cold Damage"], Assert.Single(result.ParsedSignatures).Lines);
    }

    [Theory]
    [InlineData("20(16-20)% increased Global Accuracy Rating", "accuracy_+%", "{0}% increased Global Accuracy Rating")]
    [InlineData("15(14-16)% increased Trap Damage", "trap_damage_+%", "{0}% increased Trap Damage")]
    [InlineData("5(3-5)% increased Attack Speed", "attack_speed_+%", "{0}% increased Attack Speed")]
    [InlineData("+2(2-3)% Chance to Block Spell Damage while holding a Shield", "spell_block_%", "{0}% Chance to Block Spell Damage while holding a Shield", "+#")]
    [InlineData("+101(100-114) to maximum Life", "base_maximum_life", "{0} to maximum Life", "+#")]
    [InlineData("+47(46-48)% to Lightning Resistance", "lightning_resistance_+%", "{0}% to Lightning Resistance", "+#")]
    [InlineData("25(23-25)% increased Stun and Block Recovery", "base_stun_recovery_+%", "{0}% increased Stun and Block Recovery")]
    [InlineData("Regenerate 29.2(24.1-32) Life per second", "base_life_regeneration_rate_per_minute", "Regenerate {0} Life per second")]
    public void Match_RealAdvancedSingleValueAnnotations_Match(
        string parsedLine,
        string statId,
        string formatLine,
        string valueFormat = "#")
    {
        var modifier = Modifier(Stat(statId, 1, 100));
        var catalog = CreateCatalog(Translation(
            [statId],
            Variant([formatLine], [valueFormat])));

        var result = matcher.Match(modifier, catalog, [parsedLine]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
    }

    [Fact]
    public void Match_PercentageIncreased_UsesPercentShape()
    {
        var modifier = Modifier(Stat("accuracy_+%", 20, 20));
        var catalog = CreateCatalog(Translation(
            ["accuracy_+%"],
            Variant(["{0}% increased Global Accuracy Rating"], ["#"])));

        var result = matcher.Match(modifier, catalog, ["55% increased Global Accuracy Rating"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
    }

    [Fact]
    public void Match_IncreasedVersusReduced_ReturnsNoMatch()
    {
        var modifier = Modifier(Stat("accuracy_+%", 20, 20));
        var catalog = CreateCatalog(Translation(
            ["accuracy_+%"],
            Variant(["{0}% increased Global Accuracy Rating"], ["#"])));

        var result = matcher.Match(modifier, catalog, ["20% reduced Global Accuracy Rating"]);

        Assert.True(result.Evaluated);
        Assert.Equal(ModifierTextSignatureMatchOutcome.NoMatch, result.Outcome);
    }

    [Fact]
    public void Match_FlatVersusPercentage_ReturnsNoMatch()
    {
        var modifier = Modifier(Stat("accuracy_+%", 20, 20));
        var catalog = CreateCatalog(Translation(
            ["accuracy_+%"],
            Variant(["{0}% increased Global Accuracy Rating"], ["#"])));

        var result = matcher.Match(modifier, catalog, ["+20 to Global Accuracy Rating"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.NoMatch, result.Outcome);
    }

    [Fact]
    public void Match_TwoLineHybridModifier_PreservesLineStructure()
    {
        var modifier = Modifier(
            Stat("base_maximum_life", 40, 40),
            Stat("fire_resistance_+%", 20, 20));
        var catalog = CreateCatalog(
            Translation(["base_maximum_life"], Variant(["{0} to maximum Life"], ["+#"])),
            Translation(["fire_resistance_+%"], Variant(["{0}% to Fire Resistance"], ["+#"])));

        var result = matcher.Match(
            modifier,
            catalog,
            ["+44 to maximum Life", "+17% to Fire Resistance"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
        Assert.Equal(
            ["+<number> to maximum Life", "+<number>% to Fire Resistance"],
            Assert.Single(result.CandidateSignatures).Lines);
    }

    [Fact]
    public void Match_MultiStatTranslationCanFormOneDisplayedLine()
    {
        var modifier = Modifier(
            Stat("cold_min", 46, 46),
            Stat("cold_max", 81, 81));
        var catalog = CreateCatalog(Translation(
            ["cold_min", "cold_max"],
            Variant(["Adds {0} to {1} Cold Damage"], ["#", "#"])));

        var result = matcher.Match(
            modifier,
            catalog,
            ["Adds 999 to 1000 Cold Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
        Assert.Single(Assert.Single(result.CandidateSignatures).Lines);
    }

    [Fact]
    public void Match_MissingTranslation_ReturnsUnknown()
    {
        var modifier = Modifier(Stat("missing_stat", 1, 1));
        var catalog = CreateCatalog();

        var result = matcher.Match(modifier, catalog, ["+1 to maximum Life"]);

        Assert.False(result.Evaluated);
        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.TranslationMissing, result.ReasonCode);
    }

    [Fact]
    public void Match_UnresolvedTranslationCondition_ReturnsUnknown()
    {
        var modifier = Modifier(Stat("conditional_stat", 5, 15));
        var catalog = CreateCatalog(Translation(
            ["conditional_stat"],
            Variant(["{0}% increased Damage"], ["#"], Condition(0, 1, 10)),
            Variant(["{0}% reduced Damage"], ["#"], Condition(0, 11, null))));

        var result = matcher.Match(modifier, catalog, ["10% increased Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.TranslationConditionUnresolved, result.ReasonCode);
    }

    [Fact]
    public void Match_NegatedTranslationCondition_ReturnsUnknown()
    {
        var modifier = Modifier(Stat("negated_stat", 1, 1));
        var catalog = CreateCatalog(Translation(
            ["negated_stat"],
            Variant(["{0}% increased Damage"], ["#"], Condition(0, null, null, isNegated: true))));

        var result = matcher.Match(modifier, catalog, ["1% increased Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.TranslationConditionUnsupported, result.ReasonCode);
    }

    [Fact]
    public void Match_DuplicateTranslationConditionIndex_ReturnsUnknown()
    {
        var modifier = Modifier(Stat("duplicate_condition", 1, 1));
        var variant = Variant(
            ["{0}% increased Damage"],
            ["#"],
            Condition(0, null, null),
            Condition(0, null, null));
        var catalog = CreateCatalog(Translation(["duplicate_condition"], variant));

        var result = matcher.Match(modifier, catalog, ["1% increased Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.TranslationConditionUnresolved, result.ReasonCode);
    }

    [Fact]
    public void Match_DuplicateTranslationIndexHandler_ReturnsUnknown()
    {
        var modifier = Modifier(Stat("duplicate_handler", 1, 1));
        var variant = Variant(["{0}% increased Damage"], ["#"]) with
        {
            IndexHandlers =
            [
                new StatTranslationIndexHandler { Index = 0, Handlers = [] },
                new StatTranslationIndexHandler { Index = 0, Handlers = [] },
            ],
        };
        var catalog = CreateCatalog(Translation(["duplicate_handler"], variant));

        var result = matcher.Match(modifier, catalog, ["1% increased Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.TranslationShapeUnsupported, result.ReasonCode);
    }

    [Fact]
    public void Match_VerifiedReminderLineDoesNotCreateNoMatch()
    {
        var modifier = Modifier(Stat("recoup", 10, 10));
        var catalog = CreateCatalog(Translation(
            ["recoup"],
            Variant(["{0}% of Damage taken Recouped as Life"], ["#"])));

        var result = matcher.Match(
            modifier,
            catalog,
            [
                "10(8-10)% of Damage taken Recouped as Life",
                "(Only Damage from Hits can be Recouped, over 4 seconds following the Hit)",
            ]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
    }

    [Fact]
    public void Match_UnverifiedParenthesizedLineReturnsUnknownInsteadOfNoMatch()
    {
        var modifier = Modifier(Stat("damage", 10, 10));
        var catalog = CreateCatalog(Translation(
            ["damage"],
            Variant(["{0}% increased Damage"], ["#"])));

        var result = matcher.Match(
            modifier,
            catalog,
            [
                "10(8-10)% increased Damage",
                "(Unverified explanatory text)",
            ]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.ParsedSignatureUnsupported, result.ReasonCode);
    }

    private static GameDataCatalog CreateCatalog(params StatTranslationDefinition[] translations)
    {
        var statIds = translations.SelectMany(translation => translation.StatIds).Distinct(StringComparer.Ordinal).ToArray();
        if (statIds.Length == 0)
        {
            statIds = ["missing_stat"];
        }

        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = CreateManifest(),
            ItemBases = [],
            Modifiers =
            [
                Modifier(statIds.Select(statId => Stat(statId, 1, 1)).ToArray()),
            ],
            Stats = statIds.Select(StatDefinition).ToArray(),
            StatTranslations = translations,
        });
    }

    private static ModifierDefinition Modifier(params ModifierStat[] stats)
    {
        return new ModifierDefinition
        {
            Id = "mod.test",
            GroupId = "group.test",
            Name = "Test",
            GenerationType = ModifierGenerationType.Prefix,
            Domain = "item",
            Stats = stats
                .Select((stat, index) => stat with { Index = index })
                .ToArray(),
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = "mod.test",
                },
            ],
        };
    }

    private static ModifierStat Stat(string statId, decimal min, decimal max)
    {
        return new ModifierStat
        {
            Index = 0,
            StatId = statId,
            MinValue = min,
            MaxValue = max,
        };
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
        params StatTranslationCondition[] conditions)
    {
        conditions = conditions.Length == 0
            ? formats.Select((_, index) => Condition(index, null, null)).ToArray()
            : conditions;

        return new StatTranslationVariant
        {
            Conditions = conditions,
            ValueFormats = formats,
            IndexHandlers = formats
                .Select((_, index) => new StatTranslationIndexHandler
                {
                    Index = index,
                    Handlers = [],
                })
                .ToArray(),
            FormatLines = lines,
        };
    }

    private static StatTranslationCondition Condition(
        int index,
        decimal? min,
        decimal? max,
        bool isNegated = false)
    {
        return new StatTranslationCondition
        {
            Index = index,
            MinValue = min,
            MaxValue = max,
            IsNegated = isNegated,
        };
    }

    private static StatDefinition StatDefinition(string id)
    {
        return new StatDefinition
        {
            Id = id,
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

    private static GameDataPackageManifest CreateManifest()
    {
        return new GameDataPackageManifest
        {
            SchemaVersion = 1,
            DataVersion = "test",
            CreatedAtUtc = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "test",
                    RetrievedAtUtc = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
                    SourceVersion = "test",
                },
            ],
        };
    }
}
