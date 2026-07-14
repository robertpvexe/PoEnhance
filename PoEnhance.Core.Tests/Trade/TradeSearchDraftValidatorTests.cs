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
    public void Validate_MerchantOnlyAndInPerson_ProduceSameOutcome()
    {
        var merchantOnlyDraft = ValidDraft() with
        {
            ListingMode = TradeListingMode.MerchantOnly,
        };
        var inPersonDraft = merchantOnlyDraft with
        {
            ListingMode = TradeListingMode.InPerson,
        };

        var merchantOnlyResult = validator.Validate(merchantOnlyDraft);
        var inPersonResult = validator.Validate(inPersonDraft);

        Assert.Equal(merchantOnlyResult.IsValid, inPersonResult.IsValid);
        Assert.Equal(
            merchantOnlyResult.Diagnostics.Select(DiagnosticSignature),
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
        IReadOnlyList<TradeModifierFilterDraft>? modifiers = null)
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
            ListingMode = TradeListingMode.MerchantOnly,
        };
    }

    private static TradeModifierFilterDraft ExactModifier()
    {
        return new TradeModifierFilterDraft
        {
            OriginalText = "+55(50-59) to maximum Life",
            ParsedKind = ParsedModifierKind.Prefix,
            ParsedModifierName = "Hale",
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "mod.prefix.hale",
            ResolvedModifierName = "Hale",
            IsSelected = false,
        };
    }

    private static TradeModifierFilterDraft UnknownModifier()
    {
        return new TradeModifierFilterDraft
        {
            OriginalText = "+12(11-13)% to all Elemental Resistances",
            ParsedKind = ParsedModifierKind.Suffix,
            ParsedModifierName = "of the Rainbow",
            ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
            IsSelected = false,
        };
    }

    private static void AssertDiagnostic(
        TradeSearchValidationResult result,
        string code,
        TradeSearchValidationSeverity severity,
        int? modifierFilterIndex = null)
    {
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == code &&
            diagnostic.Severity == severity &&
            diagnostic.ModifierFilterIndex == modifierFilterIndex);
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
