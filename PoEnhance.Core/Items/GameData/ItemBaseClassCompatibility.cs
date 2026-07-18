namespace PoEnhance.Core.Items.GameData;

public static class ItemBaseClassCompatibility
{
    public static bool AreCompatible(string? parsedItemClass, string? catalogItemClass)
    {
        var parsed = parsedItemClass?.Trim();
        var catalog = catalogItemClass?.Trim();

        if (string.IsNullOrEmpty(parsed) || string.IsNullOrEmpty(catalog))
        {
            return false;
        }

        var parsedIdentity = CanonicalItemClassIdentityResolver.Resolve(parsed);
        var catalogIdentity = CanonicalItemClassIdentityResolver.Resolve(catalog);
        if (parsedIdentity.IsSupported && catalogIdentity.IsSupported)
        {
            return string.Equals(
                parsedIdentity.CanonicalItemClass,
                catalogIdentity.CanonicalItemClass,
                StringComparison.Ordinal);
        }

        // Exact normalized equality is safe for non-reviewed catalog classes because it does
        // not infer a new alias. Such a class still remains unsupported for provider mapping.
        return CanonicalItemClassIdentityResolver.HaveEquivalentNormalizedText(parsed, catalog);
    }
}
