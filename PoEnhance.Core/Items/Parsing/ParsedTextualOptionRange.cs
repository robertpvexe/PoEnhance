namespace PoEnhance.Core.Items.Parsing;

/// <summary>
/// A syntactically separated Advanced Copy textual option-range annotation. The parser does
/// not treat this candidate as semantic until an exact generated source candidate proves it.
/// </summary>
public sealed record ParsedTextualOptionRange(string Text);
