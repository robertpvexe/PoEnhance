namespace PoEnhance.Core.Items.GameData;

public sealed record ModifierEligibilityResult(
    bool Evaluated,
    ModifierEligibilityOutcome Outcome,
    string ReasonCode,
    string Reason,
    string? MatchedTag = null,
    string? ModifierDomain = null,
    string? ItemBaseDomain = null);
