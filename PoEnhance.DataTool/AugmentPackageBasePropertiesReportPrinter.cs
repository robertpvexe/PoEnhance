using PoEnhance.DataImport;

namespace PoEnhance.DataTool;

public static class AugmentPackageBasePropertiesReportPrinter
{
    public static void Print(GameDataPackageWeaponPropertyAugmentationResult result, TextWriter writer)
    {
        writer.WriteLine($"Success: {result.IsSuccess}");
        writer.WriteLine($"ItemBases: {result.ItemBaseCount}");
        writer.WriteLine($"Armour: {result.ItemBasesWithArmour}");
        writer.WriteLine($"EvasionRating: {result.ItemBasesWithEvasionRating}");
        writer.WriteLine($"EnergyShield: {result.ItemBasesWithEnergyShield}");
        writer.WriteLine($"Ward: {result.ItemBasesWithWard}");
        writer.WriteLine($"ChanceToBlock: {result.ItemBasesWithChanceToBlock}");
        writer.WriteLine($"BaseItemsSHA256: {result.BaseItemsSha256}");
        writer.WriteLine($"OutputFileSizeBytes: {result.OutputSizeBytes}");
        writer.WriteLine($"SHA256: {result.OutputSha256}");
        foreach (var (itemClass, count) in result.MissingDefencePropertiesByClass)
        {
            writer.WriteLine($"Missing[{itemClass}]: {count}");
        }
        foreach (var diagnostic in result.Diagnostics)
        {
            writer.WriteLine($"{diagnostic.Severity}: {diagnostic.Code}: {diagnostic.Message}");
        }
    }
}
