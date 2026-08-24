using System.Globalization;

namespace PoEnhance.DataTool.UniqueCorpusGate;

public sealed record UniqueCorpusGateCommandLineParseResult
{
    public bool IsValid => Errors.Count == 0 && Request is not null;

    public UniqueCorpusGateCommandLineRequest? Request { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed record UniqueCorpusGateCommandLineRequest
{
    public required string InputDirectory { get; init; }

    public string? OutputPath { get; init; }

    public string? BaselinePath { get; init; }

    public bool DeduplicateLatestCapturePerItem { get; init; } = true;

    public bool Strict { get; init; }

    public int? MaxUnclassifiedClusterComponents { get; init; }

    public decimal? MaxSupportedCoverageDropPercent { get; init; }
}

public static class UniqueCorpusGateCommandLineParser
{
    public const string CommandName = "unique-corpus-gate";

    private static readonly HashSet<string> OptionsWithValues = new(StringComparer.Ordinal)
    {
        "--input",
        "--output",
        "--baseline",
        "--max-unclassified-cluster-components",
        "--max-supported-coverage-drop-percent",
    };

    private static readonly HashSet<string> FlagOptions = new(StringComparer.Ordinal)
    {
        "--strict",
        "--keep-duplicate-captures",
    };

    public static UniqueCorpusGateCommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        var errors = new List<string>();
        if (args.Count == 0)
        {
            return Invalid("Missing command. Expected: unique-corpus-gate.");
        }

        if (!string.Equals(args[0], CommandName, StringComparison.Ordinal))
        {
            return Invalid($"Unknown command '{args[0]}'. Expected: unique-corpus-gate.");
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

            if (index + 1 >= args.Count)
            {
                errors.Add($"Option '{option}' requires a value.");
                continue;
            }

            index++;
            if (!values.TryAdd(option, args[index]))
            {
                errors.Add($"Duplicate option '{option}'.");
            }
        }

        if (!values.TryGetValue("--input", out var input) || string.IsNullOrWhiteSpace(input))
        {
            errors.Add("Missing required option '--input <directory>'.");
        }

        int? maxUnclassified = null;
        if (values.TryGetValue("--max-unclassified-cluster-components", out var unclassifiedText))
        {
            if (!int.TryParse(
                    unclassifiedText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed) ||
                parsed < 0)
            {
                errors.Add("Option '--max-unclassified-cluster-components' must be a non-negative integer.");
            }
            else
            {
                maxUnclassified = parsed;
            }
        }

        decimal? maxDrop = null;
        if (values.TryGetValue("--max-supported-coverage-drop-percent", out var dropText))
        {
            if (!decimal.TryParse(
                    dropText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var parsed) ||
                parsed < 0)
            {
                errors.Add("Option '--max-supported-coverage-drop-percent' must be a non-negative number.");
            }
            else
            {
                maxDrop = parsed;
            }
        }

        var strict = flags.Contains("--strict");
        if (strict && maxUnclassified is null && maxDrop is null &&
            !values.ContainsKey("--baseline"))
        {
            errors.Add("Strict mode requires --baseline and/or a configured threshold.");
        }

        if (errors.Count > 0)
        {
            return new UniqueCorpusGateCommandLineParseResult { Errors = errors };
        }

        return new UniqueCorpusGateCommandLineParseResult
        {
            Request = new UniqueCorpusGateCommandLineRequest
            {
                InputDirectory = input!.Trim(),
                OutputPath = TrimOrNull(values.GetValueOrDefault("--output")),
                BaselinePath = TrimOrNull(values.GetValueOrDefault("--baseline")),
                DeduplicateLatestCapturePerItem = !flags.Contains("--keep-duplicate-captures"),
                Strict = strict,
                MaxUnclassifiedClusterComponents = maxUnclassified,
                MaxSupportedCoverageDropPercent = maxDrop,
            },
        };
    }

    public static string GetUsage()
    {
        return """
unique-corpus-gate --input <directory> [--output <report.json>] [--baseline <report.json>] [--keep-duplicate-captures] [--strict --max-unclassified-cluster-components <n> --max-supported-coverage-drop-percent <n>]

Analyzes ModifierPipelineDiagnosticRecorder JSON captures and writes a Unique corpus coverage report.
Default mode only reports. Strict mode fails on configured unclassified/regression thresholds.
""";
    }

    private static UniqueCorpusGateCommandLineParseResult Invalid(string error) =>
        new() { Errors = [error] };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
