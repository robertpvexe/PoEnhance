using System.Collections.Immutable;
using System.Globalization;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerSearchController
{
    public const string DefaultLeagueIdentifier = "Mirage";
    private const int MaximumSafeProviderMessageLength = 160;
    private const int MaximumModifierTextLength = 120;

    private readonly IPathOfExileTradePriceCheckService priceCheckService;
    private readonly ITradeSearchDraftValidator draftValidator;
    private readonly IExternalUrlLauncher externalUrlLauncher;
    private readonly PathOfExileTradeSearchUrlBuilder tradeSearchUrlBuilder;
    private IPriceCheckerWindow? window;
    private TradeSearchDraft? currentDraft;
    private BaseSearchCriterion? userSelectedBaseCriterion;
    private TradeSearchValidationResult? currentValidationResult;
    private PriceCheckerItemPresentation currentPresentation = new();
    private CancellationTokenSource? activeRequestCancellation;
    private readonly List<string> paginationResultIds = [];
    private readonly HashSet<string> fetchedResultIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> displayedOfferIds = new(StringComparer.Ordinal);
    private readonly List<PriceCheckerOfferViewModel> displayedOffers = [];
    private readonly Dictionary<int, ModifierBoundInput> itemPropertyBoundInputs = [];
    private readonly Dictionary<int, ModifierBoundInput> modifierBoundInputs = [];
    private readonly Dictionary<ModifierContributorKey, ModifierBoundInput> contributorBoundInputs = [];
    private readonly Dictionary<int, decimal?> canonicalContributorParentMinimums = [];
    private readonly Dictionary<int, decimal?> manualContributorParentMinimums = [];
    private readonly Dictionary<int, PriceCheckerParentMinimumState> parentMinimumStates = [];
    private readonly HashSet<int> expandedModifierIndexes = [];
    private readonly HashSet<int> expandedItemPropertyIndexes = [];
    private PriceCheckerItemResetSnapshot? initialItemSnapshot;
    private PriceCheckerSuccessfulSearchIdentity? successfulSearch;
    private int? paginationProviderTotal;
    private bool? paginationInexact;
    private int visibleOfferCapacity = PathOfExileTradeEndpointBuilder.MaximumFetchResultIds;
    private string leagueIdentifier;
    private int generation;
    private bool isLoading;
    private bool isLoadingMore;

    public PriceCheckerSearchController(
        IPathOfExileTradePriceCheckService priceCheckService,
        ITradeSearchDraftValidator? draftValidator = null,
        IExternalUrlLauncher? externalUrlLauncher = null,
        PathOfExileTradeSearchUrlBuilder? tradeSearchUrlBuilder = null)
    {
        this.priceCheckService = priceCheckService ?? throw new ArgumentNullException(nameof(priceCheckService));
        this.draftValidator = draftValidator ?? new CoreTradeSearchDraftValidatorAdapter();
        this.externalUrlLauncher = externalUrlLauncher ?? new SystemExternalUrlLauncher();
        this.tradeSearchUrlBuilder = tradeSearchUrlBuilder ?? new PathOfExileTradeSearchUrlBuilder();
        leagueIdentifier = DefaultLeagueIdentifier;
        CurrentViewState = CreateIdleOrValidationState();
    }

    public PriceCheckerSearchViewState CurrentViewState { get; private set; }

    public PriceCheckerDeveloperDiagnosticsSnapshot CurrentDeveloperDiagnostics { get; private set; } =
        PriceCheckerDeveloperDiagnosticsSnapshot.Idle;

    public event EventHandler<PriceCheckerDeveloperDiagnosticsSnapshot>? DeveloperDiagnosticsChanged;

    public void AttachWindow(IPriceCheckerWindow priceCheckerWindow)
    {
        ArgumentNullException.ThrowIfNull(priceCheckerWindow);

        if (ReferenceEquals(window, priceCheckerWindow))
        {
            priceCheckerWindow.UpdateSearch(CurrentViewState);
            return;
        }

        if (window is not null)
        {
            DetachWindow(window);
        }

        window = priceCheckerWindow;
        priceCheckerWindow.Closed += OnWindowClosed;
        priceCheckerWindow.SearchRequested += OnSearchRequested;
        priceCheckerWindow.LoadMoreRequested += OnLoadMoreRequested;
        priceCheckerWindow.TradeRequested += OnTradeRequested;
        priceCheckerWindow.OfferCapacityChanged += OnOfferCapacityChanged;
        priceCheckerWindow.ItemPropertySelectionChanged += OnItemPropertySelectionChanged;
        priceCheckerWindow.ItemPropertyBoundsChanged += OnItemPropertyBoundsChanged;
        priceCheckerWindow.ItemPropertyExpansionChanged += OnItemPropertyExpansionChanged;
        priceCheckerWindow.RequestedItemFilterActivationChanged +=
            OnRequestedItemFilterActivationChanged;
        priceCheckerWindow.RequestedItemFilterValueChanged += OnRequestedItemFilterValueChanged;
        priceCheckerWindow.ModifierSelectionChanged += OnModifierSelectionChanged;
        priceCheckerWindow.ModifierBoundsChanged += OnModifierBoundsChanged;
        priceCheckerWindow.ModifierFilterVariantChanged += OnModifierFilterVariantChanged;
        priceCheckerWindow.ModifierExpansionChanged += OnModifierExpansionChanged;
        priceCheckerWindow.BaseCriterionToggleRequested += OnBaseCriterionToggleRequested;
        priceCheckerWindow.ItemStateChanged += OnItemStateChanged;
        priceCheckerWindow.RarityChanged += OnRarityChanged;
        priceCheckerWindow.ResetItemRequested += OnResetItemRequested;
        priceCheckerWindow.UpdateSearch(CurrentViewState);
    }

    public void DetachWindow(IPriceCheckerWindow priceCheckerWindow)
    {
        if (!ReferenceEquals(window, priceCheckerWindow))
        {
            return;
        }

        priceCheckerWindow.SearchRequested -= OnSearchRequested;
        priceCheckerWindow.LoadMoreRequested -= OnLoadMoreRequested;
        priceCheckerWindow.TradeRequested -= OnTradeRequested;
        priceCheckerWindow.OfferCapacityChanged -= OnOfferCapacityChanged;
        priceCheckerWindow.ItemPropertySelectionChanged -= OnItemPropertySelectionChanged;
        priceCheckerWindow.ItemPropertyBoundsChanged -= OnItemPropertyBoundsChanged;
        priceCheckerWindow.ItemPropertyExpansionChanged -= OnItemPropertyExpansionChanged;
        priceCheckerWindow.RequestedItemFilterActivationChanged -=
            OnRequestedItemFilterActivationChanged;
        priceCheckerWindow.RequestedItemFilterValueChanged -= OnRequestedItemFilterValueChanged;
        priceCheckerWindow.ModifierSelectionChanged -= OnModifierSelectionChanged;
        priceCheckerWindow.ModifierBoundsChanged -= OnModifierBoundsChanged;
        priceCheckerWindow.ModifierFilterVariantChanged -= OnModifierFilterVariantChanged;
        priceCheckerWindow.ModifierExpansionChanged -= OnModifierExpansionChanged;
        priceCheckerWindow.BaseCriterionToggleRequested -= OnBaseCriterionToggleRequested;
        priceCheckerWindow.ItemStateChanged -= OnItemStateChanged;
        priceCheckerWindow.RarityChanged -= OnRarityChanged;
        priceCheckerWindow.ResetItemRequested -= OnResetItemRequested;
        priceCheckerWindow.Closed -= OnWindowClosed;
        window = null;
        generation++;
        CancelActiveRequest();
        ClearPaginationState();
    }

    public async Task<PriceCheckerItemPresentation> PreparePresentationAsync(
        TradeSearchDraft draft,
        PriceCheckerItemPresentation presentation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(presentation);

        string? categoryDisplayLabel;
        try
        {
            categoryDisplayLabel = await priceCheckService
                .LoadCategoryDisplayLabelAsync(draft, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return presentation;
        }

        return string.IsNullOrWhiteSpace(categoryDisplayLabel)
            ? presentation
            : presentation with
            {
                CategoryDisplayLabel = categoryDisplayLabel,
            };
    }

    public async Task<TradeSearchDraft> PrepareDraftAsync(
        TradeSearchDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        try
        {
            return await priceCheckService
                .PrepareEffectiveDraftAsync(draft, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return draft;
        }
    }

    public void UpdateCurrentDraft(
        TradeSearchDraft draft,
        TradeSearchValidationResult validationResult,
        PriceCheckerItemPresentation? presentation = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(validationResult);

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        currentDraft = ResetModifierSelections(priceCheckService.ResolveEffectiveDraft(draft));
        expandedModifierIndexes.Clear();
        expandedItemPropertyIndexes.Clear();
        InitializeItemPropertyBoundInputs(currentDraft);
        InitializeModifierBoundInputs(currentDraft);
        var synchronizedModifiers = currentDraft.ModifierFilters
            .Select((modifier, index) => SynchronizeContributorParentMinimum(index, modifier))
            .ToArray();
        if (synchronizedModifiers.Where((modifier, index) =>
                !ReferenceEquals(modifier, currentDraft.ModifierFilters[index])).Any())
        {
            currentDraft = currentDraft with { ModifierFilters = synchronizedModifiers };
        }

        userSelectedBaseCriterion = currentDraft.Base.ActiveCriterion;
        currentPresentation = (presentation ?? new PriceCheckerItemPresentation()) with
        {
            IsRarityEditable = PriceCheckerRarity.IsOrdinary(draft.Rarity),
        };
        currentValidationResult = ReferenceEquals(currentDraft, draft)
            ? validationResult
            : draftValidator.Validate(currentDraft);
        initialItemSnapshot = CaptureInitialItemSnapshot();
        PublishCurrentContent();
        ApplyState(CreateIdleOrValidationState());
    }

    public async Task SearchAsync()
    {
        if (isLoading)
        {
            if (!isLoadingMore)
            {
                return;
            }

            generation++;
            CancelActiveRequest();
            ClearPaginationState();
        }

        var validationState = CreateLocalValidationState();
        if (validationState is not null)
        {
            ApplyState(validationState);
            return;
        }

        var draft = currentDraft;
        var validationResult = currentValidationResult;
        var trimmedLeague = TrimLeague(leagueIdentifier);
        if (draft is null || validationResult is null || trimmedLeague is null)
        {
            ApplyState(CreateIdleOrValidationState());
            return;
        }

        ClearPaginationState();
        var requestGeneration = ++generation;
        using var requestCancellation = new CancellationTokenSource();
        activeRequestCancellation = requestCancellation;
        isLoading = true;
        isLoadingMore = false;
        ApplyState(new PriceCheckerSearchViewState
        {
            Status = PriceCheckerSearchViewStatus.Loading,
            LeagueIdentifier = trimmedLeague,
            CanSearch = false,
            Message = "Searching...",
            ItemProperties = CreateItemPropertyRows(),
            Modifiers = CreateModifierRows(),
        });

        PathOfExileTradePriceCheckResult result;
        try
        {
            result = await priceCheckService.CheckAsync(
                draft,
                validationResult,
                trimmedLeague,
                InitialFetchResultCount(),
                requestCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            result = new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Search,
                IsCancelled = true,
            };
        }
        catch
        {
            result = new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Search,
                Diagnostics =
                [
                    new PathOfExileTradePriceCheckDiagnostic(
                        PathOfExileTradePriceCheckDiagnosticCodes.SearchFailed,
                        "Trade request failed. Try again later.",
                        PathOfExileTradePriceCheckStage.Search),
                ],
            };
        }

        if (!IsCurrentRequest(requestGeneration, requestCancellation))
        {
            return;
        }

        activeRequestCancellation = null;
        isLoading = false;
        isLoadingMore = false;
        var effectiveDraft = result.EffectiveDraft;
        if (effectiveDraft is not null)
        {
            currentDraft = effectiveDraft;
            currentValidationResult = draftValidator.Validate(effectiveDraft);
            PublishCurrentContent();
        }

        ApplyState(MapResult(result, effectiveDraft, trimmedLeague));
    }

    public async Task LoadMoreAsync()
    {
        if (isLoading || !TryGetNextFetchIds(out var fetchIds))
        {
            return;
        }

        var searchQueryId = successfulSearch?.QueryId;
        var requestGeneration = ++generation;
        using var requestCancellation = new CancellationTokenSource();
        activeRequestCancellation = requestCancellation;
        isLoading = true;
        isLoadingMore = true;
        ApplyState(CreateLoadedResultsState(
            "Loading more offers...",
            canLoadMore: false));

        PathOfExileTradePriceCheckResult result;
        try
        {
            result = await priceCheckService.FetchMoreAsync(
                searchQueryId,
                fetchIds,
                requestCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            result = new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Fetch,
                IsCancelled = true,
            };
        }
        catch
        {
            result = new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Fetch,
                Diagnostics =
                [
                    new PathOfExileTradePriceCheckDiagnostic(
                        PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed,
                        "Loading more Trade offers failed.",
                        PathOfExileTradePriceCheckStage.Fetch),
                ],
            };
        }

        if (!IsCurrentRequest(requestGeneration, requestCancellation))
        {
            return;
        }

        activeRequestCancellation = null;
        isLoading = false;
        isLoadingMore = false;
        if (result.IsSuccess)
        {
            fetchedResultIds.UnionWith(fetchIds);
            AppendOffers(result.Offers, fetchIds);
            ApplyState(CreateLoadedResultsState("Search complete."));
            return;
        }

        ApplyState(CreateLoadedResultsState(
            result.IsCancelled
                ? "Loading more offers was cancelled."
                : "Could not load more offers. Try again."));
    }

    public void UpdateItemPropertySelection(int itemPropertyIndex, bool isSelected)
    {
        if (currentDraft is null ||
            itemPropertyIndex < 0 ||
            itemPropertyIndex >= currentDraft.ItemProperties.Length)
        {
            return;
        }

        var property = currentDraft.ItemProperties[itemPropertyIndex];
        if ((!IsItemPropertyAvailable(property) && isSelected) || property.IsSelected == isSelected)
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        currentDraft = currentDraft with
        {
            ItemProperties = currentDraft.ItemProperties
                .Select((candidate, index) => index == itemPropertyIndex
                    ? candidate with { IsSelected = isSelected }
                    : candidate)
                .ToImmutableArray(),
        };
        currentValidationResult = draftValidator.Validate(currentDraft);
        PublishCurrentContent();
        ApplyState(CreateIdleOrValidationState());
    }

    public void UpdateItemPropertyBounds(
        int itemPropertyIndex,
        string? minimumText,
        string? maximumText)
    {
        if (currentDraft is null ||
            itemPropertyIndex < 0 ||
            itemPropertyIndex >= currentDraft.ItemProperties.Length ||
            !IsItemPropertyAvailable(currentDraft.ItemProperties[itemPropertyIndex]))
        {
            return;
        }

        var previousInput = itemPropertyBoundInputs.GetValueOrDefault(itemPropertyIndex);
        var input = new ModifierBoundInput(minimumText ?? string.Empty, maximumText ?? string.Empty);
        itemPropertyBoundInputs[itemPropertyIndex] = input;
        var minimum = ParseBound(input.MinimumText);
        var maximum = ParseBound(input.MaximumText);
        var property = currentDraft.ItemProperties[itemPropertyIndex];
        var next = property with
        {
            RequestedMinimum = minimum.IsValid ? minimum.Value : property.RequestedMinimum,
            RequestedMaximum = maximum.IsValid
                ? maximum.Value
                : property.RequestedMaximum,
        };
        var inputChanged = previousInput is null || previousInput != input;
        if (next == property && !HasInvalidItemPropertyBoundInput(input) && !inputChanged)
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        currentDraft = currentDraft with
        {
            ItemProperties = currentDraft.ItemProperties
                .Select((candidate, index) => index == itemPropertyIndex ? next : candidate)
                .ToImmutableArray(),
        };
        currentValidationResult = draftValidator.Validate(currentDraft);
        PublishCurrentContent();
        ApplyState(CreateBoundChangeInvalidatedState());
    }

    public void UpdateItemPropertyExpansion(int itemPropertyIndex, bool isExpanded)
    {
        if (currentDraft is null ||
            ItemPropertyModifierIndexes(currentDraft, itemPropertyIndex).Count == 0)
        {
            return;
        }

        if (isExpanded)
        {
            expandedItemPropertyIndexes.Add(itemPropertyIndex);
        }
        else
        {
            expandedItemPropertyIndexes.Remove(itemPropertyIndex);
        }
        ApplyState(CurrentViewState with
        {
            ItemProperties = CreateItemPropertyRows(),
            Modifiers = CreateModifierRows(),
        });
    }

    public void UpdateRequestedItemFilterActivation(
        TradeSearchRequestedItemFilterKind kind,
        bool isActive)
    {
        if (currentDraft is null)
        {
            return;
        }

        var filter = currentDraft.RequestedItemFilters.FirstOrDefault(filter => filter.Kind == kind);
        if (filter is null || filter.IsActive == isActive)
        {
            return;
        }

        UpdateRequestedItemFilter(
            TradeSearchDraftMapper.ParseRequestedItemFilterText(
                filter,
                filter.CurrentText,
                isActive));
    }

    public void UpdateRequestedItemFilterValue(
        TradeSearchRequestedItemFilterKind kind,
        string? text)
    {
        if (currentDraft is null)
        {
            return;
        }

        var filter = currentDraft.RequestedItemFilters.FirstOrDefault(filter => filter.Kind == kind);
        if (filter is null ||
            filter.IsActive && string.Equals(filter.CurrentText, text ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }

        UpdateRequestedItemFilter(
            TradeSearchDraftMapper.ParseRequestedItemFilterText(
                filter,
                text,
                isActive: true));
    }

    private void UpdateRequestedItemFilter(TradeSearchRequestedItemFilter updatedFilter)
    {
        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        var updatedDraft = currentDraft! with
        {
            RequestedItemFilters = currentDraft.RequestedItemFilters
                .Select(filter => filter.Kind == updatedFilter.Kind ? updatedFilter : filter)
                .ToImmutableArray(),
        };
        currentDraft = priceCheckService.ResolveEffectiveDraft(updatedDraft);
        currentValidationResult = draftValidator.Validate(currentDraft);
        PublishCurrentContent();
        ApplyState(CreateBoundChangeInvalidatedState());
    }

    public void UpdateModifierSelection(
        int modifierIndex,
        bool isSelected)
    {
        if (currentDraft is null ||
            modifierIndex < 0 ||
            modifierIndex >= currentDraft.ModifierFilters.Count)
        {
            return;
        }

        var requestedModifier = currentDraft.ModifierFilters[modifierIndex];
        if (isSelected && !IsModifierInteractionReady(requestedModifier))
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();

        var modifierFilters = currentDraft.ModifierFilters
            .Select((modifier, index) => index == modifierIndex
                ? modifier with
                {
                    IsSelected = isSelected,
                    Contributors = !isSelected
                        ? modifier.Contributors
                            .Select(contributor => contributor with { IsSelected = false })
                            .ToArray()
                        : modifier.Contributors,
                }
                : modifier)
            .ToArray();
        var userSelectedDraft = currentDraft with
        {
            ModifierFilters = modifierFilters,
            Base = currentDraft.Base with
            {
                ActiveCriterion = userSelectedBaseCriterion ?? currentDraft.Base.ActiveCriterion,
            },
        };
        currentDraft = priceCheckService.ResolveEffectiveDraft(userSelectedDraft);
        currentDraft = ReplaceModifier(
            currentDraft,
            modifierIndex,
            SynchronizeContributorParentMinimum(
                modifierIndex,
                currentDraft.ModifierFilters[modifierIndex]));
        currentValidationResult = draftValidator.Validate(currentDraft);

        PublishCurrentContent();
        ApplyState(CreateIdleOrValidationState());
    }

    public void UpdateModifierContributorSelection(
        int modifierIndex,
        int contributorIndex,
        bool isSelected)
    {
        if (!TryGetContributor(modifierIndex, contributorIndex, out var parent, out var contributor) ||
            !SearchComponentContributorActivation.SupportsComposition(parent) ||
            contributor.IsSelected == isSelected)
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();

        var contributors = parent.Contributors
            .Select((candidate, index) => index == contributorIndex
                ? candidate with { IsSelected = isSelected }
                : candidate)
            .ToArray();
        var nextParent = parent with
        {
            IsSelected = parent.IsSelected || isSelected,
            Contributors = contributors,
        };
        manualContributorParentMinimums.Remove(modifierIndex);
        parentMinimumStates[modifierIndex] = nextParent.Contributors.Any(candidate => candidate.IsSelected)
            ? PriceCheckerParentMinimumState.ChildDerived
            : PriceCheckerParentMinimumState.CanonicalDefault;
        nextParent = SynchronizeContributorParentMinimum(modifierIndex, nextParent);

        currentDraft = currentDraft! with
        {
            ModifierFilters = currentDraft.ModifierFilters
                .Select((modifier, index) => index == modifierIndex ? nextParent : modifier)
                .ToArray(),
        };
        currentValidationResult = draftValidator.Validate(currentDraft);
        PublishCurrentContent();
        ApplyState(CreateIdleOrValidationState());
    }

    public void UpdateModifierBounds(int modifierIndex, string? minimumText, string? maximumText)
    {
        if (currentDraft is null || modifierIndex < 0 || modifierIndex >= currentDraft.ModifierFilters.Count ||
            !currentDraft.ModifierFilters[modifierIndex].SupportsValueBounds ||
            !IsModifierInteractionReady(currentDraft.ModifierFilters[modifierIndex]))
        {
            return;
        }

        var previousInput = modifierBoundInputs.GetValueOrDefault(modifierIndex);
        var input = new ModifierBoundInput(minimumText ?? string.Empty, maximumText ?? string.Empty);
        modifierBoundInputs[modifierIndex] = input;
        var component = currentDraft.ModifierFilters[modifierIndex];
        var parsedMinimum = ParseBound(input.MinimumText);
        var parsedMaximum = ParseBound(input.MaximumText);
        var next = component with
        {
            RequestedMinimum = parsedMinimum.IsValid ? parsedMinimum.Value : component.RequestedMinimum,
            RequestedMaximum = parsedMaximum.IsValid ? parsedMaximum.Value : component.RequestedMaximum,
        };
        if (component.Contributors.Count > 0 &&
            parsedMinimum.IsValid &&
            !string.Equals(previousInput?.MinimumText, input.MinimumText, StringComparison.Ordinal))
        {
            manualContributorParentMinimums[modifierIndex] = parsedMinimum.Value;
            parentMinimumStates[modifierIndex] = PriceCheckerParentMinimumState.ManualOverride;
            next = SynchronizeContributorParentMinimum(modifierIndex, next);
        }

        if (next == component && !HasInvalidBoundInput(input))
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        currentDraft = currentDraft with
        {
            ModifierFilters = currentDraft.ModifierFilters
                .Select((modifier, index) => index == modifierIndex ? next : modifier)
                .ToArray(),
        };
        currentValidationResult = draftValidator.Validate(currentDraft);
        RefreshDisplayedContributorActivity(modifierIndex, next);
        ApplyState(CreateBoundChangeInvalidatedState());
    }

    public void UpdateModifierContributorBounds(
        int modifierIndex,
        int contributorIndex,
        string? minimumText,
        string? maximumText)
    {
        if (!TryGetContributor(modifierIndex, contributorIndex, out var parent, out var contributor) ||
            !SearchComponentContributorActivation.SupportsComposition(parent) ||
            !contributor.SupportsValueBounds)
        {
            return;
        }

        var key = new ModifierContributorKey(modifierIndex, contributorIndex);
        var input = new ModifierBoundInput(minimumText ?? string.Empty, maximumText ?? string.Empty);
        contributorBoundInputs[key] = input;
        var parsedMinimum = ParseBound(input.MinimumText);
        var parsedMaximum = ParseBound(input.MaximumText);
        var nextContributor = contributor with
        {
            RequestedMinimum = parsedMinimum.IsValid ? parsedMinimum.Value : contributor.RequestedMinimum,
            RequestedMaximum = parsedMaximum.IsValid ? parsedMaximum.Value : contributor.RequestedMaximum,
        };
        if (nextContributor == contributor && !HasInvalidBoundInput(input))
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        var nextParent = parent with
        {
            Contributors = parent.Contributors
                .Select((candidate, childIndex) => childIndex == contributorIndex
                    ? nextContributor
                    : candidate)
                .ToArray(),
        };
        nextParent = SynchronizeContributorParentMinimum(modifierIndex, nextParent);
        currentDraft = ReplaceModifier(currentDraft!, modifierIndex, nextParent);
        currentValidationResult = draftValidator.Validate(currentDraft);
        RefreshDisplayedContributorActivity(modifierIndex, nextParent);
        ApplyState(CreateBoundChangeInvalidatedState());
    }

    public void UpdateModifierFilterVariant(int modifierIndex, string? variantIdentity)
    {
        if (currentDraft is null ||
            modifierIndex < 0 ||
            modifierIndex >= currentDraft.ModifierFilters.Count ||
            string.IsNullOrWhiteSpace(variantIdentity))
        {
            return;
        }

        var component = currentDraft.ModifierFilters[modifierIndex];
        var selectedIdentity = variantIdentity.Trim();
        if (HasFixedProviderVariant(component) ||
            !component.IsSelected ||
            string.Equals(component.SelectedFilterVariantIdentity, selectedIdentity, StringComparison.Ordinal) ||
            !component.FilterVariants.Any(option => string.Equals(
                option.Identity,
                selectedIdentity,
                StringComparison.Ordinal)))
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        var pendingDraft = currentDraft with
        {
            ModifierFilters = currentDraft.ModifierFilters
                .Select((modifier, index) => index == modifierIndex
                    ? modifier with
                    {
                        SelectedFilterVariantIdentity = selectedIdentity,
                        ProviderResolutionStatus = SearchComponentProviderResolutionStatus.NotResolved,
                        ProviderStatId = null,
                        ProviderStatText = null,
                        ProviderCandidateStatIds = [],
                        ProviderDiagnosticCode = null,
                        ProviderDiagnosticMessage = null,
                    }
                    : modifier)
                .ToArray(),
        };
        currentDraft = priceCheckService.ResolveEffectiveDraft(pendingDraft);
        var resolved = currentDraft.ModifierFilters[modifierIndex];
        if (resolved.SupportsValueBounds &&
            modifierBoundInputs.TryGetValue(modifierIndex, out var input))
        {
            var minimum = ParseBound(input.MinimumText);
            var maximum = ParseBound(input.MaximumText);
            resolved = resolved with
            {
                RequestedMinimum = minimum.IsValid ? minimum.Value : resolved.RequestedMinimum,
                RequestedMaximum = maximum.IsValid ? maximum.Value : resolved.RequestedMaximum,
            };
            currentDraft = currentDraft with
            {
                ModifierFilters = currentDraft.ModifierFilters
                    .Select((modifier, index) => index == modifierIndex ? resolved : modifier)
                    .ToArray(),
            };
        }

        currentDraft = ReplaceModifier(
            currentDraft,
            modifierIndex,
            SynchronizeContributorParentMinimum(
                modifierIndex,
                currentDraft.ModifierFilters[modifierIndex]));

        currentValidationResult = draftValidator.Validate(currentDraft);
        PublishCurrentContent();
        ApplyState(CreateIdleOrValidationState());
    }

    public void UpdateModifierExpansion(int modifierIndex, bool isExpanded)
    {
        if (currentDraft?.ModifierFilters.ElementAtOrDefault(modifierIndex)?.Contributors.Count is not > 0)
        {
            return;
        }

        if (isExpanded)
        {
            expandedModifierIndexes.Add(modifierIndex);
        }
        else
        {
            expandedModifierIndexes.Remove(modifierIndex);
        }

        ApplyState(CurrentViewState with { Modifiers = CreateModifierRows() });
    }

    public void ResetCurrentItem()
    {
        if (initialItemSnapshot is null)
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();

        var snapshot = initialItemSnapshot;
        currentDraft = snapshot.Draft;
        currentValidationResult = snapshot.ValidationResult;
        currentPresentation = snapshot.Presentation;
        userSelectedBaseCriterion = snapshot.UserSelectedBaseCriterion;
        RestoreDictionary(itemPropertyBoundInputs, snapshot.ItemPropertyBoundInputs);
        RestoreDictionary(modifierBoundInputs, snapshot.ModifierBoundInputs);
        RestoreDictionary(contributorBoundInputs, snapshot.ContributorBoundInputs);
        RestoreDictionary(canonicalContributorParentMinimums, snapshot.CanonicalParentMinimums);
        RestoreDictionary(manualContributorParentMinimums, snapshot.ManualParentMinimums);
        RestoreDictionary(parentMinimumStates, snapshot.ParentMinimumStates);

        PublishCurrentContent();
        ApplyState(CreateIdleOrValidationState());
    }

    public void ToggleBaseCriterion()
    {
        if (currentDraft?.Base is not { } baseDraft)
        {
            return;
        }

        var nextCriterion = baseDraft.ActiveCriterion?.Mode switch
        {
            BaseSearchMode.Category => baseDraft.AvailableCriteria.ExactBase,
            BaseSearchMode.ExactBase => baseDraft.AvailableCriteria.Category,
            _ => null,
        };
        if (nextCriterion is null)
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        userSelectedBaseCriterion = nextCriterion;
        var userSelectedDraft = currentDraft with
        {
            Base = baseDraft with
            {
                ActiveCriterion = nextCriterion,
            },
        };
        currentDraft = priceCheckService.ResolveEffectiveDraft(userSelectedDraft);
        currentValidationResult = draftValidator.Validate(currentDraft);
        PublishCurrentContent();
        ApplyState(CreateIdleOrValidationState());
    }

    public void UpdateItemStateCriterion(TradeItemStateKind kind, TradeTriState state)
    {
        if (currentDraft is null ||
            state is TradeTriState.Auto ||
            currentDraft.ItemStateCriteria.Get(kind) == state)
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        currentDraft = currentDraft with
        {
            ItemStateCriteria = currentDraft.ItemStateCriteria.With(kind, state),
        };
        currentValidationResult = draftValidator.Validate(currentDraft);
        PublishCurrentContent();
        ApplyState(CreateBoundChangeInvalidatedState());
    }

    public void UpdateRarity(string? rarity)
    {
        if (currentDraft is null ||
            !currentPresentation.IsRarityEditable ||
            !PriceCheckerRarity.TryNormalizeEditable(rarity, out var normalizedRarity) ||
            string.Equals(currentDraft.Rarity?.Trim(), normalizedRarity, StringComparison.Ordinal))
        {
            return;
        }

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        currentDraft = currentDraft with { Rarity = normalizedRarity };
        currentValidationResult = draftValidator.Validate(currentDraft);
        PublishCurrentContent();
        ApplyState(CreateBoundChangeInvalidatedState());
    }

    private void OnSearchRequested(object? sender, EventArgs e)
    {
        _ = SearchAsync();
    }

    private void OnLoadMoreRequested(object? sender, EventArgs e)
    {
        _ = LoadMoreAsync();
    }

    private void OnTradeRequested(object? sender, EventArgs e)
    {
        var search = successfulSearch;
        if (search is null)
        {
            return;
        }

        if (!tradeSearchUrlBuilder.TryBuild(search.LeagueIdentifier, search.QueryId, out var uri) ||
            uri is null ||
            !externalUrlLauncher.TryOpen(uri))
        {
            ApplyState(CurrentViewState with
            {
                Message = "Could not open Trade in your browser.",
                CanOpenTrade = successfulSearch is not null,
            });
        }
    }

    private void OnOfferCapacityChanged(
        object? sender,
        PriceCheckerOfferCapacityChangedEventArgs e)
    {
        var capacity = Math.Max(0, e.Capacity);
        if (capacity == visibleOfferCapacity)
        {
            return;
        }

        visibleOfferCapacity = capacity;
        TrimDisplayedOffersToCapacity();
        ApplyState(CurrentViewState with
        {
            CanLoadMore = CanFetchMore(),
            Offers = displayedOffers.ToArray(),
            Summary = CurrentViewState.Status is PriceCheckerSearchViewStatus.Success or PriceCheckerSearchViewStatus.Loading
                ? CreateSuccessSummary(displayedOffers.Count, paginationProviderTotal, paginationInexact)
                : CurrentViewState.Summary,
        });
    }

    private void OnItemPropertySelectionChanged(
        object? sender,
        PriceCheckerItemPropertySelectionChangedEventArgs e)
    {
        UpdateItemPropertySelection(e.ItemPropertyIndex, e.IsSelected);
    }

    private void OnItemPropertyBoundsChanged(
        object? sender,
        PriceCheckerItemPropertyBoundsChangedEventArgs e)
    {
        UpdateItemPropertyBounds(
            e.ItemPropertyIndex,
            e.MinimumText,
            e.MaximumText);
    }

    private void OnItemPropertyExpansionChanged(
        object? sender,
        PriceCheckerItemPropertyExpansionChangedEventArgs e)
    {
        UpdateItemPropertyExpansion(e.ItemPropertyIndex, e.IsExpanded);
    }

    private void OnRequestedItemFilterActivationChanged(
        object? sender,
        PriceCheckerRequestedItemFilterActivationChangedEventArgs e)
    {
        UpdateRequestedItemFilterActivation(e.Kind, e.IsActive);
    }

    private void OnRequestedItemFilterValueChanged(
        object? sender,
        PriceCheckerRequestedItemFilterValueChangedEventArgs e)
    {
        UpdateRequestedItemFilterValue(e.Kind, e.Text);
    }

    private void OnModifierSelectionChanged(
        object? sender,
        PriceCheckerModifierSelectionChangedEventArgs e)
    {
        if (e.ContributorIndex.HasValue)
        {
            UpdateModifierContributorSelection(
                e.ModifierIndex,
                e.ContributorIndex.Value,
                e.IsSelected);
            return;
        }

        UpdateModifierSelection(e.ModifierIndex, e.IsSelected);
    }

    private void OnModifierBoundsChanged(object? sender, PriceCheckerModifierBoundsChangedEventArgs e)
    {
        if (e.ContributorIndex.HasValue)
        {
            UpdateModifierContributorBounds(
                e.ModifierIndex,
                e.ContributorIndex.Value,
                e.MinimumText,
                e.MaximumText);
            return;
        }

        UpdateModifierBounds(e.ModifierIndex, e.MinimumText, e.MaximumText);
    }

    private void OnModifierFilterVariantChanged(
        object? sender,
        PriceCheckerModifierFilterVariantChangedEventArgs e)
    {
        UpdateModifierFilterVariant(e.ModifierIndex, e.VariantIdentity);
    }

    private void OnModifierExpansionChanged(
        object? sender,
        PriceCheckerModifierExpansionChangedEventArgs e)
    {
        UpdateModifierExpansion(e.ModifierIndex, e.IsExpanded);
    }

    private void OnBaseCriterionToggleRequested(object? sender, EventArgs e)
    {
        ToggleBaseCriterion();
    }

    private void OnItemStateChanged(object? sender, PriceCheckerItemStateChangedEventArgs e)
    {
        UpdateItemStateCriterion(e.Kind, e.State);
    }

    private void OnRarityChanged(object? sender, PriceCheckerRarityChangedEventArgs e)
    {
        UpdateRarity(e.Rarity);
    }

    private void OnResetItemRequested(object? sender, EventArgs e)
    {
        ResetCurrentItem();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is IPriceCheckerWindow priceCheckerWindow)
        {
            DetachWindow(priceCheckerWindow);
        }
    }

    private void PublishCurrentContent()
    {
        if (currentDraft is null || currentValidationResult is null)
        {
            return;
        }

        window?.UpdateContent(new PriceCheckerWindowState(currentDraft, currentValidationResult)
        {
            Presentation = currentPresentation,
        });
    }

    private bool IsCurrentRequest(
        int requestGeneration,
        CancellationTokenSource requestCancellation)
    {
        return requestGeneration == generation &&
            ReferenceEquals(activeRequestCancellation, requestCancellation);
    }

    private void CancelActiveRequest()
    {
        if (activeRequestCancellation is null)
        {
            isLoading = false;
            return;
        }

        activeRequestCancellation.Cancel();
        activeRequestCancellation = null;
        isLoading = false;
        isLoadingMore = false;
    }

    private PriceCheckerSearchViewState CreateIdleOrValidationState()
    {
        return CreateLocalValidationState() ?? new PriceCheckerSearchViewState
        {
            Status = PriceCheckerSearchViewStatus.Idle,
            LeagueIdentifier = leagueIdentifier,
            CanSearch = true,
            Message = "Ready to search.",
            ItemProperties = CreateItemPropertyRows(),
            Modifiers = CreateModifierRows(),
        };
    }

    private PriceCheckerSearchViewState CreateBoundChangeInvalidatedState()
    {
        return CurrentViewState with
        {
            Status = PriceCheckerSearchViewStatus.Idle,
            CanSearch = !isLoading && CreateLocalValidationState() is null,
            CanLoadMore = false,
            CanOpenTrade = false,
            Message = "Ready to search.",
            Summary = string.Empty,
            Offers = [],
        };
    }

    private PriceCheckerSearchViewState? CreateLocalValidationState()
    {
        if (TrimLeague(leagueIdentifier) is null)
        {
            return ValidationState("League is required.");
        }

        if (currentDraft is null || currentValidationResult is null)
        {
            return ValidationState("Select a supported Trade search.");
        }

        if (itemPropertyBoundInputs
            .Where(pair => currentDraft.ItemProperties.ElementAtOrDefault(pair.Key)?.IsSelected == true)
            .Any(pair => HasInvalidItemPropertyBoundInput(pair.Value)))
        {
            return ValidationState("Item property Min and Max must be finite decimal numbers.");
        }

        if (modifierBoundInputs
            .Where(pair => currentDraft.ModifierFilters.ElementAtOrDefault(pair.Key)?.IsSelected == true)
            .Any(pair => HasInvalidBoundInput(pair.Value)))
        {
            return ValidationState("Modifier Min and Max must be finite decimal numbers.");
        }

        if (contributorBoundInputs.Any(pair =>
                currentDraft.ModifierFilters.ElementAtOrDefault(pair.Key.ModifierIndex) is { } parent &&
                SearchComponentContributorActivation.IsFilteringActive(parent) &&
                parent.Contributors.ElementAtOrDefault(pair.Key.ContributorIndex)?.IsSelected == true &&
                HasInvalidBoundInput(pair.Value)))
        {
            return ValidationState("Contributor Min and Max must be finite decimal numbers.");
        }

        if (!currentValidationResult.IsValid)
        {
            return ValidationState(LocalValidationMessage(currentValidationResult));
        }

        return null;
    }

    private PriceCheckerSearchViewState ValidationState(string message)
    {
        return new PriceCheckerSearchViewState
        {
            Status = PriceCheckerSearchViewStatus.ValidationError,
            LeagueIdentifier = leagueIdentifier,
            CanSearch = false,
            Message = message,
            ItemProperties = CreateItemPropertyRows(),
            Modifiers = CreateModifierRows(),
        };
    }

    private PriceCheckerSearchViewState MapResult(
        PathOfExileTradePriceCheckResult result,
        TradeSearchDraft? effectiveDraft,
        string searchLeagueIdentifier)
    {
        if (result.IsCancelled)
        {
            return new PriceCheckerSearchViewState
            {
                Status = PriceCheckerSearchViewStatus.Cancelled,
                LeagueIdentifier = leagueIdentifier,
                CanSearch = CanStartSearch(),
                Message = "Search cancelled.",
                ItemProperties = CreateItemPropertyRows(effectiveDraft),
                Modifiers = CreateModifierRows(effectiveDraft),
            };
        }

        if (!result.IsSuccess)
        {
            var status = result.Stage is
                    PathOfExileTradePriceCheckStage.QueryBuild or
                    PathOfExileTradePriceCheckStage.ModifierMapping
                ? PriceCheckerSearchViewStatus.ValidationError
                : PriceCheckerSearchViewStatus.ProviderOrTransportError;
            return new PriceCheckerSearchViewState
            {
                Status = status,
                LeagueIdentifier = leagueIdentifier,
                CanSearch = CanStartSearch(),
                Message = FailureMessage(result),
                ItemProperties = CreateItemPropertyRows(effectiveDraft),
                Modifiers = CreateModifierRows(effectiveDraft),
            };
        }

        InitializePaginationState(result, searchLeagueIdentifier);
        if (displayedOffers.Count == 0 && !HasUnfetchedResultIds())
        {
            return new PriceCheckerSearchViewState
            {
                Status = PriceCheckerSearchViewStatus.ZeroResults,
                LeagueIdentifier = leagueIdentifier,
                CanSearch = CanStartSearch(),
                CanOpenTrade = successfulSearch is not null,
                Message = "No offers found.",
                Summary = string.Empty,
                ItemProperties = CreateItemPropertyRows(effectiveDraft),
                Modifiers = CreateModifierRows(effectiveDraft),
            };
        }

        return new PriceCheckerSearchViewState
        {
            Status = PriceCheckerSearchViewStatus.Success,
            LeagueIdentifier = leagueIdentifier,
            CanSearch = CanStartSearch(),
            CanLoadMore = CanFetchMore(),
            CanOpenTrade = successfulSearch is not null,
            Message = "Search complete.",
            Summary = CreateSuccessSummary(
                displayedOffers.Count,
                result.ProviderTotal,
                result.Inexact),
            ItemProperties = CreateItemPropertyRows(effectiveDraft),
            Modifiers = CreateModifierRows(effectiveDraft),
            Offers = displayedOffers.ToArray(),
        };
    }

    private PriceCheckerSearchViewState CreateLoadedResultsState(
        string message,
        bool? canLoadMore = null)
    {
        return new PriceCheckerSearchViewState
        {
            Status = isLoading ? PriceCheckerSearchViewStatus.Loading : PriceCheckerSearchViewStatus.Success,
            LeagueIdentifier = leagueIdentifier,
            CanSearch = !isLoading && CanStartSearch(),
            CanLoadMore = canLoadMore ?? (!isLoading && CanFetchMore()),
            CanOpenTrade = successfulSearch is not null,
            Message = message,
            Summary = CreateSuccessSummary(
                displayedOffers.Count,
                paginationProviderTotal,
                paginationInexact),
            ItemProperties = CreateItemPropertyRows(),
            Modifiers = CreateModifierRows(),
            Offers = displayedOffers.ToArray(),
        };
    }

    private bool CanStartSearch()
    {
        return !isLoading && CreateLocalValidationState() is null;
    }

    private int InitialFetchResultCount()
    {
        return Math.Min(
            PathOfExileTradeEndpointBuilder.MaximumFetchResultIds,
            visibleOfferCapacity);
    }

    private void InitializePaginationState(
        PathOfExileTradePriceCheckResult result,
        string searchLeagueIdentifier)
    {
        ClearPaginationState();
        var queryId = TrimToNull(result.SearchQueryId);
        successfulSearch = queryId is null
            ? null
            : new PriceCheckerSuccessfulSearchIdentity(queryId, searchLeagueIdentifier);
        paginationResultIds.AddRange(result.ResultIds
            .Select(TrimToNull)
            .Where(resultId => resultId is not null)
            .Select(resultId => resultId!));
        paginationProviderTotal = result.ProviderTotal;
        paginationInexact = result.Inexact;

        var initialOfferIds = result.FetchedResultIds.Count > 0
            ? result.FetchedResultIds
            : result.Offers.Select(offer => offer.Id).ToArray();
        var displayedInitialOfferIds = initialOfferIds
            .Select(TrimToNull)
            .Where(resultId => resultId is not null)
            .Select(resultId => resultId!)
            .Distinct(StringComparer.Ordinal)
            .Take(visibleOfferCapacity)
            .ToArray();
        fetchedResultIds.UnionWith(displayedInitialOfferIds);
        AppendOffers(result.Offers, displayedInitialOfferIds);
    }

    private void ClearPaginationState()
    {
        successfulSearch = null;
        paginationResultIds.Clear();
        fetchedResultIds.Clear();
        displayedOfferIds.Clear();
        displayedOffers.Clear();
        paginationProviderTotal = null;
        paginationInexact = null;
    }

    private bool HasUnfetchedResultIds()
    {
        return successfulSearch is not null &&
            paginationResultIds.Any(resultId => !fetchedResultIds.Contains(resultId));
    }

    private bool CanFetchMore()
    {
        return HasUnfetchedResultIds() && displayedOffers.Count < visibleOfferCapacity;
    }

    private bool TryGetNextFetchIds(out IReadOnlyList<string> fetchIds)
    {
        fetchIds = [];
        if (!CanFetchMore())
        {
            return false;
        }

        var included = new HashSet<string>(fetchedResultIds, StringComparer.Ordinal);
        var nextIds = new List<string>();
        foreach (var resultId in paginationResultIds)
        {
            if (!included.Add(resultId))
            {
                continue;
            }

            nextIds.Add(resultId);
            if (nextIds.Count == Math.Min(
                    PathOfExileTradeEndpointBuilder.MaximumFetchResultIds,
                    visibleOfferCapacity - displayedOffers.Count))
            {
                break;
            }
        }

        fetchIds = nextIds;
        return nextIds.Count > 0;
    }

    private void AppendOffers(
        IReadOnlyList<PathOfExileTradeFetchedOffer> offers,
        IReadOnlyList<string> resultIds)
    {
        var offersById = offers
            .Where(offer => !string.IsNullOrWhiteSpace(offer.Id))
            .GroupBy(offer => offer.Id.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var resultId in resultIds.Distinct(StringComparer.Ordinal))
        {
            if (displayedOffers.Count >= visibleOfferCapacity)
            {
                break;
            }

            if (offersById.TryGetValue(resultId, out var offer) && displayedOfferIds.Add(resultId))
            {
                displayedOffers.Add(MapOffer(offer, DateTimeOffset.UtcNow));
            }
        }
    }

    private void TrimDisplayedOffersToCapacity()
    {
        while (displayedOffers.Count > visibleOfferCapacity)
        {
            var removed = displayedOffers[^1];
            displayedOffers.RemoveAt(displayedOffers.Count - 1);
            displayedOfferIds.Remove(removed.Id);
            fetchedResultIds.Remove(removed.Id);
        }
    }

    private static string CreateSuccessSummary(
        int fetchedOfferCount,
        int? providerTotal,
        bool? inexact)
    {
        var total = providerTotal ?? fetchedOfferCount;
        var summary = $"Showing {fetchedOfferCount} of {total} offers";
        return inexact == true ? $"{summary} (inexact)" : summary;
    }

    private static string FailureMessage(PathOfExileTradePriceCheckResult result)
    {
        if (result.Stage == PathOfExileTradePriceCheckStage.QueryBuild)
        {
            return "Select a supported Trade search.";
        }

        if (result.Stage == PathOfExileTradePriceCheckStage.CatalogLoad)
        {
            return "Could not load Trade modifier definitions.";
        }

        if (result.Stage == PathOfExileTradePriceCheckStage.ModifierMapping)
        {
            return ModifierMappingFailureMessage(result);
        }

        var diagnostic = result.Diagnostics.FirstOrDefault();
        if (diagnostic?.SourceCode == PathOfExileTradeHttpDiagnosticCodes.ProviderDeclaredError)
        {
            return $"Trade returned an error: {SafeMessage(diagnostic.Message)}";
        }

        if (result.IsTimeout)
        {
            return "Trade request failed. Try again later.";
        }

        if (!string.IsNullOrWhiteSpace(diagnostic?.ProviderCode))
        {
            return $"Trade returned an error: {SafeMessage(diagnostic.Message)}";
        }

        return "Trade request failed. Try again later.";
    }

    private static string LocalValidationMessage(TradeSearchValidationResult validationResult)
    {
        if (validationResult.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == TradeSearchValidationSeverity.Error &&
                diagnostic.Code is TradeSearchValidationDiagnosticCodes.SelectedModifierMissingText))
        {
            return "Selected modifier is not available in Trade search.";
        }

        return "Select a supported Trade search.";
    }

    private static string ModifierMappingFailureMessage(PathOfExileTradePriceCheckResult result)
    {
        var sourceCode = result.Diagnostics.FirstOrDefault(diagnostic =>
                diagnostic.Code == PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed)
            ?.SourceCode;

        return sourceCode switch
        {
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous =>
                "Selected modifier matches multiple Trade filters.",
            _ => "Selected modifier is not available in Trade search.",
        };
    }

    private static PriceCheckerOfferViewModel MapOffer(
        PathOfExileTradeFetchedOffer offer,
        DateTimeOffset now)
    {
        return new PriceCheckerOfferViewModel
        {
            Id = offer.Id.Trim(),
            ItemName = ItemName(offer.Item),
            SellerAccountName = SellerAccountName(offer.Listing.Account),
            ListedText = PriceCheckerRelativeTimeFormatter.Format(offer.Listing.Indexed, now),
            ListedToolTip = ListedToolTip(offer.Listing),
            ItemLevelText = offer.Item.ItemLevel?.ToString(CultureInfo.InvariantCulture) ??
                PriceCheckerRelativeTimeFormatter.UnavailableText,
            PriceText = FormatPrice(offer.Listing.Price),
        };
    }

    private IReadOnlyList<PriceCheckerItemPropertyViewModel> CreateItemPropertyRows(
        TradeSearchDraft? draft = null)
    {
        draft ??= currentDraft;
        if (draft is null)
        {
            return [];
        }

        return draft.ItemProperties
            .Select((property, index) =>
            {
                var childIndexes = ItemPropertyModifierIndexes(draft, index);
                var isExpanded = expandedItemPropertyIndexes.Contains(index) && childIndexes.Count > 0;
                var children = childIndexes.Select(childIndex => CreateModifierRow(
                    draft,
                    childIndex,
                    showsExpansionControl: false,
                    sectionLabelOverride: SharedWithLabel(draft, property.Kind, childIndex))).ToArray();
                return new PriceCheckerItemPropertyViewModel
                {
                    SourceIndex = index,
                    Kind = property.Kind,
                    Label = property.Label,
                    CalculationBasisLabel = property.CalculationBasisLabel,
                    IsSelected = property.IsSelected,
                    IsAvailable = IsItemPropertyAvailable(property),
                    AvailabilityReason = ItemPropertyAvailabilityReason(property),
                    MinimumText = ItemPropertyBoundText(index, property.RequestedMinimum, minimum: true),
                    MaximumText = ItemPropertyBoundText(index, property.RequestedMaximum, minimum: false),
                    IsExpanded = isExpanded,
                    Children = children,
                };
            })
            .ToArray();
    }

    private IReadOnlyList<PriceCheckerModifierViewModel> CreateModifierRows(TradeSearchDraft? draft = null)
    {
        draft ??= currentDraft;
        if (draft is null)
        {
            return [];
        }

        var groupedIndexes = GroupedModifierIndexes(draft);
        return draft.ModifierFilters
            .Select((_, index) => index)
            .Where(index => !groupedIndexes.Contains(index))
            .OrderBy(index => IsImplicitPresentationModifier(draft.ModifierFilters[index]) ? 0 : 1)
            .ThenBy(index => index)
            .Select(index => CreateModifierRow(draft, index, showsExpansionControl: true))
            .ToArray();
    }

    private static bool IsImplicitPresentationModifier(ResolvedSearchComponent component) =>
        component.IsBaseImplicit ||
        component.ParsedKind == ParsedModifierKind.Implicit ||
        component.GenerationType == ModifierGenerationType.Implicit;

    private PriceCheckerModifierViewModel CreateModifierRow(
        TradeSearchDraft draft,
        int index,
        bool showsExpansionControl,
        string? sectionLabelOverride = null)
    {
        var modifier = draft.ModifierFilters[index];
        var isUniqueModifier = modifier.ParsedKind == ParsedModifierKind.Unique;
        var isFoulbornUniqueModifier =
            modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn;
        var isFracturedModifier = modifier.IsFractured;
        var isVeiledModifier = modifier.IsVeiled;
        var requiresExactAvailability =
            isUniqueModifier || isFracturedModifier || isVeiledModifier;
        var isInteractionEnabled = IsModifierInteractionReady(modifier);
        var availabilityReason = isInteractionEnabled
            ? null
            : modifier.ProviderDiagnosticMessage ?? modifier.NotSearchableReason ??
                $"Unsupported {StaticModifierLabel(modifier)} modifier: no exact compatible Trade stat identity is available.";
        var exposesValueBounds = isInteractionEnabled;
        var variants = CreateVariantViewModels(modifier.FilterVariants);
        var contributorsEnabled = isInteractionEnabled &&
            SearchComponentContributorActivation.SupportsComposition(modifier);
        var contributors = modifier.Contributors
            .Select((contributor, contributorIndex) =>
            {
                var inactiveReason = SearchComponentContributorActivation.GetInactiveReason(
                    modifier,
                    contributor);
                return new PriceCheckerModifierContributorViewModel
                {
                    ParentSourceIndex = index,
                    ContributorIndex = contributorIndex,
                    Text = SafeModifierText(contributor.DisplayText),
                    ProvenanceLabel = ContributorProvenanceLabel(contributor.Source),
                    SourceBreakdown = SourceDetail(contributor.Source, contributorIndex),
                    IsSelected = contributor.IsSelected,
                    SupportsValueBounds = contributor.SupportsValueBounds,
                    ValueBoundsUnsupportedReason = contributor.ValueBoundsUnsupportedReason,
                    IsInteractionEnabled = contributorsEnabled,
                    InactiveReason = inactiveReason,
                    MinimumText = ContributorBoundText(
                        index,
                        contributorIndex,
                        contributor.RequestedMinimum,
                        minimum: true),
                    MaximumText = ContributorBoundText(
                        index,
                        contributorIndex,
                        contributor.RequestedMaximum,
                        minimum: false),
                };
            })
            .ToArray();
        return new PriceCheckerModifierViewModel
        {
            SourceIndex = index,
            Text = SafeModifierText(modifier.OriginalText),
            SectionLabel = FormatModifierSectionLabel(
                sectionLabelOverride ?? SectionLabelWithSources(modifier),
                requiresExactAvailability,
                isInteractionEnabled),
            SourceCount = modifier.SourceCount,
            SourceBreakdown = CombineSourceBreakdown(SourceBreakdown(modifier), availabilityReason),
            IsSelected = modifier.IsSelected,
            IsInteractionEnabled = isInteractionEnabled,
            AvailabilityReason = availabilityReason,
            SupportsValueBounds = exposesValueBounds && modifier.SupportsValueBounds,
            ValueBoundsUnsupportedReason = modifier.ValueBoundsUnsupportedReason,
            FilterVariants = variants,
            SelectedFilterVariant = variants.FirstOrDefault(option => string.Equals(
                option.Identity,
                modifier.SelectedFilterVariantIdentity,
                StringComparison.Ordinal)),
            IsCanonicalImplicit = IsImplicitPresentationModifier(modifier),
            IsUniqueModifier = isUniqueModifier,
            IsFoulbornUniqueModifier = isFoulbornUniqueModifier,
            IsFracturedModifier = isFracturedModifier,
            IsVeiledModifier = isVeiledModifier,
            MinimumText = exposesValueBounds
                ? ModifierBoundText(index, modifier.RequestedMinimum, minimum: true)
                : string.Empty,
            MaximumText = exposesValueBounds
                ? ModifierBoundText(index, modifier.RequestedMaximum, minimum: false)
                : string.Empty,
            Contributors = contributors,
            ShowsExpansionControl = showsExpansionControl && contributors.Length > 0,
            IsExpanded = showsExpansionControl &&
                contributors.Length > 0 &&
                expandedModifierIndexes.Contains(index),
            ActiveContributorCount = SearchComponentContributorActivation.ActiveSelectionCount(modifier),
        };
    }

    private static bool IsExactlySearchableRestrictedModifier(ResolvedSearchComponent modifier)
    {
        if (modifier.IsVeiled)
        {
            var selectedVariant = modifier.FilterVariants.FirstOrDefault(variant => string.Equals(
                variant.Identity,
                modifier.SelectedFilterVariantIdentity,
                StringComparison.Ordinal));
            if (selectedVariant is null)
            {
                return false;
            }

            return modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
                modifier.IsSearchable &&
                modifier.StatMappingProof == ModifierStatMappingProofStatus.ProviderExact &&
                !string.IsNullOrWhiteSpace(modifier.ProviderStatId) &&
                string.Equals(selectedVariant.ProviderKind, "veiled", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    selectedVariant.Identity,
                    PathOfExileTradeProviderIdentity.Create(modifier.ProviderStatId),
                    StringComparison.Ordinal);
        }

        if (modifier.IsFractured)
        {
            var selectedVariant = modifier.FilterVariants.FirstOrDefault(variant => string.Equals(
                variant.Identity,
                modifier.SelectedFilterVariantIdentity,
                StringComparison.Ordinal));
            return modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
                modifier.IsSearchable &&
                modifier.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
                !string.IsNullOrWhiteSpace(modifier.ResolvedModifierId) &&
                modifier.ResolvedStatIds.Count > 0 &&
                !string.IsNullOrWhiteSpace(modifier.ProviderStatId) &&
                selectedVariant is not null &&
                string.Equals(
                    selectedVariant.Identity,
                    PathOfExileTradeProviderIdentity.Create(modifier.ProviderStatId),
                    StringComparison.Ordinal);
        }

        var hasExactGameDataProof =
            modifier.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
            !string.IsNullOrWhiteSpace(modifier.ResolvedModifierId) &&
            modifier.ResolvedStatIds.Count > 0;
        return modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
            modifier.IsSearchable &&
            !string.IsNullOrWhiteSpace(modifier.ProviderStatId) &&
            (modifier.StatMappingProof == ModifierStatMappingProofStatus.ProviderExact ||
                hasExactGameDataProof);
    }

    private static bool IsModifierInteractionReady(ResolvedSearchComponent modifier)
    {
        if (modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.BaseGuaranteed)
        {
            return true;
        }

        var selectedVariant = modifier.FilterVariants.FirstOrDefault(variant => string.Equals(
            variant.Identity,
            modifier.SelectedFilterVariantIdentity,
            StringComparison.Ordinal));
        if (selectedVariant is null)
        {
            return false;
        }

        var providerReady = modifier.ProviderResolutionStatus ==
                SearchComponentProviderResolutionStatus.Exact
            ? RequiresExactAvailability(modifier)
                ? IsExactlySearchableRestrictedModifier(modifier)
                : modifier.IsSearchable && !string.IsNullOrWhiteSpace(modifier.ProviderStatId)
            : modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.NotResolved &&
                modifier.IsSearchable &&
                (modifier.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
                    !string.IsNullOrWhiteSpace(modifier.ResolvedModifierId) &&
                    modifier.ResolvedStatIds.Count > 0 ||
                    modifier.FilterVariants.Count > 0);
        if (!providerReady)
        {
            return false;
        }

        return !selectedVariant.SupportsValueBounds ||
            modifier.SupportsValueBounds &&
            modifier.CanonicalNumericValues.Count > 0;
    }

    private static bool RequiresExactAvailability(ResolvedSearchComponent modifier)
    {
        return modifier.ParsedKind == ParsedModifierKind.Unique || modifier.IsFractured || modifier.IsVeiled;
    }

    private static bool HasFixedProviderVariant(ResolvedSearchComponent modifier)
    {
        return modifier.ParsedKind == ParsedModifierKind.Unique ||
            modifier.IsVeiled ||
            IsImplicitPresentationModifier(modifier);
    }

    private static string StaticModifierLabel(ResolvedSearchComponent modifier)
    {
        if (modifier.IsFractured)
        {
            return "Fractured";
        }

        if (modifier.IsVeiled)
        {
            return "Veiled";
        }

        if (modifier.ParsedKind == ParsedModifierKind.Unique)
        {
            return modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn ? "Foulborn" : "Unique";
        }

        return modifier.ParsedKind switch
        {
            ParsedModifierKind.Prefix => "Prefix",
            ParsedModifierKind.Suffix => "Suffix",
            ParsedModifierKind.Implicit => "Implicit",
            _ => "modifier",
        };
    }

    private static string FormatModifierSectionLabel(
        string sectionLabel,
        bool requiresExactStaticAvailability,
        bool isInteractionEnabled)
    {
        return requiresExactStaticAvailability && !isInteractionEnabled
            ? $"{sectionLabel} · Unsupported"
            : sectionLabel;
    }

    private static string? CombineSourceBreakdown(string? sourceBreakdown, string? availabilityReason)
    {
        if (string.IsNullOrWhiteSpace(availabilityReason))
        {
            return sourceBreakdown;
        }

        return string.IsNullOrWhiteSpace(sourceBreakdown)
            ? availabilityReason
            : $"{availabilityReason}{Environment.NewLine}{sourceBreakdown}";
    }

    private static IReadOnlySet<int> GroupedModifierIndexes(TradeSearchDraft draft)
    {
        var visibleKinds = draft.ItemProperties.Select(property => property.Kind).ToHashSet();
        return draft.ItemPropertyContributionGroups
            .Where(group => visibleKinds.Contains(group.ParentKind))
            .SelectMany(group => group.Contributions)
            .Select(contribution => contribution.ModifierFilterIndex)
            .Where(index => index >= 0 && index < draft.ModifierFilters.Count)
            .ToHashSet();
    }

    private static IReadOnlyList<int> ItemPropertyModifierIndexes(
        TradeSearchDraft draft,
        int itemPropertyIndex)
    {
        if (itemPropertyIndex < 0 || itemPropertyIndex >= draft.ItemProperties.Length)
        {
            return [];
        }

        var kind = draft.ItemProperties[itemPropertyIndex].Kind;
        var group = draft.ItemPropertyContributionGroups.FirstOrDefault(candidate =>
            candidate.ParentKind == kind);
        return group?.Contributions
            .Select(contribution => contribution.ModifierFilterIndex)
            .Where(index => index >= 0 && index < draft.ModifierFilters.Count)
            .Distinct()
            .ToArray() ?? [];
    }

    private static string? SharedWithLabel(
        TradeSearchDraft draft,
        TradeSearchItemPropertyKind currentParent,
        int modifierIndex)
    {
        var otherLabels = draft.ItemPropertyContributionGroups
            .Where(group => group.ParentKind != currentParent && group.Contributions.Any(contribution =>
                contribution.ModifierFilterIndex == modifierIndex))
            .Select(group => draft.ItemProperties.FirstOrDefault(property => property.Kind == group.ParentKind)?.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return otherLabels.Length == 0 ? null : $"Shared with {string.Join(", ", otherLabels)}";
    }

    private static bool IsItemPropertyAvailable(TradeSearchItemProperty property)
    {
        return property.ProviderResolutionStatus == TradeSearchItemPropertyProviderResolutionStatus.Exact &&
            property.IsSearchable;
    }

    private static string? ItemPropertyAvailabilityReason(TradeSearchItemProperty property)
    {
        if (IsItemPropertyAvailable(property))
        {
            return null;
        }

        return property.Kind == TradeSearchItemPropertyKind.ChaosDps &&
            property.ProviderResolutionStatus == TradeSearchItemPropertyProviderResolutionStatus.Unsupported
                ? "Path of Exile Trade does not expose a Chaos DPS filter."
                : property.NotSearchableReason ?? "No exact Trade filter is currently available.";
    }

    private static IReadOnlyList<PriceCheckerModifierFilterVariantViewModel> CreateVariantViewModels(
        IReadOnlyList<SearchFilterVariant> variants)
    {
        return variants
            .Select(option => new PriceCheckerModifierFilterVariantViewModel
            {
                Identity = option.Identity,
                Label = option.Label,
                Description = option.Description,
                SupportsValueBounds = option.SupportsValueBounds,
            })
            .ToArray();
    }

    private void InitializeItemPropertyBoundInputs(TradeSearchDraft draft)
    {
        itemPropertyBoundInputs.Clear();
        for (var index = 0; index < draft.ItemProperties.Length; index++)
        {
            var property = draft.ItemProperties[index];
            itemPropertyBoundInputs[index] = new ModifierBoundInput(
                FormatBound(property.RequestedMinimum),
                FormatBound(property.RequestedMaximum));
        }
    }

    private void InitializeModifierBoundInputs(TradeSearchDraft draft)
    {
        modifierBoundInputs.Clear();
        contributorBoundInputs.Clear();
        canonicalContributorParentMinimums.Clear();
        manualContributorParentMinimums.Clear();
        parentMinimumStates.Clear();
        for (var index = 0; index < draft.ModifierFilters.Count; index++)
        {
            var modifier = draft.ModifierFilters[index];
            modifierBoundInputs[index] = new ModifierBoundInput(
                FormatBound(modifier.RequestedMinimum),
                FormatBound(modifier.RequestedMaximum));
            if (modifier.Contributors.Count > 0 &&
                modifier.ContributorProjection == SearchComponentContributorProjection.Additive)
            {
                canonicalContributorParentMinimums[index] = modifier.CanonicalNumericValues.Count == 1
                    ? modifier.CanonicalNumericValues[0]
                    : modifier.RequestedMinimum;
                parentMinimumStates[index] = PriceCheckerParentMinimumState.CanonicalDefault;
            }
            for (var contributorIndex = 0; contributorIndex < modifier.Contributors.Count; contributorIndex++)
            {
                var contributor = modifier.Contributors[contributorIndex];
                contributorBoundInputs[new ModifierContributorKey(index, contributorIndex)] =
                    new ModifierBoundInput(
                        FormatBound(contributor.RequestedMinimum),
                        FormatBound(contributor.RequestedMaximum));
            }
        }
    }

    private string ItemPropertyBoundText(int index, decimal? value, bool minimum)
    {
        return itemPropertyBoundInputs.TryGetValue(index, out var input)
            ? (minimum ? input.MinimumText : input.MaximumText)
            : FormatBound(value);
    }

    private string ModifierBoundText(int index, decimal? value, bool minimum)
    {
        return modifierBoundInputs.TryGetValue(index, out var input)
            ? (minimum ? input.MinimumText : input.MaximumText)
            : FormatBound(value);
    }

    private string ContributorBoundText(
        int modifierIndex,
        int contributorIndex,
        decimal? value,
        bool minimum)
    {
        return contributorBoundInputs.TryGetValue(
                new ModifierContributorKey(modifierIndex, contributorIndex),
                out var input)
            ? (minimum ? input.MinimumText : input.MaximumText)
            : FormatBound(value);
    }

    private bool TryGetContributor(
        int modifierIndex,
        int contributorIndex,
        out ResolvedSearchComponent parent,
        out SearchComponentContributor contributor)
    {
        parent = null!;
        contributor = null!;
        if (currentDraft is null ||
            modifierIndex < 0 ||
            modifierIndex >= currentDraft.ModifierFilters.Count)
        {
            return false;
        }

        parent = currentDraft.ModifierFilters[modifierIndex];
        if (contributorIndex < 0 || contributorIndex >= parent.Contributors.Count)
        {
            return false;
        }

        contributor = parent.Contributors[contributorIndex];
        return true;
    }

    private ResolvedSearchComponent SynchronizeContributorParentMinimum(
        int modifierIndex,
        ResolvedSearchComponent parent)
    {
        if (parent.Contributors.Count == 0 ||
            parent.ContributorProjection != SearchComponentContributorProjection.Additive ||
            !canonicalContributorParentMinimums.TryGetValue(modifierIndex, out var canonicalMinimum))
        {
            return parent;
        }

        var state = parentMinimumStates.GetValueOrDefault(
            modifierIndex,
            PriceCheckerParentMinimumState.CanonicalDefault);
        decimal? requiredMinimum;
        if (state == PriceCheckerParentMinimumState.ManualOverride &&
            manualContributorParentMinimums.TryGetValue(modifierIndex, out var manualMinimum))
        {
            requiredMinimum = manualMinimum;
        }
        else
        {
            requiredMinimum = canonicalMinimum;
            if (state == PriceCheckerParentMinimumState.ChildDerived &&
                parent.Contributors.Any(contributor => contributor.IsSelected))
            {
                if (!SearchComponentContributorMath.TryGetSelectedAdditiveMinimumFloor(parent, out var childFloor))
                {
                    return parent;
                }

                requiredMinimum = childFloor;
            }
        }

        var input = modifierBoundInputs.GetValueOrDefault(modifierIndex) ??
            new ModifierBoundInput(
                FormatBound(parent.RequestedMinimum),
                FormatBound(parent.RequestedMaximum));
        var minimumText = FormatBound(requiredMinimum);
        modifierBoundInputs[modifierIndex] = input with { MinimumText = minimumText };
        var displayed = FindDisplayedModifierRow(modifierIndex);
        if (displayed is not null)
        {
            displayed.MinimumText = minimumText;
        }

        return parent with { RequestedMinimum = requiredMinimum };
    }

    private static TradeSearchDraft ReplaceModifier(
        TradeSearchDraft draft,
        int modifierIndex,
        ResolvedSearchComponent modifier)
    {
        return draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select((candidate, index) => index == modifierIndex ? modifier : candidate)
                .ToArray(),
        };
    }

    private void RefreshDisplayedContributorActivity(
        int modifierIndex,
        ResolvedSearchComponent parent)
    {
        var row = FindDisplayedModifierRow(modifierIndex);
        if (row is null)
        {
            return;
        }

        foreach (var (contributor, contributorIndex) in parent.Contributors.Select(
                     (contributor, index) => (contributor, index)))
        {
            var displayed = row.Contributors.ElementAtOrDefault(contributorIndex);
            if (displayed is not null)
            {
                displayed.InactiveReason = SearchComponentContributorActivation.GetInactiveReason(
                    parent,
                    contributor);
            }
        }
    }

    private PriceCheckerModifierViewModel? FindDisplayedModifierRow(int modifierIndex)
    {
        return CurrentViewState.Modifiers.FirstOrDefault(row => row.SourceIndex == modifierIndex) ??
            CurrentViewState.ItemProperties
                .SelectMany(property => property.Children)
                .FirstOrDefault(row => row.SourceIndex == modifierIndex);
    }

    private static BoundParseResult ParseBound(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return BoundParseResult.Empty;
        }

        if (trimmed.Contains(' ') || trimmed.Contains("'", StringComparison.Ordinal) ||
            trimmed.Count(character => character is '.' or ',') > 1 ||
            !decimal.TryParse(trimmed.Replace(',', '.'), System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return BoundParseResult.Invalid;
        }

        return new BoundParseResult(true, value);
    }

    private static bool HasInvalidBoundInput(ModifierBoundInput input) =>
        !ParseBound(input.MinimumText).IsValid || !ParseBound(input.MaximumText).IsValid;

    private static bool HasInvalidItemPropertyBoundInput(ModifierBoundInput input)
    {
        return !ParseBound(input.MinimumText).IsValid || !ParseBound(input.MaximumText).IsValid;
    }

    private static string FormatBound(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture) ?? string.Empty;

    private sealed record ModifierBoundInput(string MinimumText, string MaximumText);

    private readonly record struct ModifierContributorKey(int ModifierIndex, int ContributorIndex);

    private PriceCheckerItemResetSnapshot CaptureInitialItemSnapshot()
    {
        return new PriceCheckerItemResetSnapshot(
            currentDraft!,
            currentValidationResult!,
            currentPresentation,
            userSelectedBaseCriterion,
            new Dictionary<int, ModifierBoundInput>(itemPropertyBoundInputs),
            new Dictionary<int, ModifierBoundInput>(modifierBoundInputs),
            new Dictionary<ModifierContributorKey, ModifierBoundInput>(contributorBoundInputs),
            new Dictionary<int, decimal?>(canonicalContributorParentMinimums),
            new Dictionary<int, decimal?>(manualContributorParentMinimums),
            new Dictionary<int, PriceCheckerParentMinimumState>(parentMinimumStates));
    }

    private static void RestoreDictionary<TKey, TValue>(
        IDictionary<TKey, TValue> destination,
        IReadOnlyDictionary<TKey, TValue> source)
        where TKey : notnull
    {
        destination.Clear();
        foreach (var (key, value) in source)
        {
            destination[key] = value;
        }
    }

    private sealed record PriceCheckerItemResetSnapshot(
        TradeSearchDraft Draft,
        TradeSearchValidationResult ValidationResult,
        PriceCheckerItemPresentation Presentation,
        BaseSearchCriterion? UserSelectedBaseCriterion,
        IReadOnlyDictionary<int, ModifierBoundInput> ItemPropertyBoundInputs,
        IReadOnlyDictionary<int, ModifierBoundInput> ModifierBoundInputs,
        IReadOnlyDictionary<ModifierContributorKey, ModifierBoundInput> ContributorBoundInputs,
        IReadOnlyDictionary<int, decimal?> CanonicalParentMinimums,
        IReadOnlyDictionary<int, decimal?> ManualParentMinimums,
        IReadOnlyDictionary<int, PriceCheckerParentMinimumState> ParentMinimumStates);

    private readonly record struct BoundParseResult(bool IsValid, decimal? Value = null)
    {
        public static BoundParseResult Empty => new(true, null);
        public static BoundParseResult Invalid => new(false, null);
    }

    private static TradeSearchDraft ResetModifierSelections(TradeSearchDraft draft)
    {
        if (!draft.ModifierFilters.Any(modifier =>
                modifier.IsSelected || modifier.Contributors.Any(contributor => contributor.IsSelected)))
        {
            return draft;
        }

        return draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(modifier => modifier with
                {
                    IsSelected = false,
                    Contributors = modifier.Contributors
                        .Select(contributor => contributor with { IsSelected = false })
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    private static string SectionLabel(ResolvedSearchComponent modifier)
    {
        if (modifier.IsFractured)
        {
            return "Fractured";
        }

        if (modifier.IsCrafted)
        {
            return "Crafted";
        }

        if (modifier.IsVeiled)
        {
            return "Veiled";
        }

        return modifier.ParsedKind switch
        {
            ParsedModifierKind.Implicit => "Implicit",
            ParsedModifierKind.Prefix => "Prefix",
            ParsedModifierKind.Suffix => "Suffix",
            ParsedModifierKind.Unique when modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn =>
                "Foulborn",
            ParsedModifierKind.Unique => "Unique",
            _ => string.Empty,
        };
    }

    private static string SectionLabelWithSources(ResolvedSearchComponent modifier)
    {
        var section = SectionLabel(modifier);
        var selected = SearchComponentContributorActivation.ActiveSelectionCount(modifier);
        var label = modifier.SourceCount > 1
            ? string.IsNullOrWhiteSpace(section)
                ? $"{modifier.SourceCount} sources"
                : $"{section} · {modifier.SourceCount} sources"
            : section;
        return selected > 0 ? $"{label} · {selected} selected" : label;
    }

    private static string ContributorProvenanceLabel(SearchComponentSourceProvenance source)
    {
        var section = source.ParsedKind switch
        {
            ParsedModifierKind.Prefix => "Prefix",
            ParsedModifierKind.Suffix => "Suffix",
            ParsedModifierKind.Implicit => "Implicit",
            ParsedModifierKind.Unique when source.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn =>
                "Foulborn",
            ParsedModifierKind.Unique => "Unique",
            _ => string.Empty,
        };
        if (source.IsFractured)
        {
            section = "Fractured";
        }
        else if (source.IsVeiled)
        {
            section = "Veiled";
        }
        if (source.IsCrafted)
        {
            return string.IsNullOrWhiteSpace(section) ? "Crafted" : $"Crafted {section}";
        }

        if (source.IsHybrid)
        {
            return string.IsNullOrWhiteSpace(section) ? "Hybrid" : $"Hybrid {section}";
        }

        return string.IsNullOrWhiteSpace(section)
            ? source.ProviderDomain
            : string.Equals(source.ProviderDomain, section, StringComparison.OrdinalIgnoreCase)
                ? section
                : $"{source.ProviderDomain} {section}";
    }

    private static string? SourceBreakdown(ResolvedSearchComponent modifier)
    {
        if (modifier.SourceCount <= 1)
        {
            return null;
        }

        return string.Join(Environment.NewLine, modifier.Sources.Select((source, index) =>
        {
            var observed = string.Join(", ", source.ObservedNumericValues.Select(value => FormatBound(value)));
            var canonical = string.Join(", ", source.CanonicalNumericValues.Select(value => FormatBound(value)));
            var identity = TrimToNull(source.ResolvedModifierId) ?? source.ComponentId;
            var translation = TrimToNull(source.TranslationIdentity) ?? "none";
            var transforms = string.Join(" | ", source.TranslationHandlers
                .Select(handlers => handlers.Count == 0 ? "identity" : string.Join(" → ", handlers)));
            return $"{index + 1}. {SafeModifierText(source.OriginalText)} [{source.ProviderDomain}; {identity}; observed {observed}; canonical {canonical}; translation {translation}; transforms {transforms}; provider {source.ProviderResolutionStatus}]";
        }));
    }

    private static string SafeModifierText(string? text)
    {
        var safe = new string(
            (text ?? string.Empty)
            .ReplaceLineEndings(" ")
            .Where(character => !char.IsControl(character))
            .ToArray());
        safe = string.Join(' ', safe.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safe))
        {
            return "Unknown modifier";
        }

        return safe.Length <= MaximumModifierTextLength
            ? safe
            : $"{safe[..MaximumModifierTextLength]}...";
    }

    private static string SourceDetail(SearchComponentSourceProvenance source, int index)
    {
        var observed = string.Join(", ", source.ObservedNumericValues.Select(value => FormatBound(value)));
        var canonical = string.Join(", ", source.CanonicalNumericValues.Select(value => FormatBound(value)));
        var identity = TrimToNull(source.ResolvedModifierId) ?? source.ComponentId;
        var translation = TrimToNull(source.TranslationIdentity) ?? "none";
        var transforms = string.Join(" | ", source.TranslationHandlers
            .Select(handlers => handlers.Count == 0 ? "identity" : string.Join(" → ", handlers)));
        return $"{index + 1}. {SafeModifierText(source.OriginalText)} [{source.ProviderDomain}; {identity}; observed {observed}; canonical {canonical}; translation {translation}; transforms {transforms}; provider {source.ProviderResolutionStatus}]";
    }

    private static string FormatPrice(PathOfExileTradeListingPrice? price)
    {
        if (price?.Amount is null || string.IsNullOrWhiteSpace(price.Currency))
        {
            return PriceCheckerRelativeTimeFormatter.UnavailableText;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{price.Amount.Value:G29} {price.Currency.Trim()}");
    }

    private static string SellerAccountName(PathOfExileTradeListingAccount? account)
    {
        return TrimToNull(account?.Name) ?? PriceCheckerRelativeTimeFormatter.UnavailableText;
    }

    private static string ItemName(PathOfExileTradeFetchedItem item)
    {
        return TrimToNull(item.Name) ??
            TrimToNull(item.TypeLine) ??
            TrimToNull(item.BaseType) ??
            PriceCheckerRelativeTimeFormatter.UnavailableText;
    }

    private static string? ListedToolTip(PathOfExileTradeListing listing)
    {
        return TrimToNull(listing.RawIndexed) ??
            listing.Indexed?.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string SafeMessage(string? message)
    {
        var safe = new string(
            (message ?? string.Empty)
            .ReplaceLineEndings(" ")
            .Where(character => !char.IsControl(character))
            .ToArray());
        safe = string.Join(' ', safe.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "Trade request failed. Try again later.";
        }

        return safe.Length <= MaximumSafeProviderMessageLength
            ? safe
            : $"{safe[..MaximumSafeProviderMessageLength]}...";
    }

    private static string? TrimLeague(string? value)
    {
        return TrimToNull(value);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private void ApplyState(PriceCheckerSearchViewState state)
    {
        CurrentViewState = state;
        window?.UpdateSearch(state);
        UpdateDeveloperDiagnostics(state);
    }

    private void UpdateDeveloperDiagnostics(PriceCheckerSearchViewState state)
    {
        var diagnostics = currentValidationResult?.Diagnostics
            .Select(diagnostic => new PriceCheckerDeveloperDiagnostic(
                diagnostic.Code,
                diagnostic.Message))
            .ToList() ?? [];
        if (currentDraft is not null)
        {
            diagnostics.AddRange(currentDraft.ModifierAggregationDiagnostics
                .Select(diagnostic => new PriceCheckerDeveloperDiagnostic(
                    diagnostic.Code,
                    diagnostic.Message)));
            diagnostics.AddRange(currentDraft.ModifierFilters
                .Where(modifier => !string.IsNullOrWhiteSpace(modifier.ProviderDiagnosticMessage))
                .Select(modifier => new PriceCheckerDeveloperDiagnostic(
                    modifier.ProviderDiagnosticCode ?? "AGGREGATE_PROVIDER_COVERAGE",
                    modifier.ProviderDiagnosticMessage!)));
            diagnostics.AddRange(currentDraft.ModifierFilters
                .SelectMany(modifier => modifier.Contributors)
                .Where(contributor => !string.IsNullOrWhiteSpace(contributor.ProviderDiagnosticMessage))
                .Select(contributor => new PriceCheckerDeveloperDiagnostic(
                    contributor.ProviderDiagnosticCode ?? "CONTRIBUTOR_PROVIDER_COVERAGE",
                    contributor.ProviderDiagnosticMessage!)));
            diagnostics.AddRange(currentDraft.ModifierFilters
                .Where(modifier => modifier.IsSelected &&
                    !modifier.SupportsValueBounds &&
                    modifier.ValueBoundShape != ModifierBoundShape.PresenceOnly)
                .Select(modifier => new PriceCheckerDeveloperDiagnostic(
                    "MODIFIER_BOUNDS_UNSUPPORTED",
                    modifier.ValueBoundsUnsupportedReason ??
                        "The selected modifier does not expose a faithful scalar value bound.")));
        }

        CurrentDeveloperDiagnostics = new PriceCheckerDeveloperDiagnosticsSnapshot(
            state.Status.ToString(),
            diagnostics);
        DeveloperDiagnosticsChanged?.Invoke(this, CurrentDeveloperDiagnostics);
    }
}
