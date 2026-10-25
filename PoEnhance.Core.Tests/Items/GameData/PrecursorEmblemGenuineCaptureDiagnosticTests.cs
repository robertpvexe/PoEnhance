using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// U.2.5D — genuine Precursor Sapphire Ctrl+C capture: runtime GeneratedCandidate recovery
/// for AID <c>+N%</c> Suppress and AID range-annotated Duration against packaged PoB lines.
/// </summary>
public sealed class PrecursorEmblemGenuineCaptureDiagnosticTests
{
    private static readonly Lazy<GameDataCatalog> Catalog = new(LoadCatalog);

    [Fact]
    public void GenuinePrecursorSapphire_AllSevenDisplayedRows_RecoverExactIncludingTwoFormerUnsupported()
    {
        var raw = File.ReadAllText(FindReplay(
            "Precursors-Emblem-Sapphire-Frenzy-Suppress.clipboard.txt"));
        Assert.Contains("Precursor's Emblem", raw, StringComparison.Ordinal);
        Assert.Contains("Sapphire Ring", raw, StringComparison.Ordinal);
        Assert.Contains("Note: ~b/o 200 chaos", raw, StringComparison.Ordinal);

        var parsed = new ItemTextParser().Parse(raw);
        Assert.Equal(7, parsed.Modifiers.Count);
        Assert.DoesNotContain(
            parsed.Modifiers,
            mod => mod.Text.Contains("~b/o", StringComparison.OrdinalIgnoreCase));

        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, Catalog.Value);
        var unique = new ParsedUniqueItemResolver().Resolve(parsed, Catalog.Value, baseResolution);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);
        Assert.Equal("Precursor's Emblem", unique.Identity!.CanonicalName);
        Assert.Equal(
            "Sapphire Ring",
            Assert.Single(unique.CompatibleVersions).BaseType);

        var suppressIndex = IndexOfModifier(
            parsed,
            "Suppress Spell Damage per Frenzy Charge");
        Assert.Contains(
            "(40% of Damage from Suppressed Hits",
            parsed.Modifiers[suppressIndex].ReminderLines.Single(),
            StringComparison.Ordinal);

        var suppress = Assert.Single(
            unique.ModifierBlocks,
            block => block.ParsedModifierIndex == suppressIndex);
        Assert.True(suppress.IsResolved);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, suppress.SourceSemantics);
        Assert.Null(suppress.DiagnosticCode);
        Assert.Equal(
            ["spell_suppression_chance_%_per_frenzy_charge"],
            suppress.StatIds.ToArray());
        Assert.Contains("ChargeBonusDodgeChancePerFrenzyCharge", suppress.ModifierIds);

        var durationIndex = IndexOfModifier(parsed, "increased Frenzy Charge Duration");
        var duration = Assert.Single(
            unique.ModifierBlocks,
            block => block.ParsedModifierIndex == durationIndex);
        Assert.True(duration.IsResolved);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, duration.SourceSemantics);
        Assert.Null(duration.DiagnosticCode);
        Assert.Equal(["base_frenzy_charge_duration_+%"], duration.StatIds.ToArray());
        Assert.Contains("ChargeBonusFrenzyChargeDuration", duration.ModifierIds);

        var resolutions = new ParsedItemModifierCandidateResolver()
            .Resolve(parsed, Catalog.Value, baseResolution);
        var draft = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            resolutions,
            Catalog.Value);
        Assert.True(draft.IsSuccess);
        Assert.Equal(7, draft.Draft!.ModifierFilters.Count);

        Assert.DoesNotContain(
            draft.Draft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Suppressed Hits",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            draft.Draft.ModifierFilters,
            component => component.OriginalText.Contains(
                "~b/o",
                StringComparison.OrdinalIgnoreCase));

        var anger = Assert.Single(
            draft.Draft.ModifierFilters,
            component => component.OriginalText.Contains("Anger has", StringComparison.Ordinal));
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, anger.ResolutionStatus);
        Assert.Equal("V2ManaReservationEfficiencyAngerCorrupted", anger.ResolvedModifierId);

        AssertExactGenerated(
            draft.Draft.ModifierFilters,
            "Suppress Spell Damage per Frenzy Charge",
            ["spell_suppression_chance_%_per_frenzy_charge"],
            1m);
        AssertExactGenerated(
            draft.Draft.ModifierFilters,
            "increased Frenzy Charge Duration",
            ["base_frenzy_charge_duration_+%"],
            25m);
        AssertExactGenerated(
            draft.Draft.ModifierFilters,
            "increased Movement Speed per Frenzy Charge",
            ["movement_velocity_+%_per_frenzy_charge"],
            1m);

        Assert.All(
            draft.Draft.ModifierFilters.Where(component =>
                component.OriginalText.Contains("maximum Life", StringComparison.Ordinal) ||
                component.OriginalText.Contains("maximum Energy Shield", StringComparison.Ordinal) ||
                component.OriginalText.Contains("to Dexterity", StringComparison.Ordinal)),
            component => Assert.Equal(
                ModifierCandidateResolutionStatus.Exact,
                component.ResolutionStatus));
    }

    [Fact]
    public void GenuinePrecursorSapphire_DurationAnnotationOutsideCatalogDomain_StaysUnresolved()
    {
        // Negative: AID range outside packaged PoB (20-120) must not claim ChargeBonus Duration.
        var raw = File.ReadAllText(FindReplay(
            "Precursors-Emblem-Sapphire-Frenzy-Suppress.clipboard.txt"))
            .Replace(
                "25(20-40)% increased Frenzy Charge Duration",
                "25(130-150)% increased Frenzy Charge Duration",
                StringComparison.Ordinal);

        var parsed = new ItemTextParser().Parse(raw);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, Catalog.Value);
        var unique = new ParsedUniqueItemResolver().Resolve(parsed, Catalog.Value, baseResolution);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);

        var durationIndex = parsed.Modifiers
            .Select((mod, index) => (mod, index))
            .Single(pair => pair.mod.Text.Contains(
                "increased Frenzy Charge Duration",
                StringComparison.Ordinal))
            .index;
        var duration = Assert.Single(
            unique.ModifierBlocks,
            block => block.ParsedModifierIndex == durationIndex);
        Assert.False(duration.IsResolved);
        Assert.Equal("UNIQUE_GENERATED_CANDIDATE_NOT_FOUND", duration.DiagnosticCode);
        Assert.Empty(duration.StatIds);
    }

    private static int IndexOfModifier(ParsedItem parsed, string textContains)
    {
        for (var index = 0; index < parsed.Modifiers.Count; index++)
        {
            if (parsed.Modifiers[index].Text.Contains(textContains, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Modifier containing '{textContains}' not found.");
    }

    private static void AssertExactGenerated(
        IReadOnlyList<ResolvedSearchComponent> filters,
        string textContains,
        IReadOnlyList<string> expectedStatIds,
        decimal observedMin)
    {
        var component = Assert.Single(
            filters,
            filter => filter.OriginalText.Contains(textContains, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, component.UniqueSourceSemantics);
        Assert.True(component.HasExactUniqueSourceProvenance);
        Assert.Equal(expectedStatIds, component.ResolvedStatIds.ToArray());
        Assert.Equal([observedMin], component.ObservedNumericValues.ToArray());
        Assert.Equal(observedMin, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
    }

    private static GameDataCatalog LoadCatalog()
    {
        var path = Path.Combine(FindRepoRoot(), "artifacts", "poenhance-game-data.json");
        var result = GameDataPackageLoader.LoadFromFileAsync(path).GetAwaiter().GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(result.Package));
    }

    private static string FindReplay(string fileName) =>
        Path.Combine(
            FindRepoRoot(),
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            fileName);

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")) ||
                File.Exists(Path.Combine(directory.FullName, "PoEnhance.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("PoEnhance repo root");
    }
}
