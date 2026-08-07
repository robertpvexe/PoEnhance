using System.Text;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoeTagImporterTests
{
    [Fact]
    public void Import_ValidMalformedAndDuplicateTags_ClassifiesEveryEntry()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""["ring", "default", "ring", "", 4]"""));

        var result = new RePoeTagImporter().Import(stream);

        Assert.Equal(5, result.SourceRecordsRead);
        Assert.Equal(2, result.RecordsImported);
        Assert.Equal(3, result.RecordsSkipped);
        Assert.Equal(["default", "ring"], result.ImportedRecords.Select(tag => tag.Id));
        Assert.All(result.ImportedRecords, tag =>
        {
            var source = Assert.Single(tag.Sources);
            Assert.Equal("repoe", source.SourceId);
            Assert.Equal(tag.Id, source.ExternalId);
        });
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RePoeImportDiagnosticCodes.TagRecordDuplicate);
        Assert.Equal(2, result.Diagnostics.Count(diagnostic => diagnostic.Code == RePoeImportDiagnosticCodes.TagRecordMalformed));
    }
}
