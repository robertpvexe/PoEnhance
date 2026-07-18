using System.Globalization;
using System.Text.RegularExpressions;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

internal static partial class CanonicalModifierEffectAggregator
{
    public static CanonicalModifierEffectAggregationResult Aggregate(
        IReadOnlyList<ResolvedSearchComponent> sourceComponents)
    {
        ArgumentNullException.ThrowIfNull(sourceComponents);

        var hybridSourceIndexes = sourceComponents
            .Where(component => component.SourceModifierIndex >= 0)
            .GroupBy(component => component.SourceModifierIndex)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        var components = sourceComponents
            .Select(component => EnsureSourceProvenance(
                component,
                hybridSourceIndexes.Contains(component.SourceModifierIndex)))
            .ToArray();
        var diagnostics = CreateSkippedAggregationDiagnostics(components);
        var keyed = components
            .Select((component, index) => new IndexedComponent(
                index,
                component,
                TryCreateKey(component, out var key) ? key : null))
            .ToArray();
        var groupsByFirstIndex = keyed
            .Where(candidate => candidate.Key is not null)
            .GroupBy(candidate => candidate.Key!)
            .Where(group => group.Count() > 1)
            .ToDictionary(
                group => group.Min(candidate => candidate.Index),
                group => group.OrderBy(candidate => candidate.Index).ToArray());
        var aggregatedIndexes = groupsByFirstIndex.Values
            .SelectMany(group => group.Skip(1))
            .Select(candidate => candidate.Index)
            .ToHashSet();
        var results = new List<ResolvedSearchComponent>(components.Length);

        for (var index = 0; index < components.Length; index++)
        {
            if (groupsByFirstIndex.TryGetValue(index, out var group))
            {
                results.Add(CreateAggregate(group.Select(candidate => candidate.Component).ToArray()));
                continue;
            }

            if (!aggregatedIndexes.Contains(index))
            {
                results.Add(components[index]);
            }
        }

        return new CanonicalModifierEffectAggregationResult(results, diagnostics);
    }

    internal static string ProviderDomainFor(ResolvedSearchComponent component)
    {
        if (component.IsFractured)
        {
            return "Fractured";
        }

        if (component.IsCrafted)
        {
            return "Crafted";
        }

        if (component.IsVeiled)
        {
            return "Veiled";
        }

        if (component.GenerationType == ModifierGenerationType.Enchantment)
        {
            return "Enchant";
        }

        if (component.IsBaseImplicit ||
            component.ParsedKind == ParsedModifierKind.Implicit ||
            component.GenerationType == ModifierGenerationType.Implicit)
        {
            return "Implicit";
        }

        return component.ParsedKind is ParsedModifierKind.Prefix or ParsedModifierKind.Suffix ||
            component.GenerationType is ModifierGenerationType.Prefix or ModifierGenerationType.Suffix
                ? "Explicit"
                : component.ParsedKind == ParsedModifierKind.Unique
                    ? "Unique"
                    : "Unknown";
    }

    private static ResolvedSearchComponent EnsureSourceProvenance(
        ResolvedSearchComponent component,
        bool isHybrid)
    {
        var canonicalValues = CanonicalValues(component);
        if (component.Sources.Count > 0 && component.CanonicalNumericValues.Count > 0)
        {
            return isHybrid
                ? component with
                {
                    Sources = component.Sources
                        .Select(source => source with { IsHybrid = true })
                        .ToArray(),
                }
                : component;
        }

        return component with
        {
            CanonicalNumericValues = canonicalValues,
            Sources = component.Sources.Count > 0
                ? component.Sources
                : [CreateSource(component, canonicalValues, isHybrid)],
        };
    }

