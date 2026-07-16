using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

internal static class ReviewedItemPropertySemanticProvenanceFactory
{
    private const string SourceId = "poenhance.item-property-semantics";
    private const string InputLabel = "item-property-semantics.json";

    public static GameDataPackageReviewedItemPropertySemanticInput Create(
        string inputPath,
        byte[] inputBytes)
    {
        using var document = JsonDocument.Parse(inputBytes);
        var root = document.RootElement;
        return new GameDataPackageReviewedItemPropertySemanticInput
        {
            SourceId = SourceId,
            Label = InputLabel,
            DisplayPath = Path.GetFileName(Path.GetFullPath(inputPath)),
            SizeBytes = inputBytes.LongLength,
            Sha256 = GameDataPackageHash.ComputeSha256(inputBytes),
            SchemaVersion = root.GetProperty("schemaVersion").GetInt32(),
            ReviewVersion = root.GetProperty("reviewVersion").GetString(),
        };
    }

}
