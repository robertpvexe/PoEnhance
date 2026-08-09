using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ParsedItemBaseImplicitRecognitionResolverTests
{
    private readonly ParsedItemBaseImplicitRecognitionResolver resolver = new();

    [Fact]
    public void Resolve_OldStructuredEffectForSameCanonicalBase_IsHistoricalExactWithoutChangingCurrentBase()
    {
        var catalog = Catalog();
        var item = new ItemTextParser().Parse("""
Item Class: Warstaves
Rarity: Magic
Foul Staff of Ashes
--------
Warstaff
--------
Item Level: 84
--------
{ Implicit Modifier }
+22% Chance to Block Attack Damage while wielding a Staff
--------
{ Suffix Modifier "of Ashes" (Tier: 1) - Damage }
50% increased Fire Damage
""");

        var resolution = Assert.Single(
            new ParsedItemModifierCandidateResolver().Resolve(item, catalog),
            result => result.ParsedModifier.Kind == ParsedModifierKind.Implicit);

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, resolution.Status);
        var recognition = Assert.IsType<BaseImplicitRecognitionResult>(resolution.BaseImplicitRecognition);
        Assert.Equal(BaseImplicitRecognitionStatus.HistoricalExact, recognition.Status);
        var match = Assert.Single(recognition.Matches);
        Assert.Equal(BaseImplicitSnapshotRole.HistoricalObserved, match.SourceSnapshot.Role);
        Assert.Equal("c50acab2ed660a70511e7f91ee09db4e632089e4", match.SourceSnapshot.CommitSha);
        Assert.Equal("3.28.0.13", match.SourceSnapshot.DataVersion);
        Assert.Equal("old_block", Assert.Single(match.Effect.Modifier!.Stats).StatId);
        Assert.Equal(["current-spell-damage"], Assert.Single(catalog.FindItemBasesById("base.staff")).ImplicitModifierIds);
        Assert.Empty(catalog.FindModifiersById("old-staff-block"));
    }

    [Fact]
    public void Resolve_CurrentExactHasPrecedenceOverHistoricalEvidence()
    {
        var catalog = Catalog();
        var parsed = Implicit("+35% increased Spell Damage");

        var result = resolver.Resolve(parsed, Assert.Single(catalog.FindItemBasesById("base.staff")), catalog);

        Assert.Equal(BaseImplicitRecognitionStatus.CurrentExact, result.Status);
        Assert.All(result.Matches, match =>
            Assert.Equal(BaseImplicitSnapshotRole.CurrentCandidate, match.SourceSnapshot.Role));
    }

    [Fact]
    public void Resolve_DoesNotUseHistoryAcrossCanonicalBaseIdsOrAsTextOnlyEvidence()
    {
        var catalog = Catalog();

        var unrelated = resolver.Resolve(
            Implicit("+22% Chance to Block Attack Damage while wielding a Staff"),
            Assert.Single(catalog.FindItemBasesById("base.other")),
            catalog);
        var wrongValue = resolver.Resolve(
            Implicit("+21% Chance to Block Attack Damage while wielding a Staff"),
            Assert.Single(catalog.FindItemBasesById("base.staff")),
            catalog);

        Assert.Equal(BaseImplicitRecognitionStatus.Unknown, unrelated.Status);
        Assert.Equal(BaseImplicitRecognitionStatus.Unknown, wrongValue.Status);
    }

    [Theory]
    [InlineData(ParsedImplicitModifierOrigin.Corrupted)]
    [InlineData(ParsedImplicitModifierOrigin.SearingExarch)]
    [InlineData(ParsedImplicitModifierOrigin.EaterOfWorlds)]
    [InlineData(ParsedImplicitModifierOrigin.Synthesis)]
    public void Resolve_SpecialImplicitOriginsRemainAuthoritative(ParsedImplicitModifierOrigin origin)
    {
        var catalog = Catalog();
        var parsed = Implicit("+22% Chance to Block Attack Damage while wielding a Staff") with
        {
            ImplicitOrigin = origin,
        };

        var result = resolver.Resolve(parsed, Assert.Single(catalog.FindItemBasesById("base.staff")), catalog);

        Assert.Equal(BaseImplicitRecognitionStatus.Unknown, result.Status);
        Assert.Equal("base-implicit-origin-ineligible", result.DiagnosticCode);
    }

    [Fact]
    public void Resolve_ConflictingHistoricalMechanicalSignaturesFailClosed()
    {
        var catalog = Catalog(includeConflictingHistory: true);

        var result = resolver.Resolve(
            Implicit("+22% Chance to Block Attack Damage while wielding a Staff"),
            Assert.Single(catalog.FindItemBasesById("base.staff")),
            catalog);

        Assert.Equal(BaseImplicitRecognitionStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(2, result.Matches.Select(match => match.Effect.MechanicalSignature).Distinct().Count());
    }

    [Fact]
    public void Resolve_MultiStatArityMismatchRejectsHistoricalMatch()
    {
        var catalog = Catalog(historicalStats: [
            ("old_min", 3m, 5m, false),
            ("old_max", 70m, 82m, false),
        ], historicalFormat: "Adds # to # Lightning Damage");

        var result = resolver.Resolve(
            Implicit("Adds 5(3-5) Lightning Damage"),
            Assert.Single(catalog.FindItemBasesById("base.staff")),
            catalog);

        Assert.Equal(BaseImplicitRecognitionStatus.Unknown, result.Status);
    }

    private static ParsedModifier Implicit(string text) => new(
        [text],
        "{ Implicit Modifier }",
        ParsedModifierKind.Implicit,
        Name: null,
        Tier: null,
        Rank: null,
        CategoryText: null,
        IsCrafted: false,
        IsFractured: false,
        IsVeiled: false);

    private static GameDataCatalog Catalog(
        bool includeConflictingHistory = false,
        IReadOnlyList<(string Id, decimal Min, decimal Max, bool Local)>? historicalStats = null,
        string historicalFormat = "+#% Chance to Block Attack Damage while wielding a Staff")
    {
        historicalStats ??= [("old_block", 22m, 22m, false)];
        var currentModifier = Modifier("current-spell-damage", "repoe", [("spell_damage", 35m, 39m)]);
        var oldModifier = Modifier(
            "old-staff-block",
            "historical",
            historicalStats.Select(stat => (stat.Id, stat.Min, stat.Max)).ToArray());
        var currentEffect = Effect(
            "current-effect",
            "current",
            currentModifier,
            [Stat("spell_damage", false, "repoe")],
            Translation("current-translation", ["spell_damage"], "+#% increased Spell Damage", "repoe"),
            new string('1', 64));
        var oldEffect = Effect(
            "old-effect",
            "old",
            oldModifier,
            historicalStats.Select(stat => Stat(stat.Id, stat.Local, "historical")).ToArray(),
            Translation(
                "old-translation",
                historicalStats.Select(stat => stat.Id).ToArray(),
                historicalFormat,
                "historical"),
            new string('2', 64));

        var sources = new List<GameDataPackageSource>
        {
            PackageSource("repoe"),
            PackageSource("historical"),
        };
        var historySources = new List<BaseImplicitSourceSnapshot>
        {
            HistorySource("current", BaseImplicitSnapshotRole.CurrentCandidate, "repoe", "34a9bd548eba7c3b62ab1d1f19a99ae8b12f1564", "3.29.1.2.2"),
            HistorySource("old", BaseImplicitSnapshotRole.HistoricalObserved, "historical", "c50acab2ed660a70511e7f91ee09db4e632089e4", "3.28.0.13"),
        };
        var effects = new List<BaseImplicitMechanicalEffect> { currentEffect, oldEffect };
        var observations = new List<BaseImplicitObservation>
        {
            Observation("base.staff", "current", "current-spell-damage", "current-effect", new string('3', 64)),
            Observation("base.staff", "old", "old-staff-block", "old-effect", new string('4', 64)),
        };

        if (includeConflictingHistory)
        {
            sources.Add(PackageSource("historical-2"));
            historySources.Add(HistorySource(
                "old-2",
                BaseImplicitSnapshotRole.HistoricalObserved,
                "historical-2",
                "d50acab2ed660a70511e7f91ee09db4e632089e4",
                "older"));
            var conflictModifier = Modifier("old-staff-block-2", "historical-2", [("old_block_local", 22m, 22m)]);
            effects.Add(Effect(
                "old-effect-2",
                "old-2",
                conflictModifier,
                [Stat("old_block_local", true, "historical-2")],
                Translation(
                    "old-translation-2",
                    ["old_block_local"],
                    "+#% Chance to Block Attack Damage while wielding a Staff",
                    "historical-2"),
                new string('5', 64)));
            observations.Add(Observation(
                "base.staff",
                "old-2",
                "old-staff-block-2",
                "old-effect-2",
                new string('6', 64)));
        }

        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "test",
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                Sources = sources,
            },
            ItemBases =
            [
                new ItemBaseRecord
                {
                    Id = "base.staff",
                    Name = "Foul Staff",
                    ItemClass = "Warstaves",
                    Domain = "item",
                    ImplicitModifierIds = ["current-spell-damage"],
                    Sources = [Reference("repoe")],
                },
                new ItemBaseRecord
                {
                    Id = "base.other",
                    Name = "Other Staff",
                    ItemClass = "Warstaves",
                    Domain = "item",
                    Sources = [Reference("repoe")],
                },
            ],
            Modifiers = [currentModifier],
            Stats = [Stat("spell_damage", false, "repoe")],
            StatTranslations = [Translation("current-translation", ["spell_damage"], "+#% increased Spell Damage", "repoe")],
            BaseImplicitHistory = new BaseImplicitHistoryCatalog
            {
                SourceSnapshots = historySources,
                MechanicalEffects = effects,
                Observations = observations,
            },
        });
    }

    private static ModifierDefinition Modifier(
        string id,
        string sourceId,
        IReadOnlyList<(string Id, decimal Min, decimal Max)> stats) => new()
    {
        Id = id,
        GroupId = id + "-group",
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats = stats.Select((stat, index) => new ModifierStat
        {
            Index = index,
            StatId = stat.Id,
            MinValue = stat.Min,
            MaxValue = stat.Max,
        }).ToArray(),
        Sources = [Reference(sourceId)],
    };

    private static StatDefinition Stat(string id, bool local, string sourceId) => new()
    {
        Id = id,
        IsLocal = local,
        Sources = [Reference(sourceId)],
    };

    private static StatTranslationDefinition Translation(
        string id,
        IReadOnlyList<string> statIds,
        string format,
        string sourceId) => new()
    {
        Id = id,
        StatIds = statIds,
        Language = "English",
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = statIds.Select((_, index) => new StatTranslationCondition { Index = index }).ToArray(),
                ValueFormats = TranslationShape(format, statIds.Count).Formats,
                IndexHandlers = statIds.Select((_, index) => new StatTranslationIndexHandler { Index = index }).ToArray(),
                FormatLines = [TranslationShape(format, statIds.Count).Line],
            },
        ],
        Sources = [Reference(sourceId)],
    };

    private static BaseImplicitMechanicalEffect Effect(
        string id,
        string snapshotId,
        ModifierDefinition modifier,
        IReadOnlyList<StatDefinition> stats,
        StatTranslationDefinition translation,
        string signature) => new()
    {
        Id = id,
        SourceSnapshotId = snapshotId,
        SourceModifierId = modifier.Id,
        IsResolved = true,
        MechanicalSignature = signature,
        Modifier = modifier,
        Stats = stats,
        StatTranslations = [translation],
    };

    private static BaseImplicitSourceSnapshot HistorySource(
        string id,
        BaseImplicitSnapshotRole role,
        string sourceId,
        string commit,
        string version) => new()
    {
        Id = id,
        Role = role,
        ManifestSourceId = sourceId,
        RepositoryUri = "https://github.com/repoe-fork/repoe",
        CommitSha = commit,
        DataVersion = version,
        Files = [new() { LogicalRole = "baseItems", PackageInputLabel = id + "-base_items.json" }],
    };

    private static BaseImplicitObservation Observation(
        string baseId,
        string snapshotId,
        string modifierId,
        string effectId,
        string signature) => new()
    {
        CanonicalBaseId = baseId,
        SourceSnapshotId = snapshotId,
        ImplicitModifierIds = [modifierId],
        MechanicalEffectIds = [effectId],
        ImplicitSetMechanicalSignature = signature,
    };

    private static GameDataPackageSource PackageSource(string id) => new()
    {
        SourceId = id,
        RetrievedAtUtc = DateTimeOffset.UnixEpoch,
    };

    private static GameDataSourceReference Reference(string sourceId) => new() { SourceId = sourceId };

    private static (string Line, IReadOnlyList<string> Formats) TranslationShape(string value, int arity)
    {
        var formats = new List<string>();
        for (var index = 0; index < arity; index++)
        {
            var plusPosition = value.IndexOf("+#", StringComparison.Ordinal);
            var plainPosition = value.IndexOf('#');
            if (plusPosition >= 0 && plusPosition == plainPosition - 1)
            {
                value = value.Remove(plusPosition, 2).Insert(plusPosition, $"{{{index}}}");
                formats.Add("+#");
            }
            else if (plainPosition >= 0)
            {
                value = value.Remove(plainPosition, 1).Insert(plainPosition, $"{{{index}}}");
                formats.Add("#");
            }
        }

        return (value, formats);
    }
}
