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
        IPathOfExileTradeStatsClient tradeStatsClient,
        IPathOfExileTradeItemsClient tradeItemsClient,
        IPathOfExileTradeStatMatcher tradeStatMatcher,
        IPathOfExileTradeStatCatalogProvider tradeStatCatalogProvider,
        IPathOfExileTradeItemCatalogProvider tradeItemCatalogProvider,
        IPathOfExileTradeSelectedModifierMapper tradeSelectedModifierMapper,
        IPathOfExileTradeItemIdentityMapper tradeItemIdentityMapper,
        IPathOfExileTradePriceCheckService priceCheckService,
        PriceCheckerWindowController priceCheckerWindowController)
    {
        PathOfExileTradeHttpClient = pathOfExileTradeHttpClient;
        RuntimeGameDataService = runtimeGameDataService;
        ProvisionalGameDataRecordingService = provisionalGameDataRecordingService;
        TradeSearchClient = tradeSearchClient;
        TradeFetchClient = tradeFetchClient;
        TradeStatsClient = tradeStatsClient;
        TradeItemsClient = tradeItemsClient;
        TradeStatMatcher = tradeStatMatcher;
        TradeStatCatalogProvider = tradeStatCatalogProvider;
        TradeItemCatalogProvider = tradeItemCatalogProvider;
        TradeSelectedModifierMapper = tradeSelectedModifierMapper;
        TradeItemIdentityMapper = tradeItemIdentityMapper;
        PriceCheckService = priceCheckService;
        PriceCheckerWindowController = priceCheckerWindowController;
    }

    public HttpClient PathOfExileTradeHttpClient { get; }

    public RuntimeGameDataService RuntimeGameDataService { get; }

    public ProvisionalGameDataRecordingService ProvisionalGameDataRecordingService { get; }

    public IPathOfExileTradeSearchClient TradeSearchClient { get; }

    public IPathOfExileTradeFetchClient TradeFetchClient { get; }

    public IPathOfExileTradeStatsClient TradeStatsClient { get; }

    public IPathOfExileTradeItemsClient TradeItemsClient { get; }

    public IPathOfExileTradeStatMatcher TradeStatMatcher { get; }

    public IPathOfExileTradeStatCatalogProvider TradeStatCatalogProvider { get; }

    public IPathOfExileTradeItemCatalogProvider TradeItemCatalogProvider { get; }

    public IPathOfExileTradeSelectedModifierMapper TradeSelectedModifierMapper { get; }

    public IPathOfExileTradeItemIdentityMapper TradeItemIdentityMapper { get; }

    public IPathOfExileTradePriceCheckService PriceCheckService { get; }

    public PriceCheckerWindowController PriceCheckerWindowController { get; }

    public static PoEnhanceApplicationComposition CreateDefault()
    {
        var tradeHttpClient = CreatePathOfExileTradeHttpClient();
        var searchClient = new PathOfExileTradeSearchClient(tradeHttpClient);
        var fetchClient = new PathOfExileTradeFetchClient(tradeHttpClient);
        var statsClient = new PathOfExileTradeStatsClient(tradeHttpClient);
        var itemsClient = new PathOfExileTradeItemsClient(tradeHttpClient);
        var statMatcher = new PathOfExileTradeStatMatcher();
        var statCatalogProvider = new PathOfExileTradeStatCatalogProvider(statsClient);
        var itemCatalogProvider = new PathOfExileTradeItemCatalogProvider(itemsClient);
        var selectedModifierMapper = new PathOfExileTradeSelectedModifierMapper(statMatcher);
        var itemIdentityMapper = new PathOfExileTradeItemIdentityMapper();
        var priceCheckService = new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            statCatalogProvider,
            itemCatalogProvider,
            selectedModifierMapper,
            itemIdentityMapper,
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
            statsClient,
            itemsClient,
            statMatcher,
            statCatalogProvider,
            itemCatalogProvider,
            selectedModifierMapper,
            itemIdentityMapper,
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
