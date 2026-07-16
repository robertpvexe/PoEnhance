using PoEnhance.DataImport;

namespace PoEnhance.DataTool;

public static class BuildPackageCommandLineParser
{
    private const string BuildPackageCommandName = "build-package";

    private static readonly HashSet<string> OptionsWithValues = new(StringComparer.Ordinal)
    {
        "--base-items",
        "--mods",
        "--stats",
        "--translations",
        "--item-property-semantics",
        "--output",
        "--source-root",
        "--source-data-root",
        "--source-uri",
        "--source-branch",
        "--data-version",
        "--league",
        "--patch",
        "--source-version",
    };

    private static readonly HashSet<string> FlagOptions = new(StringComparer.Ordinal)
    {
        "--verbose-diagnostics",
    };

    public static BuildPackageCommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        var errors = new List<string>();

        if (args.Count == 0)
        {
            return Invalid("Missing command. Expected: build-package.");
        }

        if (!string.Equals(args[0], BuildPackageCommandName, StringComparison.Ordinal))
        {
            return Invalid($"Unknown command '{args[0]}'. Expected: build-package.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index];
            if (FlagOptions.Contains(option))
            {
                if (!flags.Add(option))
                {
                    errors.Add($"Duplicate option '{option}'.");
                }

                continue;
            }

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

        AddMissingRequiredOption(values, "--base-items", errors);
        AddMissingRequiredOption(values, "--mods", errors);
        AddMissingRequiredOption(values, "--stats", errors);
        AddMissingRequiredOption(values, "--translations", errors);
        AddMissingRequiredOption(values, "--item-property-semantics", errors);
        AddMissingRequiredOption(values, "--output", errors);
        AddMissingRequiredOption(values, "--source-root", errors);
        AddMissingRequiredOption(values, "--source-data-root", errors);
        AddMissingRequiredOption(values, "--source-uri", errors);
        AddMissingRequiredOption(values, "--source-branch", errors);
        AddMissingRequiredOption(values, "--source-version", errors);
        AddMissingRequiredOption(values, "--data-version", errors);

        if (errors.Count > 0)
        {
            return new BuildPackageCommandLineParseResult
            {
                Errors = errors,
            };
        }

        return new BuildPackageCommandLineParseResult
        {
            Request = new GameDataPackageBuildRequest
            {
                BaseItemsPath = values["--base-items"],
                ModsPath = values["--mods"],
                StatsPath = values["--stats"],
                TranslationsPath = values["--translations"],
                ItemPropertySemanticsPath = values["--item-property-semantics"],
                OutputPath = values["--output"],
                SourceRootPath = values["--source-root"],
                SourceDataRootPath = values["--source-data-root"],
                SourceUri = values["--source-uri"],
                SourceBranch = values["--source-branch"],
                DataVersion = values["--data-version"],
                League = values.GetValueOrDefault("--league"),
                Patch = values.GetValueOrDefault("--patch"),
                SourceVersion = values.GetValueOrDefault("--source-version"),
            },
            VerboseDiagnostics = flags.Contains("--verbose-diagnostics"),
        };

        static BuildPackageCommandLineParseResult Invalid(string error)
        {
            return new BuildPackageCommandLineParseResult
            {
                Errors = [error],
            };
        }
    }

    public static string GetUsage()
    {
        return """
            Usage:
              PoEnhance.DataTool build-package --base-items <path> --mods <path> --stats <path> --translations <path> --item-property-semantics <path> --output <path> --source-root <git-checkout> --source-data-root <data-root> --source-uri <uri> --source-branch <branch> --source-version <sha> --data-version <value> [--league <value>] [--patch <value>] [--verbose-diagnostics]

            Example:
              dotnet run --project .\PoEnhance.DataTool -- build-package --base-items .\data\repoe\base_items.json --mods .\data\repoe\mods.json --stats .\data\repoe\stats.json --translations .\data\repoe\stat_translations.json --item-property-semantics .\data\semantics\item-property-semantics.json --output .\artifacts\poenhance-game-data.json --source-root .\local-data\repoe --source-data-root .\data\repoe --source-uri https://github.com/repoe-fork/repoe --source-branch master --source-version c50acab2ed660a70511e7f91ee09db4e632089e4 --data-version dev-001
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
}
