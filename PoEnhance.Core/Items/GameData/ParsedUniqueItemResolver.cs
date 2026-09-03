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
        var fullyCompatibleVersions = identity.Versions
            .Where(version => VersionContainsEveryCopiedBlock(
                version,
                parsedItem.UniqueModifiers,
                isFoulborn))
            .ToArray();
        var compatibleVersions = fullyCompatibleVersions.Length > 0
            ? fullyCompatibleVersions
            : SelectBestPartialVersions(
                identity.Versions,
                parsedItem.UniqueModifiers,
                isFoulborn);
        var blockResolutions = ResolveBlocks(
            parsedItem,
            identity,
            compatibleVersions,
            identity.Versions,
            isFoulborn,
            catalog);
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
        UniqueItemIdentity identity,
        IReadOnlyList<UniqueItemVersionObservation> versions,
        IReadOnlyList<UniqueItemVersionObservation> identityVersions,
        bool isFoulborn,
        GameDataCatalog catalog)
    {
        var results = new List<UniqueModifierBlockResolution>();
        var allowExactHybridRowFallback = versions.Count == 0 &&
            HasExactCrossRoleHybridEvidence(
                identityVersions,
                parsedItem.UniqueModifiers,
                isFoulborn);
        for (var modifierIndex = 0; modifierIndex < parsedItem.Modifiers.Count; modifierIndex++)
        {
            var parsedModifier = parsedItem.Modifiers[modifierIndex];
            if (parsedModifier.Kind == ParsedModifierKind.Unique)
            {
                results.Add(parsedModifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn
                    ? ResolveFoulbornBlock(
                        modifierIndex,
                        parsedModifier,
                        identity,
                        versions,
                        catalog)
                    : ResolveOrdinaryBlock(
                        modifierIndex,
                        parsedModifier,
                        parsedItem,
                        versions,
                        identityVersions,
                        isFoulborn,
                        catalog,
                        isIdentityBoundRecovery: false,
                        allowExactHybridRowFallback));
                continue;
            }

            if (!IsEligibleForIdentityBoundRecovery(parsedModifier, isFoulborn))
            {
                continue;
            }

            // The row is otherwise unsupported, so it is only retained when the identity-bound
            // source evidence proves it outright. Anything less is left skipped exactly as before.
            var recovered = ResolveOrdinaryBlock(
                modifierIndex,
                parsedModifier,
                parsedItem,
                versions,
                identityVersions,
                isFoulborn,
                catalog,
                isIdentityBoundRecovery: true,
                allowExactHybridRowFallback);
            if (recovered.IsResolved)
            {
                results.Add(recovered);
            }
        }

        return results;
    }

    /// <summary>
    /// A parsed row whose Advanced Item Description metadata carries no kind this parser recognizes
    /// may still be a Unique source block; the client labels some of them with domains such as
    /// "Monster Modifier". Rows with a recognized non-Unique kind keep their own domain and are
    /// never diverted here.
    /// </summary>
    private static bool IsEligibleForIdentityBoundRecovery(
        ParsedModifier parsedModifier,
        bool isFoulborn)
    {
        return !isFoulborn &&
            parsedModifier.Kind == ParsedModifierKind.Unknown &&
            parsedModifier.UniqueOrigin == ParsedUniqueModifierOrigin.Unspecified &&
            parsedModifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Unspecified &&
            !parsedModifier.IsCrafted &&
            !parsedModifier.IsFractured &&
            !parsedModifier.IsVeiled &&
            parsedModifier.ValueLines.Count > 0;
    }

    private static UniqueModifierBlockResolution ResolveOrdinaryBlock(
        int modifierIndex,
        ParsedModifier parsedModifier,
        ParsedItem parsedItem,
        IReadOnlyList<UniqueItemVersionObservation> versions,
        IReadOnlyList<UniqueItemVersionObservation> identityVersions,
        bool isFoulborn,
        GameDataCatalog catalog,
        bool isIdentityBoundRecovery,
        bool allowExactHybridRowFallback)
    {
        var blockScopeVersions = versions.Count > 0 ? versions : identityVersions;
        var matchedByVersion = blockScopeVersions
            .Select(version => new VersionBlockMatches(
                version,
                MatchVersionBlocks(version, parsedModifier, parsedItem.UniqueModifiers)))
            .ToArray();
        var hasSafeRowFallback = false;
        if (!MatchesCoverEveryRetainedVersion(matchedByVersion) &&
            (allowExactHybridRowFallback && TrySelectExactHybridRowMatches(
                    identityVersions,
                    parsedModifier,
                    out var hybridMatches) ||
                TrySelectSafeRuntimeFallbackMatches(
                    identityVersions,
                    parsedModifier,
                    out hybridMatches)))
        {
            matchedByVersion = hybridMatches;
            hasSafeRowFallback = true;
        }
        var matchedBlocks = matchedByVersion
            .SelectMany(version => version.Matches)
            .ToArray();
        var blocks = matchedBlocks
            .Select(candidate => candidate.Block)
            .DistinctBy(block => block.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var matchedPresentationLines = matchedBlocks
            .Where(candidate => candidate.Match.PresentationLines.Count > 0)
            .Select(candidate => string.Join('\u001f', candidate.Match.PresentationLines))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var presentationLines = matchedPresentationLines.Length > 0 || matchedBlocks.Any(candidate =>
            candidate.Match.Kind == UniqueBlockTextMatchKind.Direct)
            ? matchedPresentationLines
            : identityVersions.SelectMany(version => version.ModifierBlocks)
                .Where(block => block.Kind == UniqueModifierBlockKind.Unique)
                .Select(block => MatchGeneratedPresentation(
                    block,
                    parsedModifier))
                .Where(match => match.Count > 0)
                .Select(match => string.Join('\u001f', match))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        var coversEveryLine = parsedModifier.ValueLines.Count > 0 &&
            blocks.Length > 0 &&
            matchedByVersion.Length > 0 &&
            (hasSafeRowFallback || MatchesCoverEveryRetainedVersion(matchedByVersion));
        var mappings = blocks.Select(block => block.MechanicalMapping).ToArray();
        var sourceSemantics = blocks.Select(block => block.SourceSemantics).Distinct().ToArray();
        var candidatePoolMembershipIds = blocks
            .SelectMany(block => block.CandidatePoolMembershipIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var optionChoiceMemberships = blocks
            .SelectMany(block => block.OptionChoiceMemberships)
            .DistinctBy(membership =>
                $"{membership.OptionAxisId}\u001f{membership.OptionChoiceId}",
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(membership => membership.OptionAxisId, StringComparer.Ordinal)
            .ThenBy(membership => membership.OptionChoiceId, StringComparer.Ordinal)
            .ToArray();
        var sourceSemanticsAreUnambiguous = sourceSemantics.Length == 1;
        var isGeneratedCandidate = sourceSemanticsAreUnambiguous &&
            sourceSemantics[0] == UniqueModifierSourceSemantics.GeneratedCandidate;
        var candidatePoolProofIsComplete = !isGeneratedCandidate ||
            candidatePoolMembershipIds.Length > 0;
        var usesTextualOptionRangeProjection = matchedBlocks.Any(candidate =>
            candidate.Match.Kind == UniqueBlockTextMatchKind.TextualOptionRangeProjection);
        var textualOptionRangeCollision = usesTextualOptionRangeProjection &&
            matchedByVersion.Any(version => version.Matches.Count > 1);
        var textualOptionRangeAnnotations = matchedBlocks
            .SelectMany(candidate => candidate.Match.TextualOptionRangeAnnotations)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var matchingCandidateVersions = matchedByVersion
            .Where(version => version.Matches.Any(candidate =>
                candidate.Block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate))
            .Select(version => version.Version)
            .ToArray();
        var selectionLimitRejectsBlock = isGeneratedCandidate &&
            matchingCandidateVersions.Length > 0 &&
            matchingCandidateVersions.All(version => GeneratedSelectionLimitExceeded(
                version,
                parsedItem.UniqueModifiers,
                isFoulborn));
        var matchingOptionVersions = matchedByVersion
            .Where(version => version.Matches.Any(candidate =>
                candidate.Block.OptionChoiceMemberships.Count > 0))
            .Select(version => version.Version)
            .ToArray();
        var optionSelectionLimitRejectsBlock = matchingOptionVersions.Length > 0 &&
            matchingOptionVersions.All(version => OptionSelectionLimitExceeded(
                version,
                parsedItem.UniqueModifiers,
                isFoulborn));
        var canPreserveCurrentProofAcrossHistoricalEncodingConflicts =
            TryPreserveCurrentProofAcrossCompatibleHistoricalEncodingConflicts(
                matchedByVersion,
                out var currentProofMatchedBlocks,
                out var compatibleHistoricalConflictEvidence);
        var provisionalMatchedBlocks = canPreserveCurrentProofAcrossHistoricalEncodingConflicts
            ? currentProofMatchedBlocks
            : matchedBlocks;
        var provisionalBlocks = provisionalMatchedBlocks
            .Select(candidate => candidate.Block)
            .DistinctBy(block => block.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var provisionalMappings = provisionalBlocks.Select(block => block.MechanicalMapping).ToArray();
        var provisionalMappingsAreResolved = coversEveryLine &&
            provisionalMappings.Length > 0 &&
            provisionalMappings.All(mapping =>
                mapping.Status is UniqueModifierMechanicalMappingStatus.Exact or
                    UniqueModifierMechanicalMappingStatus.EquivalentSourceSet);
        var provisionalEffectiveStatIds = provisionalMatchedBlocks
            .Select(EffectiveStatIds)
            .ToArray();
        var provisionalStatVectors = provisionalEffectiveStatIds
            .Select(statIds => string.Join('\u001f', statIds))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var provisionalResolved = provisionalMappingsAreResolved &&
            provisionalStatVectors.Length == 1 &&
            sourceSemanticsAreUnambiguous &&
            candidatePoolProofIsComplete &&
            !textualOptionRangeCollision &&
            !selectionLimitRejectsBlock &&
            !optionSelectionLimitRejectsBlock;
        var preservedCurrentProofAcrossHistoricalEncodingConflicts =
            canPreserveCurrentProofAcrossHistoricalEncodingConflicts && provisionalResolved;
        var resolutionMatchedBlocks = preservedCurrentProofAcrossHistoricalEncodingConflicts
            ? provisionalMatchedBlocks
            : matchedBlocks;
        var resolutionBlocks = preservedCurrentProofAcrossHistoricalEncodingConflicts
            ? provisionalBlocks
            : blocks;
        var resolutionMappings = preservedCurrentProofAcrossHistoricalEncodingConflicts
            ? provisionalMappings
            : mappings;
        var mappingsAreResolved = preservedCurrentProofAcrossHistoricalEncodingConflicts
            ? provisionalMappingsAreResolved
            : coversEveryLine && mappings.Length > 0 && mappings.All(mapping =>
                mapping.Status is UniqueModifierMechanicalMappingStatus.Exact or
                    UniqueModifierMechanicalMappingStatus.EquivalentSourceSet);
        var effectiveStatIds = preservedCurrentProofAcrossHistoricalEncodingConflicts
            ? provisionalEffectiveStatIds
            : matchedBlocks.Select(EffectiveStatIds).ToArray();
        var statVectors = effectiveStatIds.Select(statIds => string.Join('\u001f', statIds))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resolved = preservedCurrentProofAcrossHistoricalEncodingConflicts
            ? provisionalResolved
            : mappingsAreResolved &&
                statVectors.Length == 1 &&
                sourceSemanticsAreUnambiguous &&
                candidatePoolProofIsComplete &&
                !textualOptionRangeCollision &&
                !selectionLimitRejectsBlock &&
                !optionSelectionLimitRejectsBlock;
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
        var sourceObservationIds = resolutionBlocks.SelectMany(block => block.SourceObservationIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var aggregationDiagnosticCode = preservedCurrentProofAcrossHistoricalEncodingConflicts
            ? UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof
            : null;
        var aggregationDiagnostic = aggregationDiagnosticCode is null
            ? null
            : FormatHistoricalEncodingCompatibilityDiagnostic(
                resolutionBlocks,
                compatibleHistoricalConflictEvidence,
                effectiveStatIds[0]);
        return new UniqueModifierBlockResolution
        {
            ParsedModifierIndex = modifierIndex,
            IsResolved = resolved,
            IsIdentityBoundRecovery = isIdentityBoundRecovery,
            RecoveredSourceKind = resolved && isIdentityBoundRecovery
                ? ParsedModifierKind.Unique
                : null,
            RecoveredSourceUniqueOrigin = resolved && isIdentityBoundRecovery
                ? ParsedUniqueModifierOrigin.Ordinary
                : null,
            IsEquivalentSourceSet = resolutionBlocks.Length > 1 ||
                sourceObservationIds.Length > 1 ||
                resolutionMatchedBlocks.Any(candidate => candidate.Match.CompositionComponent is not null) ||
                resolutionMappings.Any(mapping =>
                    mapping.Status == UniqueModifierMechanicalMappingStatus.EquivalentSourceSet),
            CatalogBlocks = resolutionBlocks,
            ModifierIds = resolutionMappings.SelectMany(mapping => mapping.ModifierIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray(),
            StatIds = resolved ? effectiveStatIds[0] : [],
            StatLocalities = resolved
                ? effectiveStatIds[0].Select(statId => ResolveStatLocality(statId, catalog)).ToArray()
                : [],
            CanonicalSignatures = resolutionMatchedBlocks.Select(candidate => string.Join(
                    "\n",
                    candidate.Match.CompositionComponent?.CanonicalSignatures ??
                        candidate.Block.CanonicalSignatures))
                .Where(signature => !string.IsNullOrWhiteSpace(signature))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(signature => signature, StringComparer.Ordinal)
                .ToArray(),
            SourceSemantics = sourceSemanticsAreUnambiguous
                ? sourceSemantics[0]
                : UniqueModifierSourceSemantics.Fixed,
            CandidatePoolMembershipIds = isGeneratedCandidate
                ? candidatePoolMembershipIds
                : [],
            OptionChoiceMemberships = optionChoiceMemberships,
            TextualOptionRangeAnnotations = resolved && usesTextualOptionRangeProjection
                ? textualOptionRangeAnnotations
                : [],
            SourceObservationIds = sourceObservationIds,
            PresentationLines = presentationLines.Length == 1
                ? presentationLines[0].Split('\u001f')
                : [],
            ConflictEvidence = resolved
                ? null
                : SelectConflictEvidence(mappings),
            NonBlockingHistoricalConflictEvidence = preservedCurrentProofAcrossHistoricalEncodingConflicts
                ? compatibleHistoricalConflictEvidence
                : null,
            AggregationDiagnosticCode = aggregationDiagnosticCode,
            AggregationDiagnostic = aggregationDiagnostic,
            DiagnosticCode = resolved ? null : optionSelectionLimitRejectsBlock
                ? "UNIQUE_OPTION_SELECTION_LIMIT_EXCEEDED"
                : selectionLimitRejectsBlock
                ? "UNIQUE_GENERATED_SELECTION_LIMIT_EXCEEDED"
                : textualOptionRangeCollision
                    ? "UNIQUE_GENERATED_TEXTUAL_OPTION_RANGE_AMBIGUOUS"
                : coversEveryLine
                ? !sourceSemanticsAreUnambiguous
                    ? "UNIQUE_BLOCK_SOURCE_SEMANTICS_AMBIGUOUS"
                    : !candidatePoolProofIsComplete
                        ? "UNIQUE_GENERATED_POOL_MEMBERSHIP_MISSING"
                    : statVectors.Length > 1
                    ? "UNIQUE_BLOCK_INDEPENDENT_DIMENSIONS"
                    : mappingDiagnosticCodes.Length == 1
                        ? mappingDiagnosticCodes[0]
                        : "UNIQUE_BLOCK_MECHANICS_UNSUPPORTED"
                : blockScopeVersions.Any(version => version.ModifierBlocks.Any(block =>
                    block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate))
                    ? "UNIQUE_GENERATED_CANDIDATE_NOT_FOUND"
                    : "UNIQUE_BLOCK_VERSION_MISMATCH",
            Diagnostic = resolved ? null : optionSelectionLimitRejectsBlock
                ? "The copied item contains more independently selected source choices than the option axis permits."
                : selectionLimitRejectsBlock
                ? "The copied item contains more generated candidate blocks than the selected source definition permits."
                : textualOptionRangeCollision
                    ? "Separating the textual option-range annotation leaves multiple generated source candidates."
                : coversEveryLine
                ? !sourceSemanticsAreUnambiguous
                    ? "The copied block matches both fixed and generated-candidate source semantics."
                    : !candidatePoolProofIsComplete
                        ? "The generated source block has no retained candidate-pool membership proof."
                    : statVectors.Length > 1
                    ? "The source block has independently mapped mechanical dimensions and is not representable as one editable Trade bound."
                    : mappingDiagnostics.Length == 1
                        ? mappingDiagnostics[0]
                        : "At least one line in the source block lacks unambiguous RePoE mechanical evidence."
                : blockScopeVersions.Any(version => version.ModifierBlocks.Any(block =>
                    block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate))
                    ? "No exact candidate in the selected generated source pool matched the copied block."
                    : "The source block was not present in every retained compatible version observation.",
        };
    }

    private static UniqueModifierBlockResolution ResolveFoulbornBlock(
        int modifierIndex,
        ParsedModifier parsedModifier,
        UniqueItemIdentity identity,
        IReadOnlyList<UniqueItemVersionObservation> compatibleVersions,
        GameDataCatalog catalog)
    {
        if (compatibleVersions.Count > 0 &&
            compatibleVersions.All(version => version.Role != UniqueItemVersionRole.Current))
        {
            return UnsupportedFoulbornBlock(
                modifierIndex,
                "FOULBORN_REPLACEMENT_VERSION_MISMATCH",
                "The copied ordinary blocks select historical-only Unique evidence, but the imported Foulborn relationship is current-only.");
        }

        var relationships = catalog.FindFoulbornRelationshipsByUniqueItemId(identity.Id)
            .Where(relationship =>
                relationship.Status == UniqueFoulbornModifierRelationshipStatus.Exact &&
                relationship.AppliesToRole == UniqueItemVersionRole.Current &&
                !string.IsNullOrWhiteSpace(relationship.FoulbornModifierId))
            .ToArray();
        if (relationships.Length == 0)
        {
            return UnsupportedFoulbornBlock(
                modifierIndex,
                "FOULBORN_REPLACEMENT_RELATIONSHIP_NOT_FOUND",
                "The copied Foulborn replacement block has no exact item-scoped relationship in GameData.");
        }

        var matcher = new ModifierTextSignatureMatcher();
        var candidates = relationships
            .SelectMany(relationship => catalog.FindModifiersById(relationship.FoulbornModifierId)
                .Select(modifier => new
                {
                    Relationship = relationship,
                    Modifier = modifier,
                    Match = matcher.Match(modifier, catalog, parsedModifier.ValueLines),
                }))
            .Where(candidate => candidate.Match.Outcome == ModifierTextSignatureMatchOutcome.Match)
            .ToArray();
        if (candidates.Length == 0)
        {
            return UnsupportedFoulbornBlock(
                modifierIndex,
                "FOULBORN_REPLACEMENT_TEXT_MISMATCH",
                "No item-scoped Foulborn replacement modifier has an exact translated-text match for the copied block.");
        }
        if (candidates.Length > 1)
        {
            return UnsupportedFoulbornBlock(
                modifierIndex,
                "FOULBORN_REPLACEMENT_RELATIONSHIP_AMBIGUOUS",
                "Multiple item-scoped Foulborn replacement modifiers exactly match the copied block.");
        }

        var candidate = candidates[0];
        var statIds = candidate.Modifier.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
            .OrderBy(stat => stat.Index)
            .Select(stat => stat.StatId!.Trim())
            .ToArray();
        if (statIds.Length == 0)
        {
            return UnsupportedFoulbornBlock(
                modifierIndex,
                "FOULBORN_REPLACEMENT_MECHANICS_UNAVAILABLE",
                "The exact Foulborn replacement modifier has no retained stat-vector mechanics.");
        }

        return new UniqueModifierBlockResolution
        {
            ParsedModifierIndex = modifierIndex,
            IsResolved = true,
            FoulbornRelationshipIds = [candidate.Relationship.Id!],
            NormalCounterpartModifierIds = [candidate.Relationship.NormalModifierId!],
            ModifierIds = [candidate.Modifier.Id!],
            StatIds = statIds,
            StatLocalities = statIds.Select(statId => ResolveStatLocality(statId, catalog)).ToArray(),
            CanonicalSignatures = candidate.Match.CandidateSignatures
                .Select(signature => string.Join("\n", signature.Lines))
                .ToArray(),
            SourceObservationIds = [candidate.Relationship.SourceObservationId!],
        };
    }

    private static UniqueModifierBlockResolution UnsupportedFoulbornBlock(
        int modifierIndex,
        string diagnosticCode,
        string diagnostic) => new()
    {
        ParsedModifierIndex = modifierIndex,
        IsResolved = false,
        DiagnosticCode = diagnosticCode,
        Diagnostic = diagnostic,
    };

    private static ModifierLocality ResolveStatLocality(string statId, GameDataCatalog catalog)
    {
        var matches = catalog.FindStatsById(statId);
        return matches.Count == 1
            ? matches[0].IsLocal ? ModifierLocality.Local : ModifierLocality.Global
            : ModifierLocality.Unknown;
    }

    private static IReadOnlyList<string> EffectiveStatIds(MatchedBlock match) =>
        match.Match.CompositionComponent?.StatIds ?? match.Block.MechanicalMapping.StatIds;

    private static bool VersionContainsEveryCopiedBlock(
        UniqueItemVersionObservation version,
        IReadOnlyList<ParsedModifier> modifiers,
        bool isFoulborn)
    {
        if (!modifiers.All(modifier => modifier.Kind != ParsedModifierKind.Unique ||
            isFoulborn && modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn ||
            MatchVersionBlocks(version, modifier, modifiers).Count > 0))
        {
            return false;
        }

        return !GeneratedSelectionLimitExceeded(version, modifiers, isFoulborn) &&
            !OptionSelectionLimitExceeded(version, modifiers, isFoulborn);
    }

    private static bool OptionSelectionLimitExceeded(
        UniqueItemVersionObservation version,
        IReadOnlyList<ParsedModifier> modifiers,
        bool isFoulborn)
    {
        foreach (var axis in version.OptionAxes)
        {
            var selectedChoiceCount = modifiers
                .Where(modifier =>
                    modifier.Kind == ParsedModifierKind.Unique &&
                    !(isFoulborn && modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn))
                .SelectMany(modifier => MatchVersionBlocks(version, modifier, modifiers))
                .SelectMany(match => match.Block.OptionChoiceMemberships)
                .Where(membership => string.Equals(
                    membership.OptionAxisId,
                    axis.Id,
                    StringComparison.OrdinalIgnoreCase))
                .Select(membership => membership.OptionChoiceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (selectedChoiceCount > axis.SelectionLimit)
            {
                return true;
            }
        }

        return false;
    }

    private static bool GeneratedSelectionLimitExceeded(
        UniqueItemVersionObservation version,
        IReadOnlyList<ParsedModifier> modifiers,
        bool isFoulborn)
    {
        if (version.GeneratedCandidateSelectionLimit <= 0)
        {
            return false;
        }

        var candidateBlocks = version.ModifierBlocks
            .Where(block => block.Kind == UniqueModifierBlockKind.Unique &&
                block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate)
            .ToArray();
        if (candidateBlocks.Length == 0)
        {
            return false;
        }

        var copiedCandidateMembershipIds = modifiers
            .Where(modifier =>
                modifier.Kind == ParsedModifierKind.Unique &&
                !(isFoulborn && modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn))
            .SelectMany(modifier => MatchVersionBlocks(
                    version,
                    modifier,
                    modifiers)
                .Where(match => match.Block.SourceSemantics ==
                    UniqueModifierSourceSemantics.GeneratedCandidate)
                .SelectMany(match => match.Block.CandidatePoolMembershipIds))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return copiedCandidateMembershipIds.Length > version.GeneratedCandidateSelectionLimit;
    }

    private static UniqueItemVersionObservation[] SelectBestPartialVersions(
        IReadOnlyList<UniqueItemVersionObservation> versions,
        IReadOnlyList<ParsedModifier> modifiers,
        bool isFoulborn)
    {
        if (versions.Count < 2)
        {
            return [];
        }

        var scored = versions.Select(version => new
        {
            Version = version,
            MatchCount = modifiers.Count(modifier =>
                modifier.Kind == ParsedModifierKind.Unique &&
                (isFoulborn && modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn ||
                    MatchVersionBlocks(version, modifier, modifiers).Count > 0)),
        }).ToArray();
        var maximum = scored.Max(candidate => candidate.MatchCount);
        if (maximum == 0 || scored.All(candidate => candidate.MatchCount == maximum))
        {
            return [];
        }

        return scored
            .Where(candidate => candidate.MatchCount == maximum)
            .Select(candidate => candidate.Version)
            .ToArray();
    }

    private static bool MatchesCoverEveryRetainedVersion(
        IReadOnlyList<VersionBlockMatches> matches) =>
        matches.Count > 0 && matches.All(version => version.Matches.Count > 0);

    private static bool HasExactCrossRoleHybridEvidence(
        IReadOnlyList<UniqueItemVersionObservation> versions,
        IReadOnlyList<ParsedModifier> modifiers,
        bool isFoulborn)
    {
        var hasCurrentOnlyRow = false;
        var hasHistoricalOnlyRow = false;
        foreach (var modifier in modifiers.Where(modifier =>
                     modifier.Kind == ParsedModifierKind.Unique &&
                     !(isFoulborn && modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn)))
        {
            var directMatches = FindDirectRowMatches(versions, modifier);
            if (directMatches.Length == 0 || !HaveOneCompleteSemanticSet(directMatches))
            {
                continue;
            }

            var roles = directMatches
                .Select(match => match.Version.Role)
                .Distinct()
                .ToArray();
            hasCurrentOnlyRow |= roles.Length == 1 && roles[0] == UniqueItemVersionRole.Current;
            hasHistoricalOnlyRow |= roles.Length == 1 && roles[0] == UniqueItemVersionRole.Historical;
        }

        return hasCurrentOnlyRow && hasHistoricalOnlyRow;
    }

    private static bool TrySelectExactHybridRowMatches(
        IReadOnlyList<UniqueItemVersionObservation> versions,
        ParsedModifier modifier,
        out VersionBlockMatches[] matches)
    {
        matches = FindDirectRowMatches(versions, modifier);
        if (matches.Length == 0 || !HaveOneCompleteSemanticSet(matches))
        {
            matches = [];
            return false;
        }

        return true;
    }

    private static VersionBlockMatches[] FindDirectRowMatches(
        IReadOnlyList<UniqueItemVersionObservation> versions,
        ParsedModifier modifier)
    {
        return versions
            .Select(version => new VersionBlockMatches(
                version,
                version.ModifierBlocks
                    .Where(block => block.Kind == UniqueModifierBlockKind.Unique)
                    .Select(block => new MatchedBlock(block, MatchParsedModifier(block, modifier)))
                    .Where(candidate =>
                        candidate.Match.IsMatch &&
                        candidate.Match.Kind == UniqueBlockTextMatchKind.Direct &&
                        HasExactRowRollEvidence(modifier.ValueLines, candidate.Block.Lines))
                    .ToArray()))
            .Where(version => version.Matches.Count > 0)
            .ToArray();
    }

    private static bool HasExactRowRollEvidence(
        IReadOnlyList<string> parsedLines,
        IReadOnlyList<string> catalogLines)
    {
        if (parsedLines.Count != catalogLines.Count)
        {
            return false;
        }

        for (var lineIndex = 0; lineIndex < parsedLines.Count; lineIndex++)
        {
            var parsedTokens = ExtractLogicalRollTokens(parsedLines[lineIndex]);
            var catalogTokens = ExtractLogicalRollTokens(catalogLines[lineIndex]);
            if (parsedTokens.Count != catalogTokens.Count)
            {
                return false;
            }

            for (var tokenIndex = 0; tokenIndex < parsedTokens.Count; tokenIndex++)
            {
                if (!string.Equals(
                        parsedTokens[tokenIndex].CanonicalRoll,
                        catalogTokens[tokenIndex].CanonicalRoll,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TrySelectSafeRuntimeFallbackMatches(
        IReadOnlyList<UniqueItemVersionObservation> versions,
        ParsedModifier modifier,
        out VersionBlockMatches[] matches)
    {
        matches = versions
            .Select(version => new VersionBlockMatches(
                version,
                version.ModifierBlocks
                    .Where(block => block.Kind == UniqueModifierBlockKind.Unique)
                    .Select(block =>
                    {
                        var direct = MatchParsedModifier(block, modifier);
                        return new MatchedBlock(
                            block,
                            direct.IsMatch && direct.Kind == UniqueBlockTextMatchKind.Direct
                                ? direct
                                : MatchSafeRuntimePresentation(block, modifier));
                    })
                    .Where(candidate => candidate.Match.IsMatch)
                    .ToArray()))
            .Where(version => version.Matches.Count > 0)
            .ToArray();
        if (matches.Length == 0 ||
            !matches.SelectMany(version => version.Matches).Any(candidate =>
                IsSafeRuntimeFallbackKind(candidate.Match.Kind)) ||
            !HaveOneCompleteSemanticSet(matches))
        {
            matches = [];
            return false;
        }

        var annotatedBoundCandidates = matches
            .SelectMany(version => version.Matches)
            .Where(candidate => candidate.Match.Kind == UniqueBlockTextMatchKind.AnnotatedBoundProjection)
            .Select(candidate => candidate.Block)
            .ToArray();
        if (annotatedBoundCandidates.Any(block =>
                block.SourceSemanticFingerprint.Locality == UniqueModifierSemanticLocality.Unknown))
        {
            matches = [];
            return false;
        }

        return true;
    }

    private static bool IsSafeRuntimeFallbackKind(UniqueBlockTextMatchKind kind) => kind is
        UniqueBlockTextMatchKind.AnnotatedBoundProjection or
        UniqueBlockTextMatchKind.NumericPluralProjection or
        UniqueBlockTextMatchKind.SignedMixedRangeProjection or
        UniqueBlockTextMatchKind.FixedTextualOptionAnnotationProjection;

    private static bool HaveOneCompleteSemanticSet(IReadOnlyList<VersionBlockMatches> matches)
    {
        var blocks = matches
            .SelectMany(version => version.Matches)
            .Select(candidate => candidate.Block)
            .DistinctBy(block => block.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return blocks.Length > 0 &&
            blocks.All(HasCompleteMechanicalEvidence) &&
            blocks.Select(CreateSemanticEvidenceKey).Distinct(StringComparer.Ordinal).Count() == 1;
    }

    private static bool HasCompleteMechanicalEvidence(UniqueModifierBlock block)
    {
        var mapping = block.MechanicalMapping;
        return block.SourceSemantics == UniqueModifierSourceSemantics.Fixed &&
            block.Lines.Count > 0 &&
            block.CanonicalSignatures.Count == block.Lines.Count &&
            block.CanonicalSignatures.All(signature => !string.IsNullOrWhiteSpace(signature)) &&
            block.SourceObservationIds.Count > 0 &&
            block.SourceObservationIds.All(id => !string.IsNullOrWhiteSpace(id)) &&
            (mapping.Status is UniqueModifierMechanicalMappingStatus.Exact or
                UniqueModifierMechanicalMappingStatus.EquivalentSourceSet) &&
            mapping.ModifierIds.Count > 0 &&
            mapping.StatIds.Count > 0;
    }

    private static bool TryPreserveCurrentProofAcrossCompatibleHistoricalEncodingConflicts(
        IReadOnlyList<VersionBlockMatches> matchedByVersion,
        out IReadOnlyList<MatchedBlock> currentProofMatches,
        out UniqueMechanicalConflictEvidence? nonBlockingHistoricalConflictEvidence)
    {
        currentProofMatches = [];
        nonBlockingHistoricalConflictEvidence = null;

        // Historical-only / explicitly Historical-compatible sets have no Current proof to preserve.
        if (matchedByVersion.Count == 0 ||
            matchedByVersion.All(version => version.Version.Role != UniqueItemVersionRole.Current) ||
            matchedByVersion.All(version => version.Version.Role != UniqueItemVersionRole.Historical))
        {
            return false;
        }

        var currentMatches = matchedByVersion
            .Where(version => version.Version.Role == UniqueItemVersionRole.Current)
            .SelectMany(version => version.Matches)
            .DistinctBy(match => match.Block.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (currentMatches.Length == 0)
        {
            return false;
        }

        if (currentMatches.Any(match => !HasCompleteMechanicalEvidence(match.Block)))
        {
            return false;
        }

        var currentVectors = currentMatches
            .Select(match => string.Join('\u001f', EffectiveStatIds(match)))
            .Where(vector => vector.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (currentVectors.Length != 1)
        {
            return false;
        }

        var currentVector = currentVectors[0];
        var historicalMatches = matchedByVersion
            .Where(version => version.Version.Role == UniqueItemVersionRole.Historical)
            .SelectMany(version => version.Matches)
            .DistinctBy(match => match.Block.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (historicalMatches.Length == 0)
        {
            return false;
        }

        var compatibleHistoricalConflicts = new List<UniqueMechanicalConflictEvidence>();
        foreach (var historicalMatch in historicalMatches)
        {
            var mapping = historicalMatch.Block.MechanicalMapping;
            if (HasCompleteMechanicalEvidence(historicalMatch.Block))
            {
                var historicalVector = string.Join('\u001f', EffectiveStatIds(historicalMatch));
                if (!string.Equals(historicalVector, currentVector, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                continue;
            }

            if (!IsExactConflictMapping(mapping) ||
                mapping.ConflictEvidence is null ||
                !IsHistoricalEncodingConflictCompatibleWithCurrentVector(
                    mapping.ConflictEvidence,
                    currentVector))
            {
                return false;
            }

            compatibleHistoricalConflicts.Add(mapping.ConflictEvidence);
        }

        if (compatibleHistoricalConflicts.Count == 0)
        {
            return false;
        }

        currentProofMatches = currentMatches;
        nonBlockingHistoricalConflictEvidence = SelectConflictEvidence(
            compatibleHistoricalConflicts
                .Select(evidence => new UniqueModifierMechanicalMapping
                {
                    Status = UniqueModifierMechanicalMappingStatus.Ambiguous,
                    ConflictEvidence = evidence,
                    DiagnosticCode = "UNIQUE_MECHANICS_EXACT_CONFLICT",
                })
                .ToArray());
        return true;
    }

    private static bool IsExactConflictMapping(UniqueModifierMechanicalMapping mapping) =>
        mapping.Status == UniqueModifierMechanicalMappingStatus.Ambiguous &&
        string.Equals(
            mapping.DiagnosticCode,
            "UNIQUE_MECHANICS_EXACT_CONFLICT",
            StringComparison.Ordinal);

    private static bool IsHistoricalEncodingConflictCompatibleWithCurrentVector(
        UniqueMechanicalConflictEvidence conflictEvidence,
        string currentVector)
    {
        if (conflictEvidence.Kind !=
                UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent ||
            conflictEvidence.Candidates.Count < 2)
        {
            return false;
        }

        var candidateVectors = conflictEvidence.Candidates
            .Select(candidate => string.Join('\u001f', candidate.StatIds))
            .Where(vector => vector.Length > 0)
            .ToArray();
        if (!candidateVectors.Contains(currentVector, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var contradictoryNonDeprecatedVectors = conflictEvidence.Candidates
            .Where(candidate =>
                !UniqueMechanicalConflictClassifier.HasDeprecatedLegacyEncodingEvidence(candidate))
            .Select(candidate => string.Join('\u001f', candidate.StatIds))
            .Where(vector => vector.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(vector => !string.Equals(vector, currentVector, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return contradictoryNonDeprecatedVectors.Length == 0;
    }

    private static string FormatHistoricalEncodingCompatibilityDiagnostic(
        IReadOnlyList<UniqueModifierBlock> currentBlocks,
        UniqueMechanicalConflictEvidence? historicalConflict,
        IReadOnlyList<string> currentStatIds)
    {
        var currentBlockIds = string.Join(", ", currentBlocks.Select(block => block.Id));
        var historicalKind = historicalConflict?.Kind.ToString() ?? "none";
        var historicalCandidateCount = historicalConflict?.Candidates.Count ?? 0;
        return
            "Historical ExactConflict remained fail-closed in GameData but did not override Current-proven mechanics. " +
            $"Current blocks [{currentBlockIds}] vector [{string.Join(',', currentStatIds)}]; " +
            $"Historical conflict kind={historicalKind}, candidates={historicalCandidateCount}; " +
            "compatibility=non-blocking; selected provenance=Current.";
    }

    private static UniqueMechanicalConflictEvidence? SelectConflictEvidence(
        IReadOnlyList<UniqueModifierMechanicalMapping> mappings)
    {
        var conflicts = mappings
            .Select(mapping => mapping.ConflictEvidence)
            .Where(evidence => evidence is not null)
            .Cast<UniqueMechanicalConflictEvidence>()
            .ToArray();
        if (conflicts.Length == 0)
        {
            return null;
        }

        if (conflicts.Length == 1)
        {
            return conflicts[0];
        }

        // Multiple ExactConflict blocks on one parsed row remain fail-closed; retain the first
        // deterministic subtype payload for diagnostics without merging incompatible sets.
        return conflicts
            .OrderBy(evidence => evidence.Kind.ToString(), StringComparer.Ordinal)
            .ThenBy(evidence => evidence.Candidates.Count)
            .ThenBy(
                evidence => string.Join(
                    '\u001f',
                    evidence.Candidates.Select(candidate => candidate.ModifierId)),
                StringComparer.Ordinal)
            .First();
    }

    private static string CreateSemanticEvidenceKey(UniqueModifierBlock block)
    {
        var mapping = block.MechanicalMapping;
        return string.Join(
            '\u001e',
            block.SourceSemantics.ToString(),
            string.Join('\u001f', mapping.StatIds),
            CreateSemanticFingerprintKey(block.SourceSemanticFingerprint),
            CreateSemanticFingerprintKey(mapping.Provenance?.SourceSemanticFingerprint),
            CreateSemanticFingerprintKey(mapping.Provenance?.MatchedSemanticFingerprint));
    }

    private static string CreateSemanticFingerprintKey(UniqueModifierSemanticFingerprint? fingerprint)
    {
        if (fingerprint is null)
        {
            return "<none>";
        }

        return string.Join(
            '\u001d',
            fingerprint.Locality.ToString(),
            string.Join('\u001f', fingerprint.OrderedStatIds),
            fingerprint.ValueShape.ToString(),
            string.Join('\u001f', fingerprint.Values
                .OrderBy(value => value.Index)
                .Select(value => string.Join(
                    '\u001c',
                    value.Index.ToString(CultureInfo.InvariantCulture),
                    value.StatId ?? string.Empty,
                    value.Format ?? string.Empty,
                    value.Unit ?? string.Empty,
                    value.IsAuxiliary.ToString(CultureInfo.InvariantCulture),
                    string.Join('\u001b', value.Transformations)))),
            string.Join('\u001f', fingerprint.AuxiliaryStatIds));
    }

    private static UniqueBlockTextMatch MatchSafeRuntimePresentation(
        UniqueModifierBlock block,
        ParsedModifier modifier)
    {
        if (block.SourceSemantics != UniqueModifierSourceSemantics.Fixed ||
            modifier.ValueLines.Count == 0 ||
            block.Lines.Count != modifier.ValueLines.Count ||
            !HasCompleteMechanicalEvidence(block))
        {
            return UniqueBlockTextMatch.NoMatch;
        }

        var rawLines = modifier.ValueLines.Select(line => line.Trim()).ToArray();
        if (!HasObservedRollValuesWithinCatalogDomains(rawLines, block.Lines))
        {
            return UniqueBlockTextMatch.NoMatch;
        }

        var projectedLines = ProjectCanonicalRollAnnotations(rawLines);
        if (LinesMatch(block, projectedLines, allowPolarityInversion: false))
        {
            return UniqueBlockTextMatch.AnnotatedBoundMatch;
        }
        if (SignaturesDifferOnlyByNumericPlural(block, projectedLines, rawLines))
        {
            return UniqueBlockTextMatch.NumericPluralMatch;
        }
        if (SignaturesDifferOnlyBySignedMixedRange(block, projectedLines, rawLines))
        {
            return UniqueBlockTextMatch.SignedMixedRangeMatch;
        }

        if (!TryProjectTextualOptionRange(modifier, out var semanticLines, out _) ||
            !HasObservedRollValuesWithinCatalogDomains(semanticLines, block.Lines))
        {
            return UniqueBlockTextMatch.NoMatch;
        }

        var projectedSemanticLines = ProjectCanonicalRollAnnotations(semanticLines);
        return LinesMatch(block, semanticLines, allowPolarityInversion: false) ||
            LinesMatch(block, projectedSemanticLines, allowPolarityInversion: false)
            ? new UniqueBlockTextMatch(
                true,
                semanticLines,
                [],
                UniqueBlockTextMatchKind.FixedTextualOptionAnnotationProjection)
            : UniqueBlockTextMatch.NoMatch;
    }

    private static UniqueBlockTextMatch MatchParsedModifier(
        UniqueModifierBlock block,
        ParsedModifier modifier)
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
        if (block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate &&
            !HasCompatibleGeneratedCandidateRollEvidence(rawLines, block.Lines))
        {
            return UniqueBlockTextMatch.NoMatch;
        }

        var projectedLines = ProjectCanonicalRollAnnotations(rawLines);
        if (LinesMatch(block, rawLines, allowPolarityInversion: false) ||
            LinesMatch(block, projectedLines, HasSignedCanonicalRollAnnotation(rawLines)))
        {
            return UniqueBlockTextMatch.DirectMatch;
        }

        if (block.SourceSemantics != UniqueModifierSourceSemantics.GeneratedCandidate ||
            !TryProjectTextualOptionRange(
                modifier,
                out var semanticLines,
                out var textualOptionRangeAnnotations) ||
            !HasCompatibleAnnotatedRollEvidence(semanticLines, block.Lines) ||
            !HasCompatibleGeneratedCandidateRollEvidence(semanticLines, block.Lines))
        {
            return UniqueBlockTextMatch.NoMatch;
        }

        var projectedPresentationLines = ProjectCanonicalRollAnnotations(semanticLines);
        return LinesMatch(block, semanticLines, allowPolarityInversion: false) ||
            LinesMatch(
                block,
                projectedPresentationLines,
                HasSignedCanonicalRollAnnotation(rawLines))
            ? new UniqueBlockTextMatch(
                true,
                semanticLines,
                textualOptionRangeAnnotations,
                UniqueBlockTextMatchKind.TextualOptionRangeProjection)
            : UniqueBlockTextMatch.NoMatch;
    }

    private static IReadOnlyList<string> MatchGeneratedPresentation(
        UniqueModifierBlock block,
        ParsedModifier modifier)
    {
        if (modifier.ValueLines.Count == 0 || block.Lines.Count != modifier.ValueLines.Count ||
            block.SourceSemantics != UniqueModifierSourceSemantics.GeneratedCandidate ||
            !TryProjectTextualOptionRange(modifier, out var semanticLines, out _))
        {
            return [];
        }

        var rawLines = modifier.ValueLines.Select(line => line.Trim()).ToArray();
        var projectedPresentationLines = ProjectCanonicalRollAnnotations(semanticLines);
        return LinesMatch(block, semanticLines, allowPolarityInversion: false) ||
            LinesMatch(
                block,
                projectedPresentationLines,
                HasSignedCanonicalRollAnnotation(rawLines))
            ? semanticLines
            : [];
    }

    private static IReadOnlyList<MatchedBlock> MatchVersionBlocks(
        UniqueItemVersionObservation version,
        ParsedModifier modifier,
        IReadOnlyList<ParsedModifier>? compositionModifiers = null)
    {
        var matches = version.ModifierBlocks
            .Where(block => block.Kind == UniqueModifierBlockKind.Unique)
            .Select(block => new MatchedBlock(
                block,
                MatchParsedModifier(block, modifier)))
            .Where(candidate => candidate.Match.IsMatch)
            .ToList();
        if (compositionModifiers is not null)
        {
            foreach (var block in version.ModifierBlocks.Where(block =>
                         block.Kind == UniqueModifierBlockKind.Unique &&
                         block.Composition is not null &&
                         HasCompleteCompositionProjection(block, compositionModifiers)))
            {
                foreach (var component in block.Composition!.Components)
                {
                    var componentMatch = MatchParsedModifier(
                        ProjectCompositionComponent(block, component),
                        modifier);
                    if (!componentMatch.IsMatch)
                    {
                        continue;
                    }
                    matches.Add(new MatchedBlock(
                        block,
                        componentMatch with
                        {
                            Kind = UniqueBlockTextMatchKind.CompositionComponentProjection,
                            CompositionComponent = component,
                        }));
                }
            }
        }
        if (matches.Count == 0)
        {
            return [];
        }

        var strongestKind = matches.Min(candidate => candidate.Match.Kind);
        return matches.Where(candidate => candidate.Match.Kind == strongestKind).ToArray();
    }

    private static bool HasCompleteCompositionProjection(
        UniqueModifierBlock block,
        IReadOnlyList<ParsedModifier> modifiers)
    {
        var components = block.Composition?.Components;
        if (components is null || components.Count < 2)
        {
            return false;
        }

        var eligible = modifiers
            .Select((modifier, index) => (Modifier: modifier, Index: index))
            .Where(candidate =>
                candidate.Modifier.Kind == ParsedModifierKind.Unique &&
                candidate.Modifier.UniqueOrigin != ParsedUniqueModifierOrigin.Foulborn)
            .ToArray();
        var selectedModifierIndices = new HashSet<int>();
        foreach (var component in components.OrderBy(component => component.Order))
        {
            var projectedBlock = ProjectCompositionComponent(block, component);
            var matchingModifiers = eligible
                .Where(candidate => MatchParsedModifier(
                    projectedBlock,
                    candidate.Modifier).IsMatch)
                .Select(candidate => candidate.Index)
                .ToArray();
            if (matchingModifiers.Length != 1 || !selectedModifierIndices.Add(matchingModifiers[0]))
            {
                return false;
            }
        }

        return selectedModifierIndices.Count == components.Count;
    }

    private static UniqueModifierBlock ProjectCompositionComponent(
        UniqueModifierBlock block,
        UniqueModifierCompositionComponent component) => block with
    {
        Lines = component.Lines,
        CanonicalSignatures = component.CanonicalSignatures,
        Composition = null,
    };

    private static bool TryProjectTextualOptionRange(
        ParsedModifier modifier,
        out string[] semanticLines,
        out string[] annotations)
    {
        semanticLines = [];
        annotations = [];
        if (modifier.Effects.Count != modifier.ValueLines.Count ||
            !modifier.Effects.Any(effect => effect.TextualOptionRange is not null))
        {
            return false;
        }

        semanticLines = modifier.Effects
            .Select(effect => effect.SemanticText.Trim())
            .ToArray();
        annotations = modifier.Effects
            .Select(effect => effect.TextualOptionRange?.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Cast<string>()
            .ToArray();
        return semanticLines.All(line => line.Length > 0) && annotations.Length > 0;
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

    private static bool SignaturesDifferOnlyByNumericPlural(
        UniqueModifierBlock block,
        IReadOnlyList<string> candidateLines,
        IReadOnlyList<string> rawLines)
    {
        var candidateSignatures = ModifierTextSignatureNormalizer.CreateParsedSignature(candidateLines)
            .Signature.Lines;
        var renderedBlockSignatures = ModifierTextSignatureNormalizer.CreateSignature(block.Lines).Lines;
        return SignaturesDifferOnlyByNumericPlural(
                candidateSignatures,
                block.CanonicalSignatures,
                rawLines) ||
            SignaturesDifferOnlyByNumericPlural(
                candidateSignatures,
                renderedBlockSignatures,
                rawLines);
    }

    private static bool SignaturesDifferOnlyByNumericPlural(
        IReadOnlyList<string> candidateSignatures,
        IReadOnlyList<string> sourceSignatures,
        IReadOnlyList<string> rawLines)
    {
        if (candidateSignatures.Count != sourceSignatures.Count ||
            candidateSignatures.Count != rawLines.Count)
        {
            return false;
        }

        var changedLineCount = 0;
        for (var index = 0; index < candidateSignatures.Count; index++)
        {
            if (string.Equals(
                    candidateSignatures[index],
                    sourceSignatures[index],
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            changedLineCount++;
            if (changedLineCount > 1 ||
                !LineDiffersOnlyByNumericPlural(
                    candidateSignatures[index],
                    sourceSignatures[index],
                    rawLines[index]))
            {
                return false;
            }
        }

        return changedLineCount == 1;
    }

    private static bool LineDiffersOnlyByNumericPlural(
        string candidateSignature,
        string sourceSignature,
        string rawLine)
    {
        var rollTokens = ExtractLogicalRollTokens(rawLine);
        if (rollTokens.Count != 1 ||
            !rollTokens[0].IsEvaluatedAnnotation ||
            rollTokens[0].ObservedValue is not { } observedValue ||
            Math.Abs(observedValue) == 1)
        {
            return false;
        }

        var numericIndex = candidateSignature.IndexOf("<number>", StringComparison.OrdinalIgnoreCase);
        if (numericIndex < 0)
        {
            return false;
        }

        var matchingProjections = 0;
        foreach (Match word in Regex.Matches(
                     candidateSignature,
                     @"\b[\p{L}]+\b",
                     RegexOptions.CultureInvariant))
        {
            if (word.Index <= numericIndex ||
                word.Value.Length <= 2 ||
                !word.Value.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var projection = candidateSignature.Remove(word.Index + word.Length - 1, 1);
            if (string.Equals(projection, sourceSignature, StringComparison.OrdinalIgnoreCase))
            {
                matchingProjections++;
            }
        }

        return matchingProjections == 1;
    }

    private static bool SignaturesDifferOnlyBySignedMixedRange(
        UniqueModifierBlock block,
        IReadOnlyList<string> candidateLines,
        IReadOnlyList<string> rawLines)
    {
        var parsedTokens = rawLines.SelectMany(ExtractLogicalRollTokens).ToArray();
        var catalogTokens = block.Lines.SelectMany(ExtractLogicalRollTokens).ToArray();
        if (parsedTokens.Length != 1 ||
            catalogTokens.Length != 1 ||
            !parsedTokens[0].IsEvaluatedAnnotation ||
            parsedTokens[0].ObservedValue is not { } observedValue ||
            !TryReadRollBounds(
                parsedTokens[0].CanonicalRoll,
                out var parsedMinimum,
                out var parsedMaximum) ||
            !TryReadRollBounds(
                catalogTokens[0].CanonicalRoll,
                out var catalogMinimum,
                out var catalogMaximum) ||
            parsedMinimum != catalogMinimum ||
            parsedMaximum != catalogMaximum ||
            catalogMinimum >= 0 ||
            catalogMaximum <= 0 ||
            observedValue < catalogMinimum ||
            observedValue > catalogMaximum)
        {
            return false;
        }

        var candidateSignatures = ModifierTextSignatureNormalizer.CreateParsedSignature(candidateLines)
            .Signature.Lines;
        var renderedBlockSignatures = ModifierTextSignatureNormalizer.CreateSignature(block.Lines).Lines;
        return SignaturesEqualAfterNumericSignProjection(
                candidateSignatures,
                block.CanonicalSignatures) ||
            SignaturesEqualAfterNumericSignProjection(
                candidateSignatures,
                renderedBlockSignatures);
    }

    private static bool SignaturesEqualAfterNumericSignProjection(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second)
    {
        if (first.Count != second.Count || SignaturesEqual(first, second))
        {
            return false;
        }

        return first.Select(ProjectNumericPlaceholderSign).SequenceEqual(
            second.Select(ProjectNumericPlaceholderSign),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ProjectNumericPlaceholderSign(string signature) =>
        signature
            .Replace("+(-<number>", "(-<number>", StringComparison.OrdinalIgnoreCase)
            .Replace("-(-<number>", "(-<number>", StringComparison.OrdinalIgnoreCase)
            .Replace("+<number>", "<number>", StringComparison.OrdinalIgnoreCase)
            .Replace("-<number>", "<number>", StringComparison.OrdinalIgnoreCase);

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
                    !AreCompatibleAnnotatedRollTokens(
                        parsedTokens[tokenIndex],
                        catalogTokens[tokenIndex],
                        parsedLines[lineIndex],
                        catalogLines[lineIndex]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasObservedRollValuesWithinCatalogDomains(
        IReadOnlyList<string> parsedLines,
        IReadOnlyList<string> catalogLines)
    {
        var sawEvaluatedAnnotation = false;
        for (var lineIndex = 0; lineIndex < parsedLines.Count; lineIndex++)
        {
            var parsedTokens = ExtractLogicalRollTokens(parsedLines[lineIndex]);
            var catalogTokens = ExtractLogicalRollTokens(catalogLines[lineIndex]);
            if (parsedTokens.Count != catalogTokens.Count)
            {
                return false;
            }

            for (var tokenIndex = 0; tokenIndex < parsedTokens.Count; tokenIndex++)
            {
                var parsed = parsedTokens[tokenIndex];
                if (!parsed.IsEvaluatedAnnotation)
                {
                    continue;
                }

                sawEvaluatedAnnotation = true;
                if (parsed.ObservedValue is not { } observedValue ||
                    !TryReadRollBounds(
                        catalogTokens[tokenIndex].CanonicalRoll,
                        out var catalogMinimum,
                        out var catalogMaximum) ||
                    observedValue < Math.Min(catalogMinimum, catalogMaximum) ||
                    observedValue > Math.Max(catalogMinimum, catalogMaximum))
                {
                    return false;
                }
            }
        }

        return sawEvaluatedAnnotation;
    }

    private static bool HasCompatibleGeneratedCandidateRollEvidence(
        IReadOnlyList<string> parsedLines,
        IReadOnlyList<string> catalogLines)
    {
        for (var lineIndex = 0; lineIndex < parsedLines.Count; lineIndex++)
        {
            var parsedTokens = ExtractLogicalRollTokens(parsedLines[lineIndex]);
            var catalogTokens = ExtractLogicalRollTokens(catalogLines[lineIndex]);
            if (parsedTokens.Count != catalogTokens.Count)
            {
                return false;
            }

            for (var tokenIndex = 0; tokenIndex < parsedTokens.Count; tokenIndex++)
            {
                var parsed = parsedTokens[tokenIndex];
                var catalog = catalogTokens[tokenIndex];
                if (parsed.IsEvaluatedAnnotation)
                {
                    if (!string.Equals(parsed.CanonicalRoll, catalog.CanonicalRoll, StringComparison.Ordinal))
                    {
                        return false;
                    }
                    continue;
                }

                if (!TryReadRollBounds(parsed.CanonicalRoll, out var parsedMinimum, out var parsedMaximum) ||
                    !TryReadRollBounds(catalog.CanonicalRoll, out var catalogMinimum, out var catalogMaximum) ||
                    parsedMinimum < catalogMinimum ||
                    parsedMaximum > catalogMaximum)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AreCompatibleAnnotatedRollTokens(
        LogicalRollToken parsed,
        LogicalRollToken catalog,
        string parsedLine,
        string catalogLine)
    {
        if (string.Equals(parsed.CanonicalRoll, catalog.CanonicalRoll, StringComparison.Ordinal))
        {
            return true;
        }

        if (!parsed.IsEvaluatedAnnotation || catalog.IsEvaluatedAnnotation)
        {
            return false;
        }

        if (!TryReadRollBounds(parsed.CanonicalRoll, out var parsedMinimum, out var parsedMaximum) ||
            !TryReadRollBounds(catalog.CanonicalRoll, out var catalogMinimum, out var catalogMaximum))
        {
            return false;
        }

        if (parsedMinimum >= 0 || parsedMaximum >= 0)
        {
            return false;
        }

        var unsignedMinimum = Math.Min(catalogMinimum, catalogMaximum);
        var unsignedMaximum = Math.Max(catalogMinimum, catalogMaximum);
        var trimmedCatalogLine = catalogLine.TrimStart();
        return trimmedCatalogLine.StartsWith("-", StringComparison.Ordinal) &&
            parsedMinimum == -unsignedMaximum &&
            parsedMaximum == -unsignedMinimum;
    }

    private static bool TryReadRollBounds(string roll, out decimal minimum, out decimal maximum)
    {
        var parts = roll.Split(':');
        if (parts.Length == 2 &&
            string.Equals(parts[0], "fixed", StringComparison.Ordinal) &&
            decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out minimum))
        {
            maximum = minimum;
            return true;
        }
        if (parts.Length == 3 &&
            string.Equals(parts[0], "range", StringComparison.Ordinal) &&
            decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out minimum) &&
            decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out maximum))
        {
            return true;
        }

        minimum = 0;
        maximum = 0;
        return false;
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
                        true,
                        ParseRollNumber(match.Groups["actual"].Value));
                }

                return match.Groups["range"].Success
                    ? new LogicalRollToken(CreateRollSpec(
                        match.Groups["rangeFirst"].Value,
                        match.Groups["rangeSecond"].Value), false, null)
                    : new LogicalRollToken(
                        CreateRollSpec(match.Groups["single"].Value, null),
                        false,
                        null);
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

    private static bool SignaturesEqual(IReadOnlyList<string> first, IReadOnlyList<string> second) =>
        first.Count == second.Count && first.SequenceEqual(second, StringComparer.OrdinalIgnoreCase);

    private sealed record VersionBlockMatches(
        UniqueItemVersionObservation Version,
        IReadOnlyList<MatchedBlock> Matches);

    private sealed record MatchedBlock(
        UniqueModifierBlock Block,
        UniqueBlockTextMatch Match);

    private sealed record UniqueBlockTextMatch(
        bool IsMatch,
        IReadOnlyList<string> PresentationLines,
        IReadOnlyList<string> TextualOptionRangeAnnotations,
        UniqueBlockTextMatchKind Kind,
        UniqueModifierCompositionComponent? CompositionComponent = null)
    {
        public static UniqueBlockTextMatch NoMatch { get; } = new(
            false,
            [],
            [],
            UniqueBlockTextMatchKind.None);

        public static UniqueBlockTextMatch DirectMatch { get; } = new(
            true,
            [],
            [],
            UniqueBlockTextMatchKind.Direct);

        public static UniqueBlockTextMatch AnnotatedBoundMatch { get; } = new(
            true,
            [],
            [],
            UniqueBlockTextMatchKind.AnnotatedBoundProjection);

        public static UniqueBlockTextMatch NumericPluralMatch { get; } = new(
            true,
            [],
            [],
            UniqueBlockTextMatchKind.NumericPluralProjection);

        public static UniqueBlockTextMatch SignedMixedRangeMatch { get; } = new(
            true,
            [],
            [],
            UniqueBlockTextMatchKind.SignedMixedRangeProjection);
    }

    private enum UniqueBlockTextMatchKind
    {
        None = int.MaxValue,
        Direct = 0,
        CompositionComponentProjection = 1,
        TextualOptionRangeProjection = 2,
        AnnotatedBoundProjection = 3,
        NumericPluralProjection = 4,
        SignedMixedRangeProjection = 5,
        FixedTextualOptionAnnotationProjection = 6,
    }

    private sealed record LogicalRollToken(
        string CanonicalRoll,
        bool IsEvaluatedAnnotation,
        decimal? ObservedValue);

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
