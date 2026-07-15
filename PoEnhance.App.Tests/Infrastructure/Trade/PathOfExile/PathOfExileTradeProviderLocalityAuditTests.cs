using System.Text.Json;
using System.Text.RegularExpressions;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Features.PriceChecking;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeProviderLocalityAuditTests
{
    [Fact]
    public async Task OfficialCatalog_OptInNarrowProviderLocalityAudit()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("POENHANCE_RUN_PROVIDER_LOCALITY_AUDIT"),
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
        var catalog = catalogResult.Catalog!;
        var sources = await LoadNarrowProductionSources(catalog);
        var entries = sources
            .SelectMany(source => catalog
                .FindCandidatesByLogicalEffect(
                    PathOfExileTradePseudoVariantCompatibility.LogicalEffectIdentity(
                        source.SourceCandidate.Text))
                .Select(candidate => Audit(source, candidate)))
            .Where(entry => entry.IsOtherwiseSemanticallyCompatible)
            .ToArray();

        var safelyResolvedLocal = entries.Count(entry =>
            entry.ProviderLocality == PathOfExileTradeProviderStatLocality.Unmarked &&
            entry.Decision.Status == PathOfExileTradeProviderLocalityDecisionStatus.Compatible &&
            entry.Decision.EffectiveLocality == ModifierLocality.Local);
        var safelyResolvedGlobal = entries.Count(entry =>
            entry.ProviderLocality == PathOfExileTradeProviderStatLocality.Unmarked &&
            entry.Decision.Status == PathOfExileTradeProviderLocalityDecisionStatus.Compatible &&
            entry.Decision.EffectiveLocality == ModifierLocality.Global);
        var ambiguous = entries.Count(entry =>
            entry.ProviderLocality == PathOfExileTradeProviderStatLocality.Unmarked &&
            entry.Decision.Status == PathOfExileTradeProviderLocalityDecisionStatus.Ambiguous);
        var explicitConflicts = entries.Count(entry =>
            entry.Decision.ReasonCode ==
                PathOfExileTradeProviderLocalityCompatibility.ExplicitLocalityConflict);
        var insufficient = entries.Count(entry =>
            entry.ProviderLocality == PathOfExileTradeProviderStatLocality.Unmarked &&
            entry.Decision.Status == PathOfExileTradeProviderLocalityDecisionStatus.InsufficientEvidence);
        var changedFromPreviousBehavior = entries.Count(entry =>
            entry.ProviderLocality == PathOfExileTradeProviderStatLocality.Unmarked &&
            entry.Decision.IsCompatible != entry.PreviouslyCompatible);
        var representativeEffects = entries
            .Where(entry => entry.Decision.IsCompatible)
            .Select(entry => new
            {
                entry.SourceText,
                entry.ProviderKind,
                ProviderTemplate = entry.CandidateText,
                entry.Decision.EffectiveLocality,
            })
            .Distinct()
            .Take(40)
            .ToArray();
        var representativeFamilyCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["increased Physical Damage"] = FamilyCount(entries, "increased Physical Damage"),
            ["added Physical Damage"] = entries.Count(entry =>
                entry.Decision.IsCompatible &&
                entry.SourceText.Contains("Adds", StringComparison.Ordinal) &&
                entry.SourceText.Contains("Physical Damage", StringComparison.Ordinal)),
            ["added Fire Damage"] = FamilyCount(entries, "Fire Damage"),
            ["added Cold Damage"] = FamilyCount(entries, "Cold Damage"),
            ["added Lightning Damage"] = FamilyCount(entries, "Lightning Damage"),
            ["Attack Speed"] = FamilyCount(entries, "Attack Speed"),
            ["Critical Strike Chance"] = FamilyCount(entries, "Critical Strike Chance"),
            ["Accuracy Rating"] = FamilyCount(entries, "Accuracy Rating"),
            ["Leech"] = FamilyCount(entries, "Leech"),
            ["Armour"] = FamilyCount(entries, "Armour"),
            ["Evasion Rating"] = FamilyCount(entries, "Evasion Rating"),
            ["Energy Shield"] = FamilyCount(entries, "Energy Shield"),
            ["Attributes"] = FamilyCount(entries, "Strength", "Dexterity", "Intelligence", "Attributes"),
            ["Resistances"] = FamilyCount(entries, "Resistance"),
            ["Stun and Block Recovery"] = FamilyCount(entries, "Stun and Block Recovery"),
            ["Critical Strike Multiplier"] = FamilyCount(entries, "Critical Strike Multiplier"),
        };

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            SourcesInspected = sources.Count,
            OtherwiseSemanticCandidates = entries.Length,
            SafelyResolvedLocal = safelyResolvedLocal,
            SafelyResolvedGlobal = safelyResolvedGlobal,
            AmbiguousLocalGlobal = ambiguous,
            RejectedExplicitConflict = explicitConflicts,
            RejectedInsufficientEvidence = insufficient,
            ChangedFromPreviousBehavior = changedFromPreviousBehavior,
            RepresentativeFamilyCounts = representativeFamilyCounts,
            RepresentativeEffects = representativeEffects,
        }, new JsonSerializerOptions { WriteIndented = true }));

        Assert.True(sources.Count > 0);
        Assert.True(safelyResolvedLocal > 0);
        Assert.True(safelyResolvedGlobal > 0);
        Assert.True(changedFromPreviousBehavior > 0);
        Assert.Contains(entries, entry =>
            entry.Decision.IsCompatible &&
            entry.Decision.EffectiveLocality == ModifierLocality.Local &&
            entry.SourceText.Contains("Physical Damage", StringComparison.Ordinal));
        Assert.Contains(entries, entry =>
            entry.Decision.IsCompatible &&
            entry.Decision.EffectiveLocality == ModifierLocality.Local &&
            entry.SourceText.Contains("Accuracy Rating", StringComparison.Ordinal));
        Assert.Contains(entries, entry =>
            entry.Decision.IsCompatible &&
            entry.Decision.EffectiveLocality == ModifierLocality.Local &&
            (entry.SourceText.Contains("Armour", StringComparison.Ordinal) ||
                entry.SourceText.Contains("Evasion", StringComparison.Ordinal) ||
                entry.SourceText.Contains("Energy Shield", StringComparison.Ordinal)));
        Assert.Contains(entries, entry =>
            entry.Decision.IsCompatible &&
            entry.Decision.EffectiveLocality == ModifierLocality.Global &&
            entry.SourceText.Contains("Stun and Block Recovery", StringComparison.Ordinal));
    }

    private static int FamilyCount(
        IEnumerable<LocalityAuditEntry> entries,
        params string[] fragments)
    {
        return entries.Count(entry =>
            entry.Decision.IsCompatible &&
            fragments.Any(fragment => entry.SourceText.Contains(fragment, StringComparison.Ordinal)));
    }

    private static LocalityAuditEntry Audit(
        AuditSource source,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        var compatibility = PathOfExileTradePseudoVariantCompatibility.EvaluateVariant(
            source.Component,
            source.SourceCandidate,
            candidate);
        var decision = compatibility.LocalityDecision;
        var isLocalityOutcome = compatibility.IsCompatible ||
            compatibility.RejectionCode is
                PathOfExileTradeProviderLocalityCompatibility.ExplicitLocalityConflict or
                PathOfExileTradeProviderLocalityCompatibility.AmbiguousLocalityEvidence or
                PathOfExileTradeProviderLocalityCompatibility.InsufficientLocalityEvidence;
        var providerKind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        var previouslyCompatible = string.Equals(providerKind, "pseudo", StringComparison.Ordinal) ||
            source.Component.Locality switch
            {
                ModifierLocality.Local =>
                    candidate.ProviderLocality == PathOfExileTradeProviderStatLocality.Local,
                ModifierLocality.Global =>
                    candidate.ProviderLocality == PathOfExileTradeProviderStatLocality.Unmarked,
                _ => false,
            };
        return new LocalityAuditEntry(
            source.Component.OriginalText,
            candidate.Text,
            providerKind,
            candidate.ProviderLocality,
            decision,
            isLocalityOutcome,
            previouslyCompatible);
    }

    private static async Task<IReadOnlyList<AuditSource>> LoadNarrowProductionSources(
        PathOfExileTradeStatCatalog tradeCatalog)
    {
        var gameDataResult = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(gameDataResult.IsSuccess && gameDataResult.Package is not null);
        var gameDataCatalog = GameDataCatalog.FromPackage(gameDataResult.Package!);
        var parser = new ItemTextParser();
        var displayService = new ParsedItemGameDataDisplayService();
        var draftMapper = new TradeSearchDraftMapper();
        var matcher = new PathOfExileTradeStatMatcher();
        var results = new List<AuditSource>();
        var itemTexts = OrdinaryItemCorpus.Load().Blocks
            .Select(block => block.RawText)
            .Concat(LoadAdvancedCorpusItems())
            .Append(PathOfExileTradeQueryBuilderCategoryProductionTests
                .ArmageddonThirstCraftedAttackSpeedText)
            .Append(PriceCheckerProductionPathCorpusTests
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
                var match = matcher.Match(component, tradeCatalog, new PathOfExileTradeStatMatchContext
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
                    results.Add(new AuditSource(component, match.ExactCandidate));
                }
            }
        }

        return results;
    }

    private static bool CanResolve(ResolvedSearchComponent component) =>
        component.IsSearchable &&
        component.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
        !string.IsNullOrWhiteSpace(component.ResolvedModifierId) &&
        component.ResolvedStatIds.Count > 0;

    private static IEnumerable<string> LoadAdvancedCorpusItems()
    {
        var corpus = File.ReadAllText(FindRepoFile(
            "PoEnhance.Core.Tests",
            "TestData",
            "Items",
            "advanced-real-items-corpus.txt"));
        return new Regex(@"\r?\n\s*\r?\n(?=Item Class:)", RegexOptions.CultureInvariant)
            .Split(corpus.TrimEnd('\r', '\n'))
            .Where(item => !string.IsNullOrWhiteSpace(item));
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

    private sealed record AuditSource(
        ResolvedSearchComponent Component,
        PathOfExileTradeStatMatchCandidate SourceCandidate);

    private sealed record LocalityAuditEntry(
        string SourceText,
        string CandidateText,
        string ProviderKind,
        PathOfExileTradeProviderStatLocality ProviderLocality,
        PathOfExileTradeProviderLocalityDecision Decision,
        bool IsOtherwiseSemanticallyCompatible,
        bool PreviouslyCompatible);
}