    private static SearchComponentSourceProvenance CreateSource(
        ResolvedSearchComponent component,
        IReadOnlyList<decimal> canonicalValues,
        bool isHybrid)
    {
        return new SearchComponentSourceProvenance
        {
            ComponentId = component.ComponentId,
            SourceModifierIndex = component.SourceModifierIndex,
            SourceLineIndex = component.SourceLineIndex,
            SourceComponentIndex = component.SourceComponentIndex,
            OriginalText = component.OriginalText,
            CanonicalSignature = component.CanonicalSignature,
            ParsedKind = component.ParsedKind,
            ImplicitOrigin = component.ImplicitOrigin,
            UniqueOrigin = component.UniqueOrigin,
            GenerationType = component.GenerationType,
            Locality = component.Locality,
            StatMappingProof = component.StatMappingProof,
            ReviewedItemPropertySemantic = component.ReviewedItemPropertySemantic,
            ParsedModifierName = component.ParsedModifierName,
            CategoryText = component.CategoryText,
            Tier = component.Tier,
            Rank = component.Rank,
            ProviderDomain = ProviderDomainFor(component),
            IsCrafted = component.IsCrafted,
            IsFractured = component.IsFractured,
            IsVeiled = component.IsVeiled,
            IsBaseImplicit = component.IsBaseImplicit,
            IsHybrid = isHybrid,
            ResolvedModifierId = component.ResolvedModifierId,
            ResolvedModifierName = component.ResolvedModifierName,
            ResolvedStatIds = component.ResolvedStatIds.ToArray(),
            ObservedNumericValues = component.ObservedNumericValues.ToArray(),
            CanonicalNumericValues = canonicalValues.ToArray(),
            ValueBoundShape = component.ValueBoundShape,
            DefaultBoundDirection = component.DefaultBoundDirection,
            TranslationHandlers = component.ValueBoundTranslationHandlers
                .Select(handlers => (IReadOnlyList<string>)handlers.ToArray())
                .ToArray(),
            TranslationIdentity = component.ValueBoundTranslationIdentity,
            ProviderIdentity = component.SelectedFilterVariantIdentity,
            ProviderResolutionStatus = component.ProviderResolutionStatus,
        };
    }

    private static IReadOnlyList<decimal> CanonicalValues(ResolvedSearchComponent component)
    {
        if (component.CanonicalNumericValues.Count > 0)
        {
            return component.CanonicalNumericValues;
        }

        if (component.ValueBoundShape == ModifierBoundShape.Scalar)
        {
            var scalar = component.DefaultBoundDirection == ModifierBoundDirection.Minimum
                ? component.RequestedMinimum
                : component.RequestedMaximum;
            return scalar.HasValue ? [scalar.Value] : [];
        }

        return component.ValueBoundShape == ModifierBoundShape.ArithmeticMeanRange
            ? component.ObservedNumericValues
            : [];
    }

    private static bool TryCreateKey(
        ResolvedSearchComponent component,
        out AggregationKey key)
    {
        key = default!;
        var canonicalValues = CanonicalValues(component);
        if (!component.IsSearchable ||
            component.ResolutionStatus != ModifierCandidateResolutionStatus.Exact ||
            component.ResolvedStatIds.Count == 0 ||
            string.IsNullOrWhiteSpace(component.CanonicalSignature) ||
            IsNonAdditiveStatVector(component.ResolvedStatIds) ||
            !HasSupportedAdditiveShape(component, canonicalValues))
        {
            return false;
        }

        key = new AggregationKey(
            StatVector(component),
            component.CanonicalSignature.Trim(),
            component.Locality,
            NumericUnitIdentity(component.CanonicalSignature),
            component.ValueBoundShape,
            canonicalValues.Count,
            TranslationTransformIdentity(component),
            component.DefaultBoundDirection,
            component.SupportsValueBounds ? "DirectScalar" : "DeferredRangeProjection",
            IsImplicit(component),
            component.ImplicitOrigin,
            ReviewedSemanticIdentity(component.ReviewedItemPropertySemantic));
        return true;
    }

