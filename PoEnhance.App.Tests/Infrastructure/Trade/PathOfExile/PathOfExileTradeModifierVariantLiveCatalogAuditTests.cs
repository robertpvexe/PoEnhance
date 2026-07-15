using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeModifierVariantLiveCatalogAuditTests
{
    [Fact]
    public async Task OfficialCatalog_OptInFullGameDataDomainEligibilityAudit()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("POENHANCE_RUN_LIVE_MODIFIER_VARIANT_AUDIT"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var httpClient = new HttpClient();
        var catalogResult = await new PathOfExileTradeStatCatalogProvider(
                new PathOfExileTradeStatsClient(httpClient))
            .GetCatalogAsync();
        Assert.True(catalogResult.IsSuccess && catalogResult.Catalog is not null);
        var gameDataResult = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(gameDataResult.IsSuccess && gameDataResult.Package is not null);

        var report = PathOfExileTradeModifierDomainEligibilityAuditor.Audit(
            catalogResult.Catalog!,
            GameDataCatalog.FromPackage(gameDataResult.Package!));
        var reportPath = Path.Combine(
            FindRepoDirectory(),
            "artifacts",
            "audits",
            "modifier-provider-domain-eligibility-audit.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        }));
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            report.CanonicalEffectsInspected,
            report.ItemContextCombinationsInspected,
            report.ProviderCandidatesInspected,
            report.SupportedVariantsByProviderKind,
            report.RejectedVariantsByReason,
            report.AmbiguousVariants,
            report.DuplicateIdentitiesRemoved,
            report.EffectsWithNoValidProviderVariant,
            ReportPath = reportPath,
        }, new JsonSerializerOptions { WriteIndented = true }));

        Assert.True(report.CanonicalEffectsInspected > 0);
        Assert.True(report.ItemContextCombinationsInspected > 0);
        Assert.True(report.ProviderCandidatesInspected > 0);
        Assert.Contains(report.Combinations, combination =>
            combination.CanonicalSignature.Contains("Accuracy Rating", StringComparison.Ordinal) &&
            combination.ProviderKind == "implicit" &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            combination.CanonicalSignature.Contains("Accuracy Rating", StringComparison.Ordinal) &&
            combination.ItemClass is "Jewel" or "AbyssJewel" &&
            combination.ProviderKind == "implicit" &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            combination.CanonicalSignature.Contains("Accuracy Rating", StringComparison.Ordinal) &&
            combination.ItemClass is "Ring" or "Amulet" &&
            combination.ProviderKind == "implicit" &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            combination.CanonicalSignature.Contains("Attack Speed", StringComparison.Ordinal) &&
            combination.ProviderKind == "pseudo" &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            combination.CanonicalSignature.Contains("Physical Damage", StringComparison.Ordinal) &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            combination.CanonicalSignature.Contains("maximum Life", StringComparison.Ordinal) &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            combination.CanonicalSignature.Contains("Resistance", StringComparison.Ordinal) &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            (combination.CanonicalSignature.Contains("Strength", StringComparison.Ordinal) ||
                combination.CanonicalSignature.Contains("Dexterity", StringComparison.Ordinal) ||
                combination.CanonicalSignature.Contains("Intelligence", StringComparison.Ordinal)) &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            combination.ProviderKind == "enchant" &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            combination.MatchedContextTag == "base-implicit" &&
            combination.ProviderKind == "implicit" &&
            combination.Status == PathOfExileTradeModifierDomainEligibilityAuditStatus.Supported);
        Assert.Contains(report.Combinations, combination =>
            combination.Reason.StartsWith(
                PathOfExileTradeModifierVariantDiscovery.SemanticMismatch,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task OfficialCatalog_OptInApplicabilityAndDeduplicationAudit()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("POENHANCE_RUN_LIVE_MODIFIER_VARIANT_AUDIT"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var httpClient = new HttpClient();
        var catalogResult = await new PathOfExileTradeStatCatalogProvider(
                new PathOfExileTradeStatsClient(httpClient))
            .GetCatalogAsync();
        Assert.True(
            catalogResult.IsSuccess && catalogResult.Catalog is not null,
            string.Join(", ", catalogResult.Diagnostics.Select(diagnostic => diagnostic.Code)));
        var catalog = catalogResult.Catalog!;
        var all = await LoadProductionSources(catalog);
        var attackSpeed = Find(all, source =>
            source.Component.IsCrafted &&
            source.Component.Locality == ModifierLocality.Local &&
            source.Component.OriginalText.Contains("Attack Speed", StringComparison.Ordinal),
            "crafted-local-attack-speed");
        var physicalDamage = Find(all, source =>
            source.Component.Locality == ModifierLocality.Local &&
            source.Component.OriginalText.Contains("increased Physical Damage", StringComparison.Ordinal),
            "local-increased-physical-damage");
        var accuracy = Find(all, source =>
            source.Component.OriginalText.Contains("Accuracy Rating", StringComparison.Ordinal),
            "accuracy-rating");
        var resistance = Find(all, source =>
            source.Component.OriginalText.Contains("Resistance", StringComparison.Ordinal),
            "elemental-resistance");
        var maximumLife = Find(all, source =>
            source.Component.OriginalText.Contains("maximum Life", StringComparison.Ordinal),
            "maximum-life");
        var implicitEffect = Find(all, source =>
            source.Component.ParsedKind == ParsedModifierKind.Implicit,
            "implicit-effect");
        var incompatiblePseudo = Find(all, source =>
        {
            var discovery = PathOfExileTradeModifierVariantResolver.DiscoverForAudit(
                source.Component,
                catalog,
                source.SourceExactCandidate);
            return discovery.Trace.Any(trace =>
                trace.ProviderKind == "pseudo" &&
                trace.RejectionReason.StartsWith(
                    PathOfExileTradeModifierVariantDiscovery.SemanticMismatch,
                    StringComparison.Ordinal));
        }, "similarly-named-pseudo-rejected");

        var report = PathOfExileTradeModifierVariantCatalogAuditor.Audit(
            catalog,
            [
                attackSpeed,
                physicalDamage,
                accuracy,
                resistance,
                maximumLife,
                implicitEffect,
                incompatiblePseudo,
            ]);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            report.RawProviderCandidateCount,
            report.RejectedSemanticMismatchCount,
            report.RejectedItemApplicabilityCount,
            report.RejectedDuplicateIdentityCount,
            report.RejectedSameKindAmbiguityCount,
            Effects = report.Effects.Select(effect => new
            {
                effect.Label,
                effect.SourceProviderStatId,
                effect.SourceText,
                effect.RawProviderCandidateCount,
                effect.RejectedSemanticMismatchCount,
                effect.RejectedItemApplicabilityCount,
                effect.RejectedDuplicateIdentityCount,
                effect.RejectedSameKindAmbiguityCount,
                effect.FinalOptionCount,
                effect.FinalProviderKinds,
            }),
            CraftedLocalAttackSpeedTrace = report.Effects
                .Single(effect => effect.Label == "crafted-local-attack-speed")
                .Trace,
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        }));

        var attackAudit = Assert.Single(report.Effects, effect =>
            effect.Label == "crafted-local-attack-speed");
        Assert.Equal(["Crafted", "Explicit", "Fractured", "Implicit", "Pseudo"], attackAudit.FinalProviderKinds
            .OrderBy(kind => kind, StringComparer.Ordinal));
        Assert.Single(attackAudit.FinalProviderKinds, kind => kind == "Pseudo");
        Assert.Equal(
            attackAudit.FinalProviderKinds.Count,
            attackAudit.FinalProviderKinds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(attackAudit.Trace, trace =>
            trace.ProviderKind == "implicit" &&
            trace.ProviderLocality == PathOfExileTradeProviderStatLocality.Local &&
            trace.IsAccepted);
        Assert.Contains(attackAudit.Trace, trace =>
            trace.ProviderKind == "enchant" &&
            trace.RejectionReason == PathOfExileTradeModifierVariantDiscovery.ItemApplicabilityUnproven);
        Assert.Contains(attackAudit.Trace, trace =>
            trace.ProviderKind == "scourge" &&
            trace.RejectionReason.StartsWith(
                PathOfExileTradeModifierVariantDiscovery.SemanticMismatch,
                StringComparison.Ordinal));
        Assert.All(report.Effects, effect => Assert.Equal(
            effect.FinalProviderKinds.Count,
            effect.FinalProviderKinds.Distinct(StringComparer.Ordinal).Count()));
        Assert.Contains(report.Effects, effect =>
            effect.FinalProviderKinds.Contains("Pseudo", StringComparer.Ordinal));
        Assert.Contains(report.Effects, effect =>
            effect.Trace.Any(trace =>
                trace.ProviderKind == "pseudo" &&
                trace.RejectionReason.StartsWith(
                    PathOfExileTradeModifierVariantDiscovery.SemanticMismatch,
                    StringComparison.Ordinal)));
    }

    private static PathOfExileTradeModifierVariantAuditSource Find(
        IReadOnlyList<PathOfExileTradeModifierVariantAuditSource> sources,
        Func<PathOfExileTradeModifierVariantAuditSource, bool> predicate,
        string label)
    {
        var source = sources.FirstOrDefault(predicate);
        Assert.True(
            source is not null,
            $"Missing audit source '{label}'. Available: {string.Join(" | ", sources.Select(entry => $"{entry.Component.ParsedKind}:{entry.Component.OriginalText}"))}");
        return source! with { Label = label };
    }

    private static async Task<IReadOnlyList<PathOfExileTradeModifierVariantAuditSource>>
        LoadProductionSources(PathOfExileTradeStatCatalog tradeCatalog)
    {
        var gameDataResult = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(gameDataResult.IsSuccess);
        Assert.NotNull(gameDataResult.Package);
        var gameDataCatalog = GameDataCatalog.FromPackage(gameDataResult.Package!);
        var parser = new ItemTextParser();
        var displayService = new ParsedItemGameDataDisplayService();
        var draftMapper = new TradeSearchDraftMapper();
        var matcher = new PathOfExileTradeStatMatcher();
        var results = new List<PathOfExileTradeModifierVariantAuditSource>();
        var itemTexts = OrdinaryItemCorpus.Load().Blocks
            .Select(block => block.RawText)
            .Append(PathOfExileTradeQueryBuilderCategoryProductionTests.ArmageddonThirstCraftedAttackSpeedText)
            .Append(LoadAdvancedCorpusItem(4))
            .Append(Features.PriceChecking.PriceCheckerProductionPathCorpusTests
                .HorrorManglerExplicitAndCraftedPhysicalDamageText);

        foreach (var itemText in itemTexts)
        {
            var parsed = parser.Parse(itemText);
            var baseResolution = displayService.ResolveItemBase(parsed, gameDataCatalog).Result;
            var modifierResolutions = displayService
                .ResolveModifierCandidates(parsed, gameDataCatalog, baseResolution)
                .Results
                .Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray();
            var draftResult = draftMapper.CreateDraft(
                parsed,
                baseResolution,
                modifierResolutions,
                gameDataCatalog);
            if (!draftResult.IsSuccess || draftResult.Draft is null)
            {
                continue;
            }

            foreach (var component in draftResult.Draft.ModifierFilters.Where(CanResolve))
            {
                var match = matcher.Match(
                    component,
                    tradeCatalog,
                    new PathOfExileTradeStatMatchContext
                    {
                        ItemClass = draftResult.Draft.ItemClass,
                        ParsedBaseType = draftResult.Draft.ParsedBaseType,
                        ModifierLocality = component.Locality,
                        ResolvedModifierId = component.ResolvedModifierId,
                        ResolvedModifierName = component.ResolvedModifierName,
                        InternalStatIds = component.ResolvedStatIds,
                    });
                if (match.Status == PathOfExileTradeStatMatchStatus.Exact &&
                    match.ExactCandidate is not null)
                {
                    results.Add(new PathOfExileTradeModifierVariantAuditSource
                    {
                        Label = component.ComponentId,
                        Component = component,
                        SourceExactCandidate = match.ExactCandidate,
                    });
                }
            }
        }

        return results;
    }

    private static bool CanResolve(ResolvedSearchComponent component) =>
        component.ParsedKind == ParsedModifierKind.Implicit &&
        !string.IsNullOrWhiteSpace(component.CanonicalSignature) &&
        !string.IsNullOrWhiteSpace(component.OriginalText) ||
        component.IsSearchable &&
        component.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
        !string.IsNullOrWhiteSpace(component.ResolvedModifierId) &&
        component.ResolvedStatIds.Count > 0;

    private static string LoadAdvancedCorpusItem(int index)
    {
        var corpus = File.ReadAllText(FindRepoFile(
            "PoEnhance.Core.Tests",
            "TestData",
            "Items",
            "advanced-real-items-corpus.txt"));
        return new Regex(@"\r?\n\s*\r?\n(?=Item Class:)", RegexOptions.CultureInvariant)
            .Split(corpus.TrimEnd('\r', '\n'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ElementAt(index);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file: {Path.Combine(relativeParts)}");
    }

    private static string FindRepoDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the PoEnhance repository root.");
    }
}
