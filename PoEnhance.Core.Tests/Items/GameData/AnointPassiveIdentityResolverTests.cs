using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class AnointPassiveIdentityResolverTests
{
    [Theory]
    [InlineData("Lethality", 41119)]
    [InlineData("Heartseeker", 65502)]
    [InlineData("Mind Drinker", 42804)]
    [InlineData("Thick Skin", 19069)]
    [InlineData("Elder Power", 41476)]
    public void Resolve_CurrentAnoint_UsesExactPassiveHashWithoutModifierId(
        string passiveName,
        int expectedHash)
    {
        var catalog = CreateCatalog(
            new PassiveSkillIdentity
            {
                CanonicalName = passiveName,
                PassiveHash = expectedHash,
                InternalId = $"passive-{expectedHash}",
                Sources = [Source(expectedHash.ToString())],
            });
        var item = Parse($"Allocates {passiveName} (enchant)");

        var result = Assert.Single(new ParsedItemModifierCandidateResolver().Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(expectedHash, result.AnointPassiveIdentity?.PassiveHash);
        Assert.Equal("mod_granted_passive_hash", Assert.Single(result.MechanicalStatIds));
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.AnointPassiveExactMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_UnknownAnoint_FailsClosed()
    {
        var result = Assert.Single(new ParsedItemModifierCandidateResolver().Resolve(
            Parse("Allocates Missing Passive (enchant)"),
            CreateCatalog()));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.AnointPassiveNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_DuplicateCanonicalName_FailsClosedAsAmbiguous()
    {
        var catalog = CreateCatalog(
            new PassiveSkillIdentity
            {
                CanonicalName = "Duplicate Passive",
                PassiveHash = 1,
                Sources = [Source("1")],
            },
            new PassiveSkillIdentity
            {
                CanonicalName = "Duplicate Passive",
                PassiveHash = 2,
                Sources = [Source("2")],
            });

        var result = Assert.Single(new ParsedItemModifierCandidateResolver().Resolve(
            Parse("Allocates Duplicate Passive (enchant)"),
            catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.AnointPassiveAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_ForbiddenUniqueAllocatesClause_DoesNotApplyAnointResolver()
    {
        var catalog = CreateCatalog(
            new PassiveSkillIdentity
            {
                CanonicalName = "Unnatural Strength",
                PassiveHash = 1,
                Sources = [Source("1")],
            });
        var item = new ItemTextParser().Parse("""
Item Class: Jewels
Rarity: Unique
Forbidden Flesh
Cobalt Jewel
--------
Item Level: 80
--------
{ Unique Modifier — Attribute }
Allocates Unnatural Strength if you have the matching modifier on Forbidden Flame
""");

        var result = Assert.Single(new ParsedItemModifierCandidateResolver().Resolve(item, catalog));

        Assert.Equal(ParsedModifierKind.Unique, result.ParsedModifierKind);
        Assert.Null(result.AnointPassiveIdentity);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Code is
                ModifierCandidateResolutionDiagnosticCodes.AnointPassiveExactMatch or
                ModifierCandidateResolutionDiagnosticCodes.AnointPassiveNotFound or
                ModifierCandidateResolutionDiagnosticCodes.AnointPassiveAmbiguous);
    }

    [Fact]
    public void Resolve_SanctuaryDuplicateCanonicalName_FailsClosedAsAmbiguous()
    {
        var catalog = CreateCatalog(
            new PassiveSkillIdentity
            {
                CanonicalName = "Sanctuary",
                PassiveHash = 20832,
                Sources = [Source("20832")],
            },
            new PassiveSkillIdentity
            {
                CanonicalName = "Sanctuary",
                PassiveHash = 39790,
                Sources = [Source("39790")],
            });

        var result = Assert.Single(new ParsedItemModifierCandidateResolver().Resolve(
            Parse("Allocates Sanctuary (enchant)"),
            catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.AnointPassiveAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    private static ParsedItem Parse(string line) => new ItemTextParser().Parse($"""
Item Class: Amulets
Rarity: Unique
Test Name
Test Amulet
--------
Item Level: 80
--------
{line}
""");

    private static GameDataCatalog CreateCatalog(params PassiveSkillIdentity[] identities)
    {
        const string statId = "mod_granted_passive_hash";
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "test",
                CreatedAtUtc = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero),
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "test",
                        RetrievedAtUtc = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero),
                        SourceVersion = "current",
                    },
                ],
            },
            PassiveSkills = identities,
            Stats =
            [
                new StatDefinition
                {
                    Id = statId,
                    Sources = [Source(statId)],
                },
            ],
            StatTranslations =
            [
                new StatTranslationDefinition
                {
                    Id = "translation.allocates",
                    StatIds = [statId],
                    Language = "English",
                    Sources = [Source("translation.allocates")],
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
        });
    }

    private static GameDataSourceReference Source(string externalId) => new()
    {
        SourceId = "test",
        ExternalId = externalId,
    };
}
