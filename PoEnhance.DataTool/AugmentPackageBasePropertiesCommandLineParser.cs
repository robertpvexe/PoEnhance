using PoEnhance.DataImport;

namespace PoEnhance.DataTool;

public static class AugmentPackageBasePropertiesCommandLineParser
{
    public const string CommandName = "augment-package-base-properties";

    public static AugmentPackageBasePropertiesCommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        var required = new[] { "--input-package", "--base-items", "--output", "--data-version" };
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<string>();
        if (args.Count == 0 || !string.Equals(args[0], CommandName, StringComparison.Ordinal))
        {
            return new() { Errors = [$"Expected command '{CommandName}'."] };
        }

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index];
            if (!required.Contains(option, StringComparer.Ordinal))
            {
                errors.Add($"Unknown option '{option}'.");
                continue;
            }

            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                errors.Add($"Option '{option}' requires a value.");
                continue;
            }

            if (!values.TryAdd(option, args[++index]))
            {
                errors.Add($"Duplicate option '{option}'.");
            }
        }

        foreach (var option in required)
        {
            if (!values.TryGetValue(option, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Missing required option '{option}'.");
            }
        }

        if (errors.Count > 0)
        {
            return new() { Errors = errors };
        }

        if (string.Equals(
                Path.GetFullPath(values["--input-package"]),
                Path.GetFullPath(values["--output"]),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return new() { Errors = ["Input package and output paths must differ."] };
        }

        return new()
        {
            Request = new GameDataPackageWeaponPropertyAugmentationRequest
            {
                InputPackagePath = values["--input-package"],
                BaseItemsPath = values["--base-items"],
                OutputPath = values["--output"],
                DataVersion = values["--data-version"],
            },
        };
    }

    public static string GetUsage() =>
        "PoEnhance.DataTool augment-package-base-properties --input-package <path> --base-items <path> --output <path> --data-version <value>";
}
