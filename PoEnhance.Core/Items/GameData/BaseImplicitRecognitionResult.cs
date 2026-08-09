namespace PoEnhance.Core.Items.GameData;

public sealed record BaseImplicitRecognitionResult(
    BaseImplicitRecognitionStatus Status,
    IReadOnlyList<BaseImplicitRecognitionMatch> Matches,
    string DiagnosticCode,
    string Diagnostic)
{
    public static BaseImplicitRecognitionResult Unknown(string code, string diagnostic) =>
        new(BaseImplicitRecognitionStatus.Unknown, [], code, diagnostic);
}
