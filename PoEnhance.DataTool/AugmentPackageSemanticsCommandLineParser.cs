using PoEnhance.DataImport;

namespace PoEnhance.DataTool;

public static class AugmentPackageSemanticsCommandLineParser
{
    public const string CommandName = "augment-package-semantics";

    private static readonly HashSet<string> OptionsWithValues = new(StringComparer.Ordinal)
    {
        "--input-package",
        "--item-property-semantics",
        "--output",
        "--data-version",
    };

    public static AugmentPackageSemanticsCommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        var errors = new List<string>();
        if (args.Count == 0)
        {
            return Invalid($"Missing command. Expected: {CommandName}.");
        }

        if (!string.Equals(args[0], CommandName, StringComparison.Ordinal))
        {
            return Invalid($"Unknown command '{args[0]}'. Expected: {CommandName}.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index];
            if (!OptionsWithValues.Contains(option))
            {
                errors.Add($"Unknown option '{option}'.");
                continue;
            }

            if (values.ContainsKey(option))
            {
                errors.Add($"Duplicate option '{option}'.");
                index++;
                continue;
            }

            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                errors.Add($"Option '{option}' requires a value.");
                continue;
            }

            values[option] = args[index + 1];
            index++;
        }

        AddMissingRequiredOption(values, "--input-package", errors);
        AddMissingRequiredOption(values, "--item-property-semantics", errors);
        AddMissingRequiredOption(values, "--output", errors);
        AddMissingRequiredOption(values, "--data-version", errors);

        if (errors.Count == 0 &&
            TryGetFullPath(values["--input-package"], "--input-package", errors, out var inputPath) &&
            TryGetFullPath(values["--output"], "--output", errors, out var outputPath) &&
            PathsAreEqual(inputPath, outputPath))
        {
            errors.Add("Input package and output paths must resolve to different files.");
        }

        if (errors.Count > 0)
        {
            return new AugmentPackageSemanticsCommandLineParseResult
            {
                Errors = errors,
            };
        }

        return new AugmentPackageSemanticsCommandLineParseResult
        {
            Request = new GameDataPackageSemanticAugmentationRequest
            {
                InputPackagePath = values["--input-package"],
                ItemPropertySemanticsPath = values["--item-property-semantics"],
                OutputPath = values["--output"],
                DataVersion = values["--data-version"],
            },
        };

        static AugmentPackageSemanticsCommandLineParseResult Invalid(string error)
        {
            return new AugmentPackageSemanticsCommandLineParseResult
            {
                Errors = [error],
            };
        }
    }

    public static string GetUsage()
    {
        return """
            Usage:
              PoEnhance.DataTool augment-package-semantics --input-package <path> --item-property-semantics <path> --output <path> --data-version <value>

            Example:
              dotnet run --project .\PoEnhance.DataTool\PoEnhance.DataTool.csproj -- augment-package-semantics --input-package .\artifacts\poenhance-game-data.json --item-property-semantics .\data\semantics\item-property-semantics.json --output .\artifacts\poenhance-game-data.candidate.json --data-version dev-milestone-3-semantics-weapon-dps-v1
            """;
    }

    private static void AddMissingRequiredOption(
        IReadOnlyDictionary<string, string> values,
        string option,
        List<string> errors)
    {
        if (!values.TryGetValue(option, out var value) || string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Missing required option '{option}'.");
        }
    }

    private static bool TryGetFullPath(
        string path,
        string option,
        List<string> errors,
        out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            errors.Add($"Option '{option}' has an invalid path: {exception.Message}");
            fullPath = string.Empty;
            return false;
        }
    }

    private static bool PathsAreEqual(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(first, second, comparison);
    }
}
