namespace PoEnhance.DataTool.Tests;

public sealed class AugmentPackageBasePropertiesCommandLineParserTests
{
    [Fact]
    public void Parse_ValidArgumentsCreateNarrowAugmentationRequest()
    {
        var result = AugmentPackageBasePropertiesCommandLineParser.Parse(
        [
            "augment-package-base-properties",
            "--input-package", "active.json",
            "--base-items", "base_items.json",
            "--output", "candidate.json",
            "--data-version", "defence-v1",
        ]);

        Assert.True(result.IsValid);
        Assert.Equal("active.json", result.Request!.InputPackagePath);
        Assert.Equal("base_items.json", result.Request.BaseItemsPath);
        Assert.Equal("candidate.json", result.Request.OutputPath);
        Assert.Equal("defence-v1", result.Request.DataVersion);
    }

    [Fact]
    public void Parse_MissingOptionFailsClearly()
    {
        var result = AugmentPackageBasePropertiesCommandLineParser.Parse(
        [
            "augment-package-base-properties",
            "--input-package", "active.json",
            "--output", "candidate.json",
            "--data-version", "defence-v1",
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("--base-items", StringComparison.Ordinal));
    }
}
