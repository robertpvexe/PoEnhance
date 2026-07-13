using System.IO;
using System.Text.Json;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;
using Serilog;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed class ProvisionalGameDataRecordingService
{
    private const string DiscoverySource = "local-parser";
    private const string MissingCatalogConfidence = "unknown-missing-catalog";
    private readonly IProvisionalGameDataStore store;
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly HashSet<string> recordedEventKeys = [];

    public ProvisionalGameDataRecordingService(IProvisionalGameDataStore store)
        : this(store, () => DateTimeOffset.UtcNow)
    {
    }

    public ProvisionalGameDataRecordingService(
        IProvisionalGameDataStore store,
        Func<DateTimeOffset> getUtcNow)
    {
        this.store = store;
        this.getUtcNow = getUtcNow;
    }

    public ProvisionalGameDataStoreStatus Status => store.Status;

    public Task<ProvisionalGameDataStoreResult> LoadSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        return store.LoadSnapshotAsync(cancellationToken);
    }

    public async Task<ProvisionalGameDataRecordingResult> RecordAsync(
        ParsedItem parsedItem,
        RuntimeGameDataStatus gameDataStatus,
        ItemBaseResolutionResult itemBaseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResults,
        string processingEventKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);
        ArgumentNullException.ThrowIfNull(gameDataStatus);
        ArgumentNullException.ThrowIfNull(itemBaseResolution);
        ArgumentNullException.ThrowIfNull(modifierResults);
        ArgumentException.ThrowIfNullOrWhiteSpace(processingEventKey);

        if (gameDataStatus.Catalog is null || gameDataStatus.Package is null)
        {
            return NoDiscovery("Game data unavailable; provisional recording skipped.");
        }

        var now = getUtcNow();
        var records = DiscoverRecords(parsedItem, gameDataStatus.Package, itemBaseResolution, modifierResults, now);
        if (records.Count == 0)
        {
            return NoDiscovery("No high-confidence provisional records discovered.");
        }

        var eventKeys = records
            .Select(record => $"{processingEventKey}|{record.StableKey}")
            .ToArray();
        if (eventKeys.All(recordedEventKeys.Contains))
        {
            return new ProvisionalGameDataRecordingResult
            {
                IsSuccess = true,
                DiscoveredRecordCount = 0,
                StoreStatus = store.Status,
                Diagnostic = "Provisional records were already recorded for this item-processing event.",
            };
        }

        foreach (var eventKey in eventKeys)
        {
            recordedEventKeys.Add(eventKey);
        }

        ProvisionalGameDataStoreResult result;
        try
        {
            result = await store.UpsertAsync(records, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(exception, "Unexpected provisional game-data store failure");
            return new ProvisionalGameDataRecordingResult
            {
                IsSuccess = false,
                DiscoveredRecordCount = records.Count,
                StoreStatus = store.Status,
                Diagnostic = "Provisional store failure was isolated from item processing.",
            };
        }

        return new ProvisionalGameDataRecordingResult
        {
            IsSuccess = result.IsSuccess,
            DiscoveredRecordCount = records.Count,
            StoreStatus = store.Status,
            Diagnostic = result.Diagnostic,
        };
    }

    private IReadOnlyList<ProvisionalGameDataRecord> DiscoverRecords(
        ParsedItem parsedItem,
        GameDataPackage package,
        ItemBaseResolutionResult itemBaseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResults,
        DateTimeOffset now)
    {
        var records = new List<ProvisionalGameDataRecord>();
        var league = package.Manifest.League;
        var patch = package.Manifest.Patch;

        if (ShouldRecordMissingItemBase(parsedItem, itemBaseResolution, out var normalizedBaseType))
        {
            records.Add(new ProvisionalGameDataRecord
            {
                StableKey = $"item-base|{NormalizeKeyPart(parsedItem.ItemClass)}|{NormalizeKeyPart(normalizedBaseType)}",
                Kind = ProvisionalGameDataRecordKind.ItemBase,
                NormalizedIdentity = normalizedBaseType,
                OriginalIdentity = parsedItem.BaseType!.Trim(),
                ItemClass = TrimToNull(parsedItem.ItemClass),
                FirstSeenUtc = now,
                LastSeenUtc = now,
                SeenCount = 1,
                Source = DiscoverySource,
                League = TrimToNull(league),
                Patch = TrimToNull(patch),
                Confidence = MissingCatalogConfidence,
                DiscoveryContext = "Parsed base type was present and no catalog item base matched it.",
            });
        }

        records.AddRange(modifierResults
            .Where(ShouldRecordMissingModifier)
            .Select(result => CreateModifierRecord(parsedItem, package, result, now)));

        return records
            .GroupBy(record => record.StableKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static ProvisionalGameDataRecord CreateModifierRecord(
        ParsedItem parsedItem,
        GameDataPackage package,
        ModifierCandidateResolutionResult result,
        DateTimeOffset now)
    {
        var normalizedName = NormalizeIdentity(result.ParsedModifierName);
        return new ProvisionalGameDataRecord
        {
            StableKey = $"modifier|{NormalizeKeyPart(result.GenerationType?.ToString())}|{NormalizeKeyPart(normalizedName)}",
            Kind = ProvisionalGameDataRecordKind.Modifier,
            NormalizedIdentity = normalizedName!,
            OriginalIdentity = result.ParsedModifierName!.Trim(),
            ItemClass = TrimToNull(parsedItem.ItemClass),
            ModifierKind = result.ParsedModifierKind.ToString(),
            ModifierGenerationType = result.GenerationType,
            FirstSeenUtc = now,
            LastSeenUtc = now,
            SeenCount = 1,
            Source = DiscoverySource,
            League = TrimToNull(package.Manifest.League),
            Patch = TrimToNull(package.Manifest.Patch),
            Confidence = MissingCatalogConfidence,
            DiscoveryContext = "Authentic Advanced modifier name had zero catalog name candidates.",
        };
    }

    private static bool ShouldRecordMissingItemBase(
        ParsedItem parsedItem,
        ItemBaseResolutionResult itemBaseResolution,
        out string normalizedBaseType)
    {
        normalizedBaseType = NormalizeIdentity(parsedItem.BaseType) ?? string.Empty;
        return normalizedBaseType.Length > 0
            && itemBaseResolution.Status == ItemBaseResolutionStatus.Unknown
            && itemBaseResolution.Candidates.Count == 0
            && itemBaseResolution.Diagnostics.Any(diagnostic =>
                diagnostic.Code == ItemBaseResolutionDiagnosticCodes.BaseNotFound);
    }

    private static bool ShouldRecordMissingModifier(ModifierCandidateResolutionResult result)
    {
        return result.Status == ModifierCandidateResolutionStatus.Unknown
            && result.GenerationType is not null
            && result.ParsedModifier.RawMetadataLine is not null
            && !string.IsNullOrWhiteSpace(result.ParsedModifierName)
            && result.NameCandidateCount == 0
            && result.GenerationKindCandidateCount == 0
            && result.Candidates.Count == 0
            && result.Diagnostics.Any(diagnostic =>
                diagnostic.Code == ModifierCandidateResolutionDiagnosticCodes.ModifierNotFound);
    }

    private ProvisionalGameDataRecordingResult NoDiscovery(string diagnostic)
    {
        return new ProvisionalGameDataRecordingResult
        {
            IsSuccess = true,
            StoreStatus = store.Status,
            Diagnostic = diagnostic,
        };
    }

    private static string? NormalizeIdentity(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return string.Join(
            " ",
            trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }

    private static string NormalizeKeyPart(string? value)
    {
        return NormalizeIdentity(value) ?? "none";
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
