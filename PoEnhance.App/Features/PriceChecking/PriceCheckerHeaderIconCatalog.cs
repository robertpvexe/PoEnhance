using System.Collections.Immutable;
using System.IO.Packaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal enum PriceCheckerHeaderStatusKind
{
    Fractured,
    Synthesised,
    Foulborn,
    Veiled,
}

internal sealed record PriceCheckerHeaderIconEntry(
    string AssetFileName,
    string DisplayLabel,
    TradeSearchInfluenceCriterion? InfluenceCriterion)
{
    public bool IsInfluenceToggle => InfluenceCriterion is not null;

    public bool IsVisuallyEnabled =>
        InfluenceCriterion is null || InfluenceCriterion.IsEnabled;
}

internal static class PriceCheckerHeaderIconCatalog
{
    internal const double DisplayPixelSize = 28d;
    internal const double InactiveOpacity = 0.38d;
    internal const double AdjacentIconSpacing = 2d;

    private static readonly IReadOnlyDictionary<TradeSearchInfluenceKind, string> InfluenceFileNames =
        new Dictionary<TradeSearchInfluenceKind, string>
        {
            [TradeSearchInfluenceKind.Shaper] = "shaper.png",
            [TradeSearchInfluenceKind.Elder] = "elder.png",
            [TradeSearchInfluenceKind.Crusader] = "crusader.png",
            [TradeSearchInfluenceKind.Redeemer] = "redeemer.png",
            [TradeSearchInfluenceKind.Hunter] = "hunter.png",
            [TradeSearchInfluenceKind.Warlord] = "warlord.png",
            [TradeSearchInfluenceKind.SearingExarch] = "searing-exarch.png",
            [TradeSearchInfluenceKind.EaterOfWorlds] = "eater-of-worlds.png",
        };

    private static readonly IReadOnlyDictionary<PriceCheckerHeaderStatusKind, string> StatusFileNames =
        new Dictionary<PriceCheckerHeaderStatusKind, string>
        {
            [PriceCheckerHeaderStatusKind.Fractured] = "fractured.png",
            [PriceCheckerHeaderStatusKind.Synthesised] = "synthesised.png",
            [PriceCheckerHeaderStatusKind.Foulborn] = "foulborn.png",
            [PriceCheckerHeaderStatusKind.Veiled] = "veiled.png",
        };

    private static readonly Dictionary<string, BitmapSource> ColorSources = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, BitmapSource> GraySources = new(StringComparer.Ordinal);
    private static bool packSchemeEnsured;

    internal static bool TryGetInfluenceDisplayLabel(TradeSearchInfluenceKind kind, out string displayLabel)
    {
        displayLabel = kind switch
        {
            TradeSearchInfluenceKind.Shaper => "Shaper",
            TradeSearchInfluenceKind.Elder => "Elder",
            TradeSearchInfluenceKind.Crusader => "Crusader",
            TradeSearchInfluenceKind.Redeemer => "Redeemer",
            TradeSearchInfluenceKind.Hunter => "Hunter",
            TradeSearchInfluenceKind.Warlord => "Warlord",
            TradeSearchInfluenceKind.SearingExarch => "Searing Exarch",
            TradeSearchInfluenceKind.EaterOfWorlds => "Eater of Worlds",
            _ => string.Empty,
        };
        return displayLabel.Length > 0;
    }

    internal static bool TryGetStatusDisplayLabel(PriceCheckerHeaderStatusKind kind, out string displayLabel)
    {
        displayLabel = kind switch
        {
            PriceCheckerHeaderStatusKind.Fractured => "Fractured",
            PriceCheckerHeaderStatusKind.Synthesised => "Synthesised",
            PriceCheckerHeaderStatusKind.Foulborn => "Foulborn",
            PriceCheckerHeaderStatusKind.Veiled => "Veiled",
            _ => string.Empty,
        };
        return displayLabel.Length > 0;
    }

