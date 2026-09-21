using System.Text.Json;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class SoulTakerMultiLineCorruptedProviderTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public async Task SoulTakerBleedMultiline_ProviderComponents_HavePerLineStatSubsetsWithoutSiblingLeakage()
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = await ReadCaptureClipboardAsync("20260919-194458-274-Soul Taker.json");
        var parsed = parser.Parse(raw);
        var baseResolution = baseResolver.Resolve(parsed, catalog);
        var resolutions = modifierResolver.Resolve(parsed, catalog, baseResolution);
        var result = mapper.CreateDraft(parsed, baseResolution, resolutions, catalog);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var draft = Assert.IsType<TradeSearchDraft>(result.Draft);

        var bleed = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Bleeding on Hit", StringComparison.OrdinalIgnoreCase));
        var damage = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains(
                "Attack Damage against Bleeding",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, bleed.ResolutionStatus);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, damage.ResolutionStatus);
        Assert.Equal(
            "V2ChanceToBleedOnHitAndIncreasedDamageToBleedingTargetsCorrupted_",
            bleed.ResolvedModifierId);
        Assert.Equal(bleed.ResolvedModifierId, damage.ResolvedModifierId);
        Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, bleed.StatMappingProof);
        Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, damage.StatMappingProof);

        Assert.Equal(["local_chance_to_bleed_on_hit_%"], bleed.ResolvedStatIds.ToArray());
        Assert.Equal(["attack_damage_vs_bleeding_enemies_+%"], damage.ResolvedStatIds.ToArray());

        // Bleed-only must not carry sibling 30-40 bounds.
        Assert.Equal(20m, bleed.RequestedMinimum);
        Assert.Null(bleed.RequestedMaximum);
        Assert.DoesNotContain(30m, new[] { bleed.RequestedMinimum, bleed.RequestedMaximum });
        Assert.DoesNotContain(40m, new[] { bleed.RequestedMinimum, bleed.RequestedMaximum });

        // Conditional-damage-only must not carry bleed's fixed 20.
        Assert.Equal(36m, damage.RequestedMinimum);
        Assert.Null(damage.RequestedMaximum);
        Assert.NotEqual(20m, damage.RequestedMinimum);

        // Selecting both keeps distinct components sharing ModId provenance without collapse.
        Assert.Equal(2, draft.ModifierFilters.Count(filter =>
            filter.ResolvedModifierId == bleed.ResolvedModifierId));
        Assert.NotEqual(bleed.SourceLineIndex, damage.SourceLineIndex);
        Assert.NotEqual(bleed.SourceComponentIndex, damage.SourceComponentIndex);
    }

    private static async Task<string> ReadCaptureClipboardAsync(string fileName)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-A.5.22-Corrupted-Implicit-Provenance",
            fileName);
        Assert.True(File.Exists(path), $"Missing ReplayReady capture: {path}");
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);
        var raw = document.RootElement
            .GetProperty("replayContext")
            .GetProperty("rawClipboardText")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(raw));
        return raw!;
    }

    private static async Task<GameDataCatalog> LoadActiveCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        return GameDataCatalog.FromPackage(load.Package!);
    }
}
