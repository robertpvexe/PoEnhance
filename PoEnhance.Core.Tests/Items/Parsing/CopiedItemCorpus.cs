using System.Text.RegularExpressions;

namespace PoEnhance.Core.Tests.Items.Parsing;

internal static class CopiedItemCorpus
{
    private const string CorpusFileName = "advanced-real-items-corpus.txt";
    private static readonly Regex ItemBoundary = new(
        @"\r?\n\s*\r?\n(?=Item Class:)",
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> LoadItems()
    {
        var corpusPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Items", CorpusFileName);
        var corpus = File.ReadAllText(corpusPath);
        var items = ItemBoundary
            .Split(corpus.TrimEnd('\r', '\n'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        Assert.Equal(15, items.Length);
        Assert.All(items, item => Assert.StartsWith("Item Class:", item, StringComparison.Ordinal));
        return items;
    }
}
