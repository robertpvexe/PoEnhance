namespace PoEnhance.GameData;

public enum StatTranslationCompatibilityClassification
{
    Unresolved = 0,
    MechanicallyEquivalentRendering,
    EquivalentWithCanonicalizationChange,
    NumericShapeChanged,
    MechanicsChanged,
    SpecialOnlyUnsupported,
    NoRuntimeImpact,
}
