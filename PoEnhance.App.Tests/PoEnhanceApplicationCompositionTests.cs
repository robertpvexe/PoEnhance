using System.Reflection;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests;

public sealed class PoEnhanceApplicationCompositionTests
{
    [Fact]
    public void CreateDefault_UsesOneSharedHttpClientForSearchAndFetch()
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

        Assert.Same(composition.PathOfExileTradeHttpClient, searchHttpClient);
        Assert.Same(searchHttpClient, fetchHttpClient);
        Assert.Same(searchHttpClient, statsHttpClient);
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
            composition.TradeSelectedModifierMapper,
            PrivateField<IPathOfExileTradeSelectedModifierMapper>(
                composition.PriceCheckService,
                "selectedModifierMapper"));
        Assert.IsType<PathOfExileTradeStatsClient>(composition.TradeStatsClient);
        Assert.IsType<PathOfExileTradeStatMatcher>(composition.TradeStatMatcher);
        Assert.IsType<PathOfExileTradeStatCatalogProvider>(composition.TradeStatCatalogProvider);
        Assert.IsType<PathOfExileTradeSelectedModifierMapper>(composition.TradeSelectedModifierMapper);
        Assert.Same(
            composition.TradeStatsClient,
            PrivateField<IPathOfExileTradeStatsClient>(
                composition.TradeStatCatalogProvider,
                "statsClient"));
        Assert.Same(
            composition.TradeStatMatcher,
            PrivateField<IPathOfExileTradeStatMatcher>(
                composition.TradeSelectedModifierMapper,
                "statMatcher"));
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
    public void PriceCheckerSearchFeature_DoesNotIntroduceLoadMoreCacheRetryQueueTimerOrAutoRefresh()
    {
        var searchFeatureTypes = typeof(PriceCheckerSearchController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Features.PriceChecking")
            .Where(type =>
                type.Name.Contains("Search", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("Offer", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.DoesNotContain(searchFeatureTypes, type =>
            Contains(type, "LoadMore") ||
            Contains(type, "Cache") ||
            Contains(type, "Retry") ||
            Contains(type, "Queue") ||
            Contains(type, "Timer") ||
            Contains(type, "AutomaticRefresh"));
        Assert.DoesNotContain(searchFeatureTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "LoadMore") ||
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
                Contains(type, "PathOfExileTradeSelectedModifierMapper"));
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
