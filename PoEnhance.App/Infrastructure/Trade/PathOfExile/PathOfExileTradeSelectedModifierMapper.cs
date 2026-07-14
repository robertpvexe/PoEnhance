using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeSelectedModifierMapper : IPathOfExileTradeSelectedModifierMapper
{
    private readonly IPathOfExileTradeStatMatcher statMatcher;

    public PathOfExileTradeSelectedModifierMapper(IPathOfExileTradeStatMatcher statMatcher)
    {
        this.statMatcher = statMatcher ?? throw new ArgumentNullException(nameof(statMatcher));
    }

    public PathOfExileTradeSelectedModifierMappingResult Map(
        TradeSearchDraft? draft,
        PathOfExileTradeStatCatalog? catalog)
    {
        var selectedModifiers = (draft?.ModifierFilters ?? [])
            .Select((modifier, index) => new IndexedModifier(index, modifier))
            .Where(indexed => indexed.Modifier.IsSelected)
            .ToArray();

        if (selectedModifiers.Length == 0)
        {
            return PathOfExileTradeSelectedModifierMappingResult.Success([]);
        }

        if (catalog is null)
        {
            return PathOfExileTradeSelectedModifierMappingResult.Failure(
            [
                new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.CatalogRequired,
                    "A Trade stats catalog is required before selected modifiers can be mapped."),
            ]);
        }

        var filters = new List<PathOfExileTradeSelectedModifierFilter>();
        var diagnostics = new List<PathOfExileTradeSelectedModifierMappingDiagnostic>();
        var traces = new List<PathOfExileTradeStatResolutionTrace>();
        foreach (var selectedModifier in selectedModifiers)
        {
            var match = statMatcher.Match(
                ToParsedModifier(selectedModifier.Modifier),
                catalog,
                CreateContext(draft, selectedModifier.Modifier));
            if (match.Trace is not null)
            {
                traces.Add(match.Trace);
            }

            if (match.Status == PathOfExileTradeStatMatchStatus.Exact &&
                match.ExactCandidate is not null &&
                !string.IsNullOrWhiteSpace(match.ExactCandidate.StatId))
            {
                filters.Add(new PathOfExileTradeSelectedModifierFilter
                {
                    SourceIndex = selectedModifier.Index,
                    StatId = match.ExactCandidate.StatId,
                    OriginalText = selectedModifier.Modifier.OriginalText,
                    NormalizedItemTemplate = match.NormalizedItemTemplate,
                    ExtractedNumericValues = match.ExtractedNumericValues,
                });
                continue;
            }

            diagnostics.Add(ToMappingDiagnostic(
                selectedModifier.Index,
                selectedModifier.Modifier,
                match));
        }

        return diagnostics.Count == 0
            ? PathOfExileTradeSelectedModifierMappingResult.Success(filters, traces)
            : PathOfExileTradeSelectedModifierMappingResult.Failure(diagnostics, traces);
    }

    private static ParsedModifier ToParsedModifier(TradeModifierFilterDraft modifier)
    {
        return new ParsedModifier(
            [modifier.OriginalText],
            RawMetadataLine: null,
            modifier.ParsedKind,
            modifier.ParsedModifierName,
            Tier: null,
            Rank: null,
            modifier.CategoryText,
            modifier.IsCrafted,
            modifier.IsFractured,
            modifier.IsVeiled);
    }

    private static PathOfExileTradeSelectedModifierMappingDiagnostic ToMappingDiagnostic(
        int sourceIndex,
        TradeModifierFilterDraft modifier,
        PathOfExileTradeStatMatchResult match)
    {
        var sourceCode = match.Diagnostics.FirstOrDefault()?.Code;
        var code = match.Status switch
        {
            PathOfExileTradeStatMatchStatus.Ambiguous =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous,
            PathOfExileTradeStatMatchStatus.NotFound
                when sourceCode == PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch,
            PathOfExileTradeStatMatchStatus.NotFound =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound,
            _ => PathOfExileTradeSelectedModifierMappingDiagnosticCodes.InvalidInput,
        };

        return new PathOfExileTradeSelectedModifierMappingDiagnostic(
            code,
            MessageFor(code, modifier.OriginalText),
            sourceIndex,
            sourceCode);
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
            _ => $"Selected modifier cannot be mapped to Trade search: {safeModifierText}",
        };
    }

    private static PathOfExileTradeStatMatchContext CreateContext(
        TradeSearchDraft? draft,
        TradeModifierFilterDraft modifier)
    {
        return new PathOfExileTradeStatMatchContext
        {
            ItemClass = draft?.ItemClass,
            ParsedBaseType = draft?.ParsedBaseType,
            ModifierLocality = modifier.Locality,
            ResolvedModifierId = modifier.ResolvedModifierId,
            ResolvedModifierName = modifier.ResolvedModifierName,
            InternalStatIds = modifier.ResolvedStatIds,
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

    private sealed record IndexedModifier(
        int Index,
        TradeModifierFilterDraft Modifier);
}
