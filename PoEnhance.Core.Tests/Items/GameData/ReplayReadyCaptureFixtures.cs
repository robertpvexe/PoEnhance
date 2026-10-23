namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// Repo-owned PreferCapture for Core ReplayReady regressions. Does not read %TEMP%.
/// Same resolution pattern as App ProviderRuntimeCaptures (PROPERTIES.2 / 2.1).
/// </summary>
internal static class ReplayReadyCaptureFixtures
{
    private static readonly IReadOnlyDictionary<string, string> ClipboardByCaptureFileName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["20260919-184022-181-Berek's Grip.json"] = "Bereks-Grip.clipboard.txt",
            ["20260919-184430-728-Mokou's Embrace.json"] = "Mokous-Embrace.clipboard.txt",
            ["20260919-194512-110-Uul-Netol's Kiss.json"] = "Uul-Netols-Kiss.clipboard.txt",
            ["20260919-194449-087-Dreamfeather.json"] = "Dreamfeather.clipboard.txt",
            ["20260919-194348-356-Kaom's Roots.json"] = "Kaoms-Roots-Shocked-Ground.clipboard.txt",
            ["20260919-194244-619-Kaom's Roots.json"] = "Kaoms-Roots-Socketed-Curse.clipboard.txt",
            ["20260919-194458-274-Soul Taker.json"] = "Soul-Taker.clipboard.txt",
            ["20260919-194326-390-Innsbury Edge.json"] = "Innsbury-Edge.clipboard.txt",
            ["Lifesprig.clipboard.txt"] = "Lifesprig.clipboard.txt",
            ["20260924-093804-947-Lifesprig.json"] = "Lifesprig.clipboard.txt",
        };

    public static string PreferCaptureFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!ClipboardByCaptureFileName.TryGetValue(fileName.Trim(), out var clipboardFile))
        {
            throw new InvalidOperationException(
                $"No repo-owned ReplayReady capture is registered for '{fileName}'.");
        }

        return FindRepoFile(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            clipboardFile);
    }

    public static string PreferMjolnerOnslaughtCapture() =>
        FindRepoFile(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            "Mjolner-Onslaught.clipboard.txt");

    public static string ReadClipboard(string capturePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capturePath);
        return File.ReadAllText(capturePath);
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
