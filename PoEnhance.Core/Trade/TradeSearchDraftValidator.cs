using PoEnhance.Core.Items.GameData;

namespace PoEnhance.Core.Trade;

public sealed class TradeSearchDraftValidator
{
    public TradeSearchValidationResult Validate(TradeSearchDraft? draft)
    {
        if (draft is null)
        {
            return TradeSearchValidationResult.FromDiagnostics(
                [
                    Error(
                        TradeSearchValidationDiagnosticCodes.NullDraft,
                        "A Trade search draft is required for validation."),
                ]);
        }

        var diagnostics = new List<TradeSearchValidationDiagnostic>();
        ValidateBaseIdentity(draft, diagnostics);
        ValidateItemLevel(draft, diagnostics);
        ValidateUnsupportedSpecialFacts(draft, diagnostics);
        ValidateModifierFilters(draft, diagnostics);

        return TradeSearchValidationResult.FromDiagnostics(diagnostics);
    }

    private static void ValidateBaseIdentity(
        TradeSearchDraft draft,
        List<TradeSearchValidationDiagnostic> diagnostics)
    {
        var hasParsedBaseType = !string.IsNullOrWhiteSpace(draft.ParsedBaseType);
        var hasResolvedBaseIdentity =
            !string.IsNullOrWhiteSpace(draft.Base?.ResolvedBaseId) ||
            !string.IsNullOrWhiteSpace(draft.Base?.ResolvedBaseName);
        var hasActiveBaseCriterion =
            draft.Base?.ActiveCriterion?.Mode == BaseSearchMode.Category &&
            !string.IsNullOrWhiteSpace(draft.Base.ActiveCriterion.Category) ||
            draft.Base?.ActiveCriterion?.Mode == BaseSearchMode.ExactBase &&
            !string.IsNullOrWhiteSpace(draft.Base.ActiveCriterion.ExactBaseName);

        if (!hasParsedBaseType && !hasResolvedBaseIdentity && !hasActiveBaseCriterion)
        {
            diagnostics.Add(Error(
                TradeSearchValidationDiagnosticCodes.MissingBaseIdentity,
                "The draft needs an active base search criterion or a resolved base identity."));
            return;
        }

        if (hasParsedBaseType &&
            (!hasResolvedBaseIdentity || draft.Base?.Status == ItemBaseResolutionStatus.Unknown))
        {
            diagnostics.Add(Warning(
                TradeSearchValidationDiagnosticCodes.UnresolvedBase,
                "The draft uses parsed base text because no resolved catalog base identity is available."));
        }
    }

    private static void ValidateItemLevel(
        TradeSearchDraft draft,
        List<TradeSearchValidationDiagnostic> diagnostics)
    {
        if (draft.ItemLevel < 0)
        {
            diagnostics.Add(Error(
                TradeSearchValidationDiagnosticCodes.NegativeItemLevel,
                "Item level cannot be negative."));
        }
    }

    private static void ValidateUnsupportedSpecialFacts(
        TradeSearchDraft draft,
        List<TradeSearchValidationDiagnostic> diagnostics)
    {
        if (!IsOrdinaryNonUniqueRarity(draft.Rarity))
        {
            return;
        }

        foreach (var state in draft.ItemStates)
        {
            if (IsUnsupportedSpecialItemState(state))
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.UnsupportedSpecialItemFact,
                    $"This ordinary-item Trade slice does not yet support the special item state '{state}'."));
            }
        }

        if (draft.TraditionalInfluences.Count > 0 || draft.EldritchInfluences.Count > 0)
        {
            diagnostics.Add(Error(
                TradeSearchValidationDiagnosticCodes.UnsupportedSpecialItemFact,
                "Influenced ordinary items require provider filters that are outside the current Trade search slice."));
        }

        if (draft.IsCorrupted)
        {
            diagnostics.Add(Error(
                TradeSearchValidationDiagnosticCodes.UnsupportedSpecialItemFact,
                "Corrupted ordinary-item behavior is outside the current Trade search slice."));
        }

        for (var index = 0; index < draft.ModifierFilters.Count; index++)
        {
            var modifier = draft.ModifierFilters[index];
            if (modifier.IsFractured)
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.UnsupportedSpecialItemFact,
                    "Fractured ordinary modifiers require provider filters that are outside the current Trade search slice.",
                    index));
            }

            if (modifier.IsVeiled)
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.UnsupportedSpecialItemFact,
                    "Veiled ordinary modifiers require provider filters that are outside the current Trade search slice.",
                    index));
            }
        }
    }

    private static void ValidateModifierFilters(
        TradeSearchDraft draft,
        List<TradeSearchValidationDiagnostic> diagnostics)
    {
        for (var index = 0; index < draft.ModifierFilters.Count; index++)
        {
            var modifier = draft.ModifierFilters[index];
            if (!modifier.IsSelected)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(modifier.OriginalText))
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.SelectedModifierMissingText,
                    "A selected modifier needs its original displayed text.",
                    index));
            }

            if (modifier.ResolutionStatus != ModifierCandidateResolutionStatus.Exact ||
                string.IsNullOrWhiteSpace(modifier.ResolvedModifierId))
            {
                diagnostics.Add(Warning(
                    TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved,
                    "The local modifier catalog did not resolve this modifier exactly; Trade stats mapping will verify it before search.",
                    index));
            }

            if (modifier.RequestedMinimum.HasValue &&
                modifier.RequestedMaximum.HasValue &&
                modifier.RequestedMinimum.Value > modifier.RequestedMaximum.Value)
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.InvalidModifierRange,
                    "A selected modifier minimum cannot be greater than its maximum.",
                    index));
            }
        }
    }

    private static bool IsOrdinaryNonUniqueRarity(string? rarity)
    {
        return string.Equals(rarity?.Trim(), "Normal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rarity?.Trim(), "Magic", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rarity?.Trim(), "Rare", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnsupportedSpecialItemState(string? state)
    {
        return string.Equals(state?.Trim(), "Synthesised Item", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state?.Trim(), "Fractured Item", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state?.Trim(), "Mirrored", StringComparison.OrdinalIgnoreCase);
    }

    private static TradeSearchValidationDiagnostic Warning(
        string code,
        string message,
        int? modifierFilterIndex = null)
    {
        return new TradeSearchValidationDiagnostic(
            code,
            TradeSearchValidationSeverity.Warning,
            message,
            modifierFilterIndex);
    }

    private static TradeSearchValidationDiagnostic Error(
        string code,
        string message,
        int? modifierFilterIndex = null)
    {
        return new TradeSearchValidationDiagnostic(
            code,
            TradeSearchValidationSeverity.Error,
            message,
            modifierFilterIndex);
    }
}
