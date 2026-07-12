namespace PoEnhance.GameData;

public static class GameDataLookupNormalizer
{
    public static string? NormalizeIdentifier(string? value)
    {
        return Normalize(value);
    }

    public static string? NormalizeName(string? value)
    {
        return Normalize(value);
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
