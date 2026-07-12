namespace PoEnhance.DataTool.Tests;

public sealed class BuildPackageCommandLineParserTests
{
    [Fact]
    public void Parse_ValidBuildPackageCommand_ReturnsRequest()
    {
        var result = BuildPackageCommandLineParser.Parse(
        [
            "build-package",
            "--base-items",
            "base_items.json",
            "--mods",
            "mods.json",
            "--stats",
            "stats.json",
            "--translations",
            "stat_translations.json",
            "--output",
            "package.json",
            "--data-version",
            "dev-001",
            "--league",
            "Mercenaries",
            "--patch",
            "3.26.0",
            "--source-version",
            "repoe-commit",
            "--verbose-diagnostics",
        ]);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Request);
        Assert.Equal("base_items.json", result.Request.BaseItemsPath);
        Assert.Equal("mods.json", result.Request.ModsPath);
        Assert.Equal("stats.json", result.Request.StatsPath);
        Assert.Equal("stat_translations.json", result.Request.TranslationsPath);
        Assert.Equal("package.json", result.Request.OutputPath);
        Assert.Equal("dev-001", result.Request.DataVersion);
        Assert.Equal("Mercenaries", result.Request.League);
        Assert.Equal("3.26.0", result.Request.Patch);
        Assert.Equal("repoe-commit", result.Request.SourceVersion);
        Assert.True(result.VerboseDiagnostics);
    }

    [Fact]
    public void Parse_MissingCommand_ReturnsError()
    {
        var result = BuildPackageCommandLineParser.Parse([]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Missing command", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_UnknownCommand_ReturnsError()
    {
        var result = BuildPackageCommandLineParser.Parse(["not-a-command"]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Unknown command", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_UnknownOption_ReturnsError()
    {
        var result = BuildPackageCommandLineParser.Parse(
        [
            "build-package",
            "--base-items",
            "base_items.json",
            "--mods",
            "mods.json",
            "--stats",
            "stats.json",
            "--translations",
            "stat_translations.json",
            "--output",
            "package.json",
            "--data-version",
            "dev-001",
            "--wat",
            "value",
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Unknown option", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_MissingOptionValue_ReturnsError()
    {
        var result = BuildPackageCommandLineParser.Parse(
        [
            "build-package",
            "--base-items",
            "--mods",
            "mods.json",
            "--stats",
            "stats.json",
            "--translations",
            "stat_translations.json",
            "--output",
            "package.json",
            "--data-version",
            "dev-001",
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("requires a value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_MissingRequiredOption_ReturnsError()
    {
        var result = BuildPackageCommandLineParser.Parse(
        [
            "build-package",
            "--base-items",
            "base_items.json",
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("--mods", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("--data-version", StringComparison.Ordinal));
    }
}
