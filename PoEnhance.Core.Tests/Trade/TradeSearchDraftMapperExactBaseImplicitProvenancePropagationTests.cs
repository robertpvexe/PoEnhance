using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchDraftMapperExactBaseImplicitProvenancePropagationTests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public async Task CreateDraft_ExactNativeBaseOwnership_PropagatesCompleteCurrentExactProvenance()
    {
        var raw = await File.ReadAllTextAsync(FindFixture("Tainted-Pact.clipboard.txt"));
        var item = parser.Parse(raw);
        var catalog = await LoadRepoCatalogAsync();
        var baseResolution = new ParsedItemBaseResolver().Resolve(item, catalog);
        var resolutions = new ParsedItemModifierCandidateResolver()
            .Resolve(item, catalog, baseResolution);

        var result = mapper.CreateDraft(item, baseResolution, resolutions, catalog);

        Assert.True(result.IsSuccess);
        var life = Assert.Single(
            result.Draft!.ModifierFilters,
            component => component.OriginalText.Contains("Life per second", StringComparison.Ordinal) &&
                component.OriginalText.Contains("Regenerate", StringComparison.Ordinal));
        Assert.True(life.IsBaseImplicit);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, life.ResolutionStatus);
        Assert.Equal("LifeRegenerationImplicitAmulet1", life.ResolvedModifierId);
        Assert.Equal(["base_life_regeneration_rate_per_minute"], life.ResolvedStatIds);
        Assert.True(life.IsSearchable);
        var provenance = Assert.IsType<SearchComponentBaseImplicitProvenance>(life.BaseImplicitProvenance);
        Assert.Equal(BaseImplicitRecognitionStatus.CurrentExact, provenance.RecognitionStatus);
        Assert.Single(provenance.MechanicalSignatures);
        Assert.False(string.IsNullOrWhiteSpace(provenance.MechanicalSignatures[0]));
        Assert.Contains(
            provenance.SourceSnapshots,
            snapshot => snapshot.Role == BaseImplicitSnapshotRole.CurrentCandidate);
        Assert.Contains(
            life.ProviderDomainEvidence,
            evidence => evidence.IsSourceExact &&
                string.Equals(evidence.ProviderDomain, "Implicit", StringComparison.OrdinalIgnoreCase) &&
                evidence.ModifierId == "LifeRegenerationImplicitAmulet1" &&
                !string.IsNullOrWhiteSpace(evidence.ItemBaseId));
    }

    [Fact]
    public async Task CreateDraft_ExactNativeBaseOwnership_WithoutHistoryDoesNotEmitHollowCurrentExact()
    {
        var raw = await File.ReadAllTextAsync(FindFixture("Tainted-Pact.clipboard.txt"));
        var item = parser.Parse(raw);
        var package = (await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"))).Package!;
        package = package with { BaseImplicitHistory = null };
        var catalog = GameDataCatalog.FromPackage(package);
        var baseResolution = new ParsedItemBaseResolver().Resolve(item, catalog);
        var resolutions = new ParsedItemModifierCandidateResolver()
            .Resolve(item, catalog, baseResolution);

        var result = mapper.CreateDraft(item, baseResolution, resolutions, catalog);

        Assert.True(result.IsSuccess);
        var life = Assert.Single(
            result.Draft!.ModifierFilters,
            component => component.OriginalText.Contains("Life per second", StringComparison.Ordinal) &&
                component.OriginalText.Contains("Regenerate", StringComparison.Ordinal));
        Assert.True(life.IsBaseImplicit);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, life.ResolutionStatus);
        Assert.Equal("LifeRegenerationImplicitAmulet1", life.ResolvedModifierId);
        Assert.Null(life.BaseImplicitProvenance);
    }

    [Fact]
    public void CreateDraft_AmbiguousRecognitionRemainsFailClosedWithoutInventedCurrentExact()
    {
        var item = parser.Parse("""
Item Class: Amulets
Rarity: Rare
Test Amulet
Coral Amulet
--------
Item Level: 80
--------
{ Implicit Modifier }
Regenerate 2.3(2-4) Life per second
""");
        var first = Effect("first", new string('a', 64));
        var second = Effect("second", new string('b', 64));
        var recognition = new BaseImplicitRecognitionResult(
            BaseImplicitRecognitionStatus.Ambiguous,
            [
                Match(first, BaseImplicitSnapshotRole.CurrentCandidate, "snap-a"),
                Match(second, BaseImplicitSnapshotRole.CurrentCandidate, "snap-b"),
            ],
            "base-implicit-history-ambiguous",
            "Two current mechanics matched.");
        var resolution = new ModifierCandidateResolutionResult(
            ParsedModifierIndex: 0,
            ParsedModifier: item.Modifiers[0],
            ParsedModifierName: null,
            ParsedModifierKind: ParsedModifierKind.Implicit,
            GenerationType: ModifierGenerationType.Implicit,
            Status: ModifierCandidateResolutionStatus.Unknown,
            Candidates: [],
            Diagnostics: [],
            Locality: ModifierLocality.Unknown)
        {
            BaseImplicitRecognition = recognition,
        };

        var result = mapper.CreateDraft(item, modifierResolutions: [resolution]);

        var component = Assert.Single(Assert.IsType<TradeSearchDraft>(result.Draft).ModifierFilters);
        Assert.True(component.IsBaseImplicit);
        Assert.False(component.IsSearchable);
        var provenance = Assert.IsType<SearchComponentBaseImplicitProvenance>(
            component.BaseImplicitProvenance);
        Assert.Equal(BaseImplicitRecognitionStatus.Ambiguous, provenance.RecognitionStatus);
        Assert.Equal(2, provenance.MechanicalSignatures.Count);
    }

    private static async Task<GameDataCatalog> LoadRepoCatalogAsync()
    {
        var load = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(load.IsSuccess);
        return GameDataCatalog.FromPackage(load.Package!);
    }

    private static string FindFixture(string fileName) =>
        FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Unique",
            "A59BaseImplicitLifeRegen",
            fileName);

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(string.Join("/", relativeParts));
    }

    private static BaseImplicitMechanicalEffect Effect(string id, string signature)
    {
        var source = new GameDataSourceReference { SourceId = "test" };
        var modifier = new ModifierDefinition
        {
            Id = "LifeRegenerationImplicitAmulet1",
            GroupId = "life-regen",
            GenerationType = ModifierGenerationType.Implicit,
            Domain = "item",
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "base_life_regeneration_rate_per_minute",
                    MinValue = 120m,
                    MaxValue = 240m,
                },
            ],
            Sources = [source],
        };
        return new BaseImplicitMechanicalEffect
        {
            Id = id,
            SourceSnapshotId = "snap",
            SourceModifierId = modifier.Id,
            IsResolved = true,
            MechanicalSignature = signature,
            Modifier = modifier,
            Stats =
            [
                new StatDefinition
                {
                    Id = "base_life_regeneration_rate_per_minute",
                    IsLocal = false,
                    Sources = [source],
                },
            ],
            StatTranslations = [],
        };
    }

    private static BaseImplicitRecognitionMatch Match(
        BaseImplicitMechanicalEffect effect,
        BaseImplicitSnapshotRole role,
        string snapshotId) =>
        new(
            new BaseImplicitObservation
            {
                CanonicalBaseId = "Metadata/Items/Amulets/Amulet2",
                SourceSnapshotId = snapshotId,
                ImplicitModifierIds = [effect.SourceModifierId!],
                MechanicalEffectIds = [effect.Id],
            },
            effect,
            new BaseImplicitSourceSnapshot
            {
                Id = snapshotId,
                Role = role,
                ManifestSourceId = "test",
                CommitSha = "commit",
                DataVersion = "version",
            });
}
