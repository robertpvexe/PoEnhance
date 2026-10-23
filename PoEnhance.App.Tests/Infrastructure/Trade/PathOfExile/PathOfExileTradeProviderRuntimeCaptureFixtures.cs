using System.Text;
using System.Text.Json;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// Repo-owned PreferCapture for provider runtime regressions. Does not read %TEMP%.
/// </summary>
internal static class PathOfExileTradeProviderRuntimeCaptureFixtures
{
    private static readonly IReadOnlyDictionary<string, string> ClipboardByItemName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cospri's Malice"] = "Cospris-Malice.clipboard.txt",
            ["Fidelitas' Spike"] = "Fidelitas-Spike.clipboard.txt",
            ["Oro's Sacrifice"] = "Oros-Sacrifice.clipboard.txt",
            ["Nuro's Harp"] = "Nuros-Harp.clipboard.txt",
            ["Storm Cloud"] = "Storm-Cloud.clipboard.txt",
            ["Null and Void"] = "Foulborn-Null-and-Void.clipboard.txt",
            ["Foulborn Null and Void"] = "Foulborn-Null-and-Void.clipboard.txt",
            ["Chains of Command"] = "Chains-of-Command.clipboard.txt",
            ["Berek's Grip"] = "Bereks-Grip.clipboard.txt",
            ["Mokou's Embrace"] = "Mokous-Embrace.clipboard.txt",
            ["Circle of Ambition"] = "Circle-of-Ambition.clipboard.txt",
            ["Steelworm"] = "Steelworm.clipboard.txt",
            ["Tempered Spirit"] = "Tempered-Spirit.clipboard.txt",
            ["The Hungry Loop"] = "The-Hungry-Loop.clipboard.txt",
            ["Lifesprig"] = "Lifesprig.clipboard.txt",
        };

    public static string PreferCapture(string itemName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        // FormC: OrdinalIgnoreCase does not equate NFC/NFD (e.g. ö vs o+combining diaeresis).
        var key = itemName.Trim().Normalize(NormalizationForm.FormC);
        if (!ClipboardByItemName.TryGetValue(key, out var fileName))
        {
            throw new InvalidOperationException(
                $"No repo-owned provider runtime capture is registered for '{itemName}'.");
        }

        return FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Unique",
            "ProviderRuntimeCaptures",
            fileName);
    }

    public static string PreferCaptureFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        // Map legacy A.5.20 capture filenames onto repo-owned clipboard fixtures.
        var mapped = fileName switch
        {
            "20260919-184022-181-Berek's Grip.json" => PreferCapture("Berek's Grip"),
            "20260919-184430-728-Mokou's Embrace.json" => PreferCapture("Mokou's Embrace"),
            _ => throw new InvalidOperationException(
                $"No repo-owned provider runtime capture is registered for file '{fileName}'."),
        };
        return mapped;
    }

    // ASCII filenames only — avoid ö dictionary keys (CP1250 mojibake / NFC↔NFD mismatch).
    public static string PreferMjolnerOnslaughtCapture() =>
        FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Unique",
            "ProviderRuntimeCaptures",
            "Mjolner-Onslaught.clipboard.txt");

    public static string PreferMjolnerA542Capture() =>
        FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Unique",
            "ProviderRuntimeCaptures",
            "Mjolner-A542.clipboard.txt");

    public static string ReadClipboard(string capturePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capturePath);
        if (capturePath.EndsWith(".clipboard.txt", StringComparison.OrdinalIgnoreCase))
        {
            return File.ReadAllText(capturePath);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(capturePath));
        var root = document.RootElement;
        if (root.TryGetProperty("replayContext", out var replay) &&
            replay.TryGetProperty("rawClipboardText", out var clipboard))
        {
            return clipboard.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("rawClipboardText", out var rootClipboard))
        {
            return rootClipboard.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("item", out var item) &&
            root.TryGetProperty("initialModifiers", out var modifiers))
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Item Class: {item.GetProperty("itemClass").GetString()}");
            builder.AppendLine($"Rarity: {item.GetProperty("rarity").GetString()}");
            builder.AppendLine(item.GetProperty("displayName").GetString());
            builder.AppendLine(item.GetProperty("parsedBaseType").GetString());
            builder.AppendLine("--------");
            builder.AppendLine("Item Level: 80");
            builder.AppendLine("--------");
            foreach (var modifier in modifiers.EnumerateArray())
            {
                if (!modifier.TryGetProperty("raw", out var raw))
                {
                    continue;
                }

                if (raw.TryGetProperty("rawMetadataLine", out var meta) &&
                    !string.IsNullOrWhiteSpace(meta.GetString()))
                {
                    builder.AppendLine(meta.GetString());
                }

                if (raw.TryGetProperty("valueLines", out var lines))
                {
                    foreach (var line in lines.EnumerateArray())
                    {
                        var text = line.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            builder.AppendLine(text);
                        }
                    }
                }
            }

            return builder.ToString();
        }

        throw new InvalidOperationException($"Capture missing clipboard text: {capturePath}");
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Could not find repository file: {Path.Combine(relativeParts)}");
    }
}
