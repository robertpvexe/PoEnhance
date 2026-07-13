namespace PoEnhance.Core.Items.GameData;

public sealed record ModifierTextSignatureMatchResult(
    bool Evaluated,
    ModifierTextSignatureMatchOutcome Outcome,
    string ReasonCode,
    string Reason,
    IReadOnlyList<ModifierTextSignature> CandidateSignatures,
    IReadOnlyList<ModifierTextSignature> ParsedSignatures);
