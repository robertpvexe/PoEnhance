using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    public const string SourceId = "path-of-building";

    public PoBUniqueCatalogImportResult Import(
        string filePath,
        string repositoryUri,
        string tag,
        string commitSha,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord>? baseItems = null,
        IReadOnlyList<ItemPropertySemanticDescriptor>? itemPropertySemantics = null,
        IReadOnlyList<StatDefinition>? stats = null)
    {
        if (!File.Exists(filePath))
        {
            return Failure(RePoeImportDiagnosticCodes.PoBUniqueFileNotFound,
                $"Evaluated Path of Building Unique input was not found: {filePath}");
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return Failure(RePoeImportDiagnosticCodes.PoBUniqueSchemaUnsupported,
                    "Evaluated Path of Building Unique input must contain an entries array.");
            }

            return ImportEntries(
                entries,
                repositoryUri,
                tag,
                commitSha,
                modifiers,
                translations,
                baseItems ?? [],
                itemPropertySemantics ?? [],
                stats ?? []);
        }
        catch (JsonException exception)
        {
            return Failure(RePoeImportDiagnosticCodes.PoBUniqueJsonMalformed,
                $"Evaluated Path of Building Unique input is invalid JSON: {exception.Message}");
        }
    }

    private static PoBUniqueCatalogImportResult ImportEntries(
        JsonElement entries,
        string repositoryUri,
        string tag,
        string commitSha,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord> baseItems,
        IReadOnlyList<ItemPropertySemanticDescriptor> itemPropertySemantics,
        IReadOnlyList<StatDefinition> stats)
    {
        var diagnostics = new List<ImportDiagnostic>();
        var observations = new List<UniqueCatalogSourceObservation>();
        var parsed = new List<ParsedSourceItem>();
        var baseIdentityIndex = new BaseIdentityIndex(baseItems);
        var read = 0;
        var skipped = 0;

        foreach (var entry in entries.EnumerateArray())
        {
            read++;
            if (!TryReadString(entry, "sourcePath", out var sourcePath) ||
                !TryReadString(entry, "raw", out var raw))
            {
                skipped++;
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.PoBUniqueRecordUnsupported,
                    ImportDiagnosticSeverity.Warning,
                    read.ToString(CultureInfo.InvariantCulture),
                    "Evaluated Unique entry lacks sourcePath or raw text and was skipped."));
                continue;
            }

            var generated = entry.TryGetProperty("generated", out var generatedElement) &&
                generatedElement.ValueKind == JsonValueKind.True;
            if (!TryParseSourceItem(raw, out var sourceItem))
            {
                skipped++;
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.PoBUniqueRecordUnsupported,
                    ImportDiagnosticSeverity.Warning,
                    sourcePath,
                    "Evaluated Unique entry has no reliable name/base header and was skipped."));
                continue;
            }
            sourceItem = sourceItem with
            {
                EffectLines = ApplySourceSemanticFingerprints(
                    entry,
                    sourceItem,
                    sourcePath,
                    diagnostics),
            };

            var kind = ClassifyKind(sourceItem.Name);
            sourceItem = sourceItem with
            {
                BaseTypes = sourceItem.BaseTypes
                    .Select(baseType => baseIdentityIndex.Resolve(baseType, sourcePath, diagnostics))
                    .ToArray(),
            };
            var canonicalIdentityKey = CanonicalIdentityKey(sourceItem.Name, kind);
            var observationId = StableId("pob-observation", sourcePath, Sha256(raw));
            observations.Add(new UniqueCatalogSourceObservation
            {
                Id = observationId,
                ManifestSourceId = SourceId,
                RepositoryUri = repositoryUri.Trim(),
                Tag = tag.Trim(),
                CommitSha = commitSha.Trim().ToLowerInvariant(),
                SourcePath = sourcePath.Trim().Replace('\\', '/'),
                IsGenerated = generated,
                ObservedKind = kind,
                RawEntrySha256 = Sha256(raw),
                ObservedName = sourceItem.Name,
                ObservedBaseTypes = sourceItem.BaseTypes
                    .Select(baseType => baseType.SourceText)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                CanonicalIdentityKey = canonicalIdentityKey,
                IdentityNormalizationRule = UniqueSourceIdentityNormalizer.CanonicalRule,
                IdentityDecisionReason =
                    "Pinned PoB display name and explicit ordinary/Replica/Foulborn role define the source identity; the canonical key is comparison-only and collision checked.",
            });
            parsed.Add(sourceItem with
            {
                ObservationId = observationId,
                Kind = kind,
                IsGenerated = generated,
            });
        }

        foreach (var collision in parsed
                     .GroupBy(item => CanonicalIdentityKey(item.Name, item.Kind), StringComparer.Ordinal)
                     .Select(group => new
                     {
                         Key = group.Key,
                         Names = group.Select(item => item.Name)
                             .Distinct(StringComparer.Ordinal)
                             .OrderBy(value => value, StringComparer.Ordinal)
                             .ToArray(),
                     })
                     .Where(group => group.Names.Length > 1))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.PoBUniqueIdentityNormalizationCollision,
                ImportDiagnosticSeverity.Error,
                collision.Key,
                $"Canonical Unique identity key '{collision.Key}' collides across distinct source names: {string.Join(", ", collision.Names)}."));
        }

        var mechanicalIndex = BuildMechanicalIndex(
            modifiers,
            translations,
            baseItems,
            itemPropertySemantics,
            stats);
        var identities = parsed
            .GroupBy(item => new IdentityKey(item.Name, item.Kind))
            .Select(group => BuildIdentity(group, mechanicalIndex))
            .OrderBy(identity => identity.CanonicalName, StringComparer.Ordinal)
            .ThenBy(identity => identity.Kind)
            .ThenBy(identity => identity.Id, StringComparer.Ordinal)
            .ToArray();

        return new PoBUniqueCatalogImportResult
        {
            Catalog = new UniqueItemCatalog
            {
                SourceObservations = observations.OrderBy(source => source.Id, StringComparer.Ordinal).ToArray(),
                Items = identities,
            },
            Diagnostics = diagnostics,
            SourceRecordsRead = read,
            RecordsImported = parsed.Count,
            RecordsSkipped = skipped,
        };
    }

    private static UniqueItemIdentity BuildIdentity(
        IGrouping<IdentityKey, ParsedSourceItem> group,
        MechanicalIndex mechanicalIndex)
    {
        var identityId = StableId("unique", group.Key.Name, group.Key.Kind.ToString());
        var versions = group
            .SelectMany(item => BuildVersions(identityId, item, mechanicalIndex))
            .GroupBy(version => new
            {
                version.Label,
                version.Role,
                version.BaseType,
                Signature = string.Join('\u001f', version.ModifierBlocks.Select(block => block.Id)),
            })
            .Select(versionGroup => versionGroup.First() with
            {
                SourceObservationIds = versionGroup
                    .SelectMany(version => version.SourceObservationIds)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                ModifierBlocks = MergeBlockProvenance(versionGroup.SelectMany(version => version.ModifierBlocks)),
            })
            .OrderBy(version => version.Role)
            .ThenBy(version => version.Label, StringComparer.Ordinal)
            .ThenBy(version => version.Id, StringComparer.Ordinal)
            .ToArray();

        return new UniqueItemIdentity
        {
            Id = identityId,
            CanonicalName = group.Key.Name,
            CanonicalIdentityKey = CanonicalIdentityKey(group.Key.Name, group.Key.Kind),
            Kind = group.Key.Kind,
            BaseTypeEvidence = versions
                .Select(version => version.BaseType!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            Versions = versions,
            SourceObservationIds = group.Select(item => item.ObservationId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static IReadOnlyList<UniqueModifierBlock> MergeBlockProvenance(
        IEnumerable<UniqueModifierBlock> blocks)
    {
        return blocks
            .GroupBy(block => block.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First() with
            {
                SourceObservationIds = group.SelectMany(block => block.SourceObservationIds)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
            })
            .OrderBy(block => block.Kind)
            .ThenBy(block => block.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<UniqueItemVersionObservation> BuildVersions(
        string identityId,
        ParsedSourceItem item,
        MechanicalIndex mechanicalIndex)
    {
        var hasExplicitCurrentLabel = item.Variants.Any(variant =>
            ClassifyVersionRole(variant.Label, hasExplicitCurrentSibling: false) == UniqueItemVersionRole.Current);
        var classifiedVariants = item.Variants
            .Where(variant => ClassifyVersionRole(variant.Label, hasExplicitCurrentLabel) != UniqueItemVersionRole.Unknown)
            .ToArray();
        var hasCurrentVariant = classifiedVariants.Any(variant =>
            ClassifyVersionRole(variant.Label, hasExplicitCurrentLabel) == UniqueItemVersionRole.Current);
        var primaryVariants = hasCurrentVariant
            ? classifiedVariants
            : item.IsGenerated
                ? []
                : item.Variants.ToArray();
        var primaryIndices = primaryVariants.Select(variant => variant.Index).ToHashSet();
        var optionIndices = item.Variants
            .Where(variant => !primaryIndices.Contains(variant.Index) &&
                (!item.IsGenerated ||
                    ClassifyVersionRole(variant.Label, hasExplicitCurrentLabel) != UniqueItemVersionRole.Historical))
            .Select(variant => variant.Index)
            .ToHashSet();
        var baseVariantIndices = item.BaseTypes.SelectMany(baseType => baseType.Variants).ToHashSet();
        var specs = BuildVersionSpecs(item, primaryVariants, hasExplicitCurrentLabel);

        foreach (var spec in specs)
        {
            var implicitLines = item.EffectLines.Take(item.ImplicitCount)
                .Where(line => IsApplicable(line, spec, optionIndices, baseVariantIndices))
                .Select(line => SelectEffectLine(
                    line,
                    item.ObservationId!,
                    optionIndices,
                    baseVariantIndices,
                    spec.SourceBaseType))
                .ToArray();
            var uniqueLines = item.EffectLines.Skip(item.ImplicitCount)
                .Where(line => IsApplicable(line, spec, optionIndices, baseVariantIndices))
                .Select(line => SelectEffectLine(
                    line,
                    item.ObservationId!,
                    optionIndices,
                    baseVariantIndices,
                    spec.SourceBaseType))
                .ToArray();
            var blocks = GroupBlocks(
                    implicitLines,
                    UniqueModifierBlockKind.Implicit,
                    identityId,
                    spec.Label,
                    spec.BaseType,
                    item.ObservationId!,
                    item.IsGenerated,
                    mechanicalIndex)
                .Concat(GroupBlocks(
                    uniqueLines,
                    UniqueModifierBlockKind.Unique,
                    identityId,
                    spec.Label,
                    spec.BaseType,
                    item.ObservationId!,
                    item.IsGenerated,
                    mechanicalIndex))
                .ToArray();
            var selectedCandidateCount = item.SelectedVariantIndices.Count(index =>
                optionIndices.Contains(index) && !baseVariantIndices.Contains(index));
            var hasCandidateBlocks = blocks.Any(block =>
                block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate);
            var inferredCandidateCount = hasCandidateBlocks
                ? item.AlternateVariantSlotCount + (primaryVariants.Length == 0 ? 1 : 0)
                : 0;
            yield return new UniqueItemVersionObservation
            {
                Id = StableId("unique-version", identityId, spec.Label, spec.BaseType,
                    string.Join('\u001f', blocks.Select(block => block.Id))),
                Label = spec.Label,
                Role = spec.Role,
                BaseType = spec.BaseType,
                SourceBaseType = spec.SourceBaseType,
                CanonicalBaseTypeKey = spec.CanonicalBaseTypeKey,
                BaseTypeNormalizationRule = spec.BaseTypeNormalizationRule,
                RePoeBaseItemIds = spec.RePoeBaseItemIds,
                RoleDecisionReason = spec.RoleDecisionReason,
                VariantDecisionReason = spec.VariantDecisionReason,
                GeneratedCandidateSelectionLimit = Math.Max(
                    selectedCandidateCount,
                    inferredCandidateCount),
                ModifierBlocks = blocks,
                SourceObservationIds = [item.ObservationId!],
            };
        }
    }

    private static SelectedEffectLine SelectEffectLine(
        SourceEffectLine line,
        string observationId,
        ISet<int> optionIndices,
        ISet<int> baseVariantIndices,
        string sourceBaseType)
    {
        var candidateIndices = line.Variants
            .Where(index => optionIndices.Contains(index) && !baseVariantIndices.Contains(index))
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
        return new SelectedEffectLine(
            line.Text,
            candidateIndices.Length > 0,
            candidateIndices.Select(index => StableId(
                    "pob-generated-candidate",
                    observationId,
                    index.ToString(CultureInfo.InvariantCulture)))
                .ToArray(),
            SelectSourceSemanticFingerprint(line.SemanticFingerprints, sourceBaseType));
    }

    private static UniqueModifierSemanticFingerprint SelectSourceSemanticFingerprint(
        IReadOnlyList<SourceSemanticFingerprintObservation> observations,
        string sourceBaseType)
    {
        var matching = observations
            .Where(observation => string.Equals(
                NormalizeExactEvidence(observation.BaseType),
                NormalizeExactEvidence(sourceBaseType),
                StringComparison.Ordinal))
            .ToArray();
        var localities = matching
            .Select(observation => observation.Fingerprint.Locality)
            .Distinct()
            .ToArray();
        return new UniqueModifierSemanticFingerprint
        {
            Locality = localities.Length == 1
                ? localities[0]
                : UniqueModifierSemanticLocality.Unknown,
            EvidenceMethods = matching
                .SelectMany(observation => observation.Fingerprint.EvidenceMethods)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static IReadOnlyList<VersionSpec> BuildVersionSpecs(
        ParsedSourceItem item,
        IReadOnlyList<SourceVariant> primaryVariants,
        bool hasExplicitCurrentLabel)
    {
        if (primaryVariants.Count > 0)
        {
            return primaryVariants.Select(variant =>
                {
                    var role = ClassifyVersionRoleEvidence(variant.Label, hasExplicitCurrentLabel);
                    var baseType = SelectBaseType(item.BaseTypes, variant.Index);
                    return VersionSpecFor(
                        variant.Label,
                        role.Role == UniqueItemVersionRole.Unknown
                            ? UniqueItemVersionRole.Current
                            : role.Role,
                        variant.Index,
                        baseType,
                        role.Role == UniqueItemVersionRole.Unknown
                            ? "No explicit current/history marker exists; the pinned non-generated PoB alternative is retained as a simultaneous current definition."
                            : role.Reason,
                        "Source variant label is retained as a distinct version observation.");
                })
                .ToArray();
        }

        var variantBases = item.BaseTypes
            .SelectMany(baseType => baseType.Variants.Select(variantIndex => new
            {
                BaseType = baseType,
                VariantIndex = variantIndex,
            }))
            .ToArray();
        if (variantBases.Length == 0)
        {
            return
            [
                VersionSpecFor(
                    "Observed",
                    UniqueItemVersionRole.Current,
                    variantIndex: null,
                    item.BaseTypes[0],
                    "No version label exists in the pinned source; the sole evaluated non-generated observation is current.",
                    item.IsGenerated
                        ? "Generated/special option evidence remains on the evaluated observation for E5 family modeling."
                        : "The source contains one observed definition."),
            ];
        }

        return variantBases.Select(baseVariant => VersionSpecFor(
                $"Observed: {baseVariant.BaseType.Text}",
                UniqueItemVersionRole.Current,
                baseVariant.VariantIndex,
                baseVariant.BaseType,
                "No explicit current/history marker exists; the pinned non-generated base alternative is retained as current.",
                "The source base directive is retained as a distinct simultaneous current alternative."))
            .ToArray();
    }

    private static VersionSpec VersionSpecFor(
        string label,
        UniqueItemVersionRole role,
        int? variantIndex,
        SourceBaseType baseType,
        string roleDecisionReason,
        string variantDecisionReason) => new(
            label,
            role,
            variantIndex,
            baseType.Text,
            baseType.SourceText,
            baseType.CanonicalKey,
            baseType.NormalizationRule,
            baseType.RePoeBaseItemIds,
            roleDecisionReason,
            variantDecisionReason);

    private static SourceBaseType SelectBaseType(
        IReadOnlyList<SourceBaseType> baseTypes,
        int variantIndex)
    {
        return baseTypes.FirstOrDefault(baseType => baseType.Variants.Contains(variantIndex)) ??
            baseTypes.FirstOrDefault(baseType => baseType.Variants.Count == 0) ??
            baseTypes[0];
    }

    private static bool IsApplicable(
        SourceEffectLine line,
        VersionSpec spec,
        ISet<int> optionIndices,
        ISet<int> baseVariantIndices)
    {
        return line.Variants.Count == 0 ||
            spec.VariantIndex.HasValue && line.Variants.Contains(spec.VariantIndex.Value) ||
            line.Variants.Any(index => optionIndices.Contains(index) && !baseVariantIndices.Contains(index));
    }

    private static IEnumerable<UniqueModifierBlock> GroupBlocks(
        IReadOnlyList<SelectedEffectLine> lines,
        UniqueModifierBlockKind kind,
        string identityId,
        string versionLabel,
        string baseType,
        string observationId,
        bool isGeneratedSource,
        MechanicalIndex mechanicalIndex)
    {
        for (var index = 0; index < lines.Count;)
        {
            var firstLine = lines[index];
            var maximumLength = 1;
            while (index + maximumLength < lines.Count &&
                SameSourceSemantics(firstLine, lines[index + maximumLength]))
            {
                maximumLength++;
            }
            var selectedLength = 1;
            for (var length = maximumLength; length > 1; length--)
            {
                var candidateLines = lines.Skip(index).Take(length).ToArray();
                if (mechanicalIndex.HasMatch(
                        candidateLines.Select(line => line.Text).ToArray(),
                        baseType,
                        candidateLines.Any(line => line.HasGeneratedOptionEvidence),
                        CombineSourceSemanticFingerprints(candidateLines.Select(line =>
                            line.SemanticFingerprint))))
                {
                    selectedLength = length;
                    break;
                }
            }

            var selectedLines = lines.Skip(index).Take(selectedLength).ToArray();
            yield return BuildBlock(
                identityId,
                versionLabel,
                selectedLines.Select(line => line.Text).ToArray(),
                kind,
                observationId,
                isGeneratedSource,
                firstLine.HasGeneratedOptionEvidence,
                baseType,
                selectedLines.Any(line => line.HasGeneratedOptionEvidence),
                firstLine.CandidatePoolMembershipIds,
                CombineSourceSemanticFingerprints(selectedLines.Select(line =>
                    line.SemanticFingerprint)),
                mechanicalIndex);
            index += selectedLength;
        }
    }

    private static bool SameSourceSemantics(SelectedEffectLine first, SelectedEffectLine second) =>
        first.HasGeneratedOptionEvidence == second.HasGeneratedOptionEvidence &&
        (!first.HasGeneratedOptionEvidence || first.CandidatePoolMembershipIds.SequenceEqual(
            second.CandidatePoolMembershipIds,
            StringComparer.Ordinal));

    private static UniqueModifierSemanticFingerprint CombineSourceSemanticFingerprints(
        IEnumerable<UniqueModifierSemanticFingerprint> fingerprints)
    {
        var materialized = fingerprints.ToArray();
        var localities = materialized.Select(fingerprint => fingerprint.Locality).ToArray();
        var locality = localities.Length == 0 ||
            localities.Any(value => value == UniqueModifierSemanticLocality.Unknown)
                ? UniqueModifierSemanticLocality.Unknown
                : localities.Distinct().Count() == 1
                    ? localities[0]
                    : UniqueModifierSemanticLocality.Mixed;
        return new UniqueModifierSemanticFingerprint
        {
            Locality = locality,
            EvidenceMethods = materialized
                .SelectMany(fingerprint => fingerprint.EvidenceMethods)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static UniqueModifierBlock BuildBlock(
        string identityId,
        string versionLabel,
        IReadOnlyList<string> lines,
        UniqueModifierBlockKind kind,
        string observationId,
        bool isGeneratedSource,
        bool isGeneratedCandidate,
        string baseType,
        bool hasGeneratedOptionEvidence,
        IReadOnlyList<string> candidatePoolMembershipIds,
        UniqueModifierSemanticFingerprint sourceSemanticFingerprint,
        MechanicalIndex mechanicalIndex)
    {
        var signatures = lines.Select(NormalizeSignature).ToArray();
        var signature = string.Join("\n", signatures);
        var resolution = mechanicalIndex.Resolve(
            lines,
            baseType,
            hasGeneratedOptionEvidence,
            sourceSemanticFingerprint);
        var candidates = resolution.Candidates;
        var semanticFingerprints = candidates
            .Select(SemanticFingerprintEquivalenceKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var status = candidates.Count switch
        {
            0 => UniqueModifierMechanicalMappingStatus.Unsupported,
            1 => UniqueModifierMechanicalMappingStatus.Exact,
            _ when semanticFingerprints.Length == 1 => UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            _ => UniqueModifierMechanicalMappingStatus.Ambiguous,
        };
        var resolved = status is UniqueModifierMechanicalMappingStatus.Exact or
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet;
        var translationEvidence = candidates
            .SelectMany(candidate => candidate.ProvenanceTranslations)
            .DistinctBy(evidence => string.Join(
                '\u001f',
                evidence.TranslationId,
                string.Join(',', evidence.ModifierStatIndices),
                string.Join(',', evidence.DefaultedStatIds)), StringComparer.OrdinalIgnoreCase)
            .OrderBy(evidence => evidence.TranslationId, StringComparer.Ordinal)
            .ToArray();
        var resolutionReasons = resolution.ResolutionReasons
            .Concat(translationEvidence.Any(evidence => evidence.IndexHandlers.Any(handler =>
                    handler.Handlers.Any(IsStructuredOptionHandler)))
                ? ["structured-translation-option"]
                : [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reason => reason, StringComparer.Ordinal)
            .ToArray();
        return new UniqueModifierBlock
        {
            Id = isGeneratedCandidate
                ? StableId(
                    "unique-block",
                    identityId,
                    versionLabel,
                    kind.ToString(),
                    signature,
                    string.Join('\u001f', candidatePoolMembershipIds),
                    string.Join('\n', lines))
                : StableId("unique-block", identityId, versionLabel, kind.ToString(), signature),
            Kind = kind,
            Lines = lines,
            CanonicalSignatures = signatures,
            SourceSemantics = isGeneratedCandidate
                ? UniqueModifierSourceSemantics.GeneratedCandidate
                : UniqueModifierSourceSemantics.Fixed,
            SourceSemanticFingerprint = sourceSemanticFingerprint,
            CandidatePoolMembershipIds = candidatePoolMembershipIds,
            MechanicalMapping = new UniqueModifierMechanicalMapping
            {
                Status = status,
                ModifierIds = candidates.Select(candidate => candidate.ModifierId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                StatIds = semanticFingerprints.Length == 1 ? candidates[0].StatIds : [],
                Provenance = resolved && resolutionReasons.Length > 0
                    ? new UniqueModifierMechanicalProvenance
                    {
                        ResolutionReasons = resolutionReasons,
                        Translations = translationEvidence,
                        SourceSemanticFingerprint = sourceSemanticFingerprint,
                        MatchedSemanticFingerprint = candidates[0].CandidateSemanticFingerprint,
                        UsedComposition = translationEvidence.Length > 1 ||
                            translationEvidence.Any(evidence => evidence.DefaultedStatIds.Count > 0),
                        CatalogValuesUsedForSelection = resolution.UsedStrictEvidence,
                        ValueAuthority = "copiedInstance",
                        SafetyRationale = "Pinned modifier, translation-condition, and base-property evidence leaves one mechanical stat vector; copied instance values remain authoritative.",
                    }
                    : null,
                DiagnosticCode = status switch
                {
                    UniqueModifierMechanicalMappingStatus.Unsupported when
                        resolution.RejectedBySemanticFingerprint =>
                        "UNIQUE_MECHANICS_SEMANTIC_MISMATCH",
                    UniqueModifierMechanicalMappingStatus.Unsupported when isGeneratedSource =>
                        "UNIQUE_GENERATED_MECHANICS_NOT_FOUND",
                    UniqueModifierMechanicalMappingStatus.Unsupported => "UNIQUE_MECHANICS_NOT_FOUND",
                    UniqueModifierMechanicalMappingStatus.Ambiguous when resolution.UsedStrictEvidence =>
                        "UNIQUE_MECHANICS_EXACT_CONFLICT",
                    UniqueModifierMechanicalMappingStatus.Ambiguous => "UNIQUE_MECHANICS_CONFLICT",
                    _ => null,
                },
                Diagnostic = status switch
                {
                    UniqueModifierMechanicalMappingStatus.Unsupported when
                        resolution.RejectedBySemanticFingerprint =>
                        "Exact PoB text/value evidence matched RePoE candidates, but every candidate contradicted the available source semantic fingerprint.",
                    UniqueModifierMechanicalMappingStatus.Unsupported when isGeneratedSource =>
                        "No exact or safely equivalent Unique-generation RePoE translation matched this evaluated generated PoB source block.",
                    UniqueModifierMechanicalMappingStatus.Unsupported =>
                        "No exact Unique-generation evidence or broader RePoE stat-translation signature matched this PoB Unique source block.",
                    UniqueModifierMechanicalMappingStatus.Ambiguous when resolution.UsedStrictEvidence =>
                        "Exact Unique-generation text and value evidence matched conflicting RePoE mechanical stat vectors.",
                    UniqueModifierMechanicalMappingStatus.Ambiguous =>
                        "The PoB Unique line matched conflicting RePoE mechanical stat vectors.",
                    _ => null,
                },
            },
            SourceObservationIds = [observationId],
        };
    }

    private static MechanicalIndex BuildMechanicalIndex(
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord> baseItems,
        IReadOnlyList<ItemPropertySemanticDescriptor> itemPropertySemantics,
        IReadOnlyList<StatDefinition> stats)
    {
        var statsById = stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.Id))
            .GroupBy(stat => stat.Id!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var translationByVector = translations
            .GroupBy(translation => VectorKey(translation.StatIds), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var translationsByStat = translations
            .Where(translation => translation.StatIds.Count > 0)
            .SelectMany(translation => translation.StatIds
                .Where(statId => !string.IsNullOrWhiteSpace(statId))
                .Select(statId => new KeyValuePair<string, StatTranslationDefinition>(
                    statId.Trim(),
                    translation)))
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Value)
                    .DistinctBy(translation => translation.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var translationsByFirstStat = translations
            .Where(translation => translation.StatIds.Count > 0 &&
                !string.IsNullOrWhiteSpace(translation.StatIds[0]))
            .GroupBy(translation => translation.StatIds[0].Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var broadIndex = new Dictionary<string, List<MechanicalCandidate>>(StringComparer.OrdinalIgnoreCase);
        var exactIndex = new Dictionary<string, List<MechanicalCandidate>>(StringComparer.Ordinal);
        var dynamicPatterns = new List<DynamicMechanicalCandidate>();
        var partialExactIndex = new Dictionary<string, List<MechanicalCandidate>>(StringComparer.Ordinal);
        var partialDynamicPatterns = new List<DynamicMechanicalCandidate>();
        foreach (var modifier in modifiers)
        {
            var statIds = modifier.Stats.OrderBy(stat => stat.Index)
                .Select(stat => stat.StatId?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToArray();
            if (statIds.Length == 0)
            {
                continue;
            }

            if (translationByVector.TryGetValue(VectorKey(statIds), out var matches))
            {
                foreach (var translation in matches)
                foreach (var variant in translation.Variants)
                {
                    if (variant.FormatLines.Count == 0)
                    {
                        continue;
                    }

                    var signatures = variant.FormatLines
                        .Select(line => NormalizeSignature(Render(line, variant.ValueFormats)))
                        .Where(signature => signature.Length > 0)
                        .ToArray();
                    var signature = string.Join("\n", signatures);
                    if (signature.Length == 0)
                    {
                        continue;
                    }

                    if (!broadIndex.TryGetValue(signature, out var candidates))
                    {
                        candidates = [];
                        broadIndex.Add(signature, candidates);
                    }
                    candidates.Add(new MechanicalCandidate(
                        modifier.Id!,
                        statIds,
                        modifier.Domain,
                        TranslationEvidence: [CreateTranslationEvidence(
                            translation,
                            variant,
                            statIds)],
                        SemanticFingerprint: BuildCandidateSemanticFingerprint(
                            statIds,
                            statsById,
                            [CreateTranslationEvidence(translation, variant, statIds)])));
                }
            }

            if (!string.Equals(
                    modifier.SourceGenerationType?.Trim(),
                    "unique",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var strictCandidate = new MechanicalCandidate(
                modifier.Id!,
                statIds,
                modifier.Domain,
                SemanticFingerprint: BuildCandidateSemanticFingerprint(
                    statIds,
                    statsById,
                    []));
            foreach (var rendering in BuildStrictRenderings(
                         modifier,
                         translationsByFirstStat,
                         allowMissingStats: false))
            {
                var evidencedCandidate = strictCandidate with
                {
                    StrictValueEvidenceCount = rendering.ValueEvidenceCount,
                    StrictPatternSpecificity = rendering.DynamicPatternText?.Length ?? 0,
                    TranslationEvidence = rendering.TranslationEvidence,
                    OrderedRenderingText = rendering.ExactText,
                    SemanticFingerprint = BuildCandidateSemanticFingerprint(
                        statIds,
                        statsById,
                        rendering.TranslationEvidence),
                };
                if (rendering.DynamicPatternText is not null)
                {
                    dynamicPatterns.Add(new DynamicMechanicalCandidate(
                        evidencedCandidate,
                        new Regex(
                            $"\\A{rendering.DynamicPatternText}\\z",
                            RegexOptions.CultureInvariant)));
                    continue;
                }

                var exactKey = UnorderedMultilineKey(rendering.ExactText!);
                if (!exactIndex.TryGetValue(exactKey, out var exactCandidates))
                {
                    exactCandidates = [];
                    exactIndex.Add(exactKey, exactCandidates);
                }
                exactCandidates.Add(evidencedCandidate);
            }

            foreach (var rendering in BuildStrictRenderings(
                         modifier,
                         translationsByStat,
                         allowMissingStats: true).Where(rendering =>
                         rendering.TranslationEvidence.Any(evidence =>
                             evidence.DefaultedStatIds.Count > 0)))
            {
                var evidencedCandidate = strictCandidate with
                {
                    StrictValueEvidenceCount = rendering.ValueEvidenceCount,
                    StrictPatternSpecificity = rendering.DynamicPatternText?.Length ?? 0,
                    TranslationEvidence = rendering.TranslationEvidence,
                    OrderedRenderingText = rendering.ExactText,
                    SemanticFingerprint = BuildCandidateSemanticFingerprint(
                        statIds,
                        statsById,
                        rendering.TranslationEvidence),
                };
                if (rendering.DynamicPatternText is not null)
                {
                    partialDynamicPatterns.Add(new DynamicMechanicalCandidate(
                        evidencedCandidate,
                        new Regex(
                            $"\\A{rendering.DynamicPatternText}\\z",
                            RegexOptions.CultureInvariant)));
                    continue;
                }

                var partialKey = UnorderedMultilineKey(rendering.ExactText!);
                if (!partialExactIndex.TryGetValue(partialKey, out var partialCandidates))
                {
                    partialCandidates = [];
                    partialExactIndex.Add(partialKey, partialCandidates);
                }
                partialCandidates.Add(evidencedCandidate);
            }
        }

        return new MechanicalIndex(
            FreezeIndex(broadIndex, StringComparer.OrdinalIgnoreCase),
            FreezeIndex(exactIndex, StringComparer.Ordinal),
            dynamicPatterns
                .DistinctBy(candidate => string.Join(
                    '\u001f',
                    candidate.Candidate.ModifierId,
                    candidate.Pattern.ToString()), StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate.Candidate.ModifierId, StringComparer.Ordinal)
                .ToArray(),
            FreezeIndex(partialExactIndex, StringComparer.Ordinal),
            partialDynamicPatterns
                .DistinctBy(candidate => string.Join(
                    '\u001f',
                    candidate.Candidate.ModifierId,
                    candidate.Pattern.ToString()), StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate.Candidate.ModifierId, StringComparer.Ordinal)
                .ToArray(),
            baseItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Name) &&
                    !string.IsNullOrWhiteSpace(item.Domain))
                .GroupBy(item => item.Name!.Trim(), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlySet<string>)group
                        .Select(item => item.Domain!.Trim())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    StringComparer.Ordinal),
            baseItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name!.Trim(), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => new BaseMechanicalCapability(
                        HasWeaponProperties: group.Any(item => item.WeaponProperties is not null),
                        HasDefenceProperties: group.Any(item => item.DefenceProperties is not null)),
                    StringComparer.Ordinal),
            itemPropertySemantics
                .Where(descriptor => descriptor.OrderedStatIds.Count > 0)
                .GroupBy(
                    descriptor => VectorKey(descriptor.OrderedStatIds),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase));
    }

    private static UniqueModifierTranslationEvidence CreateTranslationEvidence(
        StatTranslationDefinition translation,
        StatTranslationVariant variant,
        IReadOnlyList<string> statIds) => new()
    {
        TranslationId = translation.Id,
        StatIds = translation.StatIds.ToArray(),
        ModifierStatIndices = Enumerable.Range(0, statIds.Count).ToArray(),
        Conditions = variant.Conditions.ToArray(),
        ValueFormats = variant.ValueFormats.ToArray(),
        FormatLines = variant.FormatLines.ToArray(),
        IndexHandlers = variant.IndexHandlers.ToArray(),
    };

    private static UniqueModifierSemanticFingerprint BuildCandidateSemanticFingerprint(
        IReadOnlyList<string> statIds,
        IReadOnlyDictionary<string, StatDefinition> statsById,
        IReadOnlyList<UniqueModifierTranslationEvidence> translations)
    {
        var knownStats = statIds
            .Select(statId => statsById.GetValueOrDefault(statId))
            .ToArray();
        var locality = knownStats.Any(stat => stat is null)
            ? UniqueModifierSemanticLocality.Unknown
            : knownStats.All(stat => stat!.IsLocal)
                ? UniqueModifierSemanticLocality.Local
                : knownStats.All(stat => !stat!.IsLocal)
                    ? UniqueModifierSemanticLocality.Global
                    : UniqueModifierSemanticLocality.Mixed;
        var orderedTranslations = translations
            .OrderBy(evidence => evidence.ModifierStatIndices.Count == 0
                ? int.MaxValue
                : evidence.ModifierStatIndices.Min())
            .ThenBy(evidence => evidence.TranslationId, StringComparer.Ordinal)
            .ToArray();
        var values = new List<UniqueModifierSemanticValue>();
        foreach (var evidence in orderedTranslations)
        {
            for (var index = 0; index < evidence.StatIds.Count; index++)
            {
                var format = ResolveValueFormat(index, evidence);
                var statId = evidence.StatIds[index]?.Trim();
                values.Add(new UniqueModifierSemanticValue
                {
                    Index = values.Count,
                    StatId = statId,
                    Format = string.IsNullOrWhiteSpace(format) ? null : format,
                    Unit = DetectValueUnit(index, format, evidence.FormatLines),
                    Transformations = evidence.IndexHandlers
                        .Where(handler => handler.Index == index)
                        .SelectMany(handler => handler.Handlers)
                        .SelectMany(NormalizeValueTransformation)
                        .ToArray(),
                    IsAuxiliary = evidence.DefaultedStatIds.Contains(
                        statId,
                        StringComparer.OrdinalIgnoreCase),
                });
            }
        }
        var hasCompleteValueProjection = values.Count > 0 && values.All(value =>
            !string.IsNullOrWhiteSpace(value.StatId) &&
            !string.IsNullOrWhiteSpace(value.Format) &&
            !string.IsNullOrWhiteSpace(value.Unit));
        if (!hasCompleteValueProjection)
        {
            values.Clear();
        }
        var displayedValues = values.Count(value => !string.Equals(
            value.Format,
            "ignore",
            StringComparison.OrdinalIgnoreCase));
        var valueShape = values.Count == 0
            ? UniqueModifierSemanticValueShape.Unknown
            : displayedValues == 0
                ? UniqueModifierSemanticValueShape.Presence
                : displayedValues == 1
                    ? UniqueModifierSemanticValueShape.Scalar
                    : UniqueModifierSemanticValueShape.Vector;
        return new UniqueModifierSemanticFingerprint
        {
            Locality = locality,
            OrderedStatIds = statIds.ToArray(),
            ValueShape = valueShape,
            Values = values,
            AuxiliaryStatIds = orderedTranslations
                .SelectMany(evidence => evidence.DefaultedStatIds)
                .Where(statId => !string.IsNullOrWhiteSpace(statId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            EvidenceMethods = ["repoe-stat-vector-v1"],
        };
    }

    private static string? ResolveValueFormat(
        int index,
        UniqueModifierTranslationEvidence evidence)
    {
        var declared = index < evidence.ValueFormats.Count
            ? evidence.ValueFormats[index]?.Trim()
            : null;
        if (!string.IsNullOrWhiteSpace(declared))
        {
            return declared;
        }

        var placeholder = $"{{{index}}}";
        return evidence.FormatLines.Any(line => line.Contains(
            placeholder,
            StringComparison.Ordinal))
                ? null
                : "ignore";
    }

    private static string DetectValueUnit(
        int index,
        string? format,
        IReadOnlyList<string> formatLines)
    {
        if (string.Equals(format?.Trim(), "ignore", StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }
        if (format?.Contains('%', StringComparison.Ordinal) == true)
        {
            return "percent";
        }
        var placeholder = Regex.Escape($"{{{index}}}");
        return formatLines.Any(line => Regex.IsMatch(
                line,
                $"(?:{placeholder}\\s*%|%\\s*{placeholder})",
                RegexOptions.CultureInvariant))
            ? "percent"
            : "number";
    }

    private static IReadOnlyList<string> NormalizeValueTransformation(string rawHandler)
    {
        var handler = rawHandler.Trim().ToLowerInvariant();
        return handler switch
        {
            "" => [],
            "negate" => ["scale:-1"],
            "double" => ["scale:2"],
            "negate_and_double" => ["scale:-2"],
            "divide_by_one_hundred" => ["scale:0.01"],
            "divide_by_one_hundred_2dp" => ["scale:0.01", "round:2"],
            "divide_by_one_hundred_2dp_if_required" =>
                ["scale:0.01", "round:2-if-required"],
            "old_leech_percent" => ["scale:0.2"],
            "old_leech_permyriad" => ["scale:0.002"],
            "passive_hash" => ["lookup:passive"],
            _ when handler.StartsWith("display_indexable_", StringComparison.Ordinal) =>
                [$"lookup:{handler["display_indexable_".Length..]}"],
            _ => [$"source:{handler}"],
        };
    }

    private static string SemanticFingerprintKey(UniqueModifierSemanticFingerprint fingerprint) =>
        string.Join(
            '\u001d',
            fingerprint.Locality.ToString(),
            string.Join('\u001f', fingerprint.OrderedStatIds.Select(value =>
                value.Trim().ToLowerInvariant())),
            fingerprint.ValueShape.ToString(),
            string.Join('\u001e', fingerprint.Values.Select(value => string.Join(
                '\u001f',
                value.Index.ToString(CultureInfo.InvariantCulture),
                value.StatId?.Trim().ToLowerInvariant(),
                value.Format?.Trim().ToLowerInvariant(),
                value.Unit?.Trim().ToLowerInvariant(),
                string.Join(',', value.Transformations.Select(transform =>
                    transform.Trim().ToLowerInvariant())),
                value.IsAuxiliary.ToString(CultureInfo.InvariantCulture)))),
            string.Join('\u001f', fingerprint.AuxiliaryStatIds.Select(value =>
                value.Trim().ToLowerInvariant())));

    private static string SemanticFingerprintEquivalenceKey(MechanicalCandidate candidate)
    {
        var fingerprint = candidate.CandidateSemanticFingerprint;
        var key = SemanticFingerprintKey(fingerprint);
        var hasComparableValueProjection =
            fingerprint.ValueShape != UniqueModifierSemanticValueShape.Unknown &&
            fingerprint.Values.Count > 0;
        return hasComparableValueProjection
            ? key
            : string.Join('\u001d', key, candidate.ModifierId.Trim().ToLowerInvariant());
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<MechanicalCandidate>> FreezeIndex(
        IReadOnlyDictionary<string, List<MechanicalCandidate>> source,
        IEqualityComparer<string> comparer) => source.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MechanicalCandidate>)pair.Value
                .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
                .ToArray(),
            comparer);

    private static IReadOnlyList<StrictRendering> BuildStrictRenderings(
        ModifierDefinition modifier,
        IReadOnlyDictionary<string, StatTranslationDefinition[]> translationsByStat,
        bool allowMissingStats)
    {
        var stats = modifier.Stats.OrderBy(stat => stat.Index).ToArray();
        var allGroups = stats
            .Select(stat => stat.StatId?.Trim())
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .Cast<string>()
            .SelectMany(statId => translationsByStat.GetValueOrDefault(statId) ?? [])
            .DistinctBy(translation => translation.Id, StringComparer.OrdinalIgnoreCase)
            .Select(translation => TryCreateStrictRenderingGroup(
                translation,
                stats,
                allowMissingStats))
            .Where(group => group is not null)
            .Select(group => group!)
            .OrderBy(group => group.StatIndices.Min())
            .ThenBy(group => group.TranslationId, StringComparer.Ordinal)
            .ToArray();
        var groups = allGroups;
        if (groups.Length == 0)
        {
            return [];
        }

        var selections = new List<IReadOnlyList<StrictRenderingGroup>>();
        SelectCompatibleRenderingGroups(
            groups,
            position: 0,
            selected: [],
            occupiedStatIndices: [],
            selections);
        var maximumCoveredStats = selections.Max(selection => selection
            .SelectMany(group => group.StatIndices)
            .Distinct()
            .Count());
        var renderings = new List<StrictRendering>();
        foreach (var selection in selections.Where(selection => selection
                     .SelectMany(group => group.StatIndices)
                     .Distinct()
                     .Count() == maximumCoveredStats))
        {
            IReadOnlyList<StrictRendering> combined = [StrictRendering.Static(string.Empty, 0)];
            foreach (var group in selection.OrderBy(group => group.StatIndices.Min()))
            {
                combined = combined.SelectMany(left => group.Renderings.Select(right =>
                        StrictRendering.Combine(left, right)))
                    .DistinctBy(rendering => rendering.Key, StringComparer.Ordinal)
                    .Take(256)
                    .ToArray();
            }
            renderings.AddRange(combined);
        }
        if (!allowMissingStats)
        {
            return renderings
                .Where(rendering => rendering.Key.Length > 0)
                .DistinctBy(rendering => rendering.Key, StringComparer.Ordinal)
                .Take(256)
                .ToArray();
        }

        var distinctRenderings = renderings
            .Where(rendering => rendering.Key.Length > 0)
            .GroupBy(rendering => rendering.Key, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(rendering => rendering.TranslationEvidence.Sum(
                    evidence => evidence.DefaultedStatIds.Count))
                .First())
            .ToArray();
        if (distinctRenderings.Length == 0)
        {
            return [];
        }
        var fewestDefaultedStats = distinctRenderings.Min(rendering => rendering
            .TranslationEvidence
            .Sum(evidence => evidence.DefaultedStatIds.Count));
        return distinctRenderings
            .Where(rendering => rendering.TranslationEvidence.Sum(
                evidence => evidence.DefaultedStatIds.Count) == fewestDefaultedStats)
            .Take(256)
            .ToArray();
    }

    private static StrictRenderingGroup? TryCreateStrictRenderingGroup(
        StatTranslationDefinition translation,
        IReadOnlyList<ModifierStat> modifierStats,
        bool allowMissingStats)
    {
        var indices = new List<int>();
        var stats = new List<ModifierStat>();
        var defaultedStatIds = new List<string>();
        var matchedModifierIndices = new HashSet<int>();
        foreach (var rawStatId in translation.StatIds)
        {
            var statId = rawStatId?.Trim();
            var found = -1;
            for (var index = 0; index < modifierStats.Count; index++)
            {
                if (!matchedModifierIndices.Contains(index) && string.Equals(
                        modifierStats[index].StatId?.Trim(),
                        statId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    found = index;
                    break;
                }
            }
            if (found < 0)
            {
                if (!allowMissingStats || string.IsNullOrWhiteSpace(statId))
                {
                    return null;
                }
                stats.Add(new ModifierStat
                {
                    Index = stats.Count,
                    StatId = statId,
                    MinValue = 0m,
                    MaxValue = 0m,
                });
                defaultedStatIds.Add(statId);
                continue;
            }
            indices.Add(found);
            stats.Add(modifierStats[found]);
            matchedModifierIndices.Add(found);
        }

        if (indices.Count == 0)
        {
            return null;
        }

        var renderings = translation.Variants
            .Select(variant => TryConstrainStats(
                    variant,
                    stats,
                    allowNegatedConditions: defaultedStatIds.Count > 0,
                    out var constrained)
                ? TryCreateStrictRendering(variant, constrained) is { } rendering
                    ? rendering with
                    {
                        TranslationEvidence =
                        [
                            new UniqueModifierTranslationEvidence
                            {
                                TranslationId = translation.Id,
                                StatIds = translation.StatIds.ToArray(),
                                ModifierStatIndices = indices.ToArray(),
                                DefaultedStatIds = defaultedStatIds.ToArray(),
                                Conditions = variant.Conditions.ToArray(),
                                ValueFormats = variant.ValueFormats.ToArray(),
                                FormatLines = variant.FormatLines.ToArray(),
                                IndexHandlers = variant.IndexHandlers.ToArray(),
                            },
                        ],
                    }
                    : null
                : null)
            .Where(rendering => rendering is not null)
            .Select(rendering => rendering!)
            .DistinctBy(rendering => rendering.Key, StringComparer.Ordinal)
            .ToArray();
        return renderings.Length == 0
            ? null
            : new StrictRenderingGroup(translation.Id!, indices, renderings);
    }

    private static void SelectCompatibleRenderingGroups(
        IReadOnlyList<StrictRenderingGroup> groups,
        int position,
        List<StrictRenderingGroup> selected,
        HashSet<int> occupiedStatIndices,
        List<IReadOnlyList<StrictRenderingGroup>> selections)
    {
        if (position == groups.Count)
        {
            if (selected.Count > 0)
            {
                selections.Add(selected.ToArray());
            }
            return;
        }

        SelectCompatibleRenderingGroups(
            groups,
            position + 1,
            selected,
            occupiedStatIndices,
            selections);
        var group = groups[position];
        if (group.StatIndices.Any(occupiedStatIndices.Contains))
        {
            return;
        }
        selected.Add(group);
        foreach (var index in group.StatIndices)
        {
            occupiedStatIndices.Add(index);
        }
        SelectCompatibleRenderingGroups(
            groups,
            position + 1,
            selected,
            occupiedStatIndices,
            selections);
        selected.RemoveAt(selected.Count - 1);
        foreach (var index in group.StatIndices)
        {
            occupiedStatIndices.Remove(index);
        }
    }

    private static bool TryConstrainStats(
        StatTranslationVariant variant,
        IReadOnlyList<ModifierStat> stats,
        bool allowNegatedConditions,
        out IReadOnlyList<ModifierStat> constrained)
    {
        constrained = [];
        if (variant.Conditions.Count != stats.Count)
        {
            return false;
        }

        var conditions = variant.Conditions
            .GroupBy(condition => condition.Index)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var result = new ModifierStat[stats.Count];
        for (var index = 0; index < stats.Count; index++)
        {
            var stat = stats[index];
            if (!stat.MinValue.HasValue ||
                !stat.MaxValue.HasValue ||
                !conditions.TryGetValue(index, out var indexed) ||
                indexed.Length != 1)
            {
                return false;
            }
            if (indexed[0].IsNegated)
            {
                if (!allowNegatedConditions)
                {
                    return false;
                }
                var excludedMinimum = indexed[0].MinValue ?? decimal.MinValue;
                var excludedMaximum = indexed[0].MaxValue ?? decimal.MaxValue;
                if (stat.MaxValue.Value < excludedMinimum ||
                    stat.MinValue.Value > excludedMaximum)
                {
                    result[index] = stat;
                    continue;
                }

                // A partially overlapping negated interval would require a disjoint range.
                // Retain fail-closed behavior instead of manufacturing one interval.
                return false;
            }
            var minimum = Math.Max(
                stat.MinValue.Value,
                indexed[0].MinValue ?? stat.MinValue.Value);
            var maximum = Math.Min(
                stat.MaxValue.Value,
                indexed[0].MaxValue ?? stat.MaxValue.Value);
            if (minimum > maximum)
            {
                return false;
            }
            result[index] = stat with { MinValue = minimum, MaxValue = maximum };
        }
        constrained = result;
        return true;
    }

    private static StrictRendering? TryCreateStrictRendering(
        StatTranslationVariant variant,
        IReadOnlyList<ModifierStat> stats)
    {
        if (variant.ValueFormats.Count != stats.Count ||
            variant.IndexHandlers.Count != stats.Count)
        {
            return null;
        }

        var replacements = new Dictionary<int, StrictValue>();
        for (var index = 0; index < stats.Count; index++)
        {
            var handlers = variant.IndexHandlers
                .Where(handler => handler.Index == index)
                .ToArray();
            if (handlers.Length != 1)
            {
                return null;
            }
            if (variant.ValueFormats[index].Trim().Equals("ignore", StringComparison.OrdinalIgnoreCase))
            {
                if (variant.FormatLines.Any(line =>
                        line.Contains($"{{{index}}}", StringComparison.Ordinal)))
                {
                    return null;
                }
                continue;
            }
            if (!TryCreateStrictValue(
                    stats[index],
                    variant.ValueFormats[index],
                    handlers[0].Handlers,
                    out var replacement))
            {
                return null;
            }
            replacements[index] = replacement;
        }

        var exactLines = new List<string>();
        var patternLines = new List<string>();
        var hasDynamicValue = false;
        foreach (var sourceLine in variant.FormatLines)
        {
            var exactLine = sourceLine;
            var pattern = Regex.Escape(sourceLine);
            foreach (var replacement in replacements)
            {
                var placeholder = $"{{{replacement.Key}}}";
                if (!sourceLine.Contains(placeholder, StringComparison.Ordinal))
                {
                    continue;
                }

                if (replacement.Value.IsDynamic)
                {
                    hasDynamicValue = true;
                    exactLine = exactLine.Replace(placeholder, "<dynamic>", StringComparison.Ordinal);
                    pattern = pattern.Replace(Regex.Escape(placeholder), "[^\\r\\n]+?", StringComparison.Ordinal);
                }
                else
                {
                    exactLine = exactLine.Replace(placeholder, replacement.Value.Text, StringComparison.Ordinal);
                    pattern = pattern.Replace(
                        Regex.Escape(placeholder),
                        Regex.Escape(replacement.Value.Text),
                        StringComparison.Ordinal);
                }
            }

            if (UnresolvedPlaceholderPattern().IsMatch(exactLine))
            {
                return null;
            }

            exactLines.Add(NormalizeExactEvidence(exactLine));
            patternLines.Add(NormalizePatternWhitespace(pattern));
        }

        var exactText = string.Join("\n", exactLines);
        return hasDynamicValue
            ? StrictRendering.Dynamic(string.Join("\\n", patternLines), replacements.Count)
            : StrictRendering.Static(exactText, replacements.Count);
    }

    private static bool TryCreateStrictValue(
        ModifierStat stat,
        string format,
        IReadOnlyList<string> handlers,
        out StrictValue value)
    {
        value = default;
        if (handlers.Count == 1 && IsStructuredOptionHandler(handlers[0]))
        {
            value = StrictValue.Dynamic;
            return format.Trim() == "#";
        }

        if (format.Trim() is not ("#" or "+#") ||
            !stat.MinValue.HasValue ||
            !stat.MaxValue.HasValue ||
            !TryProjectValue(stat.MinValue.Value, handlers, out var minimum) ||
            !TryProjectValue(stat.MaxValue.Value, handlers, out var maximum))
        {
            return false;
        }

        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }
        var prefix = format.Trim() == "+#" && minimum >= 0m ? "+" : string.Empty;
        value = new StrictValue(
            prefix + (minimum == maximum
                ? FormatDecimal(minimum)
                : $"({FormatDecimal(minimum)}-{FormatDecimal(maximum)})"),
            IsDynamic: false);
        return true;
    }

    private static bool TryProjectValue(
        decimal source,
        IReadOnlyList<string> handlers,
        out decimal projected)
    {
        projected = source;
        foreach (var rawHandler in handlers)
        {
            var handler = rawHandler.Trim().ToLowerInvariant();
            projected = handler switch
            {
                "" => projected,
                "negate" => -projected,
                "double" => projected * 2m,
                "negate_and_double" => -projected * 2m,
                "divide_by_one_hundred" or
                    "divide_by_one_hundred_2dp" or
                    "divide_by_one_hundred_2dp_if_required" => projected / 100m,
                "old_leech_percent" => projected / 5m,
                "old_leech_permyriad" => projected / 500m,
                _ => decimal.MinValue,
            };
            if (projected == decimal.MinValue)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsStructuredOptionHandler(string rawHandler)
    {
        var handler = rawHandler.Trim();
        return handler.StartsWith("display_indexable_", StringComparison.OrdinalIgnoreCase) ||
            handler.Equals("passive_hash", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string NormalizeExactEvidence(string value) =>
        WhitespacePattern().Replace(value.Trim(), " ");

    private static string UnorderedMultilineKey(string value) =>
        UnorderedMultilineKey(value.Split('\n'));

    private static string UnorderedMultilineKey(IEnumerable<string> lines) => string.Join(
        "\n",
        lines.Select(NormalizeExactEvidence).OrderBy(line => line, StringComparer.Ordinal));

    private static string NormalizePatternWhitespace(string value) =>
        Regex.Replace(value.Trim(), @"(?:\\ )+", @"\s+", RegexOptions.CultureInvariant);

    private static bool TryParseSourceItem(string raw, out ParsedSourceItem item)
    {
        item = default!;
        var lines = raw.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
        while (lines.Count > 0 && (lines[0].StartsWith("Item Class:", StringComparison.Ordinal) ||
            lines[0].StartsWith("Rarity:", StringComparison.Ordinal)))
        {
            lines.RemoveAt(0);
        }
        if (lines.Count < 2)
        {
            return false;
        }

        var name = StripDirectives(lines[0], out _);
        var baseIndex = 1;
        while (baseIndex < lines.Count && IsMetadataLine(lines[baseIndex]))
        {
            baseIndex++;
        }
        if (baseIndex >= lines.Count)
        {
            return false;
        }
        var firstBaseType = StripDirectives(lines[baseIndex], out var firstBaseVariants);
        if (name.Length == 0 || firstBaseType.Length == 0)
        {
            return false;
        }

        var baseTypes = new List<SourceBaseType>
        {
            UnresolvedBaseType(firstBaseType, firstBaseVariants),
        };
        var contentStart = baseIndex + 1;
        while (contentStart < lines.Count &&
            VariantDirectivePattern().IsMatch(lines[contentStart]))
        {
            var baseType = StripDirectives(lines[contentStart], out var baseVariants);
            if (baseType.Length == 0 || baseVariants.Count == 0)
            {
                break;
            }
            baseTypes.Add(UnresolvedBaseType(baseType, baseVariants));
            contentStart++;
        }

        var variants = new List<SourceVariant>();
        var effects = new List<SourceEffectLine>();
        var selectedVariantIndices = new List<int>();
        var alternateVariantSlotCount = 0;
        var implicitCount = 0;
        for (var index = contentStart; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.StartsWith("Variant:", StringComparison.Ordinal))
            {
                variants.Add(new SourceVariant(variants.Count + 1, line["Variant:".Length..].Trim()));
                continue;
            }
            if ((line.StartsWith("Selected Variant:", StringComparison.Ordinal) ||
                    line.StartsWith("Selected Alt Variant", StringComparison.Ordinal)) &&
                line.IndexOf(':') is var separator && separator >= 0 &&
                int.TryParse(
                    line[(separator + 1)..].Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var selectedVariantIndex))
            {
                selectedVariantIndices.Add(selectedVariantIndex);
                continue;
            }
            if (line.StartsWith("Has Alt Variant", StringComparison.Ordinal) &&
                line.EndsWith(": true", StringComparison.OrdinalIgnoreCase))
            {
                alternateVariantSlotCount++;
                continue;
            }
            if (line.StartsWith("Implicits:", StringComparison.Ordinal) &&
                int.TryParse(line["Implicits:".Length..].Trim(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var parsedImplicitCount))
            {
                implicitCount = Math.Max(0, parsedImplicitCount);
                continue;
            }
            if (IsMetadataLine(line))
            {
                continue;
            }

            var text = StripDirectives(line, out var selectedVariants);
            if (text.Length > 0 && !IsMetadataLine(text))
            {
                effects.Add(new SourceEffectLine(text, selectedVariants, []));
            }
        }

        item = new ParsedSourceItem(
            name,
            baseTypes,
            variants,
            effects,
            selectedVariantIndices,
            alternateVariantSlotCount,
            implicitCount,
            null,
            UniqueItemKind.Unknown);
        return true;
    }

    private static IReadOnlyList<SourceEffectLine> ApplySourceSemanticFingerprints(
        JsonElement entry,
        ParsedSourceItem item,
        string sourcePath,
        List<ImportDiagnostic> diagnostics)
    {
        if (!entry.TryGetProperty("semanticFingerprints", out var sourceFingerprints) ||
            sourceFingerprints.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return item.EffectLines;
        }
        if (sourceFingerprints.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.PoBUniqueRecordUnsupported,
                ImportDiagnosticSeverity.Warning,
                sourcePath,
                "PoB source semantic fingerprints were not an array and were ignored."));
            return item.EffectLines;
        }

        var observations = new List<SourceSemanticFingerprintObservation>();
        foreach (var sourceFingerprint in sourceFingerprints.EnumerateArray())
        {
            if (sourceFingerprint.ValueKind != JsonValueKind.Object ||
                !TryReadString(sourceFingerprint, "kind", out var rawKind) ||
                !sourceFingerprint.TryGetProperty("lineIndex", out var lineIndexElement) ||
                lineIndexElement.ValueKind != JsonValueKind.Number ||
                !lineIndexElement.TryGetInt32(out var lineIndex) ||
                lineIndex < 0 ||
                !TryReadString(sourceFingerprint, "line", out var line) ||
                !TryReadString(sourceFingerprint, "baseType", out var baseType) ||
                !TryReadString(sourceFingerprint, "locality", out var rawLocality) ||
                !TryReadString(sourceFingerprint, "evidenceMethod", out var evidenceMethod) ||
                !TryParseBlockKind(rawKind, out var kind) ||
                !TryParseLocality(rawLocality, out var locality))
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.PoBUniqueRecordUnsupported,
                    ImportDiagnosticSeverity.Warning,
                    sourcePath,
                    "One PoB source semantic fingerprint was malformed and was ignored."));
                continue;
            }

            observations.Add(new SourceSemanticFingerprintObservation(
                kind,
                lineIndex,
                NormalizeExactEvidence(line),
                baseType.Trim(),
                new UniqueModifierSemanticFingerprint
                {
                    Locality = locality,
                    EvidenceMethods = [evidenceMethod.Trim()],
                }));
        }

        return item.EffectLines.Select((line, effectIndex) =>
        {
            var kind = effectIndex < item.ImplicitCount
                ? UniqueModifierBlockKind.Implicit
                : UniqueModifierBlockKind.Unique;
            var matching = observations
                .Where(observation => observation.Kind == kind &&
                    string.Equals(
                        observation.Line,
                        NormalizeExactEvidence(line.Text),
                        StringComparison.Ordinal))
                .ToArray();
            return line with { SemanticFingerprints = matching };
        }).ToArray();
    }

    private static bool TryParseBlockKind(string value, out UniqueModifierBlockKind kind)
    {
        if (string.Equals(value.Trim(), "implicit", StringComparison.OrdinalIgnoreCase))
        {
            kind = UniqueModifierBlockKind.Implicit;
            return true;
        }
        if (string.Equals(value.Trim(), "unique", StringComparison.OrdinalIgnoreCase))
        {
            kind = UniqueModifierBlockKind.Unique;
            return true;
        }
        kind = UniqueModifierBlockKind.Unknown;
        return false;
    }

    private static bool TryParseLocality(string value, out UniqueModifierSemanticLocality locality)
    {
        locality = value.Trim().ToLowerInvariant() switch
        {
            "global" => UniqueModifierSemanticLocality.Global,
            "local" => UniqueModifierSemanticLocality.Local,
            "mixed" => UniqueModifierSemanticLocality.Mixed,
            "unknown" => UniqueModifierSemanticLocality.Unknown,
            _ => (UniqueModifierSemanticLocality)(-1),
        };
        return Enum.IsDefined(locality);
    }

    private static bool IsMetadataLine(string line)
    {
        string[] prefixes =
        [
            "Requires Level ", "LevelReq:", "League:", "Source:", "Limited to:",
            "Requires Level:", "Radius:", "Upgrade:", "Crafted:", "Talisman Tier:",
            "Suffix:",
            "Has Alt Variant", "Selected Variant:", "Selected Alt Variant", "Requirements:",
            "Level:", "Item Level:", "DropLevel:", "Sockets:", "Armour:", "Evasion Rating:",
            "Energy Shield:", "Ward:", "Physical Damage:", "Critical Strike Chance:",
            "Attacks per Second:", "Weapon Range:", "Shaper Item", "Elder Item", "Synthesised Item",
            "Crusader Item", "Redeemer Item", "Hunter Item", "Warlord Item", "Eater of Worlds Item",
            "Searing Exarch Item",
        ];
        return prefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal)) ||
            line is "Corrupted" or "Mirrored" or "This item can be anointed by Cassia";
    }

    private static string StripDirectives(string line, out IReadOnlyList<int> variants)
    {
        var selected = new List<int>();
        foreach (Match match in VariantDirectivePattern().Matches(line))
        {
            foreach (var value in match.Groups[1].Value.Split(','))
            {
                if (int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    selected.Add(index);
                }
            }
        }
        variants = selected;
        return DirectivePattern().Replace(line, string.Empty).Trim();
    }

    internal static string NormalizeSignature(string value)
    {
        var normalized = RangePattern().Replace(value.Trim(), match =>
            match.Groups["sign"].Value + "<number>");
        normalized = NumberPattern().Replace(normalized, match =>
            match.Groups["sign"].Value + "<number>");
        normalized = WhitespacePattern().Replace(normalized, " ");
        return normalized.Trim();
    }

    private static string Render(string format, IReadOnlyList<string> valueFormats)
    {
        var rendered = format;
        for (var index = 0; index < valueFormats.Count; index++)
        {
            var replacement = valueFormats[index] == "+#" ? "+<number>" : "<number>";
            rendered = rendered.Replace($"{{{index}}}", replacement, StringComparison.Ordinal);
        }
        return rendered;
    }

    private static UniqueItemKind ClassifyKind(string name) =>
        name.StartsWith("Replica ", StringComparison.Ordinal)
            ? UniqueItemKind.Replica
            : name.StartsWith("Foulborn ", StringComparison.Ordinal)
                ? UniqueItemKind.FoulbornObserved
                : UniqueItemKind.Ordinary;

    private static UniqueItemVersionRole ClassifyVersionRole(
        string label,
        bool hasExplicitCurrentSibling) =>
        ClassifyVersionRoleEvidence(label, hasExplicitCurrentSibling).Role;

    private static VersionRoleEvidence ClassifyVersionRoleEvidence(
        string label,
        bool hasExplicitCurrentSibling)
    {
        if (label.Equals("Current", StringComparison.OrdinalIgnoreCase) ||
            label.StartsWith("Current ", StringComparison.OrdinalIgnoreCase) ||
            label.EndsWith(" Current", StringComparison.OrdinalIgnoreCase))
        {
            return new VersionRoleEvidence(
                UniqueItemVersionRole.Current,
                "The pinned PoB variant label explicitly marks this observation Current.");
        }

        if (HistoricalVersionMarkerPattern().IsMatch(label) ||
            label.Contains("Legacy", StringComparison.OrdinalIgnoreCase))
        {
            return new VersionRoleEvidence(
                UniqueItemVersionRole.Historical,
                "The pinned PoB variant label explicitly uses a Pre/Legacy history marker.");
        }

        if (hasExplicitCurrentSibling && BareVersionLabelPattern().IsMatch(label))
        {
            return new VersionRoleEvidence(
                UniqueItemVersionRole.Historical,
                "The pinned PoB variant label is a bare historical patch identifier and the source provides a separate explicit Current observation.");
        }

        return new VersionRoleEvidence(
            UniqueItemVersionRole.Unknown,
            "The pinned PoB variant label has no explicit current/history marker.");
    }

    private static string CanonicalIdentityKey(string name, UniqueItemKind kind) =>
        $"{kind.ToString().ToLowerInvariant()}|{UniqueSourceIdentityNormalizer.NormalizeKey(name)}";

    private static SourceBaseType UnresolvedBaseType(
        string sourceText,
        IReadOnlyList<int> variants) => new(
            sourceText,
            sourceText,
            UniqueSourceIdentityNormalizer.NormalizeKey(sourceText),
            "unresolved-source-base-text-v1",
            [],
            variants);

    private static bool TryReadString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var propertyElement) &&
            propertyElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value = propertyElement.GetString()!);
    }

    private static string VectorKey(IEnumerable<string> values) => string.Join('\u001f', values);

    private static string StableId(string prefix, params string[] values) =>
        $"{prefix}:{Sha256(string.Join('\u001f', values))[..24]}";

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static PoBUniqueCatalogImportResult Failure(string code, string message) => new()
    {
        Diagnostics = [Diagnostic(code, ImportDiagnosticSeverity.Error, null, message)],
    };

    private static ImportDiagnostic Diagnostic(
        string code, ImportDiagnosticSeverity severity, string? sourceRecordId, string message) =>
        new(code, severity, sourceRecordId, message);

    [GeneratedRegex(@"\{variant:([^}]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariantDirectivePattern();

    [GeneratedRegex(@"\{[^}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex DirectivePattern();

    [GeneratedRegex(@"(?<sign>[+-]?)\(\s*[+-]?\d+(?:[\.,]\d+)?\s*-\s*[+-]?\d+(?:[\.,]\d+)?\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RangePattern();

    [GeneratedRegex(@"(?<![A-Za-z<])(?<sign>[+-]?)\d+(?:[\.,]\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"(?:^|[\s(])Pre(?:[\s.]|$)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex HistoricalVersionMarkerPattern();

    [GeneratedRegex(@"^\d+\.\d+(?:\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex BareVersionLabelPattern();

    [GeneratedRegex(@"\{\d+\}", RegexOptions.CultureInvariant)]
    private static partial Regex UnresolvedPlaceholderPattern();

    private sealed record IdentityKey(string Name, UniqueItemKind Kind);
    private sealed record SourceBaseType(
        string Text,
        string SourceText,
        string CanonicalKey,
        string NormalizationRule,
        IReadOnlyList<string> RePoeBaseItemIds,
        IReadOnlyList<int> Variants);
    private sealed record SourceVariant(int Index, string Label);
    private sealed record SourceEffectLine(
        string Text,
        IReadOnlyList<int> Variants,
        IReadOnlyList<SourceSemanticFingerprintObservation> SemanticFingerprints);
    private sealed record SelectedEffectLine(
        string Text,
        bool HasGeneratedOptionEvidence,
        IReadOnlyList<string> CandidatePoolMembershipIds,
        UniqueModifierSemanticFingerprint SemanticFingerprint);
    private sealed record SourceSemanticFingerprintObservation(
        UniqueModifierBlockKind Kind,
        int LineIndex,
        string Line,
        string BaseType,
        UniqueModifierSemanticFingerprint Fingerprint);
    private sealed record VersionSpec(
        string Label,
        UniqueItemVersionRole Role,
        int? VariantIndex,
        string BaseType,
        string SourceBaseType,
        string CanonicalBaseTypeKey,
        string BaseTypeNormalizationRule,
        IReadOnlyList<string> RePoeBaseItemIds,
        string RoleDecisionReason,
        string VariantDecisionReason);
    private sealed record VersionRoleEvidence(
        UniqueItemVersionRole Role,
        string Reason);
    private sealed record MechanicalCandidate(
        string ModifierId,
        IReadOnlyList<string> StatIds,
        string? Domain,
        int StrictValueEvidenceCount = 0,
        int StrictPatternSpecificity = 0,
        IReadOnlyList<UniqueModifierTranslationEvidence>? TranslationEvidence = null,
        string? OrderedRenderingText = null,
        UniqueModifierSemanticFingerprint? SemanticFingerprint = null)
    {
        public IReadOnlyList<UniqueModifierTranslationEvidence> ProvenanceTranslations =>
            TranslationEvidence ?? [];

        public UniqueModifierSemanticFingerprint CandidateSemanticFingerprint =>
            SemanticFingerprint ?? new();
    }
    private sealed record MechanicalResolution(
        IReadOnlyList<MechanicalCandidate> Candidates,
        bool UsedStrictEvidence,
        IReadOnlyList<string> ResolutionReasons,
        bool RejectedBySemanticFingerprint = false);
    private sealed record DynamicMechanicalCandidate(
        MechanicalCandidate Candidate,
        Regex Pattern);
    private sealed record StrictRenderingGroup(
        string TranslationId,
        IReadOnlyList<int> StatIndices,
        IReadOnlyList<StrictRendering> Renderings);
    private readonly record struct StrictValue(string Text, bool IsDynamic)
    {
        public static StrictValue Dynamic => new(string.Empty, IsDynamic: true);
    }
    private sealed record StrictRendering(
        string? ExactText,
        string? DynamicPatternText,
        int ValueEvidenceCount,
        IReadOnlyList<UniqueModifierTranslationEvidence> TranslationEvidence)
    {
        public string Key => ExactText ?? DynamicPatternText!;

        public static StrictRendering Static(string text, int valueEvidenceCount) =>
            new(text, null, valueEvidenceCount, []);

        public static StrictRendering Dynamic(string patternText, int valueEvidenceCount) =>
            new(null, patternText, valueEvidenceCount, []);

        public static StrictRendering Combine(StrictRendering left, StrictRendering right)
        {
            if (left.DynamicPatternText is null && right.DynamicPatternText is null)
            {
                return Static(
                    Join(left.ExactText!, right.ExactText!),
                    left.ValueEvidenceCount + right.ValueEvidenceCount) with
                {
                    TranslationEvidence = left.TranslationEvidence
                        .Concat(right.TranslationEvidence)
                        .ToArray(),
                };
            }

            var leftPattern = left.DynamicPatternText ?? Regex.Escape(left.ExactText!);
            var rightPattern = right.DynamicPatternText ?? Regex.Escape(right.ExactText!);
            return Dynamic(
                Join(leftPattern, rightPattern),
                left.ValueEvidenceCount + right.ValueEvidenceCount) with
            {
                TranslationEvidence = left.TranslationEvidence
                    .Concat(right.TranslationEvidence)
                    .ToArray(),
            };
        }

        private static string Join(string left, string right) => left.Length == 0
            ? right
            : right.Length == 0
                ? left
                : left + "\n" + right;
    }
    private sealed class MechanicalIndex(
        IReadOnlyDictionary<string, IReadOnlyList<MechanicalCandidate>> broad,
        IReadOnlyDictionary<string, IReadOnlyList<MechanicalCandidate>> exact,
        IReadOnlyList<DynamicMechanicalCandidate> dynamic,
        IReadOnlyDictionary<string, IReadOnlyList<MechanicalCandidate>> partialExact,
        IReadOnlyList<DynamicMechanicalCandidate> partialDynamic,
        IReadOnlyDictionary<string, IReadOnlySet<string>> baseDomains,
        IReadOnlyDictionary<string, BaseMechanicalCapability> baseCapabilities,
        IReadOnlyDictionary<string, ItemPropertySemanticDescriptor> propertySemantics)
    {
        public bool HasMatch(
            IReadOnlyList<string> lines,
            string baseType,
            bool hasGeneratedOptionEvidence,
            UniqueModifierSemanticFingerprint sourceSemanticFingerprint) =>
            Resolve(lines, baseType, hasGeneratedOptionEvidence, sourceSemanticFingerprint)
                .Candidates.Count > 0;

        public MechanicalResolution Resolve(
            IReadOnlyList<string> lines,
            string baseType,
            bool hasGeneratedOptionEvidence,
            UniqueModifierSemanticFingerprint sourceSemanticFingerprint)
        {
            var orderedExactText = string.Join("\n", lines.Select(NormalizeExactEvidence));
            var exactText = UnorderedMultilineKey(orderedExactText);
            var staticStrict = CandidateFilterResult.Empty;
            if (exact.TryGetValue(exactText, out var staticMatches))
            {
                staticStrict = FilterCandidates(staticMatches
                    .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
                    .ToArray(), baseType, hasGeneratedOptionEvidence);
                staticStrict = staticStrict with
                {
                    Candidates = RetainStrongestValueEvidence(staticStrict.Candidates),
                };
                staticStrict = ApplySemanticFingerprint(
                    staticStrict,
                    sourceSemanticFingerprint);
            }
            var dynamicStrict = FilterCandidates(dynamic
                .Where(candidate => candidate.Pattern.IsMatch(orderedExactText))
                .Select(candidate => candidate.Candidate)
                .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
                .ToArray(), baseType, hasGeneratedOptionEvidence);
            dynamicStrict = dynamicStrict with
            {
                Candidates = RetainStrongestValueEvidence(
                    hasGeneratedOptionEvidence
                        ? RetainMostSpecificDynamicEvidence(dynamicStrict.Candidates)
                        : dynamicStrict.Candidates),
            };
            dynamicStrict = ApplySemanticFingerprint(dynamicStrict, sourceSemanticFingerprint);
            if (hasGeneratedOptionEvidence && dynamicStrict.HadCandidatesBeforeSemanticFingerprint)
            {
                return Resolution(dynamicStrict, usedStrictEvidence: true, orderedExactText);
            }
            if (staticStrict.HadCandidatesBeforeSemanticFingerprint)
            {
                return Resolution(staticStrict, usedStrictEvidence: true, orderedExactText);
            }
            if (dynamicStrict.HadCandidatesBeforeSemanticFingerprint)
            {
                return Resolution(dynamicStrict, usedStrictEvidence: true, orderedExactText);
            }

            var signature = string.Join("\n", lines.Select(NormalizeSignature));
            var broadCandidates = broad.GetValueOrDefault(signature) ?? [];
            if (broadCandidates.Count > 0)
            {
                return Resolution(
                    ApplySemanticFingerprint(
                        new CandidateFilterResult(
                            broadCandidates,
                            ExcludedByPropertyCapability: false),
                        sourceSemanticFingerprint),
                    usedStrictEvidence: false);
            }

            var partialStatic = CandidateFilterResult.Empty;
            if (partialExact.TryGetValue(exactText, out var partialStaticMatches))
            {
                partialStatic = FilterCandidates(partialStaticMatches
                    .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
                    .ToArray(), baseType, hasGeneratedOptionEvidence);
                partialStatic = partialStatic with
                {
                    Candidates = RetainStrongestValueEvidence(partialStatic.Candidates),
                };
                partialStatic = ApplySemanticFingerprint(
                    partialStatic,
                    sourceSemanticFingerprint);
            }
            var partialDynamicMatches = FilterCandidates(partialDynamic
                .Where(candidate => candidate.Pattern.IsMatch(orderedExactText))
                .Select(candidate => candidate.Candidate)
                .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
                .ToArray(), baseType, hasGeneratedOptionEvidence);
            partialDynamicMatches = partialDynamicMatches with
            {
                Candidates = RetainStrongestValueEvidence(
                    hasGeneratedOptionEvidence
                        ? RetainMostSpecificDynamicEvidence(partialDynamicMatches.Candidates)
                        : partialDynamicMatches.Candidates),
            };
            partialDynamicMatches = ApplySemanticFingerprint(
                partialDynamicMatches,
                sourceSemanticFingerprint);
            if (hasGeneratedOptionEvidence &&
                partialDynamicMatches.HadCandidatesBeforeSemanticFingerprint)
            {
                return Resolution(partialDynamicMatches, usedStrictEvidence: true, orderedExactText);
            }
            if (partialStatic.HadCandidatesBeforeSemanticFingerprint)
            {
                return Resolution(partialStatic, usedStrictEvidence: true, orderedExactText);
            }
            return Resolution(partialDynamicMatches, usedStrictEvidence: true, orderedExactText);
        }

        private static MechanicalResolution Resolution(
            CandidateFilterResult filtered,
            bool usedStrictEvidence,
            string? orderedSourceText = null)
        {
            var reasons = new List<string>();
            if (filtered.ExcludedByPropertyCapability)
            {
                reasons.Add("base-item-property-capability");
            }
            if (filtered.UsedSemanticFingerprint)
            {
                reasons.Add("source-semantic-fingerprint");
            }
            if (filtered.Candidates.Any(candidate => candidate.ProvenanceTranslations.Any(
                    evidence => evidence.DefaultedStatIds.Count > 0)))
            {
                reasons.Add("implicit-zero-stat-composition");
            }
            if (filtered.Candidates.Any(candidate =>
                    candidate.OrderedRenderingText?.Contains('\n') == true &&
                    orderedSourceText?.Contains('\n') == true &&
                    !string.Equals(
                        candidate.OrderedRenderingText,
                        orderedSourceText,
                        StringComparison.Ordinal)))
            {
                reasons.Add("order-independent-complete-multiline");
            }
            return new MechanicalResolution(
                filtered.Candidates,
                usedStrictEvidence,
                reasons,
                filtered.RejectedBySemanticFingerprint);
        }

        private static CandidateFilterResult ApplySemanticFingerprint(
            CandidateFilterResult filtered,
            UniqueModifierSemanticFingerprint sourceFingerprint)
        {
            var hadCandidates = filtered.Candidates.Count > 0;
            if (!hadCandidates ||
                sourceFingerprint.Locality == UniqueModifierSemanticLocality.Unknown)
            {
                return filtered with
                {
                    HadCandidatesBeforeSemanticFingerprint = hadCandidates,
                };
            }

            var candidateLocalities = filtered.Candidates
                .Select(candidate => candidate.CandidateSemanticFingerprint.Locality)
                .ToArray();
            if (candidateLocalities.Any(locality =>
                    locality == UniqueModifierSemanticLocality.Unknown) ||
                candidateLocalities.Distinct().Count() < 2)
            {
                // PoB slot-context locality and RePoE stat locality are only directly
                // comparable when the candidate set itself proves a local/global axis.
                // A uniform candidate set supplies no such discriminator and retains
                // the existing exact/ambiguous fail-closed outcome.
                return filtered with
                {
                    HadCandidatesBeforeSemanticFingerprint = true,
                };
            }

            var compatible = filtered.Candidates
                .Where(candidate => candidate.CandidateSemanticFingerprint.Locality ==
                    sourceFingerprint.Locality)
                .ToArray();
            return filtered with
            {
                Candidates = compatible,
                HadCandidatesBeforeSemanticFingerprint = true,
                UsedSemanticFingerprint = true,
                RejectedBySemanticFingerprint = compatible.Length == 0,
            };
        }

        private CandidateFilterResult FilterCandidates(
            IReadOnlyList<MechanicalCandidate> candidates,
            string baseType,
            bool hasGeneratedOptionEvidence)
        {
            var domainCompatible = candidates
                .Where(candidate => IsDomainCompatible(
                    candidate,
                    baseType,
                    hasGeneratedOptionEvidence))
                .ToArray();
            var propertyCompatible = domainCompatible
                .Where(candidate => IsPropertyCapabilityCompatible(candidate, baseType))
                .ToArray();
            var hasPropertyCompatibleCandidate = propertyCompatible.Length > 0;
            return new CandidateFilterResult(
                hasPropertyCompatibleCandidate ? propertyCompatible : domainCompatible,
                ExcludedByPropertyCapability: hasPropertyCompatibleCandidate &&
                    propertyCompatible.Length < domainCompatible.Length);
        }

        private static MechanicalCandidate[] RetainStrongestValueEvidence(
            IReadOnlyList<MechanicalCandidate> candidates)
        {
            if (candidates.Count == 0)
            {
                return [];
            }

            var strongest = candidates.Max(candidate => candidate.StrictValueEvidenceCount);
            return candidates
                .Where(candidate => candidate.StrictValueEvidenceCount == strongest)
                .ToArray();
        }

        private static MechanicalCandidate[] RetainMostSpecificDynamicEvidence(
            IReadOnlyList<MechanicalCandidate> candidates)
        {
            if (candidates.Count == 0)
            {
                return [];
            }

            var strongest = candidates.Max(candidate => candidate.StrictPatternSpecificity);
            return candidates
                .Where(candidate => candidate.StrictPatternSpecificity == strongest)
                .ToArray();
        }

        private bool IsDomainCompatible(
            MechanicalCandidate candidate,
            string baseType,
            bool hasGeneratedOptionEvidence)
        {
            if (hasGeneratedOptionEvidence && string.Equals(
                    candidate.Domain?.Trim(),
                    "item",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!baseDomains.TryGetValue(baseType.Trim(), out var domains))
            {
                return true;
            }
            return !string.IsNullOrWhiteSpace(candidate.Domain) &&
                domains.Contains(candidate.Domain.Trim());
        }

        private bool IsPropertyCapabilityCompatible(
            MechanicalCandidate candidate,
            string baseType)
        {
            if (!baseCapabilities.TryGetValue(baseType.Trim(), out var capability) ||
                !propertySemantics.TryGetValue(VectorKey(candidate.StatIds), out var descriptor) ||
                descriptor.Applicability != ItemPropertyApplicability.UnconditionalDisplayedLocal)
            {
                return true;
            }

            var targets = descriptor.Contributions
                .SelectMany(contribution => contribution.Targets)
                .Distinct()
                .ToArray();
            if (targets.Length == 0)
            {
                return true;
            }

            ItemPropertyTarget[] weaponTargets =
            [
                ItemPropertyTarget.PhysicalDamage,
                ItemPropertyTarget.FireDamage,
                ItemPropertyTarget.ColdDamage,
                ItemPropertyTarget.LightningDamage,
                ItemPropertyTarget.ChaosDamage,
                ItemPropertyTarget.AttacksPerSecond,
                ItemPropertyTarget.CriticalStrikeChance,
            ];
            if (targets.All(weaponTargets.Contains))
            {
                return capability.HasWeaponProperties;
            }

            ItemPropertyTarget[] defenceTargets =
            [
                ItemPropertyTarget.Armour,
                ItemPropertyTarget.Evasion,
                ItemPropertyTarget.EnergyShield,
                ItemPropertyTarget.Ward,
            ];
            return !targets.All(defenceTargets.Contains) || capability.HasDefenceProperties;
        }
    }

    private sealed record CandidateFilterResult(
        IReadOnlyList<MechanicalCandidate> Candidates,
        bool ExcludedByPropertyCapability,
        bool HadCandidatesBeforeSemanticFingerprint = false,
        bool UsedSemanticFingerprint = false,
        bool RejectedBySemanticFingerprint = false)
    {
        public static CandidateFilterResult Empty { get; } = new([], false);
    }

    private sealed record BaseMechanicalCapability(
        bool HasWeaponProperties,
        bool HasDefenceProperties);

    private sealed class BaseIdentityIndex
    {
        private readonly IReadOnlyDictionary<string, ItemBaseRecord[]> byExactName;
        private readonly IReadOnlyDictionary<string, ItemBaseRecord[]> byCanonicalKey;

        public BaseIdentityIndex(IReadOnlyList<ItemBaseRecord> baseItems)
        {
            var usable = baseItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
                .ToArray();
            byExactName = usable
                .GroupBy(item => item.Name!.Trim(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            byCanonicalKey = usable
                .GroupBy(item => UniqueSourceIdentityNormalizer.NormalizeKey(item.Name!), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        }

        public SourceBaseType Resolve(
            SourceBaseType source,
            string sourcePath,
            List<ImportDiagnostic> diagnostics)
        {
            if (byExactName.TryGetValue(source.SourceText, out var exact))
            {
                return Resolved(source, source.SourceText, exact, UniqueSourceIdentityNormalizer.ExactRule);
            }

            if (!byCanonicalKey.TryGetValue(source.CanonicalKey, out var normalized))
            {
                return source;
            }

            var canonicalNames = normalized
                .Select(item => item.Name!.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (canonicalNames.Length != 1)
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.PoBUniqueBaseNormalizationCollision,
                    ImportDiagnosticSeverity.Error,
                    sourcePath,
                    $"PoB base '{source.SourceText}' maps to colliding current RePoE base names under canonical key '{source.CanonicalKey}': {string.Join(", ", canonicalNames)}."));
                return source;
            }

            return Resolved(
                source,
                canonicalNames[0],
                normalized.Where(item => string.Equals(item.Name?.Trim(), canonicalNames[0], StringComparison.Ordinal)),
                UniqueSourceIdentityNormalizer.CanonicalRule);
        }

        private static SourceBaseType Resolved(
            SourceBaseType source,
            string canonicalName,
            IEnumerable<ItemBaseRecord> records,
            string rule) => source with
            {
                Text = canonicalName,
                CanonicalKey = UniqueSourceIdentityNormalizer.NormalizeKey(canonicalName),
                NormalizationRule = rule,
                RePoeBaseItemIds = records
                    .Select(item => item.Id!.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
            };
    }

    private sealed record ParsedSourceItem(
        string Name,
        IReadOnlyList<SourceBaseType> BaseTypes,
        IReadOnlyList<SourceVariant> Variants,
        IReadOnlyList<SourceEffectLine> EffectLines,
        IReadOnlyList<int> SelectedVariantIndices,
        int AlternateVariantSlotCount,
        int ImplicitCount,
        string? ObservationId,
        UniqueItemKind Kind,
        bool IsGenerated = false);
}
