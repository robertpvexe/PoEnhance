using System.Text;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoePassiveSkillImporterTests
{
    [Fact]
    public void Import_PreservesDeterministicDuplicateNamesAndSourceIdentity()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
{
  "passives": {
    "65502": { "hash": 65502, "id": "heartseeker626", "name": "Heartseeker" },
    "41119": { "hash": 41119, "id": "heartpierce676", "name": "Lethality" },
    "17": { "hash": 17, "id": "duplicate-a", "name": "Duplicate Name" },
    "18": { "hash": 18, "id": "duplicate-b", "name": "Duplicate Name" }
  }
}
"""));

        var result = new RePoePassiveSkillImporter().Import(stream);

        Assert.Equal(4, result.RecordsImported);
        Assert.Equal(
            [17, 18],
            result.ImportedRecords
                .Where(identity => identity.CanonicalName == "Duplicate Name")
                .Select(identity => identity.PassiveHash));
        var lethality = Assert.Single(result.ImportedRecords, identity => identity.CanonicalName == "Lethality");
        Assert.Equal(41119, lethality.PassiveHash);
        Assert.Equal("heartpierce676", lethality.InternalId);
        var source = Assert.Single(lethality.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal("41119", source.ExternalId);
        Assert.Equal("data/passive_skill_trees/Default.json", source.ExternalUri);
    }
}
