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
        "--item-classes",
        "--tags",
        "--mods-by-base",
        "--item-property-semantics",
        "--output",
        "--source-snapshot-dir",
        "--source-root",
        "--source-data-root",
        "--source-uri",
        "--source-branch",
        "--data-version",
        "--league",
        "--patch",
        "--source-version",
        "--historical-base-items",
        "--historical-mods",
        "--historical-stats",
        "--historical-translations",
        "--historical-source-root",
        "--historical-source-data-root",
        "--historical-source-uri",
        "--historical-source-branch",
        "--historical-source-version",
        "--historical-data-version",
        "--pob-uniques",
        "--pob-source-root",
        "--pob-source-uri",
        "--pob-source-tag",
        "--pob-source-version",
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
        AddMissingRequiredOption(values, "--item-classes", errors);
        AddMissingRequiredOption(values, "--tags", errors);
        AddMissingRequiredOption(values, "--mods-by-base", errors);
        AddMissingRequiredOption(values, "--item-property-semantics", errors);
        AddMissingRequiredOption(values, "--output", errors);
        AddMissingRequiredOption(values, "--source-root", errors);
        AddMissingRequiredOption(values, "--source-data-root", errors);
        AddMissingRequiredOption(values, "--source-uri", errors);
        AddMissingRequiredOption(values, "--source-branch", errors);
        AddMissingRequiredOption(values, "--source-version", errors);
        AddMissingRequiredOption(values, "--data-version", errors);

        var historicalOptions = new[]
        {
            "--historical-base-items",
            "--historical-mods",
            "--historical-stats",
            "--historical-translations",
            "--historical-source-root",
            "--historical-source-data-root",
            "--historical-source-uri",
            "--historical-source-branch",
            "--historical-source-version",
            "--historical-data-version",
        };
        if (historicalOptions.Any(values.ContainsKey))
        {
            foreach (var option in historicalOptions)
            {
                AddMissingRequiredOption(values, option, errors);
            }
        }

        var pobOptions = new[]
        {
            "--pob-uniques",
            "--pob-source-root",
            "--pob-source-uri",
            "--pob-source-tag",
            "--pob-source-version",
        };
        if (pobOptions.Any(values.ContainsKey))
        {
            foreach (var option in pobOptions)
            {
                AddMissingRequiredOption(values, option, errors);
            }
        }

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
                ItemClassesPath = values["--item-classes"],
                TagsPath = values["--tags"],
                ModsByBasePath = values["--mods-by-base"],
                ItemPropertySemanticsPath = values["--item-property-semantics"],
                OutputPath = values["--output"],
                SourceSnapshotDirectory = values.GetValueOrDefault("--source-snapshot-dir"),
                SourceRootPath = values["--source-root"],
                SourceDataRootPath = values["--source-data-root"],
                SourceUri = values["--source-uri"],
                SourceBranch = values["--source-branch"],
                DataVersion = values["--data-version"],
                League = values.GetValueOrDefault("--league"),
                Patch = values.GetValueOrDefault("--patch"),
                SourceVersion = values.GetValueOrDefault("--source-version"),
                HistoricalBaseItemsPath = values.GetValueOrDefault("--historical-base-items"),
                HistoricalModsPath = values.GetValueOrDefault("--historical-mods"),
                HistoricalStatsPath = values.GetValueOrDefault("--historical-stats"),
                HistoricalTranslationsPath = values.GetValueOrDefault("--historical-translations"),
                HistoricalSourceRootPath = values.GetValueOrDefault("--historical-source-root"),
                HistoricalSourceDataRootPath = values.GetValueOrDefault("--historical-source-data-root"),
                HistoricalSourceUri = values.GetValueOrDefault("--historical-source-uri"),
                HistoricalSourceBranch = values.GetValueOrDefault("--historical-source-branch"),
                HistoricalSourceVersion = values.GetValueOrDefault("--historical-source-version"),
                HistoricalDataVersion = values.GetValueOrDefault("--historical-data-version"),
                PoBUniquesPath = values.GetValueOrDefault("--pob-uniques"),
                PoBSourceRootPath = values.GetValueOrDefault("--pob-source-root"),
                PoBSourceUri = values.GetValueOrDefault("--pob-source-uri"),
                PoBSourceTag = values.GetValueOrDefault("--pob-source-tag"),
                PoBSourceVersion = values.GetValueOrDefault("--pob-source-version"),
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
              PoEnhance.DataTool build-package --base-items <path> --mods <path> --stats <path> --translations <path> --item-classes <path> --tags <path> --mods-by-base <path> --item-property-semantics <path> --output <path> --source-root <git-checkout> --source-data-root <data-root> --source-uri <uri> --source-branch <branch> --source-version <sha> --data-version <value> [--historical-base-items <path> --historical-mods <path> --historical-stats <path> --historical-translations <path> --historical-source-root <git-checkout> --historical-source-data-root <data-root> --historical-source-uri <uri> --historical-source-branch <branch> --historical-source-version <sha> --historical-data-version <value>] [--pob-uniques <evaluated-json> --pob-source-root <git-checkout> --pob-source-uri <uri> --pob-source-tag <tag> --pob-source-version <sha>] [--source-snapshot-dir <path>] [--league <value>] [--patch <value>] [--verbose-diagnostics]

            Example:
              dotnet run --project .\PoEnhance.DataTool -- build-package --base-items .\data\repoe\base_items.json --mods .\data\repoe\mods.json --stats .\data\repoe\stats.json --translations .\data\repoe\stat_translations.json --item-classes .\data\repoe\item_classes.json --tags .\data\repoe\tags.json --mods-by-base .\data\repoe\mods_by_base.json --item-property-semantics .\data\semantics\item-property-semantics.json --output .\artifacts\poenhance-game-data.json --source-snapshot-dir .\artifacts\source-snapshots\dev-001 --source-root .\local-data\repoe --source-data-root .\data\repoe --source-uri https://github.com/repoe-fork/repoe --source-branch master --source-version c50acab2ed660a70511e7f91ee09db4e632089e4 --data-version dev-001
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
