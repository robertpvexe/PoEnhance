namespace PoEnhance.Core.Items.GameData;

public static class ModifierCandidateResolutionDiagnosticCodes
{
    public const string ModifierExactMatch = "MODIFIER_EXACT_MATCH";
    public const string ModifierExactEligibleMatch = "MODIFIER_EXACT_ELIGIBLE_MATCH";
    public const string ModifierNotFound = "MODIFIER_NOT_FOUND";
    public const string ModifierAmbiguous = "MODIFIER_AMBIGUOUS";
    public const string ModifierEligibilityAmbiguous = "MODIFIER_ELIGIBILITY_AMBIGUOUS";
    public const string ModifierNoEligibleCandidates = "MODIFIER_NO_ELIGIBLE_CANDIDATES";
    public const string ModifierEligibilityNotEvaluated = "MODIFIER_ELIGIBILITY_NOT_EVALUATED";
    public const string ModifierNameNotAvailable = "MODIFIER_NAME_NOT_AVAILABLE";
    public const string ModifierKindUnsupported = "MODIFIER_KIND_UNSUPPORTED";
}
