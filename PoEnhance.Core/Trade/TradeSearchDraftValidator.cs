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
        ValidateRequestedItemFilters(draft, diagnostics);
        ValidateUnsupportedSpecialFacts(draft, diagnostics);
        ValidateItemProperties(draft, diagnostics);
        ValidateModifierFilters(draft, diagnostics);

        return TradeSearchValidationResult.FromDiagnostics(diagnostics);
    }

    private static void ValidateRequestedItemFilters(
        TradeSearchDraft draft,
        List<TradeSearchValidationDiagnostic> diagnostics)
    {
        foreach (var filter in draft.RequestedItemFilters.Where(filter => filter.IsActive))
        {
            if (filter.LocalValidationStatus == TradeSearchRequestedItemFilterValidationStatus.Invalid)
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.RequestedItemFilterInvalid,
                    filter.DiagnosticReason ?? $"Active {filter.Label} must be an unsigned integer."));
                continue;
            }

            if (filter.RequestedMinimum.HasValue &&
                filter.ProviderResolutionStatus != TradeSearchItemPropertyProviderResolutionStatus.Exact)
            {
                diagnostics.Add(Error(
                    filter.ProviderResolutionStatus == TradeSearchItemPropertyProviderResolutionStatus.Unsupported
                        ? TradeSearchValidationDiagnosticCodes.RequestedItemFilterUnsupported
                        : TradeSearchValidationDiagnosticCodes.RequestedItemFilterUnresolved,
                    filter.DiagnosticReason ?? $"Active {filter.Label} has no exact provider filter mapping."));
            }
        }
    }

    private static void ValidateItemProperties(
        TradeSearchDraft draft,
        List<TradeSearchValidationDiagnostic> diagnostics)
    {
        for (var index = 0; index < draft.ItemProperties.Length; index++)
        {
            var property = draft.ItemProperties[index];
            if (!property.IsSelected)
            {
                continue;
            }

            if (property.ProviderResolutionStatus != TradeSearchItemPropertyProviderResolutionStatus.Exact ||
                !property.IsSearchable)
            {
                var code = property.ProviderResolutionStatus switch
                {
                    TradeSearchItemPropertyProviderResolutionStatus.Unsupported =>
                        TradeSearchValidationDiagnosticCodes.SelectedItemPropertyUnsupported,
                    TradeSearchItemPropertyProviderResolutionStatus.Ambiguous =>
                        TradeSearchValidationDiagnosticCodes.SelectedItemPropertyAmbiguous,
                    _ => TradeSearchValidationDiagnosticCodes.SelectedItemPropertyUnresolved,
                };
                diagnostics.Add(new TradeSearchValidationDiagnostic(
                    code,
                    TradeSearchValidationSeverity.Error,
                    $"Selected item property '{property.Label}' cannot be searched: " +
                        (property.NotSearchableReason ?? "no exact provider mapping is available."),
                    ItemPropertyIndex: index));
            }

            if (property.RequestedMinimum.HasValue &&
                property.RequestedMaximum.HasValue &&
                property.RequestedMinimum.Value > property.RequestedMaximum.Value)
            {
                diagnostics.Add(new TradeSearchValidationDiagnostic(
                    TradeSearchValidationDiagnosticCodes.InvalidItemPropertyRange,
                    TradeSearchValidationSeverity.Error,
                    $"Selected item property '{property.Label}' has a minimum greater than its maximum.",
                    ItemPropertyIndex: index));
            }
        }
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
            ValidateContributors(modifier, index, diagnostics);
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

            if (IsRepresentedByExactBase(draft, modifier, out var exactBaseName))
            {
                diagnostics.Add(Info(
                    TradeSearchValidationDiagnosticCodes.SelectedModifierRepresentedByExactBase,
                    $"Selected base implicit is represented by Exact Base: {exactBaseName}.",
                    index));
            }
            else if (modifier.ResolutionStatus != ModifierCandidateResolutionStatus.Exact ||
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

            if (!string.IsNullOrWhiteSpace(modifier.SelectedFilterVariantIdentity) &&
                !modifier.FilterVariants.Any(variant => string.Equals(
                    variant.Identity,
                    modifier.SelectedFilterVariantIdentity,
                    StringComparison.Ordinal)))
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.SelectedModifierVariantUnresolved,
                    "The selected modifier type is no longer available in the official Trade stat catalog.",
                    index));
            }

            else if (modifier.SourceCount > 1 &&
                modifier.ProviderResolutionStatus is
                    SearchComponentProviderResolutionStatus.Unsupported or
                    SearchComponentProviderResolutionStatus.Ambiguous)
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.SelectedModifierVariantUnresolved,
                    modifier.ProviderDiagnosticMessage ??
                        "The aggregate has no unambiguous provider filter that covers every contributor.",
                    index));
            }

        }
    }

    private static void ValidateContributors(
        ResolvedSearchComponent parent,
        int parentIndex,
        List<TradeSearchValidationDiagnostic> diagnostics)
    {
        if (!SearchComponentContributorActivation.IsFilteringActive(parent))
        {
            return;
        }

        var selected = parent.Contributors
            .Where(contributor => contributor.IsSelected)
            .ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        var hasInvalidAdditiveMinimum = false;
        foreach (var contributor in selected)
        {
            if (contributor.ProviderResolutionStatus != SearchComponentProviderResolutionStatus.Exact ||
                string.IsNullOrWhiteSpace(contributor.ProviderIdentity))
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.InvalidContributorSourceIdentity,
                    contributor.ProviderDiagnosticMessage ??
                        $"Selected contributor '{contributor.DisplayText}' has no exact retained source provider identity.",
                    parentIndex));
            }

            if (parent.ContributorProjection == SearchComponentContributorProjection.Additive &&
                !contributor.RequestedMinimum.HasValue)
            {
                hasInvalidAdditiveMinimum = true;
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.InvalidContributorMinimum,
                    $"Selected contributor '{contributor.DisplayText}' needs a valid Min value for additive projection.",
                    parentIndex));
            }

            if (contributor.RequestedMinimum.HasValue &&
                contributor.RequestedMaximum.HasValue &&
                contributor.RequestedMinimum.Value > contributor.RequestedMaximum.Value)
            {
                diagnostics.Add(Error(
                    TradeSearchValidationDiagnosticCodes.InvalidContributorRange,
                    "A selected contributor minimum cannot be greater than its maximum.",
                    parentIndex));
            }
        }

        if (hasInvalidAdditiveMinimum)
        {
            return;
        }

        if (!SearchComponentContributorMath.TryGetActiveAdditiveMinimumFloor(parent, out _))
        {
            diagnostics.Add(Error(
                TradeSearchValidationDiagnosticCodes.UnsupportedContributorProjection,
                "The selected contributor values cannot be projected faithfully onto the parent modifier.",
                parentIndex));
        }
    }

    private static bool IsRepresentedByExactBase(
        TradeSearchDraft draft,
        ResolvedSearchComponent modifier,
        out string exactBaseName)
    {
        exactBaseName = string.Empty;
        if (!modifier.IsBaseImplicit ||
            modifier.ProviderResolutionStatus != SearchComponentProviderResolutionStatus.BaseGuaranteed ||
            draft.Base.ActiveCriterion?.Mode != BaseSearchMode.ExactBase)
        {
            return false;
        }

        var activeExactBase = draft.Base.ActiveCriterion.ExactBaseName?.Trim();
        if (string.IsNullOrWhiteSpace(activeExactBase))
        {
            return false;
        }

        exactBaseName = activeExactBase;
        return true;
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

    private static TradeSearchValidationDiagnostic Info(
        string code,
        string message,
        int? modifierFilterIndex = null)
    {
        return new TradeSearchValidationDiagnostic(
            code,
            TradeSearchValidationSeverity.Info,
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
