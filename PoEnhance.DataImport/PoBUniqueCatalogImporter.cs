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
        IReadOnlyList<ItemPropertySemanticDescriptor>? itemPropertySemantics = null)
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
                itemPropertySemantics ?? []);
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
        IReadOnlyList<ItemPropertySemanticDescriptor> itemPropertySemantics)
    {
        var diagnostics = new List<ImportDiagnostic>();
        var observations = new List<UniqueCatalogSourceObservation>();
        var parsed = new List<ParsedSourceItem>();
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

            var kind = ClassifyKind(sourceItem.Name);
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
            });
            parsed.Add(sourceItem with
            {
                ObservationId = observationId,
                Kind = kind,
                IsGenerated = generated,
            });
        }

        var mechanicalIndex = BuildMechanicalIndex(
            modifiers,
            translations,
            baseItems,
            itemPropertySemantics);
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
            Kind = group.Key.Kind,
            BaseTypeEvidence = group.SelectMany(item => item.BaseTypes)
                .Select(baseType => baseType.Text)
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
        var classifiedVariants = item.Variants
            .Where(variant => ClassifyVersionRole(variant.Label) != UniqueItemVersionRole.Unknown)
            .ToArray();
        var hasCurrentVariant = classifiedVariants.Any(variant =>
            ClassifyVersionRole(variant.Label) == UniqueItemVersionRole.Current);
        var primaryVariants = hasCurrentVariant
            ? classifiedVariants
            : item.IsGenerated
                ? []
                : item.Variants.ToArray();
        var primaryIndices = primaryVariants.Select(variant => variant.Index).ToHashSet();
        var optionIndices = item.Variants
            .Where(variant => !primaryIndices.Contains(variant.Index) &&
                (!item.IsGenerated ||
                    ClassifyVersionRole(variant.Label) != UniqueItemVersionRole.Historical))
            .Select(variant => variant.Index)
            .ToHashSet();
        var baseVariantIndices = item.BaseTypes.SelectMany(baseType => baseType.Variants).ToHashSet();
        var specs = BuildVersionSpecs(item, primaryVariants);

        foreach (var spec in specs)
        {
            var implicitLines = item.EffectLines.Take(item.ImplicitCount)
                .Where(line => IsApplicable(line, spec, optionIndices, baseVariantIndices))
                .Select(line => new SelectedEffectLine(
                    line.Text,
                    item.IsGenerated && line.Variants.Any(index =>
                        optionIndices.Contains(index) && !baseVariantIndices.Contains(index))))
                .ToArray();
            var uniqueLines = item.EffectLines.Skip(item.ImplicitCount)
                .Where(line => IsApplicable(line, spec, optionIndices, baseVariantIndices))
                .Select(line => new SelectedEffectLine(
                    line.Text,
                    item.IsGenerated && line.Variants.Any(index =>
                        optionIndices.Contains(index) && !baseVariantIndices.Contains(index))))
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
            yield return new UniqueItemVersionObservation
            {
                Id = StableId("unique-version", identityId, spec.Label, spec.BaseType,
                    string.Join('\u001f', blocks.Select(block => block.Id))),
                Label = spec.Label,
                Role = spec.Role,
                BaseType = spec.BaseType,
                ModifierBlocks = blocks,
                SourceObservationIds = [item.ObservationId!],
            };
        }
    }

    private static IReadOnlyList<VersionSpec> BuildVersionSpecs(
        ParsedSourceItem item,
        IReadOnlyList<SourceVariant> primaryVariants)
    {
        if (primaryVariants.Count > 0)
        {
            return primaryVariants.Select(variant => new VersionSpec(
                    variant.Label,
                    ClassifyVersionRole(variant.Label) is { } role &&
                        role != UniqueItemVersionRole.Unknown
                            ? role
                            : UniqueItemVersionRole.Current,
                    variant.Index,
                    SelectBaseType(item.BaseTypes, variant.Index)))
                .ToArray();
        }

        var variantBases = item.BaseTypes
            .SelectMany(baseType => baseType.Variants.Select(variantIndex => new
            {
                BaseType = baseType.Text,
                VariantIndex = variantIndex,
            }))
            .ToArray();
        if (variantBases.Length == 0)
        {
            return
            [
                new VersionSpec(
                    "Observed",
                    UniqueItemVersionRole.Current,
                    VariantIndex: null,
                    item.BaseTypes[0].Text),
            ];
        }

        return variantBases.Select(baseVariant => new VersionSpec(
                $"Observed: {baseVariant.BaseType}",
                UniqueItemVersionRole.Current,
                baseVariant.VariantIndex,
                baseVariant.BaseType))
            .ToArray();
    }

    private static string SelectBaseType(
        IReadOnlyList<SourceBaseType> baseTypes,
        int variantIndex)
    {
        return baseTypes.FirstOrDefault(baseType => baseType.Variants.Contains(variantIndex))?.Text ??
            baseTypes.FirstOrDefault(baseType => baseType.Variants.Count == 0)?.Text ??
            baseTypes[0].Text;
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
        bool isGenerated,
        MechanicalIndex mechanicalIndex)
    {
        for (var index = 0; index < lines.Count;)
        {
            var maximumLength = lines.Count - index;
            var selectedLength = 1;
            for (var length = maximumLength; length > 1; length--)
            {
                if (mechanicalIndex.HasMatch(
                        lines.Skip(index).Take(length).Select(line => line.Text).ToArray(),
                        baseType,
                        lines.Skip(index).Take(length).Any(line => line.HasGeneratedOptionEvidence)))
                {
                    selectedLength = length;
                    break;
                }
            }

            yield return BuildBlock(
                identityId,
                versionLabel,
                lines.Skip(index).Take(selectedLength).Select(line => line.Text).ToArray(),
                kind,
                observationId,
                isGenerated,
                baseType,
                lines.Skip(index).Take(selectedLength).Any(line => line.HasGeneratedOptionEvidence),
                mechanicalIndex);
            index += selectedLength;
        }
    }

    private static UniqueModifierBlock BuildBlock(
        string identityId,
        string versionLabel,
        IReadOnlyList<string> lines,
        UniqueModifierBlockKind kind,
        string observationId,
        bool isGenerated,
        string baseType,
        bool hasGeneratedOptionEvidence,
        MechanicalIndex mechanicalIndex)
    {
        var signatures = lines.Select(NormalizeSignature).ToArray();
        var signature = string.Join("\n", signatures);
        var resolution = mechanicalIndex.Resolve(
            lines,
            baseType,
            hasGeneratedOptionEvidence);
        var candidates = resolution.Candidates;
        var statVectors = candidates
            .Select(candidate => string.Join('\u001f', candidate.StatIds))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var status = candidates.Count switch
        {
            0 => UniqueModifierMechanicalMappingStatus.Unsupported,
            1 => UniqueModifierMechanicalMappingStatus.Exact,
            _ when statVectors.Length == 1 => UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
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
        return new UniqueModifierBlock
        {
            Id = StableId("unique-block", identityId, versionLabel, kind.ToString(), signature),
            Kind = kind,
            Lines = lines,
            CanonicalSignatures = signatures,
            MechanicalMapping = new UniqueModifierMechanicalMapping
            {
                Status = status,
                ModifierIds = candidates.Select(candidate => candidate.ModifierId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                StatIds = statVectors.Length == 1 ? candidates[0].StatIds : [],
                Provenance = resolved && resolution.ResolutionReasons.Count > 0
                    ? new UniqueModifierMechanicalProvenance
                    {
                        ResolutionReasons = resolution.ResolutionReasons,
                        Translations = translationEvidence,
                        UsedComposition = translationEvidence.Length > 1 ||
                            translationEvidence.Any(evidence => evidence.DefaultedStatIds.Count > 0),
                        CatalogValuesUsedForSelection = resolution.UsedStrictEvidence,
                        ValueAuthority = "copiedInstance",
                        SafetyRationale = "Pinned modifier, translation-condition, and base-property evidence leaves one mechanical stat vector; copied instance values remain authoritative.",
                    }
                    : null,
                DiagnosticCode = status switch
                {
                    UniqueModifierMechanicalMappingStatus.Unsupported when isGenerated =>
                        "UNIQUE_GENERATED_MECHANICS_NOT_FOUND",
                    UniqueModifierMechanicalMappingStatus.Unsupported => "UNIQUE_MECHANICS_NOT_FOUND",
                    UniqueModifierMechanicalMappingStatus.Ambiguous when resolution.UsedStrictEvidence =>
                        "UNIQUE_MECHANICS_EXACT_CONFLICT",
                    UniqueModifierMechanicalMappingStatus.Ambiguous => "UNIQUE_MECHANICS_CONFLICT",
                    _ => null,
                },
                Diagnostic = status switch
                {
                    UniqueModifierMechanicalMappingStatus.Unsupported when isGenerated =>
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
        IReadOnlyList<ItemPropertySemanticDescriptor> itemPropertySemantics)
    {
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
                        modifier.Domain));
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
                modifier.Domain);
            foreach (var rendering in BuildStrictRenderings(
                         modifier,
                         translationsByFirstStat,
                         allowMissingStats: false))
            {
                var evidencedCandidate = strictCandidate with
                {
                    StrictValueEvidenceCount = rendering.ValueEvidenceCount,
                    TranslationEvidence = rendering.TranslationEvidence,
                    OrderedRenderingText = rendering.ExactText,
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
                    TranslationEvidence = rendering.TranslationEvidence,
                    OrderedRenderingText = rendering.ExactText,
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
            .OrderBy(group => group.StatIndices[0])
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
            foreach (var group in selection.OrderBy(group => group.StatIndices[0]))
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
        var searchStart = 0;
        foreach (var rawStatId in translation.StatIds)
        {
            var statId = rawStatId?.Trim();
            var found = -1;
            for (var index = searchStart; index < modifierStats.Count; index++)
            {
                if (string.Equals(
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
            searchStart = found + 1;
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
        if (handlers.Count == 1 && handlers[0].Trim().StartsWith(
                "display_indexable_",
                StringComparison.OrdinalIgnoreCase))
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
            new(firstBaseType, firstBaseVariants),
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
            baseTypes.Add(new SourceBaseType(baseType, baseVariants));
            contentStart++;
        }

        var variants = new List<SourceVariant>();
        var effects = new List<SourceEffectLine>();
        var implicitCount = 0;
        for (var index = contentStart; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.StartsWith("Variant:", StringComparison.Ordinal))
            {
                variants.Add(new SourceVariant(variants.Count + 1, line["Variant:".Length..].Trim()));
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
                effects.Add(new SourceEffectLine(text, selectedVariants));
            }
        }

        item = new ParsedSourceItem(
            name,
            baseTypes,
            variants,
            effects,
            implicitCount,
            null,
            UniqueItemKind.Unknown);
        return true;
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

    private static UniqueItemVersionRole ClassifyVersionRole(string label) =>
        label.Equals("Current", StringComparison.OrdinalIgnoreCase) ||
        label.StartsWith("Current ", StringComparison.OrdinalIgnoreCase) ||
        label.EndsWith(" Current", StringComparison.OrdinalIgnoreCase)
            ? UniqueItemVersionRole.Current
            : label.Contains("Pre ", StringComparison.OrdinalIgnoreCase) ||
              label.Contains("Legacy", StringComparison.OrdinalIgnoreCase)
                ? UniqueItemVersionRole.Historical
                : UniqueItemVersionRole.Unknown;

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

    [GeneratedRegex(@"\{\d+\}", RegexOptions.CultureInvariant)]
    private static partial Regex UnresolvedPlaceholderPattern();

    private sealed record IdentityKey(string Name, UniqueItemKind Kind);
    private sealed record SourceBaseType(string Text, IReadOnlyList<int> Variants);
    private sealed record SourceVariant(int Index, string Label);
    private sealed record SourceEffectLine(string Text, IReadOnlyList<int> Variants);
    private sealed record SelectedEffectLine(string Text, bool HasGeneratedOptionEvidence);
    private sealed record VersionSpec(
        string Label,
        UniqueItemVersionRole Role,
        int? VariantIndex,
        string BaseType);
    private sealed record MechanicalCandidate(
        string ModifierId,
        IReadOnlyList<string> StatIds,
        string? Domain,
        int StrictValueEvidenceCount = 0,
        IReadOnlyList<UniqueModifierTranslationEvidence>? TranslationEvidence = null,
        string? OrderedRenderingText = null)
    {
        public IReadOnlyList<UniqueModifierTranslationEvidence> ProvenanceTranslations =>
            TranslationEvidence ?? [];
    }
    private sealed record MechanicalResolution(
        IReadOnlyList<MechanicalCandidate> Candidates,
        bool UsedStrictEvidence,
        IReadOnlyList<string> ResolutionReasons);
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
            bool hasGeneratedOptionEvidence) =>
            Resolve(lines, baseType, hasGeneratedOptionEvidence)
                .Candidates.Count > 0;

        public MechanicalResolution Resolve(
            IReadOnlyList<string> lines,
            string baseType,
            bool hasGeneratedOptionEvidence)
        {
            var orderedExactText = string.Join("\n", lines.Select(NormalizeExactEvidence));
            var exactText = UnorderedMultilineKey(orderedExactText);
            var staticStrict = CandidateFilterResult.Empty;
            if (exact.TryGetValue(exactText, out var staticMatches))
            {
                staticStrict = FilterCandidates(staticMatches
                    .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
                    .ToArray(), baseType);
                staticStrict = staticStrict with
                {
                    Candidates = RetainStrongestValueEvidence(staticStrict.Candidates),
                };
            }
            var dynamicStrict = FilterCandidates(dynamic
                .Where(candidate => candidate.Pattern.IsMatch(orderedExactText))
                .Select(candidate => candidate.Candidate)
                .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
                .ToArray(), baseType);
            dynamicStrict = dynamicStrict with
            {
                Candidates = RetainStrongestValueEvidence(dynamicStrict.Candidates),
            };
            if (hasGeneratedOptionEvidence && dynamicStrict.Candidates.Count > 0)
            {
                return Resolution(dynamicStrict, usedStrictEvidence: true, orderedExactText);
            }
            if (staticStrict.Candidates.Count > 0)
            {
                return Resolution(staticStrict, usedStrictEvidence: true, orderedExactText);
            }
            if (dynamicStrict.Candidates.Count > 0)
            {
                return Resolution(dynamicStrict, usedStrictEvidence: true, orderedExactText);
            }

            var signature = string.Join("\n", lines.Select(NormalizeSignature));
            var broadCandidates = broad.GetValueOrDefault(signature) ?? [];
            if (broadCandidates.Count > 0)
            {
                return Resolution(
                    new CandidateFilterResult(
                        broadCandidates,
                        ExcludedByPropertyCapability: false),
                    usedStrictEvidence: false);
            }

            var partialStatic = CandidateFilterResult.Empty;
            if (partialExact.TryGetValue(exactText, out var partialStaticMatches))
            {
                partialStatic = FilterCandidates(partialStaticMatches
                    .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
                    .ToArray(), baseType);
                partialStatic = partialStatic with
                {
                    Candidates = RetainStrongestValueEvidence(partialStatic.Candidates),
                };
            }
            var partialDynamicMatches = FilterCandidates(partialDynamic
                .Where(candidate => candidate.Pattern.IsMatch(orderedExactText))
                .Select(candidate => candidate.Candidate)
                .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
                .ToArray(), baseType);
            partialDynamicMatches = partialDynamicMatches with
            {
                Candidates = RetainStrongestValueEvidence(partialDynamicMatches.Candidates),
            };
            if (hasGeneratedOptionEvidence && partialDynamicMatches.Candidates.Count > 0)
            {
                return Resolution(partialDynamicMatches, usedStrictEvidence: true, orderedExactText);
            }
            if (partialStatic.Candidates.Count > 0)
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
            return new MechanicalResolution(filtered.Candidates, usedStrictEvidence, reasons);
        }

        private CandidateFilterResult FilterCandidates(
            IReadOnlyList<MechanicalCandidate> candidates,
            string baseType)
        {
            var domainCompatible = candidates
                .Where(candidate => IsDomainCompatible(candidate, baseType))
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

        private bool IsDomainCompatible(MechanicalCandidate candidate, string baseType)
        {
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
        bool ExcludedByPropertyCapability)
    {
        public static CandidateFilterResult Empty { get; } = new([], false);
    }

    private sealed record BaseMechanicalCapability(
        bool HasWeaponProperties,
        bool HasDefenceProperties);
    private sealed record ParsedSourceItem(
        string Name,
        IReadOnlyList<SourceBaseType> BaseTypes,
        IReadOnlyList<SourceVariant> Variants,
        IReadOnlyList<SourceEffectLine> EffectLines,
        int ImplicitCount,
        string? ObservationId,
        UniqueItemKind Kind,
        bool IsGenerated = false);
}
