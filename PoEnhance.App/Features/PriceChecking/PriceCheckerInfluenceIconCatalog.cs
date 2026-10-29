using System.Windows.Controls;
using System.Windows.Media;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

/// <summary>
/// Compatibility façade over <see cref="PriceCheckerHeaderIconCatalog"/> for influence assets.
/// </summary>
internal static class PriceCheckerInfluenceIconCatalog
{
    internal static bool TryGetDisplayLabel(TradeSearchInfluenceKind kind, out string displayLabel) =>
        PriceCheckerHeaderIconCatalog.TryGetInfluenceDisplayLabel(kind, out displayLabel);

    internal static bool TryGetAssetFileName(TradeSearchInfluenceKind kind, out string fileName) =>
        PriceCheckerHeaderIconCatalog.TryGetInfluenceAssetFileName(kind, out fileName);

    internal static bool TryGetPackUri(TradeSearchInfluenceKind kind, out Uri uri)
    {
        uri = null!;
        return TryGetAssetFileName(kind, out var fileName) &&
            PriceCheckerHeaderIconCatalog.TryGetPackUri(fileName, out uri);
    }

    internal static bool TryGetImageSources(
        TradeSearchInfluenceKind kind,
        out ImageSource colorSource,
        out ImageSource graySource)
    {
        colorSource = null!;
        graySource = null!;
        return TryGetAssetFileName(kind, out var fileName) &&
            PriceCheckerHeaderIconCatalog.TryGetImageSources(fileName, out colorSource, out graySource);
    }

    internal static (IReadOnlyList<T> Left, IReadOnlyList<T> Right) AllocateAlternatingSides<T>(
        IReadOnlyList<T> orderedItems) =>
        PriceCheckerHeaderIconCatalog.AllocateAlternatingSides(orderedItems);

    internal static ToolTip CreateNameOnlyToolTip(string influenceName) =>
        PriceCheckerHeaderIconCatalog.CreateNameOnlyToolTip(influenceName);
}
