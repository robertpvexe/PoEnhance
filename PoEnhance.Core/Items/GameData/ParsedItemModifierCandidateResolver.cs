using System.Collections.ObjectModel;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed class ParsedItemModifierCandidateResolver
{
    public IReadOnlyList<ModifierCandidateResolutionResult> Resolve(
        ParsedItem parsedItem,
        GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);
        ArgumentNullException.ThrowIfNull(catalog);

        var results = new List<ModifierCandidateResolutionResult>();
        for (var index = 0; index < parsedItem.Modifiers.Count; index++)
        {
            var modifier = parsedItem.Modifiers[index];
            if (!HasCandidateDiscoverySignal(modifier))
            {
                continue;
            }

            results.Add(ResolveModifier(index, modifier, catalog));
        }

        return ToReadOnly(results);
    }

    private static ModifierCandidateResolutionResult ResolveModifier(
        int index,
        ParsedModifier modifier,
        GameDataCatalog catalog)
    {
        if (!TryMapGenerationType(modifier.Kind, out var generationType))
        {
            return Unknown(
                index,
                modifier,
                generationType: null,
                candidates: [],
                ModifierCandidateResolutionDiagnosticCodes.ModifierKindUnsupported,
                "The parsed modifier kind is not supported by first-stage candidate discovery.");
        }

        if (string.IsNullOrWhiteSpace(modifier.Name))
        {
            return Unknown(
                index,
                modifier,
                generationType,
                candidates: [],
                ModifierCandidateResolutionDiagnosticCodes.ModifierNameNotAvailable,
                "The parsed modifier does not expose an authentic Advanced Item Description modifier name.");
        }

        var candidates = catalog.FindModifiersByNameAndGenerationType(modifier.Name, generationType);
        return candidates.Count switch
        {
            0 => Unknown(
                index,
                modifier,
                generationType,
                candidates,
                ModifierCandidateResolutionDiagnosticCodes.ModifierNotFound,
                "No catalog modifier matched the parsed modifier name and generation type."),
            1 => Matched(
                index,
                modifier,
                generationType,
                candidates[0]),
            _ => Unknown(
                index,
                modifier,
                generationType,
                candidates,
                ModifierCandidateResolutionDiagnosticCodes.ModifierAmbiguous,
                "Multiple catalog modifiers matched the parsed modifier name and generation type."),
        };
    }

    private static bool HasCandidateDiscoverySignal(ParsedModifier modifier)
    {
        return modifier.RawMetadataLine is not null
            || modifier.IsCrafted
            || modifier.IsFractured
            || modifier.IsVeiled;
    }

    private static bool TryMapGenerationType(
        ParsedModifierKind kind,
        out ModifierGenerationType generationType)
    {
        generationType = kind switch
        {
            ParsedModifierKind.Prefix => ModifierGenerationType.Prefix,
            ParsedModifierKind.Suffix => ModifierGenerationType.Suffix,
            ParsedModifierKind.Implicit => ModifierGenerationType.Implicit,
            _ => ModifierGenerationType.Unknown,
        };

        return generationType != ModifierGenerationType.Unknown;
    }

    private static ModifierCandidateResolutionResult Matched(
        int index,
        ParsedModifier modifier,
        ModifierGenerationType generationType,
        ModifierDefinition candidate)
    {
        return new ModifierCandidateResolutionResult(
            index,
            modifier,
            modifier.Name,
            modifier.Kind,
            generationType,
            ModifierCandidateResolutionStatus.Exact,
            ToReadOnly([candidate]),
            Diagnostics(
                ModifierCandidateResolutionDiagnosticCodes.ModifierExactMatch,
                "The parsed modifier name and generation type matched one catalog modifier."));
    }

    private static ModifierCandidateResolutionResult Unknown(
        int index,
        ParsedModifier modifier,
        ModifierGenerationType? generationType,
        IReadOnlyList<ModifierDefinition> candidates,
        string diagnosticCode,
        string reason)
    {
        return new ModifierCandidateResolutionResult(
            index,
            modifier,
            modifier.Name,
            modifier.Kind,
            generationType,
            ModifierCandidateResolutionStatus.Unknown,
            ToReadOnly(candidates),
            Diagnostics(diagnosticCode, reason));
    }

    private static IReadOnlyList<ModifierCandidateResolutionDiagnostic> Diagnostics(string code, string reason)
    {
        return ToReadOnly([new ModifierCandidateResolutionDiagnostic(code, reason)]);
    }

    private static IReadOnlyList<T> ToReadOnly<T>(IEnumerable<T> values)
    {
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}
