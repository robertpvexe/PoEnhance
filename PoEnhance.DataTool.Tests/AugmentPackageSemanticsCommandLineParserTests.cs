namespace PoEnhance.DataTool.Tests;

public sealed class AugmentPackageSemanticsCommandLineParserTests
{
    [Fact]
    public void Parse_AllRequiredArguments_ReturnsRequest()
    {
        var result = AugmentPackageSemanticsCommandLineParser.Parse(CreateValidArguments());

        Assert.True(result.IsValid);
        Assert.NotNull(result.Request);
        Assert.Equal("active.json", result.Request.InputPackagePath);
        Assert.Equal("semantics.json", result.Request.ItemPropertySemanticsPath);
        Assert.Equal("candidate.json", result.Request.OutputPath);
        Assert.Equal("candidate-version", result.Request.DataVersion);
    }

    [Theory]
    [InlineData("--input-package")]
    [InlineData("--item-property-semantics")]
    [InlineData("--output")]
    [InlineData("--data-version")]
    public void Parse_MissingRequiredArgument_ReturnsClearError(string option)
    {
        var args = CreateValidArguments();
        var index = args.IndexOf(option);
        args.RemoveRange(index, 2);

        var result = AugmentPackageSemanticsCommandLineParser.Parse(args);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains(option, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("--input-package")]
    [InlineData("--item-property-semantics")]
    [InlineData("--output")]
    [InlineData("--data-version")]
    public void Parse_BlankRequiredArgument_ReturnsClearError(string option)
    {
        var args = CreateValidArguments();
        args[args.IndexOf(option) + 1] = "   ";

        var result = AugmentPackageSemanticsCommandLineParser.Parse(args);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains(option, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_SameResolvedInputAndOutputPath_ReturnsError()
    {
        var args = CreateValidArguments();
        args[args.IndexOf("--output") + 1] = Path.Combine(".", "active.json");

        var result = AugmentPackageSemanticsCommandLineParser.Parse(args);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("different files", StringComparison.Ordinal));
    }

    [Fact]
    public void GetUsage_DocumentsAugmentCommandAndAllRequiredArguments()
    {
        var usage = AugmentPackageSemanticsCommandLineParser.GetUsage();

        Assert.Contains("augment-package-semantics", usage, StringComparison.Ordinal);
        Assert.Contains("--input-package <path>", usage, StringComparison.Ordinal);
        Assert.Contains("--item-property-semantics <path>", usage, StringComparison.Ordinal);
        Assert.Contains("--output <path>", usage, StringComparison.Ordinal);
        Assert.Contains("--data-version <value>", usage, StringComparison.Ordinal);
        Assert.Contains("aps-crit-defence-semantics.candidate.json", usage, StringComparison.Ordinal);
    }

    private static List<string> CreateValidArguments()
    {
        return
        [
            "augment-package-semantics",
            "--input-package",
            "active.json",
            "--item-property-semantics",
            "semantics.json",
            "--output",
            "candidate.json",
            "--data-version",
            "candidate-version",
        ];
    }
}
