using System.Globalization;
using System.Text.RegularExpressions;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed partial class ParsedUniqueItemResolver
{
    private const string FoulbornPrefix = "Foulborn ";

    public UniqueItemResolutionResult Resolve(
        ParsedItem parsedItem,
        GameDataCatalog catalog,
        ItemBaseResolutionResult? baseResolution = null)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);
        ArgumentNullException.ThrowIfNull(catalog);

        if (!string.Equals(parsedItem.Rarity?.Trim(), "Unique", StringComparison.OrdinalIgnoreCase))
        {
            return new UniqueItemResolutionResult { Status = UniqueItemResolutionStatus.NotApplicable };
        }

        var displayName = parsedItem.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName) || catalog.UniqueItems is null)
        {
            return Unsupported("UNIQUE_CATALOG_UNAVAILABLE",
                "Unique identity cannot be resolved because the package has no usable Unique catalog.");
        }

        var isFoulborn = displayName.StartsWith(FoulbornPrefix, StringComparison.Ordinal);
        var canonicalLookupName = isFoulborn ? displayName[FoulbornPrefix.Length..].Trim() : displayName;
        if (canonicalLookupName.Length == 0)
        {
            return Unsupported("UNIQUE_NAME_UNSUPPORTED", "The copied Foulborn name has no underlying identity.");
        }

        var baseName = SelectBaseName(parsedItem, baseResolution);
        var identities = catalog.FindUniqueItemsByExactName(canonicalLookupName)
            .Where(identity => isFoulborn
                ? identity.Kind is UniqueItemKind.Ordinary or UniqueItemKind.Replica
                : identity.Kind is UniqueItemKind.Ordinary or UniqueItemKind.Replica)
            .Where(identity => baseName is null || identity.BaseTypeEvidence.Contains(baseName, StringComparer.Ordinal))
            .ToArray();
        if (identities.Length == 0)
        {
            return Unsupported("UNIQUE_IDENTITY_NOT_FOUND",
                "The copied Unique name and base have no exact catalog identity.") with { IsFoulborn = isFoulborn };
        }
        if (identities.Length != 1)
        {
            return new UniqueItemResolutionResult
            {
                Status = UniqueItemResolutionStatus.AmbiguousIdentity,
                IdentityCandidates = identities,
                IsFoulborn = isFoulborn,
                DiagnosticCode = "UNIQUE_IDENTITY_AMBIGUOUS",
                Diagnostic = "Multiple catalog identities match the copied Unique name and base.",
            };
        }

        var identity = identities[0];
        var generatedSourceObservationIds = catalog.UniqueItems.SourceObservations
            .Where(observation => observation.IsGenerated && !string.IsNullOrWhiteSpace(observation.Id))
            .Select(observation => observation.Id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var compatibleVersions = identity.Versions
            .Where(version => VersionContainsEveryCopiedBlock(
                version,
                parsedItem.UniqueModifiers,
                isFoulborn,
                generatedSourceObservationIds))
            .ToArray();
        var blockResolutions = ResolveBlocks(
            parsedItem,
            compatibleVersions,
            identity.Versions,
            catalog,
            generatedSourceObservationIds);
        return new UniqueItemResolutionResult
        {
            Status = UniqueItemResolutionStatus.ExactIdentity,
            Identity = identity,
            IdentityCandidates = [identity],
            CompatibleVersions = compatibleVersions,
            ModifierBlocks = blockResolutions,
            IsFoulborn = isFoulborn,
            DiagnosticCode = compatibleVersions.Length == 0 ? "UNIQUE_VERSION_NOT_FOUND" : null,
            Diagnostic = compatibleVersions.Length == 0
                ? "Identity is exact, but no catalog version contains every copied Unique modifier block."
                : null,
        };
    }

    private static IReadOnlyList<UniqueModifierBlockResolution> ResolveBlocks(
        ParsedItem parsedItem,
        IReadOnlyList<UniqueItemVersionObservation> versions,
        IReadOnlyList<UniqueItemVersionObservation> identityVersions,
        GameDataCatalog catalog,
        IReadOnlySet<string> generatedSourceObservationIds)
    {
        var results = new List<UniqueModifierBlockResolution>();
        for (var modifierIndex = 0; modifierIndex < parsedItem.Modifiers.Count; modifierIndex++)
        {
            var parsedModifier = parsedItem.Modifiers[modifierIndex];
            if (parsedModifier.Kind != ParsedModifierKind.Unique)
            {
                continue;
            }

            if (parsedModifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn)
            {
                results.Add(new UniqueModifierBlockResolution
                {
                    ParsedModifierIndex = modifierIndex,
                    IsResolved = false,
                    DiagnosticCode = "FOULBORN_REPLACEMENT_MECHANICS_UNAVAILABLE",
                    Diagnostic = "The copied Foulborn replacement block has no imported relationship to the canonical underlying Unique mechanics.",
                });
                continue;
            }

            var blockScopeVersions = versions.Count > 0 ? versions : identityVersions;
            var matchedByVersion = blockScopeVersions.Select(version => new
            {
                Version = version,
                Matches = version.ModifierBlocks
                    .Where(block => block.Kind == UniqueModifierBlockKind.Unique)
                    .Select(block => new
                    {
                        Block = block,
                        Match = MatchParsedModifier(
                            block,
                            parsedModifier,
                            generatedSourceObservationIds),
                    })
                    .Where(candidate => candidate.Match.IsMatch)
                    .ToArray(),
            }).ToArray();
            var matchedBlocks = matchedByVersion
                .SelectMany(version => version.Matches)
                .ToArray();
            var blocks = matchedBlocks
                .Select(candidate => candidate.Block)
                .DistinctBy(block => block.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var presentationLines = matchedBlocks
                .Where(candidate => candidate.Match.PresentationLines.Count > 0)
                .Select(candidate => string.Join('\u001f', candidate.Match.PresentationLines))
                .Concat(identityVersions.SelectMany(version => version.ModifierBlocks)
                    .Where(block => block.Kind == UniqueModifierBlockKind.Unique)
                    .Select(block => MatchGeneratedPresentation(
                        block,
                        parsedModifier,
                        generatedSourceObservationIds))
                    .Where(match => match.Count > 0)
                    .Select(match => string.Join('\u001f', match)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var coversEveryLine = parsedModifier.ValueLines.Count > 0 &&
                blocks.Length > 0 &&
                matchedByVersion.Length > 0 &&
                matchedByVersion.All(version => version.Matches.Length > 0);
            var mappings = blocks.Select(block => block.MechanicalMapping).ToArray();
            var mappingsAreResolved = coversEveryLine && mappings.Length > 0 && mappings.All(mapping =>
                mapping.Status is UniqueModifierMechanicalMappingStatus.Exact or
                    UniqueModifierMechanicalMappingStatus.EquivalentSourceSet);
            var statVectors = mappings.Select(mapping => string.Join('\u001f', mapping.StatIds))
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var resolved = mappingsAreResolved && statVectors.Length == 1;
            var mappingDiagnosticCodes = mappings
                .Select(mapping => mapping.DiagnosticCode?.Trim())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var mappingDiagnostics = mappings
                .Select(mapping => mapping.Diagnostic?.Trim())
                .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var sourceObservationIds = blocks.SelectMany(block => block.SourceObservationIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            results.Add(new UniqueModifierBlockResolution
            {
                ParsedModifierIndex = modifierIndex,
                IsResolved = resolved,
                IsEquivalentSourceSet = blocks.Length > 1 ||
                    sourceObservationIds.Length > 1 ||
                    mappings.Any(mapping =>
                        mapping.Status == UniqueModifierMechanicalMappingStatus.EquivalentSourceSet),
                CatalogBlocks = blocks,
                ModifierIds = mappings.SelectMany(mapping => mapping.ModifierIds)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                StatIds = resolved ? mappings[0].StatIds : [],
                StatLocalities = resolved
                    ? mappings[0].StatIds.Select(statId => ResolveStatLocality(statId, catalog)).ToArray()
                    : [],
                CanonicalSignatures = blocks.Select(block => string.Join("\n", block.CanonicalSignatures))
                    .Where(signature => !string.IsNullOrWhiteSpace(signature))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(signature => signature, StringComparer.Ordinal)
                    .ToArray(),
                SourceObservationIds = sourceObservationIds,
                PresentationLines = presentationLines.Length == 1
                    ? presentationLines[0].Split('\u001f')
                    : [],
                DiagnosticCode = resolved ? null : coversEveryLine
                    ? statVectors.Length > 1
                        ? "UNIQUE_BLOCK_INDEPENDENT_DIMENSIONS"
                        : mappingDiagnosticCodes.Length == 1
                            ? mappingDiagnosticCodes[0]
                            : "UNIQUE_BLOCK_MECHANICS_UNSUPPORTED"
                    : "UNIQUE_BLOCK_VERSION_MISMATCH",
                Diagnostic = resolved ? null : coversEveryLine
                    ? statVectors.Length > 1
                        ? "The source block has independently mapped mechanical dimensions and is not representable as one editable Trade bound."
                        : mappingDiagnostics.Length == 1
                            ? mappingDiagnostics[0]
                            : "At least one line in the source block lacks unambiguous RePoE mechanical evidence."
                    : "The source block was not present in every retained compatible version observation.",
            });
        }
        return results;
    }

    private static ModifierLocality ResolveStatLocality(string statId, GameDataCatalog catalog)
    {
        var matches = catalog.FindStatsById(statId);
        return matches.Count == 1
            ? matches[0].IsLocal ? ModifierLocality.Local : ModifierLocality.Global
            : ModifierLocality.Unknown;
    }

    private static bool VersionContainsEveryCopiedBlock(
        UniqueItemVersionObservation version,
        IReadOnlyList<ParsedModifier> modifiers,
        bool isFoulborn,
        IReadOnlySet<string> generatedSourceObservationIds)
    {
        var catalogSignatures = version.ModifierBlocks
            .Where(block => block.Kind == UniqueModifierBlockKind.Unique)
            .ToArray();
        return modifiers.All(modifier => modifier.Kind != ParsedModifierKind.Unique ||
            isFoulborn && modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn ||
            catalogSignatures.Any(block => MatchParsedModifier(
                block,
                modifier,
                generatedSourceObservationIds).IsMatch));
    }

    private static UniqueBlockTextMatch MatchParsedModifier(
        UniqueModifierBlock block,
        ParsedModifier modifier,
        IReadOnlySet<string> generatedSourceObservationIds)
    {
        if (modifier.ValueLines.Count == 0 || block.Lines.Count != modifier.ValueLines.Count)
        {
            return UniqueBlockTextMatch.NoMatch;
        }

        var rawLines = modifier.ValueLines.Select(line => line.Trim()).ToArray();
        if (!HasCompatibleAnnotatedRollEvidence(rawLines, block.Lines))
        {
            return UniqueBlockTextMatch.NoMatch;
        }

        var projectedLines = ProjectCanonicalRollAnnotations(rawLines);
        if (LinesMatch(block, rawLines, allowPolarityInversion: false) ||
            LinesMatch(block, projectedLines, HasSignedCanonicalRollAnnotation(rawLines)))
        {
            return UniqueBlockTextMatch.Match;
        }

        var blockHasGeneratedEvidence = block.SourceObservationIds.Any(
            generatedSourceObservationIds.Contains);
        if (!blockHasGeneratedEvidence)
        {
            return UniqueBlockTextMatch.NoMatch;
        }

        var presentationLines = rawLines.Select(RemoveGeneratedAttachedAnnotation).ToArray();
        if (presentationLines.SequenceEqual(rawLines, StringComparer.Ordinal))
        {
            return UniqueBlockTextMatch.NoMatch;
        }

        var projectedPresentationLines = ProjectCanonicalRollAnnotations(presentationLines);
        return LinesMatch(block, presentationLines, allowPolarityInversion: false) ||
            LinesMatch(
                block,
                projectedPresentationLines,
                HasSignedCanonicalRollAnnotation(rawLines))
            ? new UniqueBlockTextMatch(true, presentationLines)
            : UniqueBlockTextMatch.NoMatch;
    }

    private static IReadOnlyList<string> MatchGeneratedPresentation(
        UniqueModifierBlock block,
        ParsedModifier modifier,
        IReadOnlySet<string> generatedSourceObservationIds)
    {
        if (modifier.ValueLines.Count == 0 || block.Lines.Count != modifier.ValueLines.Count ||
            !block.SourceObservationIds.Any(generatedSourceObservationIds.Contains))
        {
            return [];
        }

        var rawLines = modifier.ValueLines.Select(line => line.Trim()).ToArray();
        var presentationLines = rawLines.Select(RemoveGeneratedAttachedAnnotation).ToArray();
        if (presentationLines.SequenceEqual(rawLines, StringComparer.Ordinal))
        {
            return [];
        }

        var projectedPresentationLines = ProjectCanonicalRollAnnotations(presentationLines);
        return LinesMatch(block, presentationLines, allowPolarityInversion: false) ||
            LinesMatch(
                block,
                projectedPresentationLines,
                HasSignedCanonicalRollAnnotation(rawLines))
            ? presentationLines
            : [];
    }

    private static bool LinesMatch(
        UniqueModifierBlock block,
        IReadOnlyList<string> candidateLines,
        bool allowPolarityInversion)
    {
        var signatures = ModifierTextSignatureNormalizer.CreateParsedSignature(candidateLines)
            .Signature.Lines;
        var renderedBlockSignatures = ModifierTextSignatureNormalizer.CreateSignature(block.Lines).Lines;
        if (SignaturesEqual(block.CanonicalSignatures, signatures) ||
            SignaturesEqual(renderedBlockSignatures, signatures))
        {
            return true;
        }

        return allowPolarityInversion &&
            (SignaturesDifferOnlyByOneOppositePolarity(signatures, block.CanonicalSignatures) ||
                SignaturesDifferOnlyByOneOppositePolarity(signatures, renderedBlockSignatures));
    }

    private static bool SignaturesDifferOnlyByOneOppositePolarity(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        var oppositeCount = 0;
        for (var index = 0; index < first.Count; index++)
        {
            var firstPolarities = PolarityPattern().Matches(first[index])
                .Select(match => match.Value)
                .ToArray();
            var secondPolarities = PolarityPattern().Matches(second[index])
                .Select(match => match.Value)
                .ToArray();
            if (firstPolarities.Length != secondPolarities.Length ||
                !string.Equals(
                    PolarityPattern().Replace(first[index], "<polarity>"),
                    PolarityPattern().Replace(second[index], "<polarity>"),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (var polarityIndex = 0; polarityIndex < firstPolarities.Length; polarityIndex++)
            {
                if (!string.Equals(
                        firstPolarities[polarityIndex],
                        secondPolarities[polarityIndex],
                        StringComparison.OrdinalIgnoreCase))
                {
                    oppositeCount++;
                }
            }
        }

        return oppositeCount == 1;
    }

    private static bool HasCompatibleAnnotatedRollEvidence(
        IReadOnlyList<string> parsedLines,
        IReadOnlyList<string> catalogLines)
    {
        for (var lineIndex = 0; lineIndex < parsedLines.Count; lineIndex++)
        {
            var parsedTokens = ExtractLogicalRollTokens(parsedLines[lineIndex]);
            if (!parsedTokens.Any(token => token.IsEvaluatedAnnotation))
            {
                continue;
            }

            var catalogTokens = ExtractLogicalRollTokens(catalogLines[lineIndex]);
            if (parsedTokens.Count != catalogTokens.Count)
            {
                return false;
            }

            for (var tokenIndex = 0; tokenIndex < parsedTokens.Count; tokenIndex++)
            {
                if (parsedTokens[tokenIndex].IsEvaluatedAnnotation &&
                    parsedTokens[tokenIndex].CanonicalRoll != catalogTokens[tokenIndex].CanonicalRoll)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static IReadOnlyList<LogicalRollToken> ExtractLogicalRollTokens(string line)
    {
        return LogicalRollPattern().Matches(line)
            .Select(match =>
            {
                if (match.Groups["annotation"].Success)
                {
                    return new LogicalRollToken(
                        CreateRollSpec(
                            match.Groups["annotationFirst"].Value,
                            match.Groups["annotationSecond"].Success
                                ? match.Groups["annotationSecond"].Value
                                : null),
                        true);
                }

                return match.Groups["range"].Success
                    ? new LogicalRollToken(CreateRollSpec(
                        match.Groups["rangeFirst"].Value,
                        match.Groups["rangeSecond"].Value), false)
                    : new LogicalRollToken(CreateRollSpec(match.Groups["single"].Value, null), false);
            })
            .ToArray();
    }

    private static string[] ProjectCanonicalRollAnnotations(IReadOnlyList<string> lines)
    {
        return lines.Select(line => EvaluatedRollAnnotationPattern().Replace(line, match =>
        {
            var first = ParseRollNumber(match.Groups["first"].Value);
            var hasSecond = match.Groups["second"].Success;
            var externalPlus = match.Groups["actual"].Value.StartsWith('+') ? "+" : string.Empty;
            if (!hasSecond)
            {
                return externalPlus.Length > 0 && first >= 0
                    ? $"+{FormatRollNumber(first)}"
                    : FormatRollNumber(first);
            }

            var second = ParseRollNumber(match.Groups["second"].Value);
            var minimum = Math.Min(first, second);
            var maximum = Math.Max(first, second);
            return $"{externalPlus}({FormatRollNumber(minimum)}-{FormatRollNumber(maximum)})";
        })).ToArray();
    }

    private static bool HasSignedCanonicalRollAnnotation(IReadOnlyList<string> lines)
    {
        return lines.SelectMany(line => EvaluatedRollAnnotationPattern().Matches(line))
            .Any(match => ParseRollNumber(match.Groups["first"].Value) < 0 ||
                match.Groups["second"].Success &&
                ParseRollNumber(match.Groups["second"].Value) < 0);
    }

    private static string CreateRollSpec(string firstText, string? secondText)
    {
        var first = ParseRollNumber(firstText);
        if (secondText is null)
        {
            return $"fixed:{FormatRollNumber(first)}";
        }

        var second = ParseRollNumber(secondText);
        return $"range:{FormatRollNumber(Math.Min(first, second))}:{FormatRollNumber(Math.Max(first, second))}";
    }

    private static decimal ParseRollNumber(string value) =>
        decimal.Parse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static string FormatRollNumber(decimal value) =>
        value.ToString("G29", CultureInfo.InvariantCulture);

    private static string RemoveGeneratedAttachedAnnotation(string line) =>
        GeneratedAttachedAnnotationPattern().Replace(line, string.Empty);

    private static bool SignaturesEqual(IReadOnlyList<string> first, IReadOnlyList<string> second) =>
        first.Count == second.Count && first.SequenceEqual(second, StringComparer.OrdinalIgnoreCase);

    private sealed record UniqueBlockTextMatch(bool IsMatch, IReadOnlyList<string> PresentationLines)
    {
        public static UniqueBlockTextMatch NoMatch { get; } = new(false, []);

        public static UniqueBlockTextMatch Match { get; } = new(true, []);
    }

    private sealed record LogicalRollToken(string CanonicalRoll, bool IsEvaluatedAnnotation);

    [GeneratedRegex(
        @"(?<![A-Za-z<])(?<actual>[+-]?\d+(?:[\.,]\d+)?)(?<annotation>\(\s*(?<first>[+-]?\d+(?:[\.,]\d+)?)(?:\s*-\s*(?<second>[+-]?\d+(?:[\.,]\d+)?)\s*)?\))",
        RegexOptions.CultureInvariant)]
    private static partial Regex EvaluatedRollAnnotationPattern();

    [GeneratedRegex(
        @"(?<![A-Za-z<])(?:(?<actual>[+-]?\d+(?:[\.,]\d+)?)(?<annotation>\(\s*(?<annotationFirst>[+-]?\d+(?:[\.,]\d+)?)(?:\s*-\s*(?<annotationSecond>[+-]?\d+(?:[\.,]\d+)?)\s*)?\))|(?<range>\(?\s*(?<rangeFirst>[+-]?\d+(?:[\.,]\d+)?)\s*-\s*(?<rangeSecond>[+-]?\d+(?:[\.,]\d+)?)\s*\)?)|(?<single>[+-]?\d+(?:[\.,]\d+)?))",
        RegexOptions.CultureInvariant)]
    private static partial Regex LogicalRollPattern();

    [GeneratedRegex(@"\b(?:increased|reduced)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PolarityPattern();

    [GeneratedRegex(@"(?<=[A-Za-z])\((?=[^()]*[A-Za-z])[^()]+\)(?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedAttachedAnnotationPattern();

    private static string? SelectBaseName(ParsedItem item, ItemBaseResolutionResult? resolution)
    {
        if (resolution?.Status is ItemBaseResolutionStatus.Exact or ItemBaseResolutionStatus.Probable &&
            !string.IsNullOrWhiteSpace(resolution.ResolvedBaseName))
        {
            return resolution.ResolvedBaseName.Trim();
        }
        return string.IsNullOrWhiteSpace(item.BaseType) ? null : item.BaseType.Trim();
    }

    private static UniqueItemResolutionResult Unsupported(string code, string diagnostic) => new()
    {
        Status = UniqueItemResolutionStatus.Unsupported,
        DiagnosticCode = code,
        Diagnostic = diagnostic,
    };
}
