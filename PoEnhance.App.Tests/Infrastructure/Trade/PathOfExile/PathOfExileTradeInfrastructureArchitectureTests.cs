using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeInfrastructureArchitectureTests
{
    [Fact]
    public void ProviderTradeInfrastructure_IntroducesOnlySearchFetchStatsAndItemsHttpExecution()
    {
        var httpTypes = ProviderTradeTypes()
            .Where(type =>
                Contains(type, "HttpClient") ||
                Contains(type, "HttpRequest") ||
                Contains(type, "HttpResponse") ||
                Contains(type, "TradeFetchClient") ||
                Contains(type, "TradeSearchClient") ||
                Contains(type, "TradeStatsClient") ||
                Contains(type, "TradeItemsClient"))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "IPathOfExileTradeFetchClient",
                "IPathOfExileTradeItemsClient",
                "IPathOfExileTradeSearchClient",
                "IPathOfExileTradeStatsClient",
                "PathOfExileTradeFetchClient",
                "PathOfExileTradeHttpClientSupport",
                "PathOfExileTradeItemsClient",
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
        Assert.DoesNotContain(typeof(PathOfExileTradeItemsClient).GetMethods(), method =>
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
    public void PriceCheckerUi_DoesNotReferenceStatsServicesOrRefresh()
    {
        var priceCheckerTypes = typeof(PoEnhance.App.Features.PriceChecking.PriceCheckerWindowController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Features.PriceChecking")
            .ToArray();

        Assert.DoesNotContain(priceCheckerTypes, type =>
            Contains(type, "StatsRefresh") ||
            Contains(type, "CatalogRefresh") ||
            Contains(type, "Background"));
        Assert.DoesNotContain(priceCheckerTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "PathOfExileTradeStatsClient") ||
            Contains(type, "PathOfExileTradeStatMatcher") ||
            Contains(type, "PathOfExileTradeItemsClient") ||
            Contains(type, "PathOfExileTradeItemCatalogProvider") ||
            Contains(type, "PathOfExileTradeItemIdentityMapper"));
    }

    [Fact]
    public void PriceCheckerModifierUi_ExposesOpaqueVariantStateButNoProviderIdentifiersOrModels()
    {
        var viewModelType = typeof(PoEnhance.App.Features.PriceChecking.PriceCheckerModifierViewModel);
        var properties = viewModelType
            .GetProperties()
            .ToArray();
        var propertyNames = properties.Select(property => property.Name).ToArray();

        Assert.Equal(typeof(string), viewModelType.GetProperty("MinimumText")?.PropertyType);
        Assert.Equal(typeof(string), viewModelType.GetProperty("MaximumText")?.PropertyType);
        var variantType = typeof(PoEnhance.App.Features.PriceChecking.PriceCheckerModifierFilterVariantViewModel);
        Assert.Equal(typeof(string), variantType.GetProperty("Identity")?.PropertyType);
        Assert.Equal(typeof(string), variantType.GetProperty("Label")?.PropertyType);
        Assert.DoesNotContain(variantType.GetProperties().Select(property => property.Name), name =>
            Contains(name, "StatId") || Contains(name, "Provider"));
        Assert.DoesNotContain(propertyNames, name =>
            Contains(name, "StatId") ||
            Contains(name, "TradeStat") ||
            Contains(name, "Provider") ||
            Contains(name, "Tier"));
        Assert.DoesNotContain(properties.Select(property => property.PropertyType), type =>
            Contains(type, "PathOfExileTrade") ||
            Contains(type, "Json") ||
            Contains(type, "Provider"));

        var xaml = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerWindow.xaml"));
        Assert.Contains("ModifierListBox", xaml, StringComparison.Ordinal);
        Assert.Contains("CheckBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StatId", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TradeStat", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RequestedMinimum", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RequestedMaximum", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PathOfExileTrade", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Json", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Slider", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tier", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AdvancedFilter", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SelectAll", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModifierBoundProjection_HasNoItemModifierBaseOrProviderStatIdentityExceptions()
    {
        var root = RepositoryRoot();
        var source = string.Join(
            Environment.NewLine,
            File.ReadAllText(Path.Combine(
                root,
                "PoEnhance.Core",
                "Trade",
                "ModifierBoundDefaults.cs")),
            File.ReadAllText(Path.Combine(
                root,
                "PoEnhance.App",
                "Infrastructure",
                "Trade",
                "PathOfExile",
                "PathOfExileTradeModifierBoundProjector.cs")));

        Assert.DoesNotContain("ProviderStatId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolvedModifierId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolvedModifierName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ParsedModifierName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DisplayName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ParsedBaseType", source, StringComparison.Ordinal);
        Assert.DoesNotContain("explicit.stat_", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Cold Damage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Fire Damage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Lightning Damage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Physical Damage", source, StringComparison.Ordinal);
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

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
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

    private static bool Contains(string text, string value)
    {
        return text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
