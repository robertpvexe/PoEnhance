using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal static class TimelessAblationExperiment
{
    private static readonly string[] TimelessNames =
    [
        "Lethal Pride",
        "Brutal Restraint",
        "Elegant Hubris",
        "Glorious Vanity",
        "Militant Faith",
    ];

    public static async Task<TimelessAblationReport> RunAsync(
        string corpusDirectory,
        string gameDataPackagePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDataPackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var identity = await ModifierPipelineReplayGameDataGate
            .LoadIdentityAsync(gameDataPackagePath, cancellationToken)
            .ConfigureAwait(false);
        var tradeCatalog = LoadPinnedOfficialTradeCatalog();
        // Provider-context catalog so Timeless seed IsSearchable can be asserted on the same
        // production path used by Ctrl+D regressions (empty catalog yields provider-boundary noise).
        var itemCatalog = CreateTimelessCapableTradeItemCatalog();
        var filterCatalog = PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();

        var captures = LoadTimelessCaptures(corpusDirectory);
        var fixtures = LoadFixtureTexts();
        var inputDiffs = new List<TimelessInputDiffEntry>();
        var parserDiffs = new List<TimelessParserDiffEntry>();
        var ablationRows = new List<TimelessAblationRow>();
        var reverseRows = new List<TimelessAblationRow>();

        foreach (var capture in captures)
        {
            fixtures.TryGetValue(capture.ItemName, out var fixtureText);
            fixtureText ??= BuildSyntheticFixture(capture.RawClipboardText, capture.ItemName);
            inputDiffs.Add(BuildInputDiff(capture.ItemName, capture.RawClipboardText, fixtureText));
            parserDiffs.Add(BuildParserDiff(capture.ItemName, capture.RawClipboardText, fixtureText));

            // Baseline real/fixture outcomes.
            ablationRows.Add(Evaluate(
                capture.ItemName,
                "identity_real",
                capture.RawClipboardText,
                identity,
                tradeCatalog,
                itemCatalog,
                filterCatalog));
            ablationRows.Add(Evaluate(
                capture.ItemName,
                "identity_fixture",
                fixtureText,
                identity,
                tradeCatalog,
                itemCatalog,
                filterCatalog));

            foreach (var transform in BuildForwardTransforms(capture.RawClipboardText, fixtureText))
            {
                if (!transform.IsValid || transform.Text is null)
                {
                    ablationRows.Add(new TimelessAblationRow
                    {
                        ItemName = capture.ItemName,
                        TransformName = transform.TransformName,
                        Direction = "real_to_fixture",
                        Outcome = TimelessAblationClipboard.InvalidExperimentInput,
                        InvalidReason = transform.InvalidReason,
                    });
                    continue;
                }

                ablationRows.Add(Evaluate(
                    capture.ItemName,
                    transform.TransformName,
                    transform.Text,
                    identity,
                    tradeCatalog,
                    itemCatalog,
                    filterCatalog,
                    direction: "real_to_fixture"));
            }

            foreach (var feature in new[]
                     {
                         "add_real_item_level",
                         "add_real_limited_to",
                         "add_real_radius",
                         "add_real_historic_wording",
                         "add_real_conquered_parenthetical",
                         "add_real_flavour_and_socket",
                     })
            {
                var transform = TimelessAblationClipboard.BuildGreenPlusFeature(
                    fixtureText,
                    capture.RawClipboardText,
                    feature);
                if (!transform.IsValid || transform.Text is null)
                {
                    reverseRows.Add(new TimelessAblationRow
                    {
                        ItemName = capture.ItemName,
                        TransformName = feature,
                        Direction = "fixture_to_real",
                        Outcome = TimelessAblationClipboard.InvalidExperimentInput,
                        InvalidReason = transform.InvalidReason,
                    });
                    continue;
                }

                reverseRows.Add(Evaluate(
                    capture.ItemName,
                    feature,
                    transform.Text,
                    identity,
                    tradeCatalog,
                    itemCatalog,
                    filterCatalog,
                    direction: "fixture_to_real"));
            }
        }

        var minimal = InferMinimalTrigger(ablationRows, reverseRows);
        var blast = TimelessAblationCorpusBlast.Analyze(
            Path.Combine(Path.GetTempPath(), "PoEnhance-GATE-A-UNIQUE-BigManual"),
            minimal);
        var report = new TimelessAblationReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            GameDataVersion = identity.DataVersion,
            GameDataSha256 = identity.Sha256,
            InputDiffs = inputDiffs,
            ParserDiffs = parserDiffs,
            AblationRows = ablationRows,
            ReverseAdditionRows = reverseRows,
            MinimalTrigger = minimal,
            CorpusBlast = blast,
            IntentionalMismatchControls = EvaluateIntentionalMismatchControls(
                identity,
                tradeCatalog,
                itemCatalog,
                filterCatalog),
        };

        await TimelessAblationReportWriter.WriteAsync(report, outputDirectory, cancellationToken)
            .ConfigureAwait(false);
        return report;
    }

    public static IReadOnlyList<TimelessCaptureSource> LoadTimelessCaptures(string corpusDirectory)
    {
        var results = new List<TimelessCaptureSource>();
        foreach (var path in Directory.GetFiles(corpusDirectory, "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("replayContext", out var replay) ||
                !replay.TryGetProperty("captureSchemaVersion", out var schema) ||
                schema.GetString() != ModifierPipelineReplaySchemas.KnownReplaySchemaVersion ||
                !replay.TryGetProperty("rawClipboardText", out var rawElement))
            {
                continue;
            }

            var name = document.RootElement.TryGetProperty("item", out var item) &&
                       item.TryGetProperty("displayName", out var display)
                ? display.GetString()
                : null;
            if (name is null ||
                !TimelessNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var raw = rawElement.GetString();
            if (string.IsNullOrEmpty(raw))
            {
                continue;
            }

            results.Add(new TimelessCaptureSource(Path.GetFileName(path), name, raw));
        }

        return results
            .GroupBy(capture => capture.ItemName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(capture => capture.FileName, StringComparer.Ordinal).First())
            .OrderBy(capture => Array.IndexOf(TimelessNames, capture.ItemName))
            .ToArray();
    }

    private static TimelessAblationRow Evaluate(
        string itemName,
        string transformName,
        string text,
        ModifierPipelineReplayGameDataIdentity identity,
        PathOfExileTradeStatCatalog tradeCatalog,
        PathOfExileTradeItemCatalog itemCatalog,
        PathOfExileTradeFilterCatalog filterCatalog,
        string direction = "baseline")
    {
        try
        {
            var parsed = new ItemTextParser().Parse(text);
            var execution = ModifierPipelineReplayProductionPath.Execute(
                text,
                identity.Catalog,
                tradeCatalog,
                itemCatalog,
                filterCatalog);
            var unique = execution.UniqueResolution ?? execution.ProviderDraft.UniqueItemResolution;
            var seed = SelectPrimaryUniqueComponent(execution.ProviderDraft, parsed);
            var blockCode = seed?.UniqueResolutionDiagnosticCode ??
                            unique?.ModifierBlocks.FirstOrDefault(block => !block.IsResolved)?.DiagnosticCode ??
                            unique?.ModifierBlocks.FirstOrDefault()?.DiagnosticCode;
            var hasUnscalable = parsed.Modifiers
                .Where(modifier => modifier.Kind == ParsedModifierKind.Unique)
                .Any(modifier => modifier.HasUnscalableValue);
            var outcome = ClassifyCoreOutcome(unique?.DiagnosticCode, blockCode);
            return new TimelessAblationRow
            {
                ItemName = itemName,
                TransformName = transformName,
                Direction = direction,
                InputSha256 = TimelessAblationClipboard.Hash(text),
                ParserUniqueModifierCount = parsed.Modifiers.Count(modifier =>
                    modifier.Kind == ParsedModifierKind.Unique),
                ParserSeedValueLineCount = parsed.Modifiers
                    .Where(modifier => modifier.Kind == ParsedModifierKind.Unique)
                    .Select(modifier => modifier.ValueLines.Count)
                    .DefaultIfEmpty(0)
                    .Max(),
                ParserSeedValueLines = parsed.Modifiers
                    .Where(modifier => modifier.Kind == ParsedModifierKind.Unique)
                    .OrderByDescending(modifier => modifier.ValueLines.Count)
                    .SelectMany(modifier => modifier.ValueLines)
                    .Take(8)
                    .ToArray(),
                UniqueIdentityStatus = unique?.Status.ToString(),
                UniqueIdentityDiagnostic = unique?.DiagnosticCode,
                SeedBlockDiagnostic = blockCode,
                HasUnscalableValue = hasUnscalable,
                ModifierIds = seed?.ResolvedModifierId is { Length: > 0 } id
                    ? [id]
                    : seed?.Sources.Select(source => source.ResolvedModifierId)
                          .Where(value => !string.IsNullOrWhiteSpace(value))
                          .Select(value => value!)
                          .Distinct(StringComparer.Ordinal)
                          .OrderBy(value => value, StringComparer.Ordinal)
                          .ToArray()
                      ?? [],
                StatIds = seed?.ResolvedStatIds.OrderBy(value => value, StringComparer.Ordinal).ToArray() ?? [],
                IsSearchable = seed?.IsSearchable,
                Outcome = outcome,
            };
        }
        catch (Exception exception)
        {
            return new TimelessAblationRow
            {
                ItemName = itemName,
                TransformName = transformName,
                Direction = direction,
                InputSha256 = TimelessAblationClipboard.Hash(text),
                Outcome = "REPLAY_ERROR",
                InvalidReason = exception.Message,
            };
        }
    }

    private static string ClassifyCoreOutcome(
        string? uniqueIdentityDiagnostic,
        string? blockDiagnostic)
    {
        if (string.Equals(blockDiagnostic, "UNIQUE_BLOCK_VERSION_MISMATCH", StringComparison.Ordinal) ||
            string.Equals(blockDiagnostic, "UNIQUE_VERSION_NOT_FOUND", StringComparison.Ordinal) ||
            string.Equals(uniqueIdentityDiagnostic, "UNIQUE_VERSION_NOT_FOUND", StringComparison.Ordinal) ||
            string.Equals(uniqueIdentityDiagnostic, "UNIQUE_BLOCK_VERSION_MISMATCH", StringComparison.Ordinal))
        {
            return "MISMATCH";
        }

        return "EXACT";
    }

    private static ResolvedSearchComponent? SelectPrimaryUniqueComponent(
        TradeSearchDraft draft,
        ParsedItem parsed)
    {
        var uniqueComponents = draft.ModifierFilters
            .Where(component => component.ParsedKind == ParsedModifierKind.Unique)
            .ToArray();
        if (uniqueComponents.Length == 0)
        {
            return draft.ModifierFilters.FirstOrDefault();
        }

        var mismatched = uniqueComponents.FirstOrDefault(component =>
            string.Equals(
                component.UniqueResolutionDiagnosticCode,
                "UNIQUE_BLOCK_VERSION_MISMATCH",
                StringComparison.Ordinal) ||
            string.Equals(
                component.UniqueResolutionDiagnosticCode,
                "UNIQUE_VERSION_NOT_FOUND",
                StringComparison.Ordinal));
        if (mismatched is not null)
        {
            return mismatched;
        }

        // Prefer the largest Unique seed-like block (Timeless seed is multiline).
        return uniqueComponents
            .OrderByDescending(component => component.OriginalText.Split('\n').Length)
            .ThenBy(component => component.SourceModifierIndex)
            .First();
    }

    private static string ClassifyIntentionalControlOutcome(TradeSearchDraft draft)
    {
        var mismatched = draft.ModifierFilters.Any(component =>
            string.Equals(
                component.UniqueResolutionDiagnosticCode,
                "UNIQUE_BLOCK_VERSION_MISMATCH",
                StringComparison.Ordinal));
        return mismatched ? "MISMATCH" : "EXACT";
    }

    private static IEnumerable<TimelessAblationTransformResult> BuildForwardTransforms(
        string realRaw,
        string fixtureRaw)
    {
        yield return TimelessAblationClipboard.RemoveFlavour(realRaw);
        yield return TimelessAblationClipboard.RemoveSocketsSection(realRaw);
        yield return TimelessAblationClipboard.RemoveRequirementsSection(realRaw);
        yield return TimelessAblationClipboard.NormalizeItemLevel(realRaw);
        yield return TimelessAblationClipboard.RemoveLimitedTo(realRaw);
        yield return TimelessAblationClipboard.RemoveRadius(realRaw);
        yield return TimelessAblationClipboard.NormalizeHistoricWording(realRaw);
        yield return TimelessAblationClipboard.RemoveConqueredParenthetical(realRaw);
        yield return TimelessAblationClipboard.ReplaceSeedBlockWithFixtureEquivalent(realRaw, fixtureRaw);
        yield return TimelessAblationClipboard.KeepRealSeedReplaceSurroundingsWithFixture(realRaw, fixtureRaw);
        // Combined candidate suggested by parser observation: parenthetical alone.
        yield return TimelessAblationClipboard.Apply(
            realRaw,
            "remove_parenthetical_and_normalize_historic",
            lines => lines
                .Where(line => !line.StartsWith("(Conquered Passive Skills", StringComparison.Ordinal))
                .Select(line =>
                    System.Text.RegularExpressions.Regex.IsMatch(
                        line,
                        @"^Historic\s+[\u2014\-]\s+Unscalable Value$")
                        ? "Historic"
                        : line)
                .ToArray());
    }

    private static TimelessMinimalTrigger InferMinimalTrigger(
        IReadOnlyList<TimelessAblationRow> forward,
        IReadOnlyList<TimelessAblationRow> reverse)
    {
        var realExact = forward.Count(row =>
            row.TransformName == "identity_real" && row.Outcome == "EXACT");
        var realUnscalable = forward.Count(row =>
            row.TransformName == "identity_real" && row.HasUnscalableValue);
        var normalizeExact = forward
            .Where(row => row.TransformName == "normalize_historic_to_plain" && row.Outcome == "EXACT")
            .ToArray();
        var restoreExact = reverse
            .Where(row => row.TransformName == "add_real_historic_wording" && row.Outcome == "EXACT")
            .ToArray();
        var restoreMismatch = reverse
            .Where(row => row.TransformName == "add_real_historic_wording" && row.Outcome == "MISMATCH")
            .ToArray();

        // Post-A.5.6: annotation transforms preserve Exact. Pre-fix "causal repair/break"
        // counts are retained only as empty/historical contrast when mismatch no longer appears.
        var exactPreservingForward = forward
            .Where(row => row.TransformName is not ("identity_real" or "identity_fixture"))
            .Where(row => row.Outcome == "EXACT")
            .GroupBy(row => row.TransformName, StringComparer.Ordinal)
            .Select(group => new TimelessTriggerTransformStat
            {
                TransformName = group.Key,
                AffectedItemCount = group.Count(),
                ItemNames = group.Select(row => row.ItemName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            })
            .OrderByDescending(entry => entry.AffectedItemCount)
            .ThenBy(entry => entry.TransformName, StringComparer.Ordinal)
            .ToArray();

        var stillMismatchReverse = reverse
            .Where(row => row.Outcome == "MISMATCH")
            .GroupBy(row => row.TransformName, StringComparer.Ordinal)
            .Select(group => new TimelessTriggerTransformStat
            {
                TransformName = group.Key,
                AffectedItemCount = group.Count(),
                ItemNames = group.Select(row => row.ItemName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            })
            .OrderByDescending(entry => entry.AffectedItemCount)
            .ThenBy(entry => entry.TransformName, StringComparer.Ordinal)
            .ToArray();

        var postFixFamilyWide =
            realExact >= 5 &&
            realUnscalable >= 5 &&
            normalizeExact.Length >= 5 &&
            restoreExact.Length >= 5 &&
            restoreMismatch.Length == 0;

        return new TimelessMinimalTrigger
        {
            Features = ["historic_unscalable_value_aid_annotation_on_unique_seed_block"],
            EvidenceStrength = postFixFamilyWide ? "post_fix_family_wide" : "mixed",
            FamilyClassification = realExact >= 5 ? "FAMILY_WIDE" : "MULTIPLE_SUBTYPES",
            HistoricalTrigger =
                "AID `Historic — Unscalable Value` on Fixed Unique seed blocks (A.5.5 bidirectional causal proof before the production fix)",
            HistoricalEvidenceStrength = "bidirectional",
            PostFixBehavior =
                $"real Exact {realExact}/5; AID unscalable present {realUnscalable}/5; " +
                $"normalize_historic_to_plain Exact {normalizeExact.Length}/5; " +
                $"add_real_historic_wording Exact {restoreExact.Length}/5 " +
                "(annotation no longer breaks Fixed Unique compatibility)",
            ForwardExactRestoringTransforms = exactPreservingForward,
            ReverseMismatchCausingTransforms = stillMismatchReverse,
            AnnotationExactPreservingTransforms =
            [
                new TimelessTriggerTransformStat
                {
                    TransformName = "normalize_historic_to_plain",
                    AffectedItemCount = normalizeExact.Length,
                    ItemNames = normalizeExact.Select(row => row.ItemName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                },
                new TimelessTriggerTransformStat
                {
                    TransformName = "add_real_historic_wording",
                    AffectedItemCount = restoreExact.Length,
                    ItemNames = restoreExact.Select(row => row.ItemName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                },
            ],
            EarliestResponsibleLayer =
                "Historical (A.5.5): Unique Fixed-block compatibility after parse rejected AID unscalable RawText on non-TOR sibling lines during Fixed TOR projection. " +
                "Current (A.5.6): ParsedUniqueItemResolver tolerates AID `— Unscalable Value` for Fixed Unique mechanical match when cleaned SemanticText already matches.",
            DiagnosticTransition =
                "Historical: real AID `Historic — Unscalable Value` → UNIQUE_BLOCK_VERSION_MISMATCH; normalize to plain Historic restored Exact. " +
                "Current: real AID input, normalized Historic, and restored unscalable annotation all remain ExactIdentity with resolved seed ModifierIds/StatIds.",
            FutureClassInvariant =
                "Advanced Item Description unscalable-value annotations on otherwise Fixed Unique mechanical lines must not cause Unique version/block incompatibility when the cleaned mechanical ValueLines/SemanticText match a catalog Fixed block.",
            ProposedGenericRepairLayer =
                "ParsedUniqueItemResolver Fixed Unique block compatibility / unscalable-annotation handling (implemented in A.5.6)",
            ProposedGenericRepairBehavior =
                "Treat AID `— Unscalable Value` as presentation/query-bound metadata orthogonal to Fixed Unique block line identity when cleaned mechanical evidence already matches catalog Fixed lines; keep true mechanical line/version mismatches fail-closed.",
        };
    }

    private static TimelessInputDiffEntry BuildInputDiff(string itemName, string real, string fixture)
    {
        var diffs = ModifierPipelineReplayFixtureGapAnalyzer.DiffTexts(fixture, real);
        return new TimelessInputDiffEntry
        {
            ItemName = itemName,
            RealLineCount = TimelessAblationClipboard.SplitLines(real).Count,
            FixtureLineCount = TimelessAblationClipboard.SplitLines(fixture).Count,
            StructuralDifferences = diffs,
            FeatureFlags = BuildFeatureFlags(real, fixture),
        };
    }

    private static Dictionary<string, string> BuildFeatureFlags(string real, string fixture)
    {
        bool Has(string text, Func<string, bool> predicate) =>
            TimelessAblationClipboard.SplitLines(text).Any(predicate);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["limitedTo"] = $"fixture={Has(fixture, line => line.StartsWith("Limited to:", StringComparison.Ordinal))} real={Has(real, line => line.StartsWith("Limited to:", StringComparison.Ordinal))}",
            ["radius"] = $"fixture={Has(fixture, line => line.StartsWith("Radius:", StringComparison.Ordinal))} real={Has(real, line => line.StartsWith("Radius:", StringComparison.Ordinal))}",
            ["conqueredParenthetical"] = $"fixture={Has(fixture, line => line.StartsWith("(Conquered Passive Skills", StringComparison.Ordinal))} real={Has(real, line => line.StartsWith("(Conquered Passive Skills", StringComparison.Ordinal))}",
            ["historicUnscalable"] = $"fixture={Has(fixture, line => line.Contains("Unscalable Value", StringComparison.Ordinal))} real={Has(real, line => line.Contains("Unscalable Value", StringComparison.Ordinal))}",
            ["plainHistoric"] = $"fixture={Has(fixture, line => line == "Historic")} real={Has(real, line => line == "Historic")}",
            ["flavourOrSocketTail"] = $"fixture={Has(fixture, line => line.StartsWith("Place into", StringComparison.Ordinal))} real={Has(real, line => line.StartsWith("Place into", StringComparison.Ordinal))}",
        };
    }

    private static TimelessParserDiffEntry BuildParserDiff(string itemName, string real, string fixture)
    {
        var realParsed = new ItemTextParser().Parse(real);
        var fixtureParsed = new ItemTextParser().Parse(fixture);
        var realUnique = realParsed.Modifiers.Where(modifier => modifier.Kind == ParsedModifierKind.Unique).ToArray();
        var fixtureUnique = fixtureParsed.Modifiers.Where(modifier => modifier.Kind == ParsedModifierKind.Unique).ToArray();
        var realSeed = realUnique.OrderByDescending(modifier => modifier.ValueLines.Count).FirstOrDefault();
        var fixtureSeed = fixtureUnique.OrderByDescending(modifier => modifier.ValueLines.Count).FirstOrDefault();
        return new TimelessParserDiffEntry
        {
            ItemName = itemName,
            RealUniqueModifierCount = realUnique.Length,
            FixtureUniqueModifierCount = fixtureUnique.Length,
            RealSeedValueLineCount = realSeed?.ValueLines.Count ?? 0,
            FixtureSeedValueLineCount = fixtureSeed?.ValueLines.Count ?? 0,
            RealSeedValueLines = realSeed?.ValueLines.ToArray() ?? [],
            FixtureSeedValueLines = fixtureSeed?.ValueLines.ToArray() ?? [],
            FirstLikelyInfluencingDifference =
                DescribeFirstParserDifference(realSeed, fixtureSeed),
        };
    }

    private static string DescribeFirstParserDifference(
        ParsedModifier? realSeed,
        ParsedModifier? fixtureSeed)
    {
        var realLines = realSeed?.ValueLines ?? [];
        var fixtureLines = fixtureSeed?.ValueLines ?? [];
        if (realSeed?.HasUnscalableValue == true && fixtureSeed?.HasUnscalableValue != true)
        {
            return "Real Unique seed has HasUnscalableValue=true (AID `Historic — Unscalable Value`) while fixture plain Historic does not; cleaned ValueLines can still be identical.";
        }

        if (realLines.Count != fixtureLines.Count)
        {
            return $"Real seed ValueLine count {realLines.Count} vs fixture {fixtureLines.Count}.";
        }

        for (var index = 0; index < realLines.Count; index++)
        {
            if (!string.Equals(realLines[index], fixtureLines[index], StringComparison.Ordinal))
            {
                return $"ValueLine[{index}] differs: real=[{realLines[index]}] fixture=[{fixtureLines[index]}]";
            }
        }

        return "No Unique seed ValueLine text differences; inspect HasUnscalableValue/RawText AID annotations.";
    }

    private static string BuildSyntheticFixture(string realRaw, string itemName)
    {
        // Elegant Hubris has no green theory fixture; synthesize the same reduced shape used by other green fixtures.
        var lines = TimelessAblationClipboard.SplitLines(realRaw);
        var seed = TimelessAblationClipboard.ExtractUniqueSeedBlock(lines)
            .Where(line => !line.StartsWith("(Conquered Passive Skills", StringComparison.Ordinal))
            .Select(line =>
                System.Text.RegularExpressions.Regex.IsMatch(
                    line,
                    @"^Historic\s+[\u2014\-]\s+Unscalable Value$")
                    ? "Historic"
                    : line)
            .ToArray();
        var header = new List<string>
        {
            "Item Class: Jewels",
            "Rarity: Unique",
            itemName,
            "Timeless Jewel",
            "--------",
            "Item Level: 86",
            "--------",
        };
        header.AddRange(seed);
        header.Add(string.Empty);
        return string.Join('\n', header);
    }

    private static Dictionary<string, string> LoadFixtureTexts()
    {
        var source = FindRepoFile(
            "PoEnhance.App.Tests",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradeRawRuntimeRegressionTests.cs");
        var text = File.ReadAllText(source);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, string constName)
        {
            var marker = $"private const string {constName} = \"\"\"";
            var start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return;
            }

            start += marker.Length;
            var end = text.IndexOf("\"\"\";", start, StringComparison.Ordinal);
            map[name] = text[start..end].Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart('\n');
        }

        Add("Lethal Pride", "LethalPrideText");
        Add("Brutal Restraint", "BrutalRestraintText");
        Add("Glorious Vanity", "GloriousVanityText");
        Add("Militant Faith", "MilitantFaithText");
        return map;
    }

    private static IReadOnlyList<TimelessAblationRow> EvaluateIntentionalMismatchControls(
        ModifierPipelineReplayGameDataIdentity identity,
        PathOfExileTradeStatCatalog tradeCatalog,
        PathOfExileTradeItemCatalog itemCatalog,
        PathOfExileTradeFilterCatalog filterCatalog)
    {
        var source = FindRepoFile(
            "PoEnhance.App.Tests",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradeRawRuntimeRegressionTests.cs");
        var text = File.ReadAllText(source);
        string Extract(string constName)
        {
            var marker = $"private const string {constName} = \"\"\"";
            var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var end = text.IndexOf("\"\"\";", start, StringComparison.Ordinal);
            return text[start..end].Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart('\n');
        }

        return
        [
            EvaluateIntentional(
                "Replica Bated Breath",
                Extract("ReplicaBatedBreathText"),
                identity,
                tradeCatalog,
                itemCatalog,
                filterCatalog),
            EvaluateIntentional(
                "Augyre",
                Extract("AugyreText"),
                identity,
                tradeCatalog,
                itemCatalog,
                filterCatalog),
        ];
    }

    private static TimelessAblationRow EvaluateIntentional(
        string itemName,
        string text,
        ModifierPipelineReplayGameDataIdentity identity,
        PathOfExileTradeStatCatalog tradeCatalog,
        PathOfExileTradeItemCatalog itemCatalog,
        PathOfExileTradeFilterCatalog filterCatalog)
    {
        var execution = ModifierPipelineReplayProductionPath.Execute(
            text,
            identity.Catalog,
            tradeCatalog,
            itemCatalog,
            filterCatalog);
        var mismatched = execution.ProviderDraft.ModifierFilters
            .Where(component =>
                string.Equals(
                    component.UniqueResolutionDiagnosticCode,
                    "UNIQUE_BLOCK_VERSION_MISMATCH",
                    StringComparison.Ordinal))
            .ToArray();
        var sample = mismatched.FirstOrDefault() ?? execution.ProviderDraft.ModifierFilters.FirstOrDefault();
        return new TimelessAblationRow
        {
            ItemName = itemName,
            TransformName = "intentional_version_mismatch_control",
            Direction = "negative_control",
            InputSha256 = TimelessAblationClipboard.Hash(text),
            UniqueIdentityStatus = execution.UniqueResolution?.Status.ToString(),
            UniqueIdentityDiagnostic = execution.UniqueResolution?.DiagnosticCode,
            SeedBlockDiagnostic = sample?.UniqueResolutionDiagnosticCode,
            IsSearchable = sample?.IsSearchable,
            Outcome = mismatched.Length > 0 ? "MISMATCH" : "EXACT",
        };
    }

    private static PathOfExileTradeStatCatalog LoadPinnedOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var result = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        if (!result.IsSuccess || result.Catalog is null)
        {
            throw new InvalidOperationException("Pinned official Trade catalog failed to load.");
        }

        return result.Catalog;
    }

    private static PathOfExileTradeItemCatalog CreateTimelessCapableTradeItemCatalog()
    {
        static PathOfExileTradeItemEntry Unique(int order, string name, string type, string groupId) =>
            new()
            {
                ProviderOrder = order,
                GroupId = groupId,
                GroupLabel = groupId,
                Name = name,
                Type = type,
                IsUnique = true,
            };

        // Structural Timeless + working-control identities only (no production branching by name).
        return new PathOfExileTradeItemCatalog(
        [
            Unique(0, "Lethal Pride", "Timeless Jewel", "jewel"),
            Unique(1, "Brutal Restraint", "Timeless Jewel", "jewel"),
            Unique(2, "Elegant Hubris", "Timeless Jewel", "jewel"),
            Unique(3, "Glorious Vanity", "Timeless Jewel", "jewel"),
            Unique(4, "Militant Faith", "Timeless Jewel", "jewel"),
            Unique(5, "Thread of Hope", "Crimson Jewel", "jewel"),
            Unique(6, "The Battle Within", "Oakbranch Tincture", "tincture"),
            Unique(7, "Replica Bated Breath", "Chain Belt", "accessory"),
            Unique(8, "Augyre", "Void Sceptre", "weapon"),
        ]);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(relativeParts)}");
    }
}

internal sealed record TimelessCaptureSource(string FileName, string ItemName, string RawClipboardText);
