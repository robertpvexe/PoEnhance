using System.Globalization;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerSearchController
{
    public const string DefaultLeagueIdentifier = "Standard";
    private const int MaximumSafeProviderMessageLength = 160;

    private readonly IPathOfExileTradePriceCheckService priceCheckService;
    private IPriceCheckerWindow? window;
    private TradeSearchDraft? currentDraft;
    private TradeSearchValidationResult? currentValidationResult;
    private CancellationTokenSource? activeRequestCancellation;
    private string leagueIdentifier = DefaultLeagueIdentifier;
    private int generation;
    private bool isLoading;

    public PriceCheckerSearchController(IPathOfExileTradePriceCheckService priceCheckService)
    {
        this.priceCheckService = priceCheckService ?? throw new ArgumentNullException(nameof(priceCheckService));
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
        priceCheckerWindow.LeagueChanged -= OnLeagueChanged;
        priceCheckerWindow.Closed -= OnWindowClosed;
        window = null;
        generation++;
        CancelActiveRequest();
    }

    public void UpdateCurrentDraft(
        TradeSearchDraft draft,
        TradeSearchValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(validationResult);

        generation++;
        CancelActiveRequest();
        currentDraft = draft;
        currentValidationResult = validationResult;
        ApplyState(CreateIdleOrValidationState());
    }

    public async Task SearchAsync()
    {
        if (isLoading)
        {
            return;
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

        var requestGeneration = ++generation;
        using var requestCancellation = new CancellationTokenSource();
        activeRequestCancellation = requestCancellation;
        isLoading = true;
        ApplyState(new PriceCheckerSearchViewState
        {
            Status = PriceCheckerSearchViewStatus.Loading,
            LeagueIdentifier = leagueIdentifier,
            CanSearch = false,
            Message = "Searching...",
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
        ApplyState(MapResult(result));
    }

    private void OnSearchRequested(object? sender, EventArgs e)
    {
        _ = SearchAsync();
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
    }

    private PriceCheckerSearchViewState CreateIdleOrValidationState()
    {
        return CreateLocalValidationState() ?? new PriceCheckerSearchViewState
        {
            Status = PriceCheckerSearchViewStatus.Idle,
            LeagueIdentifier = leagueIdentifier,
            CanSearch = true,
            Message = "Ready to search.",
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
            return ValidationState("Select a supported base-only search.");
        }

        if (!currentValidationResult.IsValid)
        {
            return ValidationState("Select a supported base-only search.");
        }

        if (currentDraft.ModifierFilters.Any(modifier => modifier.IsSelected))
        {
            return ValidationState("Select a supported base-only search.");
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
        };
    }

    private PriceCheckerSearchViewState MapResult(PathOfExileTradePriceCheckResult result)
    {
        if (result.IsCancelled)
        {
            return new PriceCheckerSearchViewState
            {
                Status = PriceCheckerSearchViewStatus.Cancelled,
                LeagueIdentifier = leagueIdentifier,
                CanSearch = CanStartSearch(),
                Message = "Search cancelled.",
            };
        }

        if (!result.IsSuccess)
        {
            var status = result.Stage == PathOfExileTradePriceCheckStage.QueryBuild
                ? PriceCheckerSearchViewStatus.ValidationError
                : PriceCheckerSearchViewStatus.ProviderOrTransportError;
            return new PriceCheckerSearchViewState
            {
                Status = status,
                LeagueIdentifier = leagueIdentifier,
                CanSearch = CanStartSearch(),
                Message = status == PriceCheckerSearchViewStatus.ValidationError
                    ? "Select a supported base-only search."
                    : FailureMessage(result),
            };
        }

        var offers = result.Offers.Select(MapOffer).ToArray();
        if (offers.Length == 0)
        {
            return new PriceCheckerSearchViewState
            {
                Status = PriceCheckerSearchViewStatus.ZeroResults,
                LeagueIdentifier = leagueIdentifier,
                CanSearch = CanStartSearch(),
                Message = "No offers found.",
                Summary = "No offers found.",
            };
        }

        return new PriceCheckerSearchViewState
        {
            Status = PriceCheckerSearchViewStatus.Success,
            LeagueIdentifier = leagueIdentifier,
            CanSearch = CanStartSearch(),
            Message = "Search complete.",
            Summary = CreateSuccessSummary(offers.Length, result.ProviderTotal, result.Inexact),
            Offers = offers,
        };
    }

    private bool CanStartSearch()
    {
        return !isLoading && CreateLocalValidationState() is null;
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
