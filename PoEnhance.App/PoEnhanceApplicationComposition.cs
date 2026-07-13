using System.Net.Http;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App;

internal sealed class PoEnhanceApplicationComposition : IDisposable
{
    private PoEnhanceApplicationComposition(
        HttpClient pathOfExileTradeHttpClient,
        RuntimeGameDataService runtimeGameDataService,
        ProvisionalGameDataRecordingService provisionalGameDataRecordingService,
        IPathOfExileTradeSearchClient tradeSearchClient,
        IPathOfExileTradeFetchClient tradeFetchClient,
        IPathOfExileTradePriceCheckService priceCheckService,
        PriceCheckerWindowController priceCheckerWindowController)
    {
        PathOfExileTradeHttpClient = pathOfExileTradeHttpClient;
        RuntimeGameDataService = runtimeGameDataService;
        ProvisionalGameDataRecordingService = provisionalGameDataRecordingService;
        TradeSearchClient = tradeSearchClient;
        TradeFetchClient = tradeFetchClient;
        PriceCheckService = priceCheckService;
        PriceCheckerWindowController = priceCheckerWindowController;
    }

    public HttpClient PathOfExileTradeHttpClient { get; }

    public RuntimeGameDataService RuntimeGameDataService { get; }

    public ProvisionalGameDataRecordingService ProvisionalGameDataRecordingService { get; }

    public IPathOfExileTradeSearchClient TradeSearchClient { get; }

    public IPathOfExileTradeFetchClient TradeFetchClient { get; }

    public IPathOfExileTradePriceCheckService PriceCheckService { get; }

    public PriceCheckerWindowController PriceCheckerWindowController { get; }

    public static PoEnhanceApplicationComposition CreateDefault()
    {
        var tradeHttpClient = CreatePathOfExileTradeHttpClient();
        var searchClient = new PathOfExileTradeSearchClient(tradeHttpClient);
        var fetchClient = new PathOfExileTradeFetchClient(tradeHttpClient);
        var priceCheckService = new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            searchClient,
            fetchClient);
        var priceCheckerWindowController = new PriceCheckerWindowController(
            new PriceCheckerWindowFactory(),
            priceCheckService);

        return new PoEnhanceApplicationComposition(
            tradeHttpClient,
            new RuntimeGameDataService(),
            new ProvisionalGameDataRecordingService(
                new JsonProvisionalGameDataStore(
                    new ProvisionalGameDataStorePathResolver().ResolveDefaultPath())),
            searchClient,
            fetchClient,
            priceCheckService,
            priceCheckerWindowController);
    }

    public void Dispose()
    {
        PathOfExileTradeHttpClient.Dispose();
    }

    private static HttpClient CreatePathOfExileTradeHttpClient()
    {
        return new HttpClient(
            new SocketsHttpHandler
            {
                UseCookies = false,
            },
            disposeHandler: true);
    }
}
