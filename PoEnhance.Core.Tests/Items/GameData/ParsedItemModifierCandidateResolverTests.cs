using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ParsedItemModifierCandidateResolverTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();

    [Fact]
    public void Resolve_PrefixNameAndGenerationType_ReturnsExactCandidate()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Prefix Modifier "hale" (Tier: 5) - Life }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(ModifierGenerationType.Prefix, result.GenerationType);
        Assert.Equal("mod.prefix.hale.t5", Assert.Single(result.Candidates).Id);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierExactMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_ImplicitNameAndGenerationType_ReturnsExactCandidate()
    {
        var catalog = CreateCatalog(Modifier(
            "mod.implicit.gold-ring.item-rarity",
            "Gold Ring Implicit",
            ModifierGenerationType.Implicit));
        var item = ParseWithModifier("""
{ Implicit Modifier "Gold Ring Implicit" - Item Rarity }
15% increased Rarity of Items found (implicit)
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(ModifierGenerationType.Implicit, result.GenerationType);
        Assert.Equal("mod.implicit.gold-ring.item-rarity", Assert.Single(result.Candidates).Id);
    }

    [Fact]
    public void Resolve_CraftedModifierWithReliableNameAndKind_UsesUnderlyingGenerationKind()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.upgraded", "Upgraded", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Master Crafted Prefix Modifier "Upgraded" (Rank: 1) - Damage }
Adds 1 to 2 Physical Damage
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.True(result.ParsedModifier.IsCrafted);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(ModifierGenerationType.Prefix, result.GenerationType);
    }

    [Fact]
    public void Resolve_FracturedSuffixModifier_UsesUnderlyingSuffixGenerationKind()
    {
        var catalog = CreateCatalog(Modifier("mod.suffix.order", "of the Order", ModifierGenerationType.Suffix));
        var item = ParseWithModifier("""
{ Fractured Suffix Modifier "of the Order" (Tier: 4) - Caster }
12% increased Cast Speed
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.True(result.ParsedModifier.IsFractured);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(ModifierGenerationType.Suffix, result.GenerationType);
    }

    [Fact]
    public void Resolve_DuplicateNameAndGenerationType_ReturnsUnknownAmbiguousWithAllCandidatesInPackageOrder()
    {
        var catalog = CreateCatalog(
            Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix),
            Modifier("mod.prefix.hale.t6", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(
            ["mod.prefix.hale.t5", "mod.prefix.hale.t6"],
            result.Candidates.Select(candidate => candidate.Id));
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_NameMatchingDifferentGenerationType_ReturnsNotFound()
    {
        var catalog = CreateCatalog(Modifier("mod.suffix.hale", "Hale", ModifierGenerationType.Suffix));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_SupportedKindWithoutParsedName_ReturnsNameNotAvailable()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Prefix Modifier }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierNameNotAvailable,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_UnsupportedKindReportsUnsupportedWithoutFallbackMatch()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Unknown Modifier "Hale" }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Null(result.GenerationType);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierKindUnsupported,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_UniqueModifierPlaceholderIsNotMatched()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Unique Modifier }
Adds 1 to 2 Physical Damage
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Null(result.GenerationType);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierKindUnsupported,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_NormalDescriptionModifierWithoutAdvancedMetadataIsNotGuessed()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
--------
+50 to maximum Life
""");

        var results = resolver.Resolve(item, catalog);

        Assert.Empty(results);
    }

    [Fact]
    public void Resolve_DoesNotMutateParsedItemOrCatalogAndNeverReturnsProbableOrGeneric()
    {
        var catalog = CreateCatalog(
            Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix),
            Modifier("mod.suffix.order", "of the Order", ModifierGenerationType.Suffix));
        var originalModifiers = catalog.Modifiers.ToArray();
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
--------
{ Suffix Modifier "Missing" }
+10% to Fire Resistance
""");
        var originalRawText = item.RawText;
        var originalPrefixName = item.PrefixModifiers[0].Name;

        var results = resolver.Resolve(item, catalog);

        Assert.Same(item.PrefixModifiers[0], results[0].ParsedModifier);
        Assert.Equal(originalRawText, item.RawText);
        Assert.Equal(originalPrefixName, item.PrefixModifiers[0].Name);
        Assert.Equal(originalModifiers, catalog.Modifiers);
        Assert.DoesNotContain(results, result =>
            result.Status is ModifierCandidateResolutionStatus.Probable
                or ModifierCandidateResolutionStatus.Generic);
    }

    private ParsedItem ParseWithModifier(string modifierText)
    {
        return parser.Parse($"""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
--------
{modifierText}
""");
    }

    private static GameDataCatalog CreateCatalog(params ModifierDefinition[] modifiers)
    {
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = CreateManifest(),
            ItemBases = [],
            Modifiers = modifiers,
            Stats = [Stat("test_stat")],
            StatTranslations = [],
        });
    }

    private static ModifierDefinition Modifier(
        string id,
        string name,
        ModifierGenerationType generationType)
    {
        return new ModifierDefinition
        {
            Id = id,
            GroupId = $"group.{id}",
            Name = name,
            GenerationType = generationType,
            Domain = "item",
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

    private static StatDefinition Stat(string id)
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
            CreatedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
            League = "test",
            Patch = "test",
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "test",
                    RetrievedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
                    SourceVersion = "test",
                },
            ],
        };
    }
}
