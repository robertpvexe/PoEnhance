using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeInfrastructureArchitectureTests
{
    [Fact]
    public void ProviderTradeInfrastructure_IntroducesOnlySearchFetchAndStatsHttpExecution()
    {
        var httpTypes = ProviderTradeTypes()
            .Where(type =>
                Contains(type, "HttpClient") ||
                Contains(type, "HttpRequest") ||
                Contains(type, "HttpResponse") ||
                Contains(type, "TradeFetchClient") ||
                Contains(type, "TradeSearchClient") ||
                Contains(type, "TradeStatsClient"))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "IPathOfExileTradeFetchClient",
                "IPathOfExileTradeSearchClient",
                "IPathOfExileTradeStatsClient",
                "PathOfExileTradeFetchClient",
                "PathOfExileTradeHttpClientSupport",
                "PathOfExileTradeSearchClient",
                "PathOfExileTradeStatsClient",
            ],
            httpTypes);
    }

    [Fact]
    public void SearchAndFetchClients_DoNotInvokeEachOther()
    {
        Assert.DoesNotContain(typeof(PathOfExileTradeSearchClient).GetMethods(), method =>
            method.Name.Contains("Fetch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PathOfExileTradeFetchClient).GetMethods(), method =>
            method.Name.Contains("Search", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PathOfExileTradeStatsClient).GetMethods(), method =>
            method.Name.Contains("Search", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Fetch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CoreAssembly_GainsNoProviderSpecificDependency()
    {
        var coreAssembly = typeof(TradeSearchDraft).Assembly;
        var referencedNames = coreAssembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("PoEnhance.App", referencedNames);
        Assert.DoesNotContain(
            coreAssembly.GetTypes(),
            type => Contains(type, "PathOfExileTrade"));
    }

    [Fact]
    public void ProviderTradeInfrastructure_DoesNotIntroduceCurrencyExchangeOrPublicStash()
    {
        Assert.DoesNotContain(ProviderTradeTypes(), type =>
            Contains(type, "CurrencyExchange") ||
            Contains(type, "PublicStash"));
    }

    [Fact]
    public void ProviderTradeInfrastructure_KeepsStatsOutOfSearchJsonBuilder()
    {
        var referencedTypes = ReferencedMemberTypes(typeof(PathOfExileTradeQueryBuilder));

        Assert.DoesNotContain(referencedTypes, type =>
            Contains(type, "PathOfExileTradeStatCatalog") ||
            Contains(type, "PathOfExileTradeStatMatcher") ||
            Contains(type, "PathOfExileTradeStatsClient"));
    }

    [Fact]
    public void PriceCheckerUi_DoesNotIntroduceModifierControlsOrStatsRefresh()
    {
        var priceCheckerTypes = typeof(PoEnhance.App.Features.PriceChecking.PriceCheckerWindowController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Features.PriceChecking")
            .ToArray();

        Assert.DoesNotContain(priceCheckerTypes, type =>
            Contains(type, "ModifierCheckbox") ||
            Contains(type, "ModifierControl") ||
            Contains(type, "StatsRefresh") ||
            Contains(type, "CatalogRefresh") ||
            Contains(type, "Background"));
        Assert.DoesNotContain(priceCheckerTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "PathOfExileTradeStatsClient") ||
            Contains(type, "PathOfExileTradeStatMatcher"));
    }

    [Fact]
    public void RateLimitInfrastructure_DoesNotIntroduceSchedulerDelayOrRetryLoop()
    {
        Assert.DoesNotContain(ProviderTradeTypes(), type =>
            Contains(type, "Scheduler") ||
            Contains(type, "Delay") ||
            Contains(type, "Timer"));

        Assert.DoesNotContain(
            typeof(PathOfExileTradeRateLimitParser).GetMethods(),
            method => method.Name.Contains("Wait", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Delay", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<Type> ProviderTradeTypes()
    {
        return typeof(PathOfExileTradeEndpointBuilder).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Infrastructure.Trade.PathOfExile")
            .Where(type => !type.IsNested && !type.Name.StartsWith("<", StringComparison.Ordinal))
            .ToArray();
    }

    private static IEnumerable<Type> ReferencedMemberTypes(Type type)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

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
