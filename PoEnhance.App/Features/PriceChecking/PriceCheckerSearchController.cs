using System.Globalization;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

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
    private readonly Dictionary<int, ModifierBoundInput> modifierBoundInputs = [];
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
        priceCheckerWindow.ModifierSelectionChanged += OnModifierSelectionChanged;
        priceCheckerWindow.ModifierBoundsChanged += OnModifierBoundsChanged;
        priceCheckerWindow.BaseCriterionToggleRequested += OnBaseCriterionToggleRequested;
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
        priceCheckerWindow.ModifierSelectionChanged -= OnModifierSelectionChanged;
        priceCheckerWindow.ModifierBoundsChanged -= OnModifierBoundsChanged;
        priceCheckerWindow.BaseCriterionToggleRequested -= OnBaseCriterionToggleRequested;
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
        currentDraft = ResetModifierSelections(draft);
        InitializeModifierBoundInputs(currentDraft);
        userSelectedBaseCriterion = currentDraft.Base.ActiveCriterion;
        currentPresentation = presentation ?? new PriceCheckerItemPresentation();
        currentValidationResult = ReferenceEquals(currentDraft, draft)
            ? validationResult
            : draftValidator.Validate(currentDraft);
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

        generation++;
        CancelActiveRequest();
        ClearPaginationState();

        var modifierFilters = currentDraft.ModifierFilters
            .Select((modifier, index) => index == modifierIndex
                ? modifier with { IsSelected = isSelected }
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
        currentValidationResult = draftValidator.Validate(currentDraft);

        PublishCurrentContent();
        ApplyState(CreateIdleOrValidationState());
    }

    public void UpdateModifierBounds(int modifierIndex, string? minimumText, string? maximumText)
    {
        if (currentDraft is null || modifierIndex < 0 || modifierIndex >= currentDraft.ModifierFilters.Count ||
            !currentDraft.ModifierFilters[modifierIndex].SupportsValueBounds)
        {
            return;
        }

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
        ApplyState(CreateBoundChangeInvalidatedState());
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

    private void OnModifierSelectionChanged(
        object? sender,
        PriceCheckerModifierSelectionChangedEventArgs e)
    {
        UpdateModifierSelection(e.ModifierIndex, e.IsSelected);
    }

    private void OnModifierBoundsChanged(object? sender, PriceCheckerModifierBoundsChangedEventArgs e)
    {
        UpdateModifierBounds(e.ModifierIndex, e.MinimumText, e.MaximumText);
    }

    private void OnBaseCriterionToggleRequested(object? sender, EventArgs e)
    {
        ToggleBaseCriterion();
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
            Modifiers = CreateModifierRows(),
        };
    }

    private PriceCheckerSearchViewState CreateBoundChangeInvalidatedState()
    {
        return CurrentViewState with
        {
            Status = PriceCheckerSearchViewStatus.Idle,
            CanSearch = !isLoading,
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

        if (modifierBoundInputs
            .Where(pair => currentDraft.ModifierFilters.ElementAtOrDefault(pair.Key)?.IsSelected == true)
            .Any(pair => HasInvalidBoundInput(pair.Value)))
        {
            return ValidationState("Modifier Min and Max must be finite decimal numbers.");
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

    private IReadOnlyList<PriceCheckerModifierViewModel> CreateModifierRows(TradeSearchDraft? draft = null)
    {
        draft ??= currentDraft;
        if (draft is null)
        {
            return [];
        }

        return draft.ModifierFilters
            .Select((modifier, index) => new PriceCheckerModifierViewModel
            {
                SourceIndex = index,
                Text = SafeModifierText(modifier.OriginalText),
                SectionLabel = SectionLabel(modifier),
                IsSelected = modifier.IsSelected,
                SupportsValueBounds = modifier.SupportsValueBounds,
                ValueBoundsUnsupportedReason = modifier.ValueBoundsUnsupportedReason,
                MinimumText = ModifierBoundText(index, modifier.RequestedMinimum, minimum: true),
                MaximumText = ModifierBoundText(index, modifier.RequestedMaximum, minimum: false),
            })
            .ToArray();
    }

    private void InitializeModifierBoundInputs(TradeSearchDraft draft)
    {
        modifierBoundInputs.Clear();
        for (var index = 0; index < draft.ModifierFilters.Count; index++)
        {
            var modifier = draft.ModifierFilters[index];
            modifierBoundInputs[index] = new ModifierBoundInput(
                FormatBound(modifier.RequestedMinimum),
                FormatBound(modifier.RequestedMaximum));
        }
    }

    private string ModifierBoundText(int index, decimal? value, bool minimum)
    {
        return modifierBoundInputs.TryGetValue(index, out var input)
            ? (minimum ? input.MinimumText : input.MaximumText)
            : FormatBound(value);
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

    private static string FormatBound(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture) ?? string.Empty;

    private sealed record ModifierBoundInput(string MinimumText, string MaximumText);

    private readonly record struct BoundParseResult(bool IsValid, decimal? Value = null)
    {
        public static BoundParseResult Empty => new(true, null);
        public static BoundParseResult Invalid => new(false, null);
    }

    private static TradeSearchDraft ResetModifierSelections(TradeSearchDraft draft)
    {
        if (!draft.ModifierFilters.Any(modifier => modifier.IsSelected))
        {
            return draft;
        }

        return draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(modifier => modifier with { IsSelected = false })
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
            ParsedModifierKind.Unique => "Unique",
            _ => string.Empty,
        };
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
            diagnostics.AddRange(currentDraft.ModifierFilters
                .Where(modifier => modifier.IsSelected && !modifier.SupportsValueBounds)
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
