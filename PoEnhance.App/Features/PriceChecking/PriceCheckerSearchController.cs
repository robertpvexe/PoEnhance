using System.Globalization;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using Serilog;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerSearchController
{
    public const string DefaultLeagueIdentifier = "Mirage";
    private const int MaximumSafeProviderMessageLength = 160;
    private const int MaximumModifierTextLength = 120;

    private readonly IPathOfExileTradePriceCheckService priceCheckService;
    private readonly ITradeSearchDraftValidator draftValidator;
    private readonly IPriceCheckerLeaguePreferenceStore leaguePreferenceStore;
    private IPriceCheckerWindow? window;
    private TradeSearchDraft? currentDraft;
    private TradeSearchValidationResult? currentValidationResult;
    private CancellationTokenSource? activeRequestCancellation;
    private readonly List<string> paginationResultIds = [];
    private readonly HashSet<string> fetchedResultIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> displayedOfferIds = new(StringComparer.Ordinal);
    private readonly List<PriceCheckerOfferViewModel> displayedOffers = [];
    private string? paginationSearchQueryId;
    private int? paginationProviderTotal;
    private bool? paginationInexact;
    private string leagueIdentifier;
    private int generation;
    private bool isLoading;
    private bool isLoadingMore;

    public PriceCheckerSearchController(
        IPathOfExileTradePriceCheckService priceCheckService,
        ITradeSearchDraftValidator? draftValidator = null,
        IPriceCheckerLeaguePreferenceStore? leaguePreferenceStore = null)
    {
        this.priceCheckService = priceCheckService ?? throw new ArgumentNullException(nameof(priceCheckService));
        this.draftValidator = draftValidator ?? new CoreTradeSearchDraftValidatorAdapter();
        this.leaguePreferenceStore = leaguePreferenceStore ?? NullPriceCheckerLeaguePreferenceStore.Instance;
        leagueIdentifier = LoadInitialLeagueIdentifier(this.leaguePreferenceStore);
        CurrentViewState = CreateIdleOrValidationState();
    }

    public PriceCheckerSearchViewState CurrentViewState { get; private set; }

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
        priceCheckerWindow.ModifierSelectionChanged += OnModifierSelectionChanged;
        priceCheckerWindow.LeagueChanged += OnLeagueChanged;
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
        priceCheckerWindow.ModifierSelectionChanged -= OnModifierSelectionChanged;
        priceCheckerWindow.LeagueChanged -= OnLeagueChanged;
        priceCheckerWindow.Closed -= OnWindowClosed;
        window = null;
        generation++;
        CancelActiveRequest();
        ClearPaginationState();
    }

    public void UpdateCurrentDraft(
        TradeSearchDraft draft,
        TradeSearchValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(validationResult);

        generation++;
        CancelActiveRequest();
        ClearPaginationState();
        currentDraft = ResetModifierSelections(draft);
        currentValidationResult = ReferenceEquals(currentDraft, draft)
            ? validationResult
            : draftValidator.Validate(currentDraft);
        window?.UpdateContent(new PriceCheckerWindowState(currentDraft, currentValidationResult));
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

        leagueIdentifier = trimmedLeague;
        PersistLeagueIdentifier(trimmedLeague);

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
            window?.UpdateContent(new PriceCheckerWindowState(
                effectiveDraft,
                draftValidator.Validate(effectiveDraft)));
        }

        ApplyState(MapResult(result, effectiveDraft));
    }

    public async Task LoadMoreAsync()
    {
        if (isLoading || !TryGetNextFetchIds(out var fetchIds))
        {
            return;
        }

        var searchQueryId = paginationSearchQueryId;
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
        currentDraft = currentDraft with
        {
            ModifierFilters = modifierFilters,
        };
        currentValidationResult = draftValidator.Validate(currentDraft);

        window?.UpdateContent(new PriceCheckerWindowState(currentDraft, currentValidationResult));
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

    private void OnModifierSelectionChanged(
        object? sender,
        PriceCheckerModifierSelectionChangedEventArgs e)
    {
        UpdateModifierSelection(e.ModifierIndex, e.IsSelected);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is IPriceCheckerWindow priceCheckerWindow)
        {
            DetachWindow(priceCheckerWindow);
        }
    }

    private void OnLeagueChanged(object? sender, PriceCheckerLeagueChangedEventArgs e)
    {
        leagueIdentifier = e.LeagueIdentifier ?? string.Empty;
        if (!isLoading)
        {
            ApplyState(CreateIdleOrValidationState());
        }
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
        TradeSearchDraft? effectiveDraft)
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

        InitializePaginationState(result);
        if (displayedOffers.Count == 0 && !HasUnfetchedResultIds())
        {
            return new PriceCheckerSearchViewState
            {
                Status = PriceCheckerSearchViewStatus.ZeroResults,
                LeagueIdentifier = leagueIdentifier,
                CanSearch = CanStartSearch(),
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
            CanLoadMore = HasUnfetchedResultIds(),
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
            CanLoadMore = canLoadMore ?? (!isLoading && HasUnfetchedResultIds()),
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

    private void InitializePaginationState(PathOfExileTradePriceCheckResult result)
    {
        ClearPaginationState();
        paginationSearchQueryId = TrimToNull(result.SearchQueryId);
        paginationResultIds.AddRange(result.ResultIds
            .Select(TrimToNull)
            .Where(resultId => resultId is not null)
            .Select(resultId => resultId!));
        fetchedResultIds.UnionWith(result.FetchedResultIds
            .Select(TrimToNull)
            .Where(resultId => resultId is not null)
            .Select(resultId => resultId!));
        paginationProviderTotal = result.ProviderTotal;
        paginationInexact = result.Inexact;

        var initialOfferIds = result.FetchedResultIds.Count > 0
            ? result.FetchedResultIds
            : result.Offers.Select(offer => offer.Id).ToArray();
        AppendOffers(result.Offers, initialOfferIds);
    }

    private void ClearPaginationState()
    {
        paginationSearchQueryId = null;
        paginationResultIds.Clear();
        fetchedResultIds.Clear();
        displayedOfferIds.Clear();
        displayedOffers.Clear();
        paginationProviderTotal = null;
        paginationInexact = null;
    }

    private bool HasUnfetchedResultIds()
    {
        return !string.IsNullOrWhiteSpace(paginationSearchQueryId) &&
            paginationResultIds.Any(resultId => !fetchedResultIds.Contains(resultId));
    }

    private bool TryGetNextFetchIds(out IReadOnlyList<string> fetchIds)
    {
        fetchIds = [];
        if (!HasUnfetchedResultIds())
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
            if (nextIds.Count == PathOfExileTradeEndpointBuilder.MaximumFetchResultIds)
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
            if (offersById.TryGetValue(resultId, out var offer) && displayedOfferIds.Add(resultId))
            {
                displayedOffers.Add(MapOffer(offer));
            }
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

    private static PriceCheckerOfferViewModel MapOffer(PathOfExileTradeFetchedOffer offer)
    {
        return new PriceCheckerOfferViewModel
        {
            PriceText = FormatPrice(offer.Listing.Price),
            SellerText = SellerText(offer.Listing.Account),
            OnlineStatusText = OnlineStatusText(offer.Listing.Account?.Online),
            ItemText = ItemText(offer.Item),
            IndexedText = IndexedText(offer.Listing),
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
            })
            .ToArray();
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
            return "No listed price";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{price.Amount.Value:G29} {price.Currency.Trim()}");
    }

    private static string SellerText(PathOfExileTradeListingAccount? account)
    {
        return TrimToNull(account?.LastCharacterName) ??
            TrimToNull(account?.Name) ??
            "Unknown seller";
    }

    private static string OnlineStatusText(PathOfExileTradeListingOnlineState? online)
    {
        var status = TrimToNull(online?.Status);
        var league = TrimToNull(online?.League);
        return (status, league) switch
        {
            (not null, not null) => $"{status} ({league})",
            (not null, null) => status,
            (null, not null) => league,
            _ => string.Empty,
        };
    }

    private static string ItemText(PathOfExileTradeFetchedItem item)
    {
        var name = TrimToNull(item.Name);
        var typeLine = TrimToNull(item.TypeLine);
        if (name is not null && typeLine is not null && !EqualsOrdinal(name, typeLine))
        {
            return $"{name} - {typeLine}";
        }

        return name ?? typeLine ?? TrimToNull(item.BaseType) ?? "Unknown item";
    }

    private static string IndexedText(PathOfExileTradeListing listing)
    {
        if (listing.Indexed.HasValue)
        {
            return listing.Indexed.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }

        return TrimToNull(listing.RawIndexed) ?? "Indexed time unavailable";
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

    private static string LoadInitialLeagueIdentifier(IPriceCheckerLeaguePreferenceStore preferenceStore)
    {
        try
        {
            return TrimLeague(preferenceStore.LoadLeagueIdentifier()) ?? DefaultLeagueIdentifier;
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Price Checker league preference could not be loaded");
            return DefaultLeagueIdentifier;
        }
    }

    private void PersistLeagueIdentifier(string trimmedLeagueIdentifier)
    {
        try
        {
            leaguePreferenceStore.SaveLeagueIdentifier(trimmedLeagueIdentifier);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Price Checker league preference could not be saved");
        }
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool EqualsOrdinal(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private void ApplyState(PriceCheckerSearchViewState state)
    {
        CurrentViewState = state;
        window?.UpdateSearch(state);
    }
}
