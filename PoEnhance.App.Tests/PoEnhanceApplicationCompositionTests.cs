using System.Reflection;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests;

public sealed class PoEnhanceApplicationCompositionTests
{
    [Fact]
    public void CreateDefault_UsesOneSharedHttpClientForTradeClients()
    {
        using var composition = PoEnhanceApplicationComposition.CreateDefault();

        var searchHttpClient = PrivateField<HttpClient>(
            composition.TradeSearchClient,
            "httpClient");
        var fetchHttpClient = PrivateField<HttpClient>(
            composition.TradeFetchClient,
            "httpClient");
        var statsHttpClient = PrivateField<HttpClient>(
            composition.TradeStatsClient,
            "httpClient");
        var itemsHttpClient = PrivateField<HttpClient>(
            composition.TradeItemsClient,
            "httpClient");

        Assert.Same(composition.PathOfExileTradeHttpClient, searchHttpClient);
        Assert.Same(searchHttpClient, fetchHttpClient);
        Assert.Same(searchHttpClient, statsHttpClient);
        Assert.Same(searchHttpClient, itemsHttpClient);
    }

    [Fact]
    public void CreateDefault_ConfiguresClientsAndPriceCheckServiceOnce()
    {
        using var composition = PoEnhanceApplicationComposition.CreateDefault();

        Assert.Same(
            composition.TradeSearchClient,
            PrivateField<IPathOfExileTradeSearchClient>(
                composition.PriceCheckService,
                "searchClient"));
        Assert.Same(
            composition.TradeFetchClient,
            PrivateField<IPathOfExileTradeFetchClient>(
                composition.PriceCheckService,
                "fetchClient"));
        Assert.Same(
            composition.TradeStatCatalogProvider,
            PrivateField<IPathOfExileTradeStatCatalogProvider>(
                composition.PriceCheckService,
                "statCatalogProvider"));
        Assert.Same(
            composition.TradeStatMatcher,
            PrivateField<IPathOfExileTradeStatMatcher>(
                composition.PriceCheckService,
                "statMatcher"));
        Assert.Same(
            composition.TradeItemCatalogProvider,
            PrivateField<IPathOfExileTradeItemCatalogProvider>(
                composition.PriceCheckService,
                "itemCatalogProvider"));
        Assert.Same(
            composition.TradeSelectedModifierMapper,
            PrivateField<IPathOfExileTradeSelectedModifierMapper>(
                composition.PriceCheckService,
                "selectedModifierMapper"));
        Assert.Same(
            composition.TradeItemIdentityMapper,
            PrivateField<IPathOfExileTradeItemIdentityMapper>(
                composition.PriceCheckService,
                "itemIdentityMapper"));
        Assert.IsType<PathOfExileTradeStatsClient>(composition.TradeStatsClient);
        Assert.IsType<PathOfExileTradeItemsClient>(composition.TradeItemsClient);
        Assert.IsType<PathOfExileTradeStatMatcher>(composition.TradeStatMatcher);
        Assert.IsType<PathOfExileTradeStatCatalogProvider>(composition.TradeStatCatalogProvider);
        Assert.IsType<PathOfExileTradeItemCatalogProvider>(composition.TradeItemCatalogProvider);
        Assert.IsType<PathOfExileTradeSelectedModifierMapper>(composition.TradeSelectedModifierMapper);
        Assert.IsType<PathOfExileTradeItemIdentityMapper>(composition.TradeItemIdentityMapper);
        Assert.Same(
            composition.TradeStatsClient,
            PrivateField<IPathOfExileTradeStatsClient>(
                composition.TradeStatCatalogProvider,
                "statsClient"));
        Assert.Same(
            composition.TradeItemsClient,
            PrivateField<IPathOfExileTradeItemsClient>(
                composition.TradeItemCatalogProvider,
                "itemsClient"));
    }

