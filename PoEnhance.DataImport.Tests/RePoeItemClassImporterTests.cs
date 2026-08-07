using System.Text;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoeItemClassImporterTests
{
    private readonly RePoeItemClassImporter _importer = new();

    [Fact]
    public void Import_ValidRecord_PreservesIdentityMetadataAndProvenance()
    {
        using var stream = Json("""
            {
              "Ring": {
                "category": "Ring",
                "category_id": "Ring",
                "name": "Rings",
                "influence_tags": ["ring_shaper"]
              }
            }
            """);

        var result = _importer.Import(stream);

        var itemClass = Assert.Single(result.ImportedRecords);
        Assert.Equal("Ring", itemClass.Id);
        Assert.Equal("Rings", itemClass.Name);
        Assert.Equal("Ring", itemClass.CategoryId);
        Assert.Equal(["ring_shaper"], itemClass.InfluenceTagIds);
        var source = Assert.Single(itemClass.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal("Ring", source.ExternalId);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Import_MalformedAndDuplicateRecords_AreVisibleAndSkipped()
    {
        using var stream = Json("""
            {
              "Ring": { "name": "Rings", "influence_tags": null },
              "Ring": { "name": "Duplicate", "influence_tags": null },
              "Bad": { "name": "", "influence_tags": "not-an-array" }
            }
            """);

        var result = _importer.Import(stream);

        Assert.Equal(3, result.SourceRecordsRead);
        Assert.Equal(1, result.RecordsImported);
        Assert.Equal(2, result.RecordsSkipped);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RePoeImportDiagnosticCodes.ItemClassRecordDuplicate);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RePoeImportDiagnosticCodes.ItemClassRecordMalformed);
    }

    private static MemoryStream Json(string value) => new(Encoding.UTF8.GetBytes(value));
}
