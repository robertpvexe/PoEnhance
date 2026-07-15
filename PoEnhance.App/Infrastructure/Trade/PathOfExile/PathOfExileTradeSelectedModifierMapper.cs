using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeSelectedModifierMapper : IPathOfExileTradeSelectedModifierMapper
{
    public PathOfExileTradeSelectedModifierMappingResult Map(TradeSearchDraft? draft)
    {
        var selectedModifiers = (draft?.ModifierFilters ?? [])
            .Select((modifier, index) => new IndexedModifier(index, modifier))
            .Where(indexed => indexed.Modifier.IsSelected)
            .ToArray();

        if (selectedModifiers.Length == 0)
        {
            return PathOfExileTradeSelectedModifierMappingResult.Success([]);
        }

        var filters = new List<PathOfExileTradeSelectedModifierFilter>();
        var diagnostics = new List<PathOfExileTradeSelectedModifierMappingDiagnostic>();
        foreach (var selectedModifier in selectedModifiers)
        {
            if (TryCreateResolvedProviderFilter(
                    selectedModifier.Index,
                    selectedModifier.Modifier,
                    out var resolvedFilter,
                    out var resolvedDiagnostic))
            {
                if (resolvedFilter is not null)
                {
                    filters.Add(resolvedFilter);
                }

                continue;
            }

            if (selectedModifier.Modifier.ProviderResolutionStatus ==
                    SearchComponentProviderResolutionStatus.Exact &&
                !CanSerializeProviderResolvedComponent(selectedModifier.Modifier))
            {
                diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                    MessageFor(
                        PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                        selectedModifier.Modifier.OriginalText),
                    selectedModifier.Index));
                continue;
            }

            if (selectedModifier.Modifier.ProviderResolutionStatus !=
                SearchComponentProviderResolutionStatus.NotResolved)
            {
                diagnostics.Add(resolvedDiagnostic ??
                    ToProviderResolutionDiagnostic(selectedModifier.Index, selectedModifier.Modifier));
                continue;
            }

            if (!CanSerializeSelectedComponent(selectedModifier.Modifier))
            {
                diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                    MessageFor(
                        PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                        selectedModifier.Modifier.OriginalText),
                    selectedModifier.Index));
                continue;
            }

            diagnostics.Add(resolvedDiagnostic ??
                ToProviderResolutionDiagnostic(selectedModifier.Index, selectedModifier.Modifier));
        }

        var collapsedFilters = CollapseSharedPresenceFilters(filters, diagnostics);
        return diagnostics.Count == 0
            ? PathOfExileTradeSelectedModifierMappingResult.Success(collapsedFilters)
            : PathOfExileTradeSelectedModifierMappingResult.Failure(diagnostics);
    }

    private static bool TryCreateResolvedProviderFilter(
        int sourceIndex,
        ResolvedSearchComponent modifier,
        out PathOfExileTradeSelectedModifierFilter? filter,
        out PathOfExileTradeSelectedModifierMappingDiagnostic? diagnostic)
    {
        filter = null;
        diagnostic = null;

        if (modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.BaseGuaranteed)
        {
            return true;
        }

        if (modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
            !string.IsNullOrWhiteSpace(modifier.ProviderStatId) &&
            CanSerializeProviderResolvedComponent(modifier))
        {
            filter = new PathOfExileTradeSelectedModifierFilter
            {
                SourceIndex = sourceIndex,
                SourceIndexes = [sourceIndex],
                StatId = modifier.ProviderStatId.Trim(),
                OriginalText = modifier.OriginalText,
                NormalizedItemTemplate = ToProviderTemplate(modifier.CanonicalSignature),
                ExtractedNumericValues = [],
                Minimum = modifier.SupportsValueBounds ? modifier.RequestedMinimum : null,
                Maximum = modifier.SupportsValueBounds ? modifier.RequestedMaximum : null,
            };
            return true;
        }

        diagnostic = ToProviderResolutionDiagnostic(sourceIndex, modifier);
        return false;
    }

    private static IReadOnlyList<PathOfExileTradeSelectedModifierFilter> CollapseSharedPresenceFilters(
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter> filters,
        List<PathOfExileTradeSelectedModifierMappingDiagnostic> diagnostics)
    {
        return filters
            .GroupBy(filter => filter.StatId, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                if (group.Any(filter => filter.Minimum != first.Minimum || filter.Maximum != first.Maximum))
                {
                    diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                        PathOfExileTradeSelectedModifierMappingDiagnosticCodes.IncompatibleBounds,
                        "Selected modifiers resolve to one Trade stat with incompatible value bounds.",
                        first.SourceIndex));
                }
                return first with
                {
                    SourceIndexes = group
                        .SelectMany(SourceIndexes)
                        .Distinct()
                        .OrderBy(index => index)
                        .ToArray(),
                };
            })
            .ToArray();
    }

    private static IEnumerable<int> SourceIndexes(PathOfExileTradeSelectedModifierFilter filter)
    {
        return filter.SourceIndexes.Count > 0
            ? filter.SourceIndexes
            : [filter.SourceIndex];
    }

    private static bool CanSerializeSelectedComponent(ResolvedSearchComponent modifier)
    {
        return modifier.IsSearchable &&
            modifier.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
            !string.IsNullOrWhiteSpace(modifier.ResolvedModifierId) &&
            modifier.ResolvedStatIds.Count > 0;
    }

    private static bool CanSerializeProviderResolvedComponent(ResolvedSearchComponent modifier)
    {
        return CanSerializeSelectedComponent(modifier) ||
            modifier.ParsedKind == PoEnhance.Core.Items.Parsing.ParsedModifierKind.Implicit;
    }

    private static PathOfExileTradeSelectedModifierMappingDiagnostic ToProviderResolutionDiagnostic(
        int sourceIndex,
        ResolvedSearchComponent modifier)
    {
        var code = modifier.ProviderResolutionStatus switch
        {
            SearchComponentProviderResolutionStatus.Ambiguous =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous,
            SearchComponentProviderResolutionStatus.NotFound
                when modifier.ProviderDiagnosticCode == PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch,
            SearchComponentProviderResolutionStatus.NotFound =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound,
            SearchComponentProviderResolutionStatus.Unsupported
                when modifier.ProviderDiagnosticCode ==
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
            _ => PathOfExileTradeSelectedModifierMappingDiagnosticCodes.InvalidInput,
        };

        return new PathOfExileTradeSelectedModifierMappingDiagnostic(
            code,
            MessageFor(code, modifier.OriginalText),
            sourceIndex,
            modifier.ProviderDiagnosticCode);
    }

    private static string MessageFor(string code, string modifierText)
    {
        var safeModifierText = SafeModifierText(modifierText);
        return code switch
        {
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous =>
                $"Selected modifier matches multiple Trade filters: {safeModifierText}",
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch =>
                $"Selected modifier kind does not match Trade filters: {safeModifierText}",
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound =>
                $"Selected modifier is not available in Trade search: {safeModifierText}",
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance =>
                $"Selected modifier has no exact GameData Trade provenance: {safeModifierText}",
            _ => $"Selected modifier cannot be mapped to Trade search: {safeModifierText}",
        };
    }

    private static string SafeModifierText(string? modifierText)
    {
        var safe = new string(
            (modifierText ?? string.Empty)
            .ReplaceLineEndings(" ")
            .Where(character => !char.IsControl(character))
            .ToArray());
        safe = string.Join(' ', safe.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safe))
        {
            return "<blank>";
        }

        const int maximumLength = 80;
        return safe.Length <= maximumLength
            ? safe
            : $"{safe[..maximumLength]}...";
    }

    private static string ToProviderTemplate(string canonicalSignature)
    {
        return canonicalSignature
            .ReplaceLineEndings(" ")
            .Replace("+<number>", "+#", StringComparison.Ordinal)
            .Replace("-<number>", "-#", StringComparison.Ordinal)
            .Replace("<number>", "#", StringComparison.Ordinal);
    }

    private sealed record IndexedModifier(
        int Index,
        ResolvedSearchComponent Modifier);
}
