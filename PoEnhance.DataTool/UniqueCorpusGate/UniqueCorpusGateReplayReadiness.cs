namespace PoEnhance.DataTool.UniqueCorpusGate;

public static class UniqueCorpusGateReplayReadiness
{
    public const string ReplayReady = "ReplayReady";

    public const string AuditOnly = "AuditOnly";

    public const string KnownReplaySchemaVersion = "A.5.3-replay-1";

    public static string Classify(UniqueCorpusGateCaptureReplayContext? replayContext)
    {
        if (replayContext is null)
        {
            return AuditOnly;
        }

        var schema = replayContext.CaptureSchemaVersion?.Trim();
        if (string.IsNullOrWhiteSpace(schema))
        {
            return AuditOnly;
        }

        if (!string.Equals(schema, KnownReplaySchemaVersion, StringComparison.Ordinal))
        {
            // Unknown/newer schema: fail replay-readiness safely; keep capture auditable.
            return AuditOnly;
        }

        if (string.IsNullOrEmpty(replayContext.RawClipboardText))
        {
            return AuditOnly;
        }

        if (string.IsNullOrWhiteSpace(replayContext.GameDataVersion) ||
            string.IsNullOrWhiteSpace(replayContext.GameDataSha256))
        {
            return AuditOnly;
        }

        return ReplayReady;
    }

    public static bool HasRawClipboard(UniqueCorpusGateCaptureReplayContext? replayContext) =>
        !string.IsNullOrEmpty(replayContext?.RawClipboardText);

    public static bool HasGameDataIdentity(UniqueCorpusGateCaptureReplayContext? replayContext) =>
        !string.IsNullOrWhiteSpace(replayContext?.GameDataVersion) &&
        !string.IsNullOrWhiteSpace(replayContext?.GameDataSha256);
}
