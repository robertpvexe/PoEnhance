using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Features.PriceChecking;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradePseudoVariantCompatibilityTests
{
    [Fact]
    public void OfficialTotalAttackSpeedTemplate_IsRejectedForReviewedLocalDisplayedProperty()
    {
        var component = ScalarComponent("20% increased Attack Speed", "#% increased Attack Speed") with
        {
            ReviewedItemPropertySemantic = new ItemPropertySemanticDescriptor
            {
                Id = "reviewed.local-attack-speed",
                Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
            },
        };
        var source = Candidate("explicit.stat_210067635", "#% increased Attack Speed (Local)", "Explicit");
        var pseudo = Candidate(
            "pseudo.pseudo_total_attack_speed",
            "+#% total Attack Speed",
            "Pseudo");

        var result = PathOfExileTradePseudoVariantCompatibility.Evaluate(component, source, pseudo);

        Assert.False(result.IsCompatible);
        Assert.Equal(
            PathOfExileTradeProviderLocalityCompatibility.LocalDisplayedScopeUnproven,
            result.RejectionCode);
        Assert.Equal("#% increased Attack Speed", result.SourceNormalizedTemplate);
        Assert.Equal("+#% total Attack Speed", result.CandidateNormalizedTemplate);
        Assert.Equal("attack speed", result.SourceLogicalEffect);
        Assert.Equal("attack speed", result.CandidateLogicalEffect);
        Assert.Equal("increased attack speed", result.LegacySourceLogicalEffect);
        Assert.Equal("attack speed", result.LegacyCandidateLogicalEffect);
        Assert.False(result.LegacyDiscoveryCompatible);
        Assert.Equal("pseudo", result.ProviderKind);
        Assert.Equal(PathOfExileTradeProviderStatLocality.Local, result.SourceLocality);
        Assert.Equal(PathOfExileTradeProviderStatLocality.Unmarked, result.CandidateLocality);
        Assert.Equal(["+%"], result.SourceNumericSemantics);
        Assert.Equal(["+%"], result.CandidateNumericSemantics);
        Assert.True(result.HasTotalOrCombinedMarker);
        Assert.Equal(ModifierBoundDirection.Minimum, result.BoundDirection);
        Assert.Equal(ModifierBoundShape.Scalar, result.ValueShape);
        Assert.Empty(Assert.Single(result.TranslationHandlers));
        Assert.Equal(result.MaximumCompatibilityScore - 1, result.CompatibilityScore);
    }

    [Fact]
    public void OfficialTotalResistanceTemplate_MatchesSameLogicalProperty()
    {
        var component = ScalarComponent("+38% to Fire Resistance", "+#% to Fire Resistance");
        var source = Candidate("explicit.fire-resistance", "+#% to Fire Resistance", "Explicit");
        var pseudo = Candidate(
            "pseudo.pseudo_total_fire_resistance",
            "+#% total to Fire Resistance",
            "Pseudo");

        var result = PathOfExileTradePseudoVariantCompatibility.Evaluate(component, source, pseudo);

        Assert.True(result.IsCompatible);
        Assert.Equal("fire resistance", result.SourceLogicalEffect);
        Assert.Equal("fire resistance", result.CandidateLogicalEffect);
    }

    [Theory]
    [InlineData("maximum Life")]
    [InlineData("maximum Mana")]
    [InlineData("maximum Energy Shield")]
    [InlineData("Armour")]
    [InlineData("Evasion Rating")]
    public void OfficialFlatTotalTemplate_MatchesLeadingContributionPreposition(string property)
    {
        var component = ScalarComponent($"+20 to {property}", $"+# to {property}");
        var source = Candidate($"explicit.{property}", $"+# to {property}", "Explicit");
        var pseudo = Candidate($"pseudo.{property}", $"+# total {property}", "Pseudo");

        var result = PathOfExileTradePseudoVariantCompatibility.Evaluate(component, source, pseudo);

        Assert.True(result.IsCompatible);
        Assert.Equal(property.ToLowerInvariant(), result.SourceLogicalEffect);
        Assert.Equal(property.ToLowerInvariant(), result.CandidateLogicalEffect);
    }

    [Fact]
    public void OfficialTotalAllAttributesTemplate_MatchesAllAttributesLogicalEffect()
    {
        var component = ScalarComponent("+20 to all Attributes", "+# to all Attributes");
        var source = Candidate("explicit.all-attributes", "+# to all Attributes", "Explicit");
        var pseudo = Candidate(
            "pseudo.pseudo_total_all_attributes",
            "+# total to all Attributes",
            "Pseudo");

        var result = PathOfExileTradePseudoVariantCompatibility.Evaluate(component, source, pseudo);

        Assert.True(result.IsCompatible);
        Assert.Equal("all attributes", result.SourceLogicalEffect);
        Assert.Equal("all attributes", result.CandidateLogicalEffect);
    }

    [Fact]
    public void RelationalDamageTargetPhrase_IsNotErasedByLeadingPrepositionNormalization()
    {
        var component = ScalarComponent(
            "Adds 10 to 20 Fire Damage to Attacks",
            "Adds # to # Fire Damage to Attacks") with
        {
            ValueBoundShape = ModifierBoundShape.ArithmeticMeanRange,
            ObservedNumericValues = [10m, 20m],
            ValueBoundTranslationHandlers = [[], []],
        };
        var source = Candidate(
            "explicit.fire-attacks",
            "Adds # to # Fire Damage to Attacks",
            "Explicit");
        var unrelated = Candidate(
            "pseudo.fire-spells",
            "Adds # to # Fire Damage to Spells",
            "Pseudo");

        var result = PathOfExileTradePseudoVariantCompatibility.Evaluate(component, source, unrelated);

        Assert.False(result.IsCompatible);
        Assert.Equal(
            PathOfExileTradePseudoVariantCompatibility.DifferentLogicalEffect,
            result.RejectionCode);
    }

    [Fact]
    public void SimilarlyShapedTotalCastSpeed_RemainsRejectedForAttackSpeed()
    {
        var component = ScalarComponent("20% increased Attack Speed", "#% increased Attack Speed");
        var source = Candidate("explicit.attack-speed", "#% increased Attack Speed (Local)", "Explicit");
        var pseudo = Candidate("pseudo.total-cast-speed", "+#% total Cast Speed", "Pseudo");

        var result = PathOfExileTradePseudoVariantCompatibility.Evaluate(component, source, pseudo);

        Assert.False(result.IsCompatible);
        Assert.Equal(
            PathOfExileTradePseudoVariantCompatibility.DifferentLogicalEffect,
            result.RejectionCode);
        Assert.Equal("attack speed", result.SourceLogicalEffect);
        Assert.Equal("cast speed", result.CandidateLogicalEffect);
    }

    [Fact]
    public void AuditReport_PartitionsPseudoCatalogAndCountsDeduplicationAndRejections()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "pseudo.attack-speed", "+#% total Attack Speed", "Pseudo"),
            Entry(1, "pseudo.attack-speed", "+#% total Attack Speed", "Pseudo"),
            Entry(2, "pseudo.physical", "#% total increased Physical Damage", "Pseudo"),
            Entry(3, "pseudo.fire-resistance", "+#% total to Fire Resistance", "Pseudo"),
            Entry(4, "pseudo.cast-speed", "+#% total Cast Speed", "Pseudo"),
            Entry(5, "pseudo.flat-attack-speed", "+# total Attack Speed", "Pseudo"),
        ]);
        var sources = new[]
        {
            AuditSource(
                ScalarComponent("20% increased Attack Speed", "#% increased Attack Speed"),
                Candidate("explicit.attack-speed", "#% increased Attack Speed (Local)", "Explicit")),
            AuditSource(
                ScalarComponent("91% increased Physical Damage", "#% increased Physical Damage"),
                Candidate("explicit.physical", "#% increased Physical Damage", "Explicit")),
            AuditSource(
                ScalarComponent("+38% to Fire Resistance", "+#% to Fire Resistance"),
                Candidate("explicit.fire-resistance", "+#% to Fire Resistance", "Explicit")),
        };

        var report = PathOfExileTradePseudoVariantCatalogAuditor.Audit(catalog, sources);

        Assert.Equal(6, report.TotalOfficialPseudoStatsInspected);
        Assert.Equal(5, report.DistinctPseudoProviderStatCount);
        Assert.Equal(3, report.MatchedToLogicalEffectCount);
        Assert.Equal(1, report.UnreachableCount);
        Assert.Equal(1, report.RejectedIncompatibleCount);
        Assert.Equal(1, report.DuplicateProviderIdentitiesRemoved);
        Assert.Equal(1, report.NewlyCompatibleCount);
        Assert.Equal(
            1,
            report.RejectionReasonCounts[PathOfExileTradePseudoVariantCompatibility.IncompatibleNumericUnit]);
    }

    [Fact]
    public void AuditReport_RecordsMultipleCompatiblePseudoIdentitiesAsAmbiguous()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry(0, "pseudo.attack-speed-a", "+#% total Attack Speed", "Pseudo"),
            Entry(1, "pseudo.attack-speed-b", "+#% combined Attack Speed", "Pseudo"),
        ]);
        var source = AuditSource(
            ScalarComponent("20% increased Attack Speed", "#% increased Attack Speed"),
            Candidate("explicit.attack-speed", "#% increased Attack Speed (Local)", "Explicit"));

        var report = PathOfExileTradePseudoVariantCatalogAuditor.Audit(catalog, [source]);

        var ambiguity = Assert.Single(report.Ambiguities);
        Assert.Equal(
            ["pseudo.attack-speed-a", "pseudo.attack-speed-b"],
            ambiguity.CompatiblePseudoStatIds);
    }

    [Fact]
    public void ProductionPseudoCompatibilityContainsNoEffectOrProviderIdExceptions()
    {
        var compatibilitySource = File.ReadAllText(FindRepoFile(
            "PoEnhance.App",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradePseudoVariantCompatibility.cs"));
        var resolverSource = File.ReadAllText(FindRepoFile(
            "PoEnhance.App",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradeModifierVariantResolver.cs"));
        var discoverySource = File.ReadAllText(FindRepoFile(
            "PoEnhance.App",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradeModifierVariantDiscovery.cs"));
        var evidenceSource = File.ReadAllText(FindRepoFile(
            "PoEnhance.Core",
            "Trade",
            "ModifierProviderDomainEvidenceResolver.cs"));
        var localitySource = File.ReadAllText(FindRepoFile(
            "PoEnhance.App",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradeProviderLocalityCompatibility.cs"));
        var productionSource =
            $"{compatibilitySource}\n{resolverSource}\n{discoverySource}\n{evidenceSource}\n{localitySource}";

        Assert.DoesNotContain("Attack Speed", productionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Armageddon Thirst", productionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reaver Axe", productionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pseudo_total_attack_speed", productionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stat_210067635", productionSource, StringComparison.OrdinalIgnoreCase);
    }

    private static ResolvedSearchComponent ScalarComponent(string text, string signature)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = $"component:{signature}",
            OriginalText = text,
            CanonicalSignature = signature,
            ParsedKind = ParsedModifierKind.Suffix,
            Locality = ModifierLocality.Local,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = $"mod:{signature}",
            ResolvedStatIds = [$"stat:{signature}"],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [20m],
            ValueBoundTranslationHandlers = [[]],
            DefaultBoundDirection = ModifierBoundDirection.Minimum,
            RequestedMinimum = 20m,
        };
    }

    private static PathOfExileTradePseudoVariantAuditSource AuditSource(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate candidate) => new()
        {
            Component = component,
            SourceExactCandidate = candidate,
        };

    private static PathOfExileTradeStatMatchCandidate Candidate(
        string id,
        string text,
        string kind) => PathOfExileTradeStatCandidateClassifier.ToCandidate(Entry(0, id, text, kind));

    private static PathOfExileTradeStatEntry Entry(int order, string id, string text, string kind) => new()
    {
        ProviderOrder = order,
        GroupId = kind.ToLowerInvariant(),
        GroupLabel = kind,
        Id = id,
        Text = text,
        Type = kind.ToLowerInvariant(),
    };

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
}

public sealed class PathOfExileTradePseudoVariantLiveCatalogAuditTests
{
    [Fact]
    public async Task OfficialCatalog_OptInPseudoCompatibilityAuditAgainstProductionCorpus()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("POENHANCE_RUN_LIVE_PSEUDO_AUDIT"),
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
        var logicalEffects = await LoadProductionCorpusLogicalEffects(catalogResult.Catalog!);

        var report = PathOfExileTradePseudoVariantCatalogAuditor.Audit(
            catalogResult.Catalog!,
            logicalEffects);
        var representativeIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "pseudo.pseudo_total_attack_speed",
            "pseudo.pseudo_increased_physical_damage",
            "pseudo.pseudo_total_fire_resistance",
            "pseudo.pseudo_total_all_attributes",
            "pseudo.pseudo_total_life",
            "pseudo.pseudo_total_mana",
            "pseudo.pseudo_total_energy_shield",
            "pseudo.pseudo_adds_physical_damage",
            "pseudo.pseudo_total_armour",
            "pseudo.pseudo_total_evasion",
            "pseudo.pseudo_total_cast_speed",
        };
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            report.TotalOfficialPseudoStatsInspected,
            report.DistinctPseudoProviderStatCount,
            report.MatchedToLogicalEffectCount,
            report.UnreachableCount,
            report.RejectedIncompatibleCount,
            report.NewlyCompatibleCount,
            report.DuplicateProviderIdentitiesRemoved,
            report.RejectionReasonCounts,
            RepresentativeEntries = report.Entries
                .Where(entry => representativeIds.Contains(entry.StatId))
                .ToArray(),
            RejectedEntries = report.Entries
                .Where(entry => entry.Classification ==
                    PathOfExileTradePseudoVariantAuditClassification.RejectedIncompatible)
                .ToArray(),
            NewlyCompatibleEntries = report.Entries
                .Where(entry => entry.WasUnreachableByLegacyDiscovery)
                .ToArray(),
            report.Ambiguities,
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        }));

        Assert.True(report.TotalOfficialPseudoStatsInspected > 200);
        Assert.Equal(
            report.DistinctPseudoProviderStatCount,
            report.MatchedToLogicalEffectCount +
            report.UnreachableCount +
            report.RejectedIncompatibleCount);
        Assert.Contains(report.Entries, entry =>
            entry.StatId == "pseudo.pseudo_total_attack_speed" &&
            entry.Classification == PathOfExileTradePseudoVariantAuditClassification.Matched);
        Assert.Contains(report.Entries, entry =>
            entry.StatId == "pseudo.pseudo_increased_physical_damage" &&
            entry.Classification == PathOfExileTradePseudoVariantAuditClassification.Matched);
        Assert.Contains(report.Entries, entry =>
            entry.StatId == "pseudo.pseudo_total_fire_resistance" &&
            entry.Classification == PathOfExileTradePseudoVariantAuditClassification.Matched);
    }

    private static async Task<IReadOnlyList<PathOfExileTradePseudoVariantAuditSource>>
        LoadProductionCorpusLogicalEffects(PathOfExileTradeStatCatalog tradeCatalog)
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
        var logicalEffects = new List<PathOfExileTradePseudoVariantAuditSource>();

        var itemTexts = OrdinaryItemCorpus.Load().Blocks
            .Select(block => block.RawText)
            .Append(PriceCheckerProductionPathCorpusTests.HorrorManglerExplicitAndCraftedPhysicalDamageText);
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
                    logicalEffects.Add(new PathOfExileTradePseudoVariantAuditSource
                    {
                        Component = component,
                        SourceExactCandidate = match.ExactCandidate,
                    });
                }
            }
        }

        return logicalEffects
            .DistinctBy(source => string.Join(
                '\u001f',
                source.SourceExactCandidate.StatId,
                source.Component.CanonicalSignature,
                source.Component.ValueBoundShape,
                source.Component.DefaultBoundDirection))
            .ToArray();
    }

    private static bool CanResolve(ResolvedSearchComponent component) =>
        component.IsSearchable &&
        component.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
        !string.IsNullOrWhiteSpace(component.ResolvedModifierId) &&
        component.ResolvedStatIds.Count > 0;

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
}
