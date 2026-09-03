using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class PoBUniqueCatalogImporterTests
{
    [Fact]
    public void Import_ExactUniqueGenerationAndRange_WinsBeforeConflictingNormalizedSignature()
    {
        var result = ImportSingle(
            """
                Test Diamond
                Diamond Ring
                Implicits: 0
                (80-120)% increased Global Critical Strike Chance
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.critical", "unique_critical", 80, 120, "unique"),
                Modifier("ordinary.critical", "ordinary_critical", 10, 14, "prefix"),
            ],
            translations:
            [
                Translation("unique-critical", "unique_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
                Translation("ordinary-critical", "ordinary_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.critical"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["unique_critical"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_MechanicallyEquivalentExactUniqueSources_PreserveEverySourceId()
    {
        var result = ImportSingle(
            """
                Test Diamond
                Diamond Ring
                Implicits: 0
                (80-120)% increased Global Critical Strike Chance
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.critical.one", "unique_critical", 80, 120, "unique"),
                Modifier("unique.critical.two", "unique_critical", 80, 120, "unique"),
            ],
            translations:
            [
                Translation("unique-critical", "unique_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            block.MechanicalMapping.Status);
        Assert.Equal(
            ["unique.critical.one", "unique.critical.two"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(["unique_critical"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_MechanicallyDifferentExactUniqueSources_RemainAmbiguous()
    {
        var result = ImportSingle(
            """
                Test Diamond
                Diamond Ring
                Implicits: 0
                (80-120)% increased Global Critical Strike Chance
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.critical.one", "first_critical", 80, 120, "unique"),
                Modifier("unique.critical.two", "second_critical", 80, 120, "unique"),
            ],
            translations:
            [
                Translation("first-critical", "first_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
                Translation("second-critical", "second_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(UniqueModifierSemanticLocality.Unknown,
            block.SourceSemanticFingerprint.Locality);
        Assert.Empty(block.MechanicalMapping.StatIds);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
        var conflict = Assert.IsType<UniqueMechanicalConflictEvidence>(
            block.MechanicalMapping.ConflictEvidence);
        Assert.Equal(UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds, conflict.Kind);
        Assert.Equal(2, conflict.Candidates.Count);
        Assert.Equal(
            ["unique.critical.one", "unique.critical.two"],
            conflict.Candidates.Select(candidate => candidate.ModifierId).ToArray());
        Assert.Contains(
            "ExactConflict: SameDisplayTextDifferentStatIds",
            block.MechanicalMapping.Diagnostic,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Import_LocalAttackSpeedFingerprint_SelectsEquivalentLocalSourcesAndRetainsProof()
    {
        const string line = "10% increased Attack Speed";
        var result = ImportSingle(
            $"""
                Test Blade
                Stiletto
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.attack-speed.global", "attack_speed_+%", 10, 10, "unique"),
                Modifier("unique.attack-speed.local.one", "local_attack_speed_+%", 10, 10, "unique"),
                Modifier("unique.attack-speed.local.two", "local_attack_speed_+%", 10, 10, "unique"),
            ],
            translations:
            [
                Translation("attack-speed-global", "attack_speed_+%", "{0}% increased Attack Speed", "#"),
                Translation("attack-speed-local", "local_attack_speed_+%", "{0}% increased Attack Speed", "#"),
            ],
            stats:
            [
                new StatDefinition { Id = "attack_speed_+%", IsLocal = false },
                new StatDefinition { Id = "local_attack_speed_+%", IsLocal = true },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Local,
            sourceLine: line,
            sourceBaseType: "Stiletto");

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            block.MechanicalMapping.Status);
        Assert.Equal(
            ["unique.attack-speed.local.one", "unique.attack-speed.local.two"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(["local_attack_speed_+%"], block.MechanicalMapping.StatIds);
        Assert.Equal(UniqueModifierSemanticLocality.Local,
            block.SourceSemanticFingerprint.Locality);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.Contains("source-semantic-fingerprint", provenance.ResolutionReasons);
        Assert.Equal(UniqueModifierSemanticLocality.Local,
            provenance.MatchedSemanticFingerprint!.Locality);
        Assert.Equal(UniqueModifierSemanticValueShape.Scalar,
            provenance.MatchedSemanticFingerprint.ValueShape);
        Assert.Equal("percent", Assert.Single(provenance.MatchedSemanticFingerprint.Values).Unit);
        Assert.Equal("attack-speed-local", Assert.Single(provenance.Translations).TranslationId);
        Assert.Null(block.MechanicalMapping.ConflictEvidence);
        Assert.Single(block.SourceObservationIds);
        Assert.All(block.SourceObservationIds, observationId =>
            Assert.StartsWith("pob-observation:", observationId));
    }

    [Fact]
    public void Import_PresenceTranslationWithoutValueFormat_RetainsComparablePresenceFingerprint()
    {
        const string line = "Cannot be Stunned";
        var result = ImportSingle(
            $"""
                Test Belt
                Leather Belt
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.cannot-be-stunned.one", "cannot_be_stunned", 1, 1, "unique"),
                Modifier("unique.cannot-be-stunned.two", "cannot_be_stunned", 1, 1, "unique"),
                Modifier("unique.cannot-be-stunned.local", "local_cannot_be_stunned", 1, 1, "unique"),
            ],
            translations:
            [
                Translation("cannot-be-stunned", "cannot_be_stunned", line),
                Translation("cannot-be-stunned-local", "local_cannot_be_stunned", line),
            ],
            stats:
            [
                new StatDefinition { Id = "cannot_be_stunned", IsLocal = false },
                new StatDefinition { Id = "local_cannot_be_stunned", IsLocal = true },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Global,
            sourceLine: line,
            sourceBaseType: "Leather Belt");

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            block.MechanicalMapping.Status);
        var fingerprint = Assert.IsType<UniqueModifierSemanticFingerprint>(
            block.MechanicalMapping.Provenance!.MatchedSemanticFingerprint);
        Assert.Equal(UniqueModifierSemanticValueShape.Presence, fingerprint.ValueShape);
        var value = Assert.Single(fingerprint.Values);
        Assert.Equal("ignore", value.Format);
        Assert.Equal("none", value.Unit);
    }

    [Fact]
    public void Import_GlobalAttackSpeedFingerprint_SelectsGlobalCandidate()
    {
        const string line = "10% increased Attack Speed";
        var result = ImportSingle(
            $"""
                Test Belt
                Leather Belt
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.attack-speed.global", "attack_speed_+%", 10, 10, "unique"),
                Modifier("unique.attack-speed.local", "local_attack_speed_+%", 10, 10, "unique"),
            ],
            translations:
            [
                Translation("attack-speed-global", "attack_speed_+%", "{0}% increased Attack Speed", "#"),
                Translation("attack-speed-local", "local_attack_speed_+%", "{0}% increased Attack Speed", "#"),
            ],
            stats:
            [
                new StatDefinition { Id = "attack_speed_+%", IsLocal = false },
                new StatDefinition { Id = "local_attack_speed_+%", IsLocal = true },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Global,
            sourceLine: line,
            sourceBaseType: "Leather Belt");

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.attack-speed.global"], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_LocalEnergyShieldFingerprint_SelectsLocalCandidate()
    {
        const string line = "+(50-70) to maximum Energy Shield";
        var result = ImportSingle(
            $"""
                Test Gloves
                Carnal Mitts
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.energy-shield.global", "base_maximum_energy_shield", 50, 70, "unique"),
                Modifier("unique.energy-shield.local", "local_energy_shield", 50, 70, "unique"),
            ],
            translations:
            [
                Translation("energy-shield-global", "base_maximum_energy_shield", "{0} to maximum Energy Shield", "+#"),
                Translation("energy-shield-local", "local_energy_shield", "{0} to maximum Energy Shield", "+#"),
            ],
            stats:
            [
                new StatDefinition { Id = "base_maximum_energy_shield", IsLocal = false },
                new StatDefinition { Id = "local_energy_shield", IsLocal = true },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Local,
            sourceLine: line,
            sourceBaseType: "Carnal Mitts");

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(["unique.energy-shield.local"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["local_energy_shield"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_LocalAccuracyFingerprint_SelectsLocalCandidate()
    {
        const string line = "+30 to Accuracy Rating";
        var result = ImportSingle(
            $"""
                Test Bow
                Crude Bow
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.accuracy.global", "accuracy_rating", 30, 30, "unique"),
                Modifier("unique.accuracy.local", "local_accuracy_rating", 30, 30, "unique"),
            ],
            translations:
            [
                Translation("accuracy-global", "accuracy_rating", "{0} to Accuracy Rating", "+#"),
                Translation("accuracy-local", "local_accuracy_rating", "{0} to Accuracy Rating", "+#"),
            ],
            stats:
            [
                new StatDefinition { Id = "accuracy_rating", IsLocal = false },
                new StatDefinition { Id = "local_accuracy_rating", IsLocal = true },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Local,
            sourceLine: line,
            sourceBaseType: "Crude Bow");

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(["unique.accuracy.local"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["local_accuracy_rating"], block.MechanicalMapping.StatIds);
    }

    [Theory]
    [InlineData(
        "(80-100)% increased Armour",
        "local_physical_damage_reduction_rating_+%",
        "physical_damage_reduction_rating_+%",
        "Gladiator Plate")]
    [InlineData(
        "(80-100)% increased Evasion Rating",
        "local_evasion_rating_+%",
        "evasion_rating_+%",
        "Sharkskin Tunic")]
    public void Import_LocalDefenceFingerprint_SelectsLocalCandidate(
        string line,
        string localStatId,
        string globalStatId,
        string baseType)
    {
        var result = ImportSingle(
            $"""
                Test Defence
                {baseType}
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.defence.global", globalStatId, 80, 100, "unique"),
                Modifier("unique.defence.local", localStatId, 80, 100, "unique"),
            ],
            translations:
            [
                Translation("defence-global", globalStatId, line.Replace("(80-100)", "{0}"), "#"),
                Translation("defence-local", localStatId, line.Replace("(80-100)", "{0}"), "#"),
            ],
            stats:
            [
                new StatDefinition { Id = globalStatId, IsLocal = false },
                new StatDefinition { Id = localStatId, IsLocal = true },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Local,
            sourceLine: line,
            sourceBaseType: baseType);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.defence.local"], block.MechanicalMapping.ModifierIds);
        Assert.Equal([localStatId], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GlobalLeechFingerprint_RejectsCurrentAndLegacyLocalCandidates()
    {
        const string line = "2% of Physical Attack Damage Leeched as Life";
        var result = ImportSingle(
            $"""
                Test Belt
                Leather Belt
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.leech.global", "life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.leech.local", "local_life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.leech.local.legacy", "old_local_life_leech_from_physical_attack_damage_percent", 10, 10, "unique"),
            ],
            translations:
            [
                TranslationWithHandler("leech-global", "life_leech_from_physical_attack_damage_permyriad", "{0}% of Physical Attack Damage Leeched as Life", "divide_by_one_hundred"),
                TranslationWithHandler("leech-local", "local_life_leech_from_physical_attack_damage_permyriad", "{0}% of Physical Attack Damage Leeched as Life", "divide_by_one_hundred"),
                TranslationWithHandler("leech-local-legacy", "old_local_life_leech_from_physical_attack_damage_percent", "{0}% of Physical Attack Damage Leeched as Life", "old_leech_percent"),
            ],
            stats:
            [
                new StatDefinition { Id = "life_leech_from_physical_attack_damage_permyriad", IsLocal = false },
                new StatDefinition { Id = "local_life_leech_from_physical_attack_damage_permyriad", IsLocal = true },
                new StatDefinition { Id = "old_local_life_leech_from_physical_attack_damage_percent", IsLocal = true },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Global,
            sourceLine: line,
            sourceBaseType: "Leather Belt");

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.leech.global"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["scale:0.01"],
            Assert.Single(block.MechanicalMapping.Provenance!.MatchedSemanticFingerprint!.Values)
                .Transformations);
    }

    [Fact]
    public void Import_EvaluatedVariantFingerprint_UsesExactLineWhenRawVariantIndicesShift()
    {
        const string line = "2% of Physical Attack Damage Leeched as Life";
        var result = ImportSingle(
            $$"""
                Test Belt
                Leather Belt
                Variant: Pre 3.19.0
                Variant: Current
                Implicits: 0
                {variant:1}+(10-20)% to Cold Resistance
                {variant:2}+(20-30)% to Cold Resistance
                {variant:1}0.4% of Physical Attack Damage Leeched as Life
                {variant:2}{{line}}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.leech.global", "life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.leech.local", "local_life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.cold-resistance", "cold_resistance", 20, 30, "unique"),
            ],
            translations:
            [
                TranslationWithHandler("leech-global", "life_leech_from_physical_attack_damage_permyriad", "{0}% of Physical Attack Damage Leeched as Life", "divide_by_one_hundred"),
                TranslationWithHandler("leech-local", "local_life_leech_from_physical_attack_damage_permyriad", "{0}% of Physical Attack Damage Leeched as Life", "divide_by_one_hundred"),
                Translation("cold-resistance", "cold_resistance", "{0}% to Cold Resistance", "+#"),
            ],
            stats:
            [
                new StatDefinition { Id = "life_leech_from_physical_attack_damage_permyriad", IsLocal = false },
                new StatDefinition { Id = "local_life_leech_from_physical_attack_damage_permyriad", IsLocal = true },
                new StatDefinition { Id = "cold_resistance", IsLocal = false },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Global,
            sourceLine: line,
            sourceBaseType: "Leather Belt",
            sourceLineIndex: 1);

        var current = Assert.Single(Assert.Single(result.Catalog!.Items).Versions, version =>
            version.Role == UniqueItemVersionRole.Current);
        var block = Assert.Single(current.ModifierBlocks, candidate =>
            candidate.Lines.Contains(line));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.leech.global"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(UniqueModifierSemanticLocality.Global,
            block.SourceSemanticFingerprint.Locality);
    }

    [Fact]
    public void Import_CurrentLocalLeechFingerprint_ResolvesDeprecatedPercentVersusPermyriad()
    {
        const string line = "2% of Physical Attack Damage Leeched as Life";
        var result = ImportSingle(
            $"""
                Test Bow
                Crude Bow
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.leech.local", "local_life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.leech.local.legacy", "old_local_life_leech_from_physical_attack_damage_percent", 10, 10, "unique"),
            ],
            translations:
            [
                TranslationWithHandler("leech-local", "local_life_leech_from_physical_attack_damage_permyriad", "{0}% of Physical Attack Damage Leeched as Life", "divide_by_one_hundred"),
                TranslationWithHandler("leech-local-legacy", "old_local_life_leech_from_physical_attack_damage_percent", "{0}% of Physical Attack Damage Leeched as Life", "old_leech_percent"),
            ],
            stats:
            [
                new StatDefinition { Id = "local_life_leech_from_physical_attack_damage_permyriad", IsLocal = true },
                new StatDefinition { Id = "old_local_life_leech_from_physical_attack_damage_percent", IsLocal = true },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Local,
            sourceLine: line,
            sourceBaseType: "Crude Bow");

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal(UniqueItemVersionRole.Current, version.Role);
        var block = Assert.Single(version.ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Null(block.MechanicalMapping.DiagnosticCode);
        Assert.Null(block.MechanicalMapping.ConflictEvidence);
        Assert.Equal(["unique.leech.local"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["local_life_leech_from_physical_attack_damage_permyriad"],
            block.MechanicalMapping.StatIds);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.Contains("current-role-deprecated-encoding-filter", provenance.ResolutionReasons);
        Assert.DoesNotContain(
            provenance.Translations,
            evidence => evidence.StatIds.Any(statId =>
                UniqueMechanicalConflictClassifier.BuildEncodingMarkers(
                    "x",
                    [statId],
                    []).Contains(UniqueMechanicalConflictClassifier.MarkerDeprecatedName)));
    }

    [Fact]
    public void Import_CurrentPermyriadConflict_MultipleSurvivors_BecomeEquivalentSourceSet()
    {
        const string line = "2% of Physical Attack Damage Leeched as Life";
        var result = ImportSingle(
            $"""
                Test Bow
                Crude Bow
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.leech.a", "local_life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.leech.b", "local_life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.leech.legacy", "old_local_life_leech_from_physical_attack_damage_percent", 10, 10, "unique"),
            ],
            translations:
            [
                TranslationWithHandler("leech-current", "local_life_leech_from_physical_attack_damage_permyriad", "{0}% of Physical Attack Damage Leeched as Life", "divide_by_one_hundred"),
                TranslationWithHandler("leech-legacy", "old_local_life_leech_from_physical_attack_damage_percent", "{0}% of Physical Attack Damage Leeched as Life", "old_leech_percent"),
            ],
            stats:
            [
                new StatDefinition { Id = "local_life_leech_from_physical_attack_damage_permyriad", IsLocal = true },
                new StatDefinition { Id = "old_local_life_leech_from_physical_attack_damage_percent", IsLocal = true },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            block.MechanicalMapping.Status);
        Assert.Null(block.MechanicalMapping.ConflictEvidence);
        Assert.Equal(["unique.leech.a", "unique.leech.b"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["local_life_leech_from_physical_attack_damage_permyriad"],
            block.MechanicalMapping.StatIds);
        Assert.Contains(
            "current-role-deprecated-encoding-filter",
            block.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_CurrentPermyriadConflict_TwoModernVectorsSurvive_RemainsExactConflict()
    {
        const string line = "2% of Physical Attack Damage Leeched as Life";
        var result = ImportSingle(
            $"""
                Test Bow
                Crude Bow
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.leech.local", "local_life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.leech.global", "life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.leech.legacy", "old_local_life_leech_from_physical_attack_damage_percent", 10, 10, "unique"),
            ],
            translations:
            [
                TranslationWithHandler("leech-local", "local_life_leech_from_physical_attack_damage_permyriad", "{0}% of Physical Attack Damage Leeched as Life", "divide_by_one_hundred"),
                TranslationWithHandler("leech-global", "life_leech_from_physical_attack_damage_permyriad", "{0}% of Physical Attack Damage Leeched as Life", "divide_by_one_hundred"),
                TranslationWithHandler("leech-legacy", "old_local_life_leech_from_physical_attack_damage_percent", "{0}% of Physical Attack Damage Leeched as Life", "old_leech_percent"),
            ],
            stats:
            [
                new StatDefinition { Id = "local_life_leech_from_physical_attack_damage_permyriad", IsLocal = true },
                new StatDefinition { Id = "life_leech_from_physical_attack_damage_permyriad", IsLocal = false },
                new StatDefinition { Id = "old_local_life_leech_from_physical_attack_damage_percent", IsLocal = true },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
        var conflict = Assert.IsType<UniqueMechanicalConflictEvidence>(
            block.MechanicalMapping.ConflictEvidence);
        Assert.Equal(
            UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent,
            conflict.Kind);
        Assert.Equal(3, conflict.Candidates.Count);
        Assert.Empty(block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_CurrentPermyriadConflict_OnlyDeprecatedCandidates_RemainsExactConflict()
    {
        const string line = "2% of Physical Attack Damage Leeched as Life";
        var result = ImportSingle(
            $"""
                Test Bow
                Crude Bow
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.leech.legacy.a", "old_local_life_leech_from_physical_attack_damage_percent", 10, 10, "unique"),
                Modifier("unique.leech.legacy.b", "old_do_not_use_local_life_leech_from_physical_damage_%", 10, 10, "unique"),
            ],
            translations:
            [
                TranslationWithHandler("leech-legacy-a", "old_local_life_leech_from_physical_attack_damage_percent", "{0}% of Physical Attack Damage Leeched as Life", "old_leech_percent"),
                TranslationWithHandler("leech-legacy-b", "old_do_not_use_local_life_leech_from_physical_damage_%", "{0}% of Physical Attack Damage Leeched as Life", "old_leech_percent"),
            ],
            stats:
            [
                new StatDefinition { Id = "old_local_life_leech_from_physical_attack_damage_percent", IsLocal = true },
                new StatDefinition { Id = "old_do_not_use_local_life_leech_from_physical_damage_%", IsLocal = true },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
        Assert.NotNull(block.MechanicalMapping.ConflictEvidence);
        Assert.Empty(block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_HistoricalPermyriadConflict_DoesNotPreferModernEncoding()
    {
        var result = ImportSingle(
            """
                Test Bow
                Crude Bow
                Variant: Pre 2.6.0
                Variant: Current
                Implicits: 0
                {variant:1}2% of Physical Attack Damage Leeched as Life
                {variant:2}10% increased Attack Speed
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.leech.local", "local_life_leech_from_physical_attack_damage_permyriad", 200, 200, "unique"),
                Modifier("unique.leech.legacy", "old_local_life_leech_from_physical_attack_damage_percent", 10, 10, "unique"),
                Modifier("unique.attack-speed", "local_attack_speed_+%", 10, 10, "unique"),
            ],
            translations:
            [
                TranslationWithHandler("leech-local", "local_life_leech_from_physical_attack_damage_permyriad", "{0}% of Physical Attack Damage Leeched as Life", "divide_by_one_hundred"),
                TranslationWithHandler("leech-legacy", "old_local_life_leech_from_physical_attack_damage_percent", "{0}% of Physical Attack Damage Leeched as Life", "old_leech_percent"),
                Translation("attack-speed", "local_attack_speed_+%", "{0}% increased Attack Speed", "#"),
            ],
            stats:
            [
                new StatDefinition { Id = "local_life_leech_from_physical_attack_damage_permyriad", IsLocal = true },
                new StatDefinition { Id = "old_local_life_leech_from_physical_attack_damage_percent", IsLocal = true },
                new StatDefinition { Id = "local_attack_speed_+%", IsLocal = true },
            ],
            baseItems: [new ItemBaseRecord { Name = "Crude Bow", Domain = "item" }]);

        var historical = Assert.Single(
            Assert.Single(result.Catalog!.Items).Versions,
            version => version.Role == UniqueItemVersionRole.Historical);
        var leech = Assert.Single(
            historical.ModifierBlocks,
            block => block.Lines.Contains(
                "2% of Physical Attack Damage Leeched as Life"));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, leech.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", leech.MechanicalMapping.DiagnosticCode);
        var conflict = Assert.IsType<UniqueMechanicalConflictEvidence>(
            leech.MechanicalMapping.ConflictEvidence);
        Assert.Equal(
            UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent,
            conflict.Kind);
        Assert.Empty(leech.MechanicalMapping.StatIds);
        Assert.Null(leech.MechanicalMapping.Provenance);
    }

    [Fact]
    public void Import_ExactConflict_LevelVersusChanceOnHit_RetainsSubtypeAndVectors()
    {
        const string line = "Curse Enemies with Temporal Chains on Hit";
        var result = ImportSingle(
            $"""
                Test Gloves
                Silk Gloves
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.curse.level", "curse_on_hit_level_temporal_chains", 10, 10, "unique"),
                Modifier("unique.curse.chance", "curse_on_hit_%_temporal_chains", 100, 100, "unique"),
            ],
            translations:
            [
                Translation("curse-level", "curse_on_hit_level_temporal_chains", line, "ignore"),
                Translation("curse-chance", "curse_on_hit_%_temporal_chains", line, "ignore"),
            ],
            stats:
            [
                new StatDefinition { Id = "curse_on_hit_level_temporal_chains", IsLocal = false },
                new StatDefinition { Id = "curse_on_hit_%_temporal_chains", IsLocal = false },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
        Assert.Empty(block.MechanicalMapping.StatIds);
        var conflict = Assert.IsType<UniqueMechanicalConflictEvidence>(
            block.MechanicalMapping.ConflictEvidence);
        Assert.Equal(UniqueMechanicalConflictKind.LevelVsChanceOnHit, conflict.Kind);
        Assert.Contains(
            conflict.Candidates,
            candidate => candidate.StatIds.Contains("curse_on_hit_level_temporal_chains"));
        Assert.Contains(
            conflict.Candidates,
            candidate => candidate.StatIds.Contains("curse_on_hit_%_temporal_chains"));
        Assert.Null(block.MechanicalMapping.Provenance);
    }

    [Fact]
    public void Import_ExactConflict_InverseLegacyHandlerEncoding_RetainsSubtypeEvidence()
    {
        const string line = "Herald of Ice has (30-40)% increased Mana Reservation Efficiency";
        var result = ImportSingle(
            $"""
                Test Ring
                Sapphire Ring
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers:
            [
                Modifier(
                    "unique.reservation.modern",
                    "herald_of_ice_mana_reservation_efficiency_+%",
                    30,
                    40,
                    "unique"),
                Modifier(
                    "unique.reservation.legacy",
                    "herald_of_ice_mana_reservation_efficiency_-2%_per_1",
                    30,
                    40,
                    "unique"),
            ],
            translations:
            [
                Translation(
                    "reservation-modern",
                    "herald_of_ice_mana_reservation_efficiency_+%",
                    "Herald of Ice has {0}% increased Mana Reservation Efficiency",
                    "#"),
                Translation(
                    "reservation-legacy",
                    "herald_of_ice_mana_reservation_efficiency_-2%_per_1",
                    "Herald of Ice has {0}% increased Mana Reservation Efficiency",
                    "#"),
            ],
            stats:
            [
                new StatDefinition
                {
                    Id = "herald_of_ice_mana_reservation_efficiency_+%",
                    IsLocal = false,
                },
                new StatDefinition
                {
                    Id = "herald_of_ice_mana_reservation_efficiency_-2%_per_1",
                    IsLocal = false,
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
        var conflict = Assert.IsType<UniqueMechanicalConflictEvidence>(
            block.MechanicalMapping.ConflictEvidence);
        Assert.Equal(UniqueMechanicalConflictKind.InverseLegacyHandlerEncoding, conflict.Kind);
        Assert.Contains(
            conflict.Candidates,
            candidate => candidate.EncodingMarkers.Contains(
                UniqueMechanicalConflictClassifier.MarkerEfficiencyPlus));
        Assert.Contains(
            conflict.Candidates,
            candidate => candidate.EncodingMarkers.Contains(
                UniqueMechanicalConflictClassifier.MarkerEfficiencyInverse));
        Assert.Null(block.MechanicalMapping.Provenance);
    }

    [Fact]
    public void Import_KnownSourceLocalityWithUniformCandidateAxis_PreservesExactEvidence()
    {
        const string line = "+30 to Accuracy Rating";
        var result = ImportSingle(
            $"""
                Test Bow
                Crude Bow
                Implicits: 0
                {line}
                """,
            generated: false,
            modifiers: [Modifier("unique.accuracy.global", "accuracy_rating", 30, 30, "unique")],
            translations:
            [
                Translation("accuracy-global", "accuracy_rating", "{0} to Accuracy Rating", "+#"),
            ],
            stats: [new StatDefinition { Id = "accuracy_rating", IsLocal = false }],
            sourceLocality: UniqueModifierSemanticLocality.Local,
            sourceLine: line,
            sourceBaseType: "Crude Bow");

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.accuracy.global"], block.MechanicalMapping.ModifierIds);
        Assert.Null(block.MechanicalMapping.ConflictEvidence);
    }

    [Fact]
    public void Import_ExactUniqueEvidence_UsesCompatibleBaseDomainWhenAvailable()
    {
        var itemCandidate = Modifier("unique.item", "item_critical", 80, 120, "unique");
        var monsterCandidate = Modifier("unique.monster", "monster_critical", 80, 120, "unique") with
        {
            Domain = "monster",
        };
        var result = ImportSingle(
            """
                Test Diamond
                Diamond Ring
                Implicits: 0
                (80-120)% increased Global Critical Strike Chance
                """,
            generated: false,
            modifiers: [itemCandidate, monsterCandidate],
            translations:
            [
                Translation("item-critical", "item_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
                Translation("monster-critical", "monster_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord { Name = "Diamond Ring", Domain = "item" },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.item"], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_PartialTranslationVector_DefaultsAbsentStatToZeroAndRetainsProof()
    {
        var modifier = Modifier(
            "unique.returning-projectiles",
            "projectiles_return",
            1,
            1,
            "unique");
        var translation = new StatTranslationDefinition
        {
            Id = "returning-projectiles",
            StatIds = ["projectiles_return", "projectile_return_chance"],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition
                        {
                            Index = 0,
                            MinValue = 0,
                            MaxValue = 0,
                            IsNegated = true,
                        },
                        new StatTranslationCondition { Index = 1, MinValue = 0, MaxValue = 0 },
                    ],
                    ValueFormats = ["ignore", "ignore"],
                    IndexHandlers =
                    [
                        new StatTranslationIndexHandler { Index = 0 },
                        new StatTranslationIndexHandler { Index = 1 },
                    ],
                    FormatLines =
                    [
                        "Projectiles Return to you",
                        "Return",
                    ],
                },
            ],
        };

        var result = ImportSingle(
            """
                Test Return
                Topaz Ring
                Implicits: 0
                Projectiles Return to you
                Return
                """,
            generated: false,
            modifiers: [modifier],
            translations: [translation]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["projectiles_return"], block.MechanicalMapping.StatIds);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.Equal(["implicit-zero-stat-composition"], provenance.ResolutionReasons);
        Assert.True(provenance.UsedComposition);
        Assert.Equal("copiedInstance", provenance.ValueAuthority);
        var evidence = Assert.Single(provenance.Translations);
        Assert.Equal("returning-projectiles", evidence.TranslationId);
        Assert.Equal(["projectile_return_chance"], evidence.DefaultedStatIds);
        Assert.Equal(2, evidence.Conditions.Count);
        var fingerprint = Assert.IsType<UniqueModifierSemanticFingerprint>(
            provenance.MatchedSemanticFingerprint);
        Assert.Equal(["projectiles_return"], fingerprint.OrderedStatIds);
        Assert.Equal(UniqueModifierSemanticValueShape.Presence, fingerprint.ValueShape);
        Assert.Equal(["projectile_return_chance"], fingerprint.AuxiliaryStatIds);
        Assert.Equal(2, fingerprint.Values.Count);
        Assert.True(fingerprint.Values[1].IsAuxiliary);
    }

    [Fact]
    public void Import_CompleteMultilineModifier_GroupsSourceLinesIndependentOfRenderingOrder()
    {
        var modifier = Modifier(
            "unique.minion-count",
            ("zombies", 1m, 1m),
            ("skeletons", 1m, 1m),
            ("spectres", 1m, 1m));
        var result = ImportSingle(
            """
                Test Wand
                Calling Wand
                Implicits: 0
                +1 to maximum number of Raised Zombies
                +1 to maximum number of Spectres
                +1 to maximum number of Skeletons
                """,
            generated: false,
            modifiers: [modifier],
            translations:
            [
                TranslationWithDefaultedZero(
                    "zombies",
                    "zombies",
                    "quality_display_raise_zombie_is_gem",
                    "{0} to maximum number of Raised Zombies"),
                TranslationWithDefaultedZero(
                    "skeletons",
                    "skeletons",
                    "quality_display_summon_skeleton_is_gem",
                    "{0} to maximum number of Skeletons"),
                Translation("spectres", "spectres", "{0} to maximum number of Spectres", "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.minion-count"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["zombies", "skeletons", "spectres"], block.MechanicalMapping.StatIds);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.True(provenance.UsedComposition);
        Assert.Equal(3, provenance.Translations.Count);
        Assert.Equal("copiedInstance", provenance.ValueAuthority);
        Assert.Contains("implicit-zero-stat-composition", provenance.ResolutionReasons);
        Assert.Contains("order-independent-complete-multiline", provenance.ResolutionReasons);
        Assert.Null(block.Composition);
    }

    [Theory]
    [InlineData("+(30-50) to maximum Energy Shield", "local_energy_shield", 30, 50)]
    [InlineData("(100-120)% increased Armour", "local_armour", 100, 120)]
    public void Import_SourceProvenCompoundDefenceAndStun_RecordsOrderedLineComposition(
        string defenceLine,
        string defenceStatId,
        decimal minimum,
        decimal maximum)
    {
        const string stunLine = "10% increased Stun and Block Recovery";
        var modifier = Modifier(
            "unique.compound-defence",
            (defenceStatId, minimum, maximum),
            ("base_stun_recovery_+%", 10m, 10m)) with
        {
            SourceText = $"{defenceLine}\n{stunLine}",
        };
        var result = ImportSingle(
            $"""
                Test Helmet
                Iron Hat
                Implicits: 0
                {defenceLine}
                {stunLine}
                """,
            generated: false,
            modifiers: [modifier],
            translations:
            [
                Translation("defence", defenceStatId,
                    defenceStatId == "local_energy_shield"
                        ? "{0} to maximum Energy Shield"
                        : "{0}% increased Armour",
                    defenceStatId == "local_energy_shield" ? "+#" : "#"),
                Translation("stun-recovery", "base_stun_recovery_+%",
                    "{0}% increased Stun and Block Recovery", "#"),
            ],
            stats:
            [
                new StatDefinition { Id = defenceStatId, IsLocal = true },
                new StatDefinition { Id = "base_stun_recovery_+%", IsLocal = false },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([defenceStatId, "base_stun_recovery_+%"], block.MechanicalMapping.StatIds);
        var composition = Assert.IsType<UniqueModifierComposition>(block.Composition);
        Assert.Equal(2, composition.Components.Count);
        Assert.Equal([defenceLine], composition.Components[0].Lines);
        Assert.Equal([defenceStatId], composition.Components[0].StatIds);
        Assert.Equal([stunLine], composition.Components[1].Lines);
        Assert.Equal(["base_stun_recovery_+%"], composition.Components[1].StatIds);
        Assert.Empty(composition.AuxiliaryStatIds);
        Assert.Contains(
            "source-block-composition",
            block.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_SourceTextFallback_GroupsBonesOfUllrAndRetainsZeroAuxiliaryStat()
    {
        const string zombie = "+1 to Level of all Raise Zombie Gems";
        const string spectre = "+1 to Level of all Raise Spectre Gems";
        var modifier = Modifier(
            "unique.bones-of-ullr",
            ("zombie_gem_level", 1m, 1m),
            ("skeleton_gem_level", 0m, 0m),
            ("spectre_gem_level", 1m, 1m)) with
        {
            SourceText = $"{zombie}\n{spectre}",
        };
        var duplicate = modifier with { Id = "unique.bones-of-ullr.divergent" };
        var result = ImportSingle(
            $"""
                Bones of Ullr
                Silk Slippers
                Implicits: 0
                {zombie}
                {spectre}
                """,
            generated: false,
            modifiers: [modifier, duplicate],
            translations: []);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            block.MechanicalMapping.Status);
        Assert.Equal(
            ["unique.bones-of-ullr", "unique.bones-of-ullr.divergent"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["zombie_gem_level", "skeleton_gem_level", "spectre_gem_level"],
            block.MechanicalMapping.StatIds);
        var composition = Assert.IsType<UniqueModifierComposition>(block.Composition);
        Assert.Equal(["zombie_gem_level"], composition.Components[0].StatIds);
        Assert.Equal(["spectre_gem_level"], composition.Components[1].StatIds);
        Assert.Equal(["skeleton_gem_level"], composition.AuxiliaryStatIds);
        Assert.Contains(
            "repoe-modifier-source-text",
            block.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_SourceTextFallback_GroupsBattleWithinPresenceLines()
    {
        const string first = "Does not inflict Mana Burn over time";
        const string second = "Inflicts Mana Burn on you when you Hit an Enemy with a Melee Weapon";
        var modifier = Modifier(
            "unique.battle-within",
            ("local_cannot_generate_toxicity_stacks_over_time", 1m, 1m),
            ("toxicity_stacks_gained_on_hit_with_tinctured_weapons", 1m, 1m)) with
        {
            SourceText = $"{first}\n{second}",
            Domain = "tincture",
        };
        var result = ImportSingle(
            $"""
                The Battle Within
                Prismatic Tincture
                Implicits: 0
                {first}
                {second}
                """,
            generated: false,
            modifiers: [modifier],
            translations: []);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(2, Assert.IsType<UniqueModifierComposition>(block.Composition).Components.Count);
        Assert.Contains(
            "repoe-modifier-source-text",
            block.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_AdjacentIndependentLifeAndManaLines_RemainSeparateBlocks()
    {
        var life = Modifier("unique.life", "maximum_life", 20, 20, "unique") with
        {
            SourceText = "+20 to maximum Life",
        };
        var mana = Modifier("unique.mana", "maximum_mana", 20, 20, "unique") with
        {
            SourceText = "+20 to maximum Mana",
        };
        var result = ImportSingle(
            """
                Test Boots
                Wool Shoes
                Implicits: 0
                +20 to maximum Life
                +20 to maximum Mana
                """,
            generated: false,
            modifiers: [life, mana],
            translations:
            [
                Translation("life", "maximum_life", "{0} to maximum Life", "+#"),
                Translation("mana", "maximum_mana", "{0} to maximum Mana", "+#"),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block => Assert.Null(block.Composition));
        Assert.All(blocks, block => Assert.Single(block.MechanicalMapping.StatIds));
    }

    [Fact]
    public void Import_CompleteMultilineLiteralAndNumericModifier_GroupsReversedSourceOrder()
    {
        var modifier = Modifier(
            "unique.life-reservation",
            ("life_reserved", 30m, 30m),
            ("cannot_use_ci", 1m, 1m));
        var result = ImportSingle(
            """
                Test Wand
                Calling Wand
                Implicits: 0
                Cannot be used with Chaos Inoculation
                Reserves 30% of Life
                """,
            generated: false,
            modifiers: [modifier],
            translations:
            [
                Translation("reservation", "life_reserved", "Reserves {0}% of Life", "#"),
                Translation("cannot-use-ci", "cannot_use_ci", "Cannot be used with Chaos Inoculation", "ignore"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.life-reservation"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["life_reserved", "cannot_use_ci"], block.MechanicalMapping.StatIds);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.Equal(2, provenance.Translations.Count);
        Assert.Contains("order-independent-complete-multiline", provenance.ResolutionReasons);
    }

    [Fact]
    public void Import_PartialMultilineModifier_OmittingNonzeroStatDoesNotResolveCompleteVector()
    {
        var modifier = Modifier(
            "unique.minion-count",
            ("zombies", 1m, 1m),
            ("skeletons", 1m, 1m),
            ("spectres", 1m, 1m));
        var result = ImportSingle(
            """
                Test Wand
                Calling Wand
                Implicits: 0
                +1 to maximum number of Raised Zombies
                +1 to maximum number of Spectres
                """,
            generated: false,
            modifiers: [modifier],
            translations:
            [
                Translation("zombies", "zombies", "{0} to maximum number of Raised Zombies", "+#"),
                Translation("skeletons", "skeletons", "{0} to maximum number of Skeletons", "+#"),
                Translation("spectres", "spectres", "{0} to maximum number of Spectres", "+#"),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block =>
        {
            Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported,
                block.MechanicalMapping.Status);
            Assert.Empty(block.MechanicalMapping.StatIds);
            Assert.Null(block.MechanicalMapping.Provenance);
        });
    }

    [Fact]
    public void Import_CompetingCompleteMultilineVectors_GroupAtomicallyAndFailClosed()
    {
        var current = Modifier(
            "unique.minion-count.current",
            ("zombies", 1m, 1m),
            ("skeletons", 1m, 1m),
            ("spectres", 1m, 1m));
        var legacy = Modifier(
            "unique.minion-count.legacy",
            ("zombies", 1m, 1m),
            ("legacy_skeletons", 1m, 1m),
            ("spectres", 1m, 1m));
        var result = ImportSingle(
            """
                Test Wand
                Calling Wand
                Implicits: 0
                +1 to maximum number of Raised Zombies
                +1 to maximum number of Spectres
                +1 to maximum number of Skeletons
                """,
            generated: false,
            modifiers: [current, legacy],
            translations:
            [
                Translation("zombies", "zombies", "{0} to maximum number of Raised Zombies", "+#"),
                Translation("skeletons", "skeletons", "{0} to maximum number of Skeletons", "+#"),
                Translation("legacy-skeletons", "legacy_skeletons", "{0} to maximum number of Skeletons", "+#"),
                Translation("spectres", "spectres", "{0} to maximum number of Spectres", "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous,
            block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
        Assert.Equal(
            ["unique.minion-count.current", "unique.minion-count.legacy"],
            block.MechanicalMapping.ModifierIds);
        Assert.Empty(block.MechanicalMapping.StatIds);
        Assert.Null(block.MechanicalMapping.Provenance);
        var conflict = Assert.IsType<UniqueMechanicalConflictEvidence>(
            block.MechanicalMapping.ConflictEvidence);
        Assert.Equal(UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds, conflict.Kind);
        Assert.Equal(2, conflict.Candidates.Count);
        Assert.Equal(
            ["unique.minion-count.current", "unique.minion-count.legacy"],
            conflict.Candidates.Select(candidate => candidate.ModifierId).ToArray());
        Assert.Equal(
            2,
            conflict.Candidates
                .Select(candidate => string.Join('\u001f', candidate.StatIds))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.Contains(
            conflict.Candidates,
            candidate => candidate.StatIds.SequenceEqual(
                ["zombies", "skeletons", "spectres"]));
        Assert.Contains(
            conflict.Candidates,
            candidate => candidate.StatIds.SequenceEqual(
                ["zombies", "legacy_skeletons", "spectres"]));
        Assert.Contains(
            "ExactConflict: SameDisplayTextDifferentStatIds",
            block.MechanicalMapping.Diagnostic,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Import_TranslatedExactVector_WinsBeforeConflictingSourceTextFallback()
    {
        const string first = "+1 to maximum number of Raised Zombies";
        const string second = "+1 to maximum number of Spectres";
        var translated = Modifier(
            "unique.translated",
            ("zombies", 1m, 1m),
            ("spectres", 1m, 1m));
        var sourceOnly = Modifier(
            "unique.source-only",
            ("wrong_zombies", 1m, 1m),
            ("wrong_spectres", 1m, 1m)) with
        {
            SourceText = $"{first}\n{second}",
        };
        var result = ImportSingle(
            $"""
                Test Wand
                Calling Wand
                Implicits: 0
                {first}
                {second}
                """,
            generated: false,
            modifiers: [translated, sourceOnly],
            translations:
            [
                Translation("zombies", "zombies", "{0} to maximum number of Raised Zombies", "+#"),
                Translation("spectres", "spectres", "{0} to maximum number of Spectres", "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.translated"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["zombies", "spectres"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_ConflictingSourceTextVectors_DoNotReplaceIndependentlyMappedLines()
    {
        const string first = "+1 to maximum number of Raised Zombies";
        const string second = "+1 to maximum number of Spectres";
        var sourceText = $"{first}\n{second}";
        var firstCandidate = Modifier(
            "unique.source-one",
            ("first_zombies", 1m, 1m),
            ("first_spectres", 1m, 1m)) with { SourceText = sourceText };
        var secondCandidate = Modifier(
            "unique.source-two",
            ("second_zombies", 1m, 1m),
            ("second_spectres", 1m, 1m)) with { SourceText = sourceText };
        var result = ImportSingle(
            $"""
                Test Wand
                Calling Wand
                Implicits: 0
                {first}
                {second}
                """,
            generated: false,
            modifiers: [firstCandidate, secondCandidate],
            translations: []);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block =>
        {
            Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported,
                block.MechanicalMapping.Status);
            Assert.Null(block.Composition);
        });
    }

    [Fact]
    public void Import_PartialTranslationVector_DefaultZeroOutsideConditionRemainsUnsupported()
    {
        var modifier = Modifier("unique.test", "present_stat", 1, 1, "unique");
        var translation = new StatTranslationDefinition
        {
            Id = "requires-nonzero-missing-stat",
            StatIds = ["present_stat", "missing_stat"],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition { Index = 0, MinValue = 1 },
                        new StatTranslationCondition { Index = 1, MinValue = 1 },
                    ],
                    ValueFormats = ["ignore", "ignore"],
                    IndexHandlers =
                    [
                        new StatTranslationIndexHandler { Index = 0 },
                        new StatTranslationIndexHandler { Index = 1 },
                    ],
                    FormatLines = ["Requires both stats"],
                },
            ],
        };

        var result = ImportSingle(
            """
                Test Missing
                Topaz Ring
                Implicits: 0
                Requires both stats
                """,
            generated: false,
            modifiers: [modifier],
            translations: [translation]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported, block.MechanicalMapping.Status);
        Assert.Null(block.MechanicalMapping.Provenance);
    }

    [Fact]
    public void Import_PartialTranslationVector_DoesNotOverrideExistingStrictResolution()
    {
        var exact = Modifier("unique.exact", "exact_stat", 1, 1, "unique");
        var partial = Modifier("unique.partial", "partial_stat", 1, 1, "unique");
        var partialTranslation = new StatTranslationDefinition
        {
            Id = "partial-fallback",
            StatIds = ["partial_stat", "missing_stat"],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition { Index = 0, MinValue = 1, MaxValue = 1 },
                        new StatTranslationCondition { Index = 1, MinValue = 0, MaxValue = 0 },
                    ],
                    ValueFormats = ["ignore", "ignore"],
                    IndexHandlers =
                    [
                        new StatTranslationIndexHandler { Index = 0 },
                        new StatTranslationIndexHandler { Index = 1 },
                    ],
                    FormatLines = ["Existing Resolution"],
                },
            ],
        };

        var result = ImportSingle(
            """
                Test Existing
                Topaz Ring
                Implicits: 0
                Existing Resolution
                """,
            generated: false,
            modifiers: [exact, partial],
            translations:
            [
                Translation("exact", "exact_stat", "Existing Resolution", "ignore"),
                partialTranslation,
            ]);

        var mapping = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks).MechanicalMapping;
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, mapping.Status);
        Assert.Equal(["unique.exact"], mapping.ModifierIds);
        Assert.Null(mapping.Provenance);
    }

    [Fact]
    public void Import_ReviewedLocalPropertyCapabilityEliminatesImpossibleNonWeaponCandidate()
    {
        var global = Modifier("unique.global", "attack_speed", 10, 10, "unique");
        var local = Modifier("unique.local", "local_attack_speed", 10, 10, "unique");
        var result = ImportSingle(
            """
                Test Speed
                Topaz Ring
                Implicits: 0
                10% increased Attack Speed
                """,
            generated: false,
            modifiers: [global, local],
            translations:
            [
                Translation("global", "attack_speed", "{0}% increased Attack Speed", "#"),
                Translation("local", "local_attack_speed", "{0}% increased Attack Speed", "#"),
            ],
            baseItems: [new ItemBaseRecord { Name = "Topaz Ring", Domain = "item" }],
            itemPropertySemantics:
            [
                new ItemPropertySemanticDescriptor
                {
                    Id = "weapon.attack-speed.local",
                    OrderedStatIds = ["local_attack_speed"],
                    Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
                    Contributions =
                    [
                        new ItemPropertyContribution
                        {
                            Targets = [ItemPropertyTarget.AttacksPerSecond],
                            Operation = ItemPropertyOperation.IncreasedPercent,
                        },
                    ],
                },
            ]);

        var mapping = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks).MechanicalMapping;
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, mapping.Status);
        Assert.Equal(["unique.global"], mapping.ModifierIds);
        Assert.Contains("base-item-property-capability", mapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_MetadataBeforeBaseAndItemStateLines_DoNotBecomeModifierBlocks()
    {
        var result = ImportSingle(
            """
                Test Crown
                Shaper Item
                League: Test League
                Source: Test Source
                Iron Hat
                Requires Level: 20
                Implicits: 0
                +(10-20) to maximum Life
                {variant:1}Corrupted
                """,
            generated: false,
            modifiers: [Modifier("unique.life", "maximum_life", 10, 20, "unique")],
            translations: [Translation("life", "maximum_life", "{0} to maximum Life", "+#")]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal("Iron Hat", version.BaseType);
        var block = Assert.Single(version.ModifierBlocks);
        Assert.Equal(["+(10-20) to maximum Life"], block.Lines);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Single(block.SourceObservationIds);
    }

    [Fact]
    public void Import_NonContiguousTranslationVectorsAndIgnoredStats_KeepAuthenticBlockAtomic()
    {
        var modifier = new ModifierDefinition
        {
            Id = "unique.timeless",
            GroupId = "Timeless",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "misc",
            Stats =
            [
                new ModifierStat { Index = 0, StatId = "version", MinValue = 2, MaxValue = 2 },
                new ModifierStat { Index = 1, StatId = "seed", MinValue = 10000, MaxValue = 18000 },
                new ModifierStat { Index = 2, StatId = "keystone", MinValue = 1, MaxValue = 3 },
                new ModifierStat { Index = 3, StatId = "radius", MinValue = 1500, MaxValue = 1500 },
                new ModifierStat { Index = 4, StatId = "historic", MinValue = 1, MaxValue = 1 },
                new ModifierStat { Index = 5, StatId = "revision", MinValue = 1, MaxValue = 1 },
            ],
        };
        var result = ImportSingle(
            """
                Test Pride
                Timeless Jewel
                Radius: Large
                Implicits: 0
                Commanded leadership over (10000-18000) warriors under Akoya
                Passives in radius are Conquered by the Karui
                Historic
                """,
            generated: false,
            modifiers: [modifier],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "timeless-seed",
                    StatIds = ["version", "seed", "keystone", "revision"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition { Index = 0, MinValue = 2, MaxValue = 2 },
                                new StatTranslationCondition { Index = 1 },
                                new StatTranslationCondition { Index = 2, MinValue = 3, MaxValue = 3 },
                                new StatTranslationCondition { Index = 3 },
                            ],
                            ValueFormats = ["ignore", "#", "ignore", "ignore"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler { Index = 0 },
                                new StatTranslationIndexHandler { Index = 1 },
                                new StatTranslationIndexHandler { Index = 2 },
                                new StatTranslationIndexHandler { Index = 3 },
                            ],
                            FormatLines =
                            [
                                "Commanded leadership over {1} warriors under Akoya",
                                "Passives in radius are Conquered by the Karui",
                            ],
                        },
                    ],
                },
                new StatTranslationDefinition
                {
                    Id = "timeless-historic",
                    StatIds = ["historic"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["ignore"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                            FormatLines = ["Historic"],
                        },
                    ],
                },
            ],
            baseItems:
            [
                new ItemBaseRecord { Name = "Timeless Jewel", Domain = "misc" },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.timeless"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["version", "seed", "keystone", "radius", "historic", "revision"],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_ExactNumericValueEvidence_PrecedesEquivalentLiteralIgnoredRendering()
    {
        var literal = Modifier(
            "unique.literal-bleed",
            "fixed_bleed_chance",
            1,
            1,
            "unique");
        var numeric = Modifier(
            "unique.numeric-bleed",
            "bleed_chance_percent",
            50,
            50,
            "unique");
        var result = ImportSingle(
            """
                Test Axe
                Headsman Axe
                Implicits: 0
                50% chance to cause Bleeding on Hit
                """,
            generated: false,
            modifiers: [literal, numeric],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "literal-bleed",
                    StatIds = ["fixed_bleed_chance"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["ignore"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                            FormatLines = ["50% chance to cause Bleeding on Hit"],
                        },
                    ],
                },
                Translation(
                    "numeric-bleed",
                    "bleed_chance_percent",
                    "{0}% chance to cause Bleeding on Hit",
                    "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.numeric-bleed"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["bleed_chance_percent"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_CompositeCurrentAndPreLabels_SelectOnlyCurrentVariants()
    {
        var result = ImportSingle(
            """
                Test Fostering
                Test Armour
                Variant: Rhoa Pre 3.26
                Variant: Snake Pre 3.26
                Variant: Rhoa Current
                Variant: Snake Current
                Implicits: 0
                {variant:1}10% increased Rhoa Damage
                {variant:2}10% increased Snake Damage
                {variant:3}20% increased Rhoa Damage
                {variant:4}20% increased Snake Damage
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.rhoa-current", "rhoa_damage", 20, 20, "unique"),
                Modifier("unique.snake-current", "snake_damage", 20, 20, "unique"),
                Modifier("unique.rhoa-old", "rhoa_damage", 10, 10, "unique"),
                Modifier("unique.snake-old", "snake_damage", 10, 10, "unique"),
            ],
            translations:
            [
                Translation("rhoa", "rhoa_damage", "{0}% increased Rhoa Damage", "#"),
                Translation("snake", "snake_damage", "{0}% increased Snake Damage", "#"),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count(version => version.Role == UniqueItemVersionRole.Current));
        Assert.Equal(2, versions.Count(version => version.Role == UniqueItemVersionRole.Historical));
        Assert.All(versions, version => Assert.Single(version.ModifierBlocks));
        Assert.Equal(
            ["20% increased Rhoa Damage", "20% increased Snake Damage"],
            versions.Where(version => version.Role == UniqueItemVersionRole.Current)
                .SelectMany(version => version.ModifierBlocks)
                .SelectMany(block => block.Lines)
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void Import_ExplicitCurrentSibling_ClassifiesBareAndPunctuatedPatchLabelsAsHistorical()
    {
        var result = ImportSingle(
            """
                Test History
                Test Armour
                Variant: 3.19.0
                Variant: Pre.3.20.0
                Variant: Current
                Implicits: 0
                {variant:1}10% increased Armour
                {variant:2}20% increased Armour
                {variant:3}30% increased Armour
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.history-one", "armour", 10, 10, "unique"),
                Modifier("unique.history-two", "armour", 20, 20, "unique"),
                Modifier("unique.current", "armour", 30, 30, "unique"),
            ],
            translations:
            [
                Translation("armour", "armour", "{0}% increased Armour", "#"),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count(version => version.Role == UniqueItemVersionRole.Historical));
        Assert.Single(versions, version => version.Role == UniqueItemVersionRole.Current);
        Assert.All(versions.Where(version => version.Role == UniqueItemVersionRole.Historical), version =>
            Assert.Contains("histor", version.RoleDecisionReason, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Import_CurrentRePoeBaseDiacriticAlias_CanonicalizesBaseAndPreservesSourceText()
    {
        var result = ImportSingle(
            """
                Test Staff
                Maelstrom Staff
                Variant: Current
                Implicits: 0
                """,
            generated: false,
            modifiers: [],
            translations: [],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Weapons/TwoHandWeapons/Staves/Staff17",
                    Name = "Maelström Staff",
                    ItemClass = "Warstaff",
                },
            ]);

        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
        var identity = Assert.Single(result.Catalog!.Items);
        Assert.Equal("ordinary|test staff", identity.CanonicalIdentityKey);
        var version = Assert.Single(identity.Versions);
        Assert.Equal("Maelström Staff", version.BaseType);
        Assert.Equal("Maelstrom Staff", version.SourceBaseType);
        Assert.Equal(UniqueSourceIdentityNormalizer.CanonicalRule, version.BaseTypeNormalizationRule);
        Assert.Equal(["Metadata/Items/Weapons/TwoHandWeapons/Staves/Staff17"], version.RePoeBaseItemIds);
    }

    [Fact]
    public void Import_CurrentRePoeBaseCanonicalCollision_FailsClosed()
    {
        var result = ImportSingle(
            """
                Test Staff
                Ä Base
                Variant: Current
                Implicits: 0
                """,
            generated: false,
            modifiers: [],
            translations: [],
            baseItems:
            [
                new ItemBaseRecord { Id = "base-a", Name = "A Base", ItemClass = "Staff" },
                new ItemBaseRecord { Id = "base-accent", Name = "Á Base", ItemClass = "Staff" },
            ]);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.PoBUniqueBaseNormalizationCollision &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal("Ä Base", version.BaseType);
        Assert.Empty(version.RePoeBaseItemIds);
    }

    [Fact]
    public void Import_DistinctSourceNamesWithSameCanonicalKey_FailsClosedWithoutMerging()
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-collision-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new { sourcePath = "one.lua", generated = false, raw = "Tést Item\nTest Base" },
                    new { sourcePath = "two.lua", generated = false, raw = "Test Item\nTest Base" },
                },
            }));

            var result = new PoBUniqueCatalogImporter().Import(
                path,
                "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                "v2.67.2",
                "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                [],
                []);

            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == RePoeImportDiagnosticCodes.PoBUniqueIdentityNormalizationCollision &&
                diagnostic.Severity == ImportDiagnosticSeverity.Error);
            Assert.Equal(2, result.Catalog!.Items.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_UnlabelledNonGeneratedAlternatives_AreDistinctCurrentVersions()
    {
        var result = ImportSingle(
            """
                Test Voices
                Large Cluster Jewel
                Variant: Adds 1 Small Passive Skill
                Variant: Adds 3 Small Passive Skills
                Implicits: 0
                {variant:1}Adds 1 Small Passive Skill which grants nothing
                {variant:2}Adds 3 Small Passive Skills which grant nothing
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.one", "small_passives", 1, 1, "unique"),
                Modifier("unique.three", "small_passives", 3, 3, "unique"),
            ],
            translations:
            [
                Translation("passives", "small_passives",
                    "Adds {0} Small Passive Skills which grants nothing", "#"),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version =>
        {
            Assert.Equal(UniqueItemVersionRole.Current, version.Role);
            Assert.Single(version.ModifierBlocks);
        });
    }

    [Theory]
    [InlineData("display_indexable_skill")]
    [InlineData("passive_hash")]
    public void Import_GeneratedStructuredOptionMechanic_UsesEvaluatedConcreteTextAndUniqueStatVector_WhenModifierStatsAreReversed(
        string structuredHandler)
    {
        var dynamicModifiers = new ModifierDefinition[]
        {
            new()
            {
                Id = "unique.random-skill",
                GroupId = "RandomSkill",
                GenerationType = ModifierGenerationType.Implicit,
                SourceGenerationType = "unique",
                Domain = "item",
                Stats =
                [
                    new ModifierStat
                    {
                        Index = 0,
                        StatId = "random_skill_index",
                        MinValue = 1,
                        MaxValue = 287,
                    },
                    new ModifierStat
                    {
                        Index = 1,
                        StatId = "random_skill_level",
                        MinValue = 3,
                        MaxValue = 3,
                    },
                ],
            },
        };
        var dynamicTranslations = new StatTranslationDefinition[]
        {
            new()
            {
                Id = "random-skill",
                StatIds = ["random_skill_level", "random_skill_index"],
                Variants =
                [
                    new StatTranslationVariant
                    {
                        Conditions =
                        [
                            new StatTranslationCondition { Index = 0, MinValue = 1 },
                            new StatTranslationCondition { Index = 1 },
                        ],
                        ValueFormats = ["#", "#"],
                        IndexHandlers =
                        [
                            new StatTranslationIndexHandler { Index = 0 },
                            new StatTranslationIndexHandler
                            {
                                Index = 1,
                                Handlers = [structuredHandler],
                            },
                        ],
                        FormatLines = ["+{0} to Level of all {1} Gems"],
                    },
                ],
            },
        };
        var result = ImportSingle(
            """
                Replica Test Flight
                Onyx Amulet
                Implicits: 0
                +3 to Level of all Absolution Gems
                """,
            generated: true,
            modifiers: dynamicModifiers,
            translations: dynamicTranslations);

        var catalog = Assert.IsType<UniqueItemCatalog>(result.Catalog);
        var source = Assert.Single(catalog.SourceObservations);
        Assert.True(source.IsGenerated);
        var block = Assert.Single(Assert.Single(Assert.Single(catalog.Items).Versions).ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.random-skill"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["random_skill_index", "random_skill_level"], block.MechanicalMapping.StatIds);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.Contains(provenance.Translations.SelectMany(evidence => evidence.IndexHandlers),
            handler => handler.Handlers.Contains(structuredHandler));
    }

    [Fact]
    public void Import_GeneratedPassiveHashOption_ResolvesExactCurrentUniqueMechanic()
    {
        var result = ImportSingle(
            """
                Test Generated Jewel
                Crimson Jewel
                Variant: Test Passive
                Implicits: 0
                {variant:1}Allocates Test Passive if you have the matching modifier on Test Pair
                """,
            generated: true,
            modifiers:
            [
                Modifier("unique.passive-option", "unique_passive_hash", 1, 1, "unique"),
                Modifier("generic.passive-option", "generic_passive_hash", 0, 0, "unique"),
            ],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "passive-option",
                    StatIds = ["unique_passive_hash"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = ["passive_hash"],
                                },
                            ],
                            FormatLines =
                            [
                                "Allocates {0} if you have the matching modifier on Test Pair",
                            ],
                        },
                    ],
                },
                new StatTranslationDefinition
                {
                    Id = "generic-passive-option",
                    StatIds = ["generic_passive_hash"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = ["passive_hash"],
                                },
                            ],
                            FormatLines = ["Allocates {0}"],
                        },
                    ],
                },
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Jewels/Test",
                    Name = "Crimson Jewel",
                    Domain = "misc",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, block.SourceSemantics);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.passive-option"], block.MechanicalMapping.ModifierIds);
        Assert.Contains(block.MechanicalMapping.Provenance!.Translations.SelectMany(evidence => evidence.IndexHandlers),
            handler => handler.Handlers.Contains("passive_hash"));
    }

    [Fact]
    public void Import_GeneratedCandidatePool_PreservesFixedBlocksAndDistinctRollCandidates()
    {
        var result = ImportSingle(
            """
                Test Generated Crown
                Great Crown
                Selected Variant: 2
                Variant: Low
                Variant: High
                Implicits: 0
                +30 to all Attributes
                {variant:1}Socketed Gems are Supported by Level (1-10) Inspiration
                {variant:2}Socketed Gems are Supported by Level (25-35) Inspiration
                """,
            generated: true,
            modifiers:
            [
                Modifier("unique.attributes", "all_attributes", 30, 30, "unique"),
                Modifier("unique.inspiration.low", "inspiration_level", 1, 10, "unique"),
                Modifier("unique.inspiration.high", "inspiration_level", 25, 35, "unique"),
            ],
            translations:
            [
                Translation("attributes", "all_attributes", "{0} to all Attributes", "+#"),
                Translation("inspiration", "inspiration_level",
                    "Socketed Gems are Supported by Level {0} Inspiration", "#"),
            ]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal(1, version.GeneratedCandidateSelectionLimit);
        Assert.Equal(3, version.ModifierBlocks.Count);
        var fixedBlock = Assert.Single(version.ModifierBlocks, block =>
            block.SourceSemantics == UniqueModifierSourceSemantics.Fixed);
        Assert.Empty(fixedBlock.CandidatePoolMembershipIds);
        var candidates = version.ModifierBlocks
            .Where(block => block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate)
            .OrderBy(block => block.Lines[0], StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, candidates.Length);
        Assert.Equal(2, candidates.Select(block => block.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(candidates, block => Assert.Single(block.CandidatePoolMembershipIds));
        Assert.NotEqual(
            candidates[0].CandidatePoolMembershipIds[0],
            candidates[1].CandidatePoolMembershipIds[0]);
    }

    [Fact]
    public void Import_NonGeneratedMixedAxis_KeepsOptionSeparateFromGeneratedCandidateSemantics()
    {
        var result = ImportSingle(
            """
                Test Mixed Gloves
                Steelscale Gauntlets
                Has Alt Variant: true
                Selected Variant: 1
                Selected Alt Variant: 2
                Variant: Current
                Variant: Socket Option
                Implicits: 0
                {variant:1}(5-10)% increased Attack Speed
                {variant:2}Has 2 Abyssal Sockets
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.attack-speed", "attack_speed", 5, 10, "unique"),
                Modifier("unique.sockets", "abyssal_sockets", 2, 2, "unique"),
            ],
            translations:
            [
                Translation("attack-speed", "attack_speed", "{0}% increased Attack Speed", "#"),
                Translation("sockets", "abyssal_sockets", "Has {0} Abyssal Sockets", "#"),
            ]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal(0, version.GeneratedCandidateSelectionLimit);
        Assert.Equal(
            UniqueModifierSourceSemantics.Fixed,
            Assert.Single(version.ModifierBlocks, block => block.Lines[0].Contains("Attack Speed"))
                .SourceSemantics);
        var optionBlock = Assert.Single(version.ModifierBlocks,
            block => block.Lines[0].Contains("Abyssal Sockets"));
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, optionBlock.SourceSemantics);
        var axis = Assert.Single(version.OptionAxes);
        Assert.Equal(1, axis.SelectionLimit);
        var membership = Assert.Single(optionBlock.OptionChoiceMemberships);
        Assert.Equal(axis.Id, membership.OptionAxisId);
        Assert.Contains(axis.Choices, choice => choice.Id == membership.OptionChoiceId);
    }

    [Fact]
    public void Import_CoSelectableContextQualifiedOptions_ShareAtomicCurrentVersionAndRetainProvenance()
    {
        var result = ImportSingle(
            """
                Test Circle
                Ruby Ring
                Has Alt Variant: true
                Selected Variant: 2
                Selected Alt Variant: 5
                Variant: Skill Reservation (Pre 3.11.0)
                Variant: Skill Reservation (Current)
                Variant: Fire Damage
                Variant: Buff Effect (Pre 3.11.0)
                Variant: Buff Effect (Current)
                Implicits: 0
                {variant:1}Herald has (40-50)% reduced Reservation
                {variant:2}Herald has (30-40)% increased Mana Reservation Efficiency
                {variant:4}Herald has (40-50)% increased Buff Effect
                {variant:5}Herald has (50-60)% increased Buff Effect
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.reservation-old", "reservation_old", 40, 50, "unique"),
                Modifier("unique.reservation-current", "reservation_current", 30, 40, "unique"),
                Modifier("unique.reservation-local", "reservation_local", 30, 40, "unique"),
                Modifier("unique.buff-old", "buff_old", 40, 50, "unique"),
                Modifier("unique.buff-current", "buff_current", 50, 60, "unique"),
            ],
            translations:
            [
                Translation("reservation-old", "reservation_old", "Herald has {0}% reduced Reservation", "#"),
                Translation("reservation-current", "reservation_current", "Herald has {0}% increased Mana Reservation Efficiency", "#"),
                Translation("reservation-local", "reservation_local", "Herald has {0}% increased Mana Reservation Efficiency", "#"),
                Translation("buff-old", "buff_old", "Herald has {0}% increased Buff Effect", "#"),
                Translation("buff-current", "buff_current", "Herald has {0}% increased Buff Effect", "#"),
            ],
            stats:
            [
                new StatDefinition { Id = "reservation_old", IsLocal = false },
                new StatDefinition { Id = "reservation_current", IsLocal = false },
                new StatDefinition { Id = "reservation_local", IsLocal = true },
                new StatDefinition { Id = "buff_old", IsLocal = false },
                new StatDefinition { Id = "buff_current", IsLocal = false },
            ],
            sourceLocality: UniqueModifierSemanticLocality.Global,
            sourceLine: "Herald has (30-40)% increased Mana Reservation Efficiency",
            sourceBaseType: "Ruby Ring");

        var identity = Assert.Single(result.Catalog!.Items);
        Assert.Equal(2, identity.Versions.Count);
        var current = Assert.Single(identity.Versions,
            version => version.Role == UniqueItemVersionRole.Current);
        Assert.Equal("Current", current.Label);
        Assert.Equal(2, current.ModifierBlocks.Count);
        var axis = Assert.Single(current.OptionAxes);
        Assert.Equal(2, axis.SelectionLimit);
        Assert.Equal(3, axis.Choices.Count);
        Assert.All(current.ModifierBlocks, block =>
        {
            Assert.Equal(UniqueModifierSourceSemantics.Fixed, block.SourceSemantics);
            Assert.Single(block.SourceObservationIds);
            var membership = Assert.Single(block.OptionChoiceMemberships);
            Assert.Equal(axis.Id, membership.OptionAxisId);
            Assert.Single(membership.SourceObservationIds);
            Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
            Assert.Single(block.MechanicalMapping.ModifierIds);
        });
        var mechanicallyProven = Assert.Single(current.ModifierBlocks,
            block => block.MechanicalMapping.Provenance is not null);
        Assert.NotEmpty(mechanicallyProven.MechanicalMapping.Provenance!.Translations);
        Assert.Equal(2, current.ModifierBlocks
            .SelectMany(block => block.OptionChoiceMemberships)
            .Select(membership => membership.OptionChoiceId)
            .Distinct(StringComparer.Ordinal)
            .Count());
    }

    [Fact]
    public void Import_SplitStyleChoices_AreOneCurrentVersionWithTwoCoSelectableAffixes()
    {
        var result = ImportSingle(
            """
                Test Split
                Crimson Jewel
                Has Alt Variant: true
                Selected Variant: 2
                Selected Alt Variant: 3
                Variant: Strength
                Variant: Intelligence
                Variant: Energy Shield
                Limited to: 2
                Implicits: 0
                This Jewel's Socket has 25% increased effect per Allocated Passive Skill between it and your Class' starting location
                {variant:1}+5 to Strength
                {variant:2}+5 to Intelligence
                {variant:3}+5 to maximum Energy Shield
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.path-effect", "path_effect", 25, 25, "unique"),
                Modifier("unique.strength", "strength", 5, 5, "unique"),
                Modifier("unique.intelligence", "intelligence", 5, 5, "unique"),
                Modifier("unique.energy-shield", "energy_shield", 5, 5, "unique"),
            ],
            translations:
            [
                Translation("path-effect", "path_effect", "This Jewel's Socket has {0}% increased effect per Allocated Passive Skill between it and your Class' starting location", "#"),
                Translation("strength", "strength", "{0} to Strength", "+#"),
                Translation("intelligence", "intelligence", "{0} to Intelligence", "+#"),
                Translation("energy-shield", "energy_shield", "{0} to maximum Energy Shield", "+#"),
            ]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal(UniqueItemVersionRole.Current, version.Role);
        var axis = Assert.Single(version.OptionAxes);
        Assert.Equal(2, axis.SelectionLimit);
        Assert.Equal(3, axis.Choices.Count);
        var intelligence = Assert.Single(version.ModifierBlocks,
            block => block.Lines.Contains("+5 to Intelligence"));
        var energyShield = Assert.Single(version.ModifierBlocks,
            block => block.Lines.Contains("+5 to maximum Energy Shield"));
        Assert.Single(intelligence.OptionChoiceMemberships);
        Assert.Single(energyShield.OptionChoiceMemberships);
        Assert.NotEqual(
            intelligence.OptionChoiceMemberships[0].OptionChoiceId,
            energyShield.OptionChoiceMemberships[0].OptionChoiceId);
        Assert.Equal(axis.Id, intelligence.OptionChoiceMemberships[0].OptionAxisId);
        Assert.Equal(axis.Id, energyShield.OptionChoiceMemberships[0].OptionAxisId);
    }

    [Fact]
    public void Import_ContextQualifiedReservationChoice_RemainsMechanicallyAmbiguousOnConflictingStats()
    {
        var result = ImportSingle(
            """
                Test Circle Ambiguity
                Sapphire Ring
                Has Alt Variant: true
                Selected Variant: 2
                Selected Alt Variant: 4
                Variant: Skill Reservation (Pre 3.11.0)
                Variant: Skill Reservation (Current)
                Variant: Buff Effect (Pre 3.11.0)
                Variant: Buff Effect (Current)
                Implicits: 0
                {variant:1}Herald has (40-50)% reduced Reservation
                {variant:2}Herald has (30-40)% increased Mana Reservation Efficiency
                {variant:3}Herald has (40-50)% increased Buff Effect
                {variant:4}Herald has (50-60)% increased Buff Effect
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.reservation-first", "reservation_first", 30, 40, "unique"),
                Modifier("unique.reservation-second", "reservation_second", 30, 40, "unique"),
                Modifier("unique.buff-current", "buff_current", 50, 60, "unique"),
            ],
            translations:
            [
                Translation("reservation-first", "reservation_first", "Herald has {0}% increased Mana Reservation Efficiency", "#"),
                Translation("reservation-second", "reservation_second", "Herald has {0}% increased Mana Reservation Efficiency", "#"),
                Translation("buff-current", "buff_current", "Herald has {0}% increased Buff Effect", "#"),
            ]);

        var current = Assert.Single(Assert.Single(result.Catalog!.Items).Versions,
            version => version.Role == UniqueItemVersionRole.Current);
        var reservation = Assert.Single(current.ModifierBlocks,
            block => block.Lines[0].Contains("Reservation Efficiency", StringComparison.Ordinal));
        Assert.Single(reservation.OptionChoiceMemberships);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous,
            reservation.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT",
            reservation.MechanicalMapping.DiagnosticCode);
        var buff = Assert.Single(current.ModifierBlocks,
            block => block.Lines[0].Contains("Buff Effect", StringComparison.Ordinal));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, buff.MechanicalMapping.Status);
    }

    [Fact]
    public void Import_TrueAtomicVersions_StayMutuallyExclusiveAndHaveNoOptionAxis()
    {
        var result = ImportSingle(
            """
                Test Flask
                Diamond Flask
                Variant: Pre 3.15.0
                Variant: Current
                Implicits: 0
                {variant:1}30% increased Chaos Damage
                {variant:2}250% increased Chaos Damage
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.chaos-old", "chaos_old", 30, 30, "unique"),
                Modifier("unique.chaos-current", "chaos_current", 250, 250, "unique"),
            ],
            translations:
            [
                Translation("chaos-old", "chaos_old", "{0}% increased Chaos Damage", "#"),
                Translation("chaos-current", "chaos_current", "{0}% increased Chaos Damage", "#"),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version =>
        {
            Assert.Empty(version.OptionAxes);
            Assert.Single(version.ModifierBlocks);
            Assert.Empty(version.ModifierBlocks[0].OptionChoiceMemberships);
        });
    }

    [Fact]
    public void Import_GeneratedMultilineCandidate_RemainsOneAtomicPoolMember()
    {
        var result = ImportSingle(
            """
                Test Generated Staff
                Serpentine Staff
                Selected Variant: 1
                Variant: Chaos
                Implicits: 0
                {variant:1}(105-120)% increased Chaos Damage
                {variant:1}Chaos Skills have (26-30)% increased Skill Effect Duration
                """,
            generated: true,
            modifiers:
            [
                Modifier(
                    "unique.chaos-pair",
                    ("chaos_damage", 105m, 120m),
                    ("chaos_duration", 26m, 30m)),
            ],
            translations:
            [
                Translation("chaos-damage", "chaos_damage", "{0}% increased Chaos Damage", "#"),
                Translation("chaos-duration", "chaos_duration",
                    "Chaos Skills have {0}% increased Skill Effect Duration", "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, block.SourceSemantics);
        Assert.Single(block.CandidatePoolMembershipIds);
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
    }

    [Fact]
    public void Import_StaticExactUniqueRendering_PrecedesCompatibleDynamicDisplayPattern()
    {
        var fixedCandidate = Modifier(
            "unique.fixed-physical",
            "physical_spell_level",
            3,
            3,
            "unique");
        var dynamicCandidate = new ModifierDefinition
        {
            Id = "unique.random-skill",
            GroupId = "RandomSkill",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "item",
            Stats =
            [
                new ModifierStat { Index = 0, StatId = "random_skill_level", MinValue = 3, MaxValue = 3 },
                new ModifierStat { Index = 1, StatId = "random_skill_index", MinValue = 1, MaxValue = 287 },
            ],
        };
        var result = ImportSingle(
            """
                Test Dagger
                Ezomyte Dagger
                Implicits: 0
                +3 to Level of all Physical Spell Skill Gems
                """,
            generated: false,
            modifiers: [fixedCandidate, dynamicCandidate],
            translations:
            [
                Translation(
                    "physical-spell-level",
                    "physical_spell_level",
                    "{0} to Level of all Physical Spell Skill Gems",
                    "+#"),
                new StatTranslationDefinition
                {
                    Id = "random-skill",
                    StatIds = ["random_skill_level", "random_skill_index"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition { Index = 0, MinValue = 1 },
                                new StatTranslationCondition { Index = 1 },
                            ],
                            ValueFormats = ["#", "#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler { Index = 0 },
                                new StatTranslationIndexHandler
                                {
                                    Index = 1,
                                    Handlers = ["display_indexable_skill"],
                                },
                            ],
                            FormatLines = ["+{0} to Level of all {1} Gems"],
                        },
                    ],
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.fixed-physical"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["physical_spell_level"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedOptionDirective_PrefersMatchingDynamicMechanicOverCoincidentalStaticText()
    {
        var fixedCandidate = Modifier(
            "unique.fixed-absolution",
            "fixed_absolution_level",
            3,
            3,
            "unique");
        var dynamicCandidate = new ModifierDefinition
        {
            Id = "unique.random-skill",
            GroupId = "RandomSkill",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "item",
            Stats =
            [
                new ModifierStat { Index = 0, StatId = "random_skill_level", MinValue = 3, MaxValue = 3 },
                new ModifierStat { Index = 1, StatId = "random_skill_index", MinValue = 1, MaxValue = 287 },
            ],
        };
        var result = ImportSingle(
            """
                Replica Test Flight
                Onyx Amulet
                Variant: Current
                Variant: Absolution
                Implicits: 0
                {variant:2}+3 to Level of all Absolution Gems
                """,
            generated: true,
            modifiers: [fixedCandidate, dynamicCandidate],
            translations:
            [
                Translation(
                    "fixed-absolution",
                    "fixed_absolution_level",
                    "{0} to Level of all Absolution Gems",
                    "+#"),
                DynamicSkillTranslation(),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.random-skill"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["random_skill_level", "random_skill_index"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedOptions_RetainExplicitCurrentAndHistoricalObservations()
    {
        var result = ImportSingle(
            """
                Replica Test Flight
                Onyx Amulet
                Variant: Pre 3.23.0
                Variant: Current
                Variant: Absolution
                Implicits: 0
                {variant:1}10% increased Reservation Efficiency of Skills
                {variant:2}5% increased Reservation Efficiency of Skills
                {variant:3}+3 to Level of all Absolution Gems
                """,
            generated: true,
            modifiers:
            [
                Modifier("unique.old-reservation", "reservation", 10, 10, "unique"),
                Modifier("unique.current-reservation", "reservation", 5, 5, "unique"),
                new ModifierDefinition
                {
                    Id = "unique.random-skill",
                    GroupId = "RandomSkill",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat { Index = 0, StatId = "random_skill_level", MinValue = 3, MaxValue = 3 },
                        new ModifierStat { Index = 1, StatId = "random_skill_index", MinValue = 1, MaxValue = 287 },
                    ],
                },
            ],
            translations:
            [
                Translation(
                    "reservation",
                    "reservation",
                    "{0}% increased Reservation Efficiency of Skills",
                    "#"),
                DynamicSkillTranslation(),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count);
        Assert.Contains(versions, version => version.Role == UniqueItemVersionRole.Current);
        Assert.Contains(versions, version => version.Role == UniqueItemVersionRole.Historical);
        Assert.All(versions, version => Assert.Contains(version.ModifierBlocks, block =>
            block.Lines.Contains("+3 to Level of all Absolution Gems")));
    }

    [Fact]
    public void Import_UnprovenGeneratedMechanic_RemainsUnsupportedWithSpecificReason()
    {
        var result = ImportSingle(
            """
                Generated Test
                Test Base
                Implicits: 0
                This mechanic has no canonical observation
                """,
            generated: true,
            modifiers: [],
            translations: []);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported, block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_GENERATED_MECHANICS_NOT_FOUND", block.MechanicalMapping.DiagnosticCode);
        Assert.Contains("evaluated generated PoB", block.MechanicalMapping.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_EvaluatedVariantsAndGeneratedReplica_RetainsProvenanceAndMechanics()
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-uniques-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new object[]
                {
                    new
                    {
                        uniqueType = "helmet",
                        sourcePath = "Data/Uniques/helmet.lua",
                        generated = false,
                        raw = """
                            Test Crown
                            {variant:1}Iron Hat
                            {variant:2}Leather Cap
                            Variant: Pre 3.29.0
                            Variant: Current
                            Implicits: 0
                            {variant:1}+(20-30) to maximum Life
                            {variant:2}+(40-50) to maximum Life
                            Cannot be Stunned
                            """,
                    },
                    new
                    {
                        uniqueType = "generated",
                        sourcePath = "Data/Uniques/Special/Generated.lua",
                        generated = true,
                        raw = """
                            Replica Test Crown
                            Iron Hat
                            Implicits: 0
                            +(10-20) to maximum Life
                            """,
                    },
                },
            }));

            var modifiers = new[]
            {
                Modifier("unique.life", "maximum_life", 10, 50),
                Modifier("unique.stun", "cannot_be_stunned", 1, 1),
            };
            var translations = new[]
            {
                Translation("life", "maximum_life", "+{0} to maximum Life", "+#"),
                Translation("stun", "cannot_be_stunned", "Cannot be Stunned"),
            };

            var result = new PoBUniqueCatalogImporter().Import(
                path,
                "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                "v2.67.2",
                "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                modifiers,
                translations);

            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Severity == ImportDiagnosticSeverity.Error);
            Assert.Equal(2, result.RecordsImported);
            var catalog = Assert.IsType<UniqueItemCatalog>(result.Catalog);
            var ordinary = Assert.Single(catalog.Items, item => item.CanonicalName == "Test Crown");
            Assert.Equal(UniqueItemKind.Ordinary, ordinary.Kind);
            Assert.Equal(["Iron Hat", "Leather Cap"], ordinary.BaseTypeEvidence);
            Assert.Collection(
                ordinary.Versions.OrderBy(version => version.Role),
                current =>
                {
                    Assert.Equal(UniqueItemVersionRole.Current, current.Role);
                    Assert.Equal("Leather Cap", current.BaseType);
                },
                historical =>
                {
                    Assert.Equal(UniqueItemVersionRole.Historical, historical.Role);
                    Assert.Equal("Iron Hat", historical.BaseType);
                });
            Assert.All(ordinary.Versions.SelectMany(version => version.ModifierBlocks), block =>
                Assert.NotEqual(UniqueModifierMechanicalMappingStatus.Unknown, block.MechanicalMapping.Status));

            var replica = Assert.Single(catalog.Items, item => item.CanonicalName == "Replica Test Crown");
            Assert.Equal(UniqueItemKind.Replica, replica.Kind);
            var replicaSource = Assert.Single(catalog.SourceObservations, source =>
                replica.SourceObservationIds.Contains(source.Id!));
            Assert.True(replicaSource.IsGenerated);
            Assert.Equal("Data/Uniques/Special/Generated.lua", replicaSource.SourcePath);
            Assert.Equal(64, replicaSource.RawEntrySha256!.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PoBUniqueCatalogImportResult ImportSingle(
        string raw,
        bool generated,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord>? baseItems = null,
        IReadOnlyList<ItemPropertySemanticDescriptor>? itemPropertySemantics = null,
        IReadOnlyList<StatDefinition>? stats = null,
        UniqueModifierSemanticLocality? sourceLocality = null,
        string? sourceLine = null,
        string? sourceBaseType = null,
        int sourceLineIndex = 0,
        string sourceBlockKind = "unique")
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-uniques-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = generated ? "generated" : "ring",
                        sourcePath = generated
                            ? "Data/Uniques/Special/Generated.lua"
                            : "Data/Uniques/ring.lua",
                        generated,
                        raw,
                        semanticFingerprints = sourceLocality.HasValue
                            ? new[]
                            {
                                new
                                {
                                    kind = sourceBlockKind,
                                    lineIndex = sourceLineIndex,
                                    line = sourceLine,
                                    baseType = sourceBaseType,
                                    locality = sourceLocality.Value.ToString().ToLowerInvariant(),
                                    evidenceMethod = "pob-item-context-v1",
                                },
                            }
                            : null,
                    },
                },
            }));
            return new PoBUniqueCatalogImporter().Import(
                path,
                "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                "v2.67.2",
                "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                modifiers,
                translations,
                baseItems,
                itemPropertySemantics,
                stats);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ModifierDefinition Modifier(
        string id,
        string statId,
        decimal min,
        decimal max,
        string sourceGenerationType = "prefix") => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = sourceGenerationType == "unique"
            ? ModifierGenerationType.Implicit
            : ModifierGenerationType.Prefix,
        SourceGenerationType = sourceGenerationType,
        Domain = "item",
        Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = min, MaxValue = max }],
    };

    private static ModifierDefinition Modifier(
        string id,
        params (string StatId, decimal Min, decimal Max)[] stats) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats = stats.Select((stat, index) => new ModifierStat
        {
            Index = index,
            StatId = stat.StatId,
            MinValue = stat.Min,
            MaxValue = stat.Max,
        }).ToArray(),
    };

    private static StatTranslationDefinition Translation(
        string id,
        string statId,
        string format,
        params string[] valueFormats) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                FormatLines = [format],
                ValueFormats = valueFormats,
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
            },
        ],
    };

    private static StatTranslationDefinition TranslationWithDefaultedZero(
        string id,
        string statId,
        string defaultedStatId,
        string format) => new()
    {
        Id = id,
        StatIds = [statId, defaultedStatId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions =
                [
                    new StatTranslationCondition { Index = 0 },
                    new StatTranslationCondition { Index = 1, MinValue = 0, MaxValue = 0 },
                ],
                FormatLines = [format],
                ValueFormats = ["+#", "ignore"],
                IndexHandlers =
                [
                    new StatTranslationIndexHandler { Index = 0 },
                    new StatTranslationIndexHandler { Index = 1 },
                ],
            },
        ],
    };

    private static StatTranslationDefinition TranslationWithHandler(
        string id,
        string statId,
        string format,
        string handler) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                FormatLines = [format],
                ValueFormats = ["#"],
                IndexHandlers =
                [
                    new StatTranslationIndexHandler
                    {
                        Index = 0,
                        Handlers = [handler],
                    },
                ],
            },
        ],
    };

    private static StatTranslationDefinition DynamicSkillTranslation() => new()
    {
        Id = "random-skill",
        StatIds = ["random_skill_level", "random_skill_index"],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions =
                [
                    new StatTranslationCondition { Index = 0, MinValue = 1 },
                    new StatTranslationCondition { Index = 1 },
                ],
                ValueFormats = ["#", "#"],
                IndexHandlers =
                [
                    new StatTranslationIndexHandler { Index = 0 },
                    new StatTranslationIndexHandler
                    {
                        Index = 1,
                        Handlers = ["display_indexable_skill"],
                    },
                ],
                FormatLines = ["+{0} to Level of all {1} Gems"],
            },
        ],
    };
}
