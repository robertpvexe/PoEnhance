using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeInfrastructureArchitectureTests
{
    [Fact]
    public void ProviderTradeInfrastructure_IntroducesOnlySearchAndFetchHttpExecution()
    {
        var httpTypes = ProviderTradeTypes()
            .Where(type =>
                Contains(type, "HttpClient") ||
                Contains(type, "HttpRequest") ||
                Contains(type, "HttpResponse") ||
                Contains(type, "TradeFetchClient") ||
                Contains(type, "TradeSearchClient"))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "IPathOfExileTradeFetchClient",
                "IPathOfExileTradeSearchClient",
                "PathOfExileTradeFetchClient",
                "PathOfExileTradeHttpClientSupport",
                "PathOfExileTradeSearchClient",
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

    private static bool Contains(Type type, string value)
    {
        return type.FullName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }
}
