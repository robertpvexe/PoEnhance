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

    public string? OutputPrefix { get; init; }

    public string? BaselinePath { get; init; }

    public string? ObservationalBaselinePath { get; init; }

    public string? WriteObservationalBaselinePath { get; init; }

    public bool DeduplicateLatestCapturePerItem { get; init; } = true;

    public bool Strict { get; init; }

    public bool FailOnReviewRequired { get; init; } = true;

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
        "--output-prefix",
        "--baseline",
        "--observational-baseline",
        "--write-observational-baseline",
        "--max-unclassified-cluster-components",
        "--max-supported-coverage-drop-percent",
    };

    private static readonly HashSet<string> FlagOptions = new(StringComparer.Ordinal)
    {
        "--strict",
        "--keep-duplicate-captures",
        "--allow-review-required",
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

        var writeBaseline = TrimOrNull(values.GetValueOrDefault("--write-observational-baseline"));
        var observationalBaseline = TrimOrNull(values.GetValueOrDefault("--observational-baseline"));
        var strict = flags.Contains("--strict");
        if (strict &&
            writeBaseline is not null)
        {
            errors.Add("Strict mode cannot be combined with --write-observational-baseline (baseline update is explicit-only).");
        }

        if (strict &&
            maxUnclassified is null &&
            maxDrop is null &&
            !values.ContainsKey("--baseline") &&
            observationalBaseline is null)
        {
            errors.Add(
                "Strict mode requires --observational-baseline and/or --baseline and/or a configured threshold.");
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
                OutputPrefix = TrimOrNull(values.GetValueOrDefault("--output-prefix")),
                BaselinePath = TrimOrNull(values.GetValueOrDefault("--baseline")),
                ObservationalBaselinePath = observationalBaseline,
                WriteObservationalBaselinePath = writeBaseline,
                DeduplicateLatestCapturePerItem = !flags.Contains("--keep-duplicate-captures"),
                Strict = strict,
                FailOnReviewRequired = !flags.Contains("--allow-review-required"),
                MaxUnclassifiedClusterComponents = maxUnclassified,
                MaxSupportedCoverageDropPercent = maxDrop,
            },
        };
    }

    public static string GetUsage()
    {
        return """
unique-corpus-gate --input <directory> [--output <report.json>] [--output-prefix <path-prefix>] [--baseline <report.json>] [--observational-baseline <baseline.json>] [--write-observational-baseline <baseline.json>] [--keep-duplicate-captures] [--strict [--allow-review-required] --max-unclassified-cluster-components <n> --max-supported-coverage-drop-percent <n>]

Analyzes ModifierPipelineDiagnosticRecorder JSON captures and writes Unique corpus coverage, observational diffs, invariants, and golden-control results.
Default mode only reports. --write-observational-baseline explicitly creates/updates the observational snapshot and never runs during --strict.
Strict mode fails on hard regressions, review-required changes (unless --allow-review-required), invariant violations, golden-control failures, and configured unclassified/regression thresholds.
""";
    }

    private static UniqueCorpusGateCommandLineParseResult Invalid(string error) =>
        new() { Errors = [error] };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
