using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoeModsByBaseImporterTests
{
    [Fact]
    public void Import_ValidRelationship_PreservesMeaningContextAndProvenance()
    {
        var bases = new RePoeBaseItemImporter().Import(RePoeImportTestFixtures.ReducedBaseItemsPath);
        var modifiers = new RePoeModifierImporter().Import(RePoeImportTestFixtures.ReducedModsPath);

        var result = new RePoeModsByBaseImporter().Import(
            RePoeImportTestFixtures.ReducedModsByBasePath,
            RePoeImportTestFixtures.ReducedBaseItemsPath,
            RePoeImportTestFixtures.ReducedModsPath,
            bases.ImportedRecords,
            modifiers.ImportedRecords);

        Assert.False(result.HasErrors);
        Assert.NotNull(result.Evidence);
        Assert.Equal(BaseModifierEvidenceSemantics.PositiveAndContextualOnly, result.Evidence.Semantics);
        Assert.Equal(BaseModifierEvidenceCoverage.Partial, result.Evidence.Coverage);
        Assert.Equal(1, result.Audit.SourceBaseEntriesRead);
        Assert.Equal(2, result.Audit.SourceRelationshipsRead);
        Assert.Equal(2, result.Audit.RelationshipsImported);
        Assert.Equal(
            result.Audit.SourceRelationshipsRead,
            result.Audit.RelationshipsImported +
            result.Audit.RelationshipsUnavailableBases +
            result.Audit.RelationshipsUnavailableStatlessModifiers +
            result.Audit.UnresolvedRelationships);
        Assert.Equal(0, result.Audit.UnresolvedRelationships);
        var group = Assert.Single(result.Evidence.Groups);
        Assert.Equal("Metadata/Items/Rings/Ring4", Assert.Single(group.BaseItemIds));
        Assert.Equal(["AbyssFireResistanceJewel1", "AbyssJewelAddedLife1"], group.Modifiers.Select(modifier => modifier.ModifierId));
        Assert.All(group.Modifiers, modifier => Assert.True(modifier.ReportedWeight > 0));
        Assert.Contains(group.Modifiers, modifier => modifier.SourceGenerationBucket == "prefix");
        Assert.StartsWith("mods_by_base.json#/", Assert.Single(group.Sources).ExternalId, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("UnknownBase", "AbyssJewelAddedLife1", RePoeImportDiagnosticCodes.ModsByBaseUnknownBase)]
    [InlineData("Metadata/Items/Rings/Ring4", "MissingModifier", RePoeImportDiagnosticCodes.ModsByBaseUnknownModifier)]
    public void Import_UnknownReference_FailsClosed(string baseId, string modifierId, string expectedCode)
    {
        using var workspace = Workspace.Create(baseId, modifierId, statless: false, duplicate: false);

        var result = workspace.Import();

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Fact]
    public void Import_StatlessModifier_IsClassifiedSeparatelyAndCoverageRemainsPartial()
    {
        using var workspace = Workspace.Create("Metadata/Items/Rings/Ring4", "StatlessMod", statless: true, duplicate: false);

        var result = workspace.Import();

        Assert.False(result.HasErrors);
        Assert.Equal(1, result.Audit.RelationshipsUnavailableStatlessModifiers);
        Assert.Equal(0, result.Audit.UnknownModifierRelationships);
        Assert.Equal(0, result.Audit.UnresolvedRelationships);
        Assert.Equal(BaseModifierEvidenceCoverage.Partial, result.Evidence!.Coverage);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.ModsByBaseStatlessModifierUnavailable &&
            diagnostic.Severity == ImportDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Import_DuplicateBaseModifierRelationship_IsReportedAsError()
    {
        using var workspace = Workspace.Create("Metadata/Items/Rings/Ring4", "AbyssJewelAddedLife1", statless: false, duplicate: true);

        var result = workspace.Import();

        Assert.True(result.HasErrors);
        Assert.True(result.Audit.DuplicateRelationships > 0);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RePoeImportDiagnosticCodes.ModsByBaseDuplicateRelationship);
    }

    private sealed class Workspace : IDisposable
    {
        private Workspace(string root, string modsByBasePath, string modsPath)
        {
            Root = root;
            ModsByBasePath = modsByBasePath;
            ModsPath = modsPath;
        }

        public string Root { get; }
        public string ModsByBasePath { get; }
        public string ModsPath { get; }

        public static Workspace Create(string baseId, string modifierId, bool statless, bool duplicate)
        {
            var root = Path.Combine(Path.GetTempPath(), "PoEnhance.ModsByBase.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var modsPath = Path.Combine(root, "mods.json");
            File.Copy(RePoeImportTestFixtures.ReducedModsPath, modsPath);
            if (statless)
            {
                var json = File.ReadAllText(modsPath).TrimEnd();
                File.WriteAllText(modsPath, json[..^1] + ",\n\"StatlessMod\": { \"domain\": \"item\", \"generation_type\": \"prefix\", \"groups\": [\"Statless\"], \"stats\": [] }\n}");
            }

            var secondGroup = duplicate
                ? $",\n\"other\": {{ \"bases\": [\"{baseId}\"], \"mods\": {{ \"prefix\": {{ \"Type\": {{ \"{modifierId}\": 5 }} }} }}, \"conditional_mods\": null }}"
                : string.Empty;
            var modsByBasePath = Path.Combine(root, "mods_by_base.json");
            File.WriteAllText(modsByBasePath, $$"""
                {
                  "Class": {
                    "tags": {
                      "bases": ["{{baseId}}"],
                      "mods": { "prefix": { "Type": { "{{modifierId}}": 10 } } },
                      "conditional_mods": null
                    }{{secondGroup}}
                  }
                }
                """);
            return new Workspace(root, modsByBasePath, modsPath);
        }

        public RePoeModsByBaseImportResult Import()
        {
            var bases = new RePoeBaseItemImporter().Import(RePoeImportTestFixtures.ReducedBaseItemsPath);
            var modifiers = new RePoeModifierImporter().Import(ModsPath);
            return new RePoeModsByBaseImporter().Import(
                ModsByBasePath,
                RePoeImportTestFixtures.ReducedBaseItemsPath,
                ModsPath,
                bases.ImportedRecords,
                modifiers.ImportedRecords);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }
}
