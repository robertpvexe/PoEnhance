using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class PoBFoulbornRelationshipImporterTests
{
    private const string RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding";
    private const string Tag = "v2.67.2";
    private const string CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478";

    [Fact]
    public void Import_JsoncRelationships_PreserveDirectionCardinalityAndProvenanceDeterministically()
    {
        const string jsonc = """
            // generated relationship map
            {
              "Second Item": {
                "normal.shared": "foulborn.second",
              },
              "First Item": {
                "normal.two": "foulborn.two",
                "normal.shared": "foulborn.first",
              },
            }
            """;
        var catalog = Catalog("First Item", "Second Item");
        var modifiers = Modifiers(
            "normal.shared",
            "normal.two",
            "foulborn.first",
            "foulborn.second",
            "foulborn.two");

        var first = Import(jsonc, catalog, modifiers);
        var second = Import(jsonc, catalog, modifiers);

        Assert.DoesNotContain(first.Diagnostics, diagnostic =>
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
        Assert.Equal(2, first.ItemRecordsRead);
        Assert.Equal(3, first.RelationshipsRead);
        Assert.Equal(3, first.RelationshipsLinked);
        Assert.Equal(0, first.RelationshipsUnsupported);
        Assert.Equal(
            first.Relationships.Select(relationship => relationship.Id),
            second.Relationships.Select(relationship => relationship.Id));
        Assert.Equal(
            ["First Item", "First Item", "Second Item"],
            first.Relationships.Select(relationship => relationship.ItemName));

        var shared = first.Relationships
            .Where(relationship => relationship.NormalModifierId == "normal.shared")
            .ToArray();
        Assert.Equal(2, shared.Length);
        Assert.Equal(["foulborn.first", "foulborn.second"],
            shared.Select(relationship => relationship.FoulbornModifierId));
        Assert.All(shared, relationship =>
        {
            Assert.Equal(UniqueFoulbornModifierRelationshipStatus.Exact, relationship.Status);
            Assert.NotNull(relationship.UniqueItemId);
            Assert.Equal(first.SourceObservation!.Id, relationship.SourceObservationId);
        });
        Assert.Equal("src/Data/ModFoulbornMap.jsonc", first.SourceObservation!.SourcePath);
        Assert.Equal(CommitSha, first.SourceObservation.CommitSha);
        Assert.Equal(64, first.SourceObservation.SourceFileSha256!.Length);
    }

    [Fact]
    public void Import_NonExactItemName_IsRetainedUnsupportedWithoutFuzzyPromotion()
    {
        var result = Import(
            """{ "Mjolner": { "normal": "foulborn" } }""",
            Catalog("Mjölner"),
            Modifiers("normal", "foulborn"));

        Assert.Equal(1, result.RelationshipsRead);
        Assert.Equal(0, result.RelationshipsLinked);
        Assert.Equal(1, result.RelationshipsUnsupported);
        var relationship = Assert.Single(result.Relationships);
        Assert.Equal("Mjolner", relationship.ItemName);
        Assert.Null(relationship.UniqueItemId);
        Assert.Equal(UniqueFoulbornModifierRelationshipStatus.Unsupported, relationship.Status);
        Assert.Equal("FOULBORN_UNIQUE_IDENTITY_NOT_FOUND", relationship.DiagnosticCode);
    }

    [Fact]
    public void Import_DuplicateAndConflictingItemScopedSourceRelations_FailVisibly()
    {
        var result = Import(
            """
            {
              "Test Item": {
                "normal": "foulborn.one",
                "normal": "foulborn.one",
                "normal": "foulborn.two"
              }
            }
            """,
            Catalog("Test Item"),
            Modifiers("normal", "foulborn.one", "foulborn.two"));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.PoBFoulbornDuplicateRelationship &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.PoBFoulbornConflictingRelationship &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    private static PoBFoulbornRelationshipImportResult Import(
        string content,
        UniqueItemCatalog catalog,
        IReadOnlyList<ModifierDefinition> modifiers)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-foulborn-{Guid.NewGuid():N}.jsonc");
        try
        {
            File.WriteAllText(path, content);
            return new PoBFoulbornRelationshipImporter().Import(
                path,
                "src/Data/ModFoulbornMap.jsonc",
                RepositoryUri,
                Tag,
                CommitSha,
                catalog,
                modifiers);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static UniqueItemCatalog Catalog(params string[] names) => new()
    {
        Items = names.Select(name => new UniqueItemIdentity
        {
            Id = $"unique:{name}",
            CanonicalName = name,
            Kind = UniqueItemKind.Ordinary,
            BaseTypeEvidence = ["Test Base"],
            Versions =
            [
                new UniqueItemVersionObservation
                {
                    Id = $"version:{name}",
                    Label = "Current",
                    Role = UniqueItemVersionRole.Current,
                    BaseType = "Test Base",
                },
            ],
        }).ToArray(),
    };

    private static IReadOnlyList<ModifierDefinition> Modifiers(params string[] ids) =>
        ids.Select(id => new ModifierDefinition { Id = id }).ToArray();
}
