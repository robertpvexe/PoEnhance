using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeItemIdentityMapper : IPathOfExileTradeItemIdentityMapper
{
    private const string RarityUnique = "Unique";
    private const string DecoratedFoulbornPrefix = "Foulborn ";

    public PathOfExileTradeItemIdentityMappingResult Map(
        TradeSearchDraft? draft,
        PathOfExileTradeItemCatalog? catalog)
    {
        if (catalog is null)
        {
            return Failure(
                PathOfExileTradeItemIdentityMappingDiagnosticCodes.CatalogRequired,
                "A Trade item catalog is required before Unique identity can be mapped.");
        }

        if (draft is null || !IsRarity(draft, RarityUnique))
        {
            return Failure(
                PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueIdentity,
                "Only Unique item identity is supported by this mapper.");
        }

        var displayName = TrimToNull(draft.DisplayName);
        if (displayName is null)
        {
            return Failure(
                PathOfExileTradeItemIdentityMappingDiagnosticCodes.MissingUniqueName,
                "A Unique item needs a display name before provider identity can be mapped.");
        }

        var selectedBaseType = SelectBaseType(draft);
        if (selectedBaseType is null)
        {
            return Failure(
                PathOfExileTradeItemIdentityMappingDiagnosticCodes.MissingBaseType,
                "A Unique item needs a parsed or resolved base type before provider identity can be mapped.");
        }

        var exactMatches = catalog.FindByExactDisplayText(displayName);
        if (exactMatches.Count > 0)
        {
            return MapExactFullName(displayName, selectedBaseType, exactMatches, draft);
        }

        if (displayName.StartsWith(DecoratedFoulbornPrefix, StringComparison.Ordinal))
        {
            return MapFoulborn(displayName, selectedBaseType, catalog, draft);
        }

        return Failure(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueIdentity,
            "The Unique display name and base type do not match a supported provider item identity exactly.");
    }

    private static PathOfExileTradeItemIdentityMappingResult MapExactFullName(
        string displayName,
        string selectedBaseType,
        IReadOnlyList<PathOfExileTradeItemEntry> exactMatches,
        TradeSearchDraft draft)
    {
        var compatibleUniqueMatches = exactMatches
            .Where(entry => entry.IsUnique && IsCompatibleBase(entry, selectedBaseType))
            .ToArray();

        if (compatibleUniqueMatches.Length == 1)
        {
            var match = compatibleUniqueMatches[0];
            return PathOfExileTradeItemIdentityMappingResult.Success(new PathOfExileTradeItemIdentity
            {
                CanonicalName = match.Name ?? displayName,
                CanonicalType = match.Type,
                Foulborn = ResolveFoulborn(draft.ItemVariantCriteria.Foulborn, detectedFoulborn: false),
            });
        }

        if (compatibleUniqueMatches.Length > 1)
        {
            return Failure(
                PathOfExileTradeItemIdentityMappingDiagnosticCodes.AmbiguousUniqueIdentity,
                "The Unique display name and base type matched multiple provider item identities.");
        }

        return Failure(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueIdentity,
            "The exact provider item name exists, but not as a compatible supported Unique identity.");
    }

    private static PathOfExileTradeItemIdentityMappingResult MapFoulborn(
        string displayName,
        string selectedBaseType,
        PathOfExileTradeItemCatalog catalog,
        TradeSearchDraft draft)
    {
        var underlyingName = displayName[DecoratedFoulbornPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(underlyingName))
        {
            return Failure(
                PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueDisplayVariant,
                "The Foulborn Unique display name does not include an underlying Unique name.");
        }

        var candidateMatches = catalog
            .FindByExactDisplayText(underlyingName)
            .Where(entry => entry.IsUnique && IsCompatibleBase(entry, selectedBaseType))
            .ToArray();

        if (candidateMatches.Length == 1)
        {
            var match = candidateMatches[0];
            return PathOfExileTradeItemIdentityMappingResult.Success(new PathOfExileTradeItemIdentity
            {
                CanonicalName = match.Name ?? underlyingName,
                CanonicalType = match.Type,
                Foulborn = ResolveFoulborn(draft.ItemVariantCriteria.Foulborn, detectedFoulborn: true),
            });
        }

        if (candidateMatches.Length > 1)
        {
            return Failure(
                PathOfExileTradeItemIdentityMappingDiagnosticCodes.AmbiguousUniqueIdentity,
                "The Foulborn Unique display name matched multiple underlying provider item identities.");
        }

        return Failure(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueDisplayVariant,
            "The Foulborn Unique display name could not be safely canonicalized to an underlying provider Unique identity.");
    }

    private static TradeTriState ResolveFoulborn(
        TradeTriState requested,
        bool detectedFoulborn)
    {
        return requested == TradeTriState.Auto
            ? detectedFoulborn ? TradeTriState.Yes : TradeTriState.No
            : requested;
    }

    private static string? SelectBaseType(TradeSearchDraft draft)
    {
        if (draft.Base.Status is ItemBaseResolutionStatus.Exact or ItemBaseResolutionStatus.Probable)
        {
            var resolvedBaseName = TrimToNull(draft.Base.ResolvedBaseName);
            if (resolvedBaseName is not null)
            {
                return resolvedBaseName;
            }
        }

        return TrimToNull(draft.ParsedBaseType);
    }

    private static bool IsCompatibleBase(
        PathOfExileTradeItemEntry entry,
        string selectedBaseType)
    {
        return string.Equals(entry.Type.Trim(), selectedBaseType.Trim(), StringComparison.Ordinal);
    }

    private static bool IsRarity(TradeSearchDraft draft, string rarity)
    {
        return string.Equals(draft.Rarity?.Trim(), rarity, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static PathOfExileTradeItemIdentityMappingResult Failure(
        string code,
        string message)
    {
        return PathOfExileTradeItemIdentityMappingResult.Failure(
            new PathOfExileTradeItemIdentityMappingDiagnostic(code, message));
    }
}
