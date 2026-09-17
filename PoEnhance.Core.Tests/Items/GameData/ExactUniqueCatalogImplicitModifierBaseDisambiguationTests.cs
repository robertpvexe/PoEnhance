using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ExactUniqueCatalogImplicitModifierBaseDisambiguationTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly ParsedUniqueItemResolver uniqueResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    private const string BattleWithinClipboard = """
        Item Class: Tinctures
        Rarity: Unique
        The Battle Within
        Oakbranch Tincture
        --------
        Item Level: 84
        --------
        { Implicit Modifier }
        Gain 3 Rage on Melee Weapon Hit
        --------
        { Unique Modifier — Attack }
        Does not inflict Mana Burn over time
        Inflicts Mana Burn on you when you Hit an Enemy with a Melee Weapon
        { Unique Modifier }
        Melee Weapon Attacks have Culling Strike
        { Unique Modifier }
        4(1-5)% increased Rarity of Items found per Mana Burn, up to a maximum of 100%
        """;

    [Fact]
    public async Task BattleWithin_Oakbranch_ResolvesExactUsingRageCatalogModifierId()
    {
        var catalog = await LoadCatalogAsync();
        var parsed = parser.Parse(BattleWithinClipboard);
        var before = baseResolver.Resolve(parsed, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, before.Status);
        Assert.Equal(2, before.Candidates.Count);
        Assert.Contains(
            before.Diagnostics,
            diagnostic => diagnostic.Code == ItemBaseResolutionDiagnosticCodes.BaseAmbiguous);

        var unique = uniqueResolver.Resolve(parsed, catalog, before);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(before, unique, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, after.Status);
        Assert.Equal("Metadata/Items/Tinctures/Tincture9", after.ResolvedBaseId);
        Assert.Equal("Oakbranch Tincture", after.ResolvedBaseName);
        Assert.Single(after.Candidates);
        Assert.Contains(
            after.Diagnostics,
            diagnostic => diagnostic.Code ==
                ItemBaseResolutionDiagnosticCodes.BaseExactUniqueCatalogImplicitModifierDisambiguationMatch);

        var rageBlock = Assert.Single(
            unique.ModifierBlocks,
            block => block.CatalogImplicitConsumptionReason is not null);
        Assert.Equal(["TinctureRageOnHitImplicit1"], rageBlock.ModifierIds);

        var draft = CreateDraft(BattleWithinClipboard, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Exact, draft.Base.Status);
        Assert.Equal("Metadata/Items/Tinctures/Tincture9", draft.Base.ResolvedBaseId);
        Assert.Equal("Oakbranch Tincture", draft.Base.ResolvedBaseName);
        Assert.Equal("Tincture", draft.CanonicalItemClass);

        var rage = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Gain 3 Rage", StringComparison.Ordinal));
        Assert.Equal(ParsedModifierKind.Implicit, rage.ResolvedSourceKind);
        Assert.Equal("TinctureRageOnHitImplicit1", rage.ResolvedModifierId);
        Assert.Equal(3m, rage.RequestedMinimum);
        Assert.True(rage.IsSearchable, rage.NotSearchableReason);
        // Translation recognition is still missing, so native base ownership is not upgraded.
        Assert.False(rage.IsBaseImplicit);
        Assert.Equal(
            ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
            rage.UniqueCatalogImplicitConsumptionReason);
    }

    [Fact]
    public async Task TwoCandidatesSharingProvenModifierId_RemainAmbiguous()
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var unique = uniqueResolver.Resolve(parser.Parse(BattleWithinClipboard), catalog, before);
        var twin = before.Candidates[0] with
        {
            Id = before.Candidates[0].Id + "/twin",
            ImplicitModifierIds = before.Candidates[0].ImplicitModifierIds.ToArray(),
        };
        var fabricated = before with
        {
            Candidates = [before.Candidates[0], twin],
        };
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(
            fabricated,
            unique,
            catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, after.Status);
        Assert.Equal(2, after.Candidates.Count);
        Assert.Contains(
            after.Diagnostics,
            diagnostic => diagnostic.Code ==
                ItemBaseResolutionDiagnosticCodes.BaseExactUniqueCatalogImplicitModifierDisambiguationIncomplete);
    }

    [Fact]
    public async Task ZeroCandidatesMatchingProvenModifierId_RemainAmbiguous()
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var unique = uniqueResolver.Resolve(parser.Parse(BattleWithinClipboard), catalog, before);
        var emptied = before with
        {
            Candidates = before.Candidates
                .Select(candidate => candidate with { ImplicitModifierIds = [] })
                .ToArray(),
        };
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(emptied, unique, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, after.Status);
        Assert.Equal(2, after.Candidates.Count);
        Assert.Contains(
            after.Diagnostics,
            diagnostic => diagnostic.Code ==
                ItemBaseResolutionDiagnosticCodes.BaseExactUniqueCatalogImplicitModifierDisambiguationIncomplete);
    }

    [Fact]
    public async Task EmptyImplicitCandidateDoesNotWinByAbsence()
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var unique = uniqueResolver.Resolve(parser.Parse(BattleWithinClipboard), catalog, before);
        var emptyOwner = Assert.Single(
            before.Candidates,
            candidate => candidate.ImplicitModifierIds.Count == 0);
        var rageOwner = Assert.Single(
            before.Candidates,
            candidate => candidate.ImplicitModifierIds.Count > 0);
        Assert.NotEqual(emptyOwner.Id, rageOwner.Id);

        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(before, unique, catalog);
        Assert.Equal(rageOwner.Id, after.ResolvedBaseId);
        Assert.NotEqual(emptyOwner.Id, after.ResolvedBaseId);
    }

    [Fact]
    public async Task NonExactUniqueIdentity_DoesNotRefine()
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var unique = new UniqueItemResolutionResult
        {
            Status = UniqueItemResolutionStatus.AmbiguousIdentity,
            ModifierBlocks =
            [
                new UniqueModifierBlockResolution
                {
                    ParsedModifierIndex = 0,
                    IsResolved = true,
                    CatalogImplicitConsumptionReason =
                        ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
                    ModifierIds = ["TinctureRageOnHitImplicit1"],
                    CatalogBlocks =
                    [
                        new UniqueModifierBlock
                        {
                            Kind = UniqueModifierBlockKind.Implicit,
                            MechanicalMapping = new UniqueModifierMechanicalMapping
                            {
                                Status = UniqueModifierMechanicalMappingStatus.Exact,
                                ModifierIds = ["TinctureRageOnHitImplicit1"],
                            },
                        },
                    ],
                },
            ],
        };
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(before, unique, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, after.Status);
        Assert.Equal(2, after.Candidates.Count);
        Assert.DoesNotContain(
            after.Diagnostics,
            diagnostic => diagnostic.Code ==
                ItemBaseResolutionDiagnosticCodes.BaseExactUniqueCatalogImplicitModifierDisambiguationMatch);
    }

    [Fact]
    public async Task AmbiguousCatalogMapping_DoesNotRefine()
    {
        await AssertMappingStatusDoesNotRefine(UniqueModifierMechanicalMappingStatus.Ambiguous);
    }

    [Fact]
    public async Task UnsupportedCatalogMapping_DoesNotRefine()
    {
        await AssertMappingStatusDoesNotRefine(UniqueModifierMechanicalMappingStatus.Unsupported);
    }

    [Fact]
    public async Task LegacyHistoricalOnlyIdentity_DoesNotRefine()
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var unique = new UniqueItemResolutionResult
        {
            Status = UniqueItemResolutionStatus.ExactIdentity,
            CompatibleVersions =
            [
                new UniqueItemVersionObservation
                {
                    Role = UniqueItemVersionRole.Historical,
                    BaseType = "Oakbranch Tincture",
                },
            ],
            ModifierBlocks =
            [
                new UniqueModifierBlockResolution
                {
                    ParsedModifierIndex = 0,
                    IsResolved = true,
                    CatalogImplicitConsumptionReason =
                        ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
                    ModifierIds = ["TinctureRageOnHitImplicit1"],
                    CatalogBlocks =
                    [
                        new UniqueModifierBlock
                        {
                            Kind = UniqueModifierBlockKind.Implicit,
                            MechanicalMapping = new UniqueModifierMechanicalMapping
                            {
                                Status = UniqueModifierMechanicalMappingStatus.Exact,
                                ModifierIds = ["TinctureRageOnHitImplicit1"],
                            },
                        },
                    ],
                },
            ],
        };
        Assert.True(unique.IsLegacy);
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(before, unique, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, after.Status);
        Assert.Equal(2, after.Candidates.Count);
        Assert.DoesNotContain(
            after.Diagnostics,
            diagnostic => diagnostic.Code ==
                ItemBaseResolutionDiagnosticCodes.BaseExactUniqueCatalogImplicitModifierDisambiguationMatch);
    }

    [Fact]
    public async Task NonImplicitCatalogBlock_DoesNotRefine()
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var unique = new UniqueItemResolutionResult
        {
            Status = UniqueItemResolutionStatus.ExactIdentity,
            ModifierBlocks =
            [
                new UniqueModifierBlockResolution
                {
                    ParsedModifierIndex = 0,
                    IsResolved = true,
                    CatalogImplicitConsumptionReason =
                        ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
                    ModifierIds = ["TinctureRageOnHitImplicit1"],
                    CatalogBlocks =
                    [
                        new UniqueModifierBlock
                        {
                            Kind = UniqueModifierBlockKind.Unique,
                            MechanicalMapping = new UniqueModifierMechanicalMapping
                            {
                                Status = UniqueModifierMechanicalMappingStatus.Exact,
                                ModifierIds = ["TinctureRageOnHitImplicit1"],
                            },
                        },
                    ],
                },
            ],
        };
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(before, unique, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, after.Status);
        Assert.Equal(2, after.Candidates.Count);
    }

    [Fact]
    public async Task EquivalentSourceSetBlock_DoesNotRefine()
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var unique = new UniqueItemResolutionResult
        {
            Status = UniqueItemResolutionStatus.ExactIdentity,
            ModifierBlocks =
            [
                new UniqueModifierBlockResolution
                {
                    ParsedModifierIndex = 0,
                    IsResolved = true,
                    IsEquivalentSourceSet = true,
                    CatalogImplicitConsumptionReason =
                        ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
                    ModifierIds = ["TinctureRageOnHitImplicit1"],
                    CatalogBlocks =
                    [
                        new UniqueModifierBlock
                        {
                            Kind = UniqueModifierBlockKind.Implicit,
                            MechanicalMapping = new UniqueModifierMechanicalMapping
                            {
                                Status = UniqueModifierMechanicalMappingStatus.Exact,
                                ModifierIds = ["TinctureRageOnHitImplicit1"],
                            },
                        },
                    ],
                },
            ],
        };
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(before, unique, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, after.Status);
        Assert.Equal(2, after.Candidates.Count);
    }

    [Fact]
    public async Task ConflictingMultiImplicitEvidence_RemainsAmbiguous()
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var unique = new UniqueItemResolutionResult
        {
            Status = UniqueItemResolutionStatus.ExactIdentity,
            ModifierBlocks =
            [
                new UniqueModifierBlockResolution
                {
                    ParsedModifierIndex = 0,
                    IsResolved = true,
                    CatalogImplicitConsumptionReason =
                        ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
                    ModifierIds = ["TinctureRageOnHitImplicit1", "MissingNativeImplicitForConflict"],
                    CatalogBlocks =
                    [
                        new UniqueModifierBlock
                        {
                            Kind = UniqueModifierBlockKind.Implicit,
                            MechanicalMapping = new UniqueModifierMechanicalMapping
                            {
                                Status = UniqueModifierMechanicalMappingStatus.Exact,
                                ModifierIds =
                                [
                                    "TinctureRageOnHitImplicit1",
                                    "MissingNativeImplicitForConflict",
                                ],
                            },
                        },
                    ],
                },
            ],
        };
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(before, unique, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, after.Status);
        Assert.Equal(2, after.Candidates.Count);
        Assert.Contains(
            after.Diagnostics,
            diagnostic => diagnostic.Code ==
                ItemBaseResolutionDiagnosticCodes.BaseExactUniqueCatalogImplicitModifierDisambiguationIncomplete);
    }

    [Fact]
    public async Task NoFirstMatchFallback_WhenEvidenceAbsent()
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var firstId = before.Candidates[0].Id;
        var unique = new UniqueItemResolutionResult
        {
            Status = UniqueItemResolutionStatus.ExactIdentity,
            ModifierBlocks = [],
        };
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(before, unique, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, after.Status);
        Assert.Null(after.ResolvedBaseId);
        Assert.Equal(2, after.Candidates.Count);
        Assert.Equal(firstId, before.Candidates[0].Id);
    }

    [Fact]
    public async Task SameNameTinctureCorpus_Audit()
    {
        var catalog = await LoadCatalogAsync();
        var rows = new List<string>();
        foreach (var (name, clipboard) in new (string Name, string Clipboard)[]
                 {
                     ("The Battle Within", BattleWithinClipboard),
                     ("Grasping Nightshade", """
                         Item Class: Tinctures
                         Rarity: Unique
                         Grasping Nightshade
                         Sporebloom Tincture
                         --------
                         Item Level: 84
                         --------
                         { Implicit Modifier }
                         25% chance to Blind Enemies on Hit with Melee Weapons
                         27(25-35)% increased Effect of Blind from Melee Weapons
                         """),
                     ("Sap of the Seasons", """
                         Item Class: Tinctures
                         Rarity: Unique
                         Sap of the Seasons
                         Prismatic Tincture
                         --------
                         Item Level: 84
                         --------
                         { Implicit Modifier }
                         97(70-100)% increased Elemental Damage with Melee Weapons
                         """),
                     ("Mightblood Ire", """
                         Item Class: Tinctures
                         Rarity: Unique
                         Mightblood Ire
                         Ironwood Tincture
                         --------
                         Item Level: 84
                         --------
                         { Implicit Modifier }
                         40% reduced Enemy Stun Threshold with Melee Weapons
                         20(15-25)% increased Stun Duration with Melee Weapons
                         """),
                     ("Wildfire Phloem", """
                         Item Class: Tinctures
                         Rarity: Unique
                         Wildfire Phloem
                         Ashbark Tincture
                         --------
                         Item Level: 84
                         --------
                         { Implicit Modifier }
                         25% chance to Ignite with Melee Weapons
                         75(60-90)% increased Damage with Ignite from Melee Weapons
                         """),
                 })
        {
            var parsed = parser.Parse(clipboard);
            var before = baseResolver.Resolve(parsed, catalog);
            var unique = uniqueResolver.Resolve(parsed, catalog, before);
            var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(
                before,
                unique,
                catalog);
            var draft = CreateDraft(clipboard, catalog);
            rows.Add(
                $"{name}: before={before.Status}/{before.Candidates.Count} " +
                $"after={after.Status}/{after.ResolvedBaseId} draft={draft.Base.Status}/{draft.Base.ResolvedBaseId}");
        }

        Assert.Contains(rows, row => row.StartsWith("The Battle Within:", StringComparison.Ordinal) &&
            row.Contains("Metadata/Items/Tinctures/Tincture9", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.StartsWith("Grasping Nightshade:", StringComparison.Ordinal) &&
            row.Contains("Tincture10", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.StartsWith("Sap of the Seasons:", StringComparison.Ordinal) &&
            row.Contains("Tincture1", StringComparison.Ordinal));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row)));
    }

    private async Task AssertMappingStatusDoesNotRefine(UniqueModifierMechanicalMappingStatus status)
    {
        var catalog = await LoadCatalogAsync();
        var before = await ResolveAmbiguousOakbranchAsync(catalog);
        var unique = new UniqueItemResolutionResult
        {
            Status = UniqueItemResolutionStatus.ExactIdentity,
            ModifierBlocks =
            [
                new UniqueModifierBlockResolution
                {
                    ParsedModifierIndex = 0,
                    IsResolved = true,
                    CatalogImplicitConsumptionReason =
                        ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
                    ModifierIds = ["TinctureRageOnHitImplicit1"],
                    CatalogBlocks =
                    [
                        new UniqueModifierBlock
                        {
                            Kind = UniqueModifierBlockKind.Implicit,
                            MechanicalMapping = new UniqueModifierMechanicalMapping
                            {
                                Status = status,
                                ModifierIds = ["TinctureRageOnHitImplicit1"],
                            },
                        },
                    ],
                },
            ],
        };
        var after = baseResolver.RefineWithExactUniqueCatalogImplicitModifierIds(before, unique, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, after.Status);
        Assert.Equal(2, after.Candidates.Count);
        Assert.DoesNotContain(
            after.Diagnostics,
            diagnostic => diagnostic.Code ==
                ItemBaseResolutionDiagnosticCodes.BaseExactUniqueCatalogImplicitModifierDisambiguationMatch);
    }

    private async Task<ItemBaseResolutionResult> ResolveAmbiguousOakbranchAsync(GameDataCatalog catalog)
    {
        await Task.CompletedTask;
        var parsed = parser.Parse(BattleWithinClipboard);
        var before = baseResolver.Resolve(parsed, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, before.Status);
        Assert.Equal(2, before.Candidates.Count);
        return before;
    }

    private TradeSearchDraft CreateDraft(string clipboard, GameDataCatalog catalog)
    {
        var parsed = parser.Parse(clipboard);
        var baseResolution = baseResolver.Resolve(parsed, catalog);
        var modifierResolutions = modifierResolver.Resolve(parsed, catalog, baseResolution);
        var result = mapper.CreateDraft(parsed, baseResolution, modifierResolutions, catalog);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static async Task<GameDataCatalog> LoadCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(load.Package));
    }
}
