using System.Text.RegularExpressions;

namespace PoEnhance.App.Tests.TestData.Items;

/// <summary>
/// Genuine Ward clipboard fixtures from D:\Projects\zfortests_v2.txt (Stage 03.7A).
/// Linked corpus file lives under Core.Tests TestData/Items.
/// </summary>
internal static class WardRealFixtures
{
    private const string CorpusFileName = "ward-real-fixtures-zfortests-v2.txt";
    private static readonly Regex ItemBoundary = new(
        @"\r?\n\s*\r?\n(?=Item Class:)",
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> LoadItems()
    {
        var corpusPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Items", CorpusFileName);
        var corpus = File.ReadAllText(corpusPath);
        var items = ItemBoundary
            .Split(corpus.TrimEnd('\r', '\n'))
            .Where(item => item.StartsWith("Item Class:", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(4, items.Length);
        return items;
    }

    public static string HypnoticKeep => LoadItems()[0];

    public static string OnslaughtCowl => LoadItems()[1];

    public static string HavocGrip => LoadItems()[2];

    public static string DamnationStride => LoadItems()[3];
}
