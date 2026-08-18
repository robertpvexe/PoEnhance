namespace PoEnhance.GameData;

/// <summary>
/// Describes whether a source block is part of one fixed Unique definition or one possible
/// member of a source-proven generated candidate pool. The copied item remains authoritative
/// for which generated candidates are present.
/// </summary>
public enum UniqueModifierSourceSemantics
{
    Fixed,
    GeneratedCandidate,
}
