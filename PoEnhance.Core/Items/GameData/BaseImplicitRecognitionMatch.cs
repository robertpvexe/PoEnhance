using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed record BaseImplicitRecognitionMatch(
    BaseImplicitObservation Observation,
    BaseImplicitMechanicalEffect Effect,
    BaseImplicitSourceSnapshot SourceSnapshot);