    [Fact]
    public void CreateDefault_UsesDedicatedStatsResponseBoundAndGenericSearchFetchBounds()
    {
        using var composition = PoEnhanceApplicationComposition.CreateDefault();

        Assert.Equal(
            PathOfExileTradeHttpClientSupport.DefaultMaximumResponseBodyBytes,
            PrivateField<int>(composition.TradeSearchClient, "maximumResponseBodyBytes"));
        Assert.Equal(
            PathOfExileTradeHttpClientSupport.DefaultMaximumResponseBodyBytes,
            PrivateField<int>(composition.TradeFetchClient, "maximumResponseBodyBytes"));
        Assert.Equal(
            PathOfExileTradeStatsClient.MaximumStatsResponseBodyBytes,
            PrivateField<int>(composition.TradeStatsClient, "maximumResponseBodyBytes"));
        Assert.Equal(
            PathOfExileTradeItemsClient.MaximumItemsResponseBodyBytes,
            PrivateField<int>(composition.TradeItemsClient, "maximumResponseBodyBytes"));
    }

    [Fact]
    public void CreateDefault_DoesNotAddCookieAuthorizationOrOAuthHeaders()
    {
        using var composition = PoEnhanceApplicationComposition.CreateDefault();
        var headers = composition.PathOfExileTradeHttpClient.DefaultRequestHeaders;

        Assert.Null(headers.Authorization);
        Assert.DoesNotContain(headers, header =>
            header.Key.Contains("Cookie", StringComparison.OrdinalIgnoreCase) ||
            header.Key.Contains("POESESSID", StringComparison.OrdinalIgnoreCase) ||
            header.Key.Contains("OAuth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PriceCheckerSearchFeature_DoesNotOwnHttpClientsOrProviderTransports()
    {
        var dependencyTypes = ReferencedMemberTypes(typeof(PriceCheckerSearchController)).ToArray();

        Assert.DoesNotContain(dependencyTypes, type => type == typeof(HttpClient));
        Assert.DoesNotContain(dependencyTypes, type => Contains(type, "PathOfExileTradeSearchClient"));
        Assert.DoesNotContain(dependencyTypes, type => Contains(type, "PathOfExileTradeFetchClient"));
    }

    [Fact]
    public void PriceCheckerSearchFeature_DoesNotIntroduceCacheRetryQueueTimerOrAutoRefresh()
    {
        var searchFeatureTypes = typeof(PriceCheckerSearchController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Features.PriceChecking")
            .Where(type =>
                type.Name.Contains("Search", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("Modifier", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("Offer", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.DoesNotContain(searchFeatureTypes, type =>
            Contains(type, "Cache") ||
            Contains(type, "Retry") ||
            Contains(type, "Queue") ||
            Contains(type, "Timer") ||
            Contains(type, "AutomaticRefresh"));
        Assert.DoesNotContain(searchFeatureTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "Cache") ||
            Contains(type, "Retry") ||
            Contains(type, "Queue") ||
            Contains(type, "Timer") ||
            Contains(type, "AutomaticRefresh"));
    }

    [Fact]
    public void CreateDefault_PriceCheckerWindowControllerDoesNotReferenceStatsCatalogOrMapperServices()
    {
        using var composition = PoEnhanceApplicationComposition.CreateDefault();

        Assert.DoesNotContain(
            ReferencedMemberTypes(composition.PriceCheckerWindowController.GetType()),
            type => Contains(type, "PathOfExileTradeStatsClient") ||
                Contains(type, "PathOfExileTradeStatMatcher") ||
                Contains(type, "PathOfExileTradeStatCatalogProvider") ||
                Contains(type, "PathOfExileTradeSelectedModifierMapper") ||
                Contains(type, "PathOfExileTradeItemsClient") ||
                Contains(type, "PathOfExileTradeItemCatalogProvider") ||
                Contains(type, "PathOfExileTradeItemIdentityMapper"));
    }

    private static T PrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<T>(field.GetValue(instance));
    }

    private static IEnumerable<Type> ReferencedMemberTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        return type.GetConstructors(flags).SelectMany(constructor =>
                constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetFields(flags).Select(field => field.FieldType))
            .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Select(method => method.ReturnType))
            .Concat(type.GetMethods(flags).SelectMany(method =>
                method.GetParameters().Select(parameter => parameter.ParameterType)));
    }

    private static bool Contains(Type type, string value)
    {
        return type.FullName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }
}
