using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// A.5.46 — Fleshcrafter convert-line newline translation fidelity (packaged Exact).
/// </summary>
public sealed class FleshcrafterNewlineTranslationFidelityTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly ParsedUniqueItemResolver uniqueResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    private const string ConvertLine =
        "Minions Convert 2% of their Maximum Life to Maximum Energy Shield per 1% Chaos Resistance they have";

    private const string FleshcrafterClipboard = """
        Item Class: Body Armours
        Rarity: Unique
        Fleshcrafter
        Necromancer Silks
        --------
        Energy Shield: 100
        --------
        Item Level: 84
        --------
        { Unique Modifier — Defences, Energy Shield }
        120(100-150)% increased Energy Shield
        { Unique Modifier — Minion }
        Chaos Damage taken does not bypass Minions' Energy Shield
        { Unique Modifier — Minion }
        Minions have 75(50-100)% faster start of Energy Shield Recharge
        { Unique Modifier — Minion }
        While Minions have Energy Shield, their Hits Ignore Monster Elemental Resistances
        { Unique Modifier — Minion }
        Minions Convert 2% of their Maximum Life to Maximum Energy Shield per 1% Chaos Resistance they have
        """;

    [Fact]
    public async Task PackagedFleshcrafter_ConvertLine_IsExactWithKnownMechanics()
    {
        var package = await LoadPackageAsync();
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(candidate.CanonicalName, "Fleshcrafter", StringComparison.Ordinal));
        var version = Assert.Single(item.Versions);
        Assert.Equal(UniqueItemVersionRole.Current, version.Role);

        var convert = Assert.Single(
            version.ModifierBlocks,
            block => block.Lines.Contains(ConvertLine));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, convert.MechanicalMapping.Status);
        Assert.Equal(
            ["MinionLifeConvertedToEnergyShieldUnique__1"],
            convert.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["minion_maximum_life_%_to_convert_to_maximum_energy_shield_per_1%_chaos_resistance"],
            convert.MechanicalMapping.StatIds);
        Assert.Null(convert.MechanicalMapping.DiagnosticCode);
        Assert.Null(convert.MechanicalMapping.ConflictEvidence);
    }

    [Fact]
    public async Task PackagedPureTalent_ClassShortLines_RemainUnsupported()
    {
        var package = await LoadPackageAsync();
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(candidate.CanonicalName, "Pure Talent", StringComparison.Ordinal));

        Assert.All(item.Versions, version =>
        {
            var classLines = version.ModifierBlocks.Where(block =>
                block.Lines.Any(line =>
                    line.Contains(':', StringComparison.Ordinal) &&
                    (line.StartsWith("Marauder:", StringComparison.Ordinal) ||
                     line.StartsWith("Duelist:", StringComparison.Ordinal) ||
                     line.StartsWith("Ranger:", StringComparison.Ordinal) ||
                     line.StartsWith("Shadow:", StringComparison.Ordinal) ||
                     line.StartsWith("Witch:", StringComparison.Ordinal) ||
                     line.StartsWith("Templar:", StringComparison.Ordinal) ||
                     line.StartsWith("Scion:", StringComparison.Ordinal))));
            Assert.NotEmpty(classLines);
            Assert.All(classLines, block =>
            {
                Assert.Equal(
                    UniqueModifierMechanicalMappingStatus.Unsupported,
                    block.MechanicalMapping.Status);
                Assert.Equal("UNIQUE_MECHANICS_NOT_FOUND", block.MechanicalMapping.DiagnosticCode);
                Assert.Empty(block.MechanicalMapping.StatIds);
            });
        });
    }

    [Fact]
    public async Task FleshcrafterClipboard_ConvertLine_ConsumesExactUniqueProvenance()
    {
        var catalog = await LoadCatalogAsync();
        var draft = CreateDraft(FleshcrafterClipboard, catalog);
        var convert = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Minions Convert 2%", StringComparison.Ordinal));

        Assert.Equal(ParsedModifierKind.Unique, convert.ResolvedSourceKind);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, convert.ResolutionStatus);
        Assert.Equal("MinionLifeConvertedToEnergyShieldUnique__1", convert.ResolvedModifierId);
        Assert.Equal(
            ["minion_maximum_life_%_to_convert_to_maximum_energy_shield_per_1%_chaos_resistance"],
            convert.ResolvedStatIds);
        Assert.True(convert.HasExactUniqueSourceProvenance);
        Assert.Null(convert.UniqueResolutionDiagnosticCode);
        Assert.True(convert.IsSearchable, convert.NotSearchableReason);
    }

    private TradeSearchDraft CreateDraft(string clipboard, GameDataCatalog catalog)
    {
        var parsed = parser.Parse(clipboard);
        var baseResolution = baseResolver.Resolve(parsed, catalog);
        var modifierResolutions = modifierResolver.Resolve(parsed, catalog, baseResolution);
        var unique = uniqueResolver.Resolve(parsed, catalog, baseResolution);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);
        var result = mapper.CreateDraft(parsed, baseResolution, modifierResolutions, catalog);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static async Task<GameDataPackage> LoadPackageAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess, string.Join(", ", load.Diagnostics.Select(d => d.Code)));
        Assert.Equal(4, load.Package!.Manifest.SchemaVersion);
        Assert.Equal(
            "3.29.1.2.9-unique-newline-translation-fidelity",
            load.Package.Manifest.DataVersion);
        return load.Package;
    }

    private static async Task<GameDataCatalog> LoadCatalogAsync()
    {
        return GameDataCatalog.FromPackage(await LoadPackageAsync());
    }
}
