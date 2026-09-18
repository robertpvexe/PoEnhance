using System.Security.Cryptography;
using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal static class ModifierPipelineReplayGameDataGate
{
    public static async Task<ModifierPipelineReplayGameDataIdentity> LoadIdentityAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("GameData package was not found for replay.", packagePath);
        }

        var sha256 = await ComputeFileSha256Async(packagePath, cancellationToken).ConfigureAwait(false);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath, cancellationToken)
            .ConfigureAwait(false);
        if (!load.IsSuccess || load.Package is null)
        {
            var detail = string.Join(
                " | ",
                load.Diagnostics.Select(diagnostic => $"{diagnostic.Code}:{diagnostic.Message}"));
            throw new InvalidOperationException(
                $"GameData package failed to load for replay: {detail}");
        }

        return new ModifierPipelineReplayGameDataIdentity(
            Path.GetFullPath(packagePath),
            load.Package.Manifest.DataVersion ?? string.Empty,
            sha256,
            GameDataCatalog.FromPackage(load.Package));
    }

    public static ModifierPipelineReplayGateDecision Evaluate(
        ModifierPipelineReplayCaptureDocument capture,
        ModifierPipelineReplayGameDataIdentity supplied)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(supplied);

        if (!IsReplayReady(capture, out var refusalReason))
        {
            return ModifierPipelineReplayGateDecision.Refuse(
                ModifierPipelineReplayDivergenceClass.AuditOnlyOrSchemaRefused,
                refusalReason);
        }

        var capturedVersion = capture.ReplayContext!.GameDataVersion!.Trim();
        var capturedSha = capture.ReplayContext.GameDataSha256!.Trim().ToLowerInvariant();
        var suppliedVersion = supplied.DataVersion.Trim();
        var suppliedSha = supplied.Sha256.Trim().ToLowerInvariant();

        if (!string.Equals(capturedSha, suppliedSha, StringComparison.Ordinal))
        {
            return ModifierPipelineReplayGateDecision.Refuse(
                ModifierPipelineReplayDivergenceClass.GameDataMismatch,
                $"GameData SHA mismatch: capture={capturedSha}; supplied={suppliedSha}");
        }

        if (!string.Equals(capturedVersion, suppliedVersion, StringComparison.Ordinal))
        {
            return ModifierPipelineReplayGateDecision.Refuse(
                ModifierPipelineReplayDivergenceClass.GameDataMismatch,
                $"GameData version mismatch: capture={capturedVersion}; supplied={suppliedVersion}");
        }

        return ModifierPipelineReplayGateDecision.Allow();
    }

    public static bool IsReplayReady(
        ModifierPipelineReplayCaptureDocument capture,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var replay = capture.ReplayContext;
        if (replay is null)
        {
            reason = "Missing replayContext.";
            return false;
        }

        var schema = replay.CaptureSchemaVersion?.Trim();
        if (!string.Equals(schema, ModifierPipelineReplaySchemas.KnownReplaySchemaVersion, StringComparison.Ordinal))
        {
            reason = string.IsNullOrWhiteSpace(schema)
                ? "Missing captureSchemaVersion."
                : $"Unknown/unsupported replay schema '{schema}'.";
            return false;
        }

        if (string.IsNullOrEmpty(replay.RawClipboardText))
        {
            reason = "Missing rawClipboardText.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(replay.GameDataVersion) ||
            string.IsNullOrWhiteSpace(replay.GameDataSha256))
        {
            reason = "Missing GameData version and/or SHA.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static async Task<string> ComputeFileSha256Async(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 64,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

internal sealed record ModifierPipelineReplayGameDataIdentity(
    string PackagePath,
    string DataVersion,
    string Sha256,
    GameDataCatalog Catalog);

internal sealed record ModifierPipelineReplayGateDecision(
    bool CanReplay,
    string DivergenceClass,
    string Reason)
{
    public static ModifierPipelineReplayGateDecision Allow() =>
        new(true, ModifierPipelineReplayDivergenceClass.ExactMatch, string.Empty);

    public static ModifierPipelineReplayGateDecision Refuse(string divergenceClass, string reason) =>
        new(false, divergenceClass, reason);
}

internal static class ModifierPipelineReplaySchemas
{
    public const string KnownReplaySchemaVersion = "A.5.3-replay-1";

    public const string ReportSchema = "poenhance.modifier-pipeline-replay.v1";
}

internal static class ModifierPipelineReplayDivergenceClass
{
    public const string ExactMatch = "EXACT_MATCH";
    public const string InputEquivalentOutputDivergence = "INPUT_EQUIVALENT_OUTPUT_DIVERGENCE";
    public const string CaptureFieldUnavailable = "CAPTURE_FIELD_UNAVAILABLE";
    public const string ProviderContextUnverified = "PROVIDER_CONTEXT_UNVERIFIED";
    public const string GameDataMismatch = "GAMEDATA_MISMATCH";
    public const string ReplayError = "REPLAY_ERROR";
    public const string AuditOnlyOrSchemaRefused = "AUDIT_ONLY_OR_SCHEMA_REFUSED";
}
