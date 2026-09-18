using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal static class ModifierPipelineReplayNormalizer
{
    public static string HashRawClipboard(string rawClipboardText)
    {
        ArgumentNullException.ThrowIfNull(rawClipboardText);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawClipboardText));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static ModifierPipelineNormalizedItem FromCapture(ModifierPipelineReplayCaptureDocument capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var item = capture.Item;
        var unique = capture.UniqueIdentity;
        var mech = capture.UniqueMechanicalResolution;
        var modifiers = (capture.Modifiers ?? [])
            .Select(modifier => FromCaptureModifier(modifier, mech))
            .OrderBy(modifier => modifier.SourceModifierIndex)
            .ThenBy(modifier => modifier.SourceLineIndex)
            .ThenBy(modifier => modifier.OriginalText, StringComparer.Ordinal)
            .ToArray();

        return new ModifierPipelineNormalizedItem
        {
            ItemClass = item?.ItemClass ?? "-",
            Rarity = item?.Rarity ?? "-",
            DisplayName = item?.DisplayName ?? "-",
            BaseType = item?.ParsedBaseType ?? "-",
            BaseResolutionStatus = item?.BaseResolutionStatus,
            ResolvedBaseName = item?.ResolvedBaseName,
            UniqueCanonicalName = unique?.CanonicalName ?? mech?.IdentityCanonicalName,
            UniqueCanonicalType = unique?.CanonicalType,
            UniqueResolutionStatus = mech?.Status,
            UniqueResolutionDiagnosticCode = mech?.DiagnosticCode,
            CompatibleVersionRoles = Sort(mech?.CompatibleVersionRoles),
            Modifiers = modifiers,
            DraftFilters = [],
            FinalSerializedRequestAvailability =
                "unavailable-in-A.5.3-capture; compare uses capture modifier/provider fields only",
        };
    }

    public static ModifierPipelineNormalizedItem FromReplay(
        ParsedItem parsed,
        ItemBaseResolutionResult baseResolution,
        TradeSearchDraft providerDraft,
        UniqueItemResolutionResult? uniqueResolution)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(baseResolution);
        ArgumentNullException.ThrowIfNull(providerDraft);

        var modifiers = providerDraft.ModifierFilters
            .Select(FromReplayComponent)
            .OrderBy(modifier => modifier.SourceModifierIndex)
            .ThenBy(modifier => modifier.SourceLineIndex)
            .ThenBy(modifier => modifier.OriginalText, StringComparer.Ordinal)
            .ToArray();

        var draftFilters = providerDraft.ModifierFilters
            .Select(component => new ModifierPipelineNormalizedDraftFilter
            {
                ComponentId = component.ComponentId,
                ProviderStatId = component.ProviderStatId,
                BoundShape = component.ValueBoundShape.ToString(),
                RequestedMinimum = FormatDecimal(component.RequestedMinimum),
                RequestedMaximum = FormatDecimal(component.RequestedMaximum),
                IsSearchable = component.IsSearchable,
            })
            .OrderBy(filter => filter.ComponentId, StringComparer.Ordinal)
            .ToArray();

        return new ModifierPipelineNormalizedItem
        {
            ItemClass = parsed.ItemClass ?? "-",
            Rarity = parsed.Rarity ?? "-",
            DisplayName = parsed.DisplayName ?? "-",
            BaseType = parsed.BaseType ?? "-",
            BaseResolutionStatus = baseResolution.Status.ToString(),
            ResolvedBaseName = baseResolution.ResolvedBaseName,
            UniqueCanonicalName = uniqueResolution?.Identity?.CanonicalName ??
                                  providerDraft.UniqueItemResolution?.Identity?.CanonicalName,
            UniqueCanonicalType =
                FirstBaseType(uniqueResolution?.Identity) ??
                FirstBaseType(providerDraft.UniqueItemResolution?.Identity),
            UniqueResolutionStatus = (uniqueResolution ?? providerDraft.UniqueItemResolution)?.Status.ToString(),
            UniqueResolutionDiagnosticCode =
                (uniqueResolution ?? providerDraft.UniqueItemResolution)?.DiagnosticCode,
            CompatibleVersionRoles = Sort(
                (uniqueResolution ?? providerDraft.UniqueItemResolution)?.CompatibleVersions
                    ?.Select(version => version.Role.ToString())),
            Modifiers = modifiers,
            DraftFilters = draftFilters,
            FinalSerializedRequestAvailability =
                "unavailable-serialized-http-request; draft-filter-shape-compared-when-present",
        };
    }

    private static string? FirstBaseType(UniqueItemIdentity? identity) =>
        identity?.BaseTypeEvidence.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static ModifierPipelineNormalizedModifier FromCaptureModifier(
        ModifierPipelineReplayCaptureModifier modifier,
        ModifierPipelineReplayCaptureUniqueMechanicalResolution? mech)
    {
        var block = mech?.ModifierBlocks?
            .FirstOrDefault(candidate => candidate.ParsedModifierIndex == modifier.SourceModifierIndex);
        var originalText = modifier.Raw?.OriginalText ?? string.Empty;
        var modifierIds = Prefer(
            modifier.SourceResolution?.ResolvedModifierIds,
            string.IsNullOrWhiteSpace(modifier.SourceResolution?.ResolvedModifierId)
                ? null
                : [modifier.SourceResolution!.ResolvedModifierId!],
            block?.ModifierIds);
        var statIds = Prefer(modifier.SourceResolution?.ResolvedStatIds, block?.StatIds);
        var blockIds = Prefer(modifier.SourceResolution?.UniqueCatalogBlockIds, block?.CatalogBlockIds);
        var providerIds = BuildProviderIds(
            modifier.ProviderResolution?.ProviderStatId,
            modifier.ProviderResolution?.ProviderStatAlternativeIds);

        return new ModifierPipelineNormalizedModifier
        {
            RowKey = BuildRowKey(modifier.SourceModifierIndex, modifier.SourceLineIndex, originalText),
            ComponentId = modifier.ComponentId,
            SourceModifierIndex = modifier.SourceModifierIndex,
            SourceLineIndex = modifier.SourceLineIndex,
            ParsedKind = modifier.Raw?.ParsedKind ?? modifier.ResolvedSemantics?.ParsedKind,
            OriginalText = NormalizeText(originalText),
            ValueLines = Sort(modifier.Raw?.ValueLines?.Select(NormalizeText)),
            ObservedNumericValues = FormatDecimals(modifier.Raw?.ObservedNumericValues),
            CoreStatus = modifier.SourceResolution?.Status,
            SourceDiagnosticCode = modifier.SourceResolution?.DiagnosticCode,
            AggregateDiagnosticCode = modifier.SourceResolution?.AggregateDiagnosticCode,
            UniqueBlockDiagnosticCode =
                modifier.SourceResolution?.UniqueResolutionDiagnosticCode ?? block?.DiagnosticCode,
            ModifierIds = Sort(modifierIds),
            StatIds = Sort(statIds),
            BlockIds = Sort(blockIds),
            ResolvedSourceKind = modifier.ResolvedSemantics?.ResolvedSourceKind,
            HasExactProvenance = modifier.ResolvedSemantics?.HasExactUniqueSourceProvenance,
            IsBaseImplicit = modifier.ResolvedSemantics?.IsBaseImplicit,
            ProviderStatus = modifier.ProviderResolution?.ProviderResolutionStatus,
            ProviderStatIds = Sort(providerIds),
            ProviderDiagnosticCode = modifier.ProviderResolution?.ProviderDiagnosticCode,
            IsSearchable = modifier.Consumer?.IsSearchable,
            NotSearchableReasonFamily = Family(modifier.Consumer?.NotSearchableReason),
        };
    }

    private static ModifierPipelineNormalizedModifier FromReplayComponent(ResolvedSearchComponent component)
    {
        var originalText = component.OriginalText ?? string.Empty;
        var providerIds = BuildProviderIds(component.ProviderStatId, component.ProviderStatAlternativeIds);
        return new ModifierPipelineNormalizedModifier
        {
            RowKey = BuildRowKey(component.SourceModifierIndex, component.SourceLineIndex, originalText),
            ComponentId = component.ComponentId,
            SourceModifierIndex = component.SourceModifierIndex,
            SourceLineIndex = component.SourceLineIndex,
            ParsedKind = component.ParsedKind.ToString(),
            OriginalText = NormalizeText(originalText),
            ValueLines = Sort(
                string.IsNullOrWhiteSpace(originalText)
                    ? []
                    : NormalizeText(originalText).Split('\n', StringSplitOptions.None)),
            ObservedNumericValues = FormatDecimals(component.ObservedNumericValues),
            CoreStatus = component.ResolutionStatus.ToString(),
            SourceDiagnosticCode = null,
            AggregateDiagnosticCode = null,
            UniqueBlockDiagnosticCode = component.UniqueResolutionDiagnosticCode,
            ModifierIds = Sort(
                (string.IsNullOrWhiteSpace(component.ResolvedModifierId)
                    ? component.Sources.Select(source => source.ResolvedModifierId)
                    : [component.ResolvedModifierId])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)),
            StatIds = Sort(component.ResolvedStatIds),
            BlockIds = Sort(component.UniqueCatalogBlockIds),
            ResolvedSourceKind = component.ResolvedSourceKind.ToString(),
            HasExactProvenance = component.HasExactUniqueSourceProvenance,
            IsBaseImplicit = component.IsBaseImplicit,
            ProviderStatus = component.ProviderResolutionStatus.ToString(),
            ProviderStatIds = Sort(providerIds),
            ProviderDiagnosticCode = component.ProviderDiagnosticCode,
            IsSearchable = component.IsSearchable,
            NotSearchableReasonFamily = Family(component.NotSearchableReason),
        };
    }

    public static string BuildRowKey(int sourceModifierIndex, int sourceLineIndex, string originalText) =>
        $"{sourceModifierIndex}|{sourceLineIndex}|{NormalizeText(originalText)}";

    public static string NormalizeText(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd();

    private static IReadOnlyList<string> Prefer(params IEnumerable<string>?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is null)
            {
                continue;
            }

            var materialized = candidate
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray();
            if (materialized.Length > 0)
            {
                return materialized;
            }
        }

        return [];
    }

    private static IReadOnlyList<string> BuildProviderIds(
        string? primary,
        IEnumerable<string>? alternatives)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(primary))
        {
            values.Add(primary.Trim());
        }

        if (alternatives is not null)
        {
            values.AddRange(alternatives.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        }

        return values;
    }

    private static IReadOnlyList<string> Sort(IEnumerable<string>? values) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> FormatDecimals(IEnumerable<decimal>? values) =>
        (values ?? [])
            .Select(FormatDecimalRequired)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string FormatDecimalRequired(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string? FormatDecimal(decimal? value) =>
        value is null ? null : FormatDecimalRequired(value.Value);

    private static string? Family(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();
        var separator = trimmed.IndexOf(':', StringComparison.Ordinal);
        return separator <= 0 ? trimmed : trimmed[..separator];
    }
}