    private static bool HasSupportedAdditiveShape(
        ResolvedSearchComponent component,
        IReadOnlyList<decimal> canonicalValues)
    {
        return component.ValueBoundShape switch
        {
            ModifierBoundShape.Scalar =>
                component.SupportsValueBounds &&
                canonicalValues.Count == 1 &&
                component.ObservedNumericValues.Count == 1,
            ModifierBoundShape.ArithmeticMeanRange =>
                canonicalValues.Count == 2 && component.ObservedNumericValues.Count == 2,
            _ => false,
        };
    }

    private static bool IsNonAdditiveStatVector(IReadOnlyList<string> statIds)
    {
        return statIds.Any(statId => StatIdTokenRegex().Matches(statId ?? string.Empty)
            .Select(match => match.Value)
            .Any(token => token.Equals("more", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("less", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("multiplicative", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("multiplier", StringComparison.OrdinalIgnoreCase)));
    }

    private static ResolvedSearchComponent CreateAggregate(
        IReadOnlyList<ResolvedSearchComponent> components)
    {
        var first = components[0];
        var canonicalValues = SumVectors(components.Select(CanonicalValues).ToArray());
        var observedValues = SumVectors(components
            .Select(component => component.ObservedNumericValues)
            .ToArray());
        var sources = components
            .SelectMany(component => component.Sources)
            .ToArray();
        var scalarValue = first.ValueBoundShape == ModifierBoundShape.Scalar
            ? canonicalValues[0]
            : (decimal?)null;

        return first with
        {
            ComponentId = $"aggregate:{first.ComponentId}:{sources.Length}",
            OriginalText = RenderAggregateText(first.CanonicalSignature, observedValues),
            ObservedNumericValues = observedValues,
            CanonicalNumericValues = canonicalValues,
            RequestedMinimum = first.DefaultBoundDirection == ModifierBoundDirection.Minimum
                ? scalarValue
                : null,
            RequestedMaximum = first.DefaultBoundDirection == ModifierBoundDirection.Maximum
                ? scalarValue
                : null,
            StatMappingProof = CommonStatMappingProof(components),
            Tier = null,
            Rank = null,
            FilterVariants = [],
            SelectedFilterVariantIdentity = null,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.NotResolved,
            ProviderStatId = null,
            ProviderStatText = null,
            ProviderCandidateStatIds = [],
            ProviderDiagnosticCode = null,
            ProviderDiagnosticMessage = null,
            Sources = sources,
            Contributors = sources
                .Select((source, index) => CreateContributor(source, index, first))
                .ToArray(),
            ContributorProjection = SearchComponentContributorProjection.Additive,
        };
    }

    private static ModifierStatMappingProofStatus CommonStatMappingProof(
        IReadOnlyList<ResolvedSearchComponent> components)
    {
        var first = components[0].StatMappingProof;
        return components.All(component => component.StatMappingProof == first)
            ? first
            : ModifierStatMappingProofStatus.Unknown;
    }

    private static SearchComponentContributor CreateContributor(
        SearchComponentSourceProvenance source,
        int index,
        ResolvedSearchComponent aggregateTemplate)
    {
        var scalar = source.CanonicalNumericValues.Count == 1
            ? source.CanonicalNumericValues[0]
            : (decimal?)null;
        return new SearchComponentContributor
        {
            ContributorId = $"{source.ComponentId}:{source.SourceModifierIndex}:{source.SourceComponentIndex}:{index}",
            Source = source,
            DisplayText = RenderAggregateText(source.CanonicalSignature, source.CanonicalNumericValues),
            RequestedMinimum = aggregateTemplate.DefaultBoundDirection == ModifierBoundDirection.Minimum
                ? scalar
                : null,
            RequestedMaximum = aggregateTemplate.DefaultBoundDirection == ModifierBoundDirection.Maximum
                ? scalar
                : null,
            SupportsValueBounds = aggregateTemplate.SupportsValueBounds && scalar.HasValue,
            ValueBoundsUnsupportedReason = aggregateTemplate.SupportsValueBounds && scalar.HasValue
                ? null
                : aggregateTemplate.ValueBoundsUnsupportedReason,
            ValueBoundShape = scalar.HasValue ? ModifierBoundShape.Scalar : ModifierBoundShape.Unsupported,
            DefaultBoundDirection = aggregateTemplate.DefaultBoundDirection,
        };
    }

    private static IReadOnlyList<decimal> SumVectors(
        IReadOnlyList<IReadOnlyList<decimal>> vectors)
    {
        var result = new decimal[vectors[0].Count];
        foreach (var vector in vectors)
        {
            for (var index = 0; index < result.Length; index++)
            {
                result[index] += vector[index];
            }
        }

        return result;
    }

    internal static string RenderAggregateText(
        string canonicalSignature,
        IReadOnlyList<decimal> observedValues)
    {
        var valueIndex = 0;
        return NumberPlaceholderRegex().Replace(canonicalSignature, match =>
        {
            if (valueIndex >= observedValues.Count)
            {
                return match.Value;
            }

            var value = observedValues[valueIndex++];
            var magnitude = decimal.Abs(value).ToString("G29", CultureInfo.InvariantCulture);
            var rendered = match.Groups["sign"].Value switch
            {
                "+" => value < 0m ? $"-{magnitude}" : $"+{magnitude}",
                "-" => value < 0m ? $"+{magnitude}" : $"-{magnitude}",
                _ => value.ToString("G29", CultureInfo.InvariantCulture),
            };
            return match.Groups["percent"].Value == "%" ? $"{rendered}%" : rendered;
        });
    }

    private static IReadOnlyList<TradeSearchDraftDiagnostic> CreateSkippedAggregationDiagnostics(
        IReadOnlyList<ResolvedSearchComponent> components)
    {
        var diagnostics = new List<TradeSearchDraftDiagnostic>();
        for (var leftIndex = 0; leftIndex < components.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < components.Count; rightIndex++)
            {
                var left = components[leftIndex];
                var right = components[rightIndex];
                if (!AreRelatedAggregationCandidates(left, right) ||
                    TryCreateKey(left, out var leftKey) &&
                    TryCreateKey(right, out var rightKey) &&
                    leftKey == rightKey)
                {
                    continue;
                }

                diagnostics.Add(new TradeSearchDraftDiagnostic(
                    TradeSearchDraftDiagnosticCodes.ModifierAggregationSkipped,
                    $"Canonical modifier aggregation kept '{left.ComponentId}' and '{right.ComponentId}' separate: {MismatchReason(left, right)}."));
            }
        }

        return diagnostics;
    }

    private static bool AreRelatedAggregationCandidates(
        ResolvedSearchComponent left,
        ResolvedSearchComponent right)
    {
        return string.Equals(
                left.CanonicalSignature?.Trim(),
                right.CanonicalSignature?.Trim(),
                StringComparison.Ordinal) ||
            string.Equals(StatVector(left), StatVector(right), StringComparison.Ordinal);
    }

    private static string MismatchReason(
        ResolvedSearchComponent left,
        ResolvedSearchComponent right)
    {
        if (IsImplicit(left) != IsImplicit(right))
        {
            return "different canonical source origin";
        }

        if (left.ImplicitOrigin != right.ImplicitOrigin)
        {
            return "different implicit source provenance";
        }

        if (left.Locality != right.Locality)
        {
            return "different locality";
        }

        if (!string.Equals(
                ReviewedSemanticIdentity(left.ReviewedItemPropertySemantic),
                ReviewedSemanticIdentity(right.ReviewedItemPropertySemantic),
                StringComparison.Ordinal))
        {
            return "different reviewed item-property semantics";
        }

        if (!string.Equals(StatVector(left), StatVector(right), StringComparison.Ordinal))
        {
            return "incompatible canonical stat vector";
        }

        if (!string.Equals(
                NumericUnitIdentity(left.CanonicalSignature),
                NumericUnitIdentity(right.CanonicalSignature),
                StringComparison.Ordinal))
        {
            return "incompatible numeric units";
        }

        if (IsNonAdditiveStatVector(left.ResolvedStatIds) ||
            IsNonAdditiveStatVector(right.ResolvedStatIds) ||
            !string.Equals(left.CanonicalSignature, right.CanonicalSignature, StringComparison.Ordinal))
        {
            return "non-additive logical semantics";
        }

        var leftValues = CanonicalValues(left);
        var rightValues = CanonicalValues(right);
        if (left.ValueBoundShape != right.ValueBoundShape || leftValues.Count != rightValues.Count)
        {
            return "unsupported or incompatible numeric shape";
        }

        if (!string.Equals(
                TranslationTransformIdentity(left),
                TranslationTransformIdentity(right),
                StringComparison.Ordinal))
        {
            return "incompatible translation transforms";
        }

        if (left.DefaultBoundDirection != right.DefaultBoundDirection ||
            left.SupportsValueBounds != right.SupportsValueBounds)
        {
            return "incompatible bound direction or projection semantics";
        }

        return "unsupported numeric shape or non-additive semantics";
    }

    private static string StatVector(ResolvedSearchComponent component) =>
        string.Join('\u001f', component.ResolvedStatIds.Select(statId => statId?.Trim() ?? string.Empty));

    private static string NumericUnitIdentity(string canonicalSignature)
    {
        return string.Join('|', NumberPlaceholderRegex().Matches(canonicalSignature ?? string.Empty)
            .Select(match => $"{match.Groups["sign"].Value}{(match.Groups["percent"].Value == "%" ? "%" : "flat")}"));
    }

    private static string TranslationTransformIdentity(ResolvedSearchComponent component)
    {
        return string.Join('\u001e', component.ValueBoundTranslationHandlers
            .Select(handlers => string.Join('\u001f', handlers.Select(handler => handler.Trim()))));
    }

    private static string ReviewedSemanticIdentity(ItemPropertySemanticDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return string.Empty;
        }

        var contributions = string.Join('\u001d', descriptor.Contributions.Select(contribution =>
            $"{(int)contribution.Operation}\u001c{string.Join('\u001b', contribution.Targets.Select(target => (int)target))}"));
        var evidence = string.Join('\u001a', descriptor.Evidence.Select(entry => string.Join('\u0019',
            (int)entry.Method,
            entry.SourceId,
            entry.ReviewVersion,
            entry.ReviewReference,
            entry.CompatibleSourceId,
            entry.CompatibleSourceVersion)));
        return string.Join('\u0018',
            descriptor.Id,
            string.Join('\u001f', descriptor.OrderedStatIds),
            contributions,
            (int)descriptor.Applicability,
            evidence);
    }

    private static bool IsImplicit(ResolvedSearchComponent component) =>
        component.IsBaseImplicit ||
        component.ParsedKind == ParsedModifierKind.Implicit ||
        component.GenerationType == ModifierGenerationType.Implicit;

    private sealed record AggregationKey(
        string CanonicalStatVector,
        string LogicalEffectIdentity,
        ModifierLocality Locality,
        string NumericUnit,
        ModifierBoundShape Shape,
        int NumericArity,
        string TranslationTransforms,
        ModifierBoundDirection BoundDirection,
        string ProjectionSemantics,
        bool IsImplicit,
        ParsedImplicitModifierOrigin ImplicitOrigin,
        string ReviewedSemanticIdentity);

    private sealed record IndexedComponent(
        int Index,
        ResolvedSearchComponent Component,
        AggregationKey? Key);

    [GeneratedRegex(@"(?<sign>[+-]?)<number>(?<percent>%?)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPlaceholderRegex();

    [GeneratedRegex(@"[A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex StatIdTokenRegex();
}

internal sealed record CanonicalModifierEffectAggregationResult(
    IReadOnlyList<ResolvedSearchComponent> Components,
    IReadOnlyList<TradeSearchDraftDiagnostic> Diagnostics);
