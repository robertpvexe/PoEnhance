using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchDraftValidatorTests
{
    private readonly TradeSearchDraftValidator validator = new();

    [Fact]
    public void Validate_BaseOnlyDraftWithResolvedBase_IsValid()
    {
        var draft = ValidDraft();

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_DraftWithOneSelectedExactModifier_IsValid()
    {
        var draft = ValidDraft(modifiers: [ExactModifier() with { IsSelected = true }]);

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_NoSelectedModifiers_RemainsValid()
    {
        var draft = ValidDraft(modifiers: [ExactModifier(), UnknownModifier()]);

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_UnselectedUnknownModifier_DoesNotBlockValidation()
    {
        var draft = ValidDraft(modifiers: [UnknownModifier() with { IsSelected = false }]);

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_SelectedUnknownModifier_RemainsValidWithWarning()
    {
        var draft = ValidDraft(modifiers: [UnknownModifier() with { IsSelected = true }]);

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved,
            TradeSearchValidationSeverity.Warning,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_SelectedBaseGuaranteedImplicit_IsRepresentedByExactBaseInfoWithoutUnresolvedWarning()
    {
        var draft = ValidDraft(modifiers:
        [
            UnknownModifier() with
            {
                IsSelected = true,
                IsBaseImplicit = true,
                ParsedKind = ParsedModifierKind.Implicit,
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.BaseGuaranteed,
            },
        ]) with
        {
            Base = ValidDraft().Base with
            {
                ActiveCriterion = new BaseSearchCriterion
                {
                    Mode = BaseSearchMode.ExactBase,
                    ExactBaseName = "Stygian Vise",
                },
            },
        };

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.SelectedModifierRepresentedByExactBase,
            TradeSearchValidationSeverity.Info,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_SelectedProbableModifier_RemainsValidWithWarning()
    {
        var draft = ValidDraft(modifiers:
        [
            UnknownModifier() with
            {
                IsSelected = true,
                ParsedModifierName = "of the Rainbow",
                ResolutionStatus = ModifierCandidateResolutionStatus.Probable,
                ResolvedModifierId = null,
            },
        ]);

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved,
            TradeSearchValidationSeverity.Warning,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_SelectedModifierWithoutDisplayedText_IsInvalid()
    {
        var draft = ValidDraft(modifiers:
        [
            ExactModifier() with
            {
                IsSelected = true,
                OriginalText = " ",
            },
        ]);

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.SelectedModifierMissingText,
            TradeSearchValidationSeverity.Error,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_MinimumGreaterThanMaximum_IsInvalid()
    {
        var draft = ValidDraft(modifiers:
        [
            ExactModifier() with
            {
                IsSelected = true,
                RequestedMinimum = 20m,
                RequestedMaximum = 10m,
            },
        ]);

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.InvalidModifierRange,
            TradeSearchValidationSeverity.Error,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_UnselectedUnresolvedItemPropertyDoesNotInvalidateSearch()
    {
        var draft = ValidDraft() with
        {
            ItemProperties = [UnresolvedItemProperty()],
        };

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_SelectedUnresolvedItemPropertyIsRejectedLocally()
    {
        var draft = ValidDraft() with
        {
            ItemProperties = [UnresolvedItemProperty() with { IsSelected = true }],
        };

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.SelectedItemPropertyUnresolved,
            TradeSearchValidationSeverity.Error,
            itemPropertyIndex: 0);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Total DPS", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_SelectedExactSearchableItemPropertyPasses()
    {
        var draft = ValidDraft() with
        {
            ItemProperties = [ExactItemProperty() with { IsSelected = true }],
        };

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(
        TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
        TradeSearchValidationDiagnosticCodes.SelectedItemPropertyUnsupported)]
    [InlineData(
        TradeSearchItemPropertyProviderResolutionStatus.Ambiguous,
        TradeSearchValidationDiagnosticCodes.SelectedItemPropertyAmbiguous)]
    public void Validate_SelectedNonExactItemPropertyUsesPreciseDiagnostic(
        TradeSearchItemPropertyProviderResolutionStatus status,
        string expectedCode)
    {
        var draft = ValidDraft() with
        {
            ItemProperties =
            [
                UnresolvedItemProperty() with
                {
                    IsSelected = true,
                    ProviderResolutionStatus = status,
                    NotSearchableReason = status == TradeSearchItemPropertyProviderResolutionStatus.Unsupported
                        ? "The provider does not expose Chaos DPS."
                        : "The provider catalog contains conflicting definitions.",
                },
            ],
        };

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            expectedCode,
            TradeSearchValidationSeverity.Error,
            itemPropertyIndex: 0);
    }

    [Fact]
    public void Validate_SelectedItemPropertyMinimumGreaterThanMaximumIsInvalid()
    {
        var draft = ValidDraft() with
        {
            ItemProperties =
            [
                ExactItemProperty() with
                {
                    IsSelected = true,
                    RequestedMinimum = 200m,
                    RequestedMaximum = 199.999m,
                },
            ],
        };

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.InvalidItemPropertyRange,
            TradeSearchValidationSeverity.Error,
            itemPropertyIndex: 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_SelectedItemPropertyEqualOrNullMaximumPasses(bool useEqualMaximum)
    {
        var property = ExactItemProperty() with
        {
            IsSelected = true,
            RequestedMaximum = useEqualMaximum ? 141m : null,
        };
        var result = validator.Validate(ValidDraft() with { ItemProperties = [property] });

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_UnselectedItemPropertyInvalidRangeDoesNotBlock()
    {
        var property = ExactItemProperty() with
        {
            RequestedMinimum = 200m,
            RequestedMaximum = 100m,
        };
        var result = validator.Validate(ValidDraft() with { ItemProperties = [property] });

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_SelectedContributorFloor_AllowsExactAdditiveMinimum()
    {
        var draft = ValidDraft(modifiers: [ContributorParent(146m, firstSelected: true, secondSelected: true)]);

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.InvalidContributorParentFloor);
    }

    [Fact]
    public void Validate_SelectedContributorFloor_BelowSelectedSumSuspendsChildrenWithoutBlockingSearch()
    {
        var draft = ValidDraft(modifiers: [ContributorParent(145m, firstSelected: true, secondSelected: true)]);

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.InvalidContributorParentFloor);
    }

    [Fact]
    public void Validate_SelectedContributorWithoutProvenProjection_IsRejected()
    {
        var parent = ContributorParent(146m, firstSelected: true, secondSelected: false) with
        {
            ContributorProjection = SearchComponentContributorProjection.None,
        };

        var result = validator.Validate(ValidDraft(modifiers: [parent]));

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.UnsupportedContributorProjection,
            TradeSearchValidationSeverity.Error,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_ActiveContributorWithoutExactSourceIdentity_IsRejected()
    {
        var parent = ContributorParent(31m, firstSelected: true, secondSelected: false);
        parent = parent with
        {
            Contributors = parent.Contributors
                .Select((contributor, index) => index == 0
                    ? contributor with
                    {
                        ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Ambiguous,
                        ProviderIdentity = null,
                        ProviderDiagnosticMessage = "Contributor variant is ambiguous.",
                    }
                    : contributor)
                .ToArray(),
        };

        var result = validator.Validate(ValidDraft(modifiers: [parent]));

        Assert.False(result.IsValid);
        Assert.Single(result.Diagnostics);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.InvalidContributorSourceIdentity,
            TradeSearchValidationSeverity.Error,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_ActiveAdditiveContributorWithoutMinimum_IsRejectedPrecisely()
    {
        var parent = ContributorParent(30m, firstSelected: true, secondSelected: false);
        parent = parent with
        {
            Contributors = parent.Contributors
                .Select((contributor, index) => index == 0
                    ? contributor with { RequestedMinimum = null }
                    : contributor)
                .ToArray(),
        };

        var result = validator.Validate(ValidDraft(modifiers: [parent]));

        Assert.False(result.IsValid);
        Assert.Single(result.Diagnostics);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.InvalidContributorMinimum,
            TradeSearchValidationSeverity.Error,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_SelectedContributorUnderDeselectedParentIsInactiveAndDoesNotBlock()
    {
        var parent = ContributorParent(30m, firstSelected: true, secondSelected: false) with
        {
            IsSelected = false,
        };

        var result = validator.Validate(ValidDraft(modifiers: [parent]));

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_SelectedContributorRejectsInvertedRange()
    {
        var parent = ContributorParent(31m, firstSelected: true, secondSelected: false);
        parent = parent with
        {
            Contributors = parent.Contributors
                .Select((contributor, index) => index == 0
                    ? contributor with { RequestedMinimum = 31m, RequestedMaximum = 30m }
                    : contributor)
                .ToArray(),
        };

        var result = validator.Validate(ValidDraft(modifiers: [parent]));

        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.InvalidContributorRange,
            TradeSearchValidationSeverity.Error,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_MinimumOnlyRange_IsValid()
    {
        var draft = ValidDraft(modifiers:
        [
            ExactModifier() with
            {
                IsSelected = true,
                RequestedMinimum = 10m,
            },
        ]);

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_MaximumOnlyRange_IsValid()
    {
        var draft = ValidDraft(modifiers:
        [
            ExactModifier() with
            {
                IsSelected = true,
                RequestedMaximum = 99m,
            },
        ]);

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_MappedModifierBoundsAreUnsetByDefault()
    {
        var mapper = new TradeSearchDraftMapper();
        var parser = new ItemTextParser();
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Storm Spiral
Two-Stone Ring
--------
Item Level: 82
--------
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+55(50-59) to maximum Life
""");

        var draft = Assert.IsType<TradeSearchDraft>(mapper.CreateDraft(item).Draft);

        var filter = Assert.Single(draft.ModifierFilters);
        Assert.Null(filter.RequestedMinimum);
        Assert.Null(filter.RequestedMaximum);
    }

    [Fact]
    public void Validate_UnknownBaseWithParsedBaseText_ReturnsWarningAndRemainsValid()
    {
        var draft = ValidDraft() with
        {
            ParsedBaseType = "Onyx Amulet",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Unknown,
            },
        };

        var result = validator.Validate(draft);

        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.UnresolvedBase,
            TradeSearchValidationSeverity.Warning);
    }

    [Fact]
    public void Validate_MissingParsedAndResolvedBaseIdentity_IsInvalid()
    {
        var draft = ValidDraft() with
        {
            ParsedBaseType = null,
            Base = new TradeSearchBaseDraft(),
        };

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.MissingBaseIdentity,
            TradeSearchValidationSeverity.Error);
    }

    [Fact]
    public void Validate_NegativeItemLevel_IsInvalid()
    {
        var draft = ValidDraft() with
        {
            ItemLevel = -1,
        };

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.NegativeItemLevel,
            TradeSearchValidationSeverity.Error);
    }

    [Theory]
    [InlineData("Synthesised Item")]
    [InlineData("Fractured Item")]
    [InlineData("Mirrored")]
    public void Validate_UnsupportedOrdinaryItemState_IsInvalid(string itemState)
    {
        var draft = ValidDraft() with
        {
            ItemStates = [itemState],
        };

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.UnsupportedSpecialItemFact,
            TradeSearchValidationSeverity.Error);
    }

    [Fact]
    public void Validate_UnsupportedOrdinaryInfluence_IsInvalid()
    {
        var draft = ValidDraft() with
        {
            TraditionalInfluences = ["Shaper Item"],
        };

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.UnsupportedSpecialItemFact,
            TradeSearchValidationSeverity.Error);
    }

    [Fact]
    public void Validate_UnsupportedOrdinaryCorruption_IsInvalid()
    {
        var draft = ValidDraft() with
        {
            IsCorrupted = true,
        };

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.UnsupportedSpecialItemFact,
            TradeSearchValidationSeverity.Error);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Validate_UnsupportedOrdinarySpecialModifier_IsInvalid(
        bool isFractured,
        bool isVeiled)
    {
        var draft = ValidDraft(modifiers:
        [
            ExactModifier() with
            {
                IsFractured = isFractured,
                IsVeiled = isVeiled,
            },
        ]);

        var result = validator.Validate(draft);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.UnsupportedSpecialItemFact,
            TradeSearchValidationSeverity.Error,
            modifierFilterIndex: 0);
    }

    [Fact]
    public void Validate_InstantBuyoutAndInPerson_ProduceSameOutcome()
    {
        var instantBuyoutDraft = ValidDraft() with
        {
            ListingMode = TradeListingMode.InstantBuyout,
        };
        var inPersonDraft = instantBuyoutDraft with
        {
            ListingMode = TradeListingMode.InPerson,
        };

        var instantBuyoutResult = validator.Validate(instantBuyoutDraft);
        var inPersonResult = validator.Validate(inPersonDraft);

        Assert.Equal(instantBuyoutResult.IsValid, inPersonResult.IsValid);
        Assert.Equal(
            instantBuyoutResult.Diagnostics.Select(DiagnosticSignature),
            inPersonResult.Diagnostics.Select(DiagnosticSignature));
    }

    [Fact]
    public void Validate_DoesNotMutateDraft()
    {
        var modifier = ExactModifier() with
        {
            IsSelected = true,
            RequestedMinimum = 5m,
            RequestedMaximum = 10m,
        };
        var modifiers = new[] { modifier };
        var draft = ValidDraft(modifiers: modifiers);

        _ = validator.Validate(draft);

        Assert.Same(modifiers, draft.ModifierFilters);
        Assert.Same(modifier, Assert.Single(draft.ModifierFilters));
        Assert.Equal(5m, draft.ModifierFilters[0].RequestedMinimum);
        Assert.Equal(10m, draft.ModifierFilters[0].RequestedMaximum);
        Assert.True(draft.ModifierFilters[0].IsSelected);
    }

    [Fact]
    public void Validate_RepeatedValidation_ReturnsEquivalentResults()
    {
        var draft = ValidDraft(modifiers:
        [
            UnknownModifier() with
            {
                IsSelected = true,
                RequestedMinimum = 20m,
                RequestedMaximum = 10m,
            },
        ]);

        var firstResult = validator.Validate(draft);
        var secondResult = validator.Validate(draft);

        Assert.Equal(firstResult.IsValid, secondResult.IsValid);
        Assert.Equal(
            firstResult.Diagnostics.Select(DiagnosticSignature),
            secondResult.Diagnostics.Select(DiagnosticSignature));
    }

    [Fact]
    public void Validate_NullDraft_ReturnsStructuredDiagnostic()
    {
        var result = validator.Validate(null);

        Assert.False(result.IsValid);
        AssertDiagnostic(
            result,
            TradeSearchValidationDiagnosticCodes.NullDraft,
            TradeSearchValidationSeverity.Error);
    }

    [Fact]
    public void CoreAssembly_DoesNotReferenceWpfOrNetworkAssemblies()
    {
        var referencedNames = typeof(TradeSearchDraftValidator).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("PresentationCore", referencedNames);
        Assert.DoesNotContain("PresentationFramework", referencedNames);
        Assert.DoesNotContain("WindowsBase", referencedNames);
        Assert.DoesNotContain("System.Net.Http", referencedNames);
    }

    private static TradeSearchDraft ValidDraft(
        IReadOnlyList<ResolvedSearchComponent>? modifiers = null)
    {
        return new TradeSearchDraft
        {
            ItemClass = "Amulets",
            Rarity = "Rare",
            DisplayName = "Dusk Beads",
            ParsedBaseType = "Onyx Amulet",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.onyx-amulet",
                ResolvedBaseName = "Onyx Amulet",
            },
            ItemLevel = 84,
            ModifierFilters = modifiers ?? [],
            ListingMode = TradeListingMode.InstantBuyout,
        };
    }

    private static ResolvedSearchComponent ExactModifier()
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = "+55(50-59) to maximum Life",
            CanonicalSignature = "+<number> to maximum Life",
            ParsedKind = ParsedModifierKind.Prefix,
            ParsedModifierName = "Hale",
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "mod.prefix.hale",
            ResolvedModifierName = "Hale",
            ResolvedStatIds = ["base_maximum_life"],
            IsSearchable = true,
            IsSelected = false,
        };
    }

    private static TradeSearchItemProperty UnresolvedItemProperty()
    {
        return new TradeSearchItemProperty
        {
            Kind = TradeSearchItemPropertyKind.TotalDps,
            Label = "Total DPS",
            ObservedValue = 141m,
            RequestedMinimum = 141m,
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
            IsSearchable = false,
            NotSearchableReason = "Provider mapping for derived item properties is not available.",
        };
    }

    private static TradeSearchItemProperty ExactItemProperty()
    {
        return UnresolvedItemProperty() with
        {
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
            IsSearchable = true,
            NotSearchableReason = null,
        };
    }

    private static ResolvedSearchComponent UnknownModifier()
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = "+12(11-13)% to all Elemental Resistances",
            CanonicalSignature = "+<number>% to all Elemental Resistances",
            ParsedKind = ParsedModifierKind.Suffix,
            ParsedModifierName = "of the Rainbow",
            ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
            IsSearchable = false,
            IsSelected = false,
        };
    }

    private static ResolvedSearchComponent ContributorParent(
        decimal minimum,
        bool firstSelected,
        bool secondSelected)
    {
        var variant = new SearchFilterVariant
        {
            Identity = "variant-exact",
            Label = "Pseudo",
            Description = "#% increased total Physical Damage",
            ProviderKind = "pseudo",
            SupportsContributorComposition = true,
            SupportsValueBounds = true,
        };
        SearchComponentContributor Contributor(string id, decimal value, bool selected) => new()
        {
            ContributorId = id,
            Source = new SearchComponentSourceProvenance
            {
                ComponentId = id,
                OriginalText = $"{value}% increased Physical Damage",
                CanonicalSignature = "<number>% increased Physical Damage",
                ParsedKind = ParsedModifierKind.Prefix,
                ProviderDomain = "Explicit",
                ResolvedModifierId = id,
                ResolvedStatIds = ["local_physical_damage_+%"],
                CanonicalNumericValues = [value],
            },
            DisplayText = $"{value}% increased Physical Damage",
            IsSelected = selected,
            RequestedMinimum = value,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderIdentity = $"identity-{id}",
        };

        return ExactModifier() with
        {
            OriginalText = "146% increased Physical Damage",
            CanonicalSignature = "<number>% increased Physical Damage",
            RequestedMinimum = minimum,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            IsSelected = true,
            FilterVariants = [variant],
            SelectedFilterVariantIdentity = variant.Identity,
            ContributorProjection = SearchComponentContributorProjection.Additive,
            Contributors =
            [
                Contributor("source-30", 30m, firstSelected),
                Contributor("source-116", 116m, secondSelected),
            ],
        };
    }

    private static void AssertDiagnostic(
        TradeSearchValidationResult result,
        string code,
        TradeSearchValidationSeverity severity,
        int? modifierFilterIndex = null,
        int? itemPropertyIndex = null)
    {
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == code &&
            diagnostic.Severity == severity &&
            diagnostic.ModifierFilterIndex == modifierFilterIndex &&
            diagnostic.ItemPropertyIndex == itemPropertyIndex);
    }

    private static string DiagnosticSignature(TradeSearchValidationDiagnostic diagnostic)
    {
        return string.Join(
            "|",
            diagnostic.Code,
            diagnostic.Severity,
            diagnostic.ModifierFilterIndex?.ToString() ?? string.Empty,
            diagnostic.Message);
    }
}