    internal static bool TryGetInfluenceAssetFileName(TradeSearchInfluenceKind kind, out string fileName) =>
        InfluenceFileNames.TryGetValue(kind, out fileName!);

    internal static bool TryGetStatusAssetFileName(PriceCheckerHeaderStatusKind kind, out string fileName) =>
        StatusFileNames.TryGetValue(kind, out fileName!);

    internal static bool TryGetPackUri(string assetFileName, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(assetFileName))
        {
            return false;
        }

        EnsurePackSchemeRegistered();
        var assemblyName = typeof(PriceCheckerWindow).Assembly.GetName().Name;
        uri = new Uri(
            $"pack://application:,,,/{assemblyName};component/Assets/Influences/{assetFileName}",
            UriKind.Absolute);
        return true;
    }

    internal static bool TryGetImageSources(
        string assetFileName,
        out ImageSource colorSource,
        out ImageSource graySource)
    {
        colorSource = null!;
        graySource = null!;
        if (!TryGetPackUri(assetFileName, out var uri))
        {
            return false;
        }

        if (!ColorSources.TryGetValue(assetFileName, out var color))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            color = bitmap;
            ColorSources[assetFileName] = color;

            var gray = new FormatConvertedBitmap(color, PixelFormats.Gray8, null, 0);
            if (gray.CanFreeze)
            {
                gray.Freeze();
            }

            GraySources[assetFileName] = gray;
        }

        colorSource = ColorSources[assetFileName];
        graySource = GraySources[assetFileName];
        return true;
    }

    /// <summary>
    /// Builds left/right header icon sequences for the current draft.
    /// Influences take the header slots and suppress Fractured status icons.
    /// A lone status icon is mirrored on both sides like PoE item headers.
    /// </summary>
    internal static (IReadOnlyList<PriceCheckerHeaderIconEntry> Left, IReadOnlyList<PriceCheckerHeaderIconEntry> Right)
        ResolveHeaderIcons(TradeSearchDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var influenceEntries = CreateInfluenceEntries(draft.InfluenceCriteria);
        if (influenceEntries.Count > 0)
        {
            return AllocateAlternatingSides(influenceEntries);
        }

        var statusEntries = CreateStatusEntries(draft, suppressFractured: false);
        if (statusEntries.Count == 0)
        {
            return ([], []);
        }

        if (statusEntries.Count == 1)
        {
            var mirrored = statusEntries[0];
            return ([mirrored], [mirrored]);
        }

        return AllocateAlternatingSides(statusEntries);
    }

    internal static bool HasDisplayableInfluenceIcons(
        ImmutableArray<TradeSearchInfluenceCriterion> criteria) =>
        CreateInfluenceEntries(criteria).Count > 0;

    internal static bool IsFracturedItem(TradeSearchDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (ContainsItemState(draft, "Fractured Item"))
        {
            return true;
        }

        return draft.ModifierFilters.Any(static modifier => modifier.IsFractured);
    }

    internal static bool IsSynthesisedItem(TradeSearchDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return ContainsItemState(draft, "Synthesised Item");
    }

    internal static bool IsFoulbornItem(TradeSearchDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.UniqueItemResolution?.IsFoulborn == true ||
            draft.ItemVariantCriteria.Foulborn == TradeTriState.Yes)
        {
            return true;
        }

        return draft.ModifierFilters.Any(static modifier =>
            modifier.ResolvedSourceUniqueOrigin == ParsedUniqueModifierOrigin.Foulborn);
    }

    internal static bool HasUnrevealedVeiledMods(TradeSearchDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return draft.ModifierFilters.Any(static modifier => modifier.IsVeiled);
    }

    internal static (IReadOnlyList<T> Left, IReadOnlyList<T> Right) AllocateAlternatingSides<T>(
        IReadOnlyList<T> orderedItems)
    {
        ArgumentNullException.ThrowIfNull(orderedItems);

        var left = new List<T>();
        var right = new List<T>();
        for (var index = 0; index < orderedItems.Count; index++)
        {
            if (index % 2 == 0)
            {
                left.Insert(0, orderedItems[index]);
            }
            else
            {
                right.Add(orderedItems[index]);
            }
        }

        return (left, right);
    }

    internal static ToolTip CreateNameOnlyToolTip(string label) =>
        new()
        {
            Content = label,
            Placement = PlacementMode.Bottom,
            Padding = new Thickness(8, 4, 8, 4),
            Background = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x45)),
            BorderThickness = new Thickness(1),
            FontSize = 11,
            HasDropShadow = false,
        };

    private static List<PriceCheckerHeaderIconEntry> CreateInfluenceEntries(
        ImmutableArray<TradeSearchInfluenceCriterion> criteria)
    {
        var entries = new List<PriceCheckerHeaderIconEntry>();
        if (criteria.IsDefaultOrEmpty)
        {
            return entries;
        }

        foreach (var criterion in criteria)
        {
            if (criterion.Kind == TradeSearchInfluenceKind.Unknown ||
                !TryGetInfluenceAssetFileName(criterion.Kind, out var fileName))
            {
                continue;
            }

            var label = criterion.DisplayLabel;
            if (string.IsNullOrWhiteSpace(label))
            {
                TryGetInfluenceDisplayLabel(criterion.Kind, out label);
            }

            entries.Add(new PriceCheckerHeaderIconEntry(fileName, label, criterion));
        }

        return entries;
    }

    private static List<PriceCheckerHeaderIconEntry> CreateStatusEntries(
        TradeSearchDraft draft,
        bool suppressFractured)
    {
        var entries = new List<PriceCheckerHeaderIconEntry>();
        var showFractured = !suppressFractured &&
            !HasDisplayableInfluenceIcons(draft.InfluenceCriteria) &&
            IsFracturedItem(draft);
        if (showFractured &&
            TryGetStatusAssetFileName(PriceCheckerHeaderStatusKind.Fractured, out var fracturedFile) &&
            TryGetStatusDisplayLabel(PriceCheckerHeaderStatusKind.Fractured, out var fracturedLabel))
        {
            entries.Add(new PriceCheckerHeaderIconEntry(fracturedFile, fracturedLabel, null));
        }

        if (IsSynthesisedItem(draft) &&
            TryGetStatusAssetFileName(PriceCheckerHeaderStatusKind.Synthesised, out var synthesisedFile) &&
            TryGetStatusDisplayLabel(PriceCheckerHeaderStatusKind.Synthesised, out var synthesisedLabel))
        {
            entries.Add(new PriceCheckerHeaderIconEntry(synthesisedFile, synthesisedLabel, null));
        }

        if (IsFoulbornItem(draft) &&
            TryGetStatusAssetFileName(PriceCheckerHeaderStatusKind.Foulborn, out var foulbornFile) &&
            TryGetStatusDisplayLabel(PriceCheckerHeaderStatusKind.Foulborn, out var foulbornLabel))
        {
            entries.Add(new PriceCheckerHeaderIconEntry(foulbornFile, foulbornLabel, null));
        }

        if (HasUnrevealedVeiledMods(draft) &&
            TryGetStatusAssetFileName(PriceCheckerHeaderStatusKind.Veiled, out var veiledFile) &&
            TryGetStatusDisplayLabel(PriceCheckerHeaderStatusKind.Veiled, out var veiledLabel))
        {
            entries.Add(new PriceCheckerHeaderIconEntry(veiledFile, veiledLabel, null));
        }

        return entries;
    }

    private static bool ContainsItemState(TradeSearchDraft draft, string state) =>
        draft.ItemStates.Any(candidate =>
            string.Equals(candidate, state, StringComparison.OrdinalIgnoreCase));

    private static void EnsurePackSchemeRegistered()
    {
        if (packSchemeEnsured)
        {
            return;
        }

        _ = PackUriHelper.UriSchemePack;
        packSchemeEnsured = true;
    }
}
